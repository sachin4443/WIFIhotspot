using System.ComponentModel.DataAnnotations;
using NxtHotspot.Domain.Enums;

namespace NxtHotspot.Application.Dtos;

public sealed record PortalMethodDto(AuthenticationType AuthenticationType, bool IsDefault, bool RequireOTP);

public sealed record PortalConfigDto(string OrganizationName, string OrganizationType, string SiteName, string? LogoUrl, string ThemeColor,
    string? WelcomeMessage, string? BackgroundUrl, string? TermsAndConditions, string? PrivacyPolicy, IReadOnlyList<PortalMethodDto> Methods,
    int OtpExpirySeconds, int OtpResendCooldownSeconds, bool SmsDevelopmentMode);

public sealed class PortalOtpRequest
{
    [Required] public AuthenticationType? Method { get; set; }
    [RegularExpression(ValidationPatterns.Mobile)] public string? MobileNumber { get; set; }
    /// <summary>Student/employee ID for ID + OTP methods (the code goes to the registered mobile).</summary>
    [StringLength(100)] public string? Identifier { get; set; }
    [StringLength(20)] public string? RoomNumber { get; set; }
    [StringLength(64)] public string? MacAddress { get; set; }
}

public sealed record PortalOtpResponse(Guid ChallengeId, string MaskedMobile, int ExpiresInSeconds, int ResendInSeconds, bool DevelopmentMode);

public sealed class PortalLoginRequest
{
    [Required] public AuthenticationType? Method { get; set; }
    [RegularExpression(ValidationPatterns.Mobile)] public string? MobileNumber { get; set; }
    [StringLength(100)] public string? Identifier { get; set; }
    [StringLength(128)] public string? Password { get; set; }
    [StringLength(20)] public string? RoomNumber { get; set; }
    public Guid? ChallengeId { get; set; }
    [StringLength(10)] public string? Otp { get; set; }
    [StringLength(64)] public string? MacAddress { get; set; }
    public bool AcceptTerms { get; set; }
}

/// <summary>
/// Status "Authorized": submit Username/Password to the router's login URL (single use, short-lived, bound to the MAC when given).
/// Status "OtpRequired": the password was correct and a second-factor code was sent (ChallengeId).
/// </summary>
public sealed record PortalLoginResponse(string Status, string? Username, string? Password, DateTime? ExpiresAt, int? SessionTimeoutSeconds,
    Guid? ChallengeId, string? MaskedMobile, string Message);
