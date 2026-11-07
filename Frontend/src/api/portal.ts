import { apiClient } from './client';
import type { AuthenticationType } from '../types/models';

export interface PortalMethod { authenticationType: AuthenticationType; isDefault: boolean; requireOTP: boolean }
export interface PortalConfig {
  organizationName: string; organizationType: string; siteName: string; logoUrl: string | null; themeColor: string; welcomeMessage: string | null;
  backgroundUrl: string | null; termsAndConditions: string | null; privacyPolicy: string | null; methods: PortalMethod[];
  otpExpirySeconds: number; otpResendCooldownSeconds: number; smsDevelopmentMode: boolean;
}
export interface PortalOtp { challengeId: string; maskedMobile: string; expiresInSeconds: number; resendInSeconds: number; developmentMode: boolean }
export interface PortalLoginInput {
  method: AuthenticationType; mobileNumber?: string; identifier?: string; password?: string; roomNumber?: string;
  challengeId?: string; otp?: string; macAddress?: string; acceptTerms: boolean;
}
export interface PortalLoginResult {
  status: 'Authorized' | 'OtpRequired'; username: string | null; password: string | null; expiresAt: string | null;
  sessionTimeoutSeconds: number | null; challengeId: string | null; maskedMobile: string | null; message: string;
}

const base = (key: string) => `/api/portal/${encodeURIComponent(key)}`;

export const portalApi = {
  config: async (key: string, signal?: AbortSignal) => (await apiClient.get<PortalConfig>(`${base(key)}/config`, { signal })).data,
  requestOtp: async (key: string, body: { method: AuthenticationType; mobileNumber?: string; identifier?: string; roomNumber?: string; macAddress?: string }) =>
    (await apiClient.post<PortalOtp>(`${base(key)}/otp`, body)).data,
  login: async (key: string, body: PortalLoginInput) => (await apiClient.post<PortalLoginResult>(`${base(key)}/login`, body)).data,
};
