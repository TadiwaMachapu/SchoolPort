"use client";
import { useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { api, Class, User, ClassSubject } from "@/lib/api";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";

export default function ClassDetailPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const [cls, setCls] = useState<Class | null>(null);
  const [students, setStudents] = useState<User[]>([]);
  const [subjects, setSubjects] = useState<ClassSubject[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [tab, setTab] = useState<"students" | "subjects">("students");

  useEffect(() => {
    Promise.allSettled([
      api.classes.get(id),
      api.classes.students(id),
      api.classes.subjects(id),
    ]).then(([c, s, sub]) => {
      if (c.status === "fulfilled") setCls(c.value as Class);
      else setError("Class not found");
      if (s.status === "fulfilled") setStudents(s.value as User[]);
      if (sub.status === "fulfilled") setSubjects(sub.value as ClassSubject[]);
    }).catch(e => setError(String(e)))
      .finally(() => setLoading(false));
  }, [id]);

  if (loading) return <div className="p-8 text-gray-400 text-center py-16">Loading…</div>;
  if (error || !cls) return <div className="p-8 text-red-600">{error || "Class not found"}</div>;

  const capacity = cls.maxCapacity ?? 0;
  const fillPct = capacity > 0 ? Math.round((cls.studentCount / capacity) * 100) : 0;

  return (
    <div className="p-8 space-y-6 max-w-5xl mx-auto">
      <div className="flex items-start justify-between">
        <div>
          <Button variant="ghost" size="sm" className="mb-2 -ml-2 text-gray-500" onClick={() => router.back()}>
            ← Back
          </Button>
          <h1 className="text-3xl font-bold text-gray-900">{cls.name}</h1>
          <div className="flex items-center gap-3 mt-1 flex-wrap">
            {cls.gradeLevel && <Badge variant="outline">Grade {cls.gradeLevel}</Badge>}
            {cls.academicYear && <Badge variant="outline">{cls.academicYear}</Badge>}
            {cls.teacherName && (
              <span className="text-sm text-gray-500">Teacher: <span className="font-medium text-gray-700">{cls.teacherName}</span></span>
            )}
          </div>
        </div>
      </div>

      {/* Stats row */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
        <Card>
          <CardContent className="p-4">
            <p className="text-xs text-gray-500">Students</p>
            <p className="text-2xl font-bold text-gray-900">{cls.studentCount}</p>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="p-4">
            <p className="text-xs text-gray-500">Subjects</p>
            <p className="text-2xl font-bold text-gray-900">{subjects.length}</p>
          </CardContent>
        </Card>
        {capacity > 0 && (
          <Card className="sm:col-span-2">
            <CardContent className="p-4">
              <div className="flex items-center justify-between mb-1">
                <p className="text-xs text-gray-500">Capacity</p>
                <p className="text-xs font-medium text-gray-700">{cls.studentCount}/{capacity}</p>
              </div>
              <div className="h-2 bg-gray-200 rounded-full overflow-hidden">
                <div
                  className={`h-2 rounded-full transition-all ${fillPct >= 90 ? "bg-red-500" : fillPct >= 75 ? "bg-yellow-500" : "bg-green-500"}`}
                  style={{ width: `${Math.min(fillPct, 100)}%` }}
                />
              </div>
              <p className="text-xs text-gray-400 mt-1">{fillPct}% full</p>
            </CardContent>
          </Card>
        )}
      </div>

      {/* Tabs */}
      <div className="flex gap-2">
        <Button variant={tab === "students" ? "default" : "outline"} size="sm" onClick={() => setTab("students")}>
          Students ({students.length})
        </Button>
        <Button variant={tab === "subjects" ? "default" : "outline"} size="sm" onClick={() => setTab("subjects")}>
          Subjects ({subjects.length})
        </Button>
      </div>

      {tab === "students" && (
        <Card>
          <CardContent className="p-0">
            {students.length === 0 ? (
              <div className="py-12 text-center text-gray-400">
                <div className="text-3xl mb-2">👤</div>
                <p className="text-sm">No students enrolled yet</p>
              </div>
            ) : (
              <table className="w-full text-sm">
                <thead className="border-b bg-gray-50">
                  <tr>
                    {["Name", "Email", "Status"].map(h => (
                      <th key={h} className="px-6 py-3 text-left font-medium text-gray-500 text-xs">{h}</th>
                    ))}
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {students.map(s => (
                    <tr key={s.userId} className="hover:bg-gray-50">
                      <td className="px-6 py-3">
                        <div className="flex items-center gap-3">
                          <div className="w-8 h-8 rounded-full bg-blue-100 text-blue-700 text-xs font-bold flex items-center justify-center">
                            {s.firstName[0]}{s.lastName[0]}
                          </div>
                          <div>
                            <p className="font-medium text-gray-900">{s.firstName} {s.lastName}</p>
                          </div>
                        </div>
                      </td>
                      <td className="px-6 py-3 text-gray-500">{s.email}</td>
                      <td className="px-6 py-3">
                        <Badge variant={s.isActive ? "default" : "secondary"}>
                          {s.isActive ? "Active" : "Inactive"}
                        </Badge>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </CardContent>
        </Card>
      )}

      {tab === "subjects" && (
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          {subjects.length === 0 ? (
            <Card className="sm:col-span-2">
              <CardContent className="py-12 text-center text-gray-400">
                <div className="text-3xl mb-2">📚</div>
                <p className="text-sm">No subjects assigned to this class</p>
              </CardContent>
            </Card>
          ) : (
            subjects.map(sub => (
              <Card key={sub.classSubjectId} className="hover:shadow-md transition-shadow">
                <CardContent className="p-5 flex items-start gap-4">
                  <div className="w-10 h-10 rounded-lg bg-indigo-100 text-indigo-700 text-lg font-bold flex items-center justify-center shrink-0">
                    {sub.subjectName[0]}
                  </div>
                  <div>
                    <p className="font-semibold text-gray-900">{sub.subjectName}</p>
                    {sub.teacherName ? (
                      <p className="text-sm text-gray-500 mt-0.5">
                        {sub.teacherName}
                      </p>
                    ) : (
                      <p className="text-sm text-gray-400 mt-0.5 italic">No teacher assigned</p>
                    )}
                  </div>
                </CardContent>
              </Card>
            ))
          )}
        </div>
      )}
    </div>
  );
}
