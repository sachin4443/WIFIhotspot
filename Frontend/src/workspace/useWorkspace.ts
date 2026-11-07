import { useContext } from 'react';
import { WorkspaceContext, type WorkspaceValue } from './workspaceContext';

export function useWorkspace(): WorkspaceValue {
  const ctx = useContext(WorkspaceContext);
  if (!ctx) throw new Error('useWorkspace must be used inside <WorkspaceProvider>.');
  return ctx;
}
