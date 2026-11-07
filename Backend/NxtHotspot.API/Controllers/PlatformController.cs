using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NxtHotspot.Application.Common;
using NxtHotspot.Application.Dtos;

namespace NxtHotspot.API.Controllers;

[ApiController]
[Route("api/account")]
public sealed class AccountController : ControllerBase
{
    private readonly IAccountService _account;
    public AccountController(IAccountService account) => _account = account;

    /// <summary>The caller's organization, accessible sites, roles and permissions.</summary>
    [HttpGet("context"), Authorize]
    public async Task<ActionResult<AccountContextDto>> Context(CancellationToken ct) => Ok(await _account.GetContextAsync(ct));
}

[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboard;
    public DashboardController(IDashboardService dashboard) => _dashboard = dashboard;

    [HttpGet("platform"), Authorize(Policy = Permissions.OrganizationsRead)]
    public async Task<ActionResult<PlatformDashboardDto>> Platform(CancellationToken ct) => Ok(await _dashboard.GetPlatformAsync(ct));

    /// <summary>Statistics for one organization. Any signed-in admin of that organization (site admins see their sites only).</summary>
    [HttpGet("organizations/{organizationId:guid}"), Authorize]
    public async Task<ActionResult<OrganizationStatisticsDto>> Organization(Guid organizationId, CancellationToken ct) =>
        Ok(await _dashboard.GetOrganizationAsync(organizationId, ct));
}

[ApiController]
public sealed class DirectoryController : ControllerBase
{
    private readonly ISiteDirectoryService _directory;
    public DirectoryController(ISiteDirectoryService directory) => _directory = directory;

    /// <summary>Sites across organizations (platform) or within the caller's organization.</summary>
    [HttpGet("api/sites"), Authorize(Policy = Permissions.SitesRead)]
    public async Task<ActionResult<PagedResult<SiteListItemDto>>> Sites([FromQuery] Guid? organizationId, [FromQuery] string? search,
        [FromQuery] bool? isActive, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        Ok(await _directory.ListAsync(organizationId, search, isActive, page, pageSize, ct));

    [HttpGet("api/wallets"), Authorize(Policy = Permissions.WalletRead)]
    public async Task<ActionResult<PagedResult<WalletOverviewDto>>> Wallets([FromQuery] Guid? organizationId, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        Ok(await _directory.ListWalletsAsync(organizationId, search, page, pageSize, ct));
}

[ApiController]
[Route("api/platform/subscription-plans")]
public sealed class SubscriptionPlansController : ControllerBase
{
    private readonly IPlanService _plans;
    public SubscriptionPlansController(IPlanService plans) => _plans = plans;

    [HttpGet, Authorize(Policy = Permissions.SubscriptionPlansRead)]
    public async Task<ActionResult<List<SubscriptionPlanDetailDto>>> List(CancellationToken ct) => Ok(await _plans.ListAsync(ct));

    [HttpPost, Authorize(Policy = Permissions.SubscriptionPlansWrite)]
    public async Task<ActionResult<SubscriptionPlanDetailDto>> Create([FromBody] UpsertSubscriptionPlanRequest request, CancellationToken ct)
    {
        var plan = await _plans.CreateAsync(request, ct);
        return Created($"api/platform/subscription-plans/{plan.Id}", plan);
    }

    [HttpPut("{id:guid}"), Authorize(Policy = Permissions.SubscriptionPlansWrite)]
    public async Task<ActionResult<SubscriptionPlanDetailDto>> Update(Guid id, [FromBody] UpsertSubscriptionPlanRequest request, CancellationToken ct) =>
        Ok(await _plans.UpdateAsync(id, request, ct));

    /// <summary>Assign (or clear) an organization's subscription plan.</summary>
    [HttpPut("~/api/organizations/{organizationId:guid}/subscription-plan"), Authorize(Policy = Permissions.OrganizationsWrite)]
    public async Task<IActionResult> Assign(Guid organizationId, [FromBody] AssignPlanRequest request, CancellationToken ct)
    {
        await _plans.AssignAsync(organizationId, request.SubscriptionPlanId, ct);
        return NoContent();
    }
}
