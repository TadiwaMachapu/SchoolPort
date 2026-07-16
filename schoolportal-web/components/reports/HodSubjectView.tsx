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
    return <p className="text-sm text-gray-500">No subject in your oversight scope.</p>;

  return (
    <div className="space-y-4">
      <div className="flex items-end gap-3">
        <div className="space-y-1">
          <label className="text-xs font-medium text-gray-600">Subject</label>
          <select
            value={subjectId}
            onChange={(e) => setSubjectId(e.target.value)}
            className="rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          >
            {subjects.map((s) => (
              <option key={s.subjectId} value={s.subjectId}>{s.name}</option>
            ))}
          </select>
        </div>
      </div>

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
          {/* Teacher comparison */}
          <section className="space-y-2">
            <h3 className="text-xs font-semibold text-gray-400 uppercase tracking-wider">Teacher comparison — {selectedName}</h3>
            {data.byTeacher.length === 0 ? (
              <p className="text-sm text-gray-400">No classes for this subject in scope.</p>
            ) : (
              <div className="space-y-2">
                {data.byTeacher.map((t, i) => (
                  <div key={t.teacherId ?? `unassigned-${i}`} className="rounded-xl bg-white border border-gray-200 shadow-sm p-3.5">
                    <div className="flex items-start justify-between gap-3">
                      <div className="min-w-0">
                        <p className="font-semibold text-gray-900 truncate">{t.teacherName}</p>
                        <p className="text-xs text-gray-400 mt-0.5 truncate">{t.classes.join(", ") || "—"}</p>
                      </div>
                      <div className="text-right shrink-0">
                        <p className="text-sm">
                          <span className={`font-bold ${t.atRiskCount > 0 ? "text-orange-600" : "text-emerald-600"}`}>{t.atRiskCount}</span>
                          <span className="text-gray-400"> / {t.learnerCount} at risk</span>
                        </p>
                        {t.notCapturedYet && (
                          <span className="inline-flex items-center rounded-full bg-gray-100 text-gray-500 px-2 py-0.5 text-[10px] font-medium mt-1">
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
            <h3 className="text-xs font-semibold text-gray-400 uppercase tracking-wider">Learners</h3>
            {data.learners.length === 0 ? (
              <div className="rounded-xl border-2 border-dashed border-gray-200 py-10 text-center">
                <Users className="h-8 w-8 text-gray-300 mx-auto mb-2" />
                <p className="text-sm text-gray-500">No learners with captured marks in this subject yet.</p>
              </div>
            ) : (
              <div className="overflow-x-auto rounded-xl border border-gray-200 shadow-sm">
                <table className="w-full text-sm">
                  <thead className="bg-gray-50 border-b border-gray-200">
                    <tr className="text-xs font-semibold text-gray-400 uppercase tracking-wider">
                      <th className="px-4 py-3 text-left">Learner</th>
                      <th className="px-3 py-3 text-center">Avg</th>
                      <th className="px-3 py-3 text-center">Trend</th>
                      <th className="px-3 py-3 text-center">Missing</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-100 bg-white">
                    {data.learners.map((l) => (
                      <tr key={l.studentId} className="hover:bg-gray-50">
                        <td className="px-4 py-3">
                          <div className="flex items-center gap-2">
                            <RiskDot risk={l.risk} />
                            <div className="min-w-0">
                              <p className="font-medium text-gray-900 truncate">{l.name}</p>
                              <p className="text-xs text-gray-400 truncate">{l.className}</p>
                            </div>
                          </div>
                        </td>
                        <td className="px-3 py-3 text-center font-semibold text-gray-800">
                          {l.average != null ? `${l.average}%` : "—"}
                        </td>
                        <td className="px-3 py-3">
                          <div className="flex justify-center"><TrendIcon trend={l.trend} /></div>
                        </td>
                        <td className="px-3 py-3 text-center text-gray-500">{l.missingAssessments}</td>
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
  if (trend === "improving") return <TrendingUp className="h-4 w-4 text-emerald-500" aria-label="improving" />;
  if (trend === "declining") return <TrendingDown className="h-4 w-4 text-red-500" aria-label="declining" />;
  if (trend === "stable") return <Minus className="h-4 w-4 text-gray-400" aria-label="stable" />;
  return <span className="text-gray-300 text-xs">—</span>;
}
