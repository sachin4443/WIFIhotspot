/** Runtime configuration resolved from Vite environment variables. */
const rawBaseUrl = (import.meta.env.VITE_API_BASE_URL ?? '').trim();

export const env = {
  /** Empty string = same origin (the Vite dev proxy in development). No trailing slash. */
  apiBaseUrl: rawBaseUrl.replace(/\/+$/, ''),
  appName: 'NXT Hotspot',
} as const;
