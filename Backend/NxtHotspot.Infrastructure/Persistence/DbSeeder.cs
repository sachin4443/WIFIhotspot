using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NxtHotspot.Application.Common;
using NxtHotspot.Domain.Entities;
using NxtHotspot.Domain.Enums;

namespace NxtHotspot.Infrastructure.Persistence;

/// <summary>Idempotent seed: org types, plans, permissions, system roles, optional bootstrap Super Admin.</summary>
public sealed class DbSeeder
{
    private readonly NxtDbContext _db;
    private readonly IPasswordService _passwords;
    private readonly IConfiguration _config;
    private readonly ILogger<DbSeeder> _logger;

    public DbSeeder(NxtDbContext db, IPasswordService passwords, IConfiguration config, ILogger<DbSeeder> logger)
    {
        _db = db; _passwords = passwords; _config = config; _logger = logger;
    }

    private static readonly (string Name, string Description, AuthenticationType[] Methods)[] Types =
    {
        ("Hotel", "Hotels and guest houses", new[] { AuthenticationType.RoomMobile, AuthenticationType.MobileOTP, AuthenticationType.Voucher }),
        ("Resort", "Resorts", new[] { AuthenticationType.RoomMobile, AuthenticationType.MobileOTP, AuthenticationType.Voucher }),
        ("School", "Schools", new[] { AuthenticationType.StudentPassword, AuthenticationType.StaffPassword, AuthenticationType.GuestOTP }),
        ("College", "Colleges", new[] { AuthenticationType.StudentPassword, AuthenticationType.StudentOTP, AuthenticationType.StaffPassword, AuthenticationType.GuestOTP }),
        ("University", "Universities", new[] { AuthenticationType.StudentPassword, AuthenticationType.StudentOTP, AuthenticationType.StaffPassword, AuthenticationType.GuestOTP }),
        ("Hospital", "Hospitals and clinics", new[] { AuthenticationType.MobileOTP }),
        ("Hostel", "Hostels and PGs", new[] { AuthenticationType.UsernamePassword, AuthenticationType.MobileOTP }),
        ("Cafe", "Cafes", new[] { AuthenticationType.MobileOTP, AuthenticationType.Voucher }),
        ("Restaurant", "Restaurants", new[] { AuthenticationType.MobileOTP, AuthenticationType.Voucher }),
        ("Office", "Offices", new[] { AuthenticationType.EmployeePassword, AuthenticationType.MobileOTP, AuthenticationType.Voucher }),
        ("Corporate", "Corporate campuses", new[] { AuthenticationType.EmployeePassword, AuthenticationType.MobileOTP, AuthenticationType.Voucher }),
        ("Public WiFi", "Public WiFi zones", new[] { AuthenticationType.MobileOTP, AuthenticationType.Voucher }),
        ("ISP", "Internet service providers", new[] { AuthenticationType.UsernamePassword, AuthenticationType.Voucher, AuthenticationType.MobileOTP }),
        ("Enterprise", "Enterprises and business campuses", new[] { AuthenticationType.EmployeePassword, AuthenticationType.MobileOTP, AuthenticationType.Voucher }),
        ("Shopping Mall", "Shopping malls and retail centres", new[] { AuthenticationType.MobileOTP, AuthenticationType.Voucher }),
        ("Railway / Transport", "Railway stations, airports, bus terminals and transit", new[] { AuthenticationType.MobileOTP, AuthenticationType.Voucher }),
        ("Other", "Other organization types", new[] { AuthenticationType.MobileOTP, AuthenticationType.UsernamePassword, AuthenticationType.Voucher }),
    };

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await SeedOrganizationTypesAsync(ct);
        await SeedSubscriptionPlansAsync(ct);
        await SeedPermissionsAndRolesAsync(ct);
        await SeedSuperAdminAsync(ct);
    }

    private async Task SeedOrganizationTypesAsync(CancellationToken ct)
    {
        var existing = (await _db.OrganizationTypes.Select(t => t.TypeName).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, description, methods) in Types)
        {
            if (existing.Contains(name)) continue; // never overwrite what a Super Admin has edited
            var type = new OrganizationType { TypeName = name, Description = description };
            for (var i = 0; i < methods.Length; i++)
            {
                var (otp, booking) = LoginMethodDefaults.FlagsFor(methods[i]);
                type.DefaultLoginMethods.Add(new OrganizationTypeDefaultLoginMethod
                {
                    AuthenticationType = methods[i],
                    IsDefault = i == 0,
                    RequireOTP = otp,
                    RequireBookingValidation = booking,
                    AllowMultipleDevices = true,
                    MaxDevices = 3
                });
            }
            _db.OrganizationTypes.Add(type);
        }
        await _db.SaveChangesAsync(ct);
    }

    private async Task SeedSubscriptionPlansAsync(CancellationToken ct)
    {
        if (await _db.SubscriptionPlans.AnyAsync(ct)) return;
        _db.SubscriptionPlans.AddRange(
            new SubscriptionPlan { Name = "Starter", Description = "Single site", MonthlyPrice = 0, MaxSites = 1, MaxNasDevices = 2, MaxUsers = 500 },
            new SubscriptionPlan { Name = "Standard", Description = "Multiple sites", MonthlyPrice = 0, MaxSites = 5, MaxNasDevices = 20, MaxUsers = 10000 },
            new SubscriptionPlan { Name = "Enterprise", Description = "Unlimited", MonthlyPrice = 0 });
        await _db.SaveChangesAsync(ct);
    }

    private async Task SeedPermissionsAndRolesAsync(CancellationToken ct)
    {
        var existingCodes = (await _db.Permissions.Select(p => p.Code).ToListAsync(ct)).ToHashSet();
        // Permissions added by a newer release are also granted to existing system roles whose defaults include them.
        // Permissions that already existed are never re-added, so an administrator's removals are respected.
        var newlyAdded = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (code, description, platformOnly) in Permissions.All)
            if (!existingCodes.Contains(code))
            {
                _db.Permissions.Add(new Permission { Code = code, Description = description, IsPlatformOnly = platformOnly });
                newlyAdded.Add(code);
            }
        await _db.SaveChangesAsync(ct);
        var isUpgrade = existingCodes.Count > 0;

        var perms = await _db.Permissions.ToListAsync(ct);
        var roles = await _db.Roles.Include(r => r.RolePermissions).Where(r => r.OrganizationId == null).ToListAsync(ct);

        void Ensure(string name, string description, bool platformRole, Func<Permission, bool> select, bool alwaysSync)
        {
            var role = roles.FirstOrDefault(r => r.Name == name);
            var sync = alwaysSync;
            if (role == null)
            {
                role = new Role { Name = name, Description = description, IsSystem = true, IsPlatformRole = platformRole };
                _db.Roles.Add(role);
                roles.Add(role);
                sync = true;
            }
            var candidates = sync ? perms.Where(select) : isUpgrade ? perms.Where(p => newlyAdded.Contains(p.Code)).Where(select) : Enumerable.Empty<Permission>();
            foreach (var p in candidates)
                if (role.RolePermissions.All(rp => rp.PermissionId != p.Id))
                    role.RolePermissions.Add(new RolePermission { PermissionId = p.Id });
        }

        Ensure(SystemRoles.SuperAdmin, "Platform owner with full access", true, _ => true, alwaysSync: true);
        Ensure(SystemRoles.OrgAdmin, "Organization administrator", false, p => !p.IsPlatformOnly, alwaysSync: true);
        Ensure(SystemRoles.SiteAdmin, "Site-level administrator", false,
            p => Permissions.SiteAdminDefaults.Contains(p.Code), alwaysSync: false);
        Ensure(SystemRoles.ReadOnly, "Read-only access", false,
            p => !p.IsPlatformOnly && p.Code.EndsWith(".read", StringComparison.Ordinal), alwaysSync: false);

        await _db.SaveChangesAsync(ct);
    }

    private async Task SeedSuperAdminAsync(CancellationToken ct)
    {
        var exists = await _db.AdminUsers.AnyAsync(
            u => u.OrganizationId == null && u.UserRoles.Any(ur => ur.Role!.Name == SystemRoles.SuperAdmin), ct);
        if (exists) return;

        var username = _config["Bootstrap:SuperAdmin:Username"];
        var password = _config["Bootstrap:SuperAdmin:Password"];
        var email = _config["Bootstrap:SuperAdmin:Email"] ?? "superadmin@localhost";
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning("No Super Admin exists and Bootstrap:SuperAdmin:Username/Password are not configured. " +
                               "Set them via environment variables or user-secrets and restart.");
            return;
        }

        var error = PasswordPolicy.Validate(password);
        if (error != null) throw new InvalidOperationException("Bootstrap Super Admin password rejected: " + error);

        var role = await _db.Roles.FirstAsync(r => r.OrganizationId == null && r.Name == SystemRoles.SuperAdmin, ct);
        var user = new AdminUser
        {
            Username = username.Trim(),
            NormalizedUsername = username.Trim().ToUpperInvariant(),
            PasswordHash = _passwords.Hash(password),
            FullName = "Super Admin",
            Email = email,
            HasAllSitesAccess = true
        };
        user.UserRoles.Add(new AdminUserRole { RoleId = role.Id });
        _db.AdminUsers.Add(user);
        _db.AuditLogs.Add(Services.Audit.Entry(null, null, "BootstrapSuperAdmin", nameof(AdminUser), user.Id.ToString()));
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Bootstrap Super Admin '{Username}' created. Remove the bootstrap password from configuration.", user.Username);
    }
}
