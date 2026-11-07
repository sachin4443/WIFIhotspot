import type { SubscriberType } from './hotspot';
import type { LabelCount } from './models';

export type SessionStatus = 'Active' | 'Stopped' | 'Stale';
export type DisconnectStatus = 'Pending' | 'Sent' | 'Acknowledged' | 'Rejected' | 'TimedOut' | 'Failed';
export type RadiusResult = 'Accept' | 'Reject' | 'Dropped' | 'Accounted' | 'Ack' | 'Nak' | 'Timeout';

export interface RadiusSessionRow {
  id: string; organizationId: string; organizationName: string | null; siteId: string; siteName: string; nasId: string; nasName: string;
  subscriberId: string | null; subscriberName: string | null; subscriberType: SubscriberType | null; username: string; acctSessionId: string;
  framedIpAddress: string | null; macAddress: string | null; startedAt: string; lastUpdateAt: string; stoppedAt: string | null;
  sessionTimeSeconds: number; inputBytes: number; outputBytes: number; terminateCause: string | null; status: SessionStatus;
  pendingDisconnect: DisconnectStatus | null;
}

export interface SessionQuery {
  organizationId?: string; siteId?: string; nasId?: string; subscriberId?: string; status?: SessionStatus | '';
  from?: string; to?: string; search?: string; page: number; pageSize: number;
}

export interface DisconnectCommand { id: string; sessionId: string; nasId: string; status: DisconnectStatus; attempts: number; result: string | null; createdAt: string; completedAt: string | null }

export interface UsagePoint { date: string; sessions: number; uniqueUsers: number; inputBytes: number; outputBytes: number; sessionSeconds: number }
export interface UsageRow { key: string; label: string; sessions: number; bytes: number; sessionSeconds: number }
export interface UsageReport {
  from: string; to: string; sessions: number; uniqueUsers: number; inputBytes: number; outputBytes: number; sessionSeconds: number;
  daily: UsagePoint[]; topSubscribers: UsageRow[]; bySite: UsageRow[]; byNas: UsageRow[]; bySubscriberType: UsageRow[];
}

export interface RadiusServer {
  instanceId: string; version: string; hostName: string | null; authPort: number; acctPort: number; startedAt: string; lastHeartbeatAt: string;
  isOnline: boolean; authRequests: number; authAccepts: number; authRejects: number; acctRequests: number; dropped: number;
}
export interface NasHealth { id: string; name: string; ipAddress: string; organizationName: string; siteName: string; isActive: boolean; lastSeenAt: string | null; activeSessions: number }
export interface RadiusOverview {
  anyServerOnline: boolean; servers: RadiusServer[]; activeSessions: number; requestsLast24h: number; acceptsLast24h: number; rejectsLast24h: number;
  droppedLast24h: number; topRejectReasons: LabelCount[]; nas: NasHealth[];
}
export interface RadiusRequestLog {
  id: number; organizationId: string | null; organizationName: string | null; nasId: string | null; nasName: string | null; sourceIp: string;
  packetKind: string; result: RadiusResult; username: string | null; macAddress: string | null; reason: string | null; createdAt: string;
}
