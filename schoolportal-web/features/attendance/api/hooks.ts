import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api, type AttendanceRecord, type BulkAttendanceRequest } from "@/lib/api";
import { qk } from "@/shared/api/queryKeys";

export function useAttendanceSession(classId: string, date: string) {
  return useQuery({
    queryKey: qk.attendance.session(classId, date),
    queryFn: () => api.attendance.get(classId, date),
    enabled: Boolean(classId && date),
    staleTime: 0, // attendance is always fresh
  });
}

export function useClasses(params?: { pageSize?: number; mine?: boolean }) {
  return useQuery({
    queryKey: qk.classes.list(params),
    queryFn: () => api.classes.list({ pageSize: 100, ...params }),
    staleTime: 1000 * 60 * 5,
  });
}

export function useBulkUpsertAttendance() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: BulkAttendanceRequest) => api.attendance.bulkUpsert(body),
    onSuccess: (_, vars) => {
      const classId = vars.attendances[0]?.classId;
      const date = vars.attendances[0]?.date?.slice(0, 10);
      if (classId && date) {
        qc.invalidateQueries({ queryKey: qk.attendance.session(classId, date) });
      }
    },
  });
}
