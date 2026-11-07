import { useMemo, type ReactNode } from 'react';
import { accountApi } from '../api/account';
import { useAuth } from '../auth/useAuth';
import { useApi } from '../hooks/useApi';
import { WorkspaceContext, type WorkspaceValue } from './workspaceContext';

/** Loads the signed-in admin's organization and accessible sites once per session. */
export function WorkspaceProvider({ children }: { children: ReactNode }) {
  const { status, session } = useAuth();
  const enabled = status === 'authenticated';
  const { data, loading, error, reload } = useApi((signal) => accountApi.context(signal), [session?.accessToken], enabled);

  const value = useMemo<WorkspaceValue>(
    () => ({
      context: enabled ? data ?? null : null,
      organization: enabled ? data?.organization ?? null : null,
      sites: enabled ? data?.sites ?? [] : [],
      loading,
      error,
      reload,
    }),
    [enabled, data, loading, error, reload],
  );

  return <WorkspaceContext.Provider value={value}>{children}</WorkspaceContext.Provider>;
}
