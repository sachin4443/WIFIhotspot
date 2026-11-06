using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NxtHotspot.Application.Common;
using NxtHotspot.Application.Dtos;

namespace NxtHotspot.API.Controllers;

[ApiController]
public sealed class SitesController : ControllerBase
{
    private readonly ISiteService _sites;
    public SitesController(ISiteService sites) => _sites = sites;

    [HttpGet("api/organizations/{organizationId:guid}/sites")]
    [Authorize(Policy = Permissions.SitesRead)]
    public async Task<ActionResult<List<SiteDto>>> List(Guid organizationId, CancellationToken ct) =>
        Ok(await _sites.ListAsync(organizationId, ct));

    [HttpPost("api/organizations/{organizationId:guid}/sites")]
    [Authorize(Policy = Permissions.SitesWrite)]
    public async Task<ActionResult<SiteDto>> Create(Guid organizationId, [FromBody] CreateSiteRequest request, CancellationToken ct)
    {
        var site = await _sites.CreateAsync(organizationId, request, ct);
        return Created($"api/sites/{site.Id}", site);
    }

    [HttpPut("api/sites/{siteId:guid}")]
    [Authorize(Policy = Permissions.SitesWrite)]
    public async Task<ActionResult<SiteDto>> Update(Guid siteId, [FromBody] UpdateSiteRequest request, CancellationToken ct) =>
        Ok(await _sites.UpdateAsync(siteId, request, ct));
}
