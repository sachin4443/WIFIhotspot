using System.Net;
using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NxtHotspot.Domain.Enums;
using NxtHotspot.Infrastructure.Persistence;
using NxtHotspot.RadiusProtocol;

namespace NxtHotspot.Radius.Application;

/// <summary>Executes queued DisconnectCommands by sending RFC 5176 Disconnect-Requests to the NAS and recording ACK/NAK/timeout.</summary>
public sealed class DisconnectSender
{
    public static TimeSpan ReplyTimeout { get; set; } = TimeSpan.FromSeconds(3);
    public const int MaxAttempts = 3;

    private readonly NxtDbContext _db;
    private readonly NasDirectory _nas;
    private readonly ILogger<DisconnectSender> _logger;

    public DisconnectSender(NxtDbContext db, NasDirectory nas, ILogger<DisconnectSender> logger) { _db = db; _nas = nas; _logger = logger; }

    /// <summary>Processes up to <paramref name="batch"/> pending commands. Returns how many were handled.</summary>
    public async Task<int> ProcessPendingAsync(int batch, CancellationToken ct)
    {
        var commands = await _db.DisconnectCommands.Where(c => c.Status == DisconnectStatus.Pending).OrderBy(c => c.CreatedAt).Take(batch).ToListAsync(ct);
        foreach (var cmd in commands)
        {
            var session = await _db.RadiusSessions.FirstOrDefaultAsync(s => s.Id == cmd.SessionId, ct);
            var nasRow = await _db.NasDevices.AsNoTracking().FirstOrDefaultAsync(n => n.Id == cmd.NasId, ct);
            cmd.Attempts++;
            if (session == null || nasRow == null) { Complete(cmd, DisconnectStatus.Failed, "Session or NAS no longer exists"); continue; }
            if (session.Status != RadiusSessionStatus.Active) { Complete(cmd, DisconnectStatus.Acknowledged, "Session had already ended"); continue; }
            var nas = await _nas.FindAsync(_db, IPAddress.Parse(nasRow.IpAddress), ct);
            if (nas == null || nas.Secret.Length == 0) { Complete(cmd, DisconnectStatus.Failed, "NAS has no usable shared secret"); continue; }

            var dm = new RadiusPacket(RadiusCode.DisconnectRequest, (byte)Random.Shared.Next(256))
                .Add(RadiusAttribute.String(RadiusAttributeType.AcctSessionId, session.AcctSessionId))
                .Add(RadiusAttribute.String(RadiusAttributeType.UserName, session.Username));
            if (session.FramedIpAddress != null && IPAddress.TryParse(session.FramedIpAddress, out var fip) && fip.AddressFamily == AddressFamily.InterNetwork)
                dm.Add(RadiusAttribute.Address(RadiusAttributeType.FramedIpAddress, fip));
            if (session.CallingStationId != null) dm.Add(RadiusAttribute.String(RadiusAttributeType.CallingStationId, session.CallingStationId));
            var bytes = RadiusCrypto.EncodeZeroBasedRequest(dm, nas.Secret, addMessageAuthenticator: true);
            var target = new IPEndPoint(IPAddress.Parse(nas.IpAddress), nas.CoaPort);

            try
            {
                var reply = await SendAsync(bytes, target, dm, ct);
                if (reply == null) { Complete(cmd, DisconnectStatus.TimedOut, $"No reply from {target} after {MaxAttempts} attempts"); continue; }
                if (!RadiusCrypto.VerifyResponse(reply, dm.Authenticator, nas.Secret)) { Complete(cmd, DisconnectStatus.Failed, "Reply authenticator invalid (shared secret mismatch?)"); continue; }
                if (reply.Code == RadiusCode.DisconnectAck)
                {
                    session.Status = RadiusSessionStatus.Stopped;
                    session.StoppedAt = DateTime.UtcNow;
                    session.TerminateCause = "Admin-Reset";
                    Complete(cmd, DisconnectStatus.Acknowledged, "Disconnect-ACK received");
                }
                else
                {
                    var cause = reply.GetInteger(RadiusAttributeType.ErrorCause);
                    Complete(cmd, DisconnectStatus.Rejected, $"Disconnect-NAK{(cause != null ? $" (Error-Cause {cause})" : "")}");
                }
            }
            catch (Exception ex) when (ex is SocketException or RadiusPacketException)
            {
                _logger.LogWarning(ex, "Disconnect to {Target} failed", target);
                Complete(cmd, DisconnectStatus.Failed, ex.Message);
            }
            _db.RadiusRequestLogs.Add(new Domain.Entities.RadiusRequestLog
            {
                OrganizationId = cmd.OrganizationId, SiteId = cmd.SiteId, NasId = cmd.NasId, SourceIp = nas.IpAddress, PacketKind = RadiusPacketKind.DisconnectRequest,
                Result = cmd.Status switch { DisconnectStatus.Acknowledged => RadiusResult.Ack, DisconnectStatus.Rejected => RadiusResult.Nak, _ => RadiusResult.Timeout },
                Username = session.Username, MacAddress = session.CallingStationId, Reason = cmd.Result,
            });
        }
        if (commands.Count > 0) await _db.SaveChangesAsync(ct);
        return commands.Count;
    }

    private static void Complete(Domain.Entities.DisconnectCommand cmd, DisconnectStatus status, string result)
    {
        cmd.Status = status;
        cmd.Result = result;
        cmd.CompletedAt = DateTime.UtcNow;
    }

    private static async Task<RadiusPacket?> SendAsync(byte[] bytes, IPEndPoint target, RadiusPacket request, CancellationToken ct)
    {
        using var udp = new UdpClient(target.AddressFamily);
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            await udp.SendAsync(bytes, target, ct);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(ReplyTimeout);
            try
            {
                while (true)
                {
                    var result = await udp.ReceiveAsync(timeout.Token);
                    if (!result.RemoteEndPoint.Address.Equals(target.Address)) continue;
                    var reply = RadiusPacket.Parse(result.Buffer);
                    if (reply.Identifier == request.Identifier && reply.Code is RadiusCode.DisconnectAck or RadiusCode.DisconnectNak) return reply;
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { /* retry */ }
        }
        return null;
    }
}
