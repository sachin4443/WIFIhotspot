import { createContext } from 'react';
import type { AccountContext, WorkspaceOrganization, WorkspaceSite } from '../types/workspace';

export interface WorkspaceValue {
  context: AccountContext | null;
  organization: WorkspaceOrganization | null;
  /** Sites this admin may access (empty for platform admins: they pick per organization). */
  sites: WorkspaceSite[];
  loading: boolean;
  error: string | null;
  reload: () => void;
}

export const WorkspaceContext = createContext<WorkspaceValue | null>(null);
