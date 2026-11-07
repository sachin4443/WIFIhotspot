/** Mirrors NxtHotspot.Application.Dtos.LoginRequest. */
export interface LoginRequest {
  username: string;
  password: string;
}

/** Mirrors NxtHotspot.Application.Dtos.LoginResponse (System.Text.Json camelCase). */
export interface LoginResponse {
  accessToken: string;
  expiresAtUtc: string;
  adminUserId: string;
  username: string;
  fullName: string;
  organizationId: string | null;
  isPlatformAdmin: boolean;
  roles: string[];
  permissions: string[];
}

/** Mirrors NxtHotspot.Application.Dtos.CurrentUserDto (GET /api/auth/me). */
export interface CurrentUser {
  adminUserId: string | null;
  organizationId: string | null;
  isPlatformAdmin: boolean;
  hasAllSites: boolean;
  permissions: string[];
}

/** What the frontend keeps for the signed-in admin. The token is the only credential. */
export interface AuthSession {
  accessToken: string;
  /** Epoch milliseconds. */
  expiresAt: number;
  user: {
    adminUserId: string;
    username: string;
    fullName: string;
    organizationId: string | null;
    isPlatformAdmin: boolean;
    roles: string[];
    permissions: string[];
  };
}
