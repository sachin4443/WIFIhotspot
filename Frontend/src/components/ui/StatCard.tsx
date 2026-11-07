import type { ReactNode } from 'react';

export type Tone = 'indigo' | 'blue' | 'green' | 'amber' | 'rose' | 'slate' | 'cyan';

interface StatCardProps {
  label: string;
  value: ReactNode;
  icon: ReactNode;
  tone?: Tone;
  hint?: ReactNode;
  loading?: boolean;
}

export function StatCard({ label, value, icon, tone = 'indigo', hint, loading }: StatCardProps) {
  return (
    <div className="nxt-card h-100">
      <div className="nxt-stat">
        <span className={`nxt-stat-icon tone-${tone}`} aria-hidden="true">{icon}</span>
        <div className="min-w-0 flex-grow-1">
          <div className="nxt-stat-label">{label}</div>
          {loading ? <div className="nxt-skeleton mt-2" style={{ width: '60%', height: 22 }} /> : <div className="nxt-stat-value">{value}</div>}
          {hint && !loading && <div className="nxt-stat-hint">{hint}</div>}
        </div>
      </div>
    </div>
  );
}
