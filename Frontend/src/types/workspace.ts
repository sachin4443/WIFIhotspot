/** Mirrors AccountContextDto (GET /api/account/context). */
export interface WorkspaceSite {
  id: string;
  name: string;
  code: string;
  isPrimary: boolean;
  isActive: boolean;
}

export interface WorkspaceOrganization {
  id: string;
  name: string;
  organizationTypeId: number;
  organizationTypeName: string;
  status: string;
}

export interface AccountContext {
  adminUserId: string;
  username: string;
  fullName: string;
  isPlatformAdmin: boolean;
  hasAllSites: boolean;
  organization: WorkspaceOrganization | null;
  sites: WorkspaceSite[];
  roles: string[];
  permissions: string[];
}
