using NxtHotspot.Infrastructure;
using NxtHotspot.Radius.Application;
using NxtHotspot.RadiusServer;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = Host.CreateApplicationBuilder(args);

    // Runs as a Windows Service when installed with sc.exe / New-Service; as a console app otherwise.
    builder.Services.AddWindowsService(o => o.ServiceName = "NxtHotspotRadius");
    builder.Services.AddSerilog((services, cfg) =>
    {
        cfg.ReadFrom.Configuration(builder.Configuration)
           .WriteTo.Console()
           .WriteTo.File(Path.Combine(AppContext.BaseDirectory, "logs", "radius-.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30);
        if (OperatingSystem.IsWindows()) cfg.WriteTo.EventLog("NxtHotspotRadius", manageEventSource: false, restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Warning);
    });

    if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("NxtHotspot")))
        throw new InvalidOperationException("ConnectionStrings:NxtHotspot must be set (environment variable or user-secrets).");
    if (string.IsNullOrWhiteSpace(builder.Configuration["Security:EncryptionKey"]))
        throw new InvalidOperationException("Security:EncryptionKey must be set to the same value the API uses (NAS secrets are encrypted with it).");

    builder.Services.Configure<RadiusServerOptions>(builder.Configuration.GetSection("Radius"));
    builder.Services.AddRadiusDataAccess(builder.Configuration);
    builder.Services.AddSingleton<RadiusCounters>();
    builder.Services.AddSingleton<NasDirectory>();
    builder.Services.AddScoped<RadiusAuthService>();
    builder.Services.AddScoped<RadiusAccountingService>();
    builder.Services.AddScoped<DisconnectSender>();
    builder.Services.AddScoped<RadiusMaintenance>();
    builder.Services.AddHostedService<RadiusHostedService>();
    builder.Services.AddHostedService<DisconnectWorker>();
    builder.Services.AddHostedService<MaintenanceWorker>();

    builder.Build().Run();
}
catch (Exception ex) when (ex is not OperationCanceledException)
{
    Log.Fatal(ex, "RADIUS server terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
