using NxtHotspot.Domain.Enums;

namespace NxtHotspot.Application.Dtos;

public sealed record SessionDto(Guid Id, Guid OrganizationId, string? OrganizationName, Guid SiteId, string SiteName, Guid NasId, string NasName,
    Guid? SubscriberId, string? SubscriberName, SubscriberType? SubscriberType, string Username, string AcctSessionId, string? FramedIpAddress,
    string? MacAddress, DateTime StartedAt, DateTime LastUpdateAt, DateTime? StoppedAt, long SessionTimeSeconds, long InputBytes, long OutputBytes,
    string? TerminateCause, RadiusSessionStatus Status, DisconnectStatus? PendingDisconnect);

public sealed class SessionQuery
{
    public Guid? OrganizationId { get; set; }
    public Guid? SiteId { get; set; }
    public Guid? NasId { get; set; }
    public Guid? SubscriberId { get; set; }
    public RadiusSessionStatus? Status { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public sealed record DisconnectCommandDto(Guid Id, Guid SessionId, Guid NasId, DisconnectStatus Status, int Attempts, string? Result, DateTime CreatedAt, DateTime? CompletedAt);

public sealed record UsagePointDto(string Date, int Sessions, int UniqueUsers, long InputBytes, long OutputBytes, long SessionSeconds);
public sealed record UsageRowDto(string Key, string Label, int Sessions, long Bytes, long SessionSeconds);
public sealed record UsageReportDto(DateTime From, DateTime To, int Sessions, int UniqueUsers, long InputBytes, long OutputBytes, long SessionSeconds,
    IReadOnlyList<UsagePointDto> Daily, IReadOnlyList<UsageRowDto> TopSubscribers, IReadOnlyList<UsageRowDto> BySite, IReadOnlyList<UsageRowDto> ByNas,
    IReadOnlyList<UsageRowDto> BySubscriberType);

public sealed record RadiusServerStatusDto(string InstanceId, string Version, string? HostName, int AuthPort, int AcctPort, DateTime StartedAt,
    DateTime LastHeartbeatAt, bool IsOnline, long AuthRequests, long AuthAccepts, long AuthRejects, long AcctRequests, long Dropped);

public sealed record RadiusOverviewDto(bool AnyServerOnline, IReadOnlyList<RadiusServerStatusDto> Servers, int ActiveSessions,
    int RequestsLast24h, int AcceptsLast24h, int RejectsLast24h, int DroppedLast24h, IReadOnlyList<LabelCount> TopRejectReasons,
    IReadOnlyList<NasHealthDto> Nas);

public sealed record NasHealthDto(Guid Id, string Name, string IpAddress, string OrganizationName, string SiteName, bool IsActive, DateTime? LastSeenAt, int ActiveSessions);

public sealed record RadiusRequestLogDto(long Id, Guid? OrganizationId, string? OrganizationName, Guid? NasId, string? NasName, string SourceIp,
    RadiusPacketKind PacketKind, RadiusResult Result, string? Username, string? MacAddress, string? Reason, DateTime CreatedAt);
