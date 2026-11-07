using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NxtHotspot.Application.Common;
using NxtHotspot.Application.Dtos;
using NxtHotspot.Domain.Entities;
using NxtHotspot.Domain.Enums;
using NxtHotspot.Infrastructure.Hotspot;
using NxtHotspot.Infrastructure.Persistence;
using NxtHotspot.Infrastructure.Services;
using P = NxtHotspot.Application.Common.Permissions;

namespace NxtHotspot.Tests;

public class HotspotTests
{
    private static NasService Nas(TestEnv env, NxtDbContext db, ICurrentUser u) =>
        new(db, u, new TenantAccess(db, u), env.Protector, Options.Create(new RadiusOptions { PublicAddress = "203.0.113.10", PortalBaseUrl = "https://portal.example.com" }));
    private static PackageService Packages(NxtDbContext db, ICurrentUser u) => new(db, u, new TenantAccess(db, u));
    private static SubscriberGroupService Groups(NxtDbContext db, ICurrentUser u) => new(db, u, new TenantAccess(db, u));
    private static SubscriberService Subs(TestEnv env, NxtDbContext db, ICurrentUser u) => new(db, u, new TenantAccess(db, u), env.Passwords, new DisconnectQueue(db, u));

    private static UpsertNasRequest NasReq(Guid siteId, string ip, string? secret = "radius-secret-123") =>
        new() { SiteId = siteId, Name = "Lobby router", IpAddress = ip, SharedSecret = secret, RouterOsVersion = "v7" };

    [Fact]
    public async Task Nas_secret_is_encrypted_never_returned_and_ip_is_unique()
    {
        var env = await TestEnv.CreateAsync();
        var a = await env.RegisterAsync("Hotel", "Nas A", "nasA");
        var b = await env.RegisterAsync("ISP", "Nas B", "nasB");
        using var db = env.NewDb();
        var admin = FakeCurrentUser.OrgAdmin(a.Id);

        await Assert.ThrowsAsync<ValidationFailedException>(() => Nas(env, db, admin).CreateAsync(a.Id, NasReq(a.PrimarySiteId, "10.0.0.1", null)));
        await Assert.ThrowsAsync<ValidationFailedException>(() => Nas(env, db, admin).CreateAsync(a.Id, NasReq(a.PrimarySiteId, "10.0.0.1", "short")));
        await Assert.ThrowsAsync<ValidationFailedException>(() => Nas(env, db, admin).CreateAsync(a.Id, NasReq(a.PrimarySiteId, "not-an-ip")));

        var nas = await Nas(env, db, admin).CreateAsync(a.Id, NasReq(a.PrimarySiteId, "10.0.0.1"));
        Assert.True(nas.HasSharedSecret);
        Assert.DoesNotContain(typeof(NasDto).GetProperties(), p => p.Name.Contains("Secret") && p.PropertyType == typeof(string));
        var stored = await db.NasDevices.AsNoTracking().SingleAsync(n => n.Id == nas.Id);
        Assert.NotEqual("radius-secret-123", stored.SharedSecretEncrypted);
        Assert.Equal("radius-secret-123", env.Protector.Unprotect(stored.SharedSecretEncrypted));

        // Same IP in another tenant is rejected: RADIUS identifies routers by source IP.
        await Assert.ThrowsAsync<ConflictException>(() => Nas(env, db, FakeCurrentUser.OrgAdmin(b.Id)).CreateAsync(b.Id, NasReq(b.PrimarySiteId, "10.0.0.1")));
        // Another tenant's site is invisible.
        await Assert.ThrowsAsync<NotFoundException>(() => Nas(env, db, FakeCurrentUser.OrgAdmin(b.Id)).CreateAsync(b.Id, NasReq(a.PrimarySiteId, "10.0.0.2")));
        await Assert.ThrowsAsync<ForbiddenException>(() => Nas(env, db, FakeCurrentUser.OrgAdmin(b.Id)).ListAsync(a.Id, null));

        // Update without a secret keeps the stored one.
        await Nas(env, db, admin).UpdateAsync(a.Id, nas.Id, NasReq(a.PrimarySiteId, "10.0.0.5", null));
        var after = await db.NasDevices.AsNoTracking().SingleAsync(n => n.Id == nas.Id);
        Assert.Equal("10.0.0.5", after.IpAddress);
        Assert.Equal("radius-secret-123", env.Protector.Unprotect(after.SharedSecretEncrypted));
    }

    [Fact]
    public async Task Portal_key_is_stored_hashed_and_router_config_never_contains_the_secret()
    {
        var env = await TestEnv.CreateAsync();
        var a = await env.RegisterAsync("Hotel", "Key Org", "keyadmin");
        using var db = env.NewDb();
        var admin = FakeCurrentUser.OrgAdmin(a.Id);
        var nas = await Nas(env, db, admin).CreateAsync(a.Id, NasReq(a.PrimarySiteId, "192.0.2.7"));

        var key = await Nas(env, db, admin).RegeneratePortalKeyAsync(a.Id, nas.Id);
        Assert.Equal(32, key.PortalKey.Length);
        Assert.Equal($"https://portal.example.com/p/{key.PortalKey}", key.PortalUrl);
        var stored = await db.NasDevices.AsNoTracking().SingleAsync(n => n.Id == nas.Id);
        Assert.Equal(NasService.HashKey(key.PortalKey), stored.PortalKeyHash);
        Assert.NotEqual(key.PortalKey, stored.PortalKeyHash);

        var cfg = await Nas(env, db, admin).GetRouterConfigAsync(a.Id, nas.Id);
        Assert.Contains("address=203.0.113.10", cfg.Script);
        Assert.Contains("<SHARED_SECRET>", cfg.Script);
        Assert.DoesNotContain("radius-secret-123", cfg.Script);
        Assert.Equal("v7", cfg.RouterOsVersion);
    }

    [Fact]
    public async Task Plan_limits_nas_devices_and_subscribers()
    {
        var env = await TestEnv.CreateAsync();
        Guid planId;
        using (var db0 = env.NewDb())
            planId = (await new PlanService(db0, FakeCurrentUser.Platform()).CreateAsync(new UpsertSubscriptionPlanRequest { Name = "Tiny", MaxNasDevices = 1, MaxUsers = 2 })).Id;
        var a = await env.RegisterAsync("Cafe", "Tiny Cafe", "tinyadmin", planId: planId);
        using var db = env.NewDb();
        var admin = FakeCurrentUser.OrgAdmin(a.Id);
        await Nas(env, db, admin).CreateAsync(a.Id, NasReq(a.PrimarySiteId, "198.51.100.1"));
        await Assert.ThrowsAsync<ConflictException>(() => Nas(env, db, admin).CreateAsync(a.Id, NasReq(a.PrimarySiteId, "198.51.100.2")));

        var subs = Subs(env, db, admin);
        await subs.CreateAsync(a.Id, new CreateSubscriberRequest { SubscriberType = SubscriberType.Guest, FullName = "G1", MobileNumber = "9000000001" });
        await subs.CreateAsync(a.Id, new CreateSubscriberRequest { SubscriberType = SubscriberType.Guest, FullName = "G2", MobileNumber = "9000000002" });
        await Assert.ThrowsAsync<ConflictException>(() => subs.CreateAsync(a.Id, new CreateSubscriberRequest { SubscriberType = SubscriberType.Guest, FullName = "G3", MobileNumber = "9000000003" }));
    }

    [Fact]
    public async Task Site_limited_admin_cannot_manage_other_sites_or_organization_wide_items()
    {
        var env = await TestEnv.CreateAsync();
        var a = await env.RegisterAsync("College", "Site Scope College", "ssadmin");
        Guid annex;
        using (var db0 = env.NewDb())
            annex = (await env.Sites(db0, FakeCurrentUser.OrgAdmin(a.Id)).CreateAsync(a.Id, new CreateSiteRequest { Name = "Annex", Code = "ANNEX" })).Id;

        using var db = env.NewDb();
        var limitedId = Guid.NewGuid();
        db.AdminUsers.Add(new AdminUser { Id = limitedId, OrganizationId = a.Id, Username = "annexonly", NormalizedUsername = "ANNEXONLY", FullName = "Annex", Email = "a@x.com", PasswordHash = "x" });
        db.AdminUserSites.Add(new AdminUserSite { AdminUserId = limitedId, SiteId = annex });
        await db.SaveChangesAsync();
        var limited = FakeCurrentUser.Limited(a.Id, limitedId, P.NasWrite, P.PackagesWrite, P.SubscribersWrite);

        await Nas(env, db, limited).CreateAsync(a.Id, NasReq(annex, "203.0.113.50"));
        await Assert.ThrowsAsync<NotFoundException>(() => Nas(env, db, limited).CreateAsync(a.Id, NasReq(a.PrimarySiteId, "203.0.113.51")));
        await Assert.ThrowsAsync<ForbiddenException>(() => Packages(db, limited).CreateAsync(a.Id, new UpsertPackageRequest { Name = "Org-wide" }));
        await Packages(db, limited).CreateAsync(a.Id, new UpsertPackageRequest { Name = "Annex only", SiteId = annex });
        await Assert.ThrowsAsync<ForbiddenException>(() => Groups(db, limited).CreateAsync(a.Id, new UpsertGroupRequest { Name = "CSE" }));

        var nasList = await Nas(env, db, limited).ListAsync(a.Id, null);
        Assert.All(nasList, n => Assert.Equal(annex, n.SiteId));
    }

    [Fact]
    public async Task Students_get_hashed_passwords_and_unique_ids_per_organization()
    {
        var env = await TestEnv.CreateAsync();
        var a = await env.RegisterAsync("College", "Uni A", "uniA");
        var b = await env.RegisterAsync("College", "Uni B", "uniB");
        using var db = env.NewDb();
        var subsA = Subs(env, db, FakeCurrentUser.OrgAdmin(a.Id));
        var group = await Groups(db, FakeCurrentUser.OrgAdmin(a.Id)).CreateAsync(a.Id, new UpsertGroupRequest { Name = "CSE 2026", GroupType = SubscriberGroupType.Batch });

        var s = await subsA.CreateAsync(a.Id, new CreateSubscriberRequest { SubscriberType = SubscriberType.Student, Identifier = "cs2026-001", FullName = "Anika", Password = "secret12", GroupId = group.Id });
        Assert.True(s.HasPassword);
        Assert.Equal("CSE 2026", s.GroupName);
        var stored = await db.Subscribers.AsNoTracking().SingleAsync(x => x.Id == s.Id);
        Assert.NotEqual("secret12", stored.PasswordHash);
        Assert.True(env.Passwords.Verify(stored.PasswordHash!, "secret12"));

        await Assert.ThrowsAsync<ConflictException>(() => subsA.CreateAsync(a.Id, new CreateSubscriberRequest { SubscriberType = SubscriberType.Student, Identifier = "CS2026-001", FullName = "Dup" }));
        await Assert.ThrowsAsync<ValidationFailedException>(() => subsA.CreateAsync(a.Id, new CreateSubscriberRequest { SubscriberType = SubscriberType.Student, Identifier = "x1", FullName = "Short", Password = "12345" }));
        await Assert.ThrowsAsync<ValidationFailedException>(() => subsA.CreateAsync(a.Id, new CreateSubscriberRequest { SubscriberType = SubscriberType.Student, FullName = "No id" }));

        // The same student ID is fine in another organization, and B cannot see A's student.
        var subsB = Subs(env, db, FakeCurrentUser.OrgAdmin(b.Id));
        await subsB.CreateAsync(b.Id, new CreateSubscriberRequest { SubscriberType = SubscriberType.Student, Identifier = "CS2026-001", FullName = "Other" });
        await Assert.ThrowsAsync<ForbiddenException>(() => subsB.GetAsync(a.Id, s.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => subsB.GetAsync(b.Id, s.Id));
    }

    [Fact]
    public async Task Room_guests_are_site_scoped_and_checkout_queues_disconnects()
    {
        var env = await TestEnv.CreateAsync();
        var a = await env.RegisterAsync("Hotel", "Room Hotel", "roomadmin");
        Guid annex;
        using (var db0 = env.NewDb())
            annex = (await env.Sites(db0, FakeCurrentUser.OrgAdmin(a.Id)).CreateAsync(a.Id, new CreateSiteRequest { Name = "Annex", Code = "ANX" })).Id;

        using var db = env.NewDb();
        var admin = FakeCurrentUser.OrgAdmin(a.Id);
        var subs = Subs(env, db, admin);
        var outAt = DateTime.UtcNow.AddDays(2);

        await Assert.ThrowsAsync<ValidationFailedException>(() => subs.CreateAsync(a.Id, new CreateSubscriberRequest { SubscriberType = SubscriberType.RoomGuest, FullName = "No site", RoomNumber = "101", MobileNumber = "9811111111", CheckOutAt = outAt }));
        await Assert.ThrowsAsync<ValidationFailedException>(() => subs.CreateAsync(a.Id, new CreateSubscriberRequest { SubscriberType = SubscriberType.RoomGuest, FullName = "No out", SiteId = a.PrimarySiteId, RoomNumber = "101", MobileNumber = "9811111111" }));

        var g1 = await subs.CreateAsync(a.Id, new CreateSubscriberRequest { SubscriberType = SubscriberType.RoomGuest, FullName = "Main 101", SiteId = a.PrimarySiteId, RoomNumber = "101", MobileNumber = "9811111111", CheckOutAt = outAt });
        var g2 = await subs.CreateAsync(a.Id, new CreateSubscriberRequest { SubscriberType = SubscriberType.RoomGuest, FullName = "Annex 101", SiteId = annex, RoomNumber = "101", MobileNumber = "9822222222", CheckOutAt = outAt });
        Assert.NotEqual(g1.Identifier, g2.Identifier);
        Assert.StartsWith("RG-MAIN-101-", g1.Identifier);

        var nas = await Nas(env, db, admin).CreateAsync(a.Id, NasReq(a.PrimarySiteId, "192.0.2.9"));
        db.RadiusSessions.Add(new RadiusSession { OrganizationId = a.Id, SiteId = a.PrimarySiteId, NasId = nas.Id, SubscriberId = g1.Id, Username = "x", AcctSessionId = "abc", StartedAt = DateTime.UtcNow, LastUpdateAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var checkedOut = await subs.CheckOutAsync(a.Id, g1.Id);
        Assert.Equal(SubscriberStatus.CheckedOut, checkedOut.Status);
        Assert.Equal(1, await db.DisconnectCommands.CountAsync(c => c.Status == DisconnectStatus.Pending));
        await Assert.ThrowsAsync<ValidationFailedException>(() => subs.CreateAsync(a.Id, new CreateSubscriberRequest { SubscriberType = SubscriberType.Student, Identifier = "s1", FullName = "S" })
            .ContinueWith(async t => { var s = await t; await subs.CheckOutAsync(a.Id, s.Id); }).Unwrap());
    }

    [Fact]
    public async Task Excel_import_creates_updates_reports_errors_and_export_round_trips()
    {
        var env = await TestEnv.CreateAsync();
        var a = await env.RegisterAsync("School", "Import School", "impadmin");
        using var db = env.NewDb();
        var admin = FakeCurrentUser.OrgAdmin(a.Id);
        await Packages(db, admin).CreateAsync(a.Id, new UpsertPackageRequest { Name = "Student 10 Mbps", DownloadKbps = 10240, UploadKbps = 5120 });
        var subs = Subs(env, db, admin);

        byte[] Book(params string[][] rows)
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Students");
            for (var r = 0; r < rows.Length; r++) for (var c = 0; c < rows[r].Length; c++) ws.Cell(r + 1, c + 1).Value = rows[r][c];
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        var file = Book(
            new[] { "Identifier", "FullName", "MobileNumber", "Password", "Group", "Package", "SiteCode" },
            new[] { "S-001", "Asha", "9876500001", "pass1234", "Class 10A", "Student 10 Mbps", "MAIN" },
            new[] { "S-002", "Bilal", "9876500002", "", "Class 10A", "Student 10 Mbps", "" },
            new[] { "S-003", "Chen", "12", "", "", "", "" },
            new[] { "S-004", "Dev", "", "", "", "Missing package", "" },
            new[] { "S-001", "Asha again", "", "", "", "", "" });
        var result = await subs.ImportAsync(a.Id, SubscriberType.Student, new MemoryStream(file), updateExisting: false);
        Assert.Equal(2, result.Created);
        Assert.Equal(3, result.Errors.Count);
        Assert.Contains(result.Errors, e => e.Row == 4 && e.Message.Contains("mobile"));
        Assert.Contains(result.Errors, e => e.Row == 5 && e.Message.Contains("Unknown package"));
        Assert.Contains(result.Errors, e => e.Row == 6 && e.Message.Contains("Duplicate"));
        Assert.Single(await db.SubscriberGroups.Where(g => g.OrganizationId == a.Id && g.Name == "Class 10A").ToListAsync());

        var again = Book(new[] { "Identifier", "FullName" }, new[] { "S-002", "Bilal Khan" });
        var upd = await subs.ImportAsync(a.Id, SubscriberType.Student, new MemoryStream(again), updateExisting: true);
        Assert.Equal(1, upd.Updated);
        Assert.Equal("Bilal Khan", (await subs.ListAsync(a.Id, new SubscriberQuery { Search = "S-002" })).Items.Single().FullName);

        await Assert.ThrowsAsync<ValidationFailedException>(() => subs.ImportAsync(a.Id, SubscriberType.Student, new MemoryStream(Book(new[] { "Name" }, new[] { "x" })), false));
        await Assert.ThrowsAsync<ValidationFailedException>(() => subs.ImportAsync(a.Id, SubscriberType.Student, new MemoryStream(new byte[] { 1, 2, 3 }), false));

        var export = await subs.ExportAsync(a.Id, new SubscriberQuery { Type = SubscriberType.Student });
        using var book = new XLWorkbook(new MemoryStream(export.Content));
        var sheet = book.Worksheet(1);
        Assert.Equal("Identifier", sheet.Cell(1, 2).GetString());
        Assert.Equal(3, sheet.LastRowUsed()!.RowNumber());
        Assert.DoesNotContain("pass1234", string.Join(",", sheet.CellsUsed().Select(c => c.GetString())));
    }

    [Fact]
    public async Task Packages_validate_names_and_block_deleting_in_use_package()
    {
        var env = await TestEnv.CreateAsync();
        var a = await env.RegisterAsync("ISP", "Pkg ISP", "pkgadmin");
        using var db = env.NewDb();
        var admin = FakeCurrentUser.OrgAdmin(a.Id);
        var p = await Packages(db, admin).CreateAsync(a.Id, new UpsertPackageRequest { Name = "Home 50", DownloadKbps = 51200, UploadKbps = 25600, DataLimitMb = 100_000, ValidityMinutes = 43200, MaxDevices = 3, Price = 499 });
        await Assert.ThrowsAsync<ConflictException>(() => Packages(db, admin).CreateAsync(a.Id, new UpsertPackageRequest { Name = "home 50" }));
        await Subs(env, db, admin).CreateAsync(a.Id, new CreateSubscriberRequest { SubscriberType = SubscriberType.Customer, Identifier = "cust1", FullName = "C", PackageId = p.Id });
        await Assert.ThrowsAsync<ConflictException>(() => Packages(db, admin).DeleteAsync(a.Id, p.Id));
        Assert.Equal(1, (await Packages(db, admin).ListAsync(a.Id, null)).Single().SubscriberCount);
    }
}
