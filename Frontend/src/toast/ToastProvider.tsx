import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { FiAlertCircle, FiAlertTriangle, FiCheckCircle, FiInfo } from 'react-icons/fi';
import { ToastContext, type ToastApi, type ToastOptions, type ToastVariant } from './toastContext';

interface ToastItem {
  id: number;
  variant: ToastVariant;
  title?: string;
  message: string;
  durationMs: number;
}

const ICONS: Record<ToastVariant, ReactNode> = {
  success: <FiCheckCircle />,
  danger: <FiAlertCircle />,
  warning: <FiAlertTriangle />,
  info: <FiInfo />,
};

const DEFAULT_TITLES: Record<ToastVariant, string> = {
  success: 'Success',
  danger: 'Error',
  warning: 'Warning',
  info: 'Notice',
};

const MAX_VISIBLE = 4;

/** Bootstrap 5 toast markup driven by React state (no Bootstrap JavaScript needed). */
export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<ToastItem[]>([]);
  const nextId = useRef(1);

  const dismiss = useCallback((id: number) => {
    setToasts((current) => current.filter((t) => t.id !== id));
  }, []);

  const show = useCallback((variant: ToastVariant, message: string, options?: ToastOptions) => {
    const item: ToastItem = {
      id: nextId.current++,
      variant,
      message,
      title: options?.title,
      durationMs: options?.durationMs ?? (variant === 'danger' ? 7000 : 5000),
    };
    setToasts((current) => [...current, item].slice(-MAX_VISIBLE));
  }, []);

  const api = useMemo<ToastApi>(
    () => ({
      show,
      success: (m, o) => show('success', m, o),
      error: (m, o) => show('danger', m, o),
      warning: (m, o) => show('warning', m, o),
      info: (m, o) => show('info', m, o),
    }),
    [show],
  );

  return (
    <ToastContext.Provider value={api}>
      {children}
      <div className="toast-container position-fixed top-0 end-0 p-3 nxt-toast-container" aria-live="polite" aria-atomic="false">
        {toasts.map((t) => (
          <ToastView key={t.id} toast={t} onDismiss={dismiss} />
        ))}
      </div>
    </ToastContext.Provider>
  );
}

function ToastView({ toast, onDismiss }: { toast: ToastItem; onDismiss: (id: number) => void }) {
  useEffect(() => {
    if (toast.durationMs <= 0) return;
    const timer = window.setTimeout(() => onDismiss(toast.id), toast.durationMs);
    return () => window.clearTimeout(timer);
  }, [toast.id, toast.durationMs, onDismiss]);

  return (
    <div
      className={`toast show nxt-toast nxt-toast-${toast.variant}`}
      role={toast.variant === 'danger' ? 'alert' : 'status'}
      aria-atomic="true"
    >
      <div className="d-flex align-items-start gap-2 p-3">
        <span className="nxt-toast-icon" aria-hidden="true">
          {ICONS[toast.variant]}
        </span>
        <div className="flex-grow-1 min-w-0">
          <div className="fw-semibold small">{toast.title ?? DEFAULT_TITLES[toast.variant]}</div>
          <div className="fs-sm text-muted-2 text-break">{toast.message}</div>
        </div>
        <button type="button" className="btn-close btn-sm" aria-label="Close" onClick={() => onDismiss(toast.id)} />
      </div>
    </div>
  );
}
