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
  { icon: FiRadio, title: 'RADIUS authentication & accounting', text: 'Centralised AAA for every hotspot router you manage.' },
  { icon: FiWifi, title: 'Built for every venue', text: 'ISPs, hotels, campuses, enterprises and public Wi-Fi operators.' },
  { icon: FiShield, title: 'Multi-tenant by design', text: 'Organization isolation, role-based access and full audit trail.' },
];

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
      <section className="nxt-login-hero d-none d-lg-flex">
        <div className="d-flex align-items-center gap-3">
          <BrandMark size={44} />
          <div>
            <div className="h5 fw-bold mb-0 text-white">NXT Hotspot</div>
            <div className="small nxt-login-hero-muted">Wi-Fi hotspot management platform</div>
          </div>
        </div>

        <div>
          <h1 className="display-6 fw-bold text-white mb-3">Run your hotspot network from one console.</h1>
          <p className="lead nxt-login-hero-muted mb-5">
            Manage organizations, sites, administrators and authentication for every location you serve.
          </p>
          <ul className="list-unstyled d-grid gap-4 mb-0">
            {FEATURES.map(({ icon: Icon, title, text }) => (
              <li key={title} className="d-flex gap-3">
                <span className="nxt-login-feature-icon"><Icon aria-hidden="true" /></span>
                <span>
                  <span className="d-block fw-semibold text-white">{title}</span>
                  <span className="small nxt-login-hero-muted">{text}</span>
                </span>
              </li>
            ))}
          </ul>
        </div>

        <div className="small nxt-login-hero-muted">© {new Date().getFullYear()} NXT Hotspot</div>
      </section>

      <section className="nxt-login-panel">
        <div className="nxt-login-card">
          <div className="d-flex d-lg-none align-items-center gap-2 mb-4">
            <BrandMark size={36} />
            <span className="h5 fw-bold mb-0">NXT Hotspot</span>
          </div>

          <h2 className="h3 fw-bold mb-1">Sign in</h2>
          <p className="text-body-secondary mb-4">Use your administrator account to continue.</p>

          {error && (
            <div className="alert alert-danger d-flex align-items-start gap-2 py-2" role="alert">
              <FiAlertCircle className="flex-shrink-0 mt-1" aria-hidden="true" />
              <span>{error}</span>
            </div>
          )}

          <form noValidate onSubmit={onSubmit}>
            <div className="mb-3">
              <label htmlFor="username" className="form-label fw-medium">Username</label>
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
              <label htmlFor="password" className="form-label fw-medium">Password</label>
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

            <button type="submit" className="btn btn-primary w-100 py-2 d-flex align-items-center justify-content-center gap-2" disabled={submitting}>
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

          <p className="small text-body-secondary mt-4 mb-0">
            Accounts lock for 15 minutes after 5 failed attempts. Contact your platform administrator if you cannot sign in.
          </p>
        </div>
      </section>
    </div>
  );
}
