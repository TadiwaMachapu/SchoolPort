"use client";
import { Suspense, useEffect, useRef, useState } from "react";
import { useSearchParams } from "next/navigation";
import { CheckCircle2, XCircle, Clock, CheckCheck, Save, ChevronDown, CalendarDays, TrendingUp } from "lucide-react";
import { Button } from "@/components/ui/button";
import { useAttendanceSession, useClasses, useBulkUpsertAttendance } from "@/features/attendance/api/hooks";
import { useToastStore } from "@/stores/toast.store";
import { AnimatePresence, motion } from "framer-motion";
import { api, MyAttendanceSummary } from "@/lib/api";
import { useIdentity } from "@/lib/auth-context";

const S = {
  1: { label: "Present", icon: CheckCircle2, card: "bg-emerald-50 border-emerald-300", avatar: "bg-emerald-500", badge: "text-emerald-700", ring: "ring-emerald-200" },
  0: { label: "Absent",  icon: XCircle,      card: "bg-rose-50 border-rose-300",       avatar: "bg-rose-500",    badge: "text-rose-700",    ring: "ring-rose-200"    },
  2: { label: "Late",    icon: Clock,         card: "bg-amber-50 border-amber-300",     avatar: "bg-amber-500",   badge: "text-amber-700",   ring: "ring-amber-200"   },
} as const;

type Status = 0 | 1 | 2;
const EMPTY_GUID = "00000000-0000-0000-0000-000000000000";

// ── Student attendance view ────────────────────────────────────────
const STATUS_LABELS: Record<number, { label: string; color: string }> = {
  1: { label: "Present", color: "text-emerald-700 bg-emerald-50" },
  0: { label: "Absent",  color: "text-rose-700 bg-rose-50"       },
  2: { label: "Late",    color: "text-amber-700 bg-amber-50"      },
};

function StudentAttendanceView() {
  const today = new Date();
  const [month, setMonth] = useState(today.getMonth() + 1);
  const [year,  setYear]  = useState(today.getFullYear());
  const [data,  setData]  = useState<MyAttendanceSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [expanded, setExpanded] = useState<string | null>(null);

  useEffect(() => {
    setLoading(true);
    api.attendance.mine(month, year)
      .then(setData)
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [month, year]);

  const MONTHS = ["Jan","Feb","Mar","Apr","May","Jun","Jul","Aug","Sep","Oct","Nov","Dec"];

  function prevMonth() {
    if (month === 1) { setMonth(12); setYear(y => y - 1); }
    else setMonth(m => m - 1);
  }
  function nextMonth() {
    if (month === 12) { setMonth(1); setYear(y => y + 1); }
    else setMonth(m => m + 1);
  }

  const totalPresent = data.reduce((s, c) => s + c.present, 0);
  const totalAbsent  = data.reduce((s, c) => s + c.absent, 0);
  const totalLate    = data.reduce((s, c) => s + c.late, 0);
  const totalDays    = data.reduce((s, c) => s + c.totalDays, 0);
  const overallRate  = totalDays ? Math.round((totalPresent + totalLate * 0.5) / totalDays * 100) : 100;

  return (
    <div className="p-4 md:p-6 lg:p-8 max-w-2xl">
      {/* Header + month nav */}
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-xl md:text-2xl font-semibold text-gray-900 tracking-tight">My Attendance</h1>
          <p className="text-sm text-gray-500 mt-0.5">Your attendance record by class</p>
        </div>
        <div className="flex items-center gap-1 rounded-xl border border-gray-200 p-1">
          <button onClick={prevMonth} className="rounded-lg px-3 py-1.5 text-sm text-gray-600 hover:bg-gray-100 transition-colors">‹</button>
          <span className="px-2 text-sm font-medium text-gray-700 min-w-[90px] text-center">{MONTHS[month - 1]} {year}</span>
          <button onClick={nextMonth} className="rounded-lg px-3 py-1.5 text-sm text-gray-600 hover:bg-gray-100 transition-colors">›</button>
        </div>
      </div>

      {/* Summary cards */}
      <div className="grid grid-cols-4 gap-3 mb-6">
        {[
          { label: "Rate",    value: `${overallRate}%`, color: overallRate >= 90 ? "text-emerald-600" : overallRate >= 75 ? "text-amber-600" : "text-rose-600" },
          { label: "Present", value: totalPresent, color: "text-emerald-600" },
          { label: "Late",    value: totalLate,    color: "text-amber-600"   },
          { label: "Absent",  value: totalAbsent,  color: "text-rose-600"    },
        ].map(({ label, value, color }) => (
          <div key={label} className="rounded-2xl border border-gray-200 bg-white p-3 text-center shadow-sm">
            <p className={`text-xl font-bold ${color}`}>{value}</p>
            <p className="text-xs text-gray-400 mt-0.5">{label}</p>
          </div>
        ))}
      </div>

      {loading ? (
        <div className="space-y-3">
          {[...Array(3)].map((_, i) => <div key={i} className="h-20 animate-pulse rounded-2xl bg-gray-100" />)}
        </div>
      ) : data.length === 0 ? (
        <div className="rounded-2xl border-2 border-dashed border-gray-200 py-12 text-center">
          <CalendarDays className="h-8 w-8 text-gray-200 mx-auto mb-3" />
          <p className="text-sm text-gray-500">No attendance records for this period</p>
        </div>
      ) : (
        <div className="space-y-3">
          {data.map(cls => (
            <div key={cls.classId} className="rounded-2xl border border-gray-200 bg-white shadow-sm overflow-hidden">
              <button
                onClick={() => setExpanded(e => e === cls.classId ? null : cls.classId)}
                className="w-full flex items-center justify-between px-4 py-4 text-left hover:bg-gray-50 transition-colors"
              >
                <div>
                  <p className="font-semibold text-gray-900">{cls.className}</p>
                  <p className="text-xs text-gray-400 mt-0.5">
                    {cls.present}P · {cls.absent}A · {cls.late}L · {cls.totalDays} days
                  </p>
                </div>
                <div className="flex items-center gap-3 shrink-0">
                  <div className="flex items-center gap-1.5">
                    <TrendingUp className={`h-4 w-4 ${cls.attendanceRate >= 90 ? "text-emerald-500" : cls.attendanceRate >= 75 ? "text-amber-500" : "text-rose-500"}`} />
                    <span className={`text-sm font-bold ${cls.attendanceRate >= 90 ? "text-emerald-600" : cls.attendanceRate >= 75 ? "text-amber-600" : "text-rose-600"}`}>
                      {cls.attendanceRate}%
                    </span>
                  </div>
                  <span className="text-gray-400 text-sm">{expanded === cls.classId ? "▲" : "▼"}</span>
                </div>
              </button>

              {expanded === cls.classId && cls.records.length > 0 && (
                <div className="border-t border-gray-100 divide-y divide-gray-100 max-h-64 overflow-y-auto">
                  {cls.records.map((r, i) => {
                    const cfg = STATUS_LABELS[r.status] ?? STATUS_LABELS[1];
                    return (
                      <div key={i} className="flex items-center justify-between px-4 py-2.5">
                        <span className="text-sm text-gray-600">
                          {new Date(r.date).toLocaleDateString("en-US", { weekday: "short", month: "short", day: "numeric" })}
                        </span>
                        <span className={`text-xs font-medium px-2.5 py-1 rounded-full ${cfg.color}`}>{cfg.label}</span>
                      </div>
                    );
                  })}
                </div>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

export default function AttendancePage() {
  const identity = useIdentity(); // Step 8

  if (identity === "Learner") {
    return <StudentAttendanceView />;
  }

  if (identity === "Parent") {
    return (
      <div className="flex flex-col items-center justify-center h-full p-8 text-center">
        <CalendarDays className="h-12 w-12 text-gray-200 mb-4" />
        <h2 className="text-lg font-semibold text-gray-700">Attendance</h2>
        <p className="text-sm text-gray-400 mt-2 max-w-sm">
          View your child's attendance record from the <strong>Parent Portal</strong> tab.
        </p>
      </div>
    );
  }

  return (
    <Suspense fallback={<div className="flex-1 p-8 text-center text-gray-400">Loading…</div>}>
      <AttendanceView />
    </Suspense>
  );
}

function AttendanceView() {
  const searchParams = useSearchParams();
  const toast = useToastStore();

  const [classId, setClassId]   = useState(() => searchParams.get("classId") ?? "");
  const [date, setDate]         = useState(() => new Date().toISOString().slice(0, 10));
  const [statuses, setStatuses] = useState<Record<string, Status>>({});
  const [done, setDone]         = useState(false);
  const saveTimer               = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);

  // Step 9.5 (Fix #4): scope the picker to the caller's classes (closes the picker-list leak;
  // also avoids a 403 for teachers without academics.manage under the tightened /api/classes).
  const { data: classList } = useClasses({ pageSize: 100, mine: true });
  const classes = classList?.items ?? [];

  useEffect(() => {
    if (!classId && classes.length > 0) setClassId(classes[0].classId);
  }, [classes, classId]);

  const { data: records = [], isLoading } = useAttendanceSession(classId, date);
  const mutation = useBulkUpsertAttendance();

  // Sync local statuses when records load
  useEffect(() => {
    if (records.length > 0) {
      setStatuses(Object.fromEntries(records.map((r) => [r.studentId, r.status as Status])));
      setDone(false);
    }
  }, [records]);

  const isNewSession = records.length > 0 && records.every((r) => r.attendanceId === EMPTY_GUID);

  const counts = {
    present: records.filter((r) => (statuses[r.studentId] ?? 1) === 1).length,
    absent:  records.filter((r) => (statuses[r.studentId] ?? 1) === 0).length,
    late:    records.filter((r) => (statuses[r.studentId] ?? 1) === 2).length,
  };

  const selectedClass = classes.find((c) => c.classId === classId);

  function cycle(studentId: string) {
    setDone(false);
    setStatuses((p) => {
      const cur = p[studentId] ?? 1;
      const next: Status = cur === 1 ? 0 : cur === 0 ? 2 : 1;
      return { ...p, [studentId]: next };
    });
    clearTimeout(saveTimer.current);
    saveTimer.current = setTimeout(() => triggerSave(), 1500);
  }

  function markAll(status: Status) {
    setDone(false);
    setStatuses(Object.fromEntries(records.map((r) => [r.studentId, status])));
    clearTimeout(saveTimer.current);
    saveTimer.current = setTimeout(() => triggerSave(), 1500);
  }

  async function triggerSave() {
    if (records.length === 0) return;
    try {
      await mutation.mutateAsync({
        attendances: records.map((r) => ({
          classId,
          studentId: r.studentId,
          date: new Date(date).toISOString(),
          status: statuses[r.studentId] ?? 1,
        })),
      });
      setDone(true);
      toast.success("Attendance saved", `${counts.present} present · ${counts.absent} absent`);
    } catch (e: unknown) {
      toast.error("Save failed", e instanceof Error ? e.message : "Please try again");
    }
  }

  return (
    <div className="flex flex-col h-full">
      {/* ── Top bar ─────────────────────────────────── */}
      <div className="border-b border-gray-100 bg-white px-4 md:px-6 py-3 md:py-4 shrink-0">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <h1 className="text-xl md:text-2xl font-semibold text-gray-900 tracking-tight">Attendance</h1>
            <p className="text-xs md:text-sm text-gray-500 mt-0.5">
              {selectedClass?.name ?? "Select a class"} ·{" "}
              {new Date(date + "T00:00:00").toLocaleDateString("en-US", { weekday: "short", month: "short", day: "numeric" })}
            </p>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            {/* Class selector */}
            <div className="relative">
              <select
                value={classId}
                onChange={(e) => { setClassId(e.target.value); setDone(false); }}
                className="appearance-none rounded-xl border border-gray-200 bg-white pl-3 pr-8 py-2.5 text-sm font-medium text-gray-700 focus:outline-none focus:ring-2 focus:ring-blue-500 shadow-sm min-h-[44px]"
              >
                {classes.map((c) => (
                  <option key={c.classId} value={c.classId}>{c.name}</option>
                ))}
              </select>
              <ChevronDown className="pointer-events-none absolute right-2 top-1/2 -translate-y-1/2 h-4 w-4 text-gray-400" />
            </div>
            {/* Date picker */}
            <input
              type="date"
              value={date}
              onChange={(e) => { setDate(e.target.value); setDone(false); }}
              className="rounded-xl border border-gray-200 bg-white px-3 py-2.5 text-sm font-medium text-gray-700 focus:outline-none focus:ring-2 focus:ring-blue-500 shadow-sm min-h-[44px]"
            />
          </div>
        </div>
      </div>

      {/* ── Banners ──────────────────────────────────── */}
      <AnimatePresence>
        {isNewSession && !done && (
          <motion.div
            initial={{ height: 0, opacity: 0 }}
            animate={{ height: "auto", opacity: 1 }}
            exit={{ height: 0, opacity: 0 }}
            className="overflow-hidden"
          >
            <div className="bg-blue-50 border-b border-blue-100 px-4 md:px-6 py-2.5 flex items-center gap-2 text-sm text-blue-700">
              <CheckCheck className="h-4 w-4 shrink-0" />
              <span>New session — everyone marked <strong>Present</strong>. Tap to change.</span>
            </div>
          </motion.div>
        )}
        {done && (
          <motion.div
            initial={{ height: 0, opacity: 0 }}
            animate={{ height: "auto", opacity: 1 }}
            exit={{ height: 0, opacity: 0 }}
            className="overflow-hidden"
          >
            <div className="bg-emerald-50 border-b border-emerald-100 px-4 md:px-6 py-2.5 flex items-center gap-2 text-sm text-emerald-700">
              <CheckCircle2 className="h-4 w-4 shrink-0" />
              <span>Saved · {counts.present} present{counts.absent > 0 ? `, ${counts.absent} absent` : ""}{counts.late > 0 ? `, ${counts.late} late` : ""}</span>
            </div>
          </motion.div>
        )}
      </AnimatePresence>

      {/* ── Summary + bulk actions ───────────────────── */}
      {!isLoading && records.length > 0 && (
        <div className="bg-gray-50 border-b border-gray-100 px-4 md:px-6 py-3 flex flex-wrap items-center justify-between gap-3 shrink-0">
          <div className="flex items-center gap-3 md:gap-4 text-sm font-medium">
            <span className="flex items-center gap-1.5 text-emerald-700">
              <span className="h-2 w-2 rounded-full bg-emerald-500" />{counts.present}P
            </span>
            <span className="flex items-center gap-1.5 text-rose-700">
              <span className="h-2 w-2 rounded-full bg-rose-500" />{counts.absent}A
            </span>
            {counts.late > 0 && (
              <span className="flex items-center gap-1.5 text-amber-700">
                <span className="h-2 w-2 rounded-full bg-amber-500" />{counts.late}L
              </span>
            )}
            <span className="text-gray-400 text-xs">/ {records.length}</span>
          </div>
          <div className="flex items-center gap-2">
            <button
              onClick={() => markAll(1)}
              className="flex items-center gap-1.5 rounded-lg border border-emerald-200 bg-emerald-50 px-3 py-2 text-xs font-medium text-emerald-700 hover:bg-emerald-100 active:scale-95 transition-all min-h-[36px]"
            >
              <CheckCheck className="h-3.5 w-3.5" /> All present
            </button>
            <button
              onClick={() => markAll(0)}
              className="flex items-center gap-1.5 rounded-lg border border-rose-200 bg-rose-50 px-3 py-2 text-xs font-medium text-rose-700 hover:bg-rose-100 active:scale-95 transition-all min-h-[36px]"
            >
              <XCircle className="h-3.5 w-3.5" /> All absent
            </button>
          </div>
        </div>
      )}

      {/* ── Student grid ─────────────────────────────── */}
      <div className="flex-1 overflow-y-auto px-3 md:px-6 py-4">
        {isLoading ? (
          <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-2.5 md:gap-3">
            {Array.from({ length: 12 }).map((_, i) => (
              <div key={i} className="h-24 animate-pulse rounded-2xl bg-gray-100" />
            ))}
          </div>
        ) : records.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-20 text-gray-400">
            <CheckCircle2 className="h-12 w-12 text-gray-200 mb-4" />
            <p className="text-base font-medium text-gray-500">No students enrolled</p>
            <p className="text-sm mt-1 text-center px-8">Add students to this class from the Users page</p>
          </div>
        ) : (
          <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-2.5 md:gap-3">
            {records.map((r) => {
              const status = statuses[r.studentId] ?? 1;
              const cfg = S[status];
              const Icon = cfg.icon;
              const initials = r.studentName.split(" ").map((n) => n[0]).join("").slice(0, 2).toUpperCase();
              return (
                <motion.button
                  key={r.studentId}
                  onClick={() => cycle(r.studentId)}
                  whileTap={{ scale: 0.94 }}
                  className={`relative flex flex-col items-center gap-2 rounded-2xl border-2 p-3 md:p-4 transition-colors select-none touch-manipulation focus:outline-none focus:ring-2 focus:ring-offset-2 ${cfg.card} ${cfg.ring} ring-1`}
                >
                  <div className={`h-12 w-12 md:h-11 md:w-11 rounded-full flex items-center justify-center text-white text-sm font-bold shrink-0 ${cfg.avatar}`}>
                    {initials}
                  </div>
                  <p className="text-xs font-semibold text-gray-900 text-center leading-tight line-clamp-2 w-full">
                    {r.studentName}
                  </p>
                  <div className={`flex items-center gap-1 text-[10px] font-bold uppercase tracking-wide ${cfg.badge}`}>
                    <Icon className="h-3 w-3" />
                    {cfg.label}
                  </div>
                </motion.button>
              );
            })}
          </div>
        )}
      </div>

      {/* ── Sticky save bar ──────────────────────────── */}
      {records.length > 0 && (
        <div className="border-t border-gray-100 bg-white px-4 md:px-6 py-3 md:py-4 flex items-center justify-between gap-4 shrink-0">
          <div className="text-sm text-gray-500 hidden sm:block">
            {mutation.isPending ? (
              <span className="flex items-center gap-1.5 text-blue-600">
                <span className="h-1.5 w-1.5 rounded-full bg-blue-500 animate-pulse" />
                Saving…
              </span>
            ) : done ? (
              <span className="flex items-center gap-1.5 text-emerald-600">
                <CheckCircle2 className="h-4 w-4" /> Saved
              </span>
            ) : (
              <span className="text-gray-400">Tap a student to change · auto-saves</span>
            )}
          </div>
          <Button
            onClick={triggerSave}
            loading={mutation.isPending}
            disabled={records.length === 0}
            className={`gap-2 w-full sm:w-auto ${done ? "bg-emerald-600 hover:bg-emerald-700" : ""}`}
          >
            {done ? <CheckCircle2 className="h-4 w-4" /> : <Save className="h-4 w-4" />}
            {done ? "Saved" : "Save Attendance"}
          </Button>
        </div>
      )}
    </div>
  );
}
