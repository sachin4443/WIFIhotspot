import axios from 'axios';
import type { ProblemDetails } from '../types/api';

/** Turns any thrown value from an API call into a message that is safe to show to the user. */
export function getErrorMessage(error: unknown, fallback = 'Something went wrong. Please try again.'): string {
  if (!axios.isAxiosError(error)) {
    return error instanceof Error && error.message ? error.message : fallback;
  }

  if (error.code === 'ECONNABORTED') return 'The server took too long to respond. Please try again.';

  const response = error.response;
  if (!response) return 'Cannot reach the NXT Hotspot API. Check that the backend is running.';

  // The Vite dev proxy answers 502/503/504 when the backend is down.
  if (response.status === 502 || response.status === 503 || response.status === 504) {
    return 'The NXT Hotspot API is not available right now. Check that the backend is running.';
  }
  if (response.status === 429) return 'Too many attempts. Please wait a minute and try again.';

  const data = response.data as ProblemDetails | string | undefined;
  if (data && typeof data === 'object') {
    if (data.errors) {
      const messages = Object.values(data.errors).flat().filter(Boolean);
      if (messages.length > 0) return messages.join(' ');
    }
    if (data.title) return data.title;
    if (data.detail) return data.detail;
  }

  switch (response.status) {
    case 401:
      return 'Your session is not valid. Please sign in again.';
    case 403:
      return 'You do not have permission to perform this action.';
    case 404:
      return 'The requested resource was not found.';
    default:
      return response.status >= 500 ? 'The server encountered an error. Please try again later.' : fallback;
  }
}
