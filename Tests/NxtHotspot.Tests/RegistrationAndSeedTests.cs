using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NxtHotspot.Application.Common;
using NxtHotspot.Application.Dtos;
using NxtHotspot.Domain.Enums;
using NxtHotspot.Infrastructure.Sms;

namespace NxtHotspot.Tests;

public class SeedTests
{
    [Fact]
    public async Task Seeds_all_fourteen_organization_types_with_default_login_methods()
    {
        var env = await TestEnv.CreateAsync();
        using var db = env.NewDb();

        var types = await db.OrganizationTypes.Include(t => t.DefaultLoginMethods).ToListAsync();
        Assert.Equal(14, types.Count);
        Assert.All(types, t => Assert.NotEmpty(t.DefaultLoginMethods));

        var hotel = types.Single(t => t.TypeName == "Hotel");
        var hotelDefault = Assert.Single(hotel.DefaultLoginMethods, m => m.IsDefault);
        Assert.Equal(AuthenticationType.RoomMobile, hotelDefault.AuthenticationType);
        Assert.True(hotelDefault.RequireBookingValidation);

        var cafe = types.Single(t => t.TypeName == "Cafe");
        Assert.Equal(new[] { AuthenticationType.MobileOTP, AuthenticationType.Voucher }.OrderBy(x => x),
            cafe.DefaultLoginMethods.Select(m => m.AuthenticationType).OrderBy(x => x));
    }

    [Fact]
    public async Task Seeding_twice_is_idempotent_and_keeps_admin_edits()
    {
        var env = await TestEnv.CreateAsync();
        using (var db = env.NewDb())
        {
            (await db.OrganizationTypes.SingleAsync(t => t.TypeName == "Hostel")).IsActive = false;
            await db.SaveChangesAsync();
        }

        await env.SeedAsync();

        using var check = env.NewDb();
        Assert.Equal(14, await check.OrganizationTypes.CountAsync());
        Assert.False((await check.OrganizationTypes.SingleAsync(t => t.TypeName == "Hostel")).IsActive);
        Assert.Equal(Permissions.All.Count, await check.Permissions.CountAsync());
        Assert.Equal(4, await check.Roles.CountAsync(r => r.OrganizationId == null));
    }

    [Fact]
    public async Task OrgAdmin_role_never_contains_platform_only_permissions_but_SuperAdmin_has_everything()
    {
        var env = await TestEnv.CreateAsync();
        using var db = env.NewDb();
        var roles = await db.Roles.Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission).ToListAsync();

        var orgAdmin = roles.Single(r => r.Name == SystemRoles.OrgAdmin);
        Assert.DoesNotContain(orgAdmin.RolePermissions, rp => rp.Permission!.IsPlatformOnly);
        Assert.False(orgAdmin.IsPlatformRole);

        var superAdmin = roles.Single(r => r.Name == SystemRoles.SuperAdmin);
        Assert.True(superAdmin.IsPlatformRole);
        Assert.Equal(Permissions.All.Count, superAdmin.RolePermissions.Count);
    }

    [Fact]
    public async Task No_super_admin_is_created_without_bootstrap_configuration()
    {
        var env = await TestEnv.CreateAsync();
        using var db = env.NewDb();
        Assert.Equal(0, await db.AdminUsers.CountAsync());
    }

    [Fact]
    public async Task Weak_bootstrap_password_is_refused()
    {
        var env = new TestEnv();
        await Assert.ThrowsAsync<InvalidOperationException>(() => env.SeedAsync("root", "weak"));
    }

    [Fact]
    public void Model_contains_all_phase2_entities()
    {
        var env = new TestEnv();
        using var db = env.NewDb();
        var names = db.Model.GetEntityTypes().Select(e => e.ClrType.Name).ToHashSet();
        foreach (var expected in new[]
                 {
                     "OrganizationType", "Organization", "OrganizationSettings", "OrganizationBranding", "Site",
                     "OrganizationLoginMethod", "OrganizationSmsSetting", "AdminUser", "Role", "Permission", "RolePermission",
                     "AdminUserRole", "AdminUserSite", "SiteWallet", "SiteWalletTransaction", "AuditLog", "SubscriptionPlan"
                 })
            Assert.Contains(expected, names);
    }
}

public class RegistrationTests
{
    [Fact]
    public async Task Hotel_registration_creates_everything_with_type_defaults()
    {
        var env = await TestEnv.CreateAsync();
        var sms = new SmsProviderRequest { ApiKey = "SECRET-API-KEY-123", SenderId = "NXTWFI" };

        var detail = await env.RegisterAsync("Hotel", "ABC Hotel", "hoteladmin", balance: 500m, sms: sms);

        Assert.Equal(OrganizationStatus.Active, detail.Status);
        Assert.Equal("Hotel", detail.OrganizationTypeName);
        Assert.Equal(500m, detail.WalletBalance);
        Assert.Equal("hoteladmin", detail.AdminUsername);

        // Login methods copied from the Hotel type defaults, RoomMobile default.
        Assert.Equal(new[] { AuthenticationType.MobileOTP, AuthenticationType.RoomMobile, AuthenticationType.Voucher }.OrderBy(x => x),
            detail.LoginMethods.Select(m => m.AuthenticationType).OrderBy(x => x));
        var def = Assert.Single(detail.LoginMethods, m => m.IsDefault);
        Assert.Equal(AuthenticationType.RoomMobile, def.AuthenticationType);

        using var db = env.NewDb();

        var site = await db.Sites.SingleAsync(s => s.OrganizationId == detail.Id);
        Assert.True(site.IsPrimary);
        Assert.Equal(detail.PrimarySiteId, site.Id);

        var admin = await db.AdminUsers.Include(u => u.UserRoles).ThenInclude(r => r.Role).SingleAsync(u => u.Username == "hoteladmin");
        Assert.Equal(detail.Id, admin.OrganizationId);
        Assert.True(admin.HasAllSitesAccess);
        Assert.Contains(admin.UserRoles, r => r.Role!.Name == SystemRoles.OrgAdmin);
        Assert.NotEqual(TestEnv.StrongPassword, admin.PasswordHash);
        Assert.True(env.Passwords.Verify(admin.PasswordHash, TestEnv.StrongPassword));

        var txn = await db.SiteWalletTransactions.SingleAsync(t => t.SiteId == site.Id);
        Assert.Equal(WalletTransactionType.Credit, txn.Type);
        Assert.Equal(500m, txn.BalanceAfter);
        Assert.Equal("INITIAL_BALANCE", txn.Reference);

        Assert.NotNull(await db.OrganizationSettings.SingleOrDefaultAsync(s => s.OrganizationId == detail.Id));
        Assert.NotNull(await db.OrganizationBranding.SingleOrDefaultAsync(b => b.OrganizationId == detail.Id));
        Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "OrganizationRegistered" && a.OrganizationId == detail.Id));
    }

    [Fact]
    public async Task Sms_api_key_is_encrypted_at_rest_and_never_returned()
    {
        var env = await TestEnv.CreateAsync();
        var detail = await env.RegisterAsync("Cafe", "Blue Cafe", "cafeadmin",
            sms: new SmsProviderRequest { ApiKey = "SECRET-API-KEY-123", SenderId = "NXTWFI" });

        using var db = env.NewDb();
        var row = await db.OrganizationSmsSettings.SingleAsync(s => s.OrganizationId == detail.Id);
        Assert.DoesNotContain("SECRET-API-KEY-123", row.ApiKeyEncrypted);
        Assert.Equal("SECRET-API-KEY-123", env.Protector.Unprotect(row.ApiKeyEncrypted));
        Assert.Equal("https://2factor.in/API/R1/", row.ApiEndpoint);
        Assert.Contains("{OTP}", row.MessageTemplate);

        Assert.True(detail.Sms.HasApiKey);
        Assert.DoesNotContain("SECRET-API-KEY-123", JsonSerializer.Serialize(detail));
    }

    [Fact]
    public async Task Explicit_login_methods_override_type_defaults()
    {
        var env = await TestEnv.CreateAsync();
        var detail = await env.RegisterAsync("School", "City School", "schooladmin",
            methods: new List<AuthenticationType> { AuthenticationType.StudentPassword, AuthenticationType.GuestOTP });

        Assert.Equal(2, detail.LoginMethods.Count);
        Assert.Equal(AuthenticationType.StudentPassword, Assert.Single(detail.LoginMethods, m => m.IsDefault).AuthenticationType);
        Assert.True(detail.LoginMethods.Single(m => m.AuthenticationType == AuthenticationType.GuestOTP).RequireOTP);
    }

    [Fact]
    public async Task Organization_without_sms_config_falls_back_to_platform_default_marker()
    {
        var env = await TestEnv.CreateAsync();
        var detail = await env.RegisterAsync("Office", "Acme Corp", "acmeadmin");
        Assert.False(detail.Sms.HasApiKey);
        Assert.False(detail.Sms.IsConfigured); // no platform default exists yet
    }

    [Fact]
    public async Task Duplicate_admin_username_is_rejected_case_insensitively()
    {
        var env = await TestEnv.CreateAsync();
        await env.RegisterAsync("Hotel", "Hotel One", "SharedName");
        await Assert.ThrowsAsync<ConflictException>(() => env.RegisterAsync("Hotel", "Hotel Two", "sharedname"));

        using var db = env.NewDb();
        Assert.Equal(1, await db.Organizations.CountAsync());
    }

    [Fact]
    public async Task Weak_admin_password_is_rejected()
    {
        var env = await TestEnv.CreateAsync();
        using var db = env.NewDb();
        var typeId = (await db.OrganizationTypes.FirstAsync()).OrganizationTypeId;
        var request = new RegisterOrganizationRequest
        {
            OrganizationName = "X Org", OrganizationTypeId = typeId, ContactPerson = "A", MobileNumber = "9876543210",
            Email = "a@example.com", AdminUsername = "xadmin", AdminPassword = "weak"
        };
        await Assert.ThrowsAsync<ValidationFailedException>(() => env.Orgs(db, FakeCurrentUser.Platform()).RegisterAsync(request));
    }

    [Fact]
    public async Task Only_platform_admins_can_register()
    {
        var env = await TestEnv.CreateAsync();
        using var db = env.NewDb();
        var request = new RegisterOrganizationRequest
        {
            OrganizationName = "X Org", OrganizationTypeId = 1, ContactPerson = "A", MobileNumber = "9876543210",
            Email = "a@example.com", AdminUsername = "xadmin", AdminPassword = TestEnv.StrongPassword
        };
        await Assert.ThrowsAsync<ForbiddenException>(() => env.Orgs(db, FakeCurrentUser.OrgAdmin(Guid.NewGuid())).RegisterAsync(request));
    }

    [Fact]
    public async Task Inactive_or_unknown_type_is_rejected()
    {
        var env = await TestEnv.CreateAsync();
        using (var db = env.NewDb())
        {
            (await db.OrganizationTypes.SingleAsync(t => t.TypeName == "Cafe")).IsActive = false;
            await db.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<ValidationFailedException>(() => env.RegisterAsync("Cafe", "Closed Cafe", "cafe1"));

        using var db2 = env.NewDb();
        var bad = new RegisterOrganizationRequest
        {
            OrganizationName = "X Org", OrganizationTypeId = 9999, ContactPerson = "A", MobileNumber = "9876543210",
            Email = "a@example.com", AdminUsername = "xadmin", AdminPassword = TestEnv.StrongPassword
        };
        await Assert.ThrowsAsync<ValidationFailedException>(() => env.Orgs(db2, FakeCurrentUser.Platform()).RegisterAsync(bad));
    }

    [Theory]
    [InlineData("https://evil.example/API/R1/")]   // host not allow-listed (SSRF guard)
    [InlineData("http://2factor.in/API/R1/")]      // not https
    [InlineData("not a url")]
    public async Task Sms_endpoint_must_be_https_and_allow_listed(string endpoint)
    {
        var env = await TestEnv.CreateAsync();
        var sms = new SmsProviderRequest { ApiKey = "SECRET-API-KEY-123", SenderId = "NXTWFI", ApiEndpoint = endpoint };
        await Assert.ThrowsAsync<ValidationFailedException>(() => env.RegisterAsync("Cafe", "Bad Sms Cafe", "badsms", sms: sms));

        using var db = env.NewDb();
        Assert.Equal(0, await db.Organizations.CountAsync());
        Assert.Equal(0, await db.OrganizationSmsSettings.CountAsync());
    }

    [Fact]
    public async Task Sms_template_must_contain_otp_placeholder()
    {
        var env = await TestEnv.CreateAsync();
        var sms = new SmsProviderRequest { ApiKey = "SECRET-API-KEY-123", SenderId = "NXTWFI", MessageTemplate = "Hello there" };
        await Assert.ThrowsAsync<ValidationFailedException>(() => env.RegisterAsync("Cafe", "Tmpl Cafe", "tmpl", sms: sms));
    }

    [Fact]
    public async Task Allowed_hosts_are_configurable()
    {
        var env = await TestEnv.CreateAsync();
        using var db = env.NewDb();
        var typeId = (await db.OrganizationTypes.FirstAsync(t => t.TypeName == "Cafe")).OrganizationTypeId;
        var request = new RegisterOrganizationRequest
        {
            OrganizationName = "Custom Sms Cafe", OrganizationTypeId = typeId, ContactPerson = "A", MobileNumber = "9876543210",
            Email = "a@example.com", AdminUsername = "customsms", AdminPassword = TestEnv.StrongPassword,
            Sms = new SmsProviderRequest { ApiKey = "SECRET-API-KEY-123", SenderId = "NXTWFI", ApiEndpoint = "https://sms.example.org/send" }
        };
        var detail = await env.Orgs(db, FakeCurrentUser.Platform(), new SmsOptions { AllowedHosts = new[] { "sms.example.org" } }).RegisterAsync(request);
        Assert.Equal("https://sms.example.org/send", detail.Sms.ApiEndpoint);
    }

    [Fact]
    public async Task Duplicate_login_methods_are_rejected()
    {
        var env = await TestEnv.CreateAsync();
        await Assert.ThrowsAsync<ValidationFailedException>(() => env.RegisterAsync("Cafe", "Dup Cafe", "dupcafe",
            methods: new List<AuthenticationType> { AuthenticationType.MobileOTP, AuthenticationType.MobileOTP }));
    }
}
