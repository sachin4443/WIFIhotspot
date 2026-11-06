using Microsoft.EntityFrameworkCore;
using NxtHotspot.Application.Common;
using NxtHotspot.Application.Dtos;
using NxtHotspot.Domain.Entities;
using NxtHotspot.Domain.Enums;
using NxtHotspot.Infrastructure.Persistence;

namespace NxtHotspot.Infrastructure.Services;

public sealed class WalletService : IWalletService
{
    private const int MaxAttempts = 4;
    private readonly NxtDbContext _db;

    public WalletService(NxtDbContext db) => _db = db;

    public async Task<WalletDto> GetAsync(Guid siteId, CancellationToken ct = default)
    {
        var w = await _db.SiteWallets.AsNoTracking().FirstOrDefaultAsync(x => x.SiteId == siteId, ct)
                ?? throw new NotFoundException("Wallet not found.");
        return new WalletDto(w.Id, w.SiteId, w.OrganizationId, w.Balance, w.Currency, w.IsActive);
    }

    public Task<WalletTransactionDto> CreditAsync(Guid siteId, decimal amount, string? reference, string? description,
        string? idempotencyKey, Guid? adminUserId, CancellationToken ct = default) =>
        ApplyAsync(siteId, WalletTransactionType.Credit, amount, reference, description, idempotencyKey, adminUserId, ct);

    public Task<WalletTransactionDto> DebitAsync(Guid siteId, decimal amount, string? reference, string? description,
        string? idempotencyKey, Guid? adminUserId, CancellationToken ct = default) =>
        ApplyAsync(siteId, WalletTransactionType.Debit, amount, reference, description, idempotencyKey, adminUserId, ct);

    public async Task<PagedResult<WalletTransactionDto>> TransactionsAsync(Guid siteId, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var q = _db.SiteWalletTransactions.AsNoTracking().Where(t => t.SiteId == siteId);
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<WalletTransactionDto>(rows.Select(Map).ToList(), page, pageSize, total);
    }

    private async Task<WalletTransactionDto> ApplyAsync(Guid siteId, WalletTransactionType type, decimal amount,
        string? reference, string? description, string? idempotencyKey, Guid? adminUserId, CancellationToken ct)
    {
        amount = decimal.Round(amount, 4);
        if (amount <= 0) throw new ValidationFailedException("Amount must be greater than zero.");
        if (amount > 10_000_000m) throw new ValidationFailedException("Amount is too large.");

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            if (idempotencyKey != null)
            {
                var existing = await _db.SiteWalletTransactions.AsNoTracking()
                    .FirstOrDefaultAsync(t => t.SiteId == siteId && t.IdempotencyKey == idempotencyKey, ct);
                if (existing != null) return Map(existing);
            }

            var wallet = await _db.SiteWallets.FirstOrDefaultAsync(w => w.SiteId == siteId, ct)
                         ?? throw new NotFoundException("Wallet not found.");
            if (!wallet.IsActive) throw new ConflictException("Wallet is inactive.");

            var newBalance = type == WalletTransactionType.Credit ? wallet.Balance + amount : wallet.Balance - amount;
            if (newBalance < 0) throw new InsufficientBalanceException("Insufficient wallet balance.");

            wallet.Balance = newBalance;
            wallet.Version++;
            var txn = new SiteWalletTransaction
            {
                OrganizationId = wallet.OrganizationId,
                SiteId = wallet.SiteId,
                SiteWalletId = wallet.Id,
                Type = type,
                Amount = amount,
                BalanceAfter = newBalance,
                Reference = reference,
                Description = description,
                CreatedByAdminUserId = adminUserId,
                IdempotencyKey = idempotencyKey
            };
            _db.SiteWalletTransactions.Add(txn);

            try
            {
                await _db.SaveChangesAsync(ct);
                return Map(txn);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Another request changed the balance first: reload and retry.
                _db.ChangeTracker.Clear();
            }
            catch (DbUpdateException) when (idempotencyKey != null)
            {
                // Possibly a concurrent request with the same idempotency key: loop re-checks for it.
                _db.ChangeTracker.Clear();
            }
        }
        throw new ConflictException("The wallet is busy. Please retry.");
    }

    private static WalletTransactionDto Map(SiteWalletTransaction t) =>
        new(t.Id, t.SiteWalletId, t.Type, t.Amount, t.BalanceAfter, t.Reference, t.Description, t.CreatedAt);
}
