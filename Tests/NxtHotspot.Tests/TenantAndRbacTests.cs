using Microsoft.EntityFrameworkCore;
using NxtHotspot.Application.Common;
using NxtHotspot.Application.Dtos;
using NxtHotspot.Domain.Entities;
using NxtHotspot.Infrastructure.Services;
using P = NxtHotspot.Application.Common.Permissions;

namespace NxtHotspot.Tests;

public class TenantIsolationTests
{
    private static async Task<(TestEnv Env, OrganizationDetailDto A, OrganizationDetailDto B)> TwoOrgsAsync()
    {
        var env = await TestEnv.CreateAsync();
        var a = await env.RegisterAsync("Hotel", "Hotel A", "adminA", 100m);
        var b = await env.RegisterAsync("School", "School B", "adminB", 200m);
        return (env, a, b);
    }

    [Fact]
    public async Task Org_admin_cannot_load_another_tenants_site()
    {
        var (env, a, b) = await TwoOrgsAsync();
        using var db = env.NewDb();
        var user = FakeCurrentUser.OrgAdmin(a.Id);
        await Assert.ThrowsAsync<NotFoundException>(() => new TenantAccess(db, user).GetAuthorizedSiteAsync(b.PrimarySiteId));
        // own site works
        var own = await new TenantAccess(db, user).GetAuthorizedSiteAsync(a.PrimarySiteId);
        Assert.Equal(a.Id, own.OrganizationId);
    }

    [Fact]
    public async Task Org_admin_cannot_list_sites_or_read_details_of_another_organization()
    {
        var (env, a, b) = await TwoOrgsAsync();
        using var db = env.NewDb();
        var user = FakeCurrentUser.OrgAdmin(a.Id);
        await Assert.ThrowsAsync<ForbiddenException>(() => env.Sites(db, user).ListAsync(b.Id));
        await Assert.ThrowsAsync<ForbiddenException>(() => env.Orgs(db, user).GetAsync(b.Id));
        await Assert.ThrowsAsync<ForbiddenException>(() => env.Sites(db, user).CreateAsync(b.Id, new CreateSiteRequest { Name = "Evil", Code = "EVIL" }));
    }

    [Fact]
    public async Task Org_admin_organization_list_contains_only_their_own()
    {
        var (env, a, _) = await TwoOrgsAsync();
        using var db = env.NewDb();
        var page = await env.Orgs(db, FakeCurrentUser.OrgAdmin(a.Id)).ListAsync(1, 25, null);
        Assert.Equal(a.Id, Assert.Single(page.Items).Id);

        var all = await env.Orgs(db, FakeCurrentUser.Platform()).ListAsync(1, 25, null);
        Assert.Equal(2, all.TotalCount);
    }

    [Fact]
    public async Task Only_platform_admins_can_change_organization_status()
    {
        var (env, a, _) = await TwoOrgsAsync();
        using var db = env.NewDb();
        await Assert.ThrowsAsync<ForbiddenException>(() => env.Orgs(db, FakeCurrentUser.OrgAdmin(a.Id)).SetStatusAsync(a.Id, Domain.Enums.OrganizationStatus.Suspended));
        await env.Orgs(db, FakeCurrentUser.Platform()).SetStatusAsync(a.Id, Domain.Enums.OrganizationStatus.Suspended);
        Assert.Equal(Domain.Enums.OrganizationStatus.Suspended, (await db.Organizations.SingleAsync(o => o.Id == a.Id)).Status);
    }

    [Fact]
    public async Task Site_restricted_admin_only_reaches_assigned_sites()
    {
        var env = await TestEnv.CreateAsync();
        var org = await env.RegisterAsync("Hotel", "Chain Hotel", "chainadmin");

        Guid secondSiteId;
        using (var db = env.NewDb())
        {
            var created = await env.Sites(db, FakeCurrentUser.OrgAdmin(org.Id)).CreateAsync(org.Id, new CreateSiteRequest { Name = "Jaipur", Code = "jpr" });
            secondSiteId = created.Id;
        }

        // A limited admin with no site assignments sees nothing (safe default).
        var limitedId = Guid.NewGuid();
        using (var db = env.NewDb())
        {
            var limited = FakeCurrentUser.Limited(org.Id, limitedId);
            await Assert.ThrowsAsync<NotFoundException>(() => new TenantAccess(db, limited).GetAuthorizedSiteAsync(secondSiteId));
            Assert.Empty(await env.Sites(db, limited).ListAsync(org.Id));
        }

        // Assign only the second site.
        using (var db = env.NewDb())
        {
            db.AdminUserSites.Add(new AdminUserSite { AdminUserId = limitedId, SiteId = secondSiteId });
            await db.SaveChangesAsync();
        }

        using (var db = env.NewDb())
        {
            var limited = FakeCurrentUser.Limited(org.Id, limitedId);
            Assert.Equal(secondSiteId, (await new TenantAccess(db, limited).GetAuthorizedSiteAsync(secondSiteId)).Id);
            await Assert.ThrowsAsync<NotFoundException>(() => new TenantAccess(db, limited).GetAuthorizedSiteAsync(org.PrimarySiteId));
            Assert.Equal(secondSiteId, Assert.Single(await env.Sites(db, limited).ListAsync(org.Id)).Id);
        }
    }
}

public class SiteTests
{
    [Fact]
    public async Task New_site_gets_wallet_and_code_is_unique_case_insensitively()
    {
        var env = await TestEnv.CreateAsync();
        var org = await env.RegisterAsync("Hotel", "Chain Hotel", "chainadmin");
        using var db = env.NewDb();
        var svc = env.Sites(db, FakeCurrentUser.OrgAdmin(org.Id));

        var site = await svc.CreateAsync(org.Id, new CreateSiteRequest { Name = "Delhi", Code = "del" });
        Assert.Equal("DEL", site.Code);
        Assert.False(site.IsPrimary);
        Assert.Equal(0m, (await db.SiteWallets.SingleAsync(w => w.SiteId == site.Id)).Balance);

        await Assert.ThrowsAsync<ConflictException>(() => svc.CreateAsync(org.Id, new CreateSiteRequest { Name = "Delhi 2", Code = "DEL" }));
        Assert.Equal(2, (await svc.ListAsync(org.Id)).Count);
    }

    [Fact]
    public async Task Subscription_plan_site_limit_is_enforced()
    {
        var env = await TestEnv.CreateAsync();
        Guid starter;
        using (var db = env.NewDb()) starter = (await db.SubscriptionPlans.SingleAsync(p => p.Name == "Starter")).Id;

        var org = await env.RegisterAsync("Cafe", "One Site Cafe", "oneadmin", planId: starter);
        using var db2 = env.NewDb();
        await Assert.ThrowsAsync<ConflictException>(() =>
            env.Sites(db2, FakeCurrentUser.OrgAdmin(org.Id)).CreateAsync(org.Id, new CreateSiteRequest { Name = "Second", Code = "S2" }));
    }

    [Fact]
    public async Task Primary_site_cannot_be_deactivated()
    {
        var env = await TestEnv.CreateAsync();
        var org = await env.RegisterAsync("Cafe", "Cafe", "cafeadmin");
        using var db = env.NewDb();
        await Assert.ThrowsAsync<ValidationFailedException>(() =>
            env.Sites(db, FakeCurrentUser.OrgAdmin(org.Id)).UpdateAsync(org.PrimarySiteId, new UpdateSiteRequest { Name = "Main", IsActive = false }));
    }

    [Fact]
    public async Task Site_restricted_admin_cannot_create_sites()
    {
        var env = await TestEnv.CreateAsync();
        var org = await env.RegisterAsync("Hotel", "Hotel", "hoteladmin");
        using var db = env.NewDb();
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            env.Sites(db, FakeCurrentUser.Limited(org.Id, null, P.SitesWrite)).CreateAsync(org.Id, new CreateSiteRequest { Name = "X", Code = "XX" }));
    }

    [Fact]
    public async Task Sites_cannot_be_added_to_a_suspended_organization()
    {
        var env = await TestEnv.CreateAsync();
        var org = await env.RegisterAsync("Hotel", "Hotel", "hoteladmin");
        using (var db = env.NewDb())
            await env.Orgs(db, FakeCurrentUser.Platform()).SetStatusAsync(org.Id, Domain.Enums.OrganizationStatus.Suspended);

        using var db2 = env.NewDb();
        await Assert.ThrowsAsync<ConflictException>(() =>
            env.Sites(db2, FakeCurrentUser.OrgAdmin(org.Id)).CreateAsync(org.Id, new CreateSiteRequest { Name = "X", Code = "XX" }));
    }
}

public class RbacTests
{
    private static CreateAdminUserRequest NewUser(string username, params Guid[] roleIds) => new()
    {
        Username = username, Password = TestEnv.StrongPassword, FullName = "Test User", Email = "u@example.com", RoleIds = roleIds.ToList()
    };

    [Fact]
    public async Task Org_admin_can_create_a_site_restricted_user_who_only_sees_that_site()
    {
        var env = await TestEnv.CreateAsync();
        var org = await env.RegisterAsync("Hotel", "Chain Hotel", "chainadmin");
        var siteAdminRole = await env.RoleIdAsync(SystemRoles.SiteAdmin);

        using var db = env.NewDb();
        var orgAdmin = FakeCurrentUser.OrgAdmin(org.Id);
        var jaipur = await env.Sites(db, orgAdmin).CreateAsync(org.Id, new CreateSiteRequest { Name = "Jaipur", Code = "JPR" });

        var req = NewUser("jaipuradmin", siteAdminRole);
        req.SiteIds.Add(jaipur.Id);
        var created = await env.Users(db, orgAdmin).CreateAsync(req);

        Assert.Equal(org.Id, created.OrganizationId);
        Assert.False(created.HasAllSitesAccess);
        Assert.Equal(new[] { jaipur.Id }, created.SiteIds);
        Assert.Contains(SystemRoles.SiteAdmin, created.Roles);

        var limited = FakeCurrentUser.Limited(org.Id, created.Id, P.SitesRead);
        using var db2 = env.NewDb();
        Assert.Equal(jaipur.Id, Assert.Single(await env.Sites(db2, limited).ListAsync(org.Id)).Id);
    }

    [Fact]
    public async Task Org_admin_cannot_assign_platform_roles()
    {
        var env = await TestEnv.CreateAsync();
        var org = await env.RegisterAsync("Hotel", "Hotel", "hoteladmin");
        var superRole = await env.RoleIdAsync(SystemRoles.SuperAdmin);
        using var db = env.NewDb();
        await Assert.ThrowsAsync<ForbiddenException>(() => env.Users(db, FakeCurrentUser.OrgAdmin(org.Id)).CreateAsync(NewUser("sneaky", superRole)));
    }

    [Fact]
    public async Task Caller_cannot_grant_a_role_with_permissions_they_do_not_hold()
    {
        var env = await TestEnv.CreateAsync();
        var org = await env.RegisterAsync("Hotel", "Hotel", "hoteladmin");
        var orgAdminRole = await env.RoleIdAsync(SystemRoles.OrgAdmin);
        using var db = env.NewDb();

        // A user who may create admins but lacks most permissions must not be able to mint an OrgAdmin.
        var weak = FakeCurrentUser.OrgAdmin(org.Id);
        weak.Permissions = new HashSet<string> { P.AdminUsersWrite, P.AdminUsersRead };
        await Assert.ThrowsAsync<ForbiddenException>(() => env.Users(db, weak).CreateAsync(NewUser("escalated", orgAdminRole)));
    }

    [Fact]
    public async Task Org_admin_cannot_create_users_in_another_organization()
    {
        var env = await TestEnv.CreateAsync();
        var a = await env.RegisterAsync("Hotel", "Hotel A", "adminA");
        var b = await env.RegisterAsync("School", "School B", "adminB");
        var role = await env.RoleIdAsync(SystemRoles.ReadOnly);
        using var db = env.NewDb();
        var req = NewUser("cross", role);
        req.OrganizationId = b.Id;
        await Assert.ThrowsAsync<ForbiddenException>(() => env.Users(db, FakeCurrentUser.OrgAdmin(a.Id)).CreateAsync(req));
    }

    [Fact]
    public async Task Org_admin_cannot_manage_users_of_another_organization()
    {
        var env = await TestEnv.CreateAsync();
        var a = await env.RegisterAsync("Hotel", "Hotel A", "adminA");
        var b = await env.RegisterAsync("School", "School B", "adminB");
        using var db = env.NewDb();
        var victim = await db.AdminUsers.SingleAsync(u => u.OrganizationId == b.Id);
        await Assert.ThrowsAsync<NotFoundException>(() => env.Users(db, FakeCurrentUser.OrgAdmin(a.Id)).SetActiveAsync(victim.Id, false));
        var listed = await env.Users(db, FakeCurrentUser.OrgAdmin(a.Id)).ListAsync(null);
        Assert.All(listed, u => Assert.Equal(a.Id, u.OrganizationId));
    }

    [Fact]
    public async Task Users_cannot_change_their_own_roles_or_deactivate_themselves()
    {
        var env = await TestEnv.CreateAsync();
        var org = await env.RegisterAsync("Hotel", "Hotel", "hoteladmin");
        using var db = env.NewDb();
        var admin = await db.AdminUsers.SingleAsync(u => u.OrganizationId == org.Id);
        var me = FakeCurrentUser.OrgAdmin(org.Id);
        me.AdminUserId = admin.Id;
        var svc = env.Users(db, me);
        await Assert.ThrowsAsync<ValidationFailedException>(() => svc.SetRolesAsync(admin.Id, new List<Guid>()));
        await Assert.ThrowsAsync<ValidationFailedException>(() => svc.SetActiveAsync(admin.Id, false));
    }

    [Fact]
    public async Task Platform_roles_cannot_be_given_to_organization_users()
    {
        var env = await TestEnv.CreateAsync();
        var org = await env.RegisterAsync("Hotel", "Hotel", "hoteladmin");
        var superRole = await env.RoleIdAsync(SystemRoles.SuperAdmin);
        using var db = env.NewDb();
        var req = NewUser("orgcreep", superRole);
        req.OrganizationId = org.Id;
        await Assert.ThrowsAsync<ValidationFailedException>(() => env.Users(db, FakeCurrentUser.Platform()).CreateAsync(req));
    }

    [Fact]
    public async Task Org_role_cannot_contain_platform_only_permissions_or_unheld_permissions()
    {
        var env = await TestEnv.CreateAsync();
        var org = await env.RegisterAsync("Hotel", "Hotel", "hoteladmin");
        using var db = env.NewDb();
        var orgAdmin = FakeCurrentUser.OrgAdmin(org.Id);

        await Assert.ThrowsAsync<ValidationFailedException>(() => env.Roles(db, orgAdmin).CreateRoleAsync(
            new CreateRoleRequest { Name = "Bad", PermissionCodes = new List<string> { P.WalletCredit } }));
        await Assert.ThrowsAsync<ValidationFailedException>(() => env.Roles(db, orgAdmin).CreateRoleAsync(
            new CreateRoleRequest { Name = "Unknown", PermissionCodes = new List<string> { "no.such.permission" } }));

        var limited = FakeCurrentUser.Limited(org.Id, null, P.SitesRead);
        await Assert.ThrowsAsync<ForbiddenException>(() => env.Roles(db, limited).CreateRoleAsync(
            new CreateRoleRequest { Name = "Greedy", PermissionCodes = new List<string> { P.SitesWrite } }));
    }

    [Fact]
    public async Task Custom_role_lifecycle_and_system_role_protection()
    {
        var env = await TestEnv.CreateAsync();
        var org = await env.RegisterAsync("Hotel", "Hotel", "hoteladmin");
        using var db = env.NewDb();
        var orgAdmin = FakeCurrentUser.OrgAdmin(org.Id);
        var roles = env.Roles(db, orgAdmin);

        var role = await roles.CreateRoleAsync(new CreateRoleRequest
        {
            Name = "Front Desk", PermissionCodes = new List<string> { P.SitesRead, P.WalletRead }
        });
        Assert.Equal(org.Id, role.OrganizationId);
        Assert.False(role.IsSystem);

        await Assert.ThrowsAsync<ConflictException>(() => roles.CreateRoleAsync(new CreateRoleRequest { Name = "Front Desk" }));

        var updated = await roles.SetRolePermissionsAsync(role.Id, new List<string> { P.SitesRead });
        Assert.Equal(new[] { P.SitesRead }, updated.PermissionCodes);

        // Visible in this org's list together with system (non-platform) roles, but SuperAdmin is hidden.
        var visible = await roles.ListRolesAsync();
        Assert.Contains(visible, r => r.Name == "Front Desk");
        Assert.DoesNotContain(visible, r => r.Name == SystemRoles.SuperAdmin);

        // System roles are immutable.
        var orgAdminRoleId = await env.RoleIdAsync(SystemRoles.OrgAdmin);
        await Assert.ThrowsAsync<ConflictException>(() => roles.DeleteRoleAsync(orgAdminRoleId));
        await Assert.ThrowsAsync<ConflictException>(() => roles.SetRolePermissionsAsync(orgAdminRoleId, new List<string>()));

        // Another tenant cannot touch it.
        var other = await env.RegisterAsync("School", "Other School", "otheradmin");
        using var db2 = env.NewDb();
        await Assert.ThrowsAsync<NotFoundException>(() => env.Roles(db2, FakeCurrentUser.OrgAdmin(other.Id)).DeleteRoleAsync(role.Id));

        // Role in use cannot be deleted; unused can.
        var users = env.Users(db, orgAdmin);
        var created = await users.CreateAsync(NewUser("frontdesk", role.Id));
        await Assert.ThrowsAsync<ConflictException>(() => roles.DeleteRoleAsync(role.Id));
        await users.SetRolesAsync(created.Id, new List<Guid>());
        await roles.DeleteRoleAsync(role.Id);
        Assert.False(await db.Roles.AnyAsync(r => r.Id == role.Id));
    }

    [Fact]
    public async Task Org_admin_does_not_see_platform_only_permissions()
    {
        var env = await TestEnv.CreateAsync();
        var org = await env.RegisterAsync("Hotel", "Hotel", "hoteladmin");
        using var db = env.NewDb();
        var orgView = await env.Roles(db, FakeCurrentUser.OrgAdmin(org.Id)).ListPermissionsAsync();
        Assert.DoesNotContain(orgView, p => p.IsPlatformOnly);
        var platformView = await env.Roles(db, FakeCurrentUser.Platform()).ListPermissionsAsync();
        Assert.Contains(platformView, p => p.Code == P.WalletCredit);
    }
}
