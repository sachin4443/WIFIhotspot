import type { ReactNode } from 'react';
import { useIsMobile } from '../../hooks/useMediaQuery';
import { EmptyState, ErrorState, LoadingBlock } from './States';

export interface Column<T> {
  key: string;
  header: ReactNode;
  render: (row: T) => ReactNode;
  className?: string;
  /** Text alignment for desktop cells. */
  align?: 'start' | 'end';
  /** Leave out of the mobile card (e.g. already shown as the card title). */
  hideOnMobile?: boolean;
}

interface DataTableProps<T> {
  columns: Column<T>[];
  rows: T[] | undefined;
  rowKey: (row: T) => string;
  loading?: boolean;
  error?: string | null;
  onRetry?: () => void;
  empty?: { title: string; text?: ReactNode; action?: ReactNode; icon?: ReactNode };
  onRowClick?: (row: T) => void;
  /** Mobile card title and subtitle. Defaults to the first column. */
  mobileTitle?: (row: T) => ReactNode;
  mobileBadge?: (row: T) => ReactNode;
  /** Row actions, shown in the last column on desktop and at the bottom of each card on mobile. */
  actions?: (row: T) => ReactNode;
}

/**
 * Responsive data table: a real table on tablet/desktop (horizontally scrollable when needed)
 * and a card list on phones.
 */
export function DataTable<T>({ columns, rows, rowKey, loading, error, onRetry, empty, onRowClick, mobileTitle, mobileBadge, actions }: DataTableProps<T>) {
  const isMobile = useIsMobile();

  if (error) return <ErrorState message={error} onRetry={onRetry} />;
  if (loading && !rows) return <LoadingBlock />;
  if (!rows || rows.length === 0) {
    return <EmptyState title={empty?.title ?? 'Nothing here yet'} text={empty?.text} action={empty?.action} icon={empty?.icon} />;
  }

  if (isMobile) {
    const [first, ...rest] = columns;
    return (
      <ul className="nxt-card-list" aria-busy={loading}>
        {rows.map((row) => (
          <li
            key={rowKey(row)}
            className={`nxt-card-list-item ${onRowClick ? 'is-clickable' : ''}`}
            onClick={onRowClick ? () => onRowClick(row) : undefined}
          >
            <div className="nxt-card-list-head">
              <div className="min-w-0">{mobileTitle ? mobileTitle(row) : first.render(row)}</div>
              {mobileBadge && <div className="flex-shrink-0">{mobileBadge(row)}</div>}
            </div>
            <div className="nxt-card-list-fields">
              {(mobileTitle ? columns : rest).filter((c) => !c.hideOnMobile).map((c) => (
                <div key={c.key} className="min-w-0">
                  <div className="nxt-card-list-field-label">{c.header}</div>
                  <div className="nxt-card-list-field-value">{c.render(row)}</div>
                </div>
              ))}
            </div>
            {actions && (
              <div className="nxt-card-list-actions" onClick={(e) => e.stopPropagation()}>
                {actions(row)}
              </div>
            )}
          </li>
        ))}
      </ul>
    );
  }

  return (
    <div className="nxt-table-wrap" aria-busy={loading}>
      <table className="nxt-table">
        <thead>
          <tr>
            {columns.map((c) => (
              <th key={c.key} className={`${c.align === 'end' ? 'text-end' : ''} ${c.className ?? ''}`} scope="col">{c.header}</th>
            ))}
            {actions && <th className="text-end" scope="col"><span className="visually-hidden">Actions</span></th>}
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={rowKey(row)} className={onRowClick ? 'is-clickable' : ''} onClick={onRowClick ? () => onRowClick(row) : undefined}>
              {columns.map((c) => (
                <td key={c.key} className={`${c.align === 'end' ? 'text-end' : ''} ${c.className ?? ''}`}>{c.render(row)}</td>
              ))}
              {actions && (
                <td className="text-end text-nowrap" onClick={(e) => e.stopPropagation()}>
                  <div className="d-inline-flex gap-2">{actions(row)}</div>
                </td>
              )}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
