namespace NxtHotspot.RadiusServer;

/// <summary>
/// Placeholder host. UDP listeners (1812/1813/3799) are added in Phase 5.
/// Intentionally does NOT open any sockets yet, so the service cannot be mistaken for a working RADIUS server.
/// </summary>
public sealed class RadiusHostedService : BackgroundService
{
    private readonly ILogger<RadiusHostedService> _logger;

    public RadiusHostedService(ILogger<RadiusHostedService> logger) => _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogWarning("NxtHotspot RADIUS host started (Phase 1 scaffold: no listeners yet).");
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("NxtHotspot RADIUS host stopping.");
        }
    }
}
