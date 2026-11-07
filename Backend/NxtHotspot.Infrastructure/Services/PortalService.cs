using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NxtHotspot.Application.Common;
using NxtHotspot.Application.Dtos;
using NxtHotspot.Domain.Entities;
using NxtHotspot.Domain.Enums;
using NxtHotspot.Infrastructure.Hotspot;
using NxtHotspot.Infrastructure.Persistence;
using NxtHotspot.Infrastructure.Security;
using NxtHotspot.Infrastructure.Sms;

namespace NxtHotspot.Infrastructure.Services;

/// <summary>
/// Captive-portal sign-in for every venue workflow. A successful sign-in never opens the network by itself: it issues a
/// short-lived single-use RADIUS credential that the portal submits to the router, which then asks the NXT RADIUS server.
/// </summary>
public sealed class PortalService : IPortalService
{
    public const int MaxOtpPerMobilePerHour = 5;
    public const int MaxOtpPerIpPerHour = 20;

    private readonly NxtDbContext _db;
    private readonly IPasswordService _passwords;
    private readonly ISmsSender _sms;
    private readonly SmsOptions _smsOptions;
    private readonly RadiusOptions _radius;
    private readonly byte[] _otpKey;

    public PortalService(NxtDbContext db, IPasswordService passwords, ISmsSender sms, IOptions<SmsOptions> smsOptions, IOptions<RadiusOptions> radius, IOptions<SecurityOptions> security)
    {
        _db = db; _passwords = passwords; _sms = sms; _smsOptions = smsOptions.Value; _radius = radius.Value;
        // OTP codes are stored as HMAC-SHA256 with a key derived from the platform encryption key, so a database leak does not reveal them.
        _otpKey = SHA256.HashData(Encoding.UTF8.GetBytes("nxt-otp:" + security.Value.EncryptionKey));
    }

    private sealed record Portal(NasDevice Nas, Organization Org, Site Site, OrganizationSettings Settings);

    private async Task<Portal> ResolveAsync(string portalKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(portalKey) || portalKey.Length != 32) throw new NotFoundException("This Wi-Fi sign-in link is not valid.");
        var hash = NasService.HashKey(portalKey.ToLowerInvariant());
        var nas = await _db.NasDevices.AsNoTracking().FirstOrDefaultAsync(n => n.PortalKeyHash == hash && n.IsActive, ct)
                  ?? throw new NotFoundException("This Wi-Fi sign-in link is not valid.");
        var org = await _db.Organizations.AsNoTracking().Include(o => o.Settings).FirstAsync(o => o.Id == nas.OrganizationId, ct);
        var site = await _db.Sites.AsNoTracking().FirstAsync(s => s.Id == nas.SiteId, ct);
        if (org.Status != OrganizationStatus.Active || !site.IsActive) throw new ForbiddenException("Wi-Fi sign-in is currently unavailable at this location.");
        return new Portal(nas, org, site, org.Settings ?? new OrganizationSettings { OrganizationId = org.Id });
    }

    private async Task<List<OrganizationLoginMethod>> MethodsAsync(Portal p, CancellationToken ct)
    {
        var siteMethods = await _db.OrganizationLoginMethods.AsNoTracking().Where(m => m.OrganizationId == p.Org.Id && m.SiteId == p.Site.Id).ToListAsync(ct);
        var list = siteMethods.Count > 0 ? siteMethods
            : await _db.OrganizationLoginMethods.AsNoTracking().Where(m => m.OrganizationId == p.Org.Id && m.SiteId == null).ToListAsync(ct);
        return list.Where(m => m.IsEnabled).OrderByDescending(m => m.IsDefault).ThenBy(m => m.AuthenticationType).ToList();
    }

    public async Task<PortalConfigDto> GetConfigAsync(string portalKey, CancellationToken ct = default)
    {
        var p = await ResolveAsync(portalKey, ct);
        var branding = await _db.OrganizationBranding.AsNoTracking().FirstOrDefaultAsync(b => b.OrganizationId == p.Org.Id, ct);
        var type = await _db.OrganizationTypes.AsNoTracking().Where(t => t.OrganizationTypeId == p.Org.OrganizationTypeId).Select(t => t.TypeName).FirstAsync(ct);
        var methods = (await MethodsAsync(p, ct)).Select(m => new PortalMethodDto(m.AuthenticationType, m.IsDefault, m.RequireOTP)).ToList();
        return new PortalConfigDto(p.Org.Name, type, p.Site.Name, branding?.LogoUrl, branding?.ThemeColor ?? "#4f46e5", branding?.WelcomeMessage,
            branding?.BackgroundUrl, branding?.TermsAndConditions, branding?.PrivacyPolicy, methods, p.Settings.OtpExpirySeconds,
            p.Settings.OtpResendCooldownSeconds, _smsOptions.IsDevelopmentLog);
    }

    // ---------------- OTP ----------------

    public async Task<PortalOtpResponse> RequestOtpAsync(string portalKey, PortalOtpRequest r, string? clientIp, CancellationToken ct = default)
    {
        var p = await ResolveAsync(portalKey, ct);
        var method = await RequireMethodAsync(p, r.Method!.Value, ct);
        string mobile = r.Method switch
        {
            AuthenticationType.StudentOTP => (await FindByIdentifierAsync(p, r.Identifier, new[] { SubscriberType.Student }, ct))?.MobileNumber
                                             ?? throw new ValidationFailedException("We could not send a code. Check your student ID or contact the administrator."),
            AuthenticationType.RoomMobile => RequireMobile(r.MobileNumber),
            _ when method.RequireOTP || r.Method is AuthenticationType.MobileOTP or AuthenticationType.GuestOTP => RequireMobile(r.MobileNumber),
            _ => throw new ValidationFailedException("This sign-in method does not use one-time passwords."),
        };
        return await IssueOtpAsync(p, mobile, r.MacAddress, clientIp, ct);
    }

    private async Task<PortalOtpResponse> IssueOtpAsync(Portal p, string mobile, string? mac, string? clientIp, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var last = await _db.OtpChallenges.AsNoTracking().Where(o => o.SiteId == p.Site.Id && o.MobileNumber == mobile).OrderByDescending(o => o.CreatedAt).FirstOrDefaultAsync(ct);
        if (last != null && (now - last.CreatedAt).TotalSeconds < p.Settings.OtpResendCooldownSeconds)
            throw new ConflictException($"Please wait {p.Settings.OtpResendCooldownSeconds - (int)(now - last.CreatedAt).TotalSeconds} seconds before requesting another code.");
        var hourAgo = now.AddHours(-1);
        if (await _db.OtpChallenges.CountAsync(o => o.SiteId == p.Site.Id && o.MobileNumber == mobile && o.CreatedAt >= hourAgo, ct) >= MaxOtpPerMobilePerHour)
            throw new ConflictException("Too many codes requested for this number. Try again later.");
        if (clientIp != null && await _db.OtpChallenges.CountAsync(o => o.IpAddress == clientIp && o.CreatedAt >= hourAgo, ct) >= MaxOtpPerIpPerHour)
            throw new ConflictException("Too many codes requested from this device. Try again later.");

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var challenge = new OtpChallenge
        {
            OrganizationId = p.Org.Id, SiteId = p.Site.Id, MobileNumber = mobile, CodeHash = HashOtp(code),
            ExpiresAt = now.AddSeconds(p.Settings.OtpExpirySeconds), IpAddress = clientIp, MacAddress = AccessPolicy.NormalizeMac(mac),
        };
        _db.OtpChallenges.Add(challenge);
        await _db.SaveChangesAsync(ct);

        var sent = await _sms.SendOtpAsync(p.Org.Id, p.Site.Id, mobile, code, Math.Max(1, p.Settings.OtpExpirySeconds / 60), ct);
        if (!sent.Success)
        {
            _db.OtpChallenges.Remove(challenge);
            await _db.SaveChangesAsync(ct);
            throw new ServiceUnavailableException("We could not send the code right now. Please try again or ask the front desk for help.");
        }
        return new PortalOtpResponse(challenge.Id, SmsSender.Mask(mobile), p.Settings.OtpExpirySeconds, p.Settings.OtpResendCooldownSeconds, sent.DevelopmentLog);
    }

    private async Task VerifyOtpAsync(Portal p, string mobile, Guid? challengeId, string? otp, CancellationToken ct)
    {
        if (challengeId == null || string.IsNullOrWhiteSpace(otp)) throw new ValidationFailedException("Enter the code we sent to your mobile.");
        var c = await _db.OtpChallenges.FirstOrDefaultAsync(o => o.Id == challengeId && o.SiteId == p.Site.Id, ct);
        if (c == null || c.MobileNumber != mobile || c.ConsumedAt != null) throw new ValidationFailedException("This code is no longer valid. Request a new one.");
        if (c.ExpiresAt <= DateTime.UtcNow) throw new ValidationFailedException("The code has expired. Request a new one.");
        if (c.Attempts >= p.Settings.OtpMaxAttempts) throw new ValidationFailedException("Too many wrong attempts. Request a new code.");
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(c.CodeHash), Convert.FromHexString(HashOtp(otp.Trim()))))
        {
            c.Attempts++;
            await _db.SaveChangesAsync(ct);
            var left = p.Settings.OtpMaxAttempts - c.Attempts;
            throw new ValidationFailedException(left > 0 ? $"Incorrect code. {left} attempt{(left == 1 ? "" : "s")} left." : "Too many wrong attempts. Request a new code.");
        }
        c.ConsumedAt = DateTime.UtcNow;
    }

    // ---------------- Sign-in ----------------

    public async Task<PortalLoginResponse> LoginAsync(string portalKey, PortalLoginRequest r, string? clientIp, CancellationToken ct = default)
    {
        var p = await ResolveAsync(portalKey, ct);
        var method = await RequireMethodAsync(p, r.Method!.Value, ct);
        if (!r.AcceptTerms) throw new ValidationFailedException("Please accept the terms of use to continue.");
        var mac = AccessPolicy.NormalizeMac(r.MacAddress);
        Subscriber subscriber;

        switch (r.Method)
        {
            case AuthenticationType.MobileOTP:
            case AuthenticationType.GuestOTP:
            {
                var mobile = RequireMobile(r.MobileNumber);
                await VerifyOtpAsync(p, mobile, r.ChallengeId, r.Otp, ct);
                subscriber = await FindOrCreateGuestAsync(p, mobile, ct);
                break;
            }
            case AuthenticationType.RoomMobile:
            {
                var mobile = RequireMobile(r.MobileNumber);
                var room = (r.RoomNumber ?? "").Trim().ToUpperInvariant();
                if (room.Length == 0) throw new ValidationFailedException("Enter your room number.");
                var now = DateTime.UtcNow;
                // Room numbers are only unique within a property: the lookup is always scoped to this site.
                var guests = await _db.Subscribers.Where(s => s.OrganizationId == p.Org.Id && s.SiteId == p.Site.Id && s.SubscriberType == SubscriberType.RoomGuest
                                                              && s.RoomNumber == room && s.Status == SubscriberStatus.Active).ToListAsync(ct);
                var match = guests.FirstOrDefault(g => SameMobile(g.MobileNumber, mobile) && (g.CheckInAt == null || g.CheckInAt <= now) && g.CheckOutAt > now);
                // Room + mobile always requires a matching checked-in guest; there is no anonymous fallback.
                if (match == null)
                    throw new ValidationFailedException("We could not find a current stay for this room and mobile number. Please check with the front desk.");
                if (method.RequireOTP) await VerifyOtpAsync(p, mobile, r.ChallengeId, r.Otp, ct);
                subscriber = match!;
                break;
            }
            case AuthenticationType.StudentOTP:
            {
                var s = await FindByIdentifierAsync(p, r.Identifier, new[] { SubscriberType.Student }, ct)
                        ?? throw new ValidationFailedException("Invalid student ID or code.");
                if (s.MobileNumber == null) throw new ValidationFailedException("No mobile number is registered for this student ID.");
                await VerifyOtpAsync(p, s.MobileNumber, r.ChallengeId, r.Otp, ct);
                subscriber = s;
                break;
            }
            case AuthenticationType.StudentPassword:
            case AuthenticationType.StaffPassword:
            case AuthenticationType.EmployeePassword:
            case AuthenticationType.UsernamePassword:
            {
                var allowed = r.Method switch
                {
                    AuthenticationType.StudentPassword => new[] { SubscriberType.Student },
                    AuthenticationType.StaffPassword => new[] { SubscriberType.Teacher, SubscriberType.Staff },
                    AuthenticationType.EmployeePassword => new[] { SubscriberType.Employee },
                    _ => Enum.GetValues<SubscriberType>().Where(t => t is not SubscriberType.RoomGuest).ToArray(),
                };
                var s = await FindByIdentifierAsync(p, r.Identifier, allowed, ct);
                if (s?.PasswordHash == null || string.IsNullOrEmpty(r.Password))
                {
                    _passwords.VerifyDummy(r.Password ?? "x");
                    throw new UnauthorizedAppException("Invalid ID or password.");
                }
                if (!_passwords.Verify(s.PasswordHash, r.Password)) throw new UnauthorizedAppException("Invalid ID or password.");
                if (method.RequireOTP)
                {
                    // Check access before spending an SMS on a blocked account.
                    var pre = await AccessPolicy.EvaluateAsync(_db, s, p.Org.Id, p.Site.Id, mac, DateTime.UtcNow, ct);
                    if (!pre.Allowed) throw new ForbiddenException(PublicReason(pre.Reason!));
                    if (s.MobileNumber == null) throw new ValidationFailedException("Two-step sign-in needs a registered mobile number. Contact the administrator.");
                    if (r.ChallengeId == null)
                    {
                        var otp = await IssueOtpAsync(p, s.MobileNumber, r.MacAddress, clientIp, ct);
                        return new PortalLoginResponse("OtpRequired", null, null, null, null, otp.ChallengeId, otp.MaskedMobile,
                            otp.DevelopmentMode ? "Development mode: the code was written to the server log, not sent by SMS." : $"We sent a code to {otp.MaskedMobile}.");
                    }
                    await VerifyOtpAsync(p, s.MobileNumber, r.ChallengeId, r.Otp, ct);
                }
                subscriber = s;
                break;
            }
            case AuthenticationType.Voucher:
                throw new ValidationFailedException("Voucher sign-in is not available yet.");
            default:
                throw new ValidationFailedException("Unsupported sign-in method.");
        }

        var decision = await AccessPolicy.EvaluateAsync(_db, subscriber, p.Org.Id, p.Site.Id, mac, DateTime.UtcNow, ct);
        if (!decision.Allowed)
        {
            await _db.SaveChangesAsync(ct); // persist a consumed OTP
            throw new ForbiddenException(PublicReason(decision.Reason!));
        }

        var username = "nxt-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant();
        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(15)).Replace('+', 'x').Replace('/', 'y');
        var credential = new RadiusCredential
        {
            OrganizationId = p.Org.Id, SiteId = p.Site.Id, SubscriberId = subscriber.Id, NasId = p.Nas.Id, Username = username,
            PasswordHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password))), MacAddress = mac,
            ExpiresAt = DateTime.UtcNow.AddSeconds(Math.Clamp(_radius.PortalCredentialSeconds, 30, 900)),
        };
        _db.RadiusCredentials.Add(credential);
        _db.AuditLogs.Add(Audit.Entry(p.Org.Id, null, "PortalSignIn", nameof(Subscriber), subscriber.Id.ToString(),
            $"Method={r.Method}, Site={p.Site.Code}, Mac={mac ?? "-"}", clientIp));
        await _db.SaveChangesAsync(ct);
        return new PortalLoginResponse("Authorized", username, password, credential.ExpiresAt, decision.SessionTimeout, null, null,
            "You are signed in. Connecting you to the internet…");
    }

    // ---------------- helpers ----------------

    private async Task<OrganizationLoginMethod> RequireMethodAsync(Portal p, AuthenticationType type, CancellationToken ct) =>
        (await MethodsAsync(p, ct)).FirstOrDefault(m => m.AuthenticationType == type)
        ?? throw new ValidationFailedException("This sign-in method is not enabled at this location.");

    private async Task<Subscriber?> FindByIdentifierAsync(Portal p, string? identifier, SubscriberType[] types, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(identifier)) throw new ValidationFailedException("Enter your ID.");
        var n = identifier.Trim().ToUpperInvariant();
        var s = await _db.Subscribers.FirstOrDefaultAsync(x => x.OrganizationId == p.Org.Id && x.NormalizedIdentifier == n, ct);
        return s != null && types.Contains(s.SubscriberType) ? s : null;
    }

    private async Task<Subscriber> FindOrCreateGuestAsync(Portal p, string mobile, CancellationToken ct)
    {
        var n = mobile.ToUpperInvariant();
        var existing = await _db.Subscribers.FirstOrDefaultAsync(s => s.OrganizationId == p.Org.Id && s.NormalizedIdentifier == n, ct);
        if (existing != null) return existing;
        var max = await _db.Organizations.Where(o => o.Id == p.Org.Id).Select(o => o.SubscriptionPlan != null ? o.SubscriptionPlan.MaxUsers : null).FirstOrDefaultAsync(ct);
        if (max != null && await _db.Subscribers.CountAsync(s => s.OrganizationId == p.Org.Id && s.Status != SubscriberStatus.CheckedOut && s.Status != SubscriberStatus.Expired, ct) >= max)
            throw new ServiceUnavailableException("Wi-Fi sign-in is temporarily unavailable at this location (user limit reached).");
        var guest = new Subscriber
        {
            OrganizationId = p.Org.Id, SubscriberType = SubscriberType.Guest, Identifier = mobile, NormalizedIdentifier = n,
            FullName = "Guest " + SmsSender.Mask(mobile), MobileNumber = mobile, Notes = $"Self-registered via captive portal at {p.Site.Name}",
        };
        _db.Subscribers.Add(guest);
        return guest;
    }

    private static string RequireMobile(string? mobile)
    {
        var m = SubscriberService.NormalizeMobile(mobile);
        return m ?? throw new ValidationFailedException("Enter your mobile number.");
    }

    private static bool SameMobile(string? a, string b)
    {
        static string Digits(string s) => new(s.Where(char.IsDigit).ToArray());
        if (a == null) return false;
        var x = Digits(a);
        var y = Digits(b);
        return x.Length >= 10 && y.Length >= 10 && x[^10..] == y[^10..];
    }

    private string HashOtp(string code) => Convert.ToHexString(HMACSHA256.HashData(_otpKey, Encoding.UTF8.GetBytes(code)));

    /// <summary>Reasons safe to show on a public portal.</summary>
    private static string PublicReason(string reason) => reason switch
    {
        "Unknown user" => "Sign-in failed.",
        _ when reason.StartsWith("Device limit") => $"{reason}. Disconnect another device and try again.",
        _ => reason + ".",
    };
}
