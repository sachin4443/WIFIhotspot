using NxtHotspot.RadiusServer;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.File("logs/radius-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    var builder = Host.CreateApplicationBuilder(args);

    // Runs as a Windows Service when installed; runs as a console app otherwise.
    builder.Services.AddWindowsService(o => o.ServiceName = "NxtHotspotRadius");
    builder.Services.AddSerilog();
    builder.Services.AddHostedService<RadiusHostedService>();

    builder.Build().Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "RADIUS server terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
