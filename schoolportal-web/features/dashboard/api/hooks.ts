import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { qk } from "@/shared/api/queryKeys";

export function useMe() {
  return useQuery({
    queryKey: qk.me(),
    queryFn: () => api.me.get(),
    staleTime: 1000 * 60 * 5,
  });
}

export function useAdminOverview() {
  return useQuery({
    queryKey: qk.analytics.overview(),
    queryFn: () => api.analytics.overview(),
    staleTime: 1000 * 60 * 2,
  });
}

export function useMyClasses() {
  return useQuery({
    queryKey: qk.classes.list({ mine: true, pageSize: 20 }),
    queryFn: () => api.classes.list({ mine: true, pageSize: 20 }),
    staleTime: 1000 * 60 * 5,
  });
}

export function usePendingSubmissions(limit = 8) {
  return useQuery({
    queryKey: qk.submissions.pending(limit),
    queryFn: () => api.submissions.pending(limit),
    staleTime: 1000 * 60 * 1,
  });
}

export function useMyAssignments() {
  return useQuery({
    queryKey: qk.assignments.list({ pageSize: 20 }),
    queryFn: () => api.assignments.list({ pageSize: 20 }),
    staleTime: 1000 * 60 * 2,
  });
}

export function useMyGrades() {
  return useQuery({
    queryKey: qk.gradebook.myGrades(),
    queryFn: () => api.gradebook.myGrades(),
    staleTime: 1000 * 60 * 5,
  });
}

// Single source of truth for the learner's task state (status-aware). The dashboard derives its
// Overdue/Upcoming counts from this so it can never disagree with the My Academics page.
export function useMyAcademics() {
  return useQuery({
    queryKey: ["gradebook", "myAcademics"],
    queryFn: () => api.gradebook.myAcademics(),
    staleTime: 1000 * 60 * 2,
  });
}

export function useMyCourses() {
  return useQuery({
    queryKey: qk.courses.list({ pageSize: 6, publishedOnly: true }),
    queryFn: () => api.courses.list({ pageSize: 6, publishedOnly: true }),
    staleTime: 1000 * 60 * 5,
  });
}

export function useParentChildren() {
  return useQuery({
    queryKey: qk.parent.children(),
    queryFn: () => api.parent.children(),
    staleTime: 1000 * 60 * 10,
  });
}

export function useRecentAnnouncements(pageSize = 5) {
  return useQuery({
    queryKey: qk.announcements.list({ pageSize }),
    queryFn: () => api.announcements.list({ pageSize }),
    staleTime: 1000 * 60 * 2,
  });
}

export function useCreateAnnouncement() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: Parameters<typeof api.announcements.create>[0]) =>
      api.announcements.create(body),
    onSuccess: () => qc.invalidateQueries({ queryKey: qk.announcements.all() }),
  });
}
