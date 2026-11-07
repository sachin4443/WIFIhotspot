using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using Microsoft.Extensions.Options;
using NxtHotspot.Radius.Application;

namespace NxtHotspot.RadiusServer;

public sealed class RadiusServerOptions
{
    public string ListenAddress { get; set; } = "0.0.0.0";
    public int AuthPort { get; set; } = 1812;
    public int AcctPort { get; set; } = 1813;
    /// <summary>0 = one worker per CPU core.</summary>
    public int Workers { get; set; }
    public int StaleSessionMinutes { get; set; } = 30;
    /// <summary>Name reported in the heartbeat; defaults to the machine name.</summary>
    public string InstanceId { get; set; } = "";
}

/// <summary>
/// UDP listeners for authentication and accounting. Receive loops only enqueue datagrams into a bounded channel;
/// worker tasks process them with their own DI scope, so a slow database call never blocks a socket.
/// </summary>
public sealed class RadiusHostedService : BackgroundService
{
    private sealed record Work(bool Accounting, UdpClient Socket, byte[] Data, IPEndPoint Remote);

    private readonly IServiceScopeFactory _scopes;
    private readonly RadiusServerOptions _options;
    private readonly RadiusCounters _counters;
    private readonly ILogger<RadiusHostedService> _logger;
    private readonly Channel<Work> _queue = Channel.CreateBounded<Work>(new BoundedChannelOptions(5000) { FullMode = BoundedChannelFullMode.DropWrite });

    public RadiusHostedService(IServiceScopeFactory scopes, IOptions<RadiusServerOptions> options, RadiusCounters counters, ILogger<RadiusHostedService> logger)
    {
        _scopes = scopes; _options = options.Value; _counters = counters; _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var address = IPAddress.Parse(_options.ListenAddress);
        using var auth = new UdpClient(new IPEndPoint(address, _options.AuthPort));
        using var acct = new UdpClient(new IPEndPoint(address, _options.AcctPort));
        _logger.LogInformation("NXT RADIUS server listening: authentication {Address}:{Auth}/udp, accounting {Address}:{Acct}/udp",
            address, _options.AuthPort, address, _options.AcctPort);

        var workers = _options.Workers > 0 ? _options.Workers : Math.Max(2, Environment.ProcessorCount);
        var tasks = new List<Task> { ReceiveAsync(auth, false, stoppingToken), ReceiveAsync(acct, true, stoppingToken) };
        tasks.AddRange(Enumerable.Range(0, workers).Select(_ => WorkAsync(stoppingToken)));
        await Task.WhenAll(tasks);
    }

    private async Task ReceiveAsync(UdpClient socket, bool accounting, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var r = await socket.ReceiveAsync(ct);
                if (!_queue.Writer.TryWrite(new Work(accounting, socket, r.Buffer, r.RemoteEndPoint))) Interlocked.Increment(ref _counters.Dropped);
            }
            catch (OperationCanceledException) { break; }
            catch (SocketException ex) { _logger.LogWarning(ex, "UDP receive error"); }
        }
    }

    private async Task WorkAsync(CancellationToken ct)
    {
        await foreach (var w in _queue.Reader.ReadAllAsync(ct))
        {
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                var response = w.Accounting
                    ? await scope.ServiceProvider.GetRequiredService<RadiusAccountingService>().HandleAsync(w.Data, w.Remote, ct)
                    : await scope.ServiceProvider.GetRequiredService<RadiusAuthService>().HandleAsync(w.Data, w.Remote, ct);
                if (response != null) await w.Socket.SendAsync(response, w.Remote, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                // No response: the NAS retries or fails over. Never crash the worker.
                Interlocked.Increment(ref _counters.Dropped);
                _logger.LogError(ex, "Error handling RADIUS packet from {Remote}", w.Remote);
            }
        }
    }
}

/// <summary>Sends queued Disconnect-Requests (RFC 5176) every two seconds.</summary>
public sealed class DisconnectWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<DisconnectWorker> _logger;
    public DisconnectWorker(IServiceScopeFactory scopes, ILogger<DisconnectWorker> logger) { _scopes = scopes; _logger = logger; }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<DisconnectSender>().ProcessPendingAsync(20, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Disconnect worker error"); }
            try { await Task.Delay(TimeSpan.FromSeconds(2), ct); } catch (OperationCanceledException) { break; }
        }
    }
}

/// <summary>Heartbeat every 15 s (read by the API's RADIUS monitor) and stale-session sweep every minute.</summary>
public sealed class MaintenanceWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly RadiusServerOptions _options;
    private readonly RadiusCounters _counters;
    private readonly ILogger<MaintenanceWorker> _logger;
    private readonly DateTime _startedAt = DateTime.UtcNow;

    public MaintenanceWorker(IServiceScopeFactory scopes, IOptions<RadiusServerOptions> options, RadiusCounters counters, ILogger<MaintenanceWorker> logger)
    {
        _scopes = scopes; _options = options.Value; _counters = counters; _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var instance = string.IsNullOrWhiteSpace(_options.InstanceId) ? Environment.MachineName : _options.InstanceId;
        var version = typeof(MaintenanceWorker).Assembly.GetName().Version?.ToString() ?? "1.0";
        var tick = 0;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                var m = scope.ServiceProvider.GetRequiredService<RadiusMaintenance>();
                await m.HeartbeatAsync(instance, version, _options.AuthPort, _options.AcctPort, _startedAt, _counters, ct);
                if (tick++ % 4 == 0)
                {
                    var stale = await m.MarkStaleSessionsAsync(TimeSpan.FromMinutes(_options.StaleSessionMinutes), ct);
                    if (stale > 0) _logger.LogInformation("Marked {Count} sessions stale (no accounting for {Minutes} min)", stale, _options.StaleSessionMinutes);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Maintenance worker error"); }
            try { await Task.Delay(TimeSpan.FromSeconds(15), ct); } catch (OperationCanceledException) { break; }
        }
    }
}
