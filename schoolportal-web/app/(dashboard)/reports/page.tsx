"use client";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { api, type Class, type Term, type TermReport, type SmartAtRiskStudent } from "@/lib/api";
import { useFeature } from "@/lib/use-feature";
import { usePermission, useAnyPosition, useAuth } from "@/lib/auth-context";
import { Button } from "@/components/ui/button";
import { ReportCommentCard } from "@/components/reports/ReportCommentCard";
import { PrincipalSummaryCard } from "@/components/reports/PrincipalSummaryCard";
import { GradeHeadView } from "@/components/reports/GradeHeadView";
import { HodSubjectView } from "@/components/reports/HodSubjectView";
import { SchoolOverviewView } from "@/components/reports/SchoolOverviewView";
import { FileText, Download, AlertTriangle, Loader2, ShieldAlert, Sparkles, BookOpen, Layers, GraduationCap, Building2 } from "lucide-react";

// Sprint 1.5.3 — ScopeType enum values (mirror SchoolPortal.Data.Entities.ScopeType) for reading
// position scopes off the auth context: Subject=1, Phase=2, Grade=3.
const SCOPE_SUBJECT = 1;
const SCOPE_PHASE = 2;
const SCOPE_GRADE = 3;

function phaseGrades(phase: string): number[] {
  const p = phase.replace(/\s+/g, "").toLowerCase();
  if (p === "fet") return [10, 11, 12];
  if (p === "seniorphase") return [7, 8, 9];
  return [];
}

const CAPS_LEVEL_COLOURS: Record<number, string> = {
  7: "bg-emerald-100 text-emerald-800",
  6: "bg-green-100 text-green-800",
  5: "bg-blue-100 text-blue-800",
  4: "bg-yellow-100 text-yellow-700",
  3: "bg-orange-100 text-orange-700",
  2: "bg-red-100 text-red-700",
  1: "bg-red-200 text-red-800",
};

const FLAG_LABELS: Record<string, { label: string; colour: string }> = {
  LowAttendance:     { label: "Low Attendance",    colour: "bg-amber-100 text-amber-800" },
  SubjectFailing:    { label: "Subject Failing",   colour: "bg-orange-100 text-orange-800" },
  MultipleFailures:  { label: "Multiple Failures", colour: "bg-red-100 text-red-800" },
  LowOverallAverage: { label: "Low Overall Avg",   colour: "bg-red-100 text-red-800" },
};

function capsLevelLabel(level: number) {
  return `L${level}`;
}

function printReport(report: TermReport) {
  const now = new Date().toLocaleDateString("en-ZA", { day: "2-digit", month: "long", year: "numeric" });
  const rows = report.students.map(s => {
    const subjectRows = s.subjectResults.map(r =>
      `<tr>
        <td style="padding:6px 12px;border-bottom:1px solid #f3f4f6">${r.subjectName}</td>
        <td style="padding:6px 12px;border-bottom:1px solid #f3f4f6;text-align:center">${r.average}%</td>
        <td style="padding:6px 12px;border-bottom:1px solid #f3f4f6;text-align:center">${r.capsLevel != null ? `L${r.capsLevel}` : "—"}</td>
        <td style="padding:6px 12px;border-bottom:1px solid #f3f4f6;text-align:center">${r.assignmentCount}</td>
      </tr>`
    ).join("");
    return `
      <div style="page-break-inside:avoid;margin-bottom:24px;border:1px solid #e5e7eb;border-radius:8px;overflow:hidden">
        <div style="background:#f9fafb;padding:10px 16px;display:flex;justify-content:space-between;align-items:center">
          <div>
            <strong style="font-size:14px">${s.name}</strong>
            <span style="font-size:11px;color:#6b7280;margin-left:8px">${s.studentNumber}</span>
          </div>
          <div style="text-align:right;font-size:12px">
            ${s.overallAverage != null ? `<div style="font-weight:700;font-size:16px;color:#2563eb">${s.overallAverage}%</div>` : ""}
            ${s.attendancePercent != null ? `<div style="color:#6b7280">Attendance: ${s.attendancePercent}%</div>` : ""}
          </div>
        </div>
        <table style="width:100%;border-collapse:collapse;font-size:12px">
          <thead><tr style="background:#f9fafb">
            <th style="padding:6px 12px;text-align:left;font-size:10px;text-transform:uppercase;color:#9ca3af">Subject</th>
            <th style="padding:6px 12px;text-align:center;font-size:10px;text-transform:uppercase;color:#9ca3af">Average</th>
            <th style="padding:6px 12px;text-align:center;font-size:10px;text-transform:uppercase;color:#9ca3af">CAPS Level</th>
            <th style="padding:6px 12px;text-align:center;font-size:10px;text-transform:uppercase;color:#9ca3af">Tasks</th>
          </tr></thead>
          <tbody>${subjectRows}</tbody>
        </table>
      </div>`;
  }).join("");

  const html = `<!DOCTYPE html><html><head><title>Term ${report.termNumber} ${report.year} — ${report.className}</title>
  <style>body{font-family:system-ui,sans-serif;margin:0;padding:24px;color:#111827;font-size:13px}h1{margin:0;font-size:18px;font-weight:700}@media print{@page{margin:12mm}}</style>
  </head><body>
  <div style="display:flex;justify-content:space-between;align-items:flex-start;border-bottom:2px solid #e5e7eb;padding-bottom:12px;margin-bottom:20px">
    <div>
      <h1>${report.className} — Term ${report.termNumber} Report (${report.year})</h1>
      <div style="color:#6b7280;font-size:12px;margin-top:4px">Generated ${now} · ${report.students.length} learner${report.students.length !== 1 ? "s" : ""}</div>
    </div>
  </div>
  ${rows}
  </body></html>`;

  const win = window.open("", "_blank", "width=900,height=700");
  if (!win) return;
  win.document.write(html);
  win.document.close();
  win.focus();
  setTimeout(() => win.print(), 400);
}

type PageTab = "term-report" | "at-risk" | "ai-comments" | "principal-summary"
  | "grade-oversight" | "subject-oversight" | "school-overview";

const OVERSIGHT_TABS: PageTab[] = ["grade-oversight", "subject-oversight", "school-overview"];

export default function ReportsPage() {
  const router = useRouter();
  const hasReports = useFeature("smartReports");
  const [tab, setTab] = useState<PageTab>("term-report");
  const isAdmin = usePermission("reporting.principal_summary"); // Step 8

  // Sprint 1.5.3 oversight role views — gated on the POSITION (mirrors the server gate). UX only;
  // the backend is the authority (a non-holder that reaches an endpoint gets 403).
  const auth = useAuth();
  const canGrade = useAnyPosition(["GradeHead", "PhaseHead", "Principal", "DeputyPrincipal"]);
  const canSubject = useAnyPosition(["HOD", "Principal", "DeputyPrincipal"]);
  const canOverview = useAnyPosition(["Principal", "DeputyPrincipal"]);
  const schoolWide = auth.positions.some((p) => p.key === "Principal" || p.key === "DeputyPrincipal");

  // Grades the caller may open: GradeHead grade scopes + PhaseHead phase scopes; school-wide
  // (Principal/Deputy) sees the full senior+FET span. Sorted; drives the grade selector.
  const accessibleGrades = (() => {
    const set = new Set<number>();
    if (schoolWide) [8, 9, 10, 11, 12].forEach((g) => set.add(g));
    for (const p of auth.positions) {
      if (p.key === "GradeHead")
        for (const s of p.scopes)
          if (s.scopeType === SCOPE_GRADE && s.scopeValue) {
            const g = parseInt(s.scopeValue, 10);
            if (!Number.isNaN(g)) set.add(g);
          }
      if (p.key === "PhaseHead")
        for (const s of p.scopes)
          if (s.scopeType === SCOPE_PHASE && s.scopeValue) phaseGrades(s.scopeValue).forEach((g) => set.add(g));
    }
    return [...set].sort((a, b) => a - b);
  })();

  // Subjects the caller may open: HOD subject scopes; null = school-wide (any subject).
  const allowedSubjectIds: string[] | null = schoolWide
    ? null
    : auth.positions
        .filter((p) => p.key === "HOD")
        .flatMap((p) => p.scopes.filter((s) => s.scopeType === SCOPE_SUBJECT && s.scopeRefId).map((s) => s.scopeRefId!));

  const [classes, setClasses] = useState<Class[]>([]);
  const [terms,   setTerms]   = useState<Term[]>([]);
  const [classId, setClassId] = useState("");
  const [termId,  setTermId]  = useState("");

  // Term Report
  const [report,  setReport]  = useState<TermReport | null>(null);
  const [loading, setLoading] = useState(false);
  const [error,   setError]   = useState("");

  // At-Risk
  const [atRisk,        setAtRisk]        = useState<SmartAtRiskStudent[]>([]);
  const [atRiskLoading, setAtRiskLoading] = useState(false);
  const [atRiskError,   setAtRiskError]   = useState("");

  useEffect(() => {
    api.classes.list({ pageSize: 100 }).then(r => {
      setClasses(r.items);
      if (r.items.length) setClassId(r.items[0].classId);
    }).catch(() => {});
    api.terms.list().then(ts => {
      setTerms(ts);
      const current = ts.find(t => t.isCurrent);
      if (current) setTermId(current.termId);
      else if (ts.length) setTermId(ts[0].termId);
    }).catch(() => {});
  }, []);

  async function loadReport() {
    if (!classId || !termId) return;
    setLoading(true); setError(""); setReport(null);
    try {
      setReport(await api.reports.termReport(classId, termId));
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to load report");
    } finally {
      setLoading(false);
    }
  }

  async function loadAtRisk() {
    if (!classId || !termId) return;
    setAtRiskLoading(true); setAtRiskError("");
    try {
      setAtRisk(await api.reports.atRisk(classId, termId));
    } catch (e) {
      setAtRiskError(e instanceof Error ? e.message : "Failed to load at-risk data");
    } finally {
      setAtRiskLoading(false);
    }
  }

  const selectedTerm = terms.find(t => t.termId === termId);

  if (!hasReports) {
    return (
      <div className="flex flex-col items-center justify-center h-96 text-center px-4">
        <FileText className="h-12 w-12 text-gray-200 mb-4" />
        <h2 className="text-lg font-semibold text-gray-700">Smart Reports not enabled</h2>
        <p className="text-sm text-gray-400 mt-1">Enable the Smart Reports feature in Settings.</p>
        <button onClick={() => router.push("/settings")} className="mt-4 text-sm text-blue-600 hover:underline">Go to Settings</button>
      </div>
    );
  }

  const tabs: { id: PageTab; label: string; icon: React.ReactNode; show?: boolean }[] = [
    { id: "term-report",        label: "Term Report",       icon: <BookOpen className="h-4 w-4" /> },
    { id: "at-risk",            label: "At-Risk Learners",  icon: <ShieldAlert className="h-4 w-4" /> },
    { id: "ai-comments",        label: "AI Comments",       icon: <Sparkles className="h-4 w-4" /> },
    // Sprint 1.5.3 oversight role views — rendered only for holders of the relevant position.
    { id: "grade-oversight",    label: "Grade View",        icon: <Layers className="h-4 w-4" />,        show: canGrade },
    { id: "subject-oversight",  label: "Subject View",      icon: <GraduationCap className="h-4 w-4" />, show: canSubject },
    { id: "school-overview",    label: "School Overview",   icon: <Building2 className="h-4 w-4" />,      show: canOverview },
    { id: "principal-summary",  label: "Principal Summary", icon: <FileText className="h-4 w-4" />,      show: isAdmin },
  ];
  const isOversight = OVERSIGHT_TABS.includes(tab);

  return (
    <div className="p-4 md:p-6 lg:p-8 max-w-5xl mx-auto space-y-6">
      <div className="flex items-start justify-between gap-4 flex-wrap">
        <div>
          <h1 className="text-2xl font-semibold text-gray-900 tracking-tight">Smart Reports</h1>
          <p className="text-sm text-gray-500 mt-1">Term reports, at-risk tracking, and AI-powered insights.</p>
        </div>
        {tab === "term-report" && report && (
          <Button variant="outline" onClick={() => printReport(report)} className="gap-2 shrink-0">
            <Download className="h-4 w-4" /> Print / Export PDF
          </Button>
        )}
      </div>

      {/* Selectors — class/term drive the class-scoped tabs only; the oversight views scope
          themselves by position (grade/subject/school) and the current term server-side. */}
      {!isOversight && (
      <div className="flex items-end gap-3 flex-wrap">
        <div className="space-y-1">
          <label className="text-xs font-medium text-gray-600">Class</label>
          <select value={classId} onChange={e => setClassId(e.target.value)}
            className="rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500">
            {classes.map(c => <option key={c.classId} value={c.classId}>{c.name}</option>)}
          </select>
        </div>
        <div className="space-y-1">
          <label className="text-xs font-medium text-gray-600">Term</label>
          <select value={termId} onChange={e => setTermId(e.target.value)}
            className="rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500">
            {terms.map(t => (
              <option key={t.termId} value={t.termId}>
                {t.isCurrent ? "★ " : ""}Term {t.termNumber} {t.year}
              </option>
            ))}
          </select>
        </div>
      </div>
      )}

      {/* Tabs */}
      <div className="border-b border-gray-200">
        <nav className="-mb-px flex gap-0 overflow-x-auto">
          {tabs.filter(t => t.show !== false).map(t => (
            <button
              key={t.id}
              onClick={() => setTab(t.id)}
              className={`flex items-center gap-2 whitespace-nowrap px-4 py-2.5 text-sm font-medium border-b-2 transition-colors ${
                tab === t.id
                  ? "border-blue-600 text-blue-600"
                  : "border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300"
              }`}
            >
              {t.icon}
              {t.label}
            </button>
          ))}
        </nav>
      </div>

      {/* ── Tab: Term Report ── */}
      {tab === "term-report" && (
        <div className="space-y-4">
          <div>
            <Button onClick={loadReport} loading={loading} className="gap-2">
              <FileText className="h-4 w-4" /> Generate Report
            </Button>
          </div>

          {error && (
            <div className="flex items-center gap-2 rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700">
              <AlertTriangle className="h-4 w-4 shrink-0" /> {error}
            </div>
          )}

          {loading && (
            <div className="flex items-center justify-center py-20">
              <Loader2 className="h-8 w-8 animate-spin text-gray-400" />
            </div>
          )}

          {report && !loading && (
            <div className="space-y-4">
              <div className="rounded-xl bg-blue-50 border border-blue-100 px-5 py-3 flex items-center justify-between">
                <div>
                  <p className="font-semibold text-blue-900">{report.className} — Term {report.termNumber} {report.year}</p>
                  <p className="text-xs text-blue-600 mt-0.5">
                    {new Date(report.startDate).toLocaleDateString("en-ZA")} – {new Date(report.endDate).toLocaleDateString("en-ZA")} · {report.students.length} learner{report.students.length !== 1 ? "s" : ""}
                  </p>
                </div>
                {selectedTerm?.isCurrent && (
                  <span className="text-xs font-medium bg-blue-600 text-white rounded-full px-2.5 py-1">Current Term</span>
                )}
              </div>

              {report.students.length === 0 ? (
                <div className="rounded-xl border-2 border-dashed border-gray-200 py-16 text-center">
                  <p className="text-gray-500">No learners enrolled or no grades recorded for this term.</p>
                </div>
              ) : report.students.map(s => (
                <div key={s.studentId} className="rounded-xl bg-white border border-gray-200 shadow-sm overflow-hidden">
                  <div className="flex items-center justify-between px-5 py-3 bg-gray-50 border-b border-gray-200">
                    <div>
                      <p className="font-semibold text-gray-900">{s.name}</p>
                      <p className="text-xs text-gray-400">{s.studentNumber}</p>
                    </div>
                    <div className="flex items-center gap-4 text-right">
                      {s.overallAverage != null && (
                        <div>
                          <p className="text-xl font-bold text-blue-600">{s.overallAverage}%</p>
                          <p className="text-xs text-gray-400">Overall</p>
                        </div>
                      )}
                      {s.attendancePercent != null && (
                        <div>
                          <p className="text-xl font-bold text-emerald-600">{s.attendancePercent}%</p>
                          <p className="text-xs text-gray-400">Attendance</p>
                        </div>
                      )}
                    </div>
                  </div>
                  {s.subjectResults.length === 0 ? (
                    <p className="px-5 py-4 text-sm text-gray-400">No graded tasks recorded this term.</p>
                  ) : (
                    <table className="w-full text-sm">
                      <thead className="border-b border-gray-100">
                        <tr className="text-xs font-semibold text-gray-400 uppercase tracking-wider">
                          <th className="px-5 py-2.5 text-left">Subject</th>
                          <th className="px-4 py-2.5 text-center">Average</th>
                          <th className="px-4 py-2.5 text-center">CAPS Level</th>
                          <th className="px-4 py-2.5 text-center">Tasks</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-gray-50">
                        {s.subjectResults.map((r, i) => (
                          <tr key={i} className="hover:bg-gray-50">
                            <td className="px-5 py-3">
                              <span className="font-medium text-gray-900">{r.subjectName}</span>
                              {r.capsPhase && (
                                <span className="ml-2 text-[10px] text-gray-400">{r.capsPhase === "SeniorPhase" ? "Gr 7–9" : "Gr 10–12"}</span>
                              )}
                            </td>
                            <td className="px-4 py-3 text-center font-semibold text-gray-800">{r.average}%</td>
                            <td className="px-4 py-3 text-center">
                              {r.capsLevel != null ? (
                                <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold ${CAPS_LEVEL_COLOURS[r.capsLevel]}`}>
                                  {capsLevelLabel(r.capsLevel)}
                                </span>
                              ) : (
                                <span className="text-gray-300 text-xs">—</span>
                              )}
                            </td>
                            <td className="px-4 py-3 text-center text-gray-500">{r.assignmentCount}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  )}
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      {/* ── Tab: At-Risk Learners ── */}
      {tab === "at-risk" && (
        <div className="space-y-4">
          <div className="flex items-center justify-between">
            <p className="text-sm text-gray-600">
              Learners with low attendance (&lt;80%), failing subjects (&lt;40%), or overall average below 50%.
            </p>
            <Button onClick={loadAtRisk} loading={atRiskLoading} size="sm" className="gap-2 shrink-0">
              <ShieldAlert className="h-4 w-4" /> Identify At-Risk
            </Button>
          </div>

          {atRiskError && (
            <div className="flex items-center gap-2 rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700">
              <AlertTriangle className="h-4 w-4 shrink-0" /> {atRiskError}
            </div>
          )}

          {atRiskLoading && (
            <div className="flex items-center justify-center py-16">
              <Loader2 className="h-7 w-7 animate-spin text-gray-400" />
            </div>
          )}

          {!atRiskLoading && atRisk.length === 0 && atRiskError === "" && (
            <div className="rounded-xl border-2 border-dashed border-gray-200 py-12 text-center">
              <ShieldAlert className="h-8 w-8 text-gray-300 mx-auto mb-2" />
              <p className="text-sm text-gray-500">No at-risk learners identified, or click Identify At-Risk to check.</p>
            </div>
          )}

          {atRisk.length > 0 && !atRiskLoading && (
            <>
              <p className="text-sm text-orange-700 bg-orange-50 border border-orange-100 rounded-lg px-4 py-2.5">
                {atRisk.length} learner{atRisk.length !== 1 ? "s" : ""} flagged as at-risk for this term.
              </p>
              <div className="overflow-x-auto rounded-xl border border-gray-200 shadow-sm">
                <table className="w-full text-sm">
                  <thead className="bg-gray-50 border-b border-gray-200">
                    <tr className="text-xs font-semibold text-gray-400 uppercase tracking-wider">
                      <th className="px-4 py-3 text-left">Learner</th>
                      <th className="px-4 py-3 text-center">Overall Avg</th>
                      <th className="px-4 py-3 text-center">Attendance</th>
                      <th className="px-4 py-3 text-left">Risk Flags</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-100 bg-white">
                    {atRisk.map(s => (
                      <tr key={s.studentId} className="hover:bg-gray-50">
                        <td className="px-4 py-3">
                          <p className="font-medium text-gray-900">{s.name}</p>
                          <p className="text-xs text-gray-400">{s.studentNumber}</p>
                        </td>
                        <td className="px-4 py-3 text-center">
                          {s.overallAverage != null ? (
                            <span className={`font-semibold ${s.overallAverage < 40 ? "text-red-600" : s.overallAverage < 50 ? "text-orange-600" : "text-gray-700"}`}>
                              {s.overallAverage}%
                            </span>
                          ) : "—"}
                        </td>
                        <td className="px-4 py-3 text-center">
                          {s.attendancePercent != null ? (
                            <span className={`font-semibold ${s.attendancePercent < 80 ? "text-amber-700" : "text-emerald-700"}`}>
                              {s.attendancePercent}%
                            </span>
                          ) : "—"}
                        </td>
                        <td className="px-4 py-3">
                          <div className="flex flex-wrap gap-1">
                            {s.interventionBand && (
                              <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-[10px] font-bold ${
                                s.interventionBand === "Priority" ? "bg-red-100 text-red-800"
                                : s.interventionBand === "AtRisk" ? "bg-orange-100 text-orange-800"
                                : "bg-amber-100 text-amber-800"}`}>
                                {s.interventionBand === "AtRisk" ? "At Risk" : s.interventionBand}
                              </span>
                            )}
                            {s.riskFlags.map(flag => {
                              const meta = FLAG_LABELS[flag] ?? { label: flag, colour: "bg-gray-100 text-gray-700" };
                              return (
                                <span key={flag} className={`inline-flex items-center rounded-full px-2 py-0.5 text-[10px] font-semibold ${meta.colour}`}>
                                  {meta.label}
                                </span>
                              );
                            })}
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </>
          )}
        </div>
      )}

      {/* ── Tab: AI Comments ── */}
      {tab === "ai-comments" && (
        <div className="space-y-4">
          <div className="flex items-center justify-between">
            <p className="text-sm text-gray-600">
              Generate AI-powered professional report comments for at-risk learners.
            </p>
            {atRisk.length === 0 && (
              <Button onClick={loadAtRisk} loading={atRiskLoading} size="sm" variant="outline" className="gap-2">
                <ShieldAlert className="h-4 w-4" /> Load At-Risk
              </Button>
            )}
          </div>

          {atRiskLoading && (
            <div className="flex items-center justify-center py-16">
              <Loader2 className="h-7 w-7 animate-spin text-gray-400" />
            </div>
          )}

          {!atRiskLoading && atRisk.length === 0 && (
            <div className="rounded-xl border-2 border-dashed border-gray-200 py-12 text-center">
              <Sparkles className="h-8 w-8 text-gray-300 mx-auto mb-2" />
              <p className="text-sm text-gray-500">Load at-risk learners first, then generate AI comments per learner.</p>
            </div>
          )}

          {atRisk.length > 0 && !atRiskLoading && (
            <div className="space-y-3">
              {atRisk.map(student => (
                <ReportCommentCard
                  key={student.studentId}
                  student={student}
                  termId={termId}
                  termNumber={selectedTerm?.termNumber ?? 1}
                  year={selectedTerm?.year ?? new Date().getFullYear()}
                />
              ))}
            </div>
          )}
        </div>
      )}

      {/* ── Tab: Grade View (Grade Head oversight) ── */}
      {tab === "grade-oversight" && canGrade && <GradeHeadView grades={accessibleGrades} />}

      {/* ── Tab: Subject View (HOD oversight) ── */}
      {tab === "subject-oversight" && canSubject && <HodSubjectView allowedSubjectIds={allowedSubjectIds} />}

      {/* ── Tab: School Overview (Principal/Deputy) ── */}
      {tab === "school-overview" && canOverview && <SchoolOverviewView />}

      {/* ── Tab: Principal Summary (Admin only) ── */}
      {tab === "principal-summary" && isAdmin && (
        <div className="space-y-4">
          <p className="text-sm text-gray-600">
            Generate an AI executive summary of this class&apos;s performance for the term.
          </p>
          <PrincipalSummaryCard
            classId={classId}
            termId={termId}
            className={classes.find(c => c.classId === classId)?.name ?? "Class"}
            termNumber={selectedTerm?.termNumber ?? 1}
            year={selectedTerm?.year ?? new Date().getFullYear()}
          />
        </div>
      )}
    </div>
  );
}
