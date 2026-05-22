"use client";
import { useEffect, useState } from "react";
import { api, type Class, type AttendanceRecord } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Users } from "lucide-react";

const STATUS: Record<number, { label: string; active: string; dot: string }> = {
  1: { label: "Present", active: "bg-green-100 text-green-800 ring-2 ring-green-400 ring-offset-1",  dot: "bg-green-500" },
  0: { label: "Absent",  active: "bg-red-100 text-red-800 ring-2 ring-red-400 ring-offset-1",        dot: "bg-red-500" },
  2: { label: "Late",    active: "bg-yellow-100 text-yellow-800 ring-2 ring-yellow-400 ring-offset-1", dot: "bg-yellow-500" },
};
const INACTIVE = "bg-gray-100 text-gray-500 hover:bg-gray-200";

export default function AttendancePage() {
  const [classes,  setClasses]  = useState<Class[]>([]);
  const [classId,  setClassId]  = useState("");
  const [date,     setDate]     = useState(new Date().toISOString().slice(0, 10));
  const [records,  setRecords]  = useState<AttendanceRecord[]>([]);
  const [statuses, setStatuses] = useState<Record<string, number>>({});
  const [loading,  setLoading]  = useState(false);
  const [saving,   setSaving]   = useState(false);
  const [saved,    setSaved]    = useState(false);
  const [error,    setError]    = useState("");

  useEffect(() => {
    api.classes.list({ pageSize: 100 })
      .then(r => { setClasses(r.items); if (r.items.length > 0) setClassId(r.items[0].classId); })
      .catch(e => setError(e.message));
  }, []);

  useEffect(() => {
    if (!classId) return;
    setLoading(true); setError("");
    api.attendance.get(classId, date)
      .then(r => { setRecords(r); setStatuses(Object.fromEntries(r.map(x => [x.studentId, x.status]))); })
      .catch(e => setError(e.message))
      .finally(() => setLoading(false));
  }, [classId, date]);

  async function save() {
    if (records.length === 0) return;
    setSaving(true); setSaved(false);
    try {
      await api.attendance.bulkUpsert({
        attendances: records.map(r => ({
          classId, studentId: r.studentId,
          date: new Date(date).toISOString(),
          status: statuses[r.studentId] ?? 1,
        })),
      });
      setSaved(true);
      setTimeout(() => setSaved(false), 2500);
    } finally { setSaving(false); }
  }

  function markAll(status: number) {
    setStatuses(Object.fromEntries(records.map(r => [r.studentId, status])));
  }

  const counts = {
    present: records.filter(r => (statuses[r.studentId] ?? 1) === 1).length,
    absent:  records.filter(r => (statuses[r.studentId] ?? 1) === 0).length,
    late:    records.filter(r => (statuses[r.studentId] ?? 1) === 2).length,
  };

  return (
    <div className="p-6 lg:p-8 space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-gray-900 tracking-tight">Attendance</h1>
          <p className="text-sm text-gray-500 mt-1">Mark daily attendance for your classes</p>
        </div>
      </div>

      {error && <div className="rounded-lg bg-red-50 border border-red-200 p-3 text-sm text-red-700">{error}</div>}

      {/* Controls */}
      <Card>
        <CardContent className="flex flex-wrap items-center gap-4 p-4">
          <div className="space-y-1">
            <label className="text-xs font-medium text-gray-500 uppercase tracking-wider">Class</label>
            <select value={classId} onChange={e => setClassId(e.target.value)}
              className="rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 min-w-[140px]">
              {classes.map(c => <option key={c.classId} value={c.classId}>{c.name}</option>)}
            </select>
          </div>
          <div className="space-y-1">
            <label className="text-xs font-medium text-gray-500 uppercase tracking-wider">Date</label>
            <input type="date" value={date} onChange={e => setDate(e.target.value)}
              className="rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
          </div>
        </CardContent>
      </Card>

      {/* Summary KPIs (only when records exist) */}
      {!loading && records.length > 0 && (
        <div className="grid grid-cols-3 gap-4">
          {[
            { label: "Present", count: counts.present, color: "text-green-600",  bg: "bg-green-50" },
            { label: "Absent",  count: counts.absent,  color: "text-red-600",    bg: "bg-red-50" },
            { label: "Late",    count: counts.late,    color: "text-yellow-600", bg: "bg-yellow-50" },
          ].map(s => (
            <Card key={s.label} className={s.bg}>
              <CardContent className="p-4 text-center">
                <p className={`text-3xl font-bold ${s.color}`}>{s.count}</p>
                <p className="text-sm text-gray-600 mt-1">{s.label}</p>
                <p className="text-xs text-gray-400">
                  {records.length > 0 ? Math.round((s.count / records.length) * 100) : 0}%
                </p>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      {/* Attendance table */}
      <Card>
        <CardHeader className="py-4">
          <div className="flex items-center justify-between flex-wrap gap-3">
            <CardTitle className="text-base">
              Mark Attendance
              {records.length > 0 && (
                <span className="ml-2 text-sm font-normal text-gray-400">{records.length} students</span>
              )}
            </CardTitle>
            {records.length > 0 && (
              <div className="flex items-center gap-2">
                <span className="text-xs text-gray-400">Mark all:</span>
                {([1, 0, 2] as const).map(s => (
                  <button key={s} onClick={() => markAll(s)}
                    className={`rounded-full px-3 py-1 text-xs font-medium transition-colors ${STATUS[s].active.replace("ring-2 ring-offset-1 ring-green-400", "").replace("ring-2 ring-offset-1 ring-red-400", "").replace("ring-2 ring-offset-1 ring-yellow-400", "")}`}>
                    {STATUS[s].label}
                  </button>
                ))}
              </div>
            )}
          </div>
        </CardHeader>
        <CardContent className="p-0">
          {loading ? (
            <div className="p-4 space-y-3">
              {[1,2,3,4].map(i => (
                <div key={i} className="flex items-center justify-between px-2">
                  <div className="flex items-center gap-3">
                    <Skeleton className="h-8 w-8 rounded-full" />
                    <Skeleton className="h-4 w-32" />
                  </div>
                  <div className="flex gap-2">
                    <Skeleton className="h-7 w-20 rounded-full" />
                    <Skeleton className="h-7 w-20 rounded-full" />
                    <Skeleton className="h-7 w-16 rounded-full" />
                  </div>
                </div>
              ))}
            </div>
          ) : records.length === 0 ? (
            <div className="py-16 text-center text-gray-400">
              <div className="flex justify-center mb-3">
                <Users className="h-10 w-10 text-gray-300" />
              </div>
              <p className="text-sm font-medium text-gray-500">No students enrolled in this class</p>
              <p className="text-xs text-gray-400 mt-1">Enroll students from the Users page</p>
            </div>
          ) : (
            <>
              <table className="w-full text-sm">
                <thead className="border-b border-gray-200 bg-gray-50">
                  <tr>
                    {["Student", "ID", "Status"].map(h => (
                      <th key={h} className="px-6 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wider">{h}</th>
                    ))}
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {records.map(r => {
                    const current = statuses[r.studentId] ?? 1;
                    return (
                      <tr key={r.studentId} className="hover:bg-gray-50">
                        <td className="px-6 py-3">
                          <div className="flex items-center gap-3">
                            <div className={`h-2 w-2 rounded-full ${STATUS[current].dot}`} />
                            <span className="font-medium text-gray-900">{r.studentName}</span>
                          </div>
                        </td>
                        <td className="px-6 py-3 text-gray-400 text-xs">{r.studentNumber}</td>
                        <td className="px-6 py-3">
                          <div className="flex gap-1.5">
                            {([1, 0, 2] as const).map(s => (
                              <button key={s} onClick={() => setStatuses(p => ({ ...p, [r.studentId]: s }))}
                                className={`rounded-full px-3 py-1 text-xs font-medium transition-all ${
                                  current === s ? STATUS[s].active : INACTIVE
                                }`}>
                                {STATUS[s].label}
                              </button>
                            ))}
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
              <div className="border-t border-gray-200 p-4 flex items-center justify-between">
                <p className="text-sm text-gray-500">
                  {counts.present}/{records.length} present today
                </p>
                <div className="flex items-center gap-3">
                  {saved && (
                    <span className="flex items-center gap-1 text-sm text-green-600 font-medium">
                      <svg className="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                      </svg>
                      Saved
                    </span>
                  )}
                  <Button onClick={save} loading={saving}>Save Attendance</Button>
                </div>
              </div>
            </>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
