using NxtHotspot.Domain.Enums;

namespace NxtHotspot.Application.Common;

public static class LoginMethodDefaults
{
    /// <summary>Sensible flags for a login method when no explicit per-type default row exists.</summary>
    public static (bool RequireOtp, bool RequireBookingValidation) FlagsFor(AuthenticationType type) => type switch
    {
        AuthenticationType.MobileOTP or AuthenticationType.StudentOTP or AuthenticationType.GuestOTP => (true, false),
        AuthenticationType.RoomMobile => (false, true),
        _ => (false, false)
    };
}
