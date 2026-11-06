using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NxtHotspot.API.Security;
using NxtHotspot.Application.Common;
using NxtHotspot.Domain.Entities;
using NxtHotspot.Infrastructure.Security;

namespace NxtHotspot.Tests;

public class SecretProtectorTests
{
    private static string NewKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private static AesGcmSecretProtector Make(string key) => new(Options.Create(new SecurityOptions { EncryptionKey = key }));

    [Fact]
    public void RoundTrip_returns_original()
    {
        var p = Make(NewKey());
        Assert.Equal("api-key-12345", p.Unprotect(p.Protect("api-key-12345")));
    }

    [Fact]
    public void Same_plaintext_gives_different_ciphertext_and_hides_plaintext()
    {
        var p = Make(NewKey());
        var a = p.Protect("secret-value");
        var b = p.Protect("secret-value");
        Assert.NotEqual(a, b);
        Assert.DoesNotContain("secret-value", a);
    }

    [Fact]
    public void Tampered_ciphertext_is_rejected()
    {
        var p = Make(NewKey());
        var bytes = Convert.FromBase64String(p.Protect("secret-value"));
        bytes[^1] ^= 0xFF;
        Assert.ThrowsAny<CryptographicException>(() => p.Unprotect(Convert.ToBase64String(bytes)));
    }

    [Fact]
    public void Different_key_cannot_decrypt()
    {
        var protectedValue = Make(NewKey()).Protect("secret-value");
        Assert.ThrowsAny<CryptographicException>(() => Make(NewKey()).Unprotect(protectedValue));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-base64!!")]
    public void Invalid_key_configuration_fails_fast(string key) =>
        Assert.Throws<InvalidOperationException>(() => Make(key));

    [Fact]
    public void Key_of_wrong_length_fails_fast() =>
        Assert.Throws<InvalidOperationException>(() => Make(Convert.ToBase64String(new byte[16])));
}

public class PasswordTests
{
    [Theory]
    [InlineData("short1A", false)]
    [InlineData("alllowercase123", false)]
    [InlineData("ALLUPPERCASE123", false)]
    [InlineData("NoDigitsHereAtAll", false)]
    [InlineData("Str0ngPassw0rd!", true)]
    public void Policy_enforces_length_and_character_classes(string password, bool valid) =>
        Assert.Equal(valid, PasswordPolicy.Validate(password) == null);

    [Fact]
    public void Hash_is_salted_and_verifies()
    {
        var svc = new PasswordService();
        var h1 = svc.Hash("Str0ngPassw0rd!");
        var h2 = svc.Hash("Str0ngPassw0rd!");
        Assert.NotEqual(h1, h2);
        Assert.NotEqual("Str0ngPassw0rd!", h1);
        Assert.True(svc.Verify(h1, "Str0ngPassw0rd!"));
        Assert.False(svc.Verify(h1, "WrongPassw0rd!"));
    }
}

public class JwtAndCurrentUserTests
{
    [Fact]
    public void Token_carries_identity_tenant_and_permissions()
    {
        var svc = new JwtTokenService(Options.Create(new JwtOptions { Key = new string('k', 40) }));
        var user = new AdminUser { OrganizationId = Guid.NewGuid(), Username = "alice", HasAllSitesAccess = false };

        var result = svc.Create(user, new[] { "OrgAdmin" }, new[] { "sites.read", "sites.write" });
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.AccessToken);

        Assert.Equal(user.Id.ToString(), jwt.Subject);
        Assert.Equal(user.OrganizationId.ToString(), jwt.Claims.Single(c => c.Type == "org_id").Value);
        Assert.Equal("false", jwt.Claims.Single(c => c.Type == "all_sites").Value);
        Assert.Equal(new[] { "sites.read", "sites.write" }, jwt.Claims.Where(c => c.Type == "perm").Select(c => c.Value).OrderBy(x => x));
        Assert.True(result.ExpiresAtUtc > DateTime.UtcNow);
    }

    [Fact]
    public void Platform_token_has_no_org_claim()
    {
        var svc = new JwtTokenService(Options.Create(new JwtOptions { Key = new string('k', 40) }));
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(
            svc.Create(new AdminUser { Username = "root", HasAllSitesAccess = true }, new[] { "SuperAdmin" }, new[] { "organizations.write" }).AccessToken);
        Assert.DoesNotContain(jwt.Claims, c => c.Type == "org_id");
    }

    private static HttpContextCurrentUser UserWith(params Claim[] claims)
    {
        var ctx = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        return new HttpContextCurrentUser(new HttpContextAccessor { HttpContext = ctx });
    }

    [Fact]
    public void Current_user_for_org_admin_is_not_platform()
    {
        var org = Guid.NewGuid();
        var u = UserWith(new Claim("sub", Guid.NewGuid().ToString()), new Claim("org_id", org.ToString()),
            new Claim("all_sites", "true"), new Claim("perm", "sites.read"));
        Assert.False(u.IsPlatformAdmin);
        Assert.Equal(org, u.OrganizationId);
        Assert.True(u.HasAllSites);
        Assert.Contains("sites.read", u.Permissions);
    }

    [Fact]
    public void Authenticated_user_without_org_is_platform()
    {
        var u = UserWith(new Claim("sub", Guid.NewGuid().ToString()));
        Assert.True(u.IsPlatformAdmin);
        Assert.Null(u.OrganizationId);
    }

    [Fact]
    public void Unauthenticated_caller_is_never_platform()
    {
        var ctx = new DefaultHttpContext();
        var u = new HttpContextCurrentUser(new HttpContextAccessor { HttpContext = ctx });
        Assert.False(u.IsPlatformAdmin);
    }
}
