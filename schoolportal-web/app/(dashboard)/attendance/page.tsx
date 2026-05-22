"use client";
import { useEffect, useState } from "react";
import { api, type Class, type AttendanceRecord } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

const STATUS_LABELS: Record<number, string> = { 0: "Absent", 1: "Present", 2: "Late" };
const STATUS_VARIANTS: Record<number, "success" | "destructive" | "warning"> = { 0: "destructive", 1: "success", 2: "warning" };

export default function AttendancePage() {
  const [classes, setClasses] = useState<Class[]>([]);
  const [classId, setClassId] = useState("");
  const [date, setDate] = useState(new Date().toISOString().slice(0, 10));
  const [records, setRecords] = useState<AttendanceRecord[]>([]);
  const [statuses, setStatuses] = useState<Record<string, number>>({});
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);

  const [error, setError] = useState("");

  useEffect(() => {
    api.classes.list({ pageSize: 100 })
      .then((res) => { setClasses(res.items); if (res.items.length > 0) setClassId(res.items[0].classId); })
      .catch((e) => setError(e.message));
  }, []);

  useEffect(() => {
    if (!classId) return;
    setLoading(true);
    setError("");
    api.attendance.get(classId, date)
      .then((r) => { setRecords(r); setStatuses(Object.fromEntries(r.map((x) => [x.studentId, x.status]))); })
      .catch((e) => setError(e.message))
      .finally(() => setLoading(false));
  }, [classId, date]);

  async function save() {
    setSaving(true);
    setSaved(false);
    await api.attendance.bulkUpsert({
      attendances: records.map((r) => ({
        classId,
        studentId: r.studentId,
        date: new Date(date).toISOString(),
        status: statuses[r.studentId] ?? 1,
      })),
    });
    setSaving(false);
    setSaved(true);
    setTimeout(() => setSaved(false), 2000);
  }

  return (
    <div className="p-8">
      <div className="mb-6">
        <h1 className="text-3xl font-bold text-gray-900">Attendance</h1>
      </div>

      {error && <div className="mb-4 rounded-md bg-red-50 border border-red-200 p-3 text-sm text-red-700">{error}</div>}
      <Card className="mb-6">
        <CardContent className="flex gap-4 p-4 flex-wrap">
          <select value={classId} onChange={(e) => setClassId(e.target.value)}
            className="rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500">
            {classes.map((c) => <option key={c.classId} value={c.classId}>{c.name}</option>)}
          </select>
          <input type="date" value={date} onChange={(e) => setDate(e.target.value)}
            className="rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Mark Attendance</CardTitle>
        </CardHeader>
        <CardContent className="p-0">
          {loading ? (
            <div className="flex justify-center py-12 text-gray-400">Loading…</div>
          ) : records.length === 0 ? (
            <div className="flex justify-center py-12 text-gray-400">No students enrolled in this class</div>
          ) : (
            <>
              <table className="w-full text-sm">
                <thead className="border-b border-gray-200 bg-gray-50">
                  <tr>
                    {["Student", "Number", "Status"].map((h) => (
                      <th key={h} className="px-6 py-3 text-left font-medium text-gray-500">{h}</th>
                    ))}
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {records.map((r) => (
                    <tr key={r.studentId} className="hover:bg-gray-50">
                      <td className="px-6 py-4 font-medium text-gray-900">{r.studentName}</td>
                      <td className="px-6 py-4 text-gray-600">{r.studentNumber}</td>
                      <td className="px-6 py-4">
                        <div className="flex gap-2">
                          {[1, 0, 2].map((s) => (
                            <button key={s}
                              onClick={() => setStatuses((prev) => ({ ...prev, [r.studentId]: s }))}
                              className={`rounded-full px-3 py-1 text-xs font-medium transition-colors ${statuses[r.studentId] === s ? "ring-2 ring-offset-1 ring-blue-500" : "opacity-60 hover:opacity-100"} ${s === 1 ? "bg-green-100 text-green-800" : s === 0 ? "bg-red-100 text-red-800" : "bg-yellow-100 text-yellow-800"}`}>
                              {STATUS_LABELS[s]}
                            </button>
                          ))}
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
              <div className="border-t border-gray-200 p-4 flex justify-end gap-2">
                {saved && <span className="text-sm text-green-600 self-center">✓ Saved</span>}
                <Button onClick={save} loading={saving}>Save Attendance</Button>
              </div>
            </>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
