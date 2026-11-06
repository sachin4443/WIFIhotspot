using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NxtHotspot.Application.Common;
using NxtHotspot.Application.Dtos;

namespace NxtHotspot.API.Controllers;

[ApiController]
[Route("api/admin-users")]
public sealed class AdminUsersController : ControllerBase
{
    private readonly IAdminUserService _users;
    public AdminUsersController(IAdminUserService users) => _users = users;

    [HttpGet]
    [Authorize(Policy = Permissions.AdminUsersRead)]
    public async Task<ActionResult<List<AdminUserDto>>> List([FromQuery] Guid? organizationId, CancellationToken ct) =>
        Ok(await _users.ListAsync(organizationId, ct));

    [HttpPost]
    [Authorize(Policy = Permissions.AdminUsersWrite)]
    public async Task<ActionResult<AdminUserDto>> Create([FromBody] CreateAdminUserRequest request, CancellationToken ct)
    {
        var user = await _users.CreateAsync(request, ct);
        return Created($"api/admin-users/{user.Id}", user);
    }

    [HttpPut("{id:guid}/roles")]
    [Authorize(Policy = Permissions.AdminUsersWrite)]
    public async Task<ActionResult<AdminUserDto>> SetRoles(Guid id, [FromBody] SetRolesRequest request, CancellationToken ct) =>
        Ok(await _users.SetRolesAsync(id, request.RoleIds, ct));

    [HttpPut("{id:guid}/sites")]
    [Authorize(Policy = Permissions.AdminUsersWrite)]
    public async Task<ActionResult<AdminUserDto>> SetSites(Guid id, [FromBody] SetSitesRequest request, CancellationToken ct) =>
        Ok(await _users.SetSitesAsync(id, request.SiteIds, request.HasAllSitesAccess, ct));

    [HttpPut("{id:guid}/active")]
    [Authorize(Policy = Permissions.AdminUsersWrite)]
    public async Task<IActionResult> SetActive(Guid id, [FromBody] SetActiveRequest request, CancellationToken ct)
    {
        await _users.SetActiveAsync(id, request.IsActive, ct);
        return NoContent();
    }
}
