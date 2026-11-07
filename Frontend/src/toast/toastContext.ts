import { createContext } from 'react';

export type ToastVariant = 'success' | 'danger' | 'warning' | 'info';

export interface ToastOptions {
  title?: string;
  /** Milliseconds before auto-dismiss. 0 keeps the toast until closed. Default 5000. */
  durationMs?: number;
}

export interface ToastApi {
  show(variant: ToastVariant, message: string, options?: ToastOptions): void;
  success(message: string, options?: ToastOptions): void;
  error(message: string, options?: ToastOptions): void;
  warning(message: string, options?: ToastOptions): void;
  info(message: string, options?: ToastOptions): void;
}

export const ToastContext = createContext<ToastApi | null>(null);
