using Microsoft.EntityFrameworkCore;
using NxtHotspot.Application.Common;
using NxtHotspot.Application.Dtos;
using NxtHotspot.Domain.Entities;
using NxtHotspot.Domain.Enums;
using NxtHotspot.Infrastructure.Services;

namespace NxtHotspot.Tests;

public class AuthTests
{
    private static LoginRequest Login(string user, string pass) => new() { Username = user, Password = pass };

    [Fact]
    public async Task Org_admin_login_returns_roles_and_org_scoped_permissions()
    {
        var env = await TestEnv.CreateAsync();
        var org = await env.RegisterAsync("Hotel", "Hotel", "HotelAdmin");
        using var db = env.NewDb();

        var response = await env.Auth(db).LoginAsync(Login("hoteladmin", TestEnv.StrongPassword), "127.0.0.1"); // case-insensitive

        Assert.False(response.IsPlatformAdmin);
        Assert.Equal(org.Id, response.OrganizationId);
        Assert.Contains(SystemRoles.OrgAdmin, response.Roles);
        Assert.Contains(Permissions.SitesWrite, response.Permissions);
        Assert.DoesNotContain(Permissions.OrganizationsWrite, response.Permissions);
        Assert.DoesNotContain(Permissions.WalletCredit, response.Permissions);
        Assert.False(string.IsNullOrEmpty(response.AccessToken));
    }

    [Fact]
    public async Task Bootstrap_super_admin_can_log_in_with_platform_permissions()
    {
        var env = await TestEnv.CreateAsync("root", TestEnv.StrongPassword);
        using var db = env.NewDb();
        var response = await env.Auth(db).LoginAsync(Login("ROOT", TestEnv.StrongPassword), null);
        Assert.True(response.IsPlatformAdmin);
        Assert.Null(response.OrganizationId);
        Assert.Contains(Permissions.OrganizationsWrite, response.Permissions);
        Assert.Contains(Permissions.WalletCredit, response.Permissions);
    }

    [Fact]
    public async Task Wrong_password_and_unknown_user_give_the_same_message()
    {
        var env = await TestEnv.CreateAsync();
        await env.RegisterAsync("Hotel", "Hotel", "hoteladmin");
        using var db = env.NewDb();
        var svc = env.Auth(db);

        var wrong = await Assert.ThrowsAsync<UnauthorizedAppException>(() => svc.LoginAsync(Login("hoteladmin", "WrongPassw0rd!"), null));
        var unknown = await Assert.ThrowsAsync<UnauthorizedAppException>(() => svc.LoginAsync(Login("nobody", "WrongPassw0rd!"), null));
        Assert.Equal(wrong.Message, unknown.Message);
    }

    [Fact]
    public async Task Five_failures_lock_the_account_even_for_the_correct_password_and_passwords_are_never_audited()
    {
        var env = await TestEnv.CreateAsync();
        await env.RegisterAsync("Hotel", "Hotel", "hoteladmin");

        for (var i = 0; i < 5; i++)
        {
            using var db = env.NewDb();
            await Assert.ThrowsAsync<UnauthorizedAppException>(() => env.Auth(db).LoginAsync(Login("hoteladmin", "WrongPassw0rd!"), "10.0.0.1"));
        }

        using var db2 = env.NewDb();
        var locked = await Assert.ThrowsAsync<UnauthorizedAppException>(() => env.Auth(db2).LoginAsync(Login("hoteladmin", TestEnv.StrongPassword), null));
        Assert.Contains("locked", locked.Message, StringComparison.OrdinalIgnoreCase);

        Assert.True(await db2.AuditLogs.AnyAsync(a => a.Action == "LoginLockout"));
        Assert.DoesNotContain(await db2.AuditLogs.ToListAsync(),
            a => (a.Details ?? "").Contains("WrongPassw0rd!") || (a.Details ?? "").Contains(TestEnv.StrongPassword));
    }

    [Fact]
    public async Task Successful_login_resets_failure_counter()
    {
        var env = await TestEnv.CreateAsync();
        await env.RegisterAsync("Hotel", "Hotel", "hoteladmin");
        for (var i = 0; i < 3; i++)
        {
            using var db = env.NewDb();
            await Assert.ThrowsAsync<UnauthorizedAppException>(() => env.Auth(db).LoginAsync(Login("hoteladmin", "WrongPassw0rd!"), null));
        }
        using var ok = env.NewDb();
        await env.Auth(ok).LoginAsync(Login("hoteladmin", TestEnv.StrongPassword), null);
        Assert.Equal(0, (await ok.AdminUsers.SingleAsync()).FailedLoginCount);
        Assert.NotNull((await ok.AdminUsers.SingleAsync()).LastLoginAt);
    }

    [Fact]
    public async Task Suspended_organization_users_cannot_log_in()
    {
        var env = await TestEnv.CreateAsync();
        var org = await env.RegisterAsync("Hotel", "Hotel", "hoteladmin");
        using (var db = env.NewDb())
            await env.Orgs(db, FakeCurrentUser.Platform()).SetStatusAsync(org.Id, OrganizationStatus.Suspended);

        using var db2 = env.NewDb();
        await Assert.ThrowsAsync<ForbiddenException>(() => env.Auth(db2).LoginAsync(Login("hoteladmin", TestEnv.StrongPassword), null));
    }

    [Fact]
    public async Task Deactivated_user_cannot_log_in()
    {
        var env = await TestEnv.CreateAsync();
        var org = await env.RegisterAsync("Hotel", "Hotel", "hoteladmin");
        using (var db = env.NewDb())
        {
            var user = await db.AdminUsers.SingleAsync(u => u.OrganizationId == org.Id);
            await env.Users(db, FakeCurrentUser.Platform()).SetActiveAsync(user.Id, false);
        }
        using var db2 = env.NewDb();
        await Assert.ThrowsAsync<UnauthorizedAppException>(() => env.Auth(db2).LoginAsync(Login("hoteladmin", TestEnv.StrongPassword), null));
    }
}

public class SmsResolverTests
{
    private static OrganizationSmsSetting Setting(Guid? org, Guid? site, string key, bool active = true) => new()
    {
        OrganizationId = org, SiteId = site, ApiKeyEncrypted = key, ApiEndpoint = "https://2factor.in/API/R1/",
        SenderId = "SENDER", MessageTemplate = "OTP {OTP}", IsActive = active
    };

    [Fact]
    public async Task Resolution_order_is_site_then_org_then_platform_and_tenants_never_mix()
    {
        var env = new TestEnv();
        var orgA = Guid.NewGuid(); var orgB = Guid.NewGuid(); var orgC = Guid.NewGuid();
        var siteA = Guid.NewGuid(); var siteA2 = Guid.NewGuid();
        using (var db = env.NewDb())
        {
            db.OrganizationSmsSettings.AddRange(
                Setting(null, null, "platform"),
                Setting(orgA, null, "orgA"),
                Setting(orgB, null, "orgB"),
                Setting(orgA, siteA, "siteA"));
            await db.SaveChangesAsync();
        }

        using var q = env.NewDb();
        var resolver = new SmsSettingResolver(q);

        Assert.Equal("siteA", (await resolver.ResolveAsync(orgA, siteA))!.ApiKeyEncrypted);
        Assert.Equal("orgA", (await resolver.ResolveAsync(orgA, siteA2))!.ApiKeyEncrypted);   // site without override
        Assert.Equal("orgA", (await resolver.ResolveAsync(orgA, null))!.ApiKeyEncrypted);
        Assert.Equal("orgB", (await resolver.ResolveAsync(orgB, siteA))!.ApiKeyEncrypted);    // other org's site id is ignored
        Assert.Equal("platform", (await resolver.ResolveAsync(orgC, null))!.ApiKeyEncrypted); // no own config -> platform default
    }

    [Fact]
    public async Task Inactive_settings_are_skipped_and_missing_config_returns_null()
    {
        var env = new TestEnv();
        var org = Guid.NewGuid(); var site = Guid.NewGuid();
        using (var db = env.NewDb())
        {
            db.OrganizationSmsSettings.AddRange(Setting(org, site, "siteInactive", active: false), Setting(org, null, "org"));
            await db.SaveChangesAsync();
        }
        using var q = env.NewDb();
        var resolver = new SmsSettingResolver(q);
        Assert.Equal("org", (await resolver.ResolveAsync(org, site))!.ApiKeyEncrypted);
        Assert.Null(await resolver.ResolveAsync(Guid.NewGuid(), null)); // no platform default exists
    }
}
