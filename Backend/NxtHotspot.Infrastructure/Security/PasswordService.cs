using Microsoft.AspNetCore.Identity;
using NxtHotspot.Application.Common;

namespace NxtHotspot.Infrastructure.Security;

/// <summary>PBKDF2 via ASP.NET Core's PasswordHasher (versioned format, upgradable iteration count).</summary>
public sealed class PasswordService : IPasswordService
{
    private static readonly PasswordHasher<object> Hasher = new();
    private static readonly object Subject = new();
    private static readonly string DummyHash = Hasher.HashPassword(Subject, Guid.NewGuid().ToString("N"));

    public string Hash(string password) => Hasher.HashPassword(Subject, password);

    public bool Verify(string hash, string password) =>
        Hasher.VerifyHashedPassword(Subject, hash, password) != PasswordVerificationResult.Failed;

    public void VerifyDummy(string password) => Hasher.VerifyHashedPassword(Subject, DummyHash, password);
}
