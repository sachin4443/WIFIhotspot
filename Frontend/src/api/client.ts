import axios, { AxiosHeaders } from 'axios';
import { env } from '../config/env';

/**
 * Shared Axios instance for the NXT Hotspot API.
 * The auth layer registers a token getter and an "unauthorized" handler so this module
 * stays free of React state.
 */
export const apiClient = axios.create({
  baseURL: env.apiBaseUrl || undefined,
  timeout: 30_000,
  headers: { Accept: 'application/json' },
});

type TokenGetter = () => string | null;
type UnauthorizedHandler = () => void;

let getToken: TokenGetter = () => null;
let onUnauthorized: UnauthorizedHandler = () => {};

export function configureApiAuth(tokenGetter: TokenGetter, unauthorizedHandler: UnauthorizedHandler): void {
  getToken = tokenGetter;
  onUnauthorized = unauthorizedHandler;
}

/** Requests that must never carry a token or trigger a session-expired logout. */
const ANONYMOUS_PATHS = ['/api/auth/login'];

function isAnonymous(url: string | undefined): boolean {
  // Captive-portal calls are public: never attach an admin token, never treat their 401s as session expiry.
  return !!url && (ANONYMOUS_PATHS.some((p) => url.endsWith(p)) || url.includes('/api/portal/'));
}

apiClient.interceptors.request.use((config) => {
  const token = getToken();
  if (token && !isAnonymous(config.url)) {
    const headers = AxiosHeaders.from(config.headers);
    headers.set('Authorization', `Bearer ${token}`);
    config.headers = headers;
  }
  return config;
});

apiClient.interceptors.response.use(
  (response) => response,
  (error: unknown) => {
    // A 401 on an authenticated call means the token expired or was rejected: end the session.
    // 403 is a permission problem and is left to the caller.
    if (axios.isAxiosError(error) && error.response?.status === 401 && !isAnonymous(error.config?.url)) {
      if (getToken()) onUnauthorized();
    }
    return Promise.reject(error);
  },
);
