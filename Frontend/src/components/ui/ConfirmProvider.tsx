import { useCallback, useRef, useState, type ReactNode } from 'react';
import { ConfirmContext, type ConfirmOptions } from './confirmContext';
import { Modal } from './Modal';

/** Promise-based confirmation dialog: `if (await confirm({...})) { ... }`. */
export function ConfirmProvider({ children }: { children: ReactNode }) {
  const [options, setOptions] = useState<ConfirmOptions | null>(null);
  const resolver = useRef<((value: boolean) => void) | null>(null);

  const confirm = useCallback((opts: ConfirmOptions) => {
    setOptions(opts);
    return new Promise<boolean>((resolve) => {
      resolver.current = resolve;
    });
  }, []);

  const close = useCallback((result: boolean) => {
    resolver.current?.(result);
    resolver.current = null;
    setOptions(null);
  }, []);

  return (
    <ConfirmContext.Provider value={confirm}>
      {children}
      <Modal
        open={options !== null}
        size="sm"
        title={options?.title ?? ''}
        onClose={() => close(false)}
        footer={
          <>
            <button type="button" className="btn btn-light" onClick={() => close(false)}>Cancel</button>
            <button type="button" className={`btn ${options?.tone === 'primary' ? 'btn-primary' : 'btn-danger'}`} onClick={() => close(true)}>
              {options?.confirmLabel ?? 'Confirm'}
            </button>
          </>
        }
      >
        <p className="mb-0 text-muted-2">{options?.message}</p>
      </Modal>
    </ConfirmContext.Provider>
  );
}
