const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5128";

function getToken(): string | null {
  if (typeof document === "undefined") return null;
  const m = document.cookie.match(/(?:^|; )sa_token=([^;]*)/);
  return m ? decodeURIComponent(m[1]) : null;
}

async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const token = getToken();
  const res = await fetch(`${API_URL}${path}`, {
    ...options,
    headers: {
      "Content-Type": "application/json",
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...options.headers,
    },
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({ message: res.statusText }));
    throw new Error(err.message ?? err.title ?? res.statusText);
  }
  if (res.status === 204) return undefined as T;
  return res.json();
}

// ── Types ──────────────────────────────────────────────────────

export interface SchoolFeatures {
  // Classroom pillar
  gradebook: boolean;
  virtualClassroom: boolean;
  // Reports & Insights pillar
  smartReports: boolean;
  saSamsExport: boolean;
  // Pathways pillar
  skillsProfile: boolean;
  pathways: boolean;
  matricHub: boolean;
  // Life at School pillar
  sportsCulture: boolean;
  schoolPay: boolean;
  // Connect pillar
  schoolChat: boolean;
  whatsApp: boolean;
  popiaCentre: boolean;
}

export interface SchoolSummary {
  schoolId: string;
  name: string;
  domain: string | null;
  isActive: boolean;
  features: SchoolFeatures;
  createdAt: string;
  userCount: number;
  classCount: number;
}

export interface PlatformStats {
  totalSchools: number;
  activeSchools: number;
  totalUsers: number;
  totalStudents: number;
  totalTeachers: number;
}

export interface SuperAdminUser {
  superAdminId: string;
  email: string;
  firstName: string;
  lastName: string;
}

export interface LoginResponse {
  accessToken: string;
  superAdmin: SuperAdminUser;
}

export interface CreateSchoolPayload {
  name: string;
  domain?: string;
  adminEmail: string;
  adminPassword: string;
  adminFirstName: string;
  adminLastName: string;
  features?: Partial<SchoolFeatures>;
}

export interface SuperAdminAuditLog {
  auditId: string;
  superAdminId: string;
  superAdminName: string;
  superAdminEmail: string;
  actionType: string;
  targetSchoolId: string | null;
  targetSchoolName: string | null;
  previousValue: string | null;   // raw JSON diff string
  newValue: string | null;        // raw JSON diff string
  reason: string | null;
  createdAt: string;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface AuditLogFilters {
  schoolId?: string;
  actionType?: string;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}

// ── API ────────────────────────────────────────────────────────

export const api = {
  auth: {
    login: (email: string, password: string) =>
      request<LoginResponse>("/api/super/auth/login", {
        method: "POST",
        body: JSON.stringify({ email, password }),
      }),
  },
  stats: () => request<PlatformStats>("/api/super/stats"),
  schools: {
    list: () => request<SchoolSummary[]>("/api/super/schools"),
    create: (body: CreateSchoolPayload) =>
      request<SchoolSummary>("/api/super/schools", {
        method: "POST",
        body: JSON.stringify(body),
      }),
    updateFeatures: (id: string, features: SchoolFeatures) =>
      request<SchoolSummary>(`/api/super/schools/${id}/features`, {
        method: "PUT",
        body: JSON.stringify(features),
      }),
    setStatus: (id: string, isActive: boolean, reason?: string) =>
      request<SchoolSummary>(`/api/super/schools/${id}/status`, {
        method: "PATCH",
        body: JSON.stringify({ isActive, reason }),
      }),
  },
  auditLog: {
    list: (filters: AuditLogFilters = {}) => {
      const q = new URLSearchParams();
      if (filters.schoolId) q.set("schoolId", filters.schoolId);
      if (filters.actionType) q.set("actionType", filters.actionType);
      if (filters.from) q.set("from", filters.from);
      if (filters.to) q.set("to", filters.to);
      q.set("page", String(filters.page ?? 1));
      q.set("pageSize", String(filters.pageSize ?? 50));
      return request<PagedResult<SuperAdminAuditLog>>(`/api/super/audit-log?${q.toString()}`);
    },
  },
};
