using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NxtHotspot.Application.Common;
using NxtHotspot.Application.Dtos;
using NxtHotspot.Domain.Entities;
using NxtHotspot.Domain.Enums;
using NxtHotspot.Infrastructure.Persistence;
using NxtHotspot.Infrastructure.Security;
using NxtHotspot.Infrastructure.Services;
using NxtHotspot.Infrastructure.Sms;
using P = NxtHotspot.Application.Common.Permissions;

namespace NxtHotspot.Tests;

public sealed class FakeCurrentUser : ICurrentUser
{
    public Guid? AdminUserId { get; set; } = Guid.NewGuid();
    public Guid? OrganizationId { get; set; }
    public bool IsPlatformAdmin { get; set; }
    public bool HasAllSites { get; set; }
    public IReadOnlySet<string> Permissions { get; set; } = new HashSet<string>();

    public static FakeCurrentUser Platform() => new()
    {
        IsPlatformAdmin = true,
        HasAllSites = true,
        Permissions = P.All.Select(p => p.Code).ToHashSet()
    };

    public static FakeCurrentUser OrgAdmin(Guid organizationId) => new()
    {
        OrganizationId = organizationId,
        HasAllSites = true,
        Permissions = P.All.Where(p => !p.PlatformOnly).Select(p => p.Code).ToHashSet()
    };

    public static FakeCurrentUser Limited(Guid organizationId, Guid? adminUserId, params string[] permissions) => new()
    {
        OrganizationId = organizationId,
        AdminUserId = adminUserId ?? Guid.NewGuid(),
        HasAllSites = false,
        Permissions = permissions.ToHashSet()
    };
}

public sealed class FakeTokens : ITokenService
{
    public TokenResult Create(AdminUser user, IReadOnlyCollection<string> roles, IReadOnlyCollection<string> permissions) =>
        new("test-token", DateTime.UtcNow.AddMinutes(30));
}

/// <summary>Shared in-memory database plus service factories. Each NewDb() call simulates a fresh request scope.</summary>
public sealed class TestEnv
{
    public const string StrongPassword = "Str0ngPassw0rd!";

    public string DbName { get; } = Guid.NewGuid().ToString();
    public IPasswordService Passwords { get; } = new PasswordService();
    public ISecretProtector Protector { get; } = new AesGcmSecretProtector(Options.Create(new SecurityOptions
    {
        EncryptionKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
    }));

    public NxtDbContext NewDb() =>
        new(new DbContextOptionsBuilder<NxtDbContext>().UseInMemoryDatabase(DbName).Options);

    public static async Task<TestEnv> CreateAsync(string? bootstrapUser = null, string? bootstrapPassword = null)
    {
        var env = new TestEnv();
        await env.SeedAsync(bootstrapUser, bootstrapPassword);
        return env;
    }

    public async Task SeedAsync(string? bootstrapUser = null, string? bootstrapPassword = null)
    {
        using var db = NewDb();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Bootstrap:SuperAdmin:Username"] = bootstrapUser,
            ["Bootstrap:SuperAdmin:Password"] = bootstrapPassword
        }).Build();
        await new DbSeeder(db, Passwords, config, NullLogger<DbSeeder>.Instance).SeedAsync();
    }

    public OrganizationService Orgs(NxtDbContext db, ICurrentUser user, SmsOptions? sms = null) =>
        new(db, Passwords, Protector, user, new TenantAccess(db, user), Options.Create(sms ?? new SmsOptions()));

    public SiteService Sites(NxtDbContext db, ICurrentUser user) => new(db, user, new TenantAccess(db, user));
    public AdminUserService Users(NxtDbContext db, ICurrentUser user) => new(db, Passwords, user);
    public RoleService Roles(NxtDbContext db, ICurrentUser user) => new(db, user);
    public AuthService Auth(NxtDbContext db) => new(db, Passwords, new FakeTokens());

    public async Task<OrganizationDetailDto> RegisterAsync(string typeName, string orgName, string adminUsername,
        decimal balance = 0, SmsProviderRequest? sms = null, List<AuthenticationType>? methods = null, Guid? planId = null)
    {
        using var db = NewDb();
        var typeId = await db.OrganizationTypes.Where(t => t.TypeName == typeName).Select(t => t.OrganizationTypeId).FirstAsync();
        var request = new RegisterOrganizationRequest
        {
            OrganizationName = orgName,
            OrganizationTypeId = typeId,
            ContactPerson = "Asha Rao",
            MobileNumber = "9876543210",
            Email = "owner@example.com",
            AdminUsername = adminUsername,
            AdminPassword = StrongPassword,
            InitialWalletBalance = balance,
            Sms = sms,
            DefaultLoginMethods = methods,
            SubscriptionPlanId = planId
        };
        return await Orgs(db, FakeCurrentUser.Platform()).RegisterAsync(request);
    }

    public async Task<Guid> RoleIdAsync(string name)
    {
        using var db = NewDb();
        return await db.Roles.Where(r => r.OrganizationId == null && r.Name == name).Select(r => r.Id).FirstAsync();
    }
}
