"use client";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { api, type GradeEntry, type ClassGradebook, type Class, type Term } from "@/lib/api";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { SkeletonTable } from "@/components/ui/skeleton";
import { getClientRole } from "@/lib/utils";
import { useFeature } from "@/lib/use-feature";
import { BarChart2, Download, Users } from "lucide-react";

function gradeLetter(pct: number) {
  if (pct >= 90) return { letter: "A+", variant: "success" as const };
  if (pct >= 80) return { letter: "A",  variant: "success" as const };
  if (pct >= 70) return { letter: "B",  variant: "default" as const };
  if (pct >= 60) return { letter: "C",  variant: "warning" as const };
  if (pct >= 50) return { letter: "D",  variant: "warning" as const };
  return              { letter: "F",  variant: "destructive" as const };
}

function capsLevel(pct: number): number {
  if (pct >= 80) return 7;
  if (pct >= 70) return 6;
  if (pct >= 60) return 5;
  if (pct >= 50) return 4;
  if (pct >= 40) return 3;
  if (pct >= 30) return 2;
  return 1;
}

const CAPS_LEVEL_LABEL: Record<number, string> = {
  7: "L7 Outstanding", 6: "L6 Meritorious", 5: "L5 Substantial",
  4: "L4 Adequate", 3: "L3 Moderate", 2: "L2 Elementary", 1: "L1 Not Achieved",
};

function printWindow(html: string) {
  const win = window.open("", "_blank", "width=900,height=700");
  if (!win) return;
  win.document.write(html);
  win.document.close();
  win.focus();
  setTimeout(() => { win.print(); }, 400);
}

function reportCardHtml(grades: GradeEntry[], avg: number) {
  const gl = gradeLetter(avg);
  const now = new Date().toLocaleDateString("en-US", { year: "numeric", month: "long", day: "numeric" });
  const rows = grades.map(g => {
    const { letter } = gradeLetter(g.percentage);
    return `<tr>
      <td style="padding:8px 12px;border-bottom:1px solid #e5e7eb">${g.assignmentTitle}</td>
      <td style="padding:8px 12px;border-bottom:1px solid #e5e7eb;color:#6b7280">${g.subject}</td>
      <td style="padding:8px 12px;border-bottom:1px solid #e5e7eb;color:#6b7280">${g.class}</td>
      <td style="padding:8px 12px;border-bottom:1px solid #e5e7eb;text-align:center;font-weight:600">${g.score}/${g.maxMarks}</td>
      <td style="padding:8px 12px;border-bottom:1px solid #e5e7eb;text-align:center;color:#6b7280">${g.percentage}%</td>
      <td style="padding:8px 12px;border-bottom:1px solid #e5e7eb;text-align:center;font-weight:700">${letter}</td>
    </tr>`;
  }).join("");

  return `<!DOCTYPE html><html><head><title>Report Card</title>
  <style>
    body{font-family:system-ui,sans-serif;margin:0;padding:32px;color:#111827}
    h1{margin:0;font-size:22px;font-weight:700}
    .meta{color:#6b7280;font-size:13px;margin-top:4px}
    .badge{display:inline-block;padding:4px 14px;border-radius:9999px;font-size:22px;font-weight:800;background:#dcfce7;color:#166534;margin-top:8px}
    table{width:100%;border-collapse:collapse;margin-top:24px;font-size:13px}
    thead th{background:#f9fafb;padding:8px 12px;text-align:left;font-size:11px;text-transform:uppercase;letter-spacing:.05em;color:#6b7280;border-bottom:2px solid #e5e7eb}
    .summary{display:flex;gap:24px;margin-top:24px}
    .stat{flex:1;border:1px solid #e5e7eb;border-radius:12px;padding:16px;text-align:center}
    .stat-val{font-size:28px;font-weight:700}
    .stat-lbl{font-size:12px;color:#6b7280;margin-top:4px}
    @media print{@page{margin:16mm}}
  </style></head><body>
  <div style="display:flex;align-items:center;justify-content:space-between;border-bottom:2px solid #e5e7eb;padding-bottom:16px">
    <div>
      <h1>Report Card</h1>
      <div class="meta">Generated ${now}</div>
    </div>
    <div style="text-align:right">
      <div style="font-size:12px;color:#6b7280;text-transform:uppercase;letter-spacing:.05em">Overall Grade</div>
      <div class="badge">${gl.letter}</div>
    </div>
  </div>
  <div class="summary">
    <div class="stat"><div class="stat-val" style="color:#2563eb">${avg}%</div><div class="stat-lbl">Overall Average</div></div>
    <div class="stat"><div class="stat-val">${grades.length}</div><div class="stat-lbl">Assignments Graded</div></div>
    <div class="stat"><div class="stat-val" style="color:#16a34a">${gl.letter}</div><div class="stat-lbl">Overall Grade</div></div>
  </div>
  <table>
    <thead><tr>
      <th>Assignment</th><th>Subject</th><th>Class</th>
      <th style="text-align:center">Score</th>
      <th style="text-align:center">Percent</th>
      <th style="text-align:center">Grade</th>
    </tr></thead>
    <tbody>${rows}</tbody>
  </table>
  </body></html>`;
}

function classGradebookHtml(gradebook: ClassGradebook, className: string) {
  const now = new Date().toLocaleDateString("en-US", { year: "numeric", month: "long", day: "numeric" });
  const avgs = gradebook.students.map(s => s.average).filter((a): a is number => a != null);
  const classAvg = avgs.length ? Math.round(avgs.reduce((a, b) => a + b, 0) / avgs.length) : null;

  const headerCells = gradebook.assignments.map(a =>
    `<th style="padding:8px;font-size:11px;text-transform:uppercase;color:#6b7280;border-bottom:2px solid #e5e7eb;text-align:center;min-width:72px">${a.title}<br/><span style="font-weight:400">/${a.maxMarks}</span></th>`
  ).join("");

  const rows = gradebook.students.map(s => {
    const avg = s.average != null ? Math.round(s.average) : null;
    const gl  = avg != null ? gradeLetter(avg) : null;
    const cells = s.grades.map(g =>
      g.score != null
        ? `<td style="padding:8px;text-align:center;font-weight:600">${g.score}<span style="color:#9ca3af;font-size:11px">/${g.maxMarks}</span></td>`
        : `<td style="padding:8px;text-align:center;color:#d1d5db">—</td>`
    ).join("");
    return `<tr>
      <td style="padding:8px 12px;font-weight:600;border-bottom:1px solid #f3f4f6">${s.name}<br/><span style="font-size:11px;color:#9ca3af;font-weight:400">${s.studentNumber}</span></td>
      ${cells}
      <td style="padding:8px;text-align:center;font-weight:800;border-bottom:1px solid #f3f4f6">${gl ? `${gl.letter} (${avg}%)` : "—"}</td>
    </tr>`;
  }).join("");

  return `<!DOCTYPE html><html><head><title>Gradebook — ${className}</title>
  <style>
    body{font-family:system-ui,sans-serif;margin:0;padding:32px;color:#111827;font-size:13px}
    h1{margin:0;font-size:20px;font-weight:700}
    table{width:100%;border-collapse:collapse;margin-top:20px}
    thead{background:#f9fafb}
    @media print{@page{margin:10mm;size:landscape}}
  </style></head><body>
  <div style="display:flex;justify-content:space-between;border-bottom:2px solid #e5e7eb;padding-bottom:12px">
    <div><h1>${className} — Gradebook</h1><div style="color:#6b7280;font-size:12px;margin-top:4px">Generated ${now}</div></div>
    <div style="text-align:right;font-size:13px">
      ${classAvg != null ? `<div style="font-weight:700;font-size:20px;color:#16a34a">${classAvg}%</div><div style="color:#6b7280;font-size:12px">Class Average</div>` : ""}
    </div>
  </div>
  <table>
    <thead><tr>
      <th style="padding:8px 12px;font-size:11px;text-transform:uppercase;color:#6b7280;border-bottom:2px solid #e5e7eb;text-align:left">Student</th>
      ${headerCells}
      <th style="padding:8px;font-size:11px;text-transform:uppercase;color:#6b7280;border-bottom:2px solid #e5e7eb;text-align:center">Average</th>
    </tr></thead>
    <tbody>${rows}</tbody>
  </table>
  </body></html>`;
}

export default function GradebookPage() {
  const [role, setRole] = useState("");
  const router = useRouter();
  const hasGradebook = useFeature("gradebook");

  useEffect(() => { setRole(getClientRole()); }, []);

  if (!role) return null;
  if (!hasGradebook) {
    return (
      <div className="flex flex-col items-center justify-center h-96 text-center px-4">
        <BarChart2 className="h-12 w-12 text-gray-200 mb-4" />
        <h2 className="text-lg font-semibold text-gray-700">Gradebook not enabled</h2>
        <p className="text-sm text-gray-400 mt-1">Enable the Gradebook feature in Settings to use this page.</p>
        <button onClick={() => router.push("/settings")} className="mt-4 text-sm text-blue-600 hover:underline">Go to Settings</button>
      </div>
    );
  }
  if (role === "Student") return <StudentGradebook />;
  return <TeacherGradebook />;
}

/* ─── Student view ─────────────────────────────────────────────── */
function StudentGradebook() {
  const [grades,        setGrades]        = useState<GradeEntry[]>([]);
  const [loading,       setLoading]       = useState(true);
  const [error,         setError]         = useState("");
  const [filterSubject, setFilterSubject] = useState("");

  useEffect(() => {
    api.gradebook.myGrades()
      .then(setGrades)
      .catch((e: unknown) => setError(e instanceof Error ? e.message : "Failed to load"))
      .finally(() => setLoading(false));
  }, []);

  const subjects  = [...new Set(grades.map(g => g.subject))].sort();
  const filtered  = filterSubject ? grades.filter(g => g.subject === filterSubject) : grades;
  const avg       = filtered.length > 0
    ? Math.round(filtered.reduce((s, g) => s + g.percentage, 0) / filtered.length) : 0;

  return (
    <div className="p-6 lg:p-8">
      <div className="mb-6 flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold text-gray-900 tracking-tight">My Gradebook</h1>
          <p className="text-sm text-gray-500 mt-1">Your grades and academic performance</p>
        </div>
        {!loading && grades.length > 0 && (
          <Button variant="outline" onClick={() => printWindow(reportCardHtml(filtered, avg))} className="shrink-0 flex items-center gap-2">
            <Download className="h-4 w-4" />
            Print Report Card
          </Button>
        )}
      </div>

      {error && <div className="mb-4 rounded-lg bg-red-50 border border-red-200 p-3 text-sm text-red-700">{error}</div>}

      {!loading && grades.length > 0 && (
        <div className="grid grid-cols-3 gap-4 mb-6">
          {[
            { label: "Overall Average", value: `${avg}%`,            color: "text-blue-600" },
            { label: "Graded",          value: filtered.length,       color: "text-gray-900" },
            { label: "Overall Grade",   value: gradeLetter(avg).letter, color: "text-green-600" },
          ].map(s => (
            <Card key={s.label}>
              <CardContent className="p-4 text-center">
                <p className={`text-3xl font-bold ${s.color}`}>{s.value}</p>
                <p className="text-sm text-gray-500 mt-1">{s.label}</p>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      {subjects.length > 1 && (
        <div className="mb-4">
          <select value={filterSubject} onChange={e => setFilterSubject(e.target.value)}
            className="rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500">
            <option value="">All subjects</option>
            {subjects.map(s => <option key={s}>{s}</option>)}
          </select>
        </div>
      )}

      {loading ? (
        <SkeletonTable rows={6} cols={6} />
      ) : filtered.length === 0 ? (
        <div className="rounded-xl border-2 border-dashed border-gray-300 py-16 text-center">
          <div className="flex justify-center mb-4">
            <BarChart2 className="h-10 w-10 text-gray-300" />
          </div>
          <p className="text-lg font-medium text-gray-700">No grades yet</p>
          <p className="text-sm text-gray-400 mt-1">Grades will appear here once assignments are marked</p>
        </div>
      ) : (
        <Card>
          <CardContent className="p-0">
            <table className="w-full text-sm">
              <thead className="border-b border-gray-200 bg-gray-50">
                <tr>
                  {["Assignment", "Subject", "Class", "Score", "Grade", "Graded On"].map(h => (
                    <th key={h} className="px-6 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wider">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {filtered.map(g => {
                  const gl = gradeLetter(g.percentage);
                  return (
                    <tr key={g.gradeId} className="hover:bg-gray-50">
                      <td className="px-6 py-4">
                        <p className="font-medium text-gray-900">{g.assignmentTitle}</p>
                        {g.feedback && <p className="text-xs text-gray-500 mt-0.5 line-clamp-1">{g.feedback}</p>}
                      </td>
                      <td className="px-6 py-4 text-gray-600">{g.subject}</td>
                      <td className="px-6 py-4 text-gray-600">{g.class}</td>
                      <td className="px-6 py-4">
                        <span className="font-semibold text-gray-900">{g.score}</span>
                        <span className="text-gray-400">/{g.maxMarks}</span>
                        <span className="text-xs text-gray-500 ml-1">({g.percentage}%)</span>
                      </td>
                      <td className="px-6 py-4"><Badge variant={gl.variant}>{gl.letter}</Badge></td>
                      <td className="px-6 py-4 text-gray-500 text-xs">{new Date(g.gradedAt).toLocaleDateString()}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </CardContent>
        </Card>
      )}
    </div>
  );
}

/* ─── Teacher / Admin view ─────────────────────────────────────── */
function TeacherGradebook() {
  const [classes,      setClasses]      = useState<Class[]>([]);
  const [terms,        setTerms]        = useState<Term[]>([]);
  const [classId,      setClassId]      = useState("");
  const [termId,       setTermId]       = useState("");
  const [gradebook,    setGradebook]    = useState<ClassGradebook | null>(null);
  const [loading,      setLoading]      = useState(false);
  const [classLoading, setClassLoading] = useState(true);

  useEffect(() => {
    api.classes.list({ pageSize: 100 })
      .then(r => { setClasses(r.items); if (r.items.length > 0) setClassId(r.items[0].classId); })
      .catch(() => {})
      .finally(() => setClassLoading(false));
    api.terms.list().then(setTerms).catch(() => {});
  }, []);

  useEffect(() => {
    if (!classId) return;
    setLoading(true);
    setGradebook(null);
    api.gradebook.classGradebook(classId, termId || undefined)
      .then(setGradebook)
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [classId, termId]);

  const selectedClass = classes.find(c => c.classId === classId);

  return (
    <div className="p-6 lg:p-8">
      <div className="mb-6 flex items-center justify-between flex-wrap gap-4">
        <div>
          <h1 className="text-2xl font-semibold text-gray-900 tracking-tight">Gradebook</h1>
          <p className="text-sm text-gray-500 mt-1">Class performance overview</p>
        </div>
        <div className="flex items-center gap-3 flex-wrap">
          {terms.length > 0 && (
            <select value={termId} onChange={e => setTermId(e.target.value)}
              className="rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500">
              <option value="">All terms</option>
              {terms.map(t => (
                <option key={t.termId} value={t.termId}>
                  {t.isCurrent ? "★ " : ""}Term {t.termNumber} {t.year}
                </option>
              ))}
            </select>
          )}
          {!classLoading && classes.length > 0 && (
            <select value={classId} onChange={e => setClassId(e.target.value)}
              className="rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500">
              {classes.map(c => <option key={c.classId} value={c.classId}>{c.name}</option>)}
            </select>
          )}
          {gradebook && selectedClass && (
            <Button variant="outline" onClick={() => printWindow(classGradebookHtml(gradebook, selectedClass.name))} className="flex items-center gap-2">
              <Download className="h-4 w-4" />
              Export PDF
            </Button>
          )}
        </div>
      </div>

      {classLoading || loading ? (
        <SkeletonTable rows={6} cols={5} />
      ) : !gradebook ? (
        <div className="rounded-xl border-2 border-dashed border-gray-300 py-16 text-center">
          <div className="flex justify-center mb-4">
            <BarChart2 className="h-10 w-10 text-gray-300" />
          </div>
          <p className="text-lg font-medium text-gray-700">No gradebook data</p>
          <p className="text-sm text-gray-400 mt-1">Grades will appear here once assignments are created and marked</p>
        </div>
      ) : gradebook.students.length === 0 ? (
        <div className="rounded-xl border-2 border-dashed border-gray-300 py-16 text-center">
          <div className="flex justify-center mb-4">
            <Users className="h-10 w-10 text-gray-300" />
          </div>
          <p className="text-lg font-medium text-gray-700">No students enrolled</p>
          <p className="text-sm text-gray-400 mt-1">Students need to be enrolled in {selectedClass?.name} to appear here</p>
        </div>
      ) : (
        <div className="space-y-4">
          {/* Summary cards */}
          <div className="grid grid-cols-3 gap-4">
            <Card>
              <CardContent className="p-4 text-center">
                <p className="text-3xl font-bold text-blue-600">{gradebook.students.length}</p>
                <p className="text-sm text-gray-500 mt-1">Learners</p>
              </CardContent>
            </Card>
            <Card>
              <CardContent className="p-4 text-center">
                <p className="text-3xl font-bold text-gray-900">{gradebook.assignments.length}</p>
                <p className="text-sm text-gray-500 mt-1">Assignments</p>
              </CardContent>
            </Card>
            <Card>
              <CardContent className="p-4 text-center">
                {(() => {
                  const avgs = gradebook.students.map(s => s.average).filter(a => a != null) as number[];
                  const classAvg = avgs.length > 0 ? Math.round(avgs.reduce((a, b) => a + b, 0) / avgs.length) : null;
                  return (
                    <>
                      <p className="text-3xl font-bold text-green-600">{classAvg !== null ? `${classAvg}%` : "—"}</p>
                      <p className="text-sm text-gray-500 mt-1">Class Average</p>
                    </>
                  );
                })()}
              </CardContent>
            </Card>
          </div>

          {/* Grade matrix */}
          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="text-base">{selectedClass?.name} — Grade Matrix</CardTitle>
            </CardHeader>
            <CardContent className="p-0 overflow-x-auto">
              <table className="w-full text-sm">
                <thead className="border-b border-gray-200 bg-gray-50">
                  <tr>
                    <th className="px-4 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wider sticky left-0 bg-gray-50 min-w-[160px]">
                      Student
                    </th>
                    {gradebook.assignments.map(a => (
                      <th key={a.assignmentId} className="px-3 py-3 text-center text-xs font-semibold text-gray-500 min-w-[90px]">
                        <div className="truncate max-w-[80px]" title={a.title}>{a.title}</div>
                        <div className="text-gray-400 font-normal">/{a.maxMarks}</div>
                      </th>
                    ))}
                    <th className="px-4 py-3 text-center text-xs font-semibold text-gray-500 uppercase tracking-wider min-w-[80px]">
                      Average
                    </th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {gradebook.students.map(s => {
                    const avg = s.average != null ? Math.round(s.average) : null;
                    const gl  = avg != null ? gradeLetter(avg) : null;
                    return (
                      <tr key={s.studentId} className="hover:bg-gray-50">
                        <td className="px-4 py-3 sticky left-0 bg-white hover:bg-gray-50">
                          <p className="font-medium text-gray-900">{s.name}</p>
                          <p className="text-xs text-gray-400">{s.studentNumber}</p>
                        </td>
                        {s.grades.map((g, i) => (
                          <td key={i} className="px-3 py-3 text-center">
                            {g.score != null ? (
                              <div>
                                <span className="font-semibold text-gray-900">{g.score}</span>
                                <span className="text-gray-400 text-xs">/{g.maxMarks}</span>
                                <div className="text-xs text-gray-500 mt-0.5">{g.percentage}%</div>
                              </div>
                            ) : (
                              <span className="text-gray-300 text-xs">—</span>
                            )}
                          </td>
                        ))}
                        <td className="px-4 py-3 text-center">
                          {gl && avg != null ? (
                            <div className="flex flex-col items-center gap-1">
                              <Badge variant={gl.variant}>{gl.letter}</Badge>
                              <span className="text-xs text-gray-500">{avg}%</span>
                              <span className="text-[10px] text-purple-600 font-medium">{CAPS_LEVEL_LABEL[capsLevel(avg)]}</span>
                            </div>
                          ) : (
                            <span className="text-gray-300 text-xs">—</span>
                          )}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </CardContent>
          </Card>
        </div>
      )}
    </div>
  );
}
