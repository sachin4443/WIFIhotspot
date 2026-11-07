using System.Globalization;
using System.Security.Cryptography;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using NxtHotspot.Application.Common;
using NxtHotspot.Application.Dtos;
using NxtHotspot.Domain.Entities;
using NxtHotspot.Domain.Enums;
using NxtHotspot.Infrastructure.Persistence;

namespace NxtHotspot.Infrastructure.Services;

public sealed class SubscriberService : ISubscriberService
{
    public const int MaxImportRows = 5000;
    public const int MinPasswordLength = 6;
    private const string XlsxType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly NxtDbContext _db;
    private readonly ICurrentUser _current;
    private readonly ITenantAccess _tenant;
    private readonly IPasswordService _passwords;
    private readonly IDisconnectQueue _disconnects;

    public SubscriberService(NxtDbContext db, ICurrentUser current, ITenantAccess tenant, IPasswordService passwords, IDisconnectQueue disconnects)
    {
        _db = db; _current = current; _tenant = tenant; _passwords = passwords; _disconnects = disconnects;
    }

    // ---------- queries ----------

    private async Task<IQueryable<Subscriber>> ScopedAsync(Guid organizationId, CancellationToken ct)
    {
        var sites = await _tenant.AccessibleSiteIdsAsync(organizationId, ct);
        var q = _db.Subscribers.AsNoTracking().Where(s => s.OrganizationId == organizationId);
        if (sites != null) q = q.Where(s => s.SiteId == null || sites.Contains(s.SiteId.Value));
        return q;
    }

    private static IQueryable<Subscriber> Filter(IQueryable<Subscriber> q, SubscriberQuery f)
    {
        if (f.Type != null) q = q.Where(s => s.SubscriberType == f.Type);
        if (f.Status != null) q = q.Where(s => s.Status == f.Status);
        if (f.SiteId != null) q = q.Where(s => s.SiteId == f.SiteId);
        if (f.GroupId != null) q = q.Where(s => s.GroupId == f.GroupId);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var t = f.Search.Trim().ToLower();
            q = q.Where(s => s.NormalizedIdentifier.Contains(t.ToUpper()) || s.FullName.ToLower().Contains(t)
                             || (s.MobileNumber != null && s.MobileNumber.Contains(t)) || (s.RoomNumber != null && s.RoomNumber.ToLower() == t)
                             || (s.Email != null && s.Email.ToLower().Contains(t)));
        }
        return q;
    }

    public async Task<PagedResult<SubscriberDto>> ListAsync(Guid organizationId, SubscriberQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize, 1, 200);
        var q = Filter(await ScopedAsync(organizationId, ct), query);
        var total = await q.CountAsync(ct);
        var items = await Project(q.OrderByDescending(s => s.CreatedAt).ThenBy(s => s.Identifier).Skip((page - 1) * size).Take(size)).ToListAsync(ct);
        return new PagedResult<SubscriberDto>(items, page, size, total);
    }

    public async Task<SubscriberDto> GetAsync(Guid organizationId, Guid subscriberId, CancellationToken ct = default)
    {
        var q = (await ScopedAsync(organizationId, ct)).Where(s => s.Id == subscriberId);
        return await Project(q).FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Subscriber not found.");
    }

    private IQueryable<SubscriberDto> Project(IQueryable<Subscriber> q) => q.Select(s => new SubscriberDto(
        s.Id, s.OrganizationId, s.SiteId, s.Site != null ? s.Site.Name : null, s.SubscriberType, s.Identifier, s.FullName, s.MobileNumber, s.Email,
        s.PasswordHash != null, s.GroupId, s.Group != null ? s.Group.Name : null, s.PackageId, s.Package != null ? s.Package.Name : null,
        s.RoomNumber, s.CheckInAt, s.CheckOutAt, s.ValidUntil, s.Status, s.MaxDevices, s.Notes, s.LastLoginAt,
        _db.RadiusSessions.Count(r => r.SubscriberId == s.Id && r.Status == RadiusSessionStatus.Active), s.CreatedAt));

    // ---------- commands ----------

    public async Task<SubscriberDto> CreateAsync(Guid organizationId, CreateSubscriberRequest r, CancellationToken ct = default)
    {
        await HotspotScope.EnsureWritableAsync(_tenant, _current, organizationId, r.SiteId, ct);
        await EnsureCapacityAsync(organizationId, 1, ct);
        var s = await BuildAsync(organizationId, r, ct);
        _db.Subscribers.Add(s);
        _db.AuditLogs.Add(Audit.Entry(organizationId, _current.AdminUserId, s.SubscriberType == SubscriberType.RoomGuest ? "GuestCheckedIn" : "SubscriberCreated",
            nameof(Subscriber), s.Id.ToString(), $"Type={s.SubscriberType}, Id={s.Identifier}"));
        await SaveAsync(ct);
        return await GetAsync(organizationId, s.Id, ct);
    }

    public async Task<SubscriberDto> UpdateAsync(Guid organizationId, Guid subscriberId, UpdateSubscriberRequest r, CancellationToken ct = default)
    {
        var s = await LoadWritableAsync(organizationId, subscriberId, ct);
        await HotspotScope.EnsureWritableAsync(_tenant, _current, organizationId, r.SiteId, ct);
        s.FullName = r.FullName.Trim();
        s.MobileNumber = NormalizeMobile(r.MobileNumber);
        s.Email = Clean(r.Email);
        s.SiteId = r.SiteId;
        s.GroupId = await ValidGroupAsync(organizationId, r.GroupId, ct);
        s.PackageId = await ValidPackageAsync(organizationId, r.PackageId, r.SiteId, ct);
        s.RoomNumber = Clean(r.RoomNumber)?.ToUpperInvariant();
        s.CheckInAt = r.CheckInAt?.ToUniversalTime();
        s.CheckOutAt = r.CheckOutAt?.ToUniversalTime();
        s.ValidUntil = r.ValidUntil?.ToUniversalTime();
        s.MaxDevices = r.MaxDevices;
        s.Notes = Clean(r.Notes);
        if (r.ClearPassword) s.PasswordHash = null;
        else if (!string.IsNullOrEmpty(r.NewPassword)) s.PasswordHash = HashPassword(r.NewPassword);
        ValidateTypeRules(s);
        _db.AuditLogs.Add(Audit.Entry(organizationId, _current.AdminUserId, "SubscriberUpdated", nameof(Subscriber), s.Id.ToString(),
            $"Id={s.Identifier}{(!string.IsNullOrEmpty(r.NewPassword) || r.ClearPassword ? ", PasswordChanged" : "")}"));
        await SaveAsync(ct);
        return await GetAsync(organizationId, s.Id, ct);
    }

    public async Task<SubscriberDto> SetStatusAsync(Guid organizationId, Guid subscriberId, SubscriberStatus status, CancellationToken ct = default)
    {
        var s = await LoadWritableAsync(organizationId, subscriberId, ct);
        if (status == SubscriberStatus.CheckedOut && s.SubscriberType != SubscriberType.RoomGuest)
            throw new ValidationFailedException("Only room guests can be checked out.");
        var old = s.Status;
        s.Status = status;
        _db.AuditLogs.Add(Audit.Entry(organizationId, _current.AdminUserId, "SubscriberStatusChanged", nameof(Subscriber), s.Id.ToString(), $"{old} -> {status}"));
        await _db.SaveChangesAsync(ct);
        if (status != SubscriberStatus.Active) await _disconnects.QueueForSubscriberAsync(s.Id, $"Subscriber {status.ToString().ToLower()}", ct);
        return await GetAsync(organizationId, s.Id, ct);
    }

    public async Task<SubscriberDto> CheckOutAsync(Guid organizationId, Guid subscriberId, CancellationToken ct = default)
    {
        var s = await LoadWritableAsync(organizationId, subscriberId, ct);
        if (s.SubscriberType != SubscriberType.RoomGuest) throw new ValidationFailedException("Only room guests can be checked out.");
        s.Status = SubscriberStatus.CheckedOut;
        s.CheckOutAt = DateTime.UtcNow;
        _db.AuditLogs.Add(Audit.Entry(organizationId, _current.AdminUserId, "GuestCheckedOut", nameof(Subscriber), s.Id.ToString(), $"Room={s.RoomNumber}"));
        await _db.SaveChangesAsync(ct);
        await _disconnects.QueueForSubscriberAsync(s.Id, "Guest checked out", ct);
        return await GetAsync(organizationId, s.Id, ct);
    }

    public async Task DeleteAsync(Guid organizationId, Guid subscriberId, CancellationToken ct = default)
    {
        var s = await LoadWritableAsync(organizationId, subscriberId, ct);
        if (await _db.RadiusSessions.AnyAsync(x => x.SubscriberId == subscriberId, ct))
            throw new ConflictException("This subscriber has session history. Suspend the account instead of deleting it.");
        _db.Subscribers.Remove(s);
        _db.AuditLogs.Add(Audit.Entry(organizationId, _current.AdminUserId, "SubscriberDeleted", nameof(Subscriber), s.Id.ToString(), $"Id={s.Identifier}"));
        await _db.SaveChangesAsync(ct);
    }

    // ---------- Excel ----------

    private static readonly string[] CommonColumns = { "Identifier", "FullName", "MobileNumber", "Email", "Password", "SiteCode", "Group", "Package", "ValidUntil", "Notes" };
    private static readonly string[] RoomColumns = { "SiteCode", "RoomNumber", "FullName", "MobileNumber", "Email", "CheckInAt", "CheckOutAt", "Package", "Notes" };

    private static string[] ColumnsFor(SubscriberType type) => type == SubscriberType.RoomGuest ? RoomColumns : CommonColumns;

    public FileResultDto ImportTemplate(SubscriberType type)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(type.ToString());
        var cols = ColumnsFor(type);
        for (var i = 0; i < cols.Length; i++) ws.Cell(1, i + 1).Value = cols[i];
        ws.Row(1).Style.Font.Bold = true;
        var help = wb.Worksheets.Add("Instructions");
        help.Cell(1, 1).Value = $"Import {type} subscribers. One row per subscriber; keep the header row.";
        help.Cell(2, 1).Value = type == SubscriberType.RoomGuest
            ? "SiteCode, RoomNumber, FullName, MobileNumber and CheckOutAt are required. Dates: yyyy-MM-dd HH:mm (organization local time is not applied; use UTC)."
            : "Identifier and FullName are required. Group is created if it does not exist. Package must match an existing package name. Password: at least 6 characters, optional.";
        help.Cell(3, 1).Value = $"At most {MaxImportRows} rows per file.";
        ws.Columns().AdjustToContents();
        return Save(wb, $"nxt-{type.ToString().ToLower()}-import-template.xlsx");
    }

    public async Task<ImportResultDto> ImportAsync(Guid organizationId, SubscriberType type, Stream xlsx, bool updateExisting, CancellationToken ct = default)
    {
        _tenant.EnsureOrganization(organizationId);
        await HotspotScope.EnsureOrganizationExistsAsync(_db, organizationId, ct);
        XLWorkbook wb;
        try { wb = new XLWorkbook(xlsx); }
        catch (Exception) { throw new ValidationFailedException("The file is not a valid .xlsx workbook."); }
        using var _ = wb;
        var ws = wb.Worksheets.First();
        var header = ws.Row(1).CellsUsed().ToDictionary(c => c.GetString().Trim().ToLowerInvariant(), c => c.Address.ColumnNumber);
        var required = type == SubscriberType.RoomGuest ? new[] { "sitecode", "roomnumber", "fullname", "mobilenumber", "checkoutat" } : new[] { "identifier", "fullname" };
        var missing = required.Where(h => !header.ContainsKey(h)).ToList();
        if (missing.Count > 0) throw new ValidationFailedException($"Missing column(s): {string.Join(", ", missing)}. Download the template for the expected layout.");

        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
        if (lastRow - 1 > MaxImportRows) throw new ValidationFailedException($"At most {MaxImportRows} rows can be imported at once.");

        string? Get(IXLRow row, string col) => header.TryGetValue(col, out var i) ? Clean(row.Cell(i).GetFormattedString()) : null;
        DateTime? GetDate(IXLRow row, string col)
        {
            if (!header.TryGetValue(col, out var i)) return null;
            var cell = row.Cell(i);
            if (cell.IsEmpty()) return null;
            if (cell.DataType == XLDataType.DateTime) return DateTime.SpecifyKind(cell.GetDateTime(), DateTimeKind.Utc);
            if (DateTime.TryParse(cell.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var d)) return d;
            throw new ValidationFailedException($"'{cell.GetString()}' is not a valid date in column {col}.");
        }

        var sites = await _db.Sites.AsNoTracking().Where(s => s.OrganizationId == organizationId).ToDictionaryAsync(s => s.Code.ToUpperInvariant(), s => s.Id, ct);
        var packages = (await _db.InternetPackages.AsNoTracking().Where(p => p.OrganizationId == organizationId).ToListAsync(ct))
            .GroupBy(p => p.Name.ToLowerInvariant()).ToDictionary(g => g.Key, g => g.First().Id);
        var groupType = type switch { SubscriberType.Student => SubscriberGroupType.Class, SubscriberType.RoomGuest => SubscriberGroupType.Floor, _ => SubscriberGroupType.Department };
        var groups = (await _db.SubscriberGroups.Where(g => g.OrganizationId == organizationId && g.GroupType == groupType).ToListAsync(ct))
            .ToDictionary(g => g.Name.ToLowerInvariant(), g => g);

        var errors = new List<ImportRowError>();
        int created = 0, updated = 0, skipped = 0;
        var seen = new HashSet<string>();

        for (var rowNo = 2; rowNo <= lastRow; rowNo++)
        {
            var row = ws.Row(rowNo);
            if (row.IsEmpty()) continue;
            try
            {
                var siteCode = Get(row, "sitecode");
                Guid? siteId = null;
                if (siteCode != null)
                {
                    if (!sites.TryGetValue(siteCode.ToUpperInvariant(), out var sid)) throw new ValidationFailedException($"Unknown site code '{siteCode}'.");
                    siteId = sid;
                }
                await HotspotScope.EnsureWritableAsync(_tenant, _current, organizationId, siteId, ct);

                Guid? packageId = null;
                var pkg = Get(row, "package");
                if (pkg != null)
                {
                    if (!packages.TryGetValue(pkg.ToLowerInvariant(), out var pid)) throw new ValidationFailedException($"Unknown package '{pkg}'.");
                    packageId = pid;
                }
                Guid? groupId = null;
                var groupName = Get(row, "group");
                if (groupName != null)
                {
                    if (!groups.TryGetValue(groupName.ToLowerInvariant(), out var g))
                    {
                        g = new SubscriberGroup { OrganizationId = organizationId, Name = groupName, GroupType = groupType };
                        _db.SubscriberGroups.Add(g);
                        groups[groupName.ToLowerInvariant()] = g;
                    }
                    groupId = g.Id;
                }

                var req = new CreateSubscriberRequest
                {
                    SubscriberType = type, Identifier = Get(row, "identifier"), FullName = Get(row, "fullname") ?? "", MobileNumber = Get(row, "mobilenumber"),
                    Email = Get(row, "email"), Password = Get(row, "password"), SiteId = siteId, GroupId = groupId, PackageId = packageId,
                    RoomNumber = Get(row, "roomnumber"), CheckInAt = GetDate(row, "checkinat"), CheckOutAt = GetDate(row, "checkoutat"),
                    ValidUntil = GetDate(row, "validuntil"), Notes = Get(row, "notes"),
                };
                if (string.IsNullOrWhiteSpace(req.FullName)) throw new ValidationFailedException("FullName is required.");

                var key = (req.Identifier ?? $"{siteCode}|{req.RoomNumber}|{req.MobileNumber}").ToUpperInvariant();
                if (!seen.Add(key)) throw new ValidationFailedException("Duplicate row in this file.");

                var existing = req.Identifier == null ? null
                    : await _db.Subscribers.FirstOrDefaultAsync(s => s.OrganizationId == organizationId && s.NormalizedIdentifier == req.Identifier.Trim().ToUpperInvariant(), ct);
                if (existing != null)
                {
                    if (!updateExisting) { skipped++; errors.Add(new ImportRowError(rowNo, $"'{req.Identifier}' already exists (skipped).")); continue; }
                    if (existing.SubscriberType != type) throw new ValidationFailedException($"'{req.Identifier}' exists as a {existing.SubscriberType}.");
                    existing.FullName = req.FullName.Trim();
                    existing.MobileNumber = NormalizeMobile(req.MobileNumber) ?? existing.MobileNumber;
                    existing.Email = Clean(req.Email) ?? existing.Email;
                    existing.SiteId = siteId ?? existing.SiteId;
                    existing.GroupId = groupId ?? existing.GroupId;
                    existing.PackageId = packageId ?? existing.PackageId;
                    existing.ValidUntil = req.ValidUntil ?? existing.ValidUntil;
                    existing.Notes = req.Notes ?? existing.Notes;
                    if (!string.IsNullOrEmpty(req.Password)) existing.PasswordHash = HashPassword(req.Password);
                    ValidateTypeRules(existing);
                    updated++;
                }
                else
                {
                    var s = await BuildAsync(organizationId, req, ct);
                    _db.Subscribers.Add(s);
                    created++;
                }
            }
            catch (AppException ex)
            {
                errors.Add(new ImportRowError(rowNo, ex.Message));
            }
        }

        if (created > 0) await EnsureCapacityAsync(organizationId, created, ct);
        _db.AuditLogs.Add(Audit.Entry(organizationId, _current.AdminUserId, "SubscribersImported", nameof(Subscriber), null,
            $"Type={type}, Created={created}, Updated={updated}, Errors={errors.Count}"));
        await SaveAsync(ct);
        return new ImportResultDto(created, updated, skipped, errors);
    }

    public async Task<FileResultDto> ExportAsync(Guid organizationId, SubscriberQuery query, CancellationToken ct = default)
    {
        var rows = await Project(Filter(await ScopedAsync(organizationId, ct), query).OrderBy(s => s.SubscriberType).ThenBy(s => s.Identifier).Take(50_000)).ToListAsync(ct);
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Subscribers");
        string[] headers = { "Type", "Identifier", "FullName", "MobileNumber", "Email", "Site", "Group", "Package", "RoomNumber", "CheckInAt (UTC)", "CheckOutAt (UTC)", "ValidUntil (UTC)", "Status", "HasPassword", "LastLoginAt (UTC)", "CreatedAt (UTC)" };
        for (var i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
        ws.Row(1).Style.Font.Bold = true;
        var r = 2;
        foreach (var s in rows)
        {
            object?[] v = { s.SubscriberType.ToString(), s.Identifier, s.FullName, s.MobileNumber, s.Email, s.SiteName, s.GroupName, s.PackageName, s.RoomNumber,
                s.CheckInAt, s.CheckOutAt, s.ValidUntil, s.Status.ToString(), s.HasPassword ? "Yes" : "No", s.LastLoginAt, s.CreatedAt };
            for (var i = 0; i < v.Length; i++)
            {
                var cell = ws.Cell(r, i + 1);
                if (v[i] is DateTime d) { cell.Value = d; cell.Style.DateFormat.Format = "yyyy-mm-dd hh:mm"; }
                else cell.Value = v[i]?.ToString() ?? "";
            }
            r++;
        }
        ws.Columns().AdjustToContents(1, Math.Min(r, 200));
        _db.AuditLogs.Add(Audit.Entry(organizationId, _current.AdminUserId, "SubscribersExported", nameof(Subscriber), null, $"Rows={rows.Count}"));
        await _db.SaveChangesAsync(ct);
        return Save(wb, $"nxt-subscribers-{DateTime.UtcNow:yyyyMMdd-HHmm}.xlsx");
    }

    // ---------- helpers ----------

    private async Task<Subscriber> BuildAsync(Guid organizationId, CreateSubscriberRequest r, CancellationToken ct)
    {
        var s = new Subscriber
        {
            OrganizationId = organizationId,
            SubscriberType = r.SubscriberType,
            FullName = r.FullName.Trim(),
            MobileNumber = NormalizeMobile(r.MobileNumber),
            Email = Clean(r.Email),
            SiteId = r.SiteId,
            GroupId = await ValidGroupAsync(organizationId, r.GroupId, ct),
            RoomNumber = Clean(r.RoomNumber)?.ToUpperInvariant(),
            CheckInAt = r.CheckInAt?.ToUniversalTime(),
            CheckOutAt = r.CheckOutAt?.ToUniversalTime(),
            ValidUntil = r.ValidUntil?.ToUniversalTime(),
            MaxDevices = r.MaxDevices,
            Notes = Clean(r.Notes),
            Status = SubscriberStatus.Active,
        };
        s.PackageId = await ValidPackageAsync(organizationId, r.PackageId ?? await GroupDefaultPackageAsync(s.GroupId, ct), r.SiteId, ct);
        if (!string.IsNullOrEmpty(r.Password)) s.PasswordHash = HashPassword(r.Password);

        if (s.SubscriberType == SubscriberType.RoomGuest)
        {
            s.CheckInAt ??= DateTime.UtcNow;
            var site = s.SiteId == null ? null : await _db.Sites.AsNoTracking().FirstOrDefaultAsync(x => x.Id == s.SiteId, ct);
            var suffix = Convert.ToHexString(RandomNumberGenerator.GetBytes(3));
            s.Identifier = $"RG-{site?.Code ?? "SITE"}-{s.RoomNumber}-{suffix}";
        }
        else if (s.SubscriberType == SubscriberType.Guest && string.IsNullOrWhiteSpace(r.Identifier))
        {
            s.Identifier = s.MobileNumber ?? throw new ValidationFailedException("Guests need a mobile number or an identifier.");
        }
        else
        {
            s.Identifier = Clean(r.Identifier) ?? throw new ValidationFailedException(IdentifierLabel(s.SubscriberType) + " is required.");
        }
        if (s.Identifier.Length > 100) throw new ValidationFailedException("The identifier is too long.");
        s.NormalizedIdentifier = s.Identifier.ToUpperInvariant();
        ValidateTypeRules(s);

        if (await _db.Subscribers.AnyAsync(x => x.OrganizationId == organizationId && x.NormalizedIdentifier == s.NormalizedIdentifier, ct))
            throw new ConflictException($"{IdentifierLabel(s.SubscriberType)} '{s.Identifier}' already exists.");
        return s;
    }

    private static string IdentifierLabel(SubscriberType t) => t switch
    {
        SubscriberType.Student => "Student ID",
        SubscriberType.Employee => "Employee ID",
        SubscriberType.Teacher or SubscriberType.Staff => "Staff ID",
        SubscriberType.Customer => "Username",
        _ => "Identifier",
    };

    private static void ValidateTypeRules(Subscriber s)
    {
        if (s.SubscriberType == SubscriberType.RoomGuest)
        {
            if (s.SiteId == null) throw new ValidationFailedException("Room guests must belong to a site (room numbers are only unique within a site).");
            if (string.IsNullOrWhiteSpace(s.RoomNumber)) throw new ValidationFailedException("Room number is required for room guests.");
            if (string.IsNullOrWhiteSpace(s.MobileNumber)) throw new ValidationFailedException("The guest's registered mobile number is required.");
            if (s.CheckOutAt == null) throw new ValidationFailedException("Check-out date and time are required.");
            if (s.CheckInAt != null && s.CheckOutAt <= s.CheckInAt) throw new ValidationFailedException("Check-out must be after check-in.");
        }
        if (s.SubscriberType == SubscriberType.Guest && string.IsNullOrWhiteSpace(s.MobileNumber) && string.IsNullOrWhiteSpace(s.Identifier))
            throw new ValidationFailedException("Guests need a mobile number.");
    }

    private string HashPassword(string password)
    {
        if (password.Length < MinPasswordLength || password.Length > 128)
            throw new ValidationFailedException($"Subscriber passwords must be {MinPasswordLength}–128 characters.");
        return _passwords.Hash(password);
    }

    private async Task<Guid?> ValidGroupAsync(Guid organizationId, Guid? groupId, CancellationToken ct)
    {
        if (groupId is not { } id) return null;
        var local = _db.SubscriberGroups.Local.FirstOrDefault(g => g.Id == id);
        if (local != null) return local.OrganizationId == organizationId ? id : throw new ValidationFailedException("The group does not belong to this organization.");
        if (!await _db.SubscriberGroups.AnyAsync(g => g.Id == id && g.OrganizationId == organizationId, ct))
            throw new ValidationFailedException("The group does not belong to this organization.");
        return id;
    }

    private async Task<Guid?> GroupDefaultPackageAsync(Guid? groupId, CancellationToken ct) =>
        groupId == null ? null : (_db.SubscriberGroups.Local.FirstOrDefault(g => g.Id == groupId)?.DefaultPackageId
                                  ?? await _db.SubscriberGroups.Where(g => g.Id == groupId).Select(g => g.DefaultPackageId).FirstOrDefaultAsync(ct));

    private async Task<Guid?> ValidPackageAsync(Guid organizationId, Guid? packageId, Guid? siteId, CancellationToken ct)
    {
        if (packageId is not { } id) return null;
        var p = await _db.InternetPackages.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == organizationId, ct)
                ?? throw new ValidationFailedException("The package does not belong to this organization.");
        if (p.SiteId != null && p.SiteId != siteId) throw new ValidationFailedException("This package is limited to another site.");
        return id;
    }

    private async Task EnsureCapacityAsync(Guid organizationId, int adding, CancellationToken ct)
    {
        var max = await _db.Organizations.Where(o => o.Id == organizationId).Select(o => o.SubscriptionPlan != null ? o.SubscriptionPlan.MaxUsers : null).FirstOrDefaultAsync(ct);
        if (max == null) return;
        var count = await _db.Subscribers.CountAsync(s => s.OrganizationId == organizationId && s.Status != SubscriberStatus.CheckedOut && s.Status != SubscriberStatus.Expired, ct);
        if (count + adding > max) throw new ConflictException($"The subscription plan allows {max} subscribers; {count} are in use.");
    }

    private async Task<Subscriber> LoadWritableAsync(Guid organizationId, Guid id, CancellationToken ct)
    {
        _tenant.EnsureOrganization(organizationId);
        var s = await _db.Subscribers.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == organizationId, ct)
                ?? throw new NotFoundException("Subscriber not found.");
        await HotspotScope.EnsureWritableAsync(_tenant, _current, organizationId, s.SiteId, ct);
        return s;
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { throw new ConflictException("A subscriber with this identifier already exists."); }
    }

    public static string? NormalizeMobile(string? m)
    {
        var v = Clean(m);
        if (v == null) return null;
        v = v.Replace(" ", "").Replace("-", "");
        if (!System.Text.RegularExpressions.Regex.IsMatch(v, ValidationPatterns.Mobile)) throw new ValidationFailedException($"'{m}' is not a valid mobile number.");
        return v;
    }

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    private static FileResultDto Save(XLWorkbook wb, string name)
    {
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return new FileResultDto(name, XlsxType, ms.ToArray());
    }
}
