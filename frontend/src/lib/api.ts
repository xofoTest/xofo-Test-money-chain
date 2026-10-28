export interface ProfileDto {
  id: string;
  memberCode: string;
  fullName: string;
  email: string;
  phone: string | null;
  preferredLanguage: string;
  role: string;
  accountStatus: string;
  createdAt: string;
}

export interface AuthResponse {
  token: string;
  profile: ProfileDto;
}

export interface TreeDto {
  memberCode: string;
  name: string;
  status: string;
  parentCode: string | null;
  childCount: number;
  hasChildren: boolean;
  sponsorCode: string | null;
  sponsorName: string | null;
}

export interface MemberRowDto {
  memberCode: string;
  name: string;
  email: string;
  phone: string | null;
  status: string;
  sponsorCode: string;
  sponsorName: string;
  placementParentCode: string | null;
  placementParentName: string | null;
  preferredLanguage: string;
  joined: string;
  childCount: number;
}

export interface LedgerRowDto {
  id: string;
  memberCode: string;
  memberName: string;
  type: string;
  amountPaise: number;
  status: string;
  isSimulated: boolean;
  date: string;
  reference: string;
  reason: string;
}

export interface ActivityDto { text: string; meta: string; kind: string; }
export interface AuditRowDto { date: string; admin: string; action: string; target: string; reason: string | null; }

export interface DashboardDto {
  member: ProfileDto;
  stats: Record<string, unknown>;
  activity: ActivityDto[];
  sponsorName: string | null;
  sponsorCode: string | null;
  placementParentName: string | null;
  placementParentCode: string | null;
  registrationStatus: string;
  registrationReference: string;
}

export interface EarningsDto {
  totalPaise: number;
  pendingPaise: number;
  settledPaise: number;
  simulationEnabled: boolean;
  entries: LedgerRowDto[];
}

export interface AdminOverviewDto {
  totalMembers: number;
  activeMembers: number;
  suspendedMembers: number;
  pendingMembers: number;
  registrationRecords: number;
  treeNodes: number;
  fullNodes: number;
  openSlots: number;
  rewardsTotalPaise: number;
  rewardsPendingPaise: number;
  rewardsSettledPaise: number;
  simulationEnabled: boolean;
  recentActivity: AuditRowDto[];
}

export interface SettingsDto {
  defaultLanguage: string;
  registrationFeeInr: number;
  directMemberCountThreshold: number;
  directMemberRewardInr: number;
  childRewardInr: number;
  childRewardTrigger: string;
  rewardSimulationEnabled: boolean;
  livePaymentsEnabled: boolean;
  livePayoutsEnabled: boolean;
  maintenanceMode: boolean;
}

export interface SlotRowDto { memberCode: string; name: string; childCount: number; openSlots: number; status: string; }
export interface PlacementHistoryDto { date: string; member: string; change: string; by: string | null; reason: string | null; }
export interface EligibleParentDto { memberCode: string; name: string; childCount: number; openSlots: number; }

const TOKEN_KEY = 'mc-token';

export function getToken(): string | null {
  try { return localStorage.getItem(TOKEN_KEY); } catch { return null; }
}

export function setToken(token: string | null): void {
  try {
    if (token) localStorage.setItem(TOKEN_KEY, token);
    else localStorage.removeItem(TOKEN_KEY);
  } catch { /* ignore */ }
}

async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const token = getToken();
  const res = await fetch(path, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...(options.headers || {})
    }
  });
  if (res.status === 204) return undefined as T;
  if (!res.ok) {
    let msg = `Request failed (${res.status})`;
    try {
      const data = await res.json();
      if (data?.error) msg = data.error;
    } catch { /* ignore */ }
    throw new Error(msg);
  }
  return res.json() as Promise<T>;
}

export const api = {
  get: <T>(path: string) => request<T>(path),
  post: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: 'POST', body: body === undefined ? undefined : JSON.stringify(body) }),
  put: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: 'PUT', body: body === undefined ? undefined : JSON.stringify(body) })
};
