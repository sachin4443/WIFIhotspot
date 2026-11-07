using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NxtHotspot.Application.Dtos;
using NxtHotspot.Domain.Entities;
using NxtHotspot.Domain.Enums;
using NxtHotspot.Infrastructure.Hotspot;
using NxtHotspot.Infrastructure.Persistence;
using NxtHotspot.Infrastructure.Services;
using NxtHotspot.Radius.Application;
using NxtHotspot.RadiusProtocol;

namespace NxtHotspot.Tests;

public class RadiusHandlerTests
{
    private const string Secret = "nas-shared-secret-1";
    private static readonly byte[] SecretBytes = Encoding.UTF8.GetBytes(Secret);
    private static readonly IPEndPoint NasEndpoint = new(IPAddress.Parse("10.9.8.7"), 50000);

    private sealed class Fixture
    {
        public TestEnv Env = null!;
        public OrganizationDetailDto Org = null!;
        public Guid NasId;
        public Guid PackageId;
        public NasDirectory Directory = null!;
        public RadiusCounters Counters = new();

        public RadiusAuthService Auth(NxtDbContext db) => new(db, Directory, Env.Passwords, Counters, NullLogger<RadiusAuthService>.Instance);
        public RadiusAccountingService Acct(NxtDbContext db) => new(db, Directory, Counters, NullLogger<RadiusAccountingService>.Instance);
    }

    private static async Task<Fixture> SetupAsync()
    {
        var f = new Fixture { Env = await TestEnv.CreateAsync() };
        f.Org = await f.Env.RegisterAsync("ISP", "Radius ISP", "radiusadmin");
        f.Directory = new NasDirectory(f.Env.Protector);
        using var db = f.Env.NewDb();
        var admin = FakeCurrentUser.OrgAdmin(f.Org.Id);
        var nas = await new NasService(db, admin, new TenantAccess(db, admin), f.Env.Protector, Options.Create(new RadiusOptions()))
            .CreateAsync(f.Org.Id, new UpsertNasRequest { SiteId = f.Org.PrimarySiteId, Name = "Core", IpAddress = "10.9.8.7", SharedSecret = Secret });
        f.NasId = nas.Id;
        f.PackageId = (await new PackageService(db, admin, new TenantAccess(db, admin)).CreateAsync(f.Org.Id, new UpsertPackageRequest
        {
            Name = "Home 20", DownloadKbps = 20480, UploadKbps = 10240, DataLimitMb = 1, SessionTimeoutMinutes = 60, IdleTimeoutMinutes = 10, MaxDevices = 1
        })).Id;
        var subs = new SubscriberService(db, admin, new TenantAccess(db, admin), f.Env.Passwords, new DisconnectQueue(db, admin));
        await subs.CreateAsync(f.Org.Id, new CreateSubscriberRequest { SubscriberType = SubscriberType.Customer, Identifier = "alice", FullName = "Alice", Password = "alice-pass", PackageId = f.PackageId });
        return f;
    }

    private static byte[] AccessRequest(string user, string password, string? mac = "AA-BB-CC-00-11-22", byte id = 1)
    {
        var p = new RadiusPacket(RadiusCode.AccessRequest, id, RadiusCrypto.NewRequestAuthenticator())
            .Add(RadiusAttribute.String(RadiusAttributeType.UserName, user));
        p.Add(new RadiusAttribute(RadiusAttributeType.UserPassword, RadiusCrypto.EncryptPap(password, SecretBytes, p.Authenticator)));
        if (mac != null) p.Add(RadiusAttribute.String(RadiusAttributeType.CallingStationId, mac));
        return p.Encode();
    }

    private static byte[] Accounting(int status, string sessionId, string user, uint inOctets = 0, uint outOctets = 0, uint gigaIn = 0, uint time = 0, string? cls = null, uint? cause = null)
    {
        var p = new RadiusPacket(RadiusCode.AccountingRequest, (byte)status)
            .Add(RadiusAttribute.Integer(RadiusAttributeType.AcctStatusType, (uint)status))
            .Add(RadiusAttribute.String(RadiusAttributeType.AcctSessionId, sessionId))
            .Add(RadiusAttribute.String(RadiusAttributeType.UserName, user))
            .Add(RadiusAttribute.String(RadiusAttributeType.CallingStationId, "AA:BB:CC:00:11:22"))
            .Add(RadiusAttribute.Address(RadiusAttributeType.FramedIpAddress, IPAddress.Parse("172.16.0.50")))
            .Add(RadiusAttribute.Integer(RadiusAttributeType.AcctSessionTime, time))
            .Add(RadiusAttribute.Integer(RadiusAttributeType.AcctInputOctets, inOctets))
            .Add(RadiusAttribute.Integer(RadiusAttributeType.AcctOutputOctets, outOctets))
            .Add(RadiusAttribute.Integer(RadiusAttributeType.AcctInputGigawords, gigaIn));
        if (cls != null) p.Add(RadiusAttribute.String(RadiusAttributeType.Class, cls));
        if (cause != null) p.Add(RadiusAttribute.Integer(RadiusAttributeType.AcctTerminateCause, cause.Value));
        return RadiusCrypto.EncodeZeroBasedRequest(p, SecretBytes, addMessageAuthenticator: false);
    }

    private static RadiusPacket Reply(byte[]? bytes, byte[] request)
    {
        Assert.NotNull(bytes);
        var reply = RadiusPacket.Parse(bytes!);
        Assert.True(RadiusCrypto.VerifyResponse(reply, RadiusPacket.Parse(request).Authenticator, SecretBytes), "response authenticator");
        Assert.True(RadiusCrypto.VerifyMessageAuthenticator(reply.Code == RadiusCode.AccountingResponse ? new RadiusPacket() : CloneWithRequestAuth(reply, request), SecretBytes) != false);
        return reply;
    }

    private static RadiusPacket CloneWithRequestAuth(RadiusPacket reply, byte[] request)
    {
        // Message-Authenticator in a reply is computed with the Request Authenticator in the header.
        var c = RadiusPacket.Parse(reply.Encode());
        c.Authenticator = RadiusPacket.Parse(request).Authenticator;
        return c;
    }

    [Fact]
    public async Task Accepts_valid_pap_login_with_package_policy_attributes()
    {
        var f = await SetupAsync();
        using var db = f.Env.NewDb();
        var req = AccessRequest("ALICE", "alice-pass");
        var reply = Reply(await f.Auth(db).HandleAsync(req, NasEndpoint, default), req);
        Assert.Equal(RadiusCode.AccessAccept, reply.Code);
        Assert.Equal(3600u, reply.GetInteger(RadiusAttributeType.SessionTimeout));
        Assert.Equal(600u, reply.GetInteger(RadiusAttributeType.IdleTimeout));
        var vsa = reply.Attributes.Select(a => a.AsVendor()).Where(v => v != null).Select(v => v!.Value).ToList();
        Assert.Contains(vsa, v => v.VendorId == 14988 && v.VendorType == MikrotikAttribute.RateLimit && Encoding.UTF8.GetString(v.Value) == "10240k/20480k");
        Assert.Contains(vsa, v => v.VendorType == MikrotikAttribute.TotalLimit && System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(v.Value) == 1024 * 1024);
        Assert.StartsWith("nxt:", reply.GetString(RadiusAttributeType.Class));
        Assert.True(reply.Has(RadiusAttributeType.MessageAuthenticator));
        var log = await db.RadiusRequestLogs.SingleAsync();
        Assert.Equal(RadiusResult.Accept, log.Result);
        Assert.DoesNotContain("alice-pass", log.Reason ?? "");
        Assert.NotNull((await db.NasDevices.SingleAsync()).LastSeenAt);
    }

    [Fact]
    public async Task Rejects_bad_password_with_generic_message_and_drops_unknown_nas_and_bad_secret()
    {
        var f = await SetupAsync();
        using var db = f.Env.NewDb();
        var req = AccessRequest("alice", "wrong");
        var reply = Reply(await f.Auth(db).HandleAsync(req, NasEndpoint, default), req);
        Assert.Equal(RadiusCode.AccessReject, reply.Code);
        Assert.Equal("Invalid username or password", reply.GetString(RadiusAttributeType.ReplyMessage));
        Assert.Equal("Wrong password", (await db.RadiusRequestLogs.SingleAsync()).Reason);

        var unknownUser = AccessRequest("mallory", "x");
        Assert.Equal("Invalid username or password", Reply(await f.Auth(db).HandleAsync(unknownUser, NasEndpoint, default), unknownUser).GetString(RadiusAttributeType.ReplyMessage));

        Assert.Null(await f.Auth(db).HandleAsync(AccessRequest("alice", "alice-pass"), new IPEndPoint(IPAddress.Parse("10.0.0.99"), 1), default));
        Assert.Null(await f.Auth(db).HandleAsync(new byte[] { 1, 2, 3 }, NasEndpoint, default));

        // A request signed with a different secret decrypts to garbage -> reject (not accept).
        var p = new RadiusPacket(RadiusCode.AccessRequest, 5, RadiusCrypto.NewRequestAuthenticator()).Add(RadiusAttribute.String(RadiusAttributeType.UserName, "alice"));
        p.Add(new RadiusAttribute(RadiusAttributeType.UserPassword, RadiusCrypto.EncryptPap("alice-pass", Encoding.UTF8.GetBytes("other-secret"), p.Authenticator)));
        var resp = RadiusPacket.Parse((await f.Auth(db).HandleAsync(p.Encode(), NasEndpoint, default))!);
        Assert.Equal(RadiusCode.AccessReject, resp.Code);
        Assert.Equal(2, f.Counters.Dropped);
    }

    [Fact]
    public async Task Rejects_suspended_users_and_enforces_device_limit_and_data_quota()
    {
        var f = await SetupAsync();
        using var db = f.Env.NewDb();
        var alice = await db.Subscribers.SingleAsync(s => s.Identifier == "alice");

        db.RadiusSessions.Add(new RadiusSession { OrganizationId = f.Org.Id, SiteId = f.Org.PrimarySiteId, NasId = f.NasId, SubscriberId = alice.Id, Username = "alice",
            AcctSessionId = "s-1", StartedAt = DateTime.UtcNow, LastUpdateAt = DateTime.UtcNow, CallingStationId = "AA:BB:CC:00:11:22" });
        await db.SaveChangesAsync();
        var other = AccessRequest("alice", "alice-pass", mac: "11-22-33-44-55-66");
        Assert.Equal("Device limit reached (1)", Reply(await f.Auth(db).HandleAsync(other, NasEndpoint, default), other).GetString(RadiusAttributeType.ReplyMessage));
        var same = AccessRequest("alice", "alice-pass");
        Assert.Equal(RadiusCode.AccessAccept, Reply(await f.Auth(db).HandleAsync(same, NasEndpoint, default), same).Code); // same device reconnecting

        var s1 = await db.RadiusSessions.SingleAsync();
        s1.Status = RadiusSessionStatus.Stopped;
        s1.InputBytes = 2 * 1024 * 1024; // over the 1 MB quota
        await db.SaveChangesAsync();
        var quota = AccessRequest("alice", "alice-pass");
        Assert.Equal("Data quota exhausted", Reply(await f.Auth(db).HandleAsync(quota, NasEndpoint, default), quota).GetString(RadiusAttributeType.ReplyMessage));

        alice.Status = SubscriberStatus.Suspended;
        await db.SaveChangesAsync();
        var susp = AccessRequest("alice", "alice-pass");
        Assert.Equal("Account is suspended", Reply(await f.Auth(db).HandleAsync(susp, NasEndpoint, default), susp).GetString(RadiusAttributeType.ReplyMessage));
    }

    [Fact]
    public async Task One_time_portal_credential_is_single_use_and_mac_bound()
    {
        var f = await SetupAsync();
        using var db = f.Env.NewDb();
        var alice = await db.Subscribers.SingleAsync(s => s.Identifier == "alice");
        db.RadiusCredentials.Add(new RadiusCredential
        {
            OrganizationId = f.Org.Id, SiteId = f.Org.PrimarySiteId, SubscriberId = alice.Id, Username = "nxt-abc123",
            PasswordHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("otp-cred-pass"))), MacAddress = "AA:BB:CC:00:11:22",
            ExpiresAt = DateTime.UtcNow.AddMinutes(2),
        });
        await db.SaveChangesAsync();

        var wrongDevice = AccessRequest("nxt-abc123", "otp-cred-pass", mac: "DE:AD:BE:EF:00:01");
        Assert.Equal(RadiusCode.AccessReject, Reply(await f.Auth(db).HandleAsync(wrongDevice, NasEndpoint, default), wrongDevice).Code);
        var ok = AccessRequest("nxt-abc123", "otp-cred-pass");
        Assert.Equal(RadiusCode.AccessAccept, Reply(await f.Auth(db).HandleAsync(ok, NasEndpoint, default), ok).Code);
        var replay = AccessRequest("nxt-abc123", "otp-cred-pass");
        Assert.Equal("One-time credential already used", Reply(await f.Auth(db).HandleAsync(replay, NasEndpoint, default), replay).GetString(RadiusAttributeType.ReplyMessage));
        Assert.NotNull((await db.Subscribers.SingleAsync(s => s.Id == alice.Id)).FirstLoginAt);
    }

    [Fact]
    public async Task Accounting_start_interim_stop_is_idempotent_and_combines_gigawords()
    {
        var f = await SetupAsync();
        using var db = f.Env.NewDb();
        var alice = await db.Subscribers.SingleAsync(s => s.Identifier == "alice");
        var acct = f.Acct(db);
        var cls = $"nxt:{alice.Id:N}";

        var start = Accounting(AcctStatusType.Start, "8100001", "alice", cls: cls);
        Assert.Equal(RadiusCode.AccountingResponse, Reply(await acct.HandleAsync(start, NasEndpoint, default), start).Code);
        Assert.NotNull(await acct.HandleAsync(start, NasEndpoint, default)); // retransmission acknowledged
        Assert.Equal(1, await db.RadiusAccountingEvents.CountAsync());

        var interim = Accounting(AcctStatusType.InterimUpdate, "8100001", "alice", inOctets: 500, outOctets: 7000, gigaIn: 1, time: 300);
        await acct.HandleAsync(interim, NasEndpoint, default);
        var session = await db.RadiusSessions.AsNoTracking().SingleAsync();
        Assert.Equal(alice.Id, session.SubscriberId);
        Assert.Equal((1L << 32) + 500, session.InputBytes);
        Assert.Equal(7000, session.OutputBytes);
        Assert.Equal(RadiusSessionStatus.Active, session.Status);
        Assert.Equal("172.16.0.50", session.FramedIpAddress);

        var stop = Accounting(AcctStatusType.Stop, "8100001", "alice", inOctets: 900, outOctets: 9000, gigaIn: 1, time: 600, cause: 1);
        await acct.HandleAsync(stop, NasEndpoint, default);
        session = await db.RadiusSessions.AsNoTracking().SingleAsync();
        Assert.Equal(RadiusSessionStatus.Stopped, session.Status);
        Assert.Equal("User-Request", session.TerminateCause);
        Assert.Equal(600, session.SessionTimeSeconds);

        // Wrong authenticator -> silently dropped.
        var bad = Accounting(AcctStatusType.InterimUpdate, "8100001", "alice");
        bad[5] ^= 0x01;
        Assert.Null(await acct.HandleAsync(bad, NasEndpoint, default));
    }

    [Fact]
    public async Task Accounting_on_closes_open_sessions_and_stale_sweep_marks_silent_ones()
    {
        var f = await SetupAsync();
        using var db = f.Env.NewDb();
        await f.Acct(db).HandleAsync(Accounting(AcctStatusType.Start, "a1", "alice"), NasEndpoint, default);
        await f.Acct(db).HandleAsync(Accounting(AcctStatusType.Start, "a2", "alice"), NasEndpoint, default);
        var on = new RadiusPacket(RadiusCode.AccountingRequest, 9).Add(RadiusAttribute.Integer(RadiusAttributeType.AcctStatusType, AcctStatusType.AccountingOn));
        await f.Acct(db).HandleAsync(RadiusCrypto.EncodeZeroBasedRequest(on, SecretBytes, false), NasEndpoint, default);
        Assert.All(await db.RadiusSessions.ToListAsync(), s => Assert.Equal(RadiusSessionStatus.Stopped, s.Status));

        await f.Acct(db).HandleAsync(Accounting(AcctStatusType.Start, "a3", "alice"), NasEndpoint, default);
        var s3 = await db.RadiusSessions.SingleAsync(s => s.AcctSessionId == "a3");
        s3.LastUpdateAt = DateTime.UtcNow.AddHours(-2);
        await db.SaveChangesAsync();
        Assert.Equal(1, await new RadiusMaintenance(db).MarkStaleSessionsAsync(TimeSpan.FromMinutes(30), default));
        Assert.Equal(RadiusSessionStatus.Stale, (await db.RadiusSessions.SingleAsync(s => s.AcctSessionId == "a3")).Status);
    }

    [Fact]
    public async Task Session_api_is_tenant_isolated_and_disconnect_is_queued_once()
    {
        var f = await SetupAsync();
        var other = await f.Env.RegisterAsync("Hotel", "Other Hotel", "otherhotel");
        using var db = f.Env.NewDb();
        await f.Acct(db).HandleAsync(Accounting(AcctStatusType.Start, "z1", "alice"), NasEndpoint, default);
        var sessionId = (await db.RadiusSessions.SingleAsync()).Id;

        var mine = FakeCurrentUser.OrgAdmin(f.Org.Id);
        var svc = new SessionService(db, mine, new TenantAccess(db, mine));
        Assert.Equal(1, (await svc.ListAsync(new SessionQuery { Status = RadiusSessionStatus.Active })).TotalCount);
        var theirs = FakeCurrentUser.OrgAdmin(other.Id);
        var otherSvc = new SessionService(db, theirs, new TenantAccess(db, theirs));
        Assert.Equal(0, (await otherSvc.ListAsync(new SessionQuery())).TotalCount);
        await Assert.ThrowsAsync<NxtHotspot.Application.Common.NotFoundException>(() => otherSvc.DisconnectAsync(sessionId));
        await Assert.ThrowsAsync<NxtHotspot.Application.Common.ForbiddenException>(() => otherSvc.ListAsync(new SessionQuery { OrganizationId = f.Org.Id }));

        var cmd = await svc.DisconnectAsync(sessionId);
        var again = await svc.DisconnectAsync(sessionId);
        Assert.Equal(cmd.Id, again.Id);
        Assert.Equal(DisconnectStatus.Pending, cmd.Status);

        var report = await new UsageReportService(db, mine, new TenantAccess(db, mine)).GetAsync(null, null, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddMinutes(1));
        Assert.Equal(1, report.Sessions);
        Assert.Equal(1, report.UniqueUsers);
    }
}
