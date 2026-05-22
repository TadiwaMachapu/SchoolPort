"use client";
import { useEffect, useState } from "react";
import { api } from "@/lib/api";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";
import { SkeletonTable } from "@/components/ui/skeleton";

interface GradeEntry {
  gradeId: string;
  score: number;
  maxMarks: number;
  percentage: number;
  assignmentTitle: string;
  subject: string;
  class: string;
  feedback?: string;
  gradedAt: string;
}

function gradeLetter(pct: number) {
  if (pct >= 90) return { letter: "A+", variant: "success" as const };
  if (pct >= 80) return { letter: "A",  variant: "success" as const };
  if (pct >= 70) return { letter: "B",  variant: "default" as const };
  if (pct >= 60) return { letter: "C",  variant: "warning" as const };
  if (pct >= 50) return { letter: "D",  variant: "warning" as const };
  return              { letter: "F",  variant: "destructive" as const };
}

export default function GradebookPage() {
  const [grades, setGrades] = useState<GradeEntry[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [filterSubject, setFilterSubject] = useState("");

  useEffect(() => {
    api.gradebook.myGrades()
      .then(setGrades)
      .catch((e: unknown) => setError(e instanceof Error ? e.message : "Failed to load"))
      .finally(() => setLoading(false));
  }, []);

  const subjects = [...new Set(grades.map(g => g.subject))].sort();
  const filtered = filterSubject ? grades.filter(g => g.subject === filterSubject) : grades;

  const avg = filtered.length > 0
    ? Math.round(filtered.reduce((sum, g) => sum + g.percentage, 0) / filtered.length)
    : 0;

  return (
    <div className="p-8">
      <div className="mb-6">
        <h1 className="text-3xl font-bold text-gray-900">Gradebook</h1>
        <p className="text-gray-500 mt-1">Your grades and academic performance</p>
      </div>

      {error && <div className="mb-4 rounded-md bg-red-50 border border-red-200 p-3 text-sm text-red-700">{error}</div>}

      {!loading && grades.length > 0 && (
        <div className="grid grid-cols-3 gap-4 mb-6">
          <Card>
            <CardContent className="p-4 text-center">
              <p className="text-3xl font-bold text-blue-600">{avg}%</p>
              <p className="text-sm text-gray-500 mt-1">Overall Average</p>
            </CardContent>
          </Card>
          <Card>
            <CardContent className="p-4 text-center">
              <p className="text-3xl font-bold text-gray-900">{filtered.length}</p>
              <p className="text-sm text-gray-500 mt-1">Assignments Graded</p>
            </CardContent>
          </Card>
          <Card>
            <CardContent className="p-4 text-center">
              <p className="text-3xl font-bold text-green-600">{gradeLetter(avg).letter}</p>
              <p className="text-sm text-gray-500 mt-1">Overall Grade</p>
            </CardContent>
          </Card>
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
        <div className="rounded-lg border-2 border-dashed border-gray-300 py-16 text-center">
          <div className="text-5xl mb-4">📊</div>
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
                    <th key={h} className="px-6 py-3 text-left font-medium text-gray-500">{h}</th>
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
                      <td className="px-6 py-4">
                        <Badge variant={gl.variant}>{gl.letter}</Badge>
                      </td>
                      <td className="px-6 py-4 text-gray-500">{new Date(g.gradedAt).toLocaleDateString()}</td>
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
