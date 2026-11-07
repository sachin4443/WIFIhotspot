namespace NxtHotspot.Infrastructure.Sms;

public sealed class SmsOptions
{
    /// <summary>Hosts an organization may point its SMS endpoint at (prevents SSRF). Empty = 2factor.in only.</summary>
    public string[] AllowedHosts { get; set; } = Array.Empty<string>();

    public string[] EffectiveAllowedHosts => AllowedHosts.Length > 0 ? AllowedHosts : new[] { "2factor.in" };

    public string DefaultEndpoint { get; set; } = "https://2factor.in/API/R1/";

    /// <summary>Placeholders: {OTP}, {EXPIRY}. In India the final text must match the DLT-registered template.</summary>
    /// <summary>
    /// "Provider" (default): send through the resolved SMS provider.
    /// "DevelopmentLog": write the OTP to the API log instead of sending it. Refused outside the Development environment.
    /// </summary>
    public string DeliveryMode { get; set; } = "Provider";

    public bool IsDevelopmentLog => string.Equals(DeliveryMode, "DevelopmentLog", StringComparison.OrdinalIgnoreCase);

    public string DefaultMessageTemplate { get; set; } =
        "Your WiFi OTP is {OTP}. Valid for {EXPIRY} minutes. Do not share it with anyone.";
}
