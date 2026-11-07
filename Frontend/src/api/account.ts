import { apiClient } from './client';
import type { AccountContext } from '../types/workspace';

export const accountApi = {
  /** GET /api/account/context – organization, accessible sites, roles and permissions of the caller. */
  async context(signal?: AbortSignal): Promise<AccountContext> {
    const { data } = await apiClient.get<AccountContext>('/api/account/context', { signal });
    return data;
  },
};
