import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { qk } from "@/shared/api/queryKeys";

export function useAnnouncementsList(params?: { pageSize?: number }) {
  return useQuery({
    queryKey: qk.announcements.list(params as Record<string, unknown>),
    queryFn: () => api.announcements.list({ pageSize: 50, ...params }),
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

export function useDeleteAnnouncement() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.announcements.delete(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: qk.announcements.all() }),
  });
}
