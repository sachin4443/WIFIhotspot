using System.Security.Claims;
using NxtHotspot.Application.Common;

namespace NxtHotspot.API.Security;

/// <summary>Reads the caller's identity from the validated JWT. Tenant identity always comes from here, never from request bodies.</summary>
public sealed class HttpContextCurrentUser : ICurrentUser
{
    private readonly ClaimsPrincipal? _user;

    public HttpContextCurrentUser(IHttpContextAccessor accessor) => _user = accessor.HttpContext?.User;

    public Guid? AdminUserId => Guid.TryParse(_user?.FindFirstValue("sub"), out var id) ? id : null;

    public Guid? OrganizationId => Guid.TryParse(_user?.FindFirstValue("org_id"), out var id) ? id : null;

    public bool IsPlatformAdmin => _user?.Identity?.IsAuthenticated == true && OrganizationId == null;

    public bool HasAllSites => _user?.FindFirstValue("all_sites") == "true";

    public IReadOnlySet<string> Permissions =>
        _user?.FindAll("perm").Select(c => c.Value).ToHashSet() ?? new HashSet<string>();
}
