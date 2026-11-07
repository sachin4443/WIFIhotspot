using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NxtHotspot.Application.Common;
using NxtHotspot.Application.Dtos;

namespace NxtHotspot.API.Controllers;

/// <summary>Anonymous captive-portal endpoints, rate-limited per client IP. The key comes from the router's login page.</summary>
[ApiController]
[AllowAnonymous]
[EnableRateLimiting("portal")]
[Route("api/portal/{portalKey}")]
public sealed class PortalController : ControllerBase
{
    private readonly IPortalService _portal;
    public PortalController(IPortalService portal) => _portal = portal;

    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpGet("config")]
    public async Task<ActionResult<PortalConfigDto>> Config(string portalKey, CancellationToken ct) => Ok(await _portal.GetConfigAsync(portalKey, ct));

    [HttpPost("otp")]
    public async Task<ActionResult<PortalOtpResponse>> Otp(string portalKey, [FromBody] PortalOtpRequest request, CancellationToken ct) =>
        Ok(await _portal.RequestOtpAsync(portalKey, request, ClientIp, ct));

    [HttpPost("login")]
    public async Task<ActionResult<PortalLoginResponse>> Login(string portalKey, [FromBody] PortalLoginRequest request, CancellationToken ct) =>
        Ok(await _portal.LoginAsync(portalKey, request, ClientIp, ct));
}
