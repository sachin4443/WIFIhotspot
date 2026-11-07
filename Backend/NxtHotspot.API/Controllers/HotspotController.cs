using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NxtHotspot.Application.Common;
using NxtHotspot.Application.Dtos;
using NxtHotspot.Domain.Enums;

namespace NxtHotspot.API.Controllers;

/// <summary>NAS devices, internet packages, subscriber groups and subscribers of one organization.</summary>
[ApiController]
[Route("api/organizations/{organizationId:guid}")]
public sealed class HotspotController : ControllerBase
{
    private const long MaxImportBytes = 10 * 1024 * 1024;
    private readonly INasService _nas;
    private readonly IPackageService _packages;
    private readonly ISubscriberGroupService _groups;
    private readonly ISubscriberService _subscribers;

    public HotspotController(INasService nas, IPackageService packages, ISubscriberGroupService groups, ISubscriberService subscribers)
    {
        _nas = nas; _packages = packages; _groups = groups; _subscribers = subscribers;
    }

    // ---- NAS ----
    [HttpGet("nas"), Authorize(Policy = Permissions.NasRead)]
    public async Task<ActionResult<List<NasDto>>> ListNas(Guid organizationId, [FromQuery] Guid? siteId, CancellationToken ct) =>
        Ok(await _nas.ListAsync(organizationId, siteId, ct));

    [HttpPost("nas"), Authorize(Policy = Permissions.NasWrite)]
    public async Task<ActionResult<NasDto>> CreateNas(Guid organizationId, [FromBody] UpsertNasRequest request, CancellationToken ct)
    {
        var nas = await _nas.CreateAsync(organizationId, request, ct);
        return Created($"api/organizations/{organizationId}/nas/{nas.Id}", nas);
    }

    [HttpPut("nas/{nasId:guid}"), Authorize(Policy = Permissions.NasWrite)]
    public async Task<ActionResult<NasDto>> UpdateNas(Guid organizationId, Guid nasId, [FromBody] UpsertNasRequest request, CancellationToken ct) =>
        Ok(await _nas.UpdateAsync(organizationId, nasId, request, ct));

    [HttpDelete("nas/{nasId:guid}"), Authorize(Policy = Permissions.NasWrite)]
    public async Task<IActionResult> DeleteNas(Guid organizationId, Guid nasId, CancellationToken ct)
    {
        await _nas.DeleteAsync(organizationId, nasId, ct);
        return NoContent();
    }

    /// <summary>Creates a new captive-portal key for the router. The key is returned once and stored hashed.</summary>
    [HttpPost("nas/{nasId:guid}/portal-key"), Authorize(Policy = Permissions.NasWrite)]
    public async Task<ActionResult<PortalKeyDto>> PortalKey(Guid organizationId, Guid nasId, CancellationToken ct) =>
        Ok(await _nas.RegeneratePortalKeyAsync(organizationId, nasId, ct));

    [HttpGet("nas/{nasId:guid}/router-config"), Authorize(Policy = Permissions.NasRead)]
    public async Task<ActionResult<RouterConfigDto>> RouterConfig(Guid organizationId, Guid nasId, CancellationToken ct) =>
        Ok(await _nas.GetRouterConfigAsync(organizationId, nasId, ct));

    // ---- packages ----
    [HttpGet("packages"), Authorize(Policy = Permissions.PackagesRead)]
    public async Task<ActionResult<List<InternetPackageDto>>> ListPackages(Guid organizationId, [FromQuery] Guid? siteId, CancellationToken ct) =>
        Ok(await _packages.ListAsync(organizationId, siteId, ct));

    [HttpPost("packages"), Authorize(Policy = Permissions.PackagesWrite)]
    public async Task<ActionResult<InternetPackageDto>> CreatePackage(Guid organizationId, [FromBody] UpsertPackageRequest request, CancellationToken ct)
    {
        var p = await _packages.CreateAsync(organizationId, request, ct);
        return Created($"api/organizations/{organizationId}/packages/{p.Id}", p);
    }

    [HttpPut("packages/{packageId:guid}"), Authorize(Policy = Permissions.PackagesWrite)]
    public async Task<ActionResult<InternetPackageDto>> UpdatePackage(Guid organizationId, Guid packageId, [FromBody] UpsertPackageRequest request, CancellationToken ct) =>
        Ok(await _packages.UpdateAsync(organizationId, packageId, request, ct));

    [HttpDelete("packages/{packageId:guid}"), Authorize(Policy = Permissions.PackagesWrite)]
    public async Task<IActionResult> DeletePackage(Guid organizationId, Guid packageId, CancellationToken ct)
    {
        await _packages.DeleteAsync(organizationId, packageId, ct);
        return NoContent();
    }

    // ---- groups ----
    [HttpGet("subscriber-groups"), Authorize(Policy = Permissions.SubscribersRead)]
    public async Task<ActionResult<List<SubscriberGroupDto>>> ListGroups(Guid organizationId, CancellationToken ct) =>
        Ok(await _groups.ListAsync(organizationId, ct));

    [HttpPost("subscriber-groups"), Authorize(Policy = Permissions.SubscribersWrite)]
    public async Task<ActionResult<SubscriberGroupDto>> CreateGroup(Guid organizationId, [FromBody] UpsertGroupRequest request, CancellationToken ct) =>
        Ok(await _groups.CreateAsync(organizationId, request, ct));

    [HttpPut("subscriber-groups/{groupId:guid}"), Authorize(Policy = Permissions.SubscribersWrite)]
    public async Task<ActionResult<SubscriberGroupDto>> UpdateGroup(Guid organizationId, Guid groupId, [FromBody] UpsertGroupRequest request, CancellationToken ct) =>
        Ok(await _groups.UpdateAsync(organizationId, groupId, request, ct));

    [HttpDelete("subscriber-groups/{groupId:guid}"), Authorize(Policy = Permissions.SubscribersWrite)]
    public async Task<IActionResult> DeleteGroup(Guid organizationId, Guid groupId, CancellationToken ct)
    {
        await _groups.DeleteAsync(organizationId, groupId, ct);
        return NoContent();
    }

    // ---- subscribers ----
    [HttpGet("subscribers"), Authorize(Policy = Permissions.SubscribersRead)]
    public async Task<ActionResult<PagedResult<SubscriberDto>>> ListSubscribers(Guid organizationId, [FromQuery] SubscriberQuery query, CancellationToken ct) =>
        Ok(await _subscribers.ListAsync(organizationId, query, ct));

    [HttpGet("subscribers/{subscriberId:guid}"), Authorize(Policy = Permissions.SubscribersRead)]
    public async Task<ActionResult<SubscriberDto>> GetSubscriber(Guid organizationId, Guid subscriberId, CancellationToken ct) =>
        Ok(await _subscribers.GetAsync(organizationId, subscriberId, ct));

    [HttpPost("subscribers"), Authorize(Policy = Permissions.SubscribersWrite)]
    public async Task<ActionResult<SubscriberDto>> CreateSubscriber(Guid organizationId, [FromBody] CreateSubscriberRequest request, CancellationToken ct)
    {
        var s = await _subscribers.CreateAsync(organizationId, request, ct);
        return Created($"api/organizations/{organizationId}/subscribers/{s.Id}", s);
    }

    [HttpPut("subscribers/{subscriberId:guid}"), Authorize(Policy = Permissions.SubscribersWrite)]
    public async Task<ActionResult<SubscriberDto>> UpdateSubscriber(Guid organizationId, Guid subscriberId, [FromBody] UpdateSubscriberRequest request, CancellationToken ct) =>
        Ok(await _subscribers.UpdateAsync(organizationId, subscriberId, request, ct));

    [HttpPut("subscribers/{subscriberId:guid}/status"), Authorize(Policy = Permissions.SubscribersWrite)]
    public async Task<ActionResult<SubscriberDto>> SetSubscriberStatus(Guid organizationId, Guid subscriberId, [FromBody] SetSubscriberStatusRequest request, CancellationToken ct) =>
        Ok(await _subscribers.SetStatusAsync(organizationId, subscriberId, request.Status!.Value, ct));

    [HttpPost("subscribers/{subscriberId:guid}/check-out"), Authorize(Policy = Permissions.SubscribersWrite)]
    public async Task<ActionResult<SubscriberDto>> CheckOut(Guid organizationId, Guid subscriberId, CancellationToken ct) =>
        Ok(await _subscribers.CheckOutAsync(organizationId, subscriberId, ct));

    [HttpDelete("subscribers/{subscriberId:guid}"), Authorize(Policy = Permissions.SubscribersWrite)]
    public async Task<IActionResult> DeleteSubscriber(Guid organizationId, Guid subscriberId, CancellationToken ct)
    {
        await _subscribers.DeleteAsync(organizationId, subscriberId, ct);
        return NoContent();
    }

    [HttpGet("subscribers/import-template"), Authorize(Policy = Permissions.SubscribersRead)]
    public IActionResult ImportTemplate(Guid organizationId, [FromQuery] SubscriberType type = SubscriberType.Student)
    {
        var f = _subscribers.ImportTemplate(type);
        return File(f.Content, f.ContentType, f.FileName);
    }

    [HttpPost("subscribers/import"), Authorize(Policy = Permissions.SubscribersWrite)]
    [RequestSizeLimit(MaxImportBytes)]
    public async Task<ActionResult<ImportResultDto>> Import(Guid organizationId, [FromQuery] SubscriberType type, IFormFile file,
        [FromQuery] bool updateExisting = false, CancellationToken ct = default)
    {
        if (file == null || file.Length == 0) throw new ValidationFailedException("Choose an .xlsx file to import.");
        if (file.Length > MaxImportBytes) throw new ValidationFailedException("The file is larger than 10 MB.");
        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)) throw new ValidationFailedException("Only .xlsx files are supported.");
        await using var stream = file.OpenReadStream();
        return Ok(await _subscribers.ImportAsync(organizationId, type, stream, updateExisting, ct));
    }

    [HttpGet("subscribers/export"), Authorize(Policy = Permissions.SubscribersRead)]
    public async Task<IActionResult> Export(Guid organizationId, [FromQuery] SubscriberQuery query, CancellationToken ct)
    {
        var f = await _subscribers.ExportAsync(organizationId, query, ct);
        return File(f.Content, f.ContentType, f.FileName);
    }
}
