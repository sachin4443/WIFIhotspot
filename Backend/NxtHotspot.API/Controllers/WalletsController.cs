using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NxtHotspot.Application.Common;
using NxtHotspot.Application.Dtos;

namespace NxtHotspot.API.Controllers;

[ApiController]
[Route("api/sites/{siteId:guid}/wallet")]
public sealed class WalletsController : ControllerBase
{
    private readonly IWalletService _wallets;
    private readonly ITenantAccess _tenant;
    private readonly ICurrentUser _current;

    public WalletsController(IWalletService wallets, ITenantAccess tenant, ICurrentUser current)
    {
        _wallets = wallets; _tenant = tenant; _current = current;
    }

    [HttpGet]
    [Authorize(Policy = Permissions.WalletRead)]
    public async Task<ActionResult<WalletDto>> Get(Guid siteId, CancellationToken ct)
    {
        await _tenant.GetAuthorizedSiteAsync(siteId, ct); // tenant + site authorization before touching the ledger
        return Ok(await _wallets.GetAsync(siteId, ct));
    }

    [HttpGet("transactions")]
    [Authorize(Policy = Permissions.WalletRead)]
    public async Task<ActionResult<PagedResult<WalletTransactionDto>>> Transactions(
        Guid siteId, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        await _tenant.GetAuthorizedSiteAsync(siteId, ct);
        return Ok(await _wallets.TransactionsAsync(siteId, page, pageSize, ct));
    }

    /// <summary>Platform admins add balance. Send an Idempotency-Key header to make retries safe.</summary>
    [HttpPost("credit")]
    [Authorize(Policy = Permissions.WalletCredit)]
    public async Task<ActionResult<WalletTransactionDto>> Credit(
        Guid siteId, [FromBody] CreditWalletRequest request, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        if (idempotencyKey is { Length: > 100 }) throw new ValidationFailedException("Idempotency-Key is too long.");
        await _tenant.GetAuthorizedSiteAsync(siteId, ct);
        var txn = await _wallets.CreditAsync(siteId, request.Amount, request.Reference, request.Description,
            string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim(), _current.AdminUserId, ct);
        return Ok(txn);
    }
}
