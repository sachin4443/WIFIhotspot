using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NxtHotspot.Application.Common;
using NxtHotspot.Application.Dtos;

namespace NxtHotspot.API.Controllers;

[ApiController]
public sealed class RolesController : ControllerBase
{
    private readonly IRoleService _roles;
    public RolesController(IRoleService roles) => _roles = roles;

    [HttpGet("api/roles")]
    [Authorize(Policy = Permissions.RolesRead)]
    public async Task<ActionResult<List<RoleDto>>> ListRoles(CancellationToken ct) => Ok(await _roles.ListRolesAsync(ct));

    [HttpGet("api/permissions")]
    [Authorize(Policy = Permissions.RolesRead)]
    public async Task<ActionResult<List<PermissionDto>>> ListPermissions(CancellationToken ct) => Ok(await _roles.ListPermissionsAsync(ct));

    [HttpPost("api/roles")]
    [Authorize(Policy = Permissions.RolesWrite)]
    public async Task<ActionResult<RoleDto>> Create([FromBody] CreateRoleRequest request, CancellationToken ct)
    {
        var role = await _roles.CreateRoleAsync(request, ct);
        return Created($"api/roles/{role.Id}", role);
    }

    [HttpPut("api/roles/{id:guid}/permissions")]
    [Authorize(Policy = Permissions.RolesWrite)]
    public async Task<ActionResult<RoleDto>> SetPermissions(Guid id, [FromBody] SetRolePermissionsRequest request, CancellationToken ct) =>
        Ok(await _roles.SetRolePermissionsAsync(id, request.PermissionCodes, ct));

    [HttpDelete("api/roles/{id:guid}")]
    [Authorize(Policy = Permissions.RolesWrite)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _roles.DeleteRoleAsync(id, ct);
        return NoContent();
    }
}
