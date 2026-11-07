using NxtHotspot.Application.Dtos;

namespace NxtHotspot.Application.Common;

/// <summary>Anonymous captive-portal API. The portal key (from the router's login page) selects the NAS, site and organization.</summary>
public interface IPortalService
{
    Task<PortalConfigDto> GetConfigAsync(string portalKey, CancellationToken ct = default);
    Task<PortalOtpResponse> RequestOtpAsync(string portalKey, PortalOtpRequest request, string? clientIp, CancellationToken ct = default);
    Task<PortalLoginResponse> LoginAsync(string portalKey, PortalLoginRequest request, string? clientIp, CancellationToken ct = default);
}
