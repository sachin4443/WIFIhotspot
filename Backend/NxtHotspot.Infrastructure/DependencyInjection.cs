using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NxtHotspot.Application.Common;
using NxtHotspot.Infrastructure.Persistence;
using NxtHotspot.Infrastructure.Security;
using NxtHotspot.Infrastructure.Services;
using NxtHotspot.Infrastructure.Sms;

namespace NxtHotspot.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("NxtHotspot")
            ?? throw new InvalidOperationException("ConnectionStrings:NxtHotspot is not configured.");

        services.Configure<SecurityOptions>(configuration.GetSection("Security"));
        services.Configure<SmsOptions>(configuration.GetSection("Sms"));

        services.AddDbContext<NxtDbContext>(o => o.UseNpgsql(connectionString));

        services.AddSingleton<ISecretProtector, AesGcmSecretProtector>();
        services.AddSingleton<IPasswordService, PasswordService>();

        services.AddScoped<ITenantAccess, TenantAccess>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IOrganizationService, OrganizationService>();
        services.AddScoped<ISiteService, SiteService>();
        services.AddScoped<IAdminUserService, AdminUserService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IWalletService, WalletService>();
        services.AddScoped<ISmsSettingResolver, SmsSettingResolver>();
        services.AddScoped<IOrganizationConfigService, OrganizationConfigService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<ISiteDirectoryService, SiteDirectoryService>();
        services.AddScoped<IPlanService, PlanService>();
        services.Configure<Hotspot.RadiusOptions>(configuration.GetSection("Radius"));
        services.AddScoped<IDisconnectQueue, DisconnectQueue>();
        services.AddScoped<INasService, NasService>();
        services.AddScoped<IPackageService, PackageService>();
        services.AddScoped<ISubscriberGroupService, SubscriberGroupService>();
        services.AddScoped<ISubscriberService, SubscriberService>();
        services.AddScoped<DbSeeder>();
        return services;
    }
}
