import type { ReactNode } from 'react';

export type BadgeTone = 'success' | 'warning' | 'danger' | 'info' | 'neutral';

export function Badge({ tone = 'neutral', dot, children }: { tone?: BadgeTone; dot?: boolean; children: ReactNode }) {
  return <span className={`nxt-badge ${tone} ${dot ? 'nxt-badge-dot' : ''}`}>{children}</span>;
}

const STATUS_TONES: Record<string, BadgeTone> = {
  active: 'success', online: 'success', accept: 'success', accepted: 'success', ok: 'success', credit: 'success', enabled: 'success',
  pending: 'warning', suspended: 'warning', expiring: 'warning', stale: 'warning', checkedout: 'neutral',
  inactive: 'neutral', disabled: 'neutral', stopped: 'neutral', expired: 'danger', reject: 'danger', rejected: 'danger',
  offline: 'danger', failed: 'danger', debit: 'danger', blocked: 'danger', error: 'danger',
};

/** Colour-codes common status words. */
export function StatusBadge({ status, label }: { status: string; label?: string }) {
  const tone = STATUS_TONES[status.replace(/[\s_-]/g, '').toLowerCase()] ?? 'neutral';
  return <Badge tone={tone} dot>{label ?? status.replace(/([a-z])([A-Z])/g, '$1 $2')}</Badge>;
}

export function ActiveBadge({ active }: { active: boolean }) {
  return <StatusBadge status={active ? 'Active' : 'Inactive'} />;
}
