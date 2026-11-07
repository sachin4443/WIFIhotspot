using NxtHotspot.Application.Dtos;
using NxtHotspot.Domain.Enums;

namespace NxtHotspot.Application.Common;

public interface INasService
{
    Task<List<NasDto>> ListAsync(Guid organizationId, Guid? siteId, CancellationToken ct = default);
    Task<NasDto> CreateAsync(Guid organizationId, UpsertNasRequest request, CancellationToken ct = default);
    Task<NasDto> UpdateAsync(Guid organizationId, Guid nasId, UpsertNasRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid organizationId, Guid nasId, CancellationToken ct = default);
    Task<PortalKeyDto> RegeneratePortalKeyAsync(Guid organizationId, Guid nasId, CancellationToken ct = default);
    Task<RouterConfigDto> GetRouterConfigAsync(Guid organizationId, Guid nasId, CancellationToken ct = default);
}

public interface IPackageService
{
    Task<List<InternetPackageDto>> ListAsync(Guid organizationId, Guid? siteId, CancellationToken ct = default);
    Task<InternetPackageDto> CreateAsync(Guid organizationId, UpsertPackageRequest request, CancellationToken ct = default);
    Task<InternetPackageDto> UpdateAsync(Guid organizationId, Guid packageId, UpsertPackageRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid organizationId, Guid packageId, CancellationToken ct = default);
}

public interface ISubscriberGroupService
{
    Task<List<SubscriberGroupDto>> ListAsync(Guid organizationId, CancellationToken ct = default);
    Task<SubscriberGroupDto> CreateAsync(Guid organizationId, UpsertGroupRequest request, CancellationToken ct = default);
    Task<SubscriberGroupDto> UpdateAsync(Guid organizationId, Guid groupId, UpsertGroupRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid organizationId, Guid groupId, CancellationToken ct = default);
}

public interface ISubscriberService
{
    Task<PagedResult<SubscriberDto>> ListAsync(Guid organizationId, SubscriberQuery query, CancellationToken ct = default);
    Task<SubscriberDto> GetAsync(Guid organizationId, Guid subscriberId, CancellationToken ct = default);
    Task<SubscriberDto> CreateAsync(Guid organizationId, CreateSubscriberRequest request, CancellationToken ct = default);
    Task<SubscriberDto> UpdateAsync(Guid organizationId, Guid subscriberId, UpdateSubscriberRequest request, CancellationToken ct = default);
    Task<SubscriberDto> SetStatusAsync(Guid organizationId, Guid subscriberId, SubscriberStatus status, CancellationToken ct = default);
    /// <summary>Hotel: ends the stay now and queues disconnects for the guest's active sessions.</summary>
    Task<SubscriberDto> CheckOutAsync(Guid organizationId, Guid subscriberId, CancellationToken ct = default);
    Task DeleteAsync(Guid organizationId, Guid subscriberId, CancellationToken ct = default);
    Task<ImportResultDto> ImportAsync(Guid organizationId, SubscriberType type, Stream xlsx, bool updateExisting, CancellationToken ct = default);
    Task<FileResultDto> ExportAsync(Guid organizationId, SubscriberQuery query, CancellationToken ct = default);
    FileResultDto ImportTemplate(SubscriberType type);
}

/// <summary>Queues RFC 5176 Disconnect-Requests for the RADIUS server to send.</summary>
public interface IDisconnectQueue
{
    Task<int> QueueForSubscriberAsync(Guid subscriberId, string reason, CancellationToken ct = default);
}
