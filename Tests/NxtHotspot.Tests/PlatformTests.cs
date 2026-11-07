using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NxtHotspot.Application.Common;
using NxtHotspot.Application.Dtos;
using NxtHotspot.Domain.Entities;
using NxtHotspot.Domain.Enums;
using NxtHotspot.Infrastructure.Persistence;
using NxtHotspot.Infrastructure.Services;
using P = NxtHotspot.Application.Common.Permissions;

namespace NxtHotspot.Tests;

public class PlatformTests
{
    private static DashboardService Dashboard(TestEnv env, NxtDbContext db, ICurrentUser user) =>
        new(db, user, new TenantAccess(db, user), new AuditLogService(db, user));

    private static SiteDirectoryService Directory(NxtDbContext db, ICurrentUser user) => new(db, user, new TenantAccess(db, user));

    [Fact]
    public async Task Seeder_adds_new_organization_types_and_phase3_permissions()
    {
        var env = await TestEnv.CreateAsync();
        using var db = env.NewDb();
        var types = await db.OrganizationTypes.Select(t => t.TypeName).ToListAsync();
        Assert.Contains("Enterprise", types);
        Assert.Contains("Shopping Mall", types);
        Assert.Contains("Railway / Transport", types);

        var siteAdmin = await db.Roles.Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .FirstAsync(r => r.OrganizationId == null && r.Name == SystemRoles.SiteAdmin);
        var codes = siteAdmin.RolePermissions.Select(rp => rp.Permission!.Code).ToHashSet();
        Assert.True(P.SiteAdminDefaults.SetEquals(codes));
        Assert.Contains(P.SessionsDisconnect, codes);

        var orgAdmin = await db.Roles.Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .FirstAsync(r => r.OrganizationId == null && r.Name == SystemRoles.OrgAdmin);
        Assert.DoesNotContain(orgAdmin.RolePermissions, rp => rp.Permission!.IsPlatformOnly);
        Assert.Contains(orgAdmin.RolePermissions, rp => rp.Permission!.Code == P.SubscribersWrite);
    }

    [Fact]
    public async Task Upgrade_grants_newly_added_permissions_but_respects_earlier_removals()
    {
        var env = await TestEnv.CreateAsync();
        using (var db = env.NewDb())
        {
            // Simulate a database from an older release: nas.read did not exist yet,
            // and an administrator had removed sessions.read from the Site Admin role.
            var nas = await db.Permissions.FirstAsync(p => p.Code == P.NasRead);
            db.RolePermissions.RemoveRange(db.RolePermissions.Where(rp => rp.PermissionId == nas.Id));
            db.Permissions.Remove(nas);
            var siteAdmin = await db.Roles.FirstAsync(r => r.OrganizationId == null && r.Name == SystemRoles.SiteAdmin);
            var sessions = await db.Permissions.FirstAsync(p => p.Code == P.SessionsRead);
            db.RolePermissions.Remove(await db.RolePermissions.FirstAsync(rp => rp.RoleId == siteAdmin.Id && rp.PermissionId == sessions.Id));
            await db.SaveChangesAsync();
        }

        await env.SeedAsync();

        using var check = env.NewDb();
        var codes = await check.RolePermissions.Where(rp => rp.Role!.Name == SystemRoles.SiteAdmin && rp.Role.OrganizationId == null)
            .Select(rp => rp.Permission!.Code).ToListAsync();
        Assert.Contains(P.NasRead, codes);           // newly added in this release -> granted
        Assert.DoesNotContain(P.SessionsRead, codes); // removed by an admin earlier -> stays removed
    }

    [Fact]
    public async Task Account_context_returns_only_assigned_sites_for_site_limited_admin()
    {
        var env = await TestEnv.CreateAsync();
        var org = await env.RegisterAsync("Hotel", "Hotel Context", "ctxadmin");
        Guid second;
        Guid limitedId;
        using (var db = env.NewDb())
        {
            second = (await env.Sites(db, FakeCurrentUser.OrgAdmin(org.Id)).CreateAsync(org.Id, new CreateSiteRequest { Name = "Annex", Code = "ANX" })).Id;
            var created = await env.Users(db, FakeCurrentUser.OrgAdmin(org.Id)).CreateAsync(new CreateAdminUserRequest
            {
                Username = "annexadmin", Password = TestEnv.StrongPassword, FullName = "Annex Admin", Email = "annex@example.com",
                HasAllSitesAccess = false, SiteIds = new() { second }, RoleIds = new() { await env.RoleIdAsync(SystemRoles.SiteAdmin) }
            });
            limitedId = created.Id;
        }

        using var db2 = env.NewDb();
        var user = FakeCurrentUser.Limited(org.Id, limitedId, P.SitesRead);
        var ctx = await new AccountService(db2, user).GetContextAsync();
        Assert.False(ctx.IsPlatformAdmin);
        Assert.Equal("Hotel Context", ctx.Organization!.Name);
        Assert.Equal(new[] { second }, ctx.Sites.Select(s => s.Id).ToArray());
        Assert.Contains(SystemRoles.SiteAdmin, ctx.Roles);
    }

    [Fact]
    public async Task Organization_statistics_are_tenant_isolated_and_site_scoped()
    {
        var env = await TestEnv.CreateAsync();
        var a = await env.RegisterAsync("Hotel", "Stats A", "statsA", 250m);
        var b = await env.RegisterAsync("School", "Stats B", "statsB", 75m);

        using var db = env.NewDb();
        await Assert.ThrowsAsync<ForbiddenException>(() => Dashboard(env, db, FakeCurrentUser.OrgAdmin(a.Id)).GetOrganizationAsync(b.Id));

        var stats = await Dashboard(env, db, FakeCurrentUser.OrgAdmin(a.Id)).GetOrganizationAsync(a.Id);
        Assert.Equal(250m, stats.Wallet.TotalBalance);
        Assert.Single(stats.SiteBreakdown);
        Assert.Equal(1, stats.Sites.Total);

        // A site-limited admin with no sites sees no site figures.
        var limited = FakeCurrentUser.Limited(a.Id, Guid.NewGuid(), P.SitesRead);
        var scoped = await Dashboard(env, db, limited).GetOrganizationAsync(a.Id);
        Assert.Empty(scoped.SiteBreakdown);
        Assert.Equal(0m, scoped.Wallet.TotalBalance);
        Assert.Empty(scoped.RecentActivity);
    }

    [Fact]
    public async Task Platform_dashboard_is_platform_only_and_counts_real_rows()
    {
        var env = await TestEnv.CreateAsync();
        var a = await env.RegisterAsync("Hotel", "Dash A", "dashA", 50m);
        await env.RegisterAsync("ISP", "Dash B", "dashB", 20m);

        using var db = env.NewDb();
        await Assert.ThrowsAsync<ForbiddenException>(() => Dashboard(env, db, FakeCurrentUser.OrgAdmin(a.Id)).GetPlatformAsync());
        var dash = await Dashboard(env, db, FakeCurrentUser.Platform()).GetPlatformAsync();
        Assert.Equal(2, dash.TotalOrganizations);
        Assert.Equal(2, dash.OrganizationsByStatus.Single(s => s.Label == "Active").Count);
        Assert.Equal(70m, dash.Wallet.TotalBalance);
        Assert.Equal(2, dash.Wallet.LowBalanceWallets);
        Assert.Equal(6, dash.RegistrationsByMonth.Count);
        Assert.Equal(2, dash.RegistrationsByMonth[^1].Count);
    }

    [Fact]
    public async Task Site_directory_and_wallets_never_cross_tenants()
    {
        var env = await TestEnv.CreateAsync();
        var a = await env.RegisterAsync("Hotel", "Dir A", "dirA", 10m);
        var b = await env.RegisterAsync("School", "Dir B", "dirB", 20m);

        using var db = env.NewDb();
        var orgAdmin = FakeCurrentUser.OrgAdmin(a.Id);
        var own = await Directory(db, orgAdmin).ListAsync(null, null, null, 1, 50);
        Assert.All(own.Items, s => Assert.Equal(a.Id, s.OrganizationId));
        await Assert.ThrowsAsync<ForbiddenException>(() => Directory(db, orgAdmin).ListAsync(b.Id, null, null, 1, 50));
        await Assert.ThrowsAsync<ForbiddenException>(() => Directory(db, orgAdmin).ListWalletsAsync(b.Id, null, 1, 50));

        var all = await Directory(db, FakeCurrentUser.Platform()).ListWalletsAsync(null, null, 1, 50);
        Assert.Equal(2, all.TotalCount);
        var filtered = await Directory(db, FakeCurrentUser.Platform()).ListAsync(null, "Dir B", null, 1, 50);
        Assert.All(filtered.Items, s => Assert.Equal(b.Id, s.OrganizationId));
    }

    [Fact]
    public async Task Plans_can_be_created_updated_and_assigned_with_limit_checks()
    {
        var env = await TestEnv.CreateAsync();
        var org = await env.RegisterAsync("Hotel", "Plan Org", "planadmin");
        using var db = env.NewDb();
        var platform = FakeCurrentUser.Platform();
        var plans = new PlanService(db, platform);

        var created = await plans.CreateAsync(new UpsertSubscriptionPlanRequest { Name = "Hotel Pro", MonthlyPrice = 4999, MaxSites = 1, MaxNasDevices = 4 });
        Assert.Equal("Hotel Pro", created.Name);
        await Assert.ThrowsAsync<ConflictException>(() => plans.CreateAsync(new UpsertSubscriptionPlanRequest { Name = "hotel pro" }));

        var updated = await plans.UpdateAsync(created.Id, new UpsertSubscriptionPlanRequest { Name = "Hotel Pro", MonthlyPrice = 5999, MaxSites = 1 });
        Assert.Equal(5999, updated.MonthlyPrice);

        await plans.AssignAsync(org.Id, created.Id);
        Assert.Equal(1, (await plans.ListAsync()).Single(p => p.Id == created.Id).OrganizationCount);

        // A second site would exceed MaxSites=1.
        await Assert.ThrowsAsync<ConflictException>(() =>
            env.Sites(db, FakeCurrentUser.OrgAdmin(org.Id)).CreateAsync(org.Id, new CreateSiteRequest { Name = "Two", Code = "TWO" }));

        await Assert.ThrowsAsync<ForbiddenException>(() => new PlanService(db, FakeCurrentUser.OrgAdmin(org.Id)).ListAsync());
        Assert.Contains(await db.AuditLogs.Select(l => l.Action).ToListAsync(), x => x == "SubscriptionPlanAssigned");
    }
}
