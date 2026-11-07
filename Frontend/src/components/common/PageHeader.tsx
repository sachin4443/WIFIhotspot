import type { ReactNode } from 'react';
import { Link } from 'react-router';

interface Crumb { label: string; to?: string }

interface PageHeaderProps {
  title: ReactNode;
  subtitle?: ReactNode;
  actions?: ReactNode;
  breadcrumbs?: Crumb[];
}

export function PageHeader({ title, subtitle, actions, breadcrumbs }: PageHeaderProps) {
  return (
    <div className="nxt-page-header">
      <div className="min-w-0">
        {breadcrumbs && breadcrumbs.length > 0 && (
          <nav className="nxt-breadcrumb" aria-label="Breadcrumb">
            {breadcrumbs.map((c, i) => (
              <span key={`${c.label}-${i}`}>
                {c.to ? <Link to={c.to}>{c.label}</Link> : c.label}
                {i < breadcrumbs.length - 1 && <span className="ms-1" aria-hidden="true">/</span>}
              </span>
            ))}
          </nav>
        )}
        <h1 className="nxt-page-title">{title}</h1>
        {subtitle && <p className="nxt-page-subtitle">{subtitle}</p>}
      </div>
      {actions && <div className="nxt-page-actions d-flex flex-wrap gap-2">{actions}</div>}
    </div>
  );
}
