using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NxtHotspot.Application.Common;
using NxtHotspot.Application.Dtos;
using NxtHotspot.Domain.Entities;
using NxtHotspot.Domain.Enums;
using NxtHotspot.Infrastructure.Persistence;
using NxtHotspot.Infrastructure.Services;
using NxtHotspot.Infrastructure.Sms;
using Xunit;

namespace NxtHotspot.Tests;

// NOTE: written but not yet executed (no .NET SDK in the authoring environment). Run `dotnet test` on Windows.
public class ConfigTests
{
    private static OrganizationConfigService Config(TestEnv env, NxtDbContext db, ICurrentUser user) =>
        new(db, user, new TenantAccess(db, user), env.Protector, Options.Create(new SmsOptions()));

    private static UpsertSmsConfigRequest Sms(string? key = "key-1234567890") => new()
    {
        ApiKey = key, SenderId = "NXTWFI", MessageTemplate = "Your OTP is {OTP}"
    };

    private static async Task<Guid> AddSiteAsync(TestEnv env, Guid orgId, string code)
    {
        using var db = env.NewDb();
        var s = new Site { OrganizationId = orgId, Name = code, Code = code };
        db.Sites.Add(s);
        await db.SaveChangesAsync();
        return s.Id;
    }

    [Fact]
    public async Task SmsKey_IsEncrypted_AndNeverReturned()
    {
        var env = await TestEnv.CreateAsync();
        var org = await env.RegisterAsync("Hotel", "Grand", "grand.admin");
        using var db = env.NewDb();
        var dto = await Config(env, db, FakeCurrentUser.OrgAdmin(org.Id)).UpsertSmsAsync(org.Id, null, Sms());

        Assert.True(dto.HasApiKey);
        using var db2 = env.NewDb();
        var row = await db2.OrganizationSmsSettings.SingleAsync(s => s.OrganizationId == org.Id && s.SiteId == null);
        Assert.DoesNotContain("key-1234567890", row.ApiKeyEncrypted);
        Assert.Equal("key-1234567890", env.Protector.Unprotect(row.ApiKeyEncrypted));
        Assert.DoesNotContain(await db2.AuditLogs.Select(a => a.Details ?? "").ToListAsync(), d => d.Contains("key-1234567890"));
    }

    [Fact]
    public async Task SmsUpdate_WithoutKey_KeepsStoredKey()
    {
        var env = await TestEnv.CreateAsync();
        var org = await env.RegisterAsync("Hotel", "Grand", "grand.admin");
        var admin = FakeCurrentUser.OrgAdmin(org.Id);
        using (var db = env.NewDb()) await Config(env, db, admin).UpsertSmsAsync(org.Id, null, Sms());
        using (var db = env.NewDb())
        {
            var r = Sms(null); r.SenderId = "NEWSID";
            await Config(env, db, admin).UpsertSmsAsync(org.Id, null, r);
        }
        using var db3 = env.NewDb();
        var row = await db3.OrganizationSmsSettings.SingleAsync(s => s.OrganizationId == org.Id && s.SiteId == null);
        Assert.Equal("NEWSID", row.SenderId);
        Assert.Equal("key-1234567890", env.Protector.Unprotect(row.ApiKeyEncrypted));
    }

    [Fact]
    public async Task Sms_RejectsNonAllowListedHost_NonHttps_AndMissingOtpPlaceholder()
    {
        var env = await TestEnv.CreateAsync();
        var org = await env.RegisterAsync("Hotel", "Grand", "grand.admin");
        var admin = FakeCurrentUser.OrgAdmin(org.Id);

        var evil = Sms(); evil.ApiEndpoint = "https://evil.example.com/API/R1/";
        var http = Sms(); http.ApiEndpoint = "http://2factor.in/API/R1/";
        var noOtp = Sms(); noOtp.MessageTemplate = "Hello";
        foreach (var bad in new[] { evil, http, noOtp })
        {
            using var db = env.NewDb();
            await Assert.ThrowsAsync<ValidationFailedException>(() => Config(env, db, admin).UpsertSmsAsync(org.Id, null, bad));
        }
    }

    [Fact]
    public async Task OrgAdmin_CannotTouchAnotherOrganizationsConfig()
    {
        var env = await TestEnv.CreateAsync();
        var a = await env.RegisterAsync("Hotel", "Org A", "a.admin");
        var b = await env.RegisterAsync("School", "Org B", "b.admin");
        using var db = env.NewDb();
        var svc = Config(env, db, FakeCurrentUser.OrgAdmin(a.Id));

        await Assert.ThrowsAsync<ForbiddenException>(() => svc.GetSettingsAsync(b.Id));
        await Assert.ThrowsAsync<ForbiddenException>(() => svc.UpsertSmsAsync(b.Id, null, Sms()));
        await Assert.ThrowsAsync<ForbiddenException>(() => svc.ListLoginMethodsAsync(b.Id, null, false));
    }

    [Fact]
    public async Task SiteRestrictedAdmin_CannotChangeOrganizationWideConfig()
    {
        var env = await TestEnv.CreateAsync();
        var org = await env.RegisterAsync("Hotel", "Grand", "grand.admin");
        using var db = env.NewDb();
        var limited = FakeCurrentUser.Limited(org.Id, null, Permissions.SettingsRead, Permissions.SettingsWrite);
        await Assert.ThrowsAsync<ForbiddenException>(() => Config(env, db, limited).UpdateSettingsAsync(org.Id, new UpdateOrganizationSettingsRequest()));
        await Assert.ThrowsAsync<ForbiddenException>(() => Config(env, db, limited).UpsertSmsAsync(org.Id, null, Sms()));
    }

    [Fact]
    public async Task SiteOverride_ForSiteOfAnotherOrg_IsNotFound()
    {
        var env = await TestEnv.CreateAsync();
        var a = await env.RegisterAsync("Hotel", "Org A", "a.admin");
        var b = await env.RegisterAsync("School", "Org B", "b.admin");
        var bSite = await AddSiteAsync(env, b.Id, "BR2");
        using var db = env.NewDb();
        await Assert.ThrowsAsync<NotFoundException>(() => Config(env, db, FakeCurrentUser.OrgAdmin(a.Id)).UpsertSmsAsync(a.Id, bSite, Sms()));
    }

    [Fact]
    public async Task LoginMethods_OnlyOneDefault_AndLastEnabledCannotBeRemoved()
    {
        var env = await TestEnv.CreateAsync();
        var org = await env.RegisterAsync("Hotel", "Grand", "grand.admin", methods: new() { AuthenticationType.MobileOTP });
        var admin = FakeCurrentUser.OrgAdmin(org.Id);

        using (var db = env.NewDb())
        {
            var list = await Config(env, db, admin).UpsertLoginMethodAsync(org.Id, null,
                new UpsertLoginMethodRequest { AuthenticationType = AuthenticationType.Voucher, IsDefault = true });
            Assert.Single(list, m => m.IsDefault);
            Assert.Equal(AuthenticationType.Voucher, list.Single(m => m.IsDefault).AuthenticationType);
        }

        using (var db = env.NewDb())
        {
            var svc = Config(env, db, admin);
            var all = await svc.ListLoginMethodsAsync(org.Id, null, false);
            // Disable one: allowed. Disable the other too: refused.
            await svc.UpsertLoginMethodAsync(org.Id, null, new UpsertLoginMethodRequest { AuthenticationType = AuthenticationType.MobileOTP, IsEnabled = false });
            await Assert.ThrowsAsync<ConflictException>(() => svc.UpsertLoginMethodAsync(org.Id, null,
                new UpsertLoginMethodRequest { AuthenticationType = AuthenticationType.Voucher, IsEnabled = false }));
            Assert.Equal(2, all.Count);
        }
    }

    [Fact]
    public async Task EffectiveLoginMethods_FallBackToOrganization_UntilSiteOverrides()
    {
        var env = await TestEnv.CreateAsync();
        var org = await env.RegisterAsync("Hotel", "Grand", "grand.admin", methods: new() { AuthenticationType.MobileOTP });
        var site = await AddSiteAsync(env, org.Id, "BR2");
        var admin = FakeCurrentUser.OrgAdmin(org.Id);

        using var db = env.NewDb();
        var svc = Config(env, db, admin);
        var before = await svc.ListLoginMethodsAsync(org.Id, site, true);
        Assert.Equal(AuthenticationType.MobileOTP, Assert.Single(before).AuthenticationType);

        await svc.UpsertLoginMethodAsync(org.Id, site, new UpsertLoginMethodRequest { AuthenticationType = AuthenticationType.Voucher, IsDefault = true });
        var after = await svc.ListLoginMethodsAsync(org.Id, site, true);
        Assert.Equal(AuthenticationType.Voucher, Assert.Single(after).AuthenticationType);
    }

    [Fact]
    public async Task Branding_RejectsNonHttpsUrls()
    {
        var env = await TestEnv.CreateAsync();
        var org = await env.RegisterAsync("Hotel", "Grand", "grand.admin");
        using var db = env.NewDb();
        var svc = Config(env, db, FakeCurrentUser.OrgAdmin(org.Id));
        await Assert.ThrowsAsync<ValidationFailedException>(() =>
            svc.UpdateBrandingAsync(org.Id, new UpdateOrganizationBrandingRequest { LogoUrl = "javascript:alert(1)" }));
        var ok = await svc.UpdateBrandingAsync(org.Id, new UpdateOrganizationBrandingRequest { LogoUrl = "https://cdn.example.com/logo.png" });
        Assert.Equal("https://cdn.example.com/logo.png", ok.LogoUrl);
    }

    [Fact]
    public async Task OrganizationTypes_PlatformOnly_AndValidated()
    {
        var env = await TestEnv.CreateAsync();
        var org = await env.RegisterAsync("Hotel", "Grand", "grand.admin");
        var req = new UpsertOrganizationTypeRequest
        {
            TypeName = "Gym",
            DefaultLoginMethods = new() { new OrganizationTypeLoginMethodInput { AuthenticationType = AuthenticationType.MobileOTP, IsDefault = true, RequireOTP = true } }
        };

        using (var db = env.NewDb())
            await Assert.ThrowsAsync<ForbiddenException>(() => Config(env, db, FakeCurrentUser.OrgAdmin(org.Id)).CreateTypeAsync(req));

        using (var db = env.NewDb())
        {
            var svc = Config(env, db, FakeCurrentUser.Platform());
            var created = await svc.CreateTypeAsync(req);
            Assert.Equal("Gym", created.TypeName);
            await Assert.ThrowsAsync<ConflictException>(() => svc.CreateTypeAsync(req));
            req.DefaultLoginMethods[0].IsDefault = false;
            await Assert.ThrowsAsync<ValidationFailedException>(() => svc.UpdateTypeAsync(created.OrganizationTypeId, req));
        }
    }

    [Fact]
    public async Task AuditLog_IsScopedToCallersOrganization()
    {
        var env = await TestEnv.CreateAsync();
        var a = await env.RegisterAsync("Hotel", "Org A", "a.admin");
        var b = await env.RegisterAsync("School", "Org B", "b.admin");

        using var db = env.NewDb();
        var asA = new AuditLogService(db, FakeCurrentUser.OrgAdmin(a.Id));
        var page = await asA.ListAsync(null, null, null, null, 1, 50);
        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, i => Assert.Equal(a.Id, i.OrganizationId));
        await Assert.ThrowsAsync<ForbiddenException>(() => asA.ListAsync(b.Id, null, null, null, 1, 50));

        var asPlatform = new AuditLogService(db, FakeCurrentUser.Platform());
        var onlyB = await asPlatform.ListAsync(b.Id, null, null, null, 1, 50);
        Assert.All(onlyB.Items, i => Assert.Equal(b.Id, i.OrganizationId));
    }
}
