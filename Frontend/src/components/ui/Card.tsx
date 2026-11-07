import type { ReactNode } from 'react';

export function Card({ children, className = '' }: { children: ReactNode; className?: string }) {
  return <div className={`nxt-card ${className}`}>{children}</div>;
}

interface CardHeaderProps {
  title: ReactNode;
  subtitle?: ReactNode;
  icon?: ReactNode;
  actions?: ReactNode;
}

export function CardHeader({ title, subtitle, icon, actions }: CardHeaderProps) {
  return (
    <div className="nxt-card-header">
      {icon && <span className="text-primary d-inline-flex fs-5">{icon}</span>}
      <div className="min-w-0 flex-grow-1">
        <h2 className="nxt-card-title">{title}</h2>
        {subtitle && <p className="nxt-card-subtitle">{subtitle}</p>}
      </div>
      {actions && <div className="d-flex flex-wrap gap-2">{actions}</div>}
    </div>
  );
}

export function CardBody({ children, className = '' }: { children: ReactNode; className?: string }) {
  return <div className={`nxt-card-body ${className}`}>{children}</div>;
}
