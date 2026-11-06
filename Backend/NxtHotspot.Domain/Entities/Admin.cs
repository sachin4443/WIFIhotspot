using NxtHotspot.Domain.Common;

namespace NxtHotspot.Domain.Entities;

public class AdminUser : BaseEntity
{
    /// <summary>Null = platform (Super Admin side) user.</summary>
    public Guid? OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    public string Username { get; set; } = "";
    public string NormalizedUsername { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? MobileNumber { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>When false the user can only access sites listed in AdminUserSites.</summary>
    public bool HasAllSitesAccess { get; set; }
    public int FailedLoginCount { get; set; }
    public DateTime? LockoutEnd { get; set; }
    public DateTime? LastLoginAt { get; set; }

    public ICollection<AdminUserRole> UserRoles { get; set; } = new List<AdminUserRole>();
    public ICollection<AdminUserSite> UserSites { get; set; } = new List<AdminUserSite>();
}

public class Role : BaseEntity
{
    /// <summary>Null = system role; set = custom role owned by that organization.</summary>
    public Guid? OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool IsSystem { get; set; }
    /// <summary>Platform roles can only be assigned by platform admins.</summary>
    public bool IsPlatformRole { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

public class Permission : BaseEntity
{
    public string Code { get; set; } = "";
    public string? Description { get; set; }
    /// <summary>Platform-only permissions can never be placed in an organization role.</summary>
    public bool IsPlatformOnly { get; set; }
}

public class RolePermission
{
    public Guid RoleId { get; set; }
    public Role? Role { get; set; }
    public Guid PermissionId { get; set; }
    public Permission? Permission { get; set; }
}

public class AdminUserRole
{
    public Guid AdminUserId { get; set; }
    public AdminUser? AdminUser { get; set; }
    public Guid RoleId { get; set; }
    public Role? Role { get; set; }
}

public class AdminUserSite
{
    public Guid AdminUserId { get; set; }
    public AdminUser? AdminUser { get; set; }
    public Guid SiteId { get; set; }
    public Site? Site { get; set; }
}
