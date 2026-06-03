import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { qk } from "@/shared/api/queryKeys";

export function useNotifications(limit = 30) {
  return useQuery({
    queryKey: qk.notifications.list(limit),
    queryFn: () => api.notifications.list(limit),
    staleTime: 0,
    refetchOnWindowFocus: true,
  });
}

export function useMarkNotificationRead() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.notifications.markRead(id),
    onMutate: async (id) => {
      await qc.cancelQueries({ queryKey: qk.notifications.all() });
      const prev = qc.getQueryData(qk.notifications.list());
      qc.setQueryData(qk.notifications.list(), (old: any) => {
        if (!old) return old;
        return {
          ...old,
          items: old.items.map((n: any) =>
            n.notificationId === id ? { ...n, isRead: true } : n
          ),
          unreadCount: Math.max(0, (old.unreadCount ?? 1) - 1),
        };
      });
      return { prev };
    },
    onError: (_err, _id, ctx) => {
      if (ctx?.prev) qc.setQueryData(qk.notifications.list(), ctx.prev);
    },
  });
}

export function useMarkAllRead() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => api.notifications.markAllRead(),
    onMutate: async () => {
      await qc.cancelQueries({ queryKey: qk.notifications.all() });
      const prev = qc.getQueryData(qk.notifications.list());
      qc.setQueryData(qk.notifications.list(), (old: any) => {
        if (!old) return old;
        return {
          ...old,
          items: old.items.map((n: any) => ({ ...n, isRead: true })),
          unreadCount: 0,
        };
      });
      return { prev };
    },
    onError: (_err, _vars, ctx) => {
      if (ctx?.prev) qc.setQueryData(qk.notifications.list(), ctx.prev);
    },
  });
}
