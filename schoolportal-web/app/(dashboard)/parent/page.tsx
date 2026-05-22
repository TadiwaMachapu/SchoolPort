"use client";
import { useEffect, useState } from "react";
import { api, ParentChild, ParentGrade, ParentAttendanceSummary, ParentAssignment } from "@/lib/api";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";

function gradeLetter(pct: number) {
  if (pct >= 90) return { letter: "A+", color: "text-green-600" };
  if (pct >= 80) return { letter: "A",  color: "text-green-600" };
  if (pct >= 70) return { letter: "B",  color: "text-blue-600" };
  if (pct >= 60) return { letter: "C",  color: "text-yellow-600" };
  if (pct >= 50) return { letter: "D",  color: "text-orange-600" };
  return              { letter: "F",  color: "text-red-600" };
}

export default function ParentPortalPage() {
  const [children, setChildren] = useState<ParentChild[]>([]);
  const [selected, setSelected] = useState<ParentChild | null>(null);
  const [grades, setGrades] = useState<ParentGrade[]>([]);
  const [attendance, setAttendance] = useState<ParentAttendanceSummary | null>(null);
  const [assignments, setAssignments] = useState<ParentAssignment[]>([]);
  const [tab, setTab] = useState<"grades" | "attendance" | "assignments">("grades");
  const [loading, setLoading] = useState(true);
  const [childLoading, setChildLoading] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    api.parent.children()
      .then(c => {
        setChildren(c as ParentChild[]);
        if ((c as ParentChild[]).length > 0) selectChild((c as ParentChild[])[0]);
      })
      .catch(e => setError(String(e)))
      .finally(() => setLoading(false));
  }, []);

  async function selectChild(child: ParentChild) {
    setSelected(child);
    setChildLoading(true);
    setGrades([]);
    setAttendance(null);
    setAssignments([]);
    try {
      const [g, att, asgn] = await Promise.allSettled([
        api.parent.grades(child.studentId),
        api.parent.attendance(child.studentId),
        api.parent.assignments(child.studentId),
      ]);
      if (g.status === "fulfilled") setGrades(g.value as ParentGrade[]);
      if (att.status === "fulfilled") setAttendance(att.value as ParentAttendanceSummary);
      if (asgn.status === "fulfilled") setAssignments(asgn.value as ParentAssignment[]);
    } finally {
      setChildLoading(false);
    }
  }

  const avgGrade = grades.length > 0
    ? Math.round(grades.reduce((s, g) => s + g.percentage, 0) / grades.length)
    : null;

  const pending = assignments.filter(a => !a.isSubmitted && !a.isOverdue).length;
  const overdue = assignments.filter(a => a.isOverdue).length;

  if (loading) return <div className="p-8 text-gray-400 text-center py-16">Loading…</div>;

  if (error || children.length === 0) return (
    <div className="p-8 max-w-xl mx-auto">
      <h1 className="text-3xl font-bold text-gray-900 mb-2">Parent Portal</h1>
      <Card>
        <CardContent className="py-12 text-center text-gray-400">
          <div className="text-4xl mb-3">👨‍👩‍👧</div>
          <p className="font-medium text-gray-600">No children linked</p>
          <p className="text-sm mt-1">{error || "Contact the school administrator to link your children to your account."}</p>
        </CardContent>
      </Card>
    </div>
  );

  return (
    <div className="p-8 space-y-6">
      <div>
        <h1 className="text-3xl font-bold text-gray-900">Parent Portal</h1>
        <p className="text-gray-500 mt-1">Monitor your child's academic progress</p>
      </div>

      {/* Child selector */}
      {children.length > 1 && (
        <div className="flex gap-2 flex-wrap">
          {children.map(child => (
            <button key={child.studentId}
              onClick={() => selectChild(child)}
              className={`px-4 py-2 rounded-full text-sm font-medium transition-colors border
                ${selected?.studentId === child.studentId
                  ? "bg-blue-600 text-white border-blue-600"
                  : "bg-white text-gray-700 border-gray-300 hover:border-blue-400"}`}>
              {child.name}
            </button>
          ))}
        </div>
      )}

      {selected && (
        <>
          {/* Child header */}
          <Card>
            <CardContent className="p-5 flex items-center gap-4">
              <div className="w-14 h-14 rounded-full bg-blue-100 text-blue-700 font-bold text-lg flex items-center justify-center shrink-0">
                {selected.name.split(" ").map(n => n[0]).join("").slice(0, 2)}
              </div>
              <div className="flex-1">
                <h2 className="text-xl font-bold text-gray-900">{selected.name}</h2>
                <p className="text-sm text-gray-500">{selected.studentNumber}{selected.gradeLevel ? ` · Grade ${selected.gradeLevel}` : ""}</p>
                <p className="text-sm text-gray-400">{selected.email}</p>
              </div>
              <div className="grid grid-cols-3 gap-4 text-center">
                <div>
                  <p className={`text-2xl font-bold ${avgGrade !== null ? (avgGrade >= 70 ? "text-green-600" : avgGrade >= 50 ? "text-yellow-600" : "text-red-600") : "text-gray-400"}`}>
                    {avgGrade !== null ? `${avgGrade}%` : "—"}
                  </p>
                  <p className="text-xs text-gray-500">Avg Grade</p>
                </div>
                <div>
                  <p className={`text-2xl font-bold ${attendance?.attendanceRate && attendance.attendanceRate >= 90 ? "text-green-600" : "text-orange-500"}`}>
                    {attendance ? `${attendance.attendanceRate}%` : "—"}
                  </p>
                  <p className="text-xs text-gray-500">Attendance</p>
                </div>
                <div>
                  <p className={`text-2xl font-bold ${overdue > 0 ? "text-red-600" : "text-gray-400"}`}>{overdue}</p>
                  <p className="text-xs text-gray-500">Overdue</p>
                </div>
              </div>
            </CardContent>
          </Card>

          {/* Tabs */}
          <div className="flex gap-2">
            {(["grades", "attendance", "assignments"] as const).map(t => (
              <Button key={t} size="sm" variant={tab === t ? "default" : "outline"} onClick={() => setTab(t)} className="capitalize">
                {t}
              </Button>
            ))}
          </div>

          {childLoading && <div className="text-center text-gray-400 py-8">Loading…</div>}

          {/* Grades */}
          {!childLoading && tab === "grades" && (
            <Card>
              <CardContent className="p-0">
                {grades.length === 0 ? (
                  <div className="py-12 text-center text-gray-400">
                    <div className="text-3xl mb-2">📊</div>
                    <p className="text-sm">No grades recorded yet</p>
                  </div>
                ) : (
                  <table className="w-full text-sm">
                    <thead className="border-b bg-gray-50">
                      <tr>
                        {["Assignment", "Subject", "Class", "Score", "Grade", "Date"].map(h => (
                          <th key={h} className="px-4 py-3 text-left text-xs font-medium text-gray-500">{h}</th>
                        ))}
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-gray-100">
                      {grades.map(g => {
                        const gl = gradeLetter(g.percentage);
                        return (
                          <tr key={g.gradeId} className="hover:bg-gray-50">
                            <td className="px-4 py-3">
                              <p className="font-medium text-gray-900">{g.assignmentTitle}</p>
                              {g.feedback && <p className="text-xs text-gray-500 line-clamp-1 mt-0.5">{g.feedback}</p>}
                            </td>
                            <td className="px-4 py-3 text-gray-600">{g.subject}</td>
                            <td className="px-4 py-3 text-gray-600">{g.class}</td>
                            <td className="px-4 py-3">
                              <span className="font-semibold">{g.score}</span>
                              <span className="text-gray-400">/{g.maxMarks}</span>
                              <span className="text-xs text-gray-400 ml-1">({g.percentage}%)</span>
                            </td>
                            <td className="px-4 py-3 font-bold text-lg">
                              <span className={gl.color}>{gl.letter}</span>
                            </td>
                            <td className="px-4 py-3 text-gray-400 text-xs">{new Date(g.gradedAt).toLocaleDateString()}</td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                )}
              </CardContent>
            </Card>
          )}

          {/* Attendance */}
          {!childLoading && tab === "attendance" && attendance && (
            <div className="space-y-4">
              <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
                {[
                  { label: "Attendance Rate", value: `${attendance.attendanceRate}%`, color: attendance.attendanceRate >= 90 ? "text-green-600" : "text-orange-500" },
                  { label: "Present",  value: attendance.present,  color: "text-green-600" },
                  { label: "Absent",   value: attendance.absent,   color: "text-red-600" },
                  { label: "Late",     value: attendance.late,     color: "text-yellow-600" },
                ].map(s => (
                  <Card key={s.label}>
                    <CardContent className="p-4 text-center">
                      <p className={`text-2xl font-bold ${s.color}`}>{s.value}</p>
                      <p className="text-xs text-gray-500 mt-1">{s.label}</p>
                    </CardContent>
                  </Card>
                ))}
              </div>

              <Card>
                <CardContent className="p-0">
                  <table className="w-full text-sm">
                    <thead className="border-b bg-gray-50">
                      <tr>
                        {["Date", "Class", "Status", "Notes"].map(h => (
                          <th key={h} className="px-4 py-3 text-left text-xs font-medium text-gray-500">{h}</th>
                        ))}
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-gray-100">
                      {attendance.records.slice(0, 30).map(r => (
                        <tr key={r.attendanceId} className="hover:bg-gray-50">
                          <td className="px-4 py-3 text-gray-700">{new Date(r.date).toLocaleDateString("en-US", { weekday: "short", month: "short", day: "numeric" })}</td>
                          <td className="px-4 py-3 text-gray-600">{r.className}</td>
                          <td className="px-4 py-3">
                            <Badge variant={r.status === 1 ? "default" : r.status === 0 ? "destructive" : "warning"} className="text-xs">
                              {r.statusText}
                            </Badge>
                          </td>
                          <td className="px-4 py-3 text-gray-400 text-xs">{r.notes ?? "—"}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </CardContent>
              </Card>
            </div>
          )}

          {/* Assignments */}
          {!childLoading && tab === "assignments" && (
            <Card>
              <CardContent className="p-0">
                {assignments.length === 0 ? (
                  <div className="py-12 text-center text-gray-400">
                    <div className="text-3xl mb-2">📝</div>
                    <p className="text-sm">No assignments found</p>
                  </div>
                ) : (
                  <table className="w-full text-sm">
                    <thead className="border-b bg-gray-50">
                      <tr>
                        {["Assignment", "Subject", "Due Date", "Status"].map(h => (
                          <th key={h} className="px-4 py-3 text-left text-xs font-medium text-gray-500">{h}</th>
                        ))}
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-gray-100">
                      {assignments.map(a => (
                        <tr key={a.assignmentId} className="hover:bg-gray-50">
                          <td className="px-4 py-3 font-medium text-gray-900">{a.title}</td>
                          <td className="px-4 py-3 text-gray-600">{a.subject} · {a.class}</td>
                          <td className="px-4 py-3 text-gray-600 text-xs">
                            {new Date(a.dueAt).toLocaleDateString("en-US", { month: "short", day: "numeric", hour: "2-digit", minute: "2-digit" })}
                          </td>
                          <td className="px-4 py-3">
                            {a.isSubmitted ? (
                              <Badge variant="default" className="text-xs bg-green-100 text-green-700">Submitted</Badge>
                            ) : a.isOverdue ? (
                              <Badge variant="destructive" className="text-xs">Overdue</Badge>
                            ) : (
                              <Badge variant="outline" className="text-xs">Pending</Badge>
                            )}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </CardContent>
            </Card>
          )}
        </>
      )}
    </div>
  );
}
