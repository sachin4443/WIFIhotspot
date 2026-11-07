using Microsoft.EntityFrameworkCore;
using NxtHotspot.Application.Common;
using NxtHotspot.Application.Dtos;
using NxtHotspot.Domain.Entities;
using NxtHotspot.Infrastructure.Persistence;

namespace NxtHotspot.Infrastructure.Services;

public sealed class PlanService : IPlanService
{
    private readonly NxtDbContext _db;
    private readonly ICurrentUser _current;

    public PlanService(NxtDbContext db, ICurrentUser current) { _db = db; _current = current; }

    private void EnsurePlatform()
    {
        if (!_current.IsPlatformAdmin) throw new ForbiddenException();
    }

    public async Task<List<SubscriptionPlanDetailDto>> ListAsync(CancellationToken ct = default)
    {
        EnsurePlatform();
        return await _db.SubscriptionPlans.AsNoTracking().OrderByDescending(p => p.IsActive).ThenBy(p => p.MonthlyPrice).ThenBy(p => p.Name)
            .Select(p => new SubscriptionPlanDetailDto(p.Id, p.Name, p.Description, p.MonthlyPrice, p.MaxSites, p.MaxNasDevices, p.MaxUsers,
                p.IsActive, _db.Organizations.Count(o => o.SubscriptionPlanId == p.Id), p.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<SubscriptionPlanDetailDto> CreateAsync(UpsertSubscriptionPlanRequest request, CancellationToken ct = default)
    {
        EnsurePlatform();
        var name = request.Name.Trim();
        if (await _db.SubscriptionPlans.AnyAsync(p => p.Name.ToLower() == name.ToLower(), ct))
            throw new ConflictException("A plan with this name already exists.");
        var plan = new SubscriptionPlan();
        Apply(plan, request);
        _db.SubscriptionPlans.Add(plan);
        _db.AuditLogs.Add(Audit.Entry(null, _current.AdminUserId, "SubscriptionPlanCreated", nameof(SubscriptionPlan), plan.Id.ToString(), $"Name={plan.Name}"));
        await _db.SaveChangesAsync(ct);
        return await GetAsync(plan.Id, ct);
    }

    public async Task<SubscriptionPlanDetailDto> UpdateAsync(Guid planId, UpsertSubscriptionPlanRequest request, CancellationToken ct = default)
    {
        EnsurePlatform();
        var plan = await _db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == planId, ct) ?? throw new NotFoundException("Plan not found.");
        var name = request.Name.Trim();
        if (await _db.SubscriptionPlans.AnyAsync(p => p.Id != planId && p.Name.ToLower() == name.ToLower(), ct))
            throw new ConflictException("A plan with this name already exists.");
        Apply(plan, request);
        _db.AuditLogs.Add(Audit.Entry(null, _current.AdminUserId, "SubscriptionPlanUpdated", nameof(SubscriptionPlan), plan.Id.ToString(), $"Name={plan.Name}"));
        await _db.SaveChangesAsync(ct);
        return await GetAsync(plan.Id, ct);
    }

    public async Task AssignAsync(Guid organizationId, Guid? planId, CancellationToken ct = default)
    {
        EnsurePlatform();
        var org = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == organizationId, ct) ?? throw new NotFoundException("Organization not found.");
        if (planId is { } id)
        {
            var plan = await _db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Plan not found.");
            if (!plan.IsActive) throw new ValidationFailedException("Inactive plans cannot be assigned.");
            var siteCount = await _db.Sites.CountAsync(s => s.OrganizationId == organizationId, ct);
            if (plan.MaxSites != null && siteCount > plan.MaxSites)
                throw new ConflictException($"The organization has {siteCount} sites; this plan allows {plan.MaxSites}.");
        }
        org.SubscriptionPlanId = planId;
        _db.AuditLogs.Add(Audit.Entry(organizationId, _current.AdminUserId, "SubscriptionPlanAssigned", "Organization", organizationId.ToString(),
            $"PlanId={planId?.ToString() ?? "none"}"));
        await _db.SaveChangesAsync(ct);
    }

    private async Task<SubscriptionPlanDetailDto> GetAsync(Guid id, CancellationToken ct) =>
        (await ListAsync(ct)).First(p => p.Id == id);

    private static void Apply(SubscriptionPlan plan, UpsertSubscriptionPlanRequest r)
    {
        plan.Name = r.Name.Trim();
        plan.Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim();
        plan.MonthlyPrice = r.MonthlyPrice;
        plan.MaxSites = r.MaxSites;
        plan.MaxNasDevices = r.MaxNasDevices;
        plan.MaxUsers = r.MaxUsers;
        plan.IsActive = r.IsActive;
    }
}
