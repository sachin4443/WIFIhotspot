using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NxtHotspot.Application.Common;
using NxtHotspot.Application.Dtos;
using NxtHotspot.Domain.Enums;

namespace NxtHotspot.API.Controllers;

/// <summary>RADIUS sessions, accounting, disconnects, usage reports and RADIUS monitoring. Organization scope comes from the token.</summary>
[ApiController]
public sealed class SessionsController : ControllerBase
{
    private readonly ISessionService _sessions;
    private readonly IUsageReportService _reports;
    private readonly IRadiusMonitorService _monitor;

    public SessionsController(ISessionService sessions, IUsageReportService reports, IRadiusMonitorService monitor)
    {
        _sessions = sessions; _reports = reports; _monitor = monitor;
    }

    [HttpGet("api/sessions"), Authorize(Policy = Permissions.SessionsRead)]
    public async Task<ActionResult<PagedResult<SessionDto>>> List([FromQuery] SessionQuery query, CancellationToken ct) => Ok(await _sessions.ListAsync(query, ct));

    [HttpGet("api/sessions/export"), Authorize(Policy = Permissions.SessionsRead)]
    public async Task<IActionResult> Export([FromQuery] SessionQuery query, CancellationToken ct)
    {
        var f = await _sessions.ExportAsync(query, ct);
        return File(f.Content, f.ContentType, f.FileName);
    }

    /// <summary>Queues an RFC 5176 Disconnect-Request; the RADIUS server sends it and records the NAS reply.</summary>
    [HttpPost("api/sessions/{sessionId:guid}/disconnect"), Authorize(Policy = Permissions.SessionsDisconnect)]
    public async Task<ActionResult<DisconnectCommandDto>> Disconnect(Guid sessionId, CancellationToken ct) =>
        Accepted(await _sessions.DisconnectAsync(sessionId, ct));

    [HttpGet("api/sessions/disconnects"), Authorize(Policy = Permissions.SessionsRead)]
    public async Task<ActionResult<List<DisconnectCommandDto>>> Disconnects([FromQuery] Guid? organizationId, CancellationToken ct) =>
        Ok(await _sessions.RecentDisconnectsAsync(organizationId, ct));

    [HttpGet("api/reports/usage"), Authorize(Policy = Permissions.ReportsRead)]
    public async Task<ActionResult<UsageReportDto>> Usage([FromQuery] Guid? organizationId, [FromQuery] Guid? siteId, [FromQuery] DateTime? from,
        [FromQuery] DateTime? to, CancellationToken ct)
    {
        var end = (to ?? DateTime.UtcNow).ToUniversalTime();
        var start = (from ?? end.AddDays(-30)).ToUniversalTime();
        return Ok(await _reports.GetAsync(organizationId, siteId, start, end, ct));
    }

    /// <summary>RADIUS server heartbeat, 24-hour counters and NAS health. Organization admins see their own NAS and requests only.</summary>
    [HttpGet("api/radius/overview"), Authorize(Policy = Permissions.SessionsRead)]
    public async Task<ActionResult<RadiusOverviewDto>> Overview([FromQuery] Guid? organizationId, CancellationToken ct) =>
        Ok(await _monitor.OverviewAsync(organizationId, ct));

    [HttpGet("api/radius/requests"), Authorize(Policy = Permissions.SessionsRead)]
    public async Task<ActionResult<PagedResult<RadiusRequestLogDto>>> Requests([FromQuery] Guid? organizationId, [FromQuery] RadiusResult? result,
        [FromQuery] string? search, [FromQuery] DateTime? from, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) =>
        Ok(await _monitor.RequestLogsAsync(organizationId, result, search, from?.ToUniversalTime(), page, pageSize, ct));
}
