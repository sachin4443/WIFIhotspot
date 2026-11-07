import { useId, type ReactNode } from 'react';

interface FieldProps {
  label: string;
  error?: string | null;
  hint?: ReactNode;
  required?: boolean;
  className?: string;
  children: (id: string, invalid: boolean) => ReactNode;
}

/** Label + control + validation message. The child render-prop receives the control id. */
export function Field({ label, error, hint, required, className = '', children }: FieldProps) {
  const id = useId();
  return (
    <div className={className}>
      <label htmlFor={id} className="form-label">
        {label} {required && <span className="text-danger" aria-hidden="true">*</span>}
      </label>
      {children(id, !!error)}
      {error ? <div className="invalid-feedback d-block">{error}</div> : hint ? <div className="form-text">{hint}</div> : null}
    </div>
  );
}

interface SwitchProps {
  label: ReactNode;
  checked: boolean;
  onChange: (checked: boolean) => void;
  description?: ReactNode;
  disabled?: boolean;
}

export function Switch({ label, checked, onChange, description, disabled }: SwitchProps) {
  const id = useId();
  return (
    <div className="form-check form-switch d-flex gap-2 align-items-start ps-0">
      <input id={id} className="form-check-input ms-0 mt-1 flex-shrink-0" type="checkbox" role="switch" checked={checked} disabled={disabled} onChange={(e) => onChange(e.target.checked)} />
      <label className="form-check-label" htmlFor={id}>
        <span className="d-block fw-medium fs-sm">{label}</span>
        {description && <span className="d-block text-muted-2 fs-xs">{description}</span>}
      </label>
    </div>
  );
}
