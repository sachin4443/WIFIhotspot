import { apiClient } from './client';
import type { CurrentUser, LoginRequest, LoginResponse } from '../types/auth';

export const authApi = {
  /** POST /api/auth/login (anonymous, rate-limited to 10 requests/minute per IP). */
  async login(request: LoginRequest): Promise<LoginResponse> {
    const { data } = await apiClient.post<LoginResponse>('/api/auth/login', request);
    return data;
  },

  /** GET /api/auth/me – identity and permissions from the validated token. */
  async me(signal?: AbortSignal): Promise<CurrentUser> {
    const { data } = await apiClient.get<CurrentUser>('/api/auth/me', { signal });
    return data;
  },
};
