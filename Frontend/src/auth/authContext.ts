import { createContext } from 'react';
import type { AuthSession, LoginRequest } from '../types/auth';

export type AuthStatus = 'initializing' | 'authenticated' | 'anonymous';

export type LogoutReason = 'manual' | 'expired';

export interface AuthContextValue {
  status: AuthStatus;
  session: AuthSession | null;
  user: AuthSession['user'] | null;
  login(request: LoginRequest): Promise<void>;
  logout(reason?: LogoutReason): void;
  hasPermission(code: string): boolean;
  hasAnyPermission(codes: readonly string[]): boolean;
}

export const AuthContext = createContext<AuthContextValue | null>(null);
