"use client";
import { useEffect, useState } from "react";
import { api, type SchoolOverview } from "@/lib/api";
import { Loader2, AlertTriangle } from "lucide-react";

// Sprint 1.5.3 — Principal/Deputy school-wide overview: band totals + per-grade and per-subject
// at-risk breakdowns from the shared at-risk primitive. Renders only for Principal/Deputy.

export function SchoolOverviewView() {
  const [data, setData] = useState<SchoolOverview | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    setLoading(true);
    setError("");
    api.smartReports
      .schoolOverview()
      .then(setData)
      .catch((e) => setError(e instanceof Error ? e.message : "Failed to load school overview"))
      .finally(() => setLoading(false));
  }, []);

  if (loading)
    return (
      <div className="flex items-center justify-center py-16">
        <Loader2 className="h-7 w-7 animate-spin text-text-muted" />
      </div>
    );

  if (error)
    return (
      <div className="flex items-center gap-2 rounded-lg bg-danger-100 px-4 py-3 text-sm text-danger-700">
        <AlertTriangle className="h-4 w-4 shrink-0" /> {error}
      </div>
    );

  if (!data) return null;

  const { totals, byGrade, bySubject } = data;

  return (
    <div className="space-y-6">
      {/* Band totals */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
        <BigTile label="Priority" value={totals.priority} tone="bg-red-50 text-red-700 border-red-100" />
        <BigTile label="At Risk" value={totals.atRisk} tone="bg-orange-50 text-orange-700 border-orange-100" />
        <BigTile label="Watch" value={totals.watch} tone="bg-amber-50 text-amber-700 border-amber-100" />
        <BigTile label="Learners" value={totals.totalLearners} tone="bg-surface-subtle text-text-primary border-border" />
      </div>

      {/* Per-grade breakdown */}
      <section className="space-y-2">
        <h3 className="text-xs font-semibold text-text-muted uppercase tracking-wider">By grade</h3>
        {byGrade.length === 0 ? (
          <p className="text-sm text-text-muted">No graded classes yet.</p>
        ) : (
          <div className="overflow-x-auto rounded-xl border border-border shadow-sm">
            <table className="w-full text-sm">
              <thead className="bg-surface-subtle border-b border-border">
                <tr className="text-xs font-semibold text-text-muted uppercase tracking-wider">
                  <th className="px-4 py-3 text-left">Grade</th>
                  <th className="px-3 py-3 text-center">Priority</th>
                  <th className="px-3 py-3 text-center">At Risk</th>
                  <th className="px-3 py-3 text-center">Watch</th>
                  <th className="px-3 py-3 text-center">Learners</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border bg-surface-card">
                {byGrade.map((g) => (
                  <tr key={g.grade} className="hover:bg-surface-subtle">
                    <td className="px-4 py-3 font-medium text-text-primary">Grade {g.grade}</td>
                    <td className="px-3 py-3 text-center font-semibold text-red-600">{g.priority || <span className="text-text-muted">0</span>}</td>
                    <td className="px-3 py-3 text-center font-semibold text-orange-600">{g.atRisk || <span className="text-text-muted">0</span>}</td>
                    <td className="px-3 py-3 text-center font-semibold text-amber-600">{g.watch || <span className="text-text-muted">0</span>}</td>
                    <td className="px-3 py-3 text-center text-text-secondary">{g.totalLearners}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      {/* Per-subject at-risk (most-flagged first) */}
      <section className="space-y-2">
        <h3 className="text-xs font-semibold text-text-muted uppercase tracking-wider">Subjects with the most at-risk learners</h3>
        {bySubject.length === 0 ? (
          <p className="text-sm text-text-muted">No at-risk subjects this term.</p>
        ) : (
          <div className="space-y-1.5">
            {bySubject.map((s) => {
              const max = bySubject[0].atRiskLearners || 1;
              const pct = Math.round((s.atRiskLearners / max) * 100);
              return (
                <div key={s.subjectName} className="flex items-center gap-3">
                  <span className="w-40 shrink-0 truncate text-sm text-text-primary">{s.subjectName}</span>
                  <div className="flex-1 h-2.5 rounded-full bg-surface-subtle overflow-hidden">
                    <div className="h-full rounded-full bg-orange-400" style={{ width: `${pct}%` }} />
                  </div>
                  <span className="w-8 shrink-0 text-right text-sm font-semibold text-text-primary">{s.atRiskLearners}</span>
                </div>
              );
            })}
          </div>
        )}
      </section>
    </div>
  );
}

function BigTile({ label, value, tone }: { label: string; value: number; tone: string }) {
  return (
    <div className={`rounded-xl border px-4 py-3 ${tone}`}>
      <p className="text-2xl font-bold leading-none">{value}</p>
      <p className="text-xs font-medium mt-1.5">{label}</p>
    </div>
  );
}
