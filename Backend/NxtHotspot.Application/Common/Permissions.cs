namespace NxtHotspot.Application.Common;

public static class Permissions
{
    public const string OrganizationsRead = "organizations.read";
    public const string OrganizationsWrite = "organizations.write";          // platform only
    public const string SitesRead = "sites.read";
    public const string SitesWrite = "sites.write";
    public const string AdminUsersRead = "adminusers.read";
    public const string AdminUsersWrite = "adminusers.write";
    public const string RolesRead = "roles.read";
    public const string RolesWrite = "roles.write";
    public const string WalletRead = "wallet.read";
    public const string WalletCredit = "wallet.credit";                      // platform only
    public const string OrganizationTypesRead = "organizationtypes.read";
    public const string SubscriptionPlansRead = "subscriptionplans.read";
    public const string SettingsRead = "settings.read";                      // org profile/settings/branding/login methods/SMS (no secrets)
    public const string SettingsWrite = "settings.write";
    public const string AuditRead = "audit.read";
    public const string OrganizationTypesWrite = "organizationtypes.write";  // platform only
    public const string PlatformSmsWrite = "platform.sms.write";             // platform only
    public const string WalletDebit = "wallet.debit";                        // platform only (manual adjustments)
    public const string SubscriptionPlansWrite = "subscriptionplans.write";  // platform only
    public const string NasRead = "nas.read";
    public const string NasWrite = "nas.write";
    public const string PackagesRead = "packages.read";
    public const string PackagesWrite = "packages.write";
    public const string SubscribersRead = "subscribers.read";
    public const string SubscribersWrite = "subscribers.write";
    public const string SessionsRead = "sessions.read";
    public const string SessionsDisconnect = "sessions.disconnect";
    public const string ReportsRead = "reports.read";
    public const string RadiusMonitor = "radius.monitor";                    // platform only

    /// <summary>Code, description, platform-only flag.</summary>
    public static readonly IReadOnlyList<(string Code, string Description, bool PlatformOnly)> All = new[]
    {
        (OrganizationsRead, "View organizations", false),
        (OrganizationsWrite, "Register organizations and change their status", true),
        (SitesRead, "View sites", false),
        (SitesWrite, "Create and edit sites", false),
        (AdminUsersRead, "View admin users", false),
        (AdminUsersWrite, "Create and edit admin users and their roles/sites", false),
        (RolesRead, "View roles and permissions", false),
        (RolesWrite, "Create and edit roles", false),
        (WalletRead, "View wallets and transactions", false),
        (WalletCredit, "Add balance to wallets", true),
        (OrganizationTypesRead, "View organization types", false),
        (SubscriptionPlansRead, "View subscription plans", false),
        (SettingsRead, "View organization profile, settings, branding, login methods and SMS configuration (never API keys)", false),
        (SettingsWrite, "Change organization profile, settings, branding, login methods and SMS configuration", false),
        (AuditRead, "View the audit log", false),
        (OrganizationTypesWrite, "Create and edit organization types", true),
        (PlatformSmsWrite, "Configure the platform default SMS provider", true),
        (WalletDebit, "Manually deduct wallet balance", true),
        (SubscriptionPlansWrite, "Create and edit subscription plans", true),
        (NasRead, "View NAS devices (never shared secrets)", false),
        (NasWrite, "Create and edit NAS devices and their shared secrets", false),
        (PackagesRead, "View internet packages", false),
        (PackagesWrite, "Create and edit internet packages and bandwidth policies", false),
        (SubscribersRead, "View hotspot subscribers, groups and guests", false),
        (SubscribersWrite, "Create, import, edit and suspend hotspot subscribers", false),
        (SessionsRead, "View active sessions and accounting", false),
        (SessionsDisconnect, "Disconnect active hotspot sessions", false),
        (ReportsRead, "View usage reports", false),
        (RadiusMonitor, "Monitor the RADIUS server and request logs across all organizations", true),
    };

    /// <summary>Default permissions of the Site Admin system role.</summary>
    public static readonly IReadOnlySet<string> SiteAdminDefaults = new HashSet<string>(StringComparer.Ordinal)
    {
        SitesRead, AdminUsersRead, WalletRead, RolesRead, SettingsRead,
        NasRead, PackagesRead, SubscribersRead, SubscribersWrite, SessionsRead, SessionsDisconnect, ReportsRead
    };
}

public static class SystemRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string OrgAdmin = "OrgAdmin";
    public const string SiteAdmin = "SiteAdmin";
    public const string ReadOnly = "ReadOnly";
}
