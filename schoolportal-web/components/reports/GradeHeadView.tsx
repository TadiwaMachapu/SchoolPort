"use client";
import { useEffect, useState } from "react";
import { api, type GradeView } from "@/lib/api";
import { BandChip, RiskDot, CapturedFraction, type Band } from "./BandChip";
import { Loader2, AlertTriangle, Users } from "lucide-react";

// Sprint 1.5.3 — Grade Head oversight view: every learner in the grade the caller oversees,
// cross-subject, Priority-first (server-sorted). Scoped + gated server-side; this only renders for
// GradeHead/PhaseHead/Principal/Deputy (page-level position gate).

export function GradeHeadView({ grades }: { grades: number[] }) {
  const [grade, setGrade] = useState<number | null>(grades[0] ?? null);
  const [data, setData] = useState<GradeView | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    if (grade == null) return;
    setLoading(true);
    setError("");
    setData(null);
    api.smartReports
      .gradeView(grade)
      .then(setData)
      .catch((e) => setError(e instanceof Error ? e.message : "Failed to load grade view"))
      .finally(() => setLoading(false));
  }, [grade]);

  if (grades.length === 0)
    return <p className="text-sm text-gray-500">No grade in your oversight scope.</p>;

  const learners = data?.learners ?? [];
  const counts = {
    Priority: learners.filter((l) => l.interventionBand === "Priority").length,
    AtRisk: learners.filter((l) => l.interventionBand === "AtRisk").length,
    Watch: learners.filter((l) => l.interventionBand === "Watch").length,
  };

  return (
    <div className="space-y-4">
      {grades.length > 1 && (
        <div className="flex items-end gap-3">
          <div className="space-y-1">
            <label className="text-xs font-medium text-gray-600">Grade</label>
            <select
              value={grade ?? ""}
              onChange={(e) => setGrade(Number(e.target.value))}
              className="rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            >
              {grades.map((g) => (
                <option key={g} value={g}>Grade {g}</option>
              ))}
            </select>
          </div>
        </div>
      )}

      {error && (
        <div className="flex items-center gap-2 rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700">
          <AlertTriangle className="h-4 w-4 shrink-0" /> {error}
        </div>
      )}

      {loading && (
        <div className="flex items-center justify-center py-16">
          <Loader2 className="h-7 w-7 animate-spin text-gray-400" />
        </div>
      )}

      {data && !loading && (
        <>
          {/* Band summary */}
          <div className="grid grid-cols-3 gap-2 sm:max-w-md">
            <SummaryTile label="Priority" value={counts.Priority} tone="bg-red-50 text-red-700" />
            <SummaryTile label="At Risk" value={counts.AtRisk} tone="bg-orange-50 text-orange-700" />
            <SummaryTile label="Watch" value={counts.Watch} tone="bg-amber-50 text-amber-700" />
          </div>

          {learners.length === 0 ? (
            <div className="rounded-xl border-2 border-dashed border-gray-200 py-12 text-center">
              <Users className="h-8 w-8 text-gray-300 mx-auto mb-2" />
              <p className="text-sm text-gray-500">No learners in Grade {data.grade}, or no marks captured yet.</p>
            </div>
          ) : (
            <div className="space-y-2">
              {learners.map((l) => {
                const flagged = l.subjects.filter((s) => s.risk === "red" || s.risk === "amber");
                return (
                  <div key={l.studentId} className="rounded-xl bg-white border border-gray-200 shadow-sm p-3.5">
                    <div className="flex items-start justify-between gap-3">
                      <div className="min-w-0">
                        <div className="flex items-center gap-2 flex-wrap">
                          <p className="font-semibold text-gray-900 truncate">{l.name}</p>
                          <BandChip band={l.interventionBand as Band} />
                        </div>
                        <p className="text-xs text-gray-400 mt-0.5">
                          {l.className} · {l.studentNumber}
                        </p>
                      </div>
                      <div className="text-right shrink-0">
                        <p className="text-sm font-semibold text-gray-700">
                          {l.subjectsBelowFifty} <span className="font-normal text-gray-400">below 50%</span>
                        </p>
                        <CapturedFraction captured={l.capturedSubjectCount} total={l.totalSubjectCount} />
                      </div>
                    </div>

                    {flagged.length > 0 && (
                      <div className="mt-2.5 flex flex-wrap gap-1.5">
                        {flagged.map((s) => (
                          <span
                            key={s.subjectName}
                            className="inline-flex items-center gap-1.5 rounded-full bg-gray-50 border border-gray-100 px-2 py-0.5 text-[11px] text-gray-700"
                          >
                            <RiskDot risk={s.risk} />
                            {s.subjectName}
                            <span className="text-gray-400">{s.average}%</span>
                          </span>
                        ))}
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          )}
        </>
      )}
    </div>
  );
}

function SummaryTile({ label, value, tone }: { label: string; value: number; tone: string }) {
  return (
    <div className={`rounded-lg px-3 py-2 ${tone}`}>
      <p className="text-lg font-bold leading-none">{value}</p>
      <p className="text-[11px] font-medium mt-1">{label}</p>
    </div>
  );
}
