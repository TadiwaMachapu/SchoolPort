"use client";
import { useEffect, useMemo, useState } from "react";
import { api, type SubjectView, type Subject } from "@/lib/api";
import { RiskDot } from "./BandChip";
import { Loader2, AlertTriangle, Users, TrendingDown, TrendingUp, Minus } from "lucide-react";

// Sprint 1.5.3 — HOD subject oversight view: every learner taking the subject across the HOD's
// scope, plus a teacher comparison (which teacher's classes carry more at-risk in THIS subject),
// bounded server-side to the HOD's subject scope. Renders only for HOD/Principal/Deputy.

export function HodSubjectView({ allowedSubjectIds }: { allowedSubjectIds: string[] | null }) {
  const [subjects, setSubjects] = useState<Subject[]>([]);
  const [subjectId, setSubjectId] = useState("");
  const [data, setData] = useState<SubjectView | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  // Subjects the caller may open: their HOD-scoped subjects, or all (school-wide Principal/Deputy).
  useEffect(() => {
    api.subjects
      .list()
      .then((all) => {
        const allowed = allowedSubjectIds === null ? all : all.filter((s) => allowedSubjectIds.includes(s.subjectId));
        const sorted = [...allowed].sort((a, b) => a.name.localeCompare(b.name));
        setSubjects(sorted);
        if (sorted.length) setSubjectId(sorted[0].subjectId);
      })
      .catch(() => {});
  }, [allowedSubjectIds]);

  useEffect(() => {
    if (!subjectId) return;
    setLoading(true);
    setError("");
    setData(null);
    api.smartReports
      .subjectView(subjectId)
      .then(setData)
      .catch((e) => setError(e instanceof Error ? e.message : "Failed to load subject view"))
      .finally(() => setLoading(false));
  }, [subjectId]);

  const selectedName = useMemo(() => subjects.find((s) => s.subjectId === subjectId)?.name ?? "", [subjects, subjectId]);

  if (subjects.length === 0)
    return <p className="text-sm text-text-secondary">No subject in your oversight scope.</p>;

  return (
    <div className="space-y-4">
      <div className="flex items-end gap-3">
        <div className="space-y-1">
          <label className="text-xs font-medium text-text-secondary">Subject</label>
          <select
            value={subjectId}
            onChange={(e) => setSubjectId(e.target.value)}
            className="rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary"
          >
            {subjects.map((s) => (
              <option key={s.subjectId} value={s.subjectId}>{s.name}</option>
            ))}
          </select>
        </div>
      </div>

      {error && (
        <div className="flex items-center gap-2 rounded-lg bg-danger-100 px-4 py-3 text-sm text-danger-700">
          <AlertTriangle className="h-4 w-4 shrink-0" /> {error}
        </div>
      )}

      {loading && (
        <div className="flex items-center justify-center py-16">
          <Loader2 className="h-7 w-7 animate-spin text-text-muted" />
        </div>
      )}

      {data && !loading && (
        <>
          {/* Teacher comparison */}
          <section className="space-y-2">
            <h3 className="text-xs font-semibold text-text-muted uppercase tracking-wider">Teacher comparison — {selectedName}</h3>
            {data.byTeacher.length === 0 ? (
              <p className="text-sm text-text-muted">No classes for this subject in scope.</p>
            ) : (
              <div className="space-y-2">
                {data.byTeacher.map((t, i) => (
                  <div key={t.teacherId ?? `unassigned-${i}`} className="rounded-xl bg-surface-card border border-border shadow-sm p-3.5">
                    <div className="flex items-start justify-between gap-3">
                      <div className="min-w-0">
                        <p className="font-semibold text-text-primary truncate">{t.teacherName}</p>
                        <p className="text-xs text-text-muted mt-0.5 truncate">{t.classes.join(", ") || "—"}</p>
                      </div>
                      <div className="text-right shrink-0">
                        <p className="text-sm">
                          <span className={`font-bold ${t.atRiskCount > 0 ? "text-warning-700" : "text-success-700"}`}>{t.atRiskCount}</span>
                          <span className="text-text-muted"> / {t.learnerCount} at risk</span>
                        </p>
                        {t.notCapturedYet && (
                          <span className="inline-flex items-center rounded-full bg-surface-subtle text-text-secondary px-2 py-0.5 text-[10px] font-medium mt-1">
                            Not captured yet
                          </span>
                        )}
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </section>

          {/* Learners in the subject */}
          <section className="space-y-2">
            <h3 className="text-xs font-semibold text-text-muted uppercase tracking-wider">Learners</h3>
            {data.learners.length === 0 ? (
              <div className="rounded-xl border-2 border-dashed border-border py-10 text-center">
                <Users className="h-8 w-8 text-text-muted mx-auto mb-2" />
                <p className="text-sm text-text-secondary">No learners with captured marks in this subject yet.</p>
              </div>
            ) : (
              <div className="overflow-x-auto rounded-xl border border-border shadow-sm">
                <table className="w-full text-sm">
                  <thead className="bg-surface-subtle border-b border-border">
                    <tr className="text-xs font-semibold text-text-muted uppercase tracking-wider">
                      <th className="px-4 py-3 text-left">Learner</th>
                      <th className="px-3 py-3 text-center">Avg</th>
                      <th className="px-3 py-3 text-center">Trend</th>
                      <th className="px-3 py-3 text-center">Missing</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-border bg-surface-card">
                    {data.learners.map((l) => (
                      <tr key={l.studentId} className="hover:bg-surface-subtle">
                        <td className="px-4 py-3">
                          <div className="flex items-center gap-2">
                            <RiskDot risk={l.risk} />
                            <div className="min-w-0">
                              <p className="font-medium text-text-primary truncate">{l.name}</p>
                              <p className="text-xs text-text-muted truncate">{l.className}</p>
                            </div>
                          </div>
                        </td>
                        <td className="px-3 py-3 text-center font-semibold text-text-primary">
                          {l.average != null ? `${l.average}%` : "—"}
                        </td>
                        <td className="px-3 py-3">
                          <div className="flex justify-center"><TrendIcon trend={l.trend} /></div>
                        </td>
                        <td className="px-3 py-3 text-center text-text-secondary">{l.missingAssessments}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        </>
      )}
    </div>
  );
}

function TrendIcon({ trend }: { trend: string }) {
  if (trend === "improving") return <TrendingUp className="h-4 w-4 text-success-500" aria-label="improving" />;
  if (trend === "declining") return <TrendingDown className="h-4 w-4 text-danger-500" aria-label="declining" />;
  if (trend === "stable") return <Minus className="h-4 w-4 text-text-muted" aria-label="stable" />;
  return <span className="text-text-muted text-xs">—</span>;
}
