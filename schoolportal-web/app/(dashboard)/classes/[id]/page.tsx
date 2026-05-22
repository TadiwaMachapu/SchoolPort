"use client";
import { useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { api, type Class, type User, type ClassSubject } from "@/lib/api";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";

export default function ClassDetailPage() {
  const { id }  = useParams<{ id: string }>();
  const router  = useRouter();
  const [cls,      setCls]      = useState<Class | null>(null);
  const [students, setStudents] = useState<User[]>([]);
  const [subjects, setSubjects] = useState<ClassSubject[]>([]);
  const [loading,  setLoading]  = useState(true);
  const [error,    setError]    = useState("");
  const [tab,      setTab]      = useState<"students" | "subjects">("students");

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

  if (loading) return (
    <div className="p-8 max-w-5xl mx-auto space-y-6">
      <Skeleton className="h-4 w-20" />
      <Skeleton className="h-10 w-64" />
      <div className="grid grid-cols-4 gap-4">
        {[1,2,3,4].map(i => <Skeleton key={i} className="h-20 rounded-lg" />)}
      </div>
      <Skeleton className="h-64 rounded-lg" />
    </div>
  );
  if (error || !cls) return <div className="p-8 text-red-600">{error || "Class not found"}</div>;

  const capacity  = cls.maxCapacity ?? 0;
  const fillPct   = capacity > 0 ? Math.round((cls.studentCount / capacity) * 100) : 0;
  const fillColor = fillPct >= 90 ? "bg-red-500" : fillPct >= 75 ? "bg-yellow-500" : "bg-blue-500";

  return (
    <div className="p-8 space-y-6 max-w-5xl mx-auto">
      {/* Back */}
      <button onClick={() => router.back()} className="flex items-center gap-1.5 text-sm text-gray-400 hover:text-gray-700 transition-colors">
        <svg className="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
        </svg>
        Back to Classes
      </button>

      {/* Header */}
      <div className="flex items-start justify-between flex-wrap gap-4">
        <div>
          <h1 className="text-3xl font-bold text-gray-900">{cls.name}</h1>
          <div className="flex flex-wrap items-center gap-2 mt-2">
            {cls.gradeLevel && <Badge variant="outline">Grade {cls.gradeLevel}</Badge>}
            {cls.academicYear && <Badge variant="outline">{cls.academicYear}</Badge>}
            {cls.teacherName && (
              <span className="text-sm text-gray-500">
                Teacher: <span className="font-medium text-gray-700">{cls.teacherName}</span>
              </span>
            )}
          </div>
        </div>
      </div>

      {/* Stats */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
        {[
          { label: "Students",  value: cls.studentCount, color: "text-blue-600" },
          { label: "Subjects",  value: subjects.length,  color: "text-purple-600" },
        ].map(s => (
          <Card key={s.label}>
            <CardContent className="p-4 text-center">
              <p className={`text-3xl font-bold ${s.color}`}>{s.value}</p>
              <p className="text-xs text-gray-500 mt-1">{s.label}</p>
            </CardContent>
          </Card>
        ))}
        {capacity > 0 && (
          <Card className="sm:col-span-2">
            <CardContent className="p-4">
              <div className="flex items-center justify-between mb-2">
                <p className="text-sm font-medium text-gray-700">Capacity</p>
                <p className="text-sm font-bold text-gray-900">{cls.studentCount} / {capacity}</p>
              </div>
              <div className="h-2.5 bg-gray-100 rounded-full overflow-hidden">
                <div className={`h-full rounded-full transition-all ${fillColor}`}
                  style={{ width: `${Math.min(fillPct, 100)}%` }} />
              </div>
              <p className="text-xs text-gray-400 mt-1.5">{fillPct}% full</p>
            </CardContent>
          </Card>
        )}
      </div>

      {/* Tabs */}
      <div className="flex gap-2 border-b border-gray-200 pb-0">
        {(["students", "subjects"] as const).map(t => (
          <button key={t} onClick={() => setTab(t)}
            className={`pb-3 px-1 text-sm font-medium capitalize transition-colors border-b-2 -mb-px ${
              tab === t ? "border-blue-600 text-blue-600" : "border-transparent text-gray-500 hover:text-gray-700"
            }`}>
            {t} ({t === "students" ? students.length : subjects.length})
          </button>
        ))}
      </div>

      {tab === "students" && (
        <Card>
          <CardContent className="p-0">
            {students.length === 0 ? (
              <div className="py-16 text-center text-gray-400">
                <div className="text-4xl mb-3">👤</div>
                <p className="text-sm font-medium text-gray-500">No students enrolled yet</p>
                <p className="text-xs text-gray-400 mt-1">Students can be enrolled from the Users page</p>
              </div>
            ) : (
              <table className="w-full text-sm">
                <thead className="border-b bg-gray-50">
                  <tr>
                    {["Name", "Email", "Status"].map(h => (
                      <th key={h} className="px-6 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wider">{h}</th>
                    ))}
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {students.map(s => (
                    <tr key={s.userId} className="hover:bg-gray-50">
                      <td className="px-6 py-3">
                        <div className="flex items-center gap-3">
                          <div className="h-8 w-8 rounded-full bg-blue-100 text-blue-700 text-xs font-bold flex items-center justify-center">
                            {s.firstName[0]}{s.lastName[0]}
                          </div>
                          <p className="font-medium text-gray-900">{s.firstName} {s.lastName}</p>
                        </div>
                      </td>
                      <td className="px-6 py-3 text-gray-500">{s.email}</td>
                      <td className="px-6 py-3">
                        <span className={`inline-flex items-center gap-1 text-xs font-medium ${s.isActive ? "text-green-700" : "text-gray-400"}`}>
                          <span className={`h-1.5 w-1.5 rounded-full ${s.isActive ? "bg-green-500" : "bg-gray-400"}`} />
                          {s.isActive ? "Active" : "Inactive"}
                        </span>
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
        subjects.length === 0 ? (
          <div className="rounded-xl border-2 border-dashed border-gray-300 py-16 text-center">
            <div className="text-4xl mb-3">📚</div>
            <p className="text-sm font-medium text-gray-500">No subjects assigned</p>
            <p className="text-xs text-gray-400 mt-1">Subjects can be assigned to this class by an admin</p>
          </div>
        ) : (
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            {subjects.map(sub => (
              <Card key={sub.classSubjectId} className="hover:shadow-md transition-shadow">
                <CardContent className="p-5 flex items-start gap-4">
                  <div className="h-10 w-10 rounded-lg bg-indigo-100 text-indigo-700 text-lg font-bold flex items-center justify-center shrink-0">
                    {sub.subjectName[0]}
                  </div>
                  <div>
                    <p className="font-semibold text-gray-900">{sub.subjectName}</p>
                    <p className="text-sm mt-0.5 text-gray-500">{sub.teacherName ?? <span className="italic text-gray-400">No teacher assigned</span>}</p>
                  </div>
                </CardContent>
              </Card>
            ))}
          </div>
        )
      )}
    </div>
  );
}
