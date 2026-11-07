using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NxtHotspot.Application.Common;
using NxtHotspot.Application.Dtos;
using NxtHotspot.Domain.Enums;

namespace NxtHotspot.API.Controllers;

[ApiController]
[Route("api/organizations")]
public sealed class OrganizationsController : ControllerBase
{
    private readonly IOrganizationService _orgs;
    public OrganizationsController(IOrganizationService orgs) => _orgs = orgs;

    [HttpGet]
    [Authorize(Policy = Permissions.OrganizationsRead)]
    public async Task<ActionResult<PagedResult<OrganizationSummaryDto>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] string? search = null,
        [FromQuery] OrganizationStatus? status = null, [FromQuery] int? organizationTypeId = null, CancellationToken ct = default) =>
        Ok(await _orgs.ListAsync(page, pageSize, search, ct, new OrganizationListFilter(status, organizationTypeId)));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.OrganizationsRead)]
    public async Task<ActionResult<OrganizationDetailDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await _orgs.GetAsync(id, ct));

    /// <summary>Super Admin registers an organization: org, primary site, admin, settings, SMS, login methods, wallet.</summary>
    [HttpPost]
    [Authorize(Policy = Permissions.OrganizationsWrite)]
    public async Task<ActionResult<OrganizationDetailDto>> Register([FromBody] RegisterOrganizationRequest request, CancellationToken ct)
    {
        var created = await _orgs.RegisterAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}/status")]
    [Authorize(Policy = Permissions.OrganizationsWrite)]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] SetOrganizationStatusRequest request, CancellationToken ct)
    {
        await _orgs.SetStatusAsync(id, request.Status!.Value, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api")]
public sealed class ReferenceDataController : ControllerBase
{
    private readonly IOrganizationService _orgs;
    public ReferenceDataController(IOrganizationService orgs) => _orgs = orgs;

    [HttpGet("organization-types")]
    [Authorize(Policy = Permissions.OrganizationTypesRead)]
    public async Task<ActionResult<List<OrganizationTypeDto>>> Types([FromQuery] bool includeInactive = false, CancellationToken ct = default) =>
        Ok(await _orgs.ListTypesAsync(includeInactive, ct));

    [HttpGet("subscription-plans")]
    [Authorize(Policy = Permissions.SubscriptionPlansRead)]
    public async Task<ActionResult<List<SubscriptionPlanDto>>> Plans(CancellationToken ct) =>
        Ok(await _orgs.ListPlansAsync(ct));
}
