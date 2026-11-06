using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NxtHotspot.Application.Common;
using NxtHotspot.Application.Dtos;

namespace NxtHotspot.API.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly ICurrentUser _current;
    public AuthController(IAuthService auth, ICurrentUser current) { _auth = auth; _current = current; }

    /// <summary>Who am I: identity scope and effective permissions from the validated token.</summary>
    [HttpGet("me")]
    [Authorize]
    public ActionResult<CurrentUserDto> Me() =>
        Ok(new CurrentUserDto(_current.AdminUserId, _current.OrganizationId, _current.IsPlatformAdmin, _current.HasAllSites,
            _current.Permissions.OrderBy(p => p, StringComparer.Ordinal).ToList()));

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken ct) =>
        Ok(await _auth.LoginAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct));
}
