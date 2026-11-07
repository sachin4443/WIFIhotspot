import { apiClient } from './client';

export interface HealthResponse {
  status: string;
  service: string;
}

export const healthApi = {
  /** GET /health (anonymous). */
  async check(signal?: AbortSignal): Promise<HealthResponse> {
    const { data } = await apiClient.get<HealthResponse>('/health', { signal });
    return data;
  },
};
