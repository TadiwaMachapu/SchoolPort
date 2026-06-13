const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5128";

function toQueryString(params?: Record<string, string | number | boolean | undefined | null>): string {
  if (!params) return "";
  const entries = Object.entries(params).filter(([, v]) => v !== undefined && v !== null && v !== "");
  return entries.length ? "?" + new URLSearchParams(entries.map(([k, v]) => [k, String(v)])) : "";
}

function getCookie(name: string): string | null {
  if (typeof document === "undefined") return null;
  const m = document.cookie.match(new RegExp(`(?:^|; )${name}=([^;]*)`));
  return m ? decodeURIComponent(m[1]) : null;
}

function setCookie(name: string, value: string, maxAge: number) {
  document.cookie = `${name}=${encodeURIComponent(value)}; path=/; max-age=${maxAge}; SameSite=Lax`;
}

function getToken(): string | null { return getCookie("sp_token"); }

let refreshing: Promise<string | null> | null = null;

async function refreshTokenOnce(): Promise<string | null> {
  if (refreshing) return refreshing;
  refreshing = (async () => {
    const rt = getCookie("sp_refresh_token");
    if (!rt) return null;
    try {
      const res = await fetch(`${API_URL}/api/auth/refresh`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ refreshToken: rt }),
      });
      if (!res.ok) return null;
      const data: { accessToken: string; refreshToken: string; expiresAt: string } = await res.json();
      const maxAge = Math.floor((new Date(data.expiresAt).getTime() - Date.now()) / 1000);
      setCookie("sp_token", data.accessToken, maxAge > 0 ? maxAge : 3600 * 8);
      setCookie("sp_refresh_token", data.refreshToken, 3600 * 24 * 30);
      return data.accessToken;
    } catch {
      return null;
    } finally {
      refreshing = null;
    }
  })();
  return refreshing;
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

  // Auto-refresh on 401 and retry once
  if (res.status === 401 && !path.includes("/auth/")) {
    const newToken = await refreshTokenOnce();
    if (newToken) {
      const retry = await fetch(`${API_URL}${path}`, {
        ...options,
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${newToken}`,
          ...options.headers,
        },
      });
      if (retry.ok) {
        if (retry.status === 204) return undefined as T;
        return retry.json();
      }
    }
    // Refresh failed — redirect to login
    if (typeof window !== "undefined") window.location.href = "/login";
    throw new Error("Session expired");
  }

  if (!res.ok) {
    const error = await res.json().catch(() => ({ message: res.statusText }));
    throw new Error(`[${res.status}] ${error.message ?? error.title ?? res.statusText} (${path})`);
  }

  if (res.status === 204) return undefined as T;
  return res.json();
}

// Auth
export const api = {
  auth: {
    login: (email: string, password: string) =>
      request<LoginResponse>("/api/auth/login", {
        method: "POST",
        body: JSON.stringify({ email, password }),
      }),
  },
  me: {
    get: () => request<MeResponse>("/api/me"),
  },
  schools: {
    current: () => request<{ name: string; domain?: string; theme: Record<string, unknown>; features: Record<string, boolean> }>("/api/schools/current"),
    updateInfo: (body: { name: string; domain?: string }) =>
      request<void>("/api/schools/info", { method: "PUT", body: JSON.stringify(body) }),
    updateTheme: (body: object) =>
      request<void>("/api/schools/theme", { method: "PUT", body: JSON.stringify(body) }),
    updateFeatures: (body: object) =>
      request<void>("/api/schools/features", { method: "PUT", body: JSON.stringify(body) }),
    getSettings: () =>
      request<SchoolSettings>("/api/schools/settings"),
    updateSettings: (body: SchoolSettings) =>
      request<void>("/api/schools/settings", { method: "PUT", body: JSON.stringify(body) }),
    seedCapsSubjects: () =>
      request<{ created: number; skipped: number }>("/api/schools/seed-caps-subjects", { method: "POST" }),
  },
  users: {
    list: (params?: { role?: string; q?: string; page?: number; pageSize?: number }) =>
      request<Paginated<User>>(`/api/users${toQueryString(params)}`),
    create: (body: CreateUserRequest) =>
      request<User>("/api/users", { method: "POST", body: JSON.stringify(body) }),
    update: (id: string, body: UpdateUserRequest) =>
      request<User>(`/api/users/${id}`, { method: "PUT", body: JSON.stringify(body) }),
    delete: (id: string) =>
      request<void>(`/api/users/${id}`, { method: "DELETE" }),
    directory: (q?: string) =>
      request<DirectoryUser[]>(`/api/users/directory${toQueryString({ q })}`),
    importCsv: (file: File) => {
      const form = new FormData();
      form.append("file", file);
      const token = typeof document !== "undefined"
        ? (() => { const m = document.cookie.match(/(?:^|; )sp_token=([^;]*)/); return m ? decodeURIComponent(m[1]) : null; })()
        : null;
      return fetch(`${API_URL}/api/users/import-csv`, {
        method: "POST",
        headers: token ? { Authorization: `Bearer ${token}` } : {},
        body: form,
      }).then(r => r.ok ? r.json() as Promise<ImportCsvResult> : r.json().then((e: { message?: string; title?: string }) => { throw new Error(e.message ?? e.title ?? r.statusText); }));
    },
  },
  subjects: {
    list: () => request<Subject[]>("/api/subjects"),
    get: (id: string) => request<Subject>(`/api/subjects/${id}`),
    create: (body: { name: string; code?: string; description?: string; capsPhase?: string }) =>
      request<Subject>("/api/subjects", { method: "POST", body: JSON.stringify(body) }),
    update: (id: string, body: { name: string; code?: string; description?: string; capsPhase?: string }) =>
      request<Subject>(`/api/subjects/${id}`, { method: "PUT", body: JSON.stringify(body) }),
    delete: (id: string) => request<void>(`/api/subjects/${id}`, { method: "DELETE" }),
  },
  assignments: {
    list: (params?: { page?: number; pageSize?: number }) =>
      request<Paginated<Assignment>>(`/api/assignments${toQueryString(params)}`),
    get: (id: string) => request<Assignment>(`/api/assignments/${id}`),
    create: (body: CreateAssignmentRequest) =>
      request<Assignment>("/api/assignments", { method: "POST", body: JSON.stringify(body) }),
  },
  attendance: {
    get: (classId: string, date: string) =>
      request<AttendanceRecord[]>(`/api/attendance?classId=${classId}&date=${new Date(date).toISOString()}`),
    bulkUpsert: (body: BulkAttendanceRequest) =>
      request<void>("/api/attendance/bulk", { method: "POST", body: JSON.stringify(body) }),
    mine: (month?: number, year?: number) =>
      request<MyAttendanceSummary[]>(`/api/attendance/mine${toQueryString({ month, year })}`),
  },
  quizzes: {
    list: (params?: { page?: number; pageSize?: number; teacherView?: boolean }) =>
      request<Paginated<Quiz>>(`/api/quizzes${toQueryString(params)}`),
    get: (id: string, teacherView = false) =>
      request<Quiz>(`/api/quizzes/${id}${teacherView ? "?teacherView=true" : ""}`),
    create: (body: CreateQuizRequest) =>
      request<Quiz>("/api/quizzes", { method: "POST", body: JSON.stringify(body) }),
    publish: (id: string, publish: boolean) =>
      request<Quiz>(`/api/quizzes/${id}/publish?publish=${publish}`, { method: "PUT" }),
    delete: (id: string) =>
      request<void>(`/api/quizzes/${id}`, { method: "DELETE" }),
    startAttempt: (quizId: string) =>
      request<QuizAttempt>(`/api/quizzes/${quizId}/attempts`, { method: "POST" }),
    submit: (attemptId: string, answers: { questionId: string; selectedOptionId?: string; textAnswer?: string }[]) =>
      request<QuizAttempt>(`/api/quizzes/attempts/${attemptId}/submit`, {
        method: "POST",
        body: JSON.stringify({ answers }),
      }),
    myAttempts: (quizId: string) =>
      request<QuizAttempt[]>(`/api/quizzes/${quizId}/attempts/mine`),
    allAttempts: (quizId: string) =>
      request<QuizAttempt[]>(`/api/quizzes/${quizId}/attempts`),
  },
  terms: {
    list: () => request<Term[]>("/api/terms"),
    current: () => request<Term>("/api/terms/current"),
  },
  gradebook: {
    myGrades: () => request<GradeEntry[]>("/api/gradebook/my-grades"),
    classGradebook: (classId: string, termId?: string) =>
      request<ClassGradebook>(`/api/gradebook/${classId}${termId ? `?termId=${termId}` : ""}`),
  },
  reports: {
    termReport: (classId: string, termId: string) =>
      request<TermReport>(`/api/reports/term-report/${classId}/${termId}`),
    atRisk: (classId: string, termId: string) =>
      request<SmartAtRiskStudent[]>(`/api/reports/at-risk?classId=${classId}&termId=${termId}`),
    reportComment: (studentId: string, termId: string, forceRefresh = false) =>
      request<{ available: boolean; commentText?: string; fromCache?: boolean }>(
        `/api/reports/comment?studentId=${studentId}&termId=${termId}&forceRefresh=${forceRefresh}`,
        { method: "POST" }
      ),
    principalSummary: (classId: string, termId: string, forceRefresh = false) =>
      request<{ available: boolean; summaryMarkdown?: string; fromCache?: boolean }>(
        `/api/reports/principal-summary?classId=${classId}&termId=${termId}&forceRefresh=${forceRefresh}`,
        { method: "POST" }
      ),
  },
  fees: {
    list: () => request<FeeItem[]>("/api/fees"),
    create: (body: { name: string; description?: string; amountZar: number; dueDate: string; termId?: string }) =>
      request<FeeItem>("/api/fees", { method: "POST", body: JSON.stringify(body) }),
    update: (id: string, body: { name: string; description?: string; amountZar: number; dueDate: string; termId?: string }) =>
      request<FeeItem>(`/api/fees/${id}`, { method: "PUT", body: JSON.stringify(body) }),
    delete: (id: string) => request<void>(`/api/fees/${id}`, { method: "DELETE" }),
    payments: (feeId: string) => request<FeePaymentItem[]>(`/api/fees/${feeId}/payments`),
    recordPayment: (feeId: string, body: { studentId: string; amountPaidZar: number; paidAt?: string; notes?: string }) =>
      request<void>(`/api/fees/${feeId}/payments`, { method: "POST", body: JSON.stringify(body) }),
    myStatement: () => request<FeeStatement[]>("/api/fees/my-statement"),
  },
  whatsapp: {
    settings: () => request<WhatsAppConfig>("/api/whatsapp/settings"),
    updateSettings: (body: WhatsAppConfig) => request<WhatsAppConfig>("/api/whatsapp/settings", { method: "PUT", body: JSON.stringify(body) }),
    log: (page = 1, pageSize = 50) => request<{ total: number; items: WhatsAppLogItem[] }>(`/api/whatsapp/log?page=${page}&pageSize=${pageSize}`),
    compose: (body: { recipientName: string; recipientPhone: string; message: string }) =>
      request<{ whatsAppLogId: string; status: string }>("/api/whatsapp/compose", { method: "POST", body: JSON.stringify(body) }),
    sendTest: (body: { recipientName: string; recipientPhone: string }) =>
      request<{ status: string; message: string }>("/api/whatsapp/test", { method: "POST", body: JSON.stringify(body) }),
    sendAbsenceReminders: (date?: string) =>
      request<{ queued: number; status: string }>(`/api/whatsapp/parents/absence-reminders${date ? `?date=${date}` : ""}`, { method: "POST" }),
  },
  popia: {
    myConsents: () => request<ConsentRecord>("/api/popia/consents/mine"),
    updateConsents: (body: ConsentUpdate) => request<ConsentRecord>("/api/popia/consents", { method: "PUT", body: JSON.stringify(body) }),
    myRequests: () => request<DataSubjectRequest[]>("/api/popia/requests/mine"),
    submitRequest: (body: { requestType: string; description?: string }) =>
      request<{ requestId: string }>("/api/popia/requests", { method: "POST", body: JSON.stringify(body) }),
    adminConsents: () => request<AdminConsentRow[]>("/api/popia/admin/consents"),
    adminRequests: (status?: string) => request<AdminRequestRow[]>(`/api/popia/admin/requests${status ? `?status=${status}` : ""}`),
    adminUpdateRequest: (id: string, body: { status: string; adminNotes?: string }) =>
      request<void>(`/api/popia/admin/requests/${id}`, { method: "PUT", body: JSON.stringify(body) }),
  },
  sasams: {
    exportLearners: (termId?: string) => `${process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5128"}/api/sasams/export/learners${termId ? `?termId=${termId}` : ""}`,
    exportAttendance: (termId?: string) => `${process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5128"}/api/sasams/export/attendance${termId ? `?termId=${termId}` : ""}`,
    exportResults: (termId?: string) => `${process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5128"}/api/sasams/export/results${termId ? `?termId=${termId}` : ""}`,
  },
  matric: {
    dashboard: (classId?: string) => request<MatricDashboard>(`/api/matric/dashboard${classId ? `?classId=${classId}` : ""}`),
    mine: () => request<MatricStudentResult>("/api/matric/mine"),
    subjects: () => request<string[]>("/api/matric/subjects"),
    pastPapers: (subject?: string) => request<MatricPastPaper[]>(`/api/matric/past-papers${subject ? `?subject=${encodeURIComponent(subject)}` : ""}`),
    quiz: (subject: string, count = 10) => request<MatricQuizQuestion[]>(`/api/matric/quiz?subject=${encodeURIComponent(subject)}&count=${count}`),
    tutor: (subject: string, question: string, forceRefresh = false) =>
      request<TutorResult>(`/api/matric/tutor${forceRefresh ? "?forceRefresh=true" : ""}`, {
        method: "POST",
        body: JSON.stringify({ subject, question }),
      }),
  },
  skills: {
    mine: () => request<SkillEntry[]>("/api/skills/mine"),
    learnerSkills: (userId: string) => request<SkillEntry[]>(`/api/skills/learner/${userId}`),
    create: (body: { title: string; category: string; description?: string; date: string }) =>
      request<{ skillEntryId: string }>("/api/skills", { method: "POST", body: JSON.stringify(body) }),
    delete: (id: string) => request<void>(`/api/skills/${id}`, { method: "DELETE" }),
    endorse: (id: string) => request<void>(`/api/skills/${id}/endorse`, { method: "POST" }),
  },
  activities: {
    list: () => request<ActivityItem[]>("/api/activities"),
    mine: () => request<MyActivityItem[]>("/api/activities/mine"),
    create: (body: { name: string; description?: string; activityType: string; date: string }) =>
      request<{ activityId: string }>("/api/activities", { method: "POST", body: JSON.stringify(body) }),
    update: (id: string, body: { name: string; description?: string; activityType: string; date: string }) =>
      request<void>(`/api/activities/${id}`, { method: "PUT", body: JSON.stringify(body) }),
    delete: (id: string) => request<void>(`/api/activities/${id}`, { method: "DELETE" }),
    participants: (id: string) => request<ActivityParticipantItem[]>(`/api/activities/${id}/participants`),
    addParticipant: (id: string, body: { userId: string; notes?: string }) =>
      request<{ activityParticipantId: string }>(`/api/activities/${id}/participants`, { method: "POST", body: JSON.stringify(body) }),
    removeParticipant: (activityId: string, participantId: string) =>
      request<void>(`/api/activities/${activityId}/participants/${participantId}`, { method: "DELETE" }),
  },
  pathways: {
    mySubjects: () => request<LearnerSubjectItem[]>("/api/pathways/mine"),
    learnerSubjects: (studentId: string) => request<LearnerSubjectItem[]>(`/api/pathways/learner/${studentId}`),
    classMatrix: (classId: string) => request<PathwaysMatrix>(`/api/pathways/class/${classId}`),
    enrol: (body: { studentId: string; subjectId: string; academicYearId: string }) =>
      request<{ learnerSubjectId: string }>("/api/pathways/enrol", { method: "POST", body: JSON.stringify(body) }),
    withdraw: (learnerSubjectId: string) =>
      request<void>(`/api/pathways/${learnerSubjectId}`, { method: "DELETE" }),
    // v1 career goals
    universities: () => request<UniversitySummary[]>("/api/pathways/universities"),
    universityCourses: (universityId: string) => request<UniversityCourseDetail[]>(`/api/pathways/universities/${universityId}/courses`),
    careers: () => request<CareerItem[]>("/api/pathways/careers"),
    myAps: () => request<LearnerApsResult>("/api/pathways/aps"),
    myGoals: () => request<GoalWithTracking[]>("/api/pathways/goals"),
    addGoal: (universityCourseId: string) =>
      request<GoalWithTracking>("/api/pathways/goals", { method: "POST", body: JSON.stringify({ universityCourseId }) }),
    deleteGoal: (goalId: string) => request<void>(`/api/pathways/goals/${goalId}`, { method: "DELETE" }),
    goalTracking: (goalId: string) => request<GoalTracking>(`/api/pathways/goals/${goalId}/tracking`),
    gapAnalysis: (goalId: string, forceRefresh = false) =>
      request<{ available: boolean; analysis?: GapAnalysisResult }>(`/api/pathways/goals/${goalId}/gap-analysis?forceRefresh=${forceRefresh}`, { method: "POST" }),
    gr9Profile: () => request<Gr9Profile>("/api/pathways/gr9-profile"),
    gr9Advice: (forceRefresh = false) =>
      request<{ available: boolean; advice?: Gr9AiAdvice }>(`/api/pathways/gr9-advice?forceRefresh=${forceRefresh}`, { method: "POST" }),
  },
  courses: {
    list: (params?: { page?: number; pageSize?: number; publishedOnly?: boolean }) =>
      request<Paginated<Course>>(`/api/courses${toQueryString(params)}`),
    get: (id: string) => request<Course>(`/api/courses/${id}`),
    create: (body: { title: string; description?: string; thumbnailUrl?: string; classSubjectId?: string }) =>
      request<Course>("/api/courses", { method: "POST", body: JSON.stringify(body) }),
    publish: (id: string, publish: boolean) =>
      request<Course>(`/api/courses/${id}/publish?publish=${publish}`, { method: "PUT" }),
    delete: (id: string) =>
      request<void>(`/api/courses/${id}`, { method: "DELETE" }),
    addModule: (courseId: string, body: { title: string; description?: string; order: number }) =>
      request<CourseModule>(`/api/courses/${courseId}/modules`, { method: "POST", body: JSON.stringify(body) }),
    deleteModule: (moduleId: string) =>
      request<void>(`/api/courses/modules/${moduleId}`, { method: "DELETE" }),
    addLesson: (moduleId: string, body: CreateLessonRequest) =>
      request<Lesson>(`/api/courses/modules/${moduleId}/lessons`, { method: "POST", body: JSON.stringify(body) }),
    updateLesson: (lessonId: string, body: CreateLessonRequest) =>
      request<Lesson>(`/api/courses/lessons/${lessonId}`, { method: "PUT", body: JSON.stringify(body) }),
    deleteLesson: (lessonId: string) =>
      request<void>(`/api/courses/lessons/${lessonId}`, { method: "DELETE" }),
  },
  announcements: {
    list: (params?: { page?: number; pageSize?: number }) =>
      request<Paginated<Announcement>>(`/api/announcements${toQueryString(params)}`),
    create: (body: CreateAnnouncementRequest) =>
      request<Announcement>("/api/announcements", { method: "POST", body: JSON.stringify(body) }),
    delete: (id: string) =>
      request<void>(`/api/announcements/${id}`, { method: "DELETE" }),
  },
  analytics: {
    overview: () => request<AnalyticsOverview>("/api/analytics/overview"),
    gradeDistribution: () => request<GradeDistribution>("/api/analytics/grade-distribution"),
    atRiskStudents: () => request<AtRiskStudent[]>("/api/analytics/at-risk-students"),
    classPerformance: () => request<ClassPerformance[]>("/api/analytics/class-performance"),
  },
  calendar: {
    events: (params?: { from?: string; to?: string; classId?: string }) =>
      request<{ events: CalendarEvent[]; assignmentDueDates: CalendarEvent[] }>(`/api/calendar/events${toQueryString(params)}`),
    create: (body: CreateCalendarEventRequest) =>
      request<CalendarEvent>("/api/calendar/events", { method: "POST", body: JSON.stringify(body) }),
    delete: (id: string) =>
      request<void>(`/api/calendar/events/${id}`, { method: "DELETE" }),
    timetable: (classId?: string) =>
      request<TimetableSlot[]>(`/api/calendar/timetable${toQueryString({ classId })}`),
  },
  messages: {
    threads: () => request<MessageThread[]>("/api/messages/threads"),
    getMessages: (threadId: string) => request<ChatMessage[]>(`/api/messages/threads/${threadId}`),
    createDirect: (recipientUserId: string, subject?: string) =>
      request<{ threadId: string }>("/api/messages/threads/direct", {
        method: "POST",
        body: JSON.stringify({ recipientUserId, subject }),
      }),
    sendMessage: (threadId: string, content: string) =>
      request<ChatMessage>(`/api/messages/threads/${threadId}/messages`, {
        method: "POST",
        body: JSON.stringify({ content }),
      }),
  },
  submissions: {
    pending: (limit = 10) => request<PendingSubmission[]>(`/api/submissions/pending?limit=${limit}`),
    submit: (assignmentId: string, body: { content?: string }) => {
      const form = new FormData();
      form.append("assignmentId", assignmentId);
      if (body.content) form.append("comments", body.content);
      const token = typeof document !== "undefined"
        ? (() => { const m = document.cookie.match(/(?:^|; )sp_token=([^;]*)/); return m ? decodeURIComponent(m[1]) : null; })()
        : null;
      return fetch(`${process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5128"}/api/submissions`, {
        method: "POST",
        headers: token ? { Authorization: `Bearer ${token}` } : {},
        body: form,
      }).then(r => r.ok ? r.json() : r.json().then(e => { throw new Error(e.message ?? r.statusText); }));
    },
    list: (assignmentId: string) =>
      request<Submission[]>(`/api/submissions/by-assignment/${assignmentId}`),
    grade: (submissionId: string, body: { score: number; feedback?: string }) =>
      request<void>("/api/grades", {
        method: "POST",
        body: JSON.stringify({ submissionId, score: body.score, feedback: body.feedback }),
      }),
    aiGrade: (submissionId: string) =>
      request<{ suggestedScore: number; feedback: string }>(`/api/ai/grade-suggestion/${submissionId}`, {
        method: "POST",
      }),
    mySubmission: (assignmentId: string) =>
      request<Submission | null>(`/api/submissions/by-assignment/${assignmentId}/mine`),
  },
  notifications: {
    list: (limit = 30) =>
      request<NotificationsResponse>(`/api/notifications?limit=${limit}`),
    markRead: (id: string) =>
      request<void>(`/api/notifications/${id}/read`, { method: "PUT" }),
    markAllRead: () =>
      request<void>("/api/notifications/read-all", { method: "PUT" }),
  },
  parent: {
    children: () => request<ParentChild[]>("/api/parent/children"),
    grades: (studentId: string) => request<ParentGrade[]>(`/api/parent/children/${studentId}/grades`),
    attendance: (studentId: string, params?: { month?: number; year?: number }) =>
      request<ParentAttendanceSummary>(`/api/parent/children/${studentId}/attendance${toQueryString(params)}`),
    assignments: (studentId: string) => request<ParentAssignment[]>(`/api/parent/children/${studentId}/assignments`),
    pathways: () => request<ParentPathways>("/api/parent/pathways"),
  },
  classes: {
    list: (params?: { year?: number; q?: string; mine?: boolean; page?: number; pageSize?: number }) =>
      request<Paginated<Class>>(`/api/classes${toQueryString(params)}`),
    get: (id: string) => request<Class>(`/api/classes/${id}`),
    create: (body: CreateClassRequest) =>
      request<Class>("/api/classes", { method: "POST", body: JSON.stringify(body) }),
    update: (id: string, body: CreateClassRequest) =>
      request<Class>(`/api/classes/${id}`, { method: "PUT", body: JSON.stringify(body) }),
    delete: (id: string) =>
      request<void>(`/api/classes/${id}`, { method: "DELETE" }),
    students: (id: string) => request<User[]>(`/api/classes/${id}/students`),
    subjects: (id: string) => request<ClassSubject[]>(`/api/classes/${id}/subjects`),
  },
};

// Types
export interface GradeScaleEntry { letter: string; minPercent: number; maxPercent: number; }
export interface AcademicTerm { termId?: string; name: string; startDate: string; endDate: string; isCurrent?: boolean; }
export interface LatePolicy {
  acceptLate: boolean;
  gracePeriodHours: number;
  penaltyPercentPerDay: number;
  maxPenaltyPercent: number;
  blockAfterMaxPenalty: boolean;
}
export interface StudentIdConfig { prefix: string; nextNumber: number; paddingDigits: number; includeYear: boolean; }
export interface SchoolSettings {
  gradingScale: GradeScaleEntry[];
  academicTerms: AcademicTerm[];
  latePolicy: LatePolicy;
  studentIdConfig: StudentIdConfig;
  timezone: string;
  locale: string;
}

export interface DirectoryUser {
  userId: string;
  firstName: string;
  lastName: string;
  role: string;
  email: string;
}

export interface ImportCsvResult {
  created: number;
  failed: { row: number; reason: string }[];
}

export interface LoginResponse {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  user: UserInfo;
}
export interface UserInfo {
  userId: string;
  schoolId: string;
  email: string;
  firstName: string;
  lastName: string;
  role: string;
  identity: string;
}
export interface MeResponse {
  user: { userId: string; email: string; firstName: string; lastName: string; role: string };
  school: { schoolId: string; name: string; logoUrl?: string; primaryColor?: string };
}
export interface Paginated<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}
export interface User {
  userId: string;
  schoolId: string;
  email: string;
  firstName: string;
  lastName: string;
  role: string;
  isActive: boolean;
  createdAt: string;
  lastLoginAt?: string;
}
export interface Class {
  classId: string;
  name: string;
  gradeLevel?: number;
  academicYear?: number;
  teacherId?: string;
  teacherName?: string;
  maxCapacity?: number;
  studentCount: number;
  enrollmentCount: number;
}
export interface Subject {
  subjectId: string;
  name: string;
  code?: string;
  description?: string;
  capsPhase?: string;
}
export interface Term {
  termId: string;
  termNumber: number;
  startDate: string;
  endDate: string;
  isCurrent: boolean;
  year: number;
}
export interface FeeItem {
  feeId: string;
  name: string;
  description?: string;
  amountZar: number;
  dueDate: string;
  termLabel?: string;
  totalCollected: number;
  paymentCount: number;
}
export interface FeePaymentItem {
  feePaymentId: string;
  feeId: string;
  amountPaidZar: number;
  paidAt: string;
  notes?: string;
  studentName: string;
  studentNumber: string;
  recordedBy: string;
}
export interface FeeStatement {
  feeId: string;
  name: string;
  description?: string;
  amountZar: number;
  dueDate: string;
  amountPaid: number;
  balance: number;
  isPaid: boolean;
}
export interface WhatsAppConfig {
  provider: string;
  apiKey?: string;
  phoneNumberId?: string;
  triggerAbsence: boolean;
  triggerFeeReminder: boolean;
  triggerAnnouncement: boolean;
  absenceTemplate: string;
  feeReminderTemplate: string;
  announcementTemplate: string;
}
export interface WhatsAppLogItem {
  whatsAppLogId: string;
  recipientName: string;
  recipientPhone: string;
  triggerType: string;
  messageBody: string;
  status: string;
  errorMessage?: string;
  createdAt: string;
}
export interface ConsentRecord {
  consentRecordId?: string;
  dataProcessing: boolean;
  marketingCommunications: boolean;
  thirdPartySharing: boolean;
  photography: boolean;
  updatedAt?: string;
}
export interface ConsentUpdate {
  dataProcessing: boolean;
  marketingCommunications: boolean;
  thirdPartySharing: boolean;
  photography: boolean;
}
export interface DataSubjectRequest {
  requestId: string;
  requestType: string;
  description?: string;
  status: string;
  adminNotes?: string;
  createdAt: string;
  resolvedAt?: string;
}
export interface AdminConsentRow {
  consentRecordId: string;
  userId: string;
  name: string;
  email: string;
  role: string;
  dataProcessing: boolean;
  marketingCommunications: boolean;
  thirdPartySharing: boolean;
  photography: boolean;
  updatedAt: string;
}
export interface AdminRequestRow {
  requestId: string;
  userId: string;
  name: string;
  email: string;
  requestType: string;
  description?: string;
  status: string;
  adminNotes?: string;
  createdAt: string;
  resolvedAt?: string;
}
export interface MatricSubjectResult {
  subjectName: string;
  capsPhase?: string;
  average: number;
  status: "Pass" | "AtRisk" | "Fail";
}
export interface MatricLearnerRow {
  studentId: string;
  name: string;
  studentNumber: string;
  className: string;
  subjects: MatricSubjectResult[];
  passCount: number;
  atRiskCount: number;
  failCount: number;
  overallStatus: "Pass" | "AtRisk" | "Fail" | "NoData";
}
export interface MatricDashboard {
  classes: { classId: string; name: string }[];
  learners: MatricLearnerRow[];
}
export interface MatricStudentResult {
  isGrade12: boolean;
  subjects: MatricSubjectResult[];
  passCount: number;
  atRiskCount: number;
  failCount: number;
  overallStatus: "Pass" | "AtRisk" | "Fail" | "NoData";
}
export interface SkillEntry {
  skillEntryId: string;
  title: string;
  category: string;
  description?: string;
  date: string;
  endorsedByUserId?: string;
  endorsedAt?: string;
  endorsedByName?: string;
  createdAt: string;
}
export interface ActivityItem {
  activityId: string;
  name: string;
  description?: string;
  activityType: string;
  date: string;
  createdAt: string;
  participantCount: number;
}
export interface MyActivityItem {
  activityParticipantId: string;
  activityId: string;
  name: string;
  description?: string;
  activityType: string;
  date: string;
  notes?: string;
  createdAt: string;
}
export interface ActivityParticipantItem {
  activityParticipantId: string;
  studentId: string;
  name: string;
  studentNumber: string;
  notes?: string;
  createdAt: string;
}
export interface LearnerSubjectItem {
  learnerSubjectId: string;
  subjectId: string;
  subjectName: string;
  subjectCode?: string;
  capsPhase?: string;
  year: number;
  enrolledAt: string;
}
export interface PathwaysMatrix {
  academicYearId: string;
  year: number;
  students: { studentId: string; name: string; studentNumber: string; enrolledSubjectIds: string[] }[];
  subjects: { subjectId: string; subjectName: string; capsPhase?: string }[];
}
export interface TermReport {
  classId: string;
  className: string;
  termId: string;
  termNumber: number;
  year: number;
  startDate: string;
  endDate: string;
  students: {
    studentId: string;
    name: string;
    studentNumber: string;
    subjectResults: {
      subjectName: string;
      capsPhase?: string;
      average: number;
      capsLevel?: number;
      assignmentCount: number;
    }[];
    overallAverage?: number;
    attendancePercent?: number;
    daysAbsent: number;
  }[];
}
export interface Assignment {
  assignmentId: string;
  title: string;
  description?: string;
  dueAt: string;
  maxMarks: number;
  subjectName?: string;
  className?: string;
}
export interface AttendanceRecord {
  attendanceId: string;
  studentId: string;
  studentName: string;
  studentNumber: string;
  date: string;
  status: number;
  notes?: string;
}
export interface Announcement {
  announcementId: string;
  title: string;
  content: string;
  audience: string;
  createdByName: string;
  createdAt: string;
  expiresAt?: string;
  isActive: boolean;
}
export interface CreateUserRequest {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
  role: string;
}
export interface UpdateUserRequest {
  firstName: string;
  lastName: string;
  role: string;
  phoneNumber?: string;
  isActive: boolean;
}
export interface CreateClassRequest {
  name: string;
  gradeLevel?: number;
  academicYear?: number;
  teacherId?: string;
  maxCapacity?: number;
}
export interface CreateAssignmentRequest {
  classSubjectId: string;
  title: string;
  description?: string;
  dueAt: string;
  maxMarks: number;
}
export interface BulkAttendanceRequest {
  attendances: { classId: string; studentId: string; date: string; status: number; notes?: string }[];
}
export interface MyAttendanceSummary {
  classId: string;
  className: string;
  totalDays: number;
  present: number;
  absent: number;
  late: number;
  attendanceRate: number;
  records: { date: string; status: number; notes?: string }[];
}
export interface CreateQuizRequest {
  title: string;
  description?: string;
  timeLimitMinutes?: number;
  maxAttempts: number;
  shuffleQuestions: boolean;
  showResultsImmediately: boolean;
  classSubjectId?: string;
  questions: CreateQuizQuestionRequest[];
}
export interface CreateQuizQuestionRequest {
  text: string;
  type: string;
  order: number;
  marks: number;
  explanation?: string;
  options: { text: string; isCorrect: boolean; order: number }[];
}
export interface Quiz {
  quizId: string;
  title: string;
  description?: string;
  timeLimitMinutes?: number;
  maxAttempts: number;
  shuffleQuestions: boolean;
  showResultsImmediately: boolean;
  isPublished: boolean;
  createdByName: string;
  createdAt: string;
  questionCount: number;
  questions: QuizQuestion[];
}
export interface QuizQuestion {
  questionId: string;
  text: string;
  type: string;
  order: number;
  marks: number;
  explanation?: string;
  options: QuizOption[];
}
export interface QuizOption {
  optionId: string;
  text: string;
  isCorrect: boolean;
  order: number;
}
export interface QuizAttempt {
  attemptId: string;
  quizId: string;
  quizTitle: string;
  startedAt: string;
  submittedAt?: string;
  score?: number;
  maxScore?: number;
  isCompleted: boolean;
  percentage?: number;
  answers?: QuizAnswer[];
}
export interface QuizAnswer {
  questionId: string;
  selectedOptionId?: string;
  textAnswer?: string;
  isCorrect?: boolean;
  marksAwarded?: number;
}
export interface GradeEntry {
  gradeId: string;
  score: number;
  maxMarks: number;
  percentage: number;
  assignmentTitle: string;
  subject: string;
  class: string;
  feedback?: string;
  gradedAt: string;
}
export interface ClassGradebook {
  students: {
    studentId: string;
    name: string;
    studentNumber: string;
    grades: { assignmentId: string; score?: number; maxMarks: number; percentage?: number }[];
    average?: number;
  }[];
  assignments: { assignmentId: string; title: string; maxMarks: number; subject: string }[];
  categories: { name: string; weight: number }[];
}
export interface Course {
  courseId: string;
  title: string;
  description?: string;
  thumbnailUrl?: string;
  isPublished: boolean;
  createdByName: string;
  createdAt: string;
  moduleCount: number;
  lessonCount: number;
  modules: CourseModule[];
}
export interface CourseModule {
  moduleId: string;
  title: string;
  description?: string;
  order: number;
  lessons: Lesson[];
}
export interface Lesson {
  lessonId: string;
  title: string;
  type: string;
  content?: string;
  videoUrl?: string;
  fileUrl?: string;
  externalUrl?: string;
  order: number;
  durationMinutes?: number;
  isPublished: boolean;
}
export interface CreateLessonRequest {
  title: string;
  type: string;
  content?: string;
  videoUrl?: string;
  fileUrl?: string;
  externalUrl?: string;
  order: number;
  durationMinutes?: number;
  isPublished: boolean;
}
export interface CreateAnnouncementRequest {
  title: string;
  content: string;
  audience: string;
  audienceValue?: string;
  expiresAt?: string;
}
export interface AnalyticsOverview {
  totalStudents: number;
  totalTeachers: number;
  totalClasses: number;
  totalCourses: number;
  totalAssignments: number;
  pendingSubmissions: number;
  attendanceRateThisMonth: number;
}
export interface GradeDistribution {
  aPlus: number; a: number; b: number; c: number; d: number; f: number;
  average: number; total: number;
}
export interface AtRiskStudent {
  studentId: string;
  name: string;
  studentNumber: string;
  attendanceRate: number;
  risk: string;
}
export interface ClassPerformance {
  classId: string;
  name: string;
  studentCount: number;
  averageGrade?: number;
}
export interface CalendarEvent {
  eventId: string;
  title: string;
  description?: string;
  type: string;
  startAt: string;
  endAt?: string;
  allDay: boolean;
  classId?: string;
  className?: string;
}
export interface CreateCalendarEventRequest {
  title: string;
  description?: string;
  type: string;
  startAt: string;
  endAt?: string;
  allDay?: boolean;
  classId?: string;
}
export interface TimetableSlot {
  slotId: string;
  classSubjectId?: string;
  subjectName?: string;
  className?: string;
  subject?: string;
  class?: string;
  teacher?: string;
  dayOfWeek: number;
  startTime: string;
  endTime: string;
  room?: string;
}
export interface MessageThread {
  threadId: string;
  subject?: string;
  type: string;
  classId?: string;
  className?: string;
  lastMessageAt?: string;
  participants: ThreadParticipant[];
  messages?: ChatMessage[];
  unreadCount?: number;
}
export interface ThreadParticipant {
  userId: string;
  name: string;
  role: string;
}
export interface ChatMessage {
  messageId: string;
  threadId: string;
  senderUserId: string;
  senderName: string;
  content: string;
  sentAt: string;
  isDeleted: boolean;
}
export interface CreateThreadRequest {
  subject?: string;
  type: string;
  classId?: string;
  participantIds: string[];
  initialMessage?: string;
}
export interface Submission {
  submissionId: string;
  assignmentId: string;
  studentId: string;
  studentName: string;
  studentNumber?: string;
  comments?: string;
  content?: string;
  fileUrl?: string;
  fileName?: string;
  submittedAt: string;
  grade?: {
    gradeId: string;
    score: number;
    feedback?: string;
    gradedAt?: string;
  } | null;
  status?: string;
}
export interface PendingSubmission {
  submissionId: string;
  assignmentId: string;
  assignmentTitle: string;
  maxMarks: number;
  studentName: string;
  className: string;
  subjectName: string;
  submittedAt: string;
  hasFile: boolean;
}
export interface ClassSubject {
  classSubjectId: string;
  subjectId: string;
  subjectName: string;
  teacherId?: string;
  teacherName?: string;
}
export interface ParentChild {
  studentId: string;
  studentNumber: string;
  gradeLevel?: number;
  name: string;
  email: string;
}
export interface ParentGrade {
  gradeId: string;
  score: number;
  maxMarks: number;
  percentage: number;
  assignmentTitle: string;
  subject: string;
  class: string;
  feedback?: string;
  gradedAt: string;
}
export interface ParentAttendanceSummary {
  total: number;
  present: number;
  absent: number;
  late: number;
  attendanceRate: number;
  records: {
    attendanceId: string;
    date: string;
    status: number;
    statusText: string;
    className: string;
    notes?: string;
  }[];
}
export interface NotificationItem {
  notificationId: string;
  type: string;
  title: string;
  message: string;
  link?: string;
  isRead: boolean;
  createdAt: string;
}

export interface NotificationsResponse {
  items: NotificationItem[];
  unreadCount: number;
}

export interface ParentAssignment {
  assignmentId: string;
  title: string;
  dueAt: string;
  maxMarks: number;
  subject: string;
  class: string;
  isSubmitted: boolean;
  isOverdue: boolean;
}

// ── Pathways v1 types ──────────────────────────────────────────────────────────

export interface UniversitySummary {
  universityId: string;
  name: string;
  abbreviation: string;
  province: string;
  website?: string;
  courseCount: number;
}

export interface SubjectRequirementItem {
  subjectName: string;
  minimumPercent?: number;
  notes?: string;
}

export interface UniversityCourseDetail {
  universityCourseId: string;
  name: string;
  faculty?: string;
  minimumAps: number;
  apsNotes?: string;
  careerName?: string;
  careerCategory?: string;
  subjectRequirements: SubjectRequirementItem[];
}

export interface CareerItem {
  careerId: string;
  name: string;
  description?: string;
  category?: string;
  courseCount: number;
}

export interface SubjectScore {
  subjectId: string;
  subjectName: string;
  capsPhase?: string;
  averagePercent?: number;
  apsPoints?: number;
}

export interface LearnerApsResult {
  standardAps: number;
  totalAps: number;
  subjectScores: SubjectScore[];
}

export interface GoalWithTracking {
  learnerCareerGoalId: string;
  universityCourseId: string;
  courseName: string;
  universityName: string;
  universityAbbreviation: string;
  faculty?: string;
  minimumAps: number;
  status: "Green" | "Amber" | "Red";
  currentAps: number;
  priority: number;
}

export interface SubjectGap {
  subjectName: string;
  currentPercent?: number;
  requiredPercent: number;
  gapPercent: number;
  met: boolean;
}

export interface GoalTracking {
  learnerCareerGoalId: string;
  universityCourseId: string;
  courseName: string;
  universityName: string;
  universityAbbreviation: string;
  faculty?: string;
  minimumAps: number;
  apsNotes?: string;
  currentAps: number;
  apsGap: number;
  status: "Green" | "Amber" | "Red";
  subjectGaps: SubjectGap[];
}

export interface GapAnalysisSubjectGap {
  subject: string;
  currentPercent?: number;
  requiredPercent: number;
  advice: string;
}

export interface GapAnalysisResult {
  summary: string;
  currentAps: number;
  requiredAps: number;
  apsGap: number;
  subjectGaps: GapAnalysisSubjectGap[];
  overallAdvice: string;
  studySuggestions: string[];
  fromCache: boolean;
}

export interface ParentPathways {
  studentId: string;
  studentName: string;
  currentAps: number;
  goals: GoalWithTracking[];
}

// Matric Hub v1
export interface MatricPastPaper {
  matricPastPaperId: string;
  subject: string;
  year: number;
  paperNumber: number;
  language: string;
  url: string;
  hasMemo: boolean;
  memoUrl?: string;
  notes?: string;
}

export interface MatricQuizQuestion {
  matricQuizQuestionId: string;
  subject: string;
  difficulty: string;
  questionText: string;
  optionA: string;
  optionB: string;
  optionC: string;
  optionD: string;
}

export interface TutorResult {
  available: boolean;
  answer?: string;
  fromCache?: boolean;
}

// Smart Reports v1
export interface SmartSubjectResult {
  subjectName: string;
  average: number;
}

export interface SmartAtRiskStudent {
  studentId: string;
  name: string;
  studentNumber: string;
  overallAverage?: number;
  attendancePercent?: number;
  riskFlags: string[];
  subjectResults: SmartSubjectResult[];
}

// Grade 9 Subject Advisor
export interface Gr9SubjectMark {
  subjectName: string;
  averagePercent: number;
}

export interface FetSubjectEligibility {
  fetSubject: string;
  eligibility: "Recommended" | "Borderline" | "NotRecommended" | "NoData";
  gr9Subject?: string;
  recommendedMin?: number;
  studentPercent?: number;
  careerPaths: string[];
}

export interface Gr9Profile {
  isGrade9: boolean;
  marks: Gr9SubjectMark[];
  fetEligibility: FetSubjectEligibility[];
  savedCareerGoals: string[];
}

export interface Gr9AiRecommendedSubject {
  name: string;
  reason: string;
  careerLinks: string[];
}

export interface Gr9AiImprovementArea {
  subject: string;
  currentPercent: number;
  advice: string;
}

export interface Gr9AiAdvice {
  summary: string;
  recommendedSubjects: Gr9AiRecommendedSubject[];
  improvementAreas: Gr9AiImprovementArea[];
  careerPathsEnabled: string[];
  fromCache: boolean;
}
