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
    };
}

public static class SystemRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string OrgAdmin = "OrgAdmin";
    public const string SiteAdmin = "SiteAdmin";
    public const string ReadOnly = "ReadOnly";
}
