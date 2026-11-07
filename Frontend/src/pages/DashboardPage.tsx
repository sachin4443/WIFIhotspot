import { useCallback, useEffect, useState } from 'react';
import { FiCheckCircle, FiClock, FiKey, FiRefreshCw, FiServer, FiUser, FiXCircle } from 'react-icons/fi';
import { authApi } from '../api/auth';
import { getErrorMessage } from '../api/errors';
import { healthApi, type HealthResponse } from '../api/health';
import { useAuth } from '../auth/useAuth';
import { PageHeader } from '../components/common/PageHeader';
import { useNow } from '../hooks/useNow';
import type { CurrentUser } from '../types/auth';
import { formatDateTime, formatDuration } from '../utils/format';

type Load<T> = { state: 'loading' } | { state: 'ok'; data: T } | { state: 'error'; message: string };

/** Display names for permission resource prefixes (see Permissions.cs). */
const RESOURCE_LABELS: Record<string, string> = {
  organizations: 'Organizations',
  sites: 'Sites',
  adminusers: 'Admin users',
  roles: 'Roles',
  wallet: 'Wallet',
  organizationtypes: 'Organization types',
  subscriptionplans: 'Subscription plans',
  settings: 'Organization settings',
  audit: 'Audit log',
  'platform.sms': 'Platform SMS',
};

/** Groups "organizations.read" style codes by their resource prefix. */
function groupPermissions(codes: string[]): [string, string[]][] {
  const groups = new Map<string, string[]>();
  for (const code of [...codes].sort()) {
    const dot = code.lastIndexOf('.');
    const resource = dot > 0 ? code.slice(0, dot) : code;
    const action = dot > 0 ? code.slice(dot + 1) : code;
    groups.set(resource, [...(groups.get(resource) ?? []), action]);
  }
  return [...groups.entries()];
}

/**
 * Module 1 landing page: account, session and live API status, all from real endpoints
 * (GET /api/auth/me and GET /health). Platform statistics arrive in Module 2.
 */
export function DashboardPage() {
  const { user, session } = useAuth();
  const now = useNow(1000);
  const [me, setMe] = useState<Load<CurrentUser>>({ state: 'loading' });
  const [health, setHealth] = useState<Load<HealthResponse>>({ state: 'loading' });

  const load = useCallback((signal?: AbortSignal) => {
    setMe({ state: 'loading' });
    setHealth({ state: 'loading' });
    authApi
      .me(signal)
      .then((data) => setMe({ state: 'ok', data }))
      .catch((err: unknown) => {
        if (!signal?.aborted) setMe({ state: 'error', message: getErrorMessage(err) });
      });
    healthApi
      .check(signal)
      .then((data) => setHealth({ state: 'ok', data }))
      .catch((err: unknown) => {
        if (!signal?.aborted) setHealth({ state: 'error', message: getErrorMessage(err) });
      });
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    load(controller.signal);
    return () => controller.abort();
  }, [load]);

  if (!user || !session) return null;

  const remaining = Math.max(0, session.expiresAt - now);
  const refreshing = me.state === 'loading' || health.state === 'loading';

  return (
    <>
      <PageHeader
        title={`Welcome, ${user.fullName || user.username}`}
        subtitle={user.isPlatformAdmin ? 'You are signed in to the NXT Hotspot platform console.' : 'You are signed in to your organization console.'}
        actions={
          <button type="button" className="btn btn-outline-secondary btn-sm d-flex align-items-center gap-2" onClick={() => load()} disabled={refreshing}>
            <FiRefreshCw className={refreshing ? 'nxt-spin' : ''} aria-hidden="true" /> Refresh
          </button>
        }
      />

      <div className="row g-3 mb-3">
        <div className="col-12 col-md-6 col-xl-4">
          <div className="card nxt-card h-100">
            <div className="card-body">
              <div className="d-flex align-items-center gap-3 mb-3">
                <span className="nxt-stat-icon bg-primary-subtle text-primary"><FiUser aria-hidden="true" /></span>
                <div className="min-w-0">
                  <div className="text-body-secondary small">Signed in as</div>
                  <div className="fw-semibold text-truncate">{user.fullName || user.username}</div>
                </div>
              </div>
              <dl className="nxt-dl mb-0">
                <dt>Username</dt>
                <dd>@{user.username}</dd>
                <dt>Scope</dt>
                <dd>{user.isPlatformAdmin ? 'Platform (all organizations)' : 'Organization'}</dd>
                <dt>Roles</dt>
                <dd>
                  {user.roles.length > 0
                    ? user.roles.map((r) => <span key={r} className="badge text-bg-light border me-1">{r}</span>)
                    : <span className="text-body-secondary">None</span>}
                </dd>
              </dl>
            </div>
          </div>
        </div>

        <div className="col-12 col-md-6 col-xl-4">
          <div className="card nxt-card h-100">
            <div className="card-body">
              <div className="d-flex align-items-center gap-3 mb-3">
                <span className="nxt-stat-icon bg-warning-subtle text-warning-emphasis"><FiClock aria-hidden="true" /></span>
                <div>
                  <div className="text-body-secondary small">Session time remaining</div>
                  <div className="fs-4 fw-bold font-monospace">{formatDuration(remaining)}</div>
                </div>
              </div>
              <dl className="nxt-dl mb-0">
                <dt>Expires</dt>
                <dd>{formatDateTime(session.expiresAt)}</dd>
                <dt>Renewal</dt>
                <dd className="text-body-secondary">Sign in again when the session ends.</dd>
              </dl>
            </div>
          </div>
        </div>

        <div className="col-12 col-xl-4">
          <div className="card nxt-card h-100">
            <div className="card-body">
              <div className="d-flex align-items-center gap-3 mb-3">
                <span className="nxt-stat-icon bg-success-subtle text-success"><FiServer aria-hidden="true" /></span>
                <div>
                  <div className="text-body-secondary small">API connection</div>
                  <div className="fw-semibold">
                    {health.state === 'loading' && <span className="text-body-secondary">Checking…</span>}
                    {health.state === 'ok' && (
                      <span className="text-success d-inline-flex align-items-center gap-1"><FiCheckCircle aria-hidden="true" /> Online</span>
                    )}
                    {health.state === 'error' && (
                      <span className="text-danger d-inline-flex align-items-center gap-1"><FiXCircle aria-hidden="true" /> Unreachable</span>
                    )}
                  </div>
                </div>
              </div>
              <dl className="nxt-dl mb-0">
                <dt>Service</dt>
                <dd>{health.state === 'ok' ? health.data.service : '—'}</dd>
                <dt>Token</dt>
                <dd>
                  {me.state === 'loading' && <span className="text-body-secondary">Verifying…</span>}
                  {me.state === 'ok' && <span className="text-success">Accepted by the API</span>}
                  {me.state === 'error' && <span className="text-danger">{me.message}</span>}
                </dd>
              </dl>
              {health.state === 'error' && <div className="small text-danger mt-2">{health.message}</div>}
            </div>
          </div>
        </div>
      </div>

      <div className="card nxt-card">
        <div className="card-header bg-transparent d-flex align-items-center gap-2 py-3">
          <FiKey className="text-primary" aria-hidden="true" />
          <h2 className="h6 fw-semibold mb-0">Your permissions</h2>
          {me.state === 'ok' && <span className="badge text-bg-primary ms-auto">{me.data.permissions.length}</span>}
        </div>
        <div className="card-body">
          {me.state === 'loading' && (
            <div className="d-flex align-items-center gap-2 text-body-secondary" role="status">
              <span className="spinner-border spinner-border-sm" aria-hidden="true" /> Loading permissions…
            </div>
          )}
          {me.state === 'error' && <div className="alert alert-danger mb-0">{me.message}</div>}
          {me.state === 'ok' && me.data.permissions.length === 0 && (
            <p className="text-body-secondary mb-0">Your account has no permissions assigned yet.</p>
          )}
          {me.state === 'ok' && me.data.permissions.length > 0 && (
            <div className="row g-3">
              {groupPermissions(me.data.permissions).map(([resource, actions]) => (
                <div key={resource} className="col-12 col-sm-6 col-lg-4 col-xxl-3">
                  <div className="nxt-perm-group">
                    <div className="fw-semibold small mb-1">{RESOURCE_LABELS[resource] ?? resource}</div>
                    <div className="d-flex flex-wrap gap-1">
                      {actions.map((a) => (
                        <span key={a} className="badge text-bg-light border text-capitalize">{a}</span>
                      ))}
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </>
  );
}
