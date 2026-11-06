using Microsoft.EntityFrameworkCore;
using NxtHotspot.Application.Common;
using NxtHotspot.Application.Dtos;
using NxtHotspot.Domain.Enums;
using NxtHotspot.Infrastructure.Persistence;

namespace NxtHotspot.Infrastructure.Services;

public sealed class AuthService : IAuthService
{
    private const int MaxFailures = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private const string Generic = "Invalid username or password.";

    private readonly NxtDbContext _db;
    private readonly IPasswordService _passwords;
    private readonly ITokenService _tokens;

    public AuthService(NxtDbContext db, IPasswordService passwords, ITokenService tokens)
    {
        _db = db; _passwords = passwords; _tokens = tokens;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken ct = default)
    {
        var normalized = request.Username.Trim().ToUpperInvariant();
        var user = await _db.AdminUsers
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role).ThenInclude(r => r!.RolePermissions).ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.NormalizedUsername == normalized, ct);

        if (user == null)
        {
            _passwords.VerifyDummy(request.Password); // keep timing similar to a real check
            throw new UnauthorizedAppException(Generic);
        }

        var now = DateTime.UtcNow;
        if (user.LockoutEnd is { } lockoutEnd && lockoutEnd > now)
            throw new UnauthorizedAppException("Account is temporarily locked. Try again later.");

        if (!_passwords.Verify(user.PasswordHash, request.Password))
        {
            user.FailedLoginCount++;
            var locked = user.FailedLoginCount >= MaxFailures;
            if (locked)
            {
                user.LockoutEnd = now + LockoutDuration;
                user.FailedLoginCount = 0;
            }
            _db.AuditLogs.Add(Audit.Entry(user.OrganizationId, user.Id, locked ? "LoginLockout" : "LoginFailed",
                "AdminUser", user.Id.ToString(), ip: ipAddress));
            await _db.SaveChangesAsync(ct);
            throw new UnauthorizedAppException(Generic);
        }

        if (!user.IsActive) throw new UnauthorizedAppException(Generic);

        if (user.OrganizationId is { } orgId)
        {
            var status = await _db.Organizations.Where(o => o.Id == orgId).Select(o => o.Status).FirstOrDefaultAsync(ct);
            if (status != OrganizationStatus.Active)
                throw new ForbiddenException("This organization is not active.");
        }

        user.FailedLoginCount = 0;
        user.LockoutEnd = null;
        user.LastLoginAt = now;
        _db.AuditLogs.Add(Audit.Entry(user.OrganizationId, user.Id, "LoginSuccess", "AdminUser", user.Id.ToString(), ip: ipAddress));
        await _db.SaveChangesAsync(ct);

        var roles = user.UserRoles.Select(ur => ur.Role!.Name).Distinct().ToList();
        var permissions = user.UserRoles.SelectMany(ur => ur.Role!.RolePermissions)
            .Select(rp => rp.Permission!.Code).Distinct().ToList();

        var token = _tokens.Create(user, roles, permissions);
        return new LoginResponse(token.AccessToken, token.ExpiresAtUtc, user.Id, user.Username, user.FullName,
            user.OrganizationId, user.OrganizationId == null, roles, permissions);
    }
}
