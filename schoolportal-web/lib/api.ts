const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5128";

function toQueryString(params?: Record<string, string | number | boolean | undefined | null>): string {
  if (!params) return "";
  const entries = Object.entries(params).filter(([, v]) => v !== undefined && v !== null && v !== "");
  return entries.length ? "?" + new URLSearchParams(entries.map(([k, v]) => [k, String(v)])) : "";
}

function getToken(): string | null {
  if (typeof document === "undefined") return null;
  const match = document.cookie.match(/(?:^|; )sp_token=([^;]*)/);
  return match ? decodeURIComponent(match[1]) : null;
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
    current: () => request<{ theme: Record<string, unknown>; features: Record<string, boolean> }>("/api/schools/current"),
    updateTheme: (body: Record<string, unknown>) =>
      request<void>("/api/schools/theme", { method: "PUT", body: JSON.stringify(body) }),
    updateFeatures: (body: Record<string, boolean>) =>
      request<void>("/api/schools/features", { method: "PUT", body: JSON.stringify(body) }),
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
  },
  subjects: {
    list: () => request<Subject[]>("/api/subjects"),
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
  },
  quizzes: {
    list: (params?: { page?: number; pageSize?: number }) =>
      request<Paginated<Quiz>>(`/api/quizzes${toQueryString(params)}`),
    get: (id: string) => request<Quiz>(`/api/quizzes/${id}`),
    startAttempt: (quizId: string) =>
      request<QuizAttempt>(`/api/quizzes/${quizId}/attempts`, { method: "POST" }),
    submit: (attemptId: string, answers: { questionId: string; selectedOptionId?: string; textAnswer?: string }[]) =>
      request<QuizAttempt>(`/api/quizzes/attempts/${attemptId}/submit`, {
        method: "POST",
        body: JSON.stringify({ answers }),
      }),
    myAttempts: (quizId: string) =>
      request<QuizAttempt[]>(`/api/quizzes/${quizId}/attempts/mine`),
  },
  gradebook: {
    myGrades: () => request<GradeEntry[]>("/api/gradebook/my-grades"),
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
  parent: {
    children: () => request<ParentChild[]>("/api/parent/children"),
    grades: (studentId: string) => request<ParentGrade[]>(`/api/parent/children/${studentId}/grades`),
    attendance: (studentId: string, params?: { month?: number; year?: number }) =>
      request<ParentAttendanceSummary>(`/api/parent/children/${studentId}/attendance${toQueryString(params)}`),
    assignments: (studentId: string) => request<ParentAssignment[]>(`/api/parent/children/${studentId}/assignments`),
  },
  classes: {
    list: (params?: { year?: number; q?: string; page?: number; pageSize?: number }) =>
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
