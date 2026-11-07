import type { ReactNode } from 'react';
import { FiAlertCircle, FiAlertTriangle, FiCheckCircle, FiInbox, FiInfo, FiRefreshCw } from 'react-icons/fi';

export function EmptyState({ icon, title, text, action }: { icon?: ReactNode; title: string; text?: ReactNode; action?: ReactNode }) {
  return (
    <div className="nxt-empty">
      <div className="nxt-empty-icon" aria-hidden="true">{icon ?? <FiInbox />}</div>
      <div className="nxt-empty-title">{title}</div>
      {text && <p className="nxt-empty-text">{text}</p>}
      {action}
    </div>
  );
}

export function ErrorState({ message, onRetry }: { message: string; onRetry?: () => void }) {
  return (
    <div className="nxt-empty">
      <div className="nxt-empty-icon" style={{ background: '#fef2f2', color: '#dc2626' }} aria-hidden="true"><FiAlertCircle /></div>
      <div className="nxt-empty-title">Could not load data</div>
      <p className="nxt-empty-text">{message}</p>
      {onRetry && (
        <button type="button" className="btn btn-light btn-sm btn-icon" onClick={onRetry}>
          <FiRefreshCw aria-hidden="true" /> Try again
        </button>
      )}
    </div>
  );
}

export function LoadingBlock({ label = 'Loading…' }: { label?: string }) {
  return (
    <div className="nxt-loading-row" role="status">
      <span className="spinner-border spinner-border-sm text-primary me-2" aria-hidden="true" />
      {label}
    </div>
  );
}

const NOTICE_ICONS = { info: <FiInfo />, warning: <FiAlertTriangle />, danger: <FiAlertCircle />, success: <FiCheckCircle /> };

export function Notice({ tone = 'info', children, className = '' }: { tone?: keyof typeof NOTICE_ICONS; children: ReactNode; className?: string }) {
  return (
    <div className={`nxt-notice ${tone} ${className}`} role={tone === 'danger' ? 'alert' : 'note'}>
      {NOTICE_ICONS[tone]}
      <div className="min-w-0">{children}</div>
    </div>
  );
}
