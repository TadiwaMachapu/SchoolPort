import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api, type CreateAssignmentRequest } from "@/lib/api";
import { qk } from "@/shared/api/queryKeys";

export function useAssignments(params?: { page?: number; pageSize?: number }) {
  return useQuery({
    queryKey: qk.assignments.list(params),
    queryFn: () => api.assignments.list(params),
  });
}

export function useCreateAssignment() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateAssignmentRequest) => api.assignments.create(body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: qk.assignments.all() });
    },
  });
}
