import { useState, type FormEvent } from 'react';
import { useLocation, useNavigate } from 'react-router';
import { FiAlertCircle, FiEye, FiEyeOff, FiLock, FiLogIn, FiRadio, FiShield, FiUser, FiWifi } from 'react-icons/fi';
import { getErrorMessage } from '../api/errors';
import { safeRedirect } from '../auth/ProtectedRoute';
import { useAuth } from '../auth/useAuth';
import { BrandMark } from '../components/common/BrandMark';
import { useToast } from '../toast/useToast';

// Limits from NxtHotspot.Application.Dtos.LoginRequest.
const USERNAME_MAX = 50;
const PASSWORD_MAX = 128;

const FEATURES = [
  { icon: FiRadio, title: 'Custom RADIUS engine', text: 'Authentication, accounting and session control for MikroTik hotspots.' },
  { icon: FiWifi, title: 'Venue-aware sign-in', text: 'Mobile OTP, hotel room login, student and employee credentials.' },
  { icon: FiShield, title: 'Multi-tenant by design', text: 'Organization isolation, role-based access and full audit trail.' },
];

const VENUES = ['ISP', 'Hotel', 'School', 'College', 'University', 'Enterprise', 'Public Wi-Fi', 'Hospital', 'Café', 'Mall', 'Transport'];

export function LoginPage() {
  const { login } = useAuth();
  const toast = useToast();
  const navigate = useNavigate();
  const location = useLocation();

  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [touched, setTouched] = useState(false);

  const usernameInvalid = touched && username.trim().length === 0;
  const passwordInvalid = touched && password.length === 0;

  const onSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setTouched(true);
    if (!username.trim() || !password || submitting) return;

    setSubmitting(true);
    setError(null);
    try {
      await login({ username: username.trim(), password });
      toast.success('Signed in successfully.', { title: 'Welcome back' });
      navigate(safeRedirect(location.state), { replace: true });
    } catch (err) {
      setError(getErrorMessage(err, 'Sign-in failed. Please try again.'));
      setPassword('');
      setTouched(false);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="nxt-login">
      <section className="nxt-login-aside d-none d-lg-flex">
        <div className="d-flex align-items-center gap-3">
          <BrandMark size={40} />
          <div>
            <div className="h5 fw-bold mb-0">NXT Hotspot</div>
            <div className="fs-sm text-muted-2">Wi-Fi hotspot &amp; RADIUS management</div>
          </div>
        </div>

        <div>
          <h1 className="fw-bold mb-3" style={{ fontSize: '2.1rem', lineHeight: 1.2 }}>One platform for every hotspot you operate.</h1>
          <p className="text-muted-2 mb-4" style={{ fontSize: '1.05rem' }}>
            Organizations, sites, NAS devices, subscribers, packages and sessions — managed from a single, multi-tenant console.
          </p>
          <ul className="list-unstyled d-grid gap-3 mb-4">
            {FEATURES.map(({ icon: Icon, title, text }) => (
              <li key={title} className="d-flex gap-3">
                <span className="nxt-login-feature-icon"><Icon aria-hidden="true" /></span>
                <span>
                  <span className="d-block fw-semibold">{title}</span>
                  <span className="fs-sm text-muted-2">{text}</span>
                </span>
              </li>
            ))}
          </ul>
          <div className="nxt-login-types">
            {VENUES.map((v) => <span key={v} className="nxt-badge neutral">{v}</span>)}
          </div>
        </div>

        <div className="fs-xs text-muted-2">© {new Date().getFullYear()} NXT Hotspot</div>
      </section>

      <section className="nxt-login-panel">
        <div className="nxt-login-card">
          <div className="d-flex d-lg-none align-items-center gap-2 mb-4">
            <BrandMark size={36} />
            <div>
              <div className="h5 fw-bold mb-0">NXT Hotspot</div>
              <div className="fs-xs text-muted-2">Wi-Fi hotspot &amp; RADIUS management</div>
            </div>
          </div>

          <h2 className="h3 fw-bold mb-1">Sign in to your console</h2>
          <p className="text-muted-2 mb-4">Platform, organization and site administrators sign in here.</p>

          {error && (
            <div className="alert alert-danger d-flex align-items-start gap-2 py-2" role="alert">
              <FiAlertCircle className="flex-shrink-0 mt-1" aria-hidden="true" />
              <span>{error}</span>
            </div>
          )}

          <form noValidate onSubmit={onSubmit}>
            <div className="mb-3">
              <label htmlFor="username" className="form-label">Username</label>
              <div className="input-group has-validation">
                <span className="input-group-text"><FiUser aria-hidden="true" /></span>
                <input
                  id="username"
                  name="username"
                  type="text"
                  className={`form-control ${usernameInvalid ? 'is-invalid' : ''}`}
                  autoComplete="username"
                  autoCapitalize="none"
                  spellCheck={false}
                  maxLength={USERNAME_MAX}
                  value={username}
                  onChange={(e) => setUsername(e.target.value)}
                  disabled={submitting}
                  autoFocus
                  required
                />
                <div className="invalid-feedback">Enter your username.</div>
              </div>
            </div>

            <div className="mb-4">
              <label htmlFor="password" className="form-label">Password</label>
              <div className="input-group has-validation">
                <span className="input-group-text"><FiLock aria-hidden="true" /></span>
                <input
                  id="password"
                  name="password"
                  type={showPassword ? 'text' : 'password'}
                  className={`form-control ${passwordInvalid ? 'is-invalid' : ''}`}
                  autoComplete="current-password"
                  maxLength={PASSWORD_MAX}
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  disabled={submitting}
                  required
                />
                <button
                  type="button"
                  className="btn btn-outline-secondary nxt-password-toggle"
                  onClick={() => setShowPassword((v) => !v)}
                  aria-label={showPassword ? 'Hide password' : 'Show password'}
                  disabled={submitting}
                >
                  {showPassword ? <FiEyeOff /> : <FiEye />}
                </button>
                <div className="invalid-feedback">Enter your password.</div>
              </div>
            </div>

            <button type="submit" className="btn btn-primary w-100 py-2 mt-1 d-flex align-items-center justify-content-center gap-2" disabled={submitting}>
              {submitting ? (
                <>
                  <span className="spinner-border spinner-border-sm" aria-hidden="true" /> Signing in…
                </>
              ) : (
                <>
                  <FiLogIn aria-hidden="true" /> Sign in
                </>
              )}
            </button>
          </form>

          <p className="fs-xs text-muted-2 mt-4 mb-0">
            Accounts lock for 15 minutes after 5 failed attempts. Contact your platform administrator if you cannot sign in.
          </p>
        </div>
      </section>
    </div>
  );
}
