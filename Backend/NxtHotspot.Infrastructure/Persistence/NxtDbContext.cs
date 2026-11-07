using Microsoft.EntityFrameworkCore;
using NxtHotspot.Domain.Common;
using NxtHotspot.Domain.Entities;

namespace NxtHotspot.Infrastructure.Persistence;

public class NxtDbContext : DbContext
{
    public NxtDbContext(DbContextOptions<NxtDbContext> options) : base(options) { }

    public DbSet<OrganizationType> OrganizationTypes => Set<OrganizationType>();
    public DbSet<OrganizationTypeDefaultLoginMethod> OrganizationTypeDefaultLoginMethods => Set<OrganizationTypeDefaultLoginMethod>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationSettings> OrganizationSettings => Set<OrganizationSettings>();
    public DbSet<OrganizationBranding> OrganizationBranding => Set<OrganizationBranding>();
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<OrganizationLoginMethod> OrganizationLoginMethods => Set<OrganizationLoginMethod>();
    public DbSet<OrganizationSmsSetting> OrganizationSmsSettings => Set<OrganizationSmsSetting>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<AdminUserRole> AdminUserRoles => Set<AdminUserRole>();
    public DbSet<AdminUserSite> AdminUserSites => Set<AdminUserSite>();
    public DbSet<SiteWallet> SiteWallets => Set<SiteWallet>();
    public DbSet<SiteWalletTransaction> SiteWalletTransactions => Set<SiteWalletTransaction>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // ---- Hotspot, RADIUS and captive portal ----
    public DbSet<NasDevice> NasDevices => Set<NasDevice>();
    public DbSet<InternetPackage> InternetPackages => Set<InternetPackage>();
    public DbSet<SubscriberGroup> SubscriberGroups => Set<SubscriberGroup>();
    public DbSet<Subscriber> Subscribers => Set<Subscriber>();
    public DbSet<RadiusCredential> RadiusCredentials => Set<RadiusCredential>();
    public DbSet<OtpChallenge> OtpChallenges => Set<OtpChallenge>();
    public DbSet<SmsLog> SmsLogs => Set<SmsLog>();
    public DbSet<RadiusSession> RadiusSessions => Set<RadiusSession>();
    public DbSet<RadiusAccountingEvent> RadiusAccountingEvents => Set<RadiusAccountingEvent>();
    public DbSet<RadiusRequestLog> RadiusRequestLogs => Set<RadiusRequestLog>();
    public DbSet<DisconnectCommand> DisconnectCommands => Set<DisconnectCommand>();
    public DbSet<RadiusServerStatus> RadiusServerStatuses => Set<RadiusServerStatus>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Enums are stored as readable strings.
        b.Entity<OrganizationTypeDefaultLoginMethod>().Property(x => x.AuthenticationType).HasConversion<string>().HasMaxLength(40);
        b.Entity<OrganizationLoginMethod>().Property(x => x.AuthenticationType).HasConversion<string>().HasMaxLength(40);
        b.Entity<Organization>().Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Entity<SiteWalletTransaction>().Property(x => x.Type).HasConversion<string>().HasMaxLength(20);

        b.Entity<OrganizationType>(e =>
        {
            e.ToTable("OrganizationTypes");
            e.HasKey(x => x.OrganizationTypeId);
            e.Property(x => x.TypeName).HasMaxLength(100).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
            e.HasIndex(x => x.TypeName).IsUnique();
        });

        b.Entity<OrganizationTypeDefaultLoginMethod>(e =>
        {
            e.ToTable("OrganizationTypeDefaultLoginMethods");
            e.HasOne(x => x.OrganizationType).WithMany(t => t.DefaultLoginMethods)
                .HasForeignKey(x => x.OrganizationTypeId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.OrganizationTypeId, x.AuthenticationType }).IsUnique();
        });

        b.Entity<SubscriptionPlan>(e =>
        {
            e.ToTable("SubscriptionPlans");
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.MonthlyPrice).HasPrecision(18, 2);
            e.HasIndex(x => x.Name).IsUnique();
        });

        b.Entity<Organization>(e =>
        {
            e.ToTable("Organizations");
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.ContactPerson).HasMaxLength(150).IsRequired();
            e.Property(x => x.MobileNumber).HasMaxLength(20).IsRequired();
            e.Property(x => x.Email).HasMaxLength(254).IsRequired();
            e.Property(x => x.Address).HasMaxLength(500);
            e.HasOne(x => x.OrganizationType).WithMany().HasForeignKey(x => x.OrganizationTypeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.SubscriptionPlan).WithMany().HasForeignKey(x => x.SubscriptionPlanId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.Name);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.OrganizationTypeId);
        });

        b.Entity<OrganizationSettings>(e =>
        {
            e.ToTable("OrganizationSettings");
            e.Property(x => x.TimeZoneId).HasMaxLength(64).IsRequired();
            e.HasOne(x => x.Organization).WithOne(o => o.Settings)
                .HasForeignKey<OrganizationSettings>(x => x.OrganizationId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.OrganizationId).IsUnique();
        });

        b.Entity<OrganizationBranding>(e =>
        {
            e.ToTable("OrganizationBranding");
            e.Property(x => x.LogoUrl).HasMaxLength(500);
            e.Property(x => x.BackgroundUrl).HasMaxLength(500);
            e.Property(x => x.WelcomeMessage).HasMaxLength(500);
            e.Property(x => x.ThemeColor).HasMaxLength(7);
            e.HasOne(x => x.Organization).WithOne(o => o.Branding)
                .HasForeignKey<OrganizationBranding>(x => x.OrganizationId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.OrganizationId).IsUnique();
        });

        b.Entity<Site>(e =>
        {
            e.ToTable("Sites");
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.Property(x => x.Code).HasMaxLength(30).IsRequired();
            e.Property(x => x.Address).HasMaxLength(500);
            e.Property(x => x.City).HasMaxLength(100);
            e.HasOne(x => x.Organization).WithMany(o => o.Sites).HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.OrganizationId, x.Code }).IsUnique();
            // At most one primary site per organization.
            e.HasIndex(x => x.OrganizationId).IsUnique().HasFilter("\"IsPrimary\" = true").HasDatabaseName("UX_Sites_OneOrgPrimary");
        });

        b.Entity<OrganizationLoginMethod>(e =>
        {
            e.ToTable("OrganizationLoginMethods", t => t.HasCheckConstraint("CK_OrganizationLoginMethods_MaxDevices", "\"MaxDevices\" >= 1"));
            e.HasKey(x => x.LoginMethodId);
            e.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.OrganizationId, x.AuthenticationType }).IsUnique()
                .HasFilter("\"SiteId\" IS NULL").HasDatabaseName("UX_LoginMethods_Org");
            e.HasIndex(x => new { x.SiteId, x.AuthenticationType }).IsUnique()
                .HasFilter("\"SiteId\" IS NOT NULL").HasDatabaseName("UX_LoginMethods_Site");
        });

        b.Entity<OrganizationSmsSetting>(e =>
        {
            // A site-level override must belong to an organization; only the platform default has neither.
            e.ToTable("OrganizationSmsSettings", t => t.HasCheckConstraint("CK_OrganizationSmsSettings_SiteNeedsOrg",
                "\"SiteId\" IS NULL OR \"OrganizationId\" IS NOT NULL"));
            e.HasKey(x => x.SmsSettingId);
            e.Property(x => x.ProviderName).HasMaxLength(50).IsRequired();
            e.Property(x => x.ApiEndpoint).HasMaxLength(500).IsRequired();
            e.Property(x => x.ApiKeyEncrypted).HasMaxLength(1000).IsRequired();
            e.Property(x => x.SenderId).HasMaxLength(20).IsRequired();
            e.Property(x => x.MessageTemplate).HasMaxLength(500).IsRequired();
            e.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.OrganizationId).IsUnique()
                .HasFilter("\"SiteId\" IS NULL AND \"OrganizationId\" IS NOT NULL").HasDatabaseName("UX_SmsSettings_Org");
            e.HasIndex(x => x.SiteId).IsUnique()
                .HasFilter("\"SiteId\" IS NOT NULL").HasDatabaseName("UX_SmsSettings_Site");
        });

        b.Entity<AdminUser>(e =>
        {
            e.ToTable("AdminUsers");
            e.Property(x => x.Username).HasMaxLength(50).IsRequired();
            e.Property(x => x.NormalizedUsername).HasMaxLength(50).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
            e.Property(x => x.FullName).HasMaxLength(150).IsRequired();
            e.Property(x => x.Email).HasMaxLength(254).IsRequired();
            e.Property(x => x.MobileNumber).HasMaxLength(20);
            e.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.NormalizedUsername).IsUnique();
            e.HasIndex(x => x.OrganizationId);
        });

        b.Entity<Role>(e =>
        {
            e.ToTable("Roles");
            e.Property(x => x.Name).HasMaxLength(80).IsRequired();
            e.Property(x => x.Description).HasMaxLength(300);
            e.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.Name).IsUnique().HasFilter("\"OrganizationId\" IS NULL").HasDatabaseName("UX_Roles_System");
            e.HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique().HasFilter("\"OrganizationId\" IS NOT NULL").HasDatabaseName("UX_Roles_Org");
        });

        b.Entity<Permission>(e =>
        {
            e.ToTable("Permissions");
            e.Property(x => x.Code).HasMaxLength(100).IsRequired();
            e.Property(x => x.Description).HasMaxLength(300);
            e.HasIndex(x => x.Code).IsUnique();
        });

        b.Entity<RolePermission>(e =>
        {
            e.ToTable("RolePermissions");
            e.HasKey(x => new { x.RoleId, x.PermissionId });
            e.HasOne(x => x.Role).WithMany(r => r.RolePermissions).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Permission).WithMany().HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<AdminUserRole>(e =>
        {
            e.ToTable("AdminUserRoles");
            e.HasKey(x => new { x.AdminUserId, x.RoleId });
            e.HasOne(x => x.AdminUser).WithMany(u => u.UserRoles).HasForeignKey(x => x.AdminUserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<AdminUserSite>(e =>
        {
            e.ToTable("AdminUserSites");
            e.HasKey(x => new { x.AdminUserId, x.SiteId });
            e.HasOne(x => x.AdminUser).WithMany(u => u.UserSites).HasForeignKey(x => x.AdminUserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<SiteWallet>(e =>
        {
            e.ToTable("SiteWallets", t => t.HasCheckConstraint("CK_SiteWallets_Balance_NonNegative", "\"Balance\" >= 0"));
            e.Property(x => x.Balance).HasPrecision(18, 4);
            e.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.SiteId).IsUnique();
            e.HasIndex(x => x.OrganizationId);
        });

        b.Entity<SiteWalletTransaction>(e =>
        {
            e.ToTable("SiteWalletTransactions", t =>
            {
                t.HasCheckConstraint("CK_SiteWalletTransactions_Amount_Positive", "\"Amount\" > 0");
                t.HasCheckConstraint("CK_SiteWalletTransactions_BalanceAfter_NonNegative", "\"BalanceAfter\" >= 0");
            });
            e.Property(x => x.Amount).HasPrecision(18, 4);
            e.Property(x => x.BalanceAfter).HasPrecision(18, 4);
            e.Property(x => x.Reference).HasMaxLength(100);
            e.Property(x => x.Description).HasMaxLength(300);
            e.Property(x => x.IdempotencyKey).HasMaxLength(100);
            e.HasOne(x => x.SiteWallet).WithMany().HasForeignKey(x => x.SiteWalletId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.SiteWalletId, x.CreatedAt });
            e.HasIndex(x => new { x.SiteWalletId, x.IdempotencyKey }).IsUnique().HasFilter("\"IdempotencyKey\" IS NOT NULL")
                .HasDatabaseName("UX_WalletTxn_Idempotency");
            e.HasIndex(x => x.OrganizationId);
        });

        b.Entity<AuditLog>(e =>
        {
            e.ToTable("AuditLogs");
            e.Property(x => x.Action).HasMaxLength(100).IsRequired();
            e.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
            e.Property(x => x.EntityId).HasMaxLength(100);
            e.Property(x => x.Details).HasMaxLength(2000);
            e.Property(x => x.IpAddress).HasMaxLength(64);
            e.HasIndex(x => new { x.OrganizationId, x.CreatedAt });
            e.HasIndex(x => x.AdminUserId);
        });

        ConfigureHotspot(b);
    }

    private static void ConfigureHotspot(ModelBuilder b)
    {
        b.Entity<NasDevice>(e =>
        {
            e.ToTable("NasDevices", t =>
            {
                t.HasCheckConstraint("CK_NasDevices_Ports", "\"AuthPort\" BETWEEN 1 AND 65535 AND \"AcctPort\" BETWEEN 1 AND 65535 AND \"CoaPort\" BETWEEN 1 AND 65535");
            });
            e.Property(x => x.Vendor).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.Property(x => x.IpAddress).HasMaxLength(45).IsRequired();
            e.Property(x => x.RouterOsVersion).HasMaxLength(20);
            e.Property(x => x.SharedSecretEncrypted).HasMaxLength(500).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.PortalKeyHash).HasMaxLength(64);
            e.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.IpAddress).IsUnique();
            e.HasIndex(x => x.PortalKeyHash).IsUnique().HasFilter("\"PortalKeyHash\" IS NOT NULL");
            e.HasIndex(x => new { x.OrganizationId, x.SiteId });
        });

        b.Entity<InternetPackage>(e =>
        {
            e.ToTable("InternetPackages", t =>
            {
                t.HasCheckConstraint("CK_InternetPackages_Limits", "\"DownloadKbps\" >= 0 AND \"UploadKbps\" >= 0 AND \"MaxDevices\" >= 1 AND \"Price\" >= 0");
            });
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.Price).HasPrecision(18, 2);
            e.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique();
        });

        b.Entity<SubscriberGroup>(e =>
        {
            e.ToTable("SubscriberGroups");
            e.Property(x => x.GroupType).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
            e.HasOne(x => x.DefaultPackage).WithMany().HasForeignKey(x => x.DefaultPackageId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.OrganizationId, x.GroupType, x.Name }).IsUnique();
        });

        b.Entity<Subscriber>(e =>
        {
            e.ToTable("Subscribers");
            e.Property(x => x.SubscriberType).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Identifier).HasMaxLength(100).IsRequired();
            e.Property(x => x.NormalizedIdentifier).HasMaxLength(100).IsRequired();
            e.Property(x => x.FullName).HasMaxLength(150).IsRequired();
            e.Property(x => x.MobileNumber).HasMaxLength(20);
            e.Property(x => x.Email).HasMaxLength(254);
            e.Property(x => x.PasswordHash).HasMaxLength(200);
            e.Property(x => x.RoomNumber).HasMaxLength(20);
            e.Property(x => x.Notes).HasMaxLength(500);
            e.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Group).WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Package).WithMany().HasForeignKey(x => x.PackageId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.OrganizationId, x.NormalizedIdentifier }).IsUnique();
            e.HasIndex(x => new { x.OrganizationId, x.SubscriberType, x.Status });
            e.HasIndex(x => new { x.OrganizationId, x.MobileNumber });
            e.HasIndex(x => new { x.SiteId, x.RoomNumber });
        });

        b.Entity<RadiusCredential>(e =>
        {
            e.ToTable("RadiusCredentials");
            e.Property(x => x.Username).HasMaxLength(64).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(64).IsRequired();
            e.Property(x => x.MacAddress).HasMaxLength(17);
            e.HasOne(x => x.Subscriber).WithMany().HasForeignKey(x => x.SubscriberId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.Username).IsUnique();
            e.HasIndex(x => x.ExpiresAt);
        });

        b.Entity<OtpChallenge>(e =>
        {
            e.ToTable("OtpChallenges");
            e.Property(x => x.Purpose).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.MobileNumber).HasMaxLength(20).IsRequired();
            e.Property(x => x.CodeHash).HasMaxLength(64).IsRequired();
            e.Property(x => x.IpAddress).HasMaxLength(64);
            e.Property(x => x.MacAddress).HasMaxLength(17);
            e.HasIndex(x => new { x.SiteId, x.MobileNumber, x.CreatedAt });
            e.HasIndex(x => x.ExpiresAt);
        });

        b.Entity<SmsLog>(e =>
        {
            e.ToTable("SmsLogs");
            e.Property(x => x.MobileMasked).HasMaxLength(20).IsRequired();
            e.Property(x => x.ProviderName).HasMaxLength(50).IsRequired();
            e.Property(x => x.Error).HasMaxLength(500);
            e.HasIndex(x => new { x.OrganizationId, x.CreatedAt });
        });

        b.Entity<RadiusSession>(e =>
        {
            e.ToTable("RadiusSessions", t => t.HasCheckConstraint("CK_RadiusSessions_Counters", "\"InputBytes\" >= 0 AND \"OutputBytes\" >= 0 AND \"SessionTimeSeconds\" >= 0"));
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Username).HasMaxLength(100).IsRequired();
            e.Property(x => x.AcctSessionId).HasMaxLength(100).IsRequired();
            e.Property(x => x.FramedIpAddress).HasMaxLength(45);
            e.Property(x => x.CallingStationId).HasMaxLength(64);
            e.Property(x => x.CalledStationId).HasMaxLength(100);
            e.Property(x => x.NasPortId).HasMaxLength(100);
            e.Property(x => x.TerminateCause).HasMaxLength(50);
            e.HasOne(x => x.Nas).WithMany().HasForeignKey(x => x.NasId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Subscriber).WithMany().HasForeignKey(x => x.SubscriberId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => new { x.NasId, x.AcctSessionId }).IsUnique();
            e.HasIndex(x => new { x.OrganizationId, x.Status });
            e.HasIndex(x => new { x.OrganizationId, x.StartedAt });
            e.HasIndex(x => new { x.SiteId, x.StartedAt });
            e.HasIndex(x => x.SubscriberId);
        });

        b.Entity<RadiusAccountingEvent>(e =>
        {
            e.ToTable("RadiusAccountingEvents");
            e.Property(x => x.AcctSessionId).HasMaxLength(100).IsRequired();
            e.HasIndex(x => new { x.NasId, x.AcctSessionId, x.StatusType, x.SessionTime, x.DelayTime }).IsUnique();
            e.HasIndex(x => x.ReceivedAt);
        });

        b.Entity<RadiusRequestLog>(e =>
        {
            e.ToTable("RadiusRequestLogs");
            e.Property(x => x.PacketKind).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Result).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.SourceIp).HasMaxLength(45).IsRequired();
            e.Property(x => x.Username).HasMaxLength(100);
            e.Property(x => x.MacAddress).HasMaxLength(64);
            e.Property(x => x.Reason).HasMaxLength(300);
            e.HasIndex(x => x.CreatedAt);
            e.HasIndex(x => new { x.OrganizationId, x.CreatedAt });
        });

        b.Entity<DisconnectCommand>(e =>
        {
            e.ToTable("DisconnectCommands");
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Result).HasMaxLength(300);
            e.HasIndex(x => new { x.Status, x.CreatedAt });
            e.HasIndex(x => x.SessionId);
        });

        b.Entity<RadiusServerStatus>(e =>
        {
            e.ToTable("RadiusServerStatuses");
            e.HasKey(x => x.InstanceId);
            e.Property(x => x.InstanceId).HasMaxLength(100);
            e.Property(x => x.Version).HasMaxLength(50);
            e.Property(x => x.HostName).HasMaxLength(100);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<BaseEntity>().Where(e => e.State == EntityState.Modified))
            entry.Entity.UpdatedAt = now;
        return base.SaveChangesAsync(cancellationToken);
    }
}
