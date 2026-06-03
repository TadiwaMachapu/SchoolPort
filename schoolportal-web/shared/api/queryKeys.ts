export const qk = {
  me: () => ["me"] as const,
  school: () => ["school"] as const,

  notifications: {
    all: () => ["notifications"] as const,
    list: (limit?: number) => ["notifications", "list", limit] as const,
  },

  classes: {
    all: () => ["classes"] as const,
    list: (params?: Record<string, unknown>) => ["classes", "list", params] as const,
    detail: (id: string) => ["classes", id] as const,
    students: (id: string) => ["classes", id, "students"] as const,
    subjects: (id: string) => ["classes", id, "subjects"] as const,
  },

  assignments: {
    all: () => ["assignments"] as const,
    list: (params?: Record<string, unknown>) => ["assignments", "list", params] as const,
    detail: (id: string) => ["assignments", id] as const,
  },

  attendance: {
    all: () => ["attendance"] as const,
    session: (classId: string, date: string) => ["attendance", classId, date] as const,
  },

  announcements: {
    all: () => ["announcements"] as const,
    list: (params?: Record<string, unknown>) => ["announcements", "list", params] as const,
  },

  users: {
    all: () => ["users"] as const,
    list: (params?: Record<string, unknown>) => ["users", "list", params] as const,
    detail: (id: string) => ["users", id] as const,
  },

  analytics: {
    overview: () => ["analytics", "overview"] as const,
    gradeDistribution: () => ["analytics", "grade-distribution"] as const,
  },

  gradebook: {
    myGrades: () => ["gradebook", "my-grades"] as const,
    class: (classId: string) => ["gradebook", classId] as const,
  },

  submissions: {
    pending: (limit?: number) => ["submissions", "pending", limit] as const,
  },

  courses: {
    all: () => ["courses"] as const,
    list: (params?: Record<string, unknown>) => ["courses", "list", params] as const,
    detail: (id: string) => ["courses", id] as const,
  },

  parent: {
    children: () => ["parent", "children"] as const,
    grades: (studentId: string) => ["parent", studentId, "grades"] as const,
    attendance: (studentId: string) => ["parent", studentId, "attendance"] as const,
    assignments: (studentId: string) => ["parent", studentId, "assignments"] as const,
  },
} as const;
