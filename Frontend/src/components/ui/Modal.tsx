import { useEffect, useId, useRef, type ReactNode } from 'react';
import { FiX } from 'react-icons/fi';

interface ModalProps {
  open: boolean;
  title: ReactNode;
  subtitle?: ReactNode;
  onClose: () => void;
  children: ReactNode;
  footer?: ReactNode;
  size?: 'sm' | 'md' | 'lg';
  /** Prevent closing via backdrop/Escape while saving. */
  busy?: boolean;
}

/** Accessible modal dialog. Becomes a bottom sheet on phones. */
export function Modal({ open, title, subtitle, onClose, children, footer, size = 'md', busy }: ModalProps) {
  const titleId = useId();
  const dialogRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    const previous = document.activeElement as HTMLElement | null;
    const original = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    const focusTarget = dialogRef.current?.querySelector<HTMLElement>('input, select, textarea, button:not(.nxt-modal-close)');
    (focusTarget ?? dialogRef.current)?.focus();
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && !busy) onClose();
    };
    document.addEventListener('keydown', onKey);
    return () => {
      document.body.style.overflow = original;
      document.removeEventListener('keydown', onKey);
      previous?.focus?.();
    };
  }, [open, busy, onClose]);

  if (!open) return null;

  return (
    <div className="nxt-modal-backdrop" onMouseDown={(e) => { if (e.target === e.currentTarget && !busy) onClose(); }}>
      <div ref={dialogRef} className={`nxt-modal size-${size}`} role="dialog" aria-modal="true" aria-labelledby={titleId} tabIndex={-1}>
        <div className="nxt-modal-header">
          <div className="min-w-0 flex-grow-1">
            <h2 id={titleId} className="nxt-modal-title">{title}</h2>
            {subtitle && <div className="text-muted-2 fs-sm mt-1">{subtitle}</div>}
          </div>
          <button type="button" className="nxt-icon-btn nxt-modal-close" onClick={onClose} disabled={busy} aria-label="Close">
            <FiX />
          </button>
        </div>
        <div className="nxt-modal-body">{children}</div>
        {footer && <div className="nxt-modal-footer">{footer}</div>}
      </div>
    </div>
  );
}
