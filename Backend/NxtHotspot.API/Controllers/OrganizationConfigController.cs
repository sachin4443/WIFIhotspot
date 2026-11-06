using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NxtHotspot.Application.Common;
using NxtHotspot.Application.Dtos;

namespace NxtHotspot.API.Controllers;

/// <summary>
/// Organization profile, settings, branding, login methods and SMS configuration.
/// The organization id in the route is always checked against the caller's token by the service layer.
/// </summary>
[ApiController]
[Route("api/organizations/{organizationId:guid}")]
public sealed class OrganizationConfigController : ControllerBase
{
    private readonly IOrganizationConfigService _config;
    public OrganizationConfigController(IOrganizationConfigService config) => _config = config;

    // ---- profile ----
    [HttpGet("profile"), Authorize(Policy = Permissions.SettingsRead)]
    public async Task<ActionResult<OrganizationProfileDto>> GetProfile(Guid organizationId, CancellationToken ct) =>
        Ok(await _config.GetProfileAsync(organizationId, ct));

    [HttpPut("profile"), Authorize(Policy = Permissions.SettingsWrite)]
    public async Task<ActionResult<OrganizationProfileDto>> UpdateProfile(Guid organizationId, [FromBody] UpdateOrganizationProfileRequest request, CancellationToken ct) =>
        Ok(await _config.UpdateProfileAsync(organizationId, request, ct));

    // ---- settings ----
    [HttpGet("settings"), Authorize(Policy = Permissions.SettingsRead)]
    public async Task<ActionResult<OrganizationSettingsDto>> GetSettings(Guid organizationId, CancellationToken ct) =>
        Ok(await _config.GetSettingsAsync(organizationId, ct));

    [HttpPut("settings"), Authorize(Policy = Permissions.SettingsWrite)]
    public async Task<ActionResult<OrganizationSettingsDto>> UpdateSettings(Guid organizationId, [FromBody] UpdateOrganizationSettingsRequest request, CancellationToken ct) =>
        Ok(await _config.UpdateSettingsAsync(organizationId, request, ct));

    // ---- branding ----
    [HttpGet("branding"), Authorize(Policy = Permissions.SettingsRead)]
    public async Task<ActionResult<OrganizationBrandingDto>> GetBranding(Guid organizationId, CancellationToken ct) =>
        Ok(await _config.GetBrandingAsync(organizationId, ct));

    [HttpPut("branding"), Authorize(Policy = Permissions.SettingsWrite)]
    public async Task<ActionResult<OrganizationBrandingDto>> UpdateBranding(Guid organizationId, [FromBody] UpdateOrganizationBrandingRequest request, CancellationToken ct) =>
        Ok(await _config.UpdateBrandingAsync(organizationId, request, ct));

    // ---- login methods (organization level) ----
    [HttpGet("login-methods"), Authorize(Policy = Permissions.SettingsRead)]
    public async Task<ActionResult<List<LoginMethodDto>>> ListLoginMethods(Guid organizationId, CancellationToken ct) =>
        Ok(await _config.ListLoginMethodsAsync(organizationId, null, false, ct));

    [HttpPut("login-methods"), Authorize(Policy = Permissions.SettingsWrite)]
    public async Task<ActionResult<List<LoginMethodDto>>> UpsertLoginMethod(Guid organizationId, [FromBody] UpsertLoginMethodRequest request, CancellationToken ct) =>
        Ok(await _config.UpsertLoginMethodAsync(organizationId, null, request, ct));

    [HttpDelete("login-methods/{loginMethodId:guid}"), Authorize(Policy = Permissions.SettingsWrite)]
    public async Task<IActionResult> DeleteLoginMethod(Guid organizationId, Guid loginMethodId, CancellationToken ct)
    {
        await _config.DeleteLoginMethodAsync(organizationId, loginMethodId, ct);
        return NoContent();
    }

    // ---- login methods (site override) ----
    /// <param name="effective">true = only the enabled methods that apply to the site (its own, else the organization's).</param>
    [HttpGet("sites/{siteId:guid}/login-methods"), Authorize(Policy = Permissions.SettingsRead)]
    public async Task<ActionResult<List<LoginMethodDto>>> ListSiteLoginMethods(Guid organizationId, Guid siteId, [FromQuery] bool effective = false, CancellationToken ct = default) =>
        Ok(await _config.ListLoginMethodsAsync(organizationId, siteId, effective, ct));

    [HttpPut("sites/{siteId:guid}/login-methods"), Authorize(Policy = Permissions.SettingsWrite)]
    public async Task<ActionResult<List<LoginMethodDto>>> UpsertSiteLoginMethod(Guid organizationId, Guid siteId, [FromBody] UpsertLoginMethodRequest request, CancellationToken ct) =>
        Ok(await _config.UpsertLoginMethodAsync(organizationId, siteId, request, ct));

    // ---- SMS (the API key is write-only; responses only say whether one is stored) ----
    [HttpGet("sms"), Authorize(Policy = Permissions.SettingsRead)]
    public async Task<ActionResult<List<SmsConfigDto>>> ListSms(Guid organizationId, CancellationToken ct) =>
        Ok(await _config.ListSmsAsync(organizationId, ct));

    [HttpPut("sms"), Authorize(Policy = Permissions.SettingsWrite)]
    public async Task<ActionResult<SmsConfigDto>> UpsertSms(Guid organizationId, [FromBody] UpsertSmsConfigRequest request, CancellationToken ct) =>
        Ok(await _config.UpsertSmsAsync(organizationId, null, request, ct));

    [HttpDelete("sms"), Authorize(Policy = Permissions.SettingsWrite)]
    public async Task<IActionResult> DeleteSms(Guid organizationId, CancellationToken ct)
    {
        await _config.DeleteSmsAsync(organizationId, null, ct);
        return NoContent();
    }

    [HttpPut("sites/{siteId:guid}/sms"), Authorize(Policy = Permissions.SettingsWrite)]
    public async Task<ActionResult<SmsConfigDto>> UpsertSiteSms(Guid organizationId, Guid siteId, [FromBody] UpsertSmsConfigRequest request, CancellationToken ct) =>
        Ok(await _config.UpsertSmsAsync(organizationId, siteId, request, ct));

    [HttpDelete("sites/{siteId:guid}/sms"), Authorize(Policy = Permissions.SettingsWrite)]
    public async Task<IActionResult> DeleteSiteSms(Guid organizationId, Guid siteId, CancellationToken ct)
    {
        await _config.DeleteSmsAsync(organizationId, siteId, ct);
        return NoContent();
    }
}

/// <summary>Platform-only: organization types and the platform default SMS provider.</summary>
[ApiController]
[Route("api/platform")]
public sealed class PlatformConfigController : ControllerBase
{
    private readonly IOrganizationConfigService _config;
    public PlatformConfigController(IOrganizationConfigService config) => _config = config;

    [HttpGet("organization-types/{id:int}"), Authorize(Policy = Permissions.OrganizationTypesWrite)]
    public async Task<ActionResult<OrganizationTypeDetailDto>> GetType(int id, CancellationToken ct) =>
        Ok(await _config.GetTypeAsync(id, ct));

    [HttpPost("organization-types"), Authorize(Policy = Permissions.OrganizationTypesWrite)]
    public async Task<ActionResult<OrganizationTypeDetailDto>> CreateType([FromBody] UpsertOrganizationTypeRequest request, CancellationToken ct)
    {
        var created = await _config.CreateTypeAsync(request, ct);
        return CreatedAtAction(nameof(GetType), new { id = created.OrganizationTypeId }, created);
    }

    [HttpPut("organization-types/{id:int}"), Authorize(Policy = Permissions.OrganizationTypesWrite)]
    public async Task<ActionResult<OrganizationTypeDetailDto>> UpdateType(int id, [FromBody] UpsertOrganizationTypeRequest request, CancellationToken ct) =>
        Ok(await _config.UpdateTypeAsync(id, request, ct));

    [HttpGet("sms"), Authorize(Policy = Permissions.PlatformSmsWrite)]
    public async Task<ActionResult<SmsConfigDto>> GetSms(CancellationToken ct) => Ok(await _config.GetPlatformSmsAsync(ct));

    [HttpPut("sms"), Authorize(Policy = Permissions.PlatformSmsWrite)]
    public async Task<ActionResult<SmsConfigDto>> UpsertSms([FromBody] UpsertSmsConfigRequest request, CancellationToken ct) =>
        Ok(await _config.UpsertPlatformSmsAsync(request, ct));
}

[ApiController]
[Route("api/audit-logs")]
public sealed class AuditLogsController : ControllerBase
{
    private readonly IAuditLogService _audit;
    public AuditLogsController(IAuditLogService audit) => _audit = audit;

    [HttpGet, Authorize(Policy = Permissions.AuditRead)]
    public async Task<ActionResult<PagedResult<AuditLogDto>>> List([FromQuery] Guid? organizationId, [FromQuery] string? action,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken ct = default) =>
        Ok(await _audit.ListAsync(organizationId, action, from?.ToUniversalTime(), to?.ToUniversalTime(), page, pageSize, ct));
}

/// <summary>Manual wallet deduction (platform only). Crediting stays in <see cref="WalletsController"/>.</summary>
[ApiController]
[Route("api/sites/{siteId:guid}/wallet")]
public sealed class WalletAdjustmentsController : ControllerBase
{
    private readonly IWalletService _wallets;
    private readonly ITenantAccess _tenant;
    private readonly ICurrentUser _current;

    public WalletAdjustmentsController(IWalletService wallets, ITenantAccess tenant, ICurrentUser current)
    { _wallets = wallets; _tenant = tenant; _current = current; }

    [HttpPost("debit"), Authorize(Policy = Permissions.WalletDebit)]
    public async Task<ActionResult<WalletTransactionDto>> Debit(Guid siteId, [FromBody] DebitWalletRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct)
    {
        if (idempotencyKey is { Length: > 100 }) throw new ValidationFailedException("Idempotency-Key is too long.");
        await _tenant.GetAuthorizedSiteAsync(siteId, ct);
        return Ok(await _wallets.DebitAsync(siteId, request.Amount, request.Reference, request.Description,
            string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim(), _current.AdminUserId, ct));
    }
}
