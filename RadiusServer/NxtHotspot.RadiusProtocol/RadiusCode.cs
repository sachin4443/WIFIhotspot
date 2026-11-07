namespace NxtHotspot.RadiusProtocol;

/// <summary>RADIUS packet codes (RFC 2865, 2866, 5176).</summary>
public enum RadiusCode : byte
{
    AccessRequest = 1,
    AccessAccept = 2,
    AccessReject = 3,
    AccountingRequest = 4,
    AccountingResponse = 5,
    AccessChallenge = 11,
    DisconnectRequest = 40,
    DisconnectAck = 41,
    DisconnectNak = 42,
    CoaRequest = 43,
    CoaAck = 44,
    CoaNak = 45,
}

/// <summary>Standard attribute numbers used by NXT (RFC 2865/2866/2869/5176).</summary>
public static class RadiusAttributeType
{
    public const byte UserName = 1;
    public const byte UserPassword = 2;
    public const byte ChapPassword = 3;
    public const byte NasIpAddress = 4;
    public const byte NasPort = 5;
    public const byte ServiceType = 6;
    public const byte FramedIpAddress = 8;
    public const byte ReplyMessage = 18;
    public const byte Class = 25;
    public const byte VendorSpecific = 26;
    public const byte SessionTimeout = 27;
    public const byte IdleTimeout = 28;
    public const byte CalledStationId = 30;
    public const byte CallingStationId = 31;
    public const byte NasIdentifier = 32;
    public const byte AcctStatusType = 40;
    public const byte AcctDelayTime = 41;
    public const byte AcctInputOctets = 42;
    public const byte AcctOutputOctets = 43;
    public const byte AcctSessionId = 44;
    public const byte AcctSessionTime = 46;
    public const byte AcctInputPackets = 47;
    public const byte AcctOutputPackets = 48;
    public const byte AcctTerminateCause = 49;
    public const byte AcctInputGigawords = 52;
    public const byte AcctOutputGigawords = 53;
    public const byte EventTimestamp = 55;
    public const byte ChapChallenge = 60;
    public const byte NasPortType = 61;
    public const byte EapMessage = 79;
    public const byte MessageAuthenticator = 80;
    public const byte AcctInterimInterval = 85;
    public const byte NasPortId = 87;
    public const byte ErrorCause = 101;
}

/// <summary>Acct-Status-Type values (RFC 2866).</summary>
public static class AcctStatusType
{
    public const int Start = 1;
    public const int Stop = 2;
    public const int InterimUpdate = 3;
    public const int AccountingOn = 7;
    public const int AccountingOff = 8;
}

/// <summary>MikroTik vendor-specific attributes (vendor 14988).</summary>
public static class MikrotikAttribute
{
    public const uint VendorId = 14988;
    public const byte RateLimit = 8;
    public const byte TotalLimit = 17;
    public const byte TotalLimitGigawords = 18;
}

public sealed class RadiusPacketException : Exception
{
    public RadiusPacketException(string message) : base(message) { }
}
