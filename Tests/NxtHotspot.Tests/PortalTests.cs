using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NxtHotspot.Application.Common;
using NxtHotspot.Application.Dtos;
using NxtHotspot.Domain.Enums;
using NxtHotspot.Infrastructure.Hotspot;
using NxtHotspot.Infrastructure.Persistence;
using NxtHotspot.Infrastructure.Security;
using NxtHotspot.Infrastructure.Services;
using NxtHotspot.Infrastructure.Sms;
using NxtHotspot.Radius.Application;
using NxtHotspot.RadiusProtocol;

namespace NxtHotspot.Tests;

/// <summary>Test double: records the code instead of calling an SMS provider.</summary>
public sealed class CapturingSmsSender : ISmsSender
{
    public string? LastCode;
    public string? LastMobile;
    public bool Fail;
    public Task<SmsSendResult> SendOtpAsync(Guid organizationId, Guid siteId, string mobile, string code, int expiryMinutes, CancellationToken ct)
    {
        LastCode = code;
        LastMobile = mobile;
        return Task.FromResult(new SmsSendResult(!Fail, Fail ? "down" : null, false));
    }
}

public class PortalTests
{
    private const string Secret = "portal-nas-secret-1";

    private sealed class Fx
    {
        public TestEnv Env = null!;
        public OrganizationDetailDto Org = null!;
        public string Key = "";
        public Guid AnnexId;
        public CapturingSmsSender Sms = new();
        public PortalService Portal(NxtDbContext db) => new(db, Env.Passwords, Sms, Options.Create(new SmsOptions()), Options.Create(new RadiusOptions { PortalCredentialSeconds = 120 }),
            Options.Create(new SecurityOptions { EncryptionKey = "test-key" }));
        public SubscriberService Subs(NxtDbContext db) { var u = FakeCurrentUser.OrgAdmin(Org.Id); return new(db, u, new TenantAccess(db, u), Env.Passwords, new DisconnectQueue(db, u)); }
    }

    private static async Task<Fx> SetupAsync(string type, List<AuthenticationType> methods)
    {
        var f = new Fx { Env = await TestEnv.CreateAsync() };
        f.Org = await f.Env.RegisterAsync(type, $"{type} Portal", $"{type.ToLower().Replace(" ", "")}portal", methods: methods);
        using var db = f.Env.NewDb();
        var admin = FakeCurrentUser.OrgAdmin(f.Org.Id);
        f.AnnexId = (await f.Env.Sites(db, admin).CreateAsync(f.Org.Id, new CreateSiteRequest { Name = "Annex", Code = "ANX" })).Id;
        var nasSvc = new NasService(db, admin, new TenantAccess(db, admin), f.Env.Protector, Options.Create(new RadiusOptions()));
        var nas = await nasSvc.CreateAsync(f.Org.Id, new UpsertNasRequest { SiteId = f.Org.PrimarySiteId, Name = "Lobby", IpAddress = "10.50.0.1", SharedSecret = Secret });
        f.Key = (await nasSvc.RegeneratePortalKeyAsync(f.Org.Id, nas.Id)).PortalKey;
        return f;
    }

    private static async Task<RadiusCode> RouterLoginAsync(Fx f, NxtDbContext db, string username, string password, string mac)
    {
        var secret = Encoding.UTF8.GetBytes(Secret);
        var p = new RadiusPacket(RadiusCode.AccessRequest, 1, RadiusCrypto.NewRequestAuthenticator()).Add(RadiusAttribute.String(RadiusAttributeType.UserName, username));
        p.Add(new RadiusAttribute(RadiusAttributeType.UserPassword, RadiusCrypto.EncryptPap(password, secret, p.Authenticator)));
        p.Add(RadiusAttribute.String(RadiusAttributeType.CallingStationId, mac));
        var auth = new RadiusAuthService(db, new NasDirectory(f.Env.Protector), f.Env.Passwords, new RadiusCounters(), NullLogger<RadiusAuthService>.Instance);
        return RadiusPacket.Parse((await auth.HandleAsync(p.Encode(), new IPEndPoint(IPAddress.Parse("10.50.0.1"), 4000), default))!).Code;
    }

    [Fact]
    public async Task Config_exposes_enabled_methods_and_branding_and_rejects_bad_keys()
    {
        var f = await SetupAsync("Public WiFi", new() { AuthenticationType.MobileOTP });
        using var db = f.Env.NewDb();
        var cfg = await f.Portal(db).GetConfigAsync(f.Key);
        Assert.Equal("Public WiFi Portal", cfg.OrganizationName);
        Assert.Equal("Main Branch", cfg.SiteName);
        Assert.Equal(new[] { AuthenticationType.MobileOTP }, cfg.Methods.Select(m => m.AuthenticationType));
        await Assert.ThrowsAsync<NotFoundException>(() => f.Portal(db).GetConfigAsync(new string('0', 32)));
        await Assert.ThrowsAsync<NotFoundException>(() => f.Portal(db).GetConfigAsync("short"));
    }

    [Fact]
    public async Task Mobile_otp_flow_issues_single_use_credential_that_radius_accepts()
    {
        var f = await SetupAsync("Public WiFi", new() { AuthenticationType.MobileOTP });
        using var db = f.Env.NewDb();
        var portal = f.Portal(db);

        var otp = await portal.RequestOtpAsync(f.Key, new PortalOtpRequest { Method = AuthenticationType.MobileOTP, MobileNumber = "9876500011" }, "1.2.3.4");
        Assert.Equal("******0011", otp.MaskedMobile);
        Assert.Matches("^[0-9]{6}$", f.Sms.LastCode!);
        Assert.DoesNotContain(f.Sms.LastCode!, (await db.OtpChallenges.SingleAsync()).CodeHash);
        await Assert.ThrowsAsync<ConflictException>(() => portal.RequestOtpAsync(f.Key, new PortalOtpRequest { Method = AuthenticationType.MobileOTP, MobileNumber = "9876500011" }, "1.2.3.4"));

        var wrong = f.Sms.LastCode == "000000" ? "111111" : "000000";
        var ex = await Assert.ThrowsAsync<ValidationFailedException>(() => portal.LoginAsync(f.Key, new PortalLoginRequest { Method = AuthenticationType.MobileOTP, MobileNumber = "9876500011", ChallengeId = otp.ChallengeId, Otp = wrong, AcceptTerms = true }, null));
        Assert.Contains("attempts left", ex.Message);
        await Assert.ThrowsAsync<ValidationFailedException>(() => portal.LoginAsync(f.Key, new PortalLoginRequest { Method = AuthenticationType.MobileOTP, MobileNumber = "9876500011", ChallengeId = otp.ChallengeId, Otp = f.Sms.LastCode, AcceptTerms = false }, null));

        var ok = await portal.LoginAsync(f.Key, new PortalLoginRequest { Method = AuthenticationType.MobileOTP, MobileNumber = "9876500011", ChallengeId = otp.ChallengeId, Otp = f.Sms.LastCode, AcceptTerms = true, MacAddress = "aa-bb-cc-dd-ee-ff" }, null);
        Assert.Equal("Authorized", ok.Status);
        Assert.StartsWith("nxt-", ok.Username);
        var guest = await db.Subscribers.SingleAsync(s => s.Identifier == "9876500011");
        Assert.Equal(SubscriberType.Guest, guest.SubscriberType);

        // The OTP is single use.
        await Assert.ThrowsAsync<ValidationFailedException>(() => portal.LoginAsync(f.Key, new PortalLoginRequest { Method = AuthenticationType.MobileOTP, MobileNumber = "9876500011", ChallengeId = otp.ChallengeId, Otp = f.Sms.LastCode, AcceptTerms = true }, null));

        // The router presents the issued credential: RADIUS accepts it once, from the same device.
        Assert.Equal(RadiusCode.AccessReject, await RouterLoginAsync(f, db, ok.Username!, ok.Password!, "11:11:11:11:11:11"));
        Assert.Equal(RadiusCode.AccessAccept, await RouterLoginAsync(f, db, ok.Username!, ok.Password!, "AA:BB:CC:DD:EE:FF"));
        Assert.Equal(RadiusCode.AccessReject, await RouterLoginAsync(f, db, ok.Username!, ok.Password!, "AA:BB:CC:DD:EE:FF"));
    }

    [Fact]
    public async Task Sms_failure_does_not_leave_a_usable_challenge()
    {
        var f = await SetupAsync("Cafe", new() { AuthenticationType.MobileOTP });
        f.Sms.Fail = true;
        using var db = f.Env.NewDb();
        await Assert.ThrowsAsync<ServiceUnavailableException>(() => f.Portal(db).RequestOtpAsync(f.Key, new PortalOtpRequest { Method = AuthenticationType.MobileOTP, MobileNumber = "9876500099" }, null));
        Assert.Equal(0, await db.OtpChallenges.CountAsync());
    }

    [Fact]
    public async Task Hotel_room_login_is_scoped_to_the_site_and_the_stay()
    {
        var f = await SetupAsync("Hotel", new() { AuthenticationType.RoomMobile });
        using var db = f.Env.NewDb();
        var subs = f.Subs(db);
        var stay = DateTime.UtcNow.AddDays(1);
        var main = await subs.CreateAsync(f.Org.Id, new CreateSubscriberRequest { SubscriberType = SubscriberType.RoomGuest, FullName = "Main 305", SiteId = f.Org.PrimarySiteId, RoomNumber = "305", MobileNumber = "+919811100305", CheckOutAt = stay });
        await subs.CreateAsync(f.Org.Id, new CreateSubscriberRequest { SubscriberType = SubscriberType.RoomGuest, FullName = "Annex 306", SiteId = f.AnnexId, RoomNumber = "306", MobileNumber = "9811100306", CheckOutAt = stay });
        var portal = f.Portal(db);
        PortalLoginRequest Req(string room, string mobile) => new() { Method = AuthenticationType.RoomMobile, RoomNumber = room, MobileNumber = mobile, AcceptTerms = true };

        // Hotel default: RoomMobile requires booking validation but not OTP.
        var ok = await portal.LoginAsync(f.Key, Req("305", "9811100305"), null); // last 10 digits match +91…
        Assert.Equal("Authorized", ok.Status);
        await Assert.ThrowsAsync<ValidationFailedException>(() => portal.LoginAsync(f.Key, Req("305", "9811100999"), null)); // wrong mobile
        await Assert.ThrowsAsync<ValidationFailedException>(() => portal.LoginAsync(f.Key, Req("306", "9811100306"), null)); // guest of another property

        await subs.CheckOutAsync(f.Org.Id, main.Id);
        await Assert.ThrowsAsync<ValidationFailedException>(() => portal.LoginAsync(f.Key, Req("305", "9811100305"), null));
    }

    [Fact]
    public async Task Student_password_with_required_otp_is_two_step_and_suspension_blocks_access()
    {
        var f = await SetupAsync("College", new() { AuthenticationType.StudentPassword, AuthenticationType.StaffPassword });
        using var db = f.Env.NewDb();
        var admin = FakeCurrentUser.OrgAdmin(f.Org.Id);
        var config = new OrganizationConfigService(db, admin, new TenantAccess(db, admin), f.Env.Protector, Options.Create(new SmsOptions()));
        await config.UpsertLoginMethodAsync(f.Org.Id, null, new UpsertLoginMethodRequest { AuthenticationType = AuthenticationType.StudentPassword, IsEnabled = true, IsDefault = true, RequireOTP = true });
        var student = await f.Subs(db).CreateAsync(f.Org.Id, new CreateSubscriberRequest { SubscriberType = SubscriberType.Student, Identifier = "CSE-001", FullName = "Riya", Password = "riya-pass", MobileNumber = "9000011122" });
        await f.Subs(db).CreateAsync(f.Org.Id, new CreateSubscriberRequest { SubscriberType = SubscriberType.Teacher, Identifier = "T-01", FullName = "Prof", Password = "prof-pass" });
        var portal = f.Portal(db);

        await Assert.ThrowsAsync<UnauthorizedAppException>(() => portal.LoginAsync(f.Key, new PortalLoginRequest { Method = AuthenticationType.StudentPassword, Identifier = "cse-001", Password = "bad", AcceptTerms = true }, null));
        // A teacher cannot use the student method, and vice versa through the staff method.
        await Assert.ThrowsAsync<UnauthorizedAppException>(() => portal.LoginAsync(f.Key, new PortalLoginRequest { Method = AuthenticationType.StudentPassword, Identifier = "T-01", Password = "prof-pass", AcceptTerms = true }, null));
        Assert.Equal("Authorized", (await portal.LoginAsync(f.Key, new PortalLoginRequest { Method = AuthenticationType.StaffPassword, Identifier = "t-01", Password = "prof-pass", AcceptTerms = true }, null)).Status);

        var step1 = await portal.LoginAsync(f.Key, new PortalLoginRequest { Method = AuthenticationType.StudentPassword, Identifier = "cse-001", Password = "riya-pass", AcceptTerms = true }, null);
        Assert.Equal("OtpRequired", step1.Status);
        Assert.Null(step1.Password);
        Assert.Equal("9000011122", f.Sms.LastMobile);
        var step2 = await portal.LoginAsync(f.Key, new PortalLoginRequest { Method = AuthenticationType.StudentPassword, Identifier = "cse-001", Password = "riya-pass", ChallengeId = step1.ChallengeId, Otp = f.Sms.LastCode, AcceptTerms = true }, null);
        Assert.Equal("Authorized", step2.Status);

        await f.Subs(db).SetStatusAsync(f.Org.Id, student.Id, SubscriberStatus.Suspended);
        f.Sms.LastCode = null;
        var blocked = await Assert.ThrowsAsync<ForbiddenException>(() =>
            portal.LoginAsync(f.Key, new PortalLoginRequest { Method = AuthenticationType.StudentPassword, Identifier = "cse-001", Password = "riya-pass", AcceptTerms = true }, null));
        Assert.Contains("suspended", blocked.Message);
        Assert.Null(f.Sms.LastCode); // no SMS spent on a blocked account
    }

    [Fact]
    public async Task Disabled_methods_and_vouchers_are_refused()
    {
        var f = await SetupAsync("Enterprise", new() { AuthenticationType.EmployeePassword });
        using var db = f.Env.NewDb();
        await Assert.ThrowsAsync<ValidationFailedException>(() => f.Portal(db).RequestOtpAsync(f.Key, new PortalOtpRequest { Method = AuthenticationType.MobileOTP, MobileNumber = "9876500001" }, null));
        await Assert.ThrowsAsync<ValidationFailedException>(() => f.Portal(db).LoginAsync(f.Key, new PortalLoginRequest { Method = AuthenticationType.Voucher, AcceptTerms = true }, null));
    }
}
