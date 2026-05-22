"use client";
import { useEffect, useState } from "react";
import { api, type GradeEntry, type ClassGradebook, type Class } from "@/lib/api";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { SkeletonTable } from "@/components/ui/skeleton";
import { getClientRole } from "@/lib/utils";
import { BarChart2, Users } from "lucide-react";

function gradeLetter(pct: number) {
  if (pct >= 90) return { letter: "A+", variant: "success" as const };
  if (pct >= 80) return { letter: "A",  variant: "success" as const };
  if (pct >= 70) return { letter: "B",  variant: "default" as const };
  if (pct >= 60) return { letter: "C",  variant: "warning" as const };
  if (pct >= 50) return { letter: "D",  variant: "warning" as const };
  return              { letter: "F",  variant: "destructive" as const };
}

export default function GradebookPage() {
  const [role, setRole] = useState("");

  useEffect(() => { setRole(getClientRole()); }, []);

  if (!role) return null;
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
      <div className="mb-6">
        <h1 className="text-2xl font-semibold text-gray-900 tracking-tight">My Gradebook</h1>
        <p className="text-sm text-gray-500 mt-1">Your grades and academic performance</p>
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
  const [classes,    setClasses]    = useState<Class[]>([]);
  const [classId,    setClassId]    = useState("");
  const [gradebook,  setGradebook]  = useState<ClassGradebook | null>(null);
  const [loading,    setLoading]    = useState(false);
  const [classLoading, setClassLoading] = useState(true);

  useEffect(() => {
    api.classes.list({ pageSize: 100 })
      .then(r => { setClasses(r.items); if (r.items.length > 0) setClassId(r.items[0].classId); })
      .catch(() => {})
      .finally(() => setClassLoading(false));
  }, []);

  useEffect(() => {
    if (!classId) return;
    setLoading(true);
    setGradebook(null);
    api.gradebook.classGradebook(classId)
      .then(setGradebook)
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [classId]);

  const selectedClass = classes.find(c => c.classId === classId);

  return (
    <div className="p-6 lg:p-8">
      <div className="mb-6 flex items-center justify-between flex-wrap gap-4">
        <div>
          <h1 className="text-2xl font-semibold text-gray-900 tracking-tight">Gradebook</h1>
          <p className="text-sm text-gray-500 mt-1">Class performance overview</p>
        </div>
        {!classLoading && classes.length > 0 && (
          <select value={classId} onChange={e => setClassId(e.target.value)}
            className="rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500">
            {classes.map(c => <option key={c.classId} value={c.classId}>{c.name}</option>)}
          </select>
        )}
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
                <p className="text-sm text-gray-500 mt-1">Students</p>
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
                          {gl ? (
                            <div className="flex flex-col items-center gap-1">
                              <Badge variant={gl.variant}>{gl.letter}</Badge>
                              <span className="text-xs text-gray-500">{avg}%</span>
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
