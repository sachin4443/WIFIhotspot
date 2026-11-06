using Microsoft.EntityFrameworkCore;
using NxtHotspot.Application.Common;
using NxtHotspot.Domain.Entities;
using NxtHotspot.Domain.Enums;
using NxtHotspot.Infrastructure.Services;

namespace NxtHotspot.Tests;

public class WalletTests
{
    private static async Task<(TestEnv Env, Guid SiteId)> SetupAsync(decimal balance = 0)
    {
        var env = new TestEnv();
        var siteId = Guid.NewGuid();
        using var db = env.NewDb();
        db.SiteWallets.Add(new SiteWallet { OrganizationId = Guid.NewGuid(), SiteId = siteId, Balance = balance });
        await db.SaveChangesAsync();
        return (env, siteId);
    }

    [Fact]
    public async Task Credit_and_debit_update_balance_and_ledger()
    {
        var (env, siteId) = await SetupAsync();
        using var db = env.NewDb();
        var wallets = new WalletService(db);

        var credit = await wallets.CreditAsync(siteId, 100m, "TOPUP", null, null, null);
        var debit = await wallets.DebitAsync(siteId, 30.5m, "SMS", null, null, null);

        Assert.Equal(100m, credit.BalanceAfter);
        Assert.Equal(69.5m, debit.BalanceAfter);
        Assert.Equal(69.5m, (await wallets.GetAsync(siteId)).Balance);

        var page = await wallets.TransactionsAsync(siteId, 1, 10);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(WalletTransactionType.Debit, page.Items[0].Type); // newest first
    }

    [Fact]
    public async Task Debit_beyond_balance_is_rejected_and_balance_unchanged()
    {
        var (env, siteId) = await SetupAsync(10m);
        using var db = env.NewDb();
        var wallets = new WalletService(db);

        await Assert.ThrowsAsync<InsufficientBalanceException>(() => wallets.DebitAsync(siteId, 10.01m, null, null, null, null));

        using var check = env.NewDb();
        Assert.Equal(10m, (await new WalletService(check).GetAsync(siteId)).Balance);
        Assert.Equal(0, await check.SiteWalletTransactions.CountAsync());
    }

    [Fact]
    public async Task Same_idempotency_key_is_applied_once()
    {
        var (env, siteId) = await SetupAsync();
        using var db = env.NewDb();
        var wallets = new WalletService(db);

        var first = await wallets.CreditAsync(siteId, 50m, null, null, "req-1", null);
        var second = await wallets.CreditAsync(siteId, 50m, null, null, "req-1", null);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(50m, (await wallets.GetAsync(siteId)).Balance);
        Assert.Equal(1, await db.SiteWalletTransactions.CountAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Non_positive_amounts_are_rejected(double amount)
    {
        var (env, siteId) = await SetupAsync();
        using var db = env.NewDb();
        await Assert.ThrowsAsync<ValidationFailedException>(() => new WalletService(db).CreditAsync(siteId, (decimal)amount, null, null, null, null));
    }

    [Fact]
    public async Task Unknown_wallet_is_not_found()
    {
        var env = new TestEnv();
        using var db = env.NewDb();
        await Assert.ThrowsAsync<NotFoundException>(() => new WalletService(db).GetAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Concurrent_stale_write_is_detected_by_the_version_token()
    {
        var (env, siteId) = await SetupAsync(100m);
        using var a = env.NewDb();
        using var b = env.NewDb();
        var walletA = await a.SiteWallets.FirstAsync(w => w.SiteId == siteId);
        var walletB = await b.SiteWallets.FirstAsync(w => w.SiteId == siteId);

        walletA.Balance += 10; walletA.Version++;
        await a.SaveChangesAsync();

        walletB.Balance += 20; walletB.Version++;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => b.SaveChangesAsync());
    }
}
