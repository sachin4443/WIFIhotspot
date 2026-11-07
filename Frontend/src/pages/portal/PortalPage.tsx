import { useEffect, useMemo, useRef, useState, type FormEvent } from 'react';
import { useParams, useSearchParams } from 'react-router';
import { FiCheckCircle, FiLock, FiSmartphone, FiWifi } from 'react-icons/fi';
import { portalApi, type PortalLoginResult, type PortalOtp } from '../../api/portal';
import { getErrorMessage } from '../../api/errors';
import { Modal } from '../../components/ui/Modal';
import { useApi } from '../../hooks/useApi';
import { useNow } from '../../hooks/useNow';
import type { AuthenticationType } from '../../types/models';
import { isHexColor } from '../../utils/validation';

const LABELS: Record<AuthenticationType, string> = {
  MobileOTP: 'Mobile number', GuestOTP: 'Guest', RoomMobile: 'Hotel room', StudentPassword: 'Student', StudentOTP: 'Student (OTP)',
  StaffPassword: 'Staff', EmployeePassword: 'Employee', UsernamePassword: 'Account', Voucher: 'Voucher',
};
const ID_LABELS: Partial<Record<AuthenticationType, string>> = {
  StudentPassword: 'Student ID / enrollment number', StudentOTP: 'Student ID / enrollment number', StaffPassword: 'Staff ID',
  EmployeePassword: 'Employee ID', UsernamePassword: 'Username',
};
const OTP_FIRST: AuthenticationType[] = ['MobileOTP', 'GuestOTP', 'StudentOTP'];
const PASSWORD_METHODS: AuthenticationType[] = ['StudentPassword', 'StaffPassword', 'EmployeePassword', 'UsernamePassword'];

/**
 * Public captive portal. The router's login page sends visitors here with ?mac=…&link-login-only=…&dst=… .
 * After sign-in the one-time RADIUS credential is posted to the router, which authenticates it with the NXT RADIUS server.
 */
export function PortalPage() {
  const { key = '' } = useParams();
  const [params] = useSearchParams();
  const mac = params.get('mac') ?? undefined;
  const linkLogin = params.get('link-login-only') ?? params.get('link-login') ?? undefined;
  const dst = params.get('dst') ?? params.get('link-orig') ?? '';
  const config = useApi((s) => portalApi.config(key, s), [key]);
  const methods = useMemo(() => (config.data?.methods ?? []).filter((m) => m.authenticationType !== 'Voucher'), [config.data]);
  const [method, setMethod] = useState<AuthenticationType | null>(null);
  const active = method ?? methods.find((m) => m.isDefault)?.authenticationType ?? methods[0]?.authenticationType ?? null;
  const activeInfo = methods.find((m) => m.authenticationType === active);

  const [mobile, setMobile] = useState('');
  const [identifier, setIdentifier] = useState('');
  const [password, setPassword] = useState('');
  const [room, setRoom] = useState('');
  const [otp, setOtp] = useState('');
  const [challenge, setChallenge] = useState<PortalOtp | { challengeId: string; maskedMobile: string; resendAt: number } | null>(null);
  const [resendAt, setResendAt] = useState(0);
  const [accept, setAccept] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [done, setDone] = useState<PortalLoginResult | null>(null);
  const [showTerms, setShowTerms] = useState(false);
  const routerForm = useRef<HTMLFormElement>(null);
  const now = useNow(1000);

  useEffect(() => { document.title = config.data ? `Wi-Fi · ${config.data.organizationName}` : 'Wi-Fi sign-in'; }, [config.data]);
  useEffect(() => { if (done?.status === 'Authorized' && linkLogin && routerForm.current) routerForm.current.submit(); }, [done, linkLogin]);

  const switchMethod = (m: AuthenticationType) => { setMethod(m); setChallenge(null); setOtp(''); setError(null); setNotice(null); };
  const color = config.data && isHexColor(config.data.themeColor) ? config.data.themeColor : '#4f46e5';
  const needsOtpFirst = !!active && (OTP_FIRST.includes(active) || (active === 'RoomMobile' && !!activeInfo?.requireOTP));

  const sendOtp = async () => {
    if (!active) return;
    setError(null);
    setBusy(true);
    try {
      const r = await portalApi.requestOtp(key, { method: active, mobileNumber: mobile || undefined, identifier: identifier || undefined, roomNumber: room || undefined, macAddress: mac });
      setChallenge(r);
      setResendAt(Date.now() + r.resendInSeconds * 1000);
      setNotice(r.developmentMode ? 'Development mode: the code was written to the server log, not sent by SMS.' : `We sent a 6-digit code to ${r.maskedMobile}.`);
    } catch (err) {
      setError(getErrorMessage(err, 'Could not send the code.'));
    } finally {
      setBusy(false);
    }
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!active) return;
    if (needsOtpFirst && !challenge) { await sendOtp(); return; }
    if (!accept) { setError('Please accept the terms of use.'); return; }
    setError(null);
    setBusy(true);
    try {
      const r = await portalApi.login(key, {
        method: active, mobileNumber: mobile || undefined, identifier: identifier || undefined, password: password || undefined, roomNumber: room || undefined,
        challengeId: challenge?.challengeId, otp: otp || undefined, macAddress: mac, acceptTerms: accept,
      });
      if (r.status === 'OtpRequired') {
        setChallenge({ challengeId: r.challengeId!, maskedMobile: r.maskedMobile ?? '', resendAt: Date.now() });
        setNotice(r.message);
      } else {
        setDone(r);
      }
    } catch (err) {
      setError(getErrorMessage(err, 'Sign-in failed. Please try again.'));
    } finally {
      setBusy(false);
    }
  };

  if (config.error) {
    return (
      <div className="nxt-portal" style={{ ['--portal' as string]: '#4f46e5' }}>
        <div className="nxt-portal-card text-center"><FiWifi size={36} className="text-muted-2 mb-3" /><h1 className="h5">Wi-Fi sign-in unavailable</h1><p className="text-muted-2 mb-0">{config.error}</p></div>
      </div>
    );
  }
  if (!config.data) return <div className="nxt-portal"><div className="nxt-portal-card text-center text-muted-2"><span className="spinner-border spinner-border-sm me-2" />Loading…</div></div>;
  const c = config.data;
  const secondsLeft = Math.max(0, Math.ceil((resendAt - now) / 1000));

  return (
    <div className="nxt-portal" style={{ ['--portal' as string]: color, backgroundImage: c.backgroundUrl ? `url(${c.backgroundUrl})` : undefined }}>
      <div className="nxt-portal-card">
        <div className="nxt-portal-head">
          {c.logoUrl ? <img src={c.logoUrl} alt="" className="nxt-portal-logo" /> : <span className="nxt-portal-logo-fallback"><FiWifi /></span>}
          <h1 className="h5 fw-bold mb-1">{c.organizationName}</h1>
          <div className="fs-sm text-muted-2">{c.siteName} · Free Wi-Fi</div>
          {c.welcomeMessage && <p className="fs-sm mt-2 mb-0">{c.welcomeMessage}</p>}
        </div>

        {done ? (
          <div className="text-center py-3">
            <FiCheckCircle size={44} style={{ color }} className="mb-3" />
            <h2 className="h5 fw-semibold">You’re signed in</h2>
            <p className="text-muted-2 fs-sm">{linkLogin ? 'Connecting you to the internet…' : 'Access granted. Open this page from the Wi-Fi network to connect automatically.'}</p>
            {!linkLogin && (
              <div className="nxt-notice info text-start fs-sm"><div>One-time router login (valid 2 minutes): <code>{done.username}</code> / <code>{done.password}</code></div></div>
            )}
            {linkLogin && (
              <form ref={routerForm} method="post" action={linkLogin} className="d-none">
                <input type="hidden" name="username" value={done.username ?? ''} />
                <input type="hidden" name="password" value={done.password ?? ''} />
                <input type="hidden" name="dst" value={dst} />
                <input type="hidden" name="popup" value="false" />
              </form>
            )}
          </div>
        ) : methods.length === 0 ? (
          <p className="text-center text-muted-2 my-4">No sign-in method is enabled at this location. Please contact the staff.</p>
        ) : (
          <form onSubmit={submit} noValidate>
            {methods.length > 1 && (
              <div className="nxt-portal-methods" role="tablist">
                {methods.map((m) => (
                  <button key={m.authenticationType} type="button" role="tab" aria-selected={m.authenticationType === active}
                    className={`nxt-portal-method ${m.authenticationType === active ? 'is-active' : ''}`} onClick={() => switchMethod(m.authenticationType)}>
                    {LABELS[m.authenticationType]}
                  </button>
                ))}
              </div>
            )}

            <div className="d-grid gap-3">
              {active === 'RoomMobile' && (
                <div>
                  <label className="form-label" htmlFor="p-room">Room number</label>
                  <input id="p-room" className="form-control form-control-lg" inputMode="numeric" autoComplete="off" value={room} onChange={(e) => setRoom(e.target.value)} disabled={busy || !!challenge} />
                </div>
              )}
              {active && ID_LABELS[active] && (
                <div>
                  <label className="form-label" htmlFor="p-id">{ID_LABELS[active]}</label>
                  <input id="p-id" className="form-control form-control-lg" autoCapitalize="none" autoComplete="username" value={identifier} onChange={(e) => setIdentifier(e.target.value)} disabled={busy || !!challenge} />
                </div>
              )}
              {active && ['MobileOTP', 'GuestOTP', 'RoomMobile'].includes(active) && (
                <div>
                  <label className="form-label" htmlFor="p-mobile">{active === 'RoomMobile' ? 'Mobile number registered at check-in' : 'Mobile number'}</label>
                  <div className="input-group input-group-lg">
                    <span className="input-group-text"><FiSmartphone /></span>
                    <input id="p-mobile" type="tel" inputMode="tel" autoComplete="tel" className="form-control" value={mobile} onChange={(e) => setMobile(e.target.value)} disabled={busy || !!challenge} placeholder="10-digit mobile" />
                  </div>
                </div>
              )}
              {active && PASSWORD_METHODS.includes(active) && !challenge && (
                <div>
                  <label className="form-label" htmlFor="p-pass">Password</label>
                  <div className="input-group input-group-lg">
                    <span className="input-group-text"><FiLock /></span>
                    <input id="p-pass" type="password" autoComplete="current-password" className="form-control" value={password} onChange={(e) => setPassword(e.target.value)} disabled={busy} />
                  </div>
                </div>
              )}
              {challenge && (
                <div>
                  <label className="form-label" htmlFor="p-otp">6-digit code</label>
                  <input id="p-otp" className="form-control form-control-lg text-center font-monospace" style={{ letterSpacing: '0.4em' }} inputMode="numeric" autoComplete="one-time-code" maxLength={6} value={otp} onChange={(e) => setOtp(e.target.value.replace(/\D/g, ''))} disabled={busy} autoFocus />
                  <div className="d-flex justify-content-between fs-xs text-muted-2 mt-1">
                    <span>Sent to {challenge.maskedMobile}</span>
                    {OTP_FIRST.includes(active!) || active === 'RoomMobile'
                      ? <button type="button" className="btn btn-link btn-sm p-0" disabled={busy || secondsLeft > 0} onClick={sendOtp}>{secondsLeft > 0 ? `Resend in ${secondsLeft}s` : 'Resend code'}</button>
                      : null}
                  </div>
                </div>
              )}

              {notice && <div className="nxt-notice info fs-sm" role="status">{notice}</div>}
              {error && <div className="nxt-notice danger fs-sm" role="alert">{error}</div>}

              {(!needsOtpFirst || challenge) && (
                <label className="d-flex gap-2 align-items-start fs-sm">
                  <input type="checkbox" className="form-check-input mt-1 flex-shrink-0" checked={accept} onChange={(e) => setAccept(e.target.checked)} />
                  <span>I accept the {c.termsAndConditions || c.privacyPolicy ? <button type="button" className="btn btn-link btn-sm p-0 align-baseline" onClick={() => setShowTerms(true)}>terms of use and privacy policy</button> : 'terms of use'}.</span>
                </label>
              )}

              <button type="submit" className="btn btn-lg text-white nxt-portal-submit" disabled={busy}>
                {busy ? <span className="spinner-border spinner-border-sm" /> : needsOtpFirst && !challenge ? 'Send code' : 'Connect'}
              </button>
            </div>
          </form>
        )}
        <div className="text-center fs-xs text-muted-2 mt-4">Powered by NXT Hotspot</div>
      </div>
      <Modal open={showTerms} title="Terms of use" onClose={() => setShowTerms(false)} footer={<button type="button" className="btn btn-primary" onClick={() => { setAccept(true); setShowTerms(false); }}>I accept</button>}>
        {c.termsAndConditions && <div className="fs-sm" style={{ whiteSpace: 'pre-wrap' }}>{c.termsAndConditions}</div>}
        {c.privacyPolicy && <><h3 className="h6 mt-3">Privacy policy</h3><div className="fs-sm" style={{ whiteSpace: 'pre-wrap' }}>{c.privacyPolicy}</div></>}
      </Modal>
    </div>
  );
}
