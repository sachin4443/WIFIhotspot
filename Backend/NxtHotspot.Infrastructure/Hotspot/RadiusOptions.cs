namespace NxtHotspot.Infrastructure.Hotspot;

/// <summary>Configuration section "Radius" shared by the API and the RADIUS server.</summary>
public sealed class RadiusOptions
{
    /// <summary>Address routers use to reach the NXT RADIUS server (IP or host name). Used in generated router configuration.</summary>
    public string PublicAddress { get; set; } = "";
    /// <summary>Public base URL of the captive portal, e.g. https://portal.example.com. Used in generated portal links.</summary>
    public string PortalBaseUrl { get; set; } = "";
    public int AuthPort { get; set; } = 1812;
    public int AcctPort { get; set; } = 1813;
    /// <summary>Sessions with no accounting update for this long are marked stale.</summary>
    public int StaleSessionMinutes { get; set; } = 30;
    /// <summary>Validity of the one-time RADIUS credential issued after portal sign-in.</summary>
    public int PortalCredentialSeconds { get; set; } = 120;
}
