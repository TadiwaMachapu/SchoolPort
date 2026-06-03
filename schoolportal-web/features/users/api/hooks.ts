import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { qk } from "@/shared/api/queryKeys";

export function useUsersList(params?: { q?: string; role?: string; page?: number; pageSize?: number }) {
  return useQuery({
    queryKey: qk.users.list(params as Record<string, unknown>),
    queryFn: () => api.users.list({ pageSize: 20, ...params }),
    staleTime: 1000 * 60 * 2,
    placeholderData: (prev) => prev,
  });
}

export function useCreateUser() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: Parameters<typeof api.users.create>[0]) => api.users.create(body),
    onSuccess: () => qc.invalidateQueries({ queryKey: qk.users.all() }),
  });
}

export function useUpdateUser() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: Parameters<typeof api.users.update>[1] }) =>
      api.users.update(id, body),
    onSuccess: () => qc.invalidateQueries({ queryKey: qk.users.all() }),
  });
}
