using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NxtHotspot.Application.Common;
using NxtHotspot.Domain.Entities;

namespace NxtHotspot.API.Security;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "NxtHotspot";
    public string Audience { get; set; } = "NxtHotspot.Admin";
    public int AccessTokenMinutes { get; set; } = 30;
    /// <summary>At least 32 characters. Supply via environment variable Jwt__Key or user-secrets.</summary>
    public string Key { get; set; } = "";
}

public sealed class JwtTokenService : ITokenService
{
    private readonly JwtOptions _options;
    public JwtTokenService(IOptions<JwtOptions> options) => _options = options.Value;

    public TokenResult Create(AdminUser user, IReadOnlyCollection<string> roles, IReadOnlyCollection<string> permissions)
    {
        var claims = new List<Claim>
        {
            new("sub", user.Id.ToString()),
            new("name", user.Username),
            new("all_sites", user.HasAllSitesAccess ? "true" : "false"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };
        if (user.OrganizationId is { } orgId) claims.Add(new Claim("org_id", orgId.ToString()));
        claims.AddRange(roles.Select(r => new Claim("role", r)));
        claims.AddRange(permissions.Select(p => new Claim("perm", p)));

        var expires = DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new TokenResult(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
