export const isMobile = (v: string) => /^\+?[0-9]{10,15}$/.test(v.trim());
export const isEmail = (v: string) => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v.trim());
export const isUsername = (v: string) => /^[a-zA-Z0-9._@-]{3,50}$/.test(v.trim());
export const isSiteCode = (v: string) => /^[A-Za-z0-9_-]{2,30}$/.test(v.trim());
export const isHexColor = (v: string) => /^#[0-9a-fA-F]{6}$/.test(v.trim());
export const isHttpsUrl = (v: string) => { try { return new URL(v).protocol === 'https:'; } catch { return false; } };
export const isUrl = (v: string) => { try { const u = new URL(v); return u.protocol === 'https:' || u.protocol === 'http:'; } catch { return false; } };

/** Mirrors the backend PasswordPolicy: 10–128 chars, upper, lower and digit. */
export function passwordError(p: string): string | null {
  if (p.length < 10) return 'Use at least 10 characters.';
  if (p.length > 128) return 'Use at most 128 characters.';
  if (!/[A-Z]/.test(p) || !/[a-z]/.test(p) || !/[0-9]/.test(p)) return 'Include an uppercase letter, a lowercase letter and a digit.';
  return null;
}

/** Empty string -> null, otherwise a finite number or NaN. */
export const optionalNumber = (v: string): number | null => (v.trim() === '' ? null : Number(v));
