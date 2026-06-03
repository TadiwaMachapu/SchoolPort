import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { qk } from "@/shared/api/queryKeys";

export function useClassesList(params?: { mine?: boolean; pageSize?: number; q?: string }) {
  return useQuery({
    queryKey: qk.classes.list(params as Record<string, unknown>),
    queryFn: () => api.classes.list({ pageSize: 50, ...params }),
    staleTime: 1000 * 60 * 5,
  });
}

export function useCreateClass() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: Parameters<typeof api.classes.create>[0]) => api.classes.create(body),
    onSuccess: () => qc.invalidateQueries({ queryKey: qk.classes.all() }),
  });
}

export function useUpdateClass() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: Parameters<typeof api.classes.update>[1] }) =>
      api.classes.update(id, body),
    onSuccess: () => qc.invalidateQueries({ queryKey: qk.classes.all() }),
  });
}
