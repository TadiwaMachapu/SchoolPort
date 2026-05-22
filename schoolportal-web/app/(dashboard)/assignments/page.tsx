"use client";
import { useEffect, useState } from "react";
import Link from "next/link";
import { api, type Assignment } from "@/lib/api";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";

export default function AssignmentsPage() {
  const [assignments, setAssignments] = useState<Assignment[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);

  const [error, setError] = useState("");

  useEffect(() => {
    api.assignments.list({ pageSize: 50 })
      .then((res) => { setAssignments(res.items); setTotal(res.total); })
      .catch((e) => setError(e.message))
      .finally(() => setLoading(false));
  }, []);

  function dueBadge(dueAt: string) {
    const due = new Date(dueAt);
    const now = new Date();
    if (due < now) return <Badge variant="destructive">Overdue</Badge>;
    const diff = (due.getTime() - now.getTime()) / (1000 * 60 * 60 * 24);
    if (diff < 3) return <Badge variant="warning">Due soon</Badge>;
    return <Badge variant="success">Upcoming</Badge>;
  }

  return (
    <div className="p-8">
      <div className="mb-6">
        <h1 className="text-3xl font-bold text-gray-900">Assignments</h1>
        <p className="text-gray-500 mt-1">{total} assignments</p>
      </div>
      {error && <div className="mb-4 rounded-md bg-red-50 border border-red-200 p-3 text-sm text-red-700">{error}</div>}

      <Card>
        <CardContent className="p-0">
          {loading ? (
            <div className="flex justify-center py-12 text-gray-400">Loading…</div>
          ) : assignments.length === 0 ? (
            <div className="flex justify-center py-12 text-gray-400">No assignments yet</div>
          ) : (
            <table className="w-full text-sm">
              <thead className="border-b border-gray-200 bg-gray-50">
                <tr>
                  {["Title", "Subject", "Class", "Due Date", "Max Marks", "Status"].map((h) => (
                    <th key={h} className="px-6 py-3 text-left font-medium text-gray-500">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {assignments.map((a) => (
                  <tr key={a.assignmentId} className="hover:bg-gray-50">
                    <td className="px-6 py-4 font-medium text-gray-900">
                      <Link href={`/assignments/${a.assignmentId}`} className="hover:text-blue-600 hover:underline">
                        {a.title}
                      </Link>
                    </td>
                    <td className="px-6 py-4 text-gray-600">{a.subjectName ?? "—"}</td>
                    <td className="px-6 py-4 text-gray-600">{a.className ?? "—"}</td>
                    <td className="px-6 py-4 text-gray-600">{new Date(a.dueAt).toLocaleDateString()}</td>
                    <td className="px-6 py-4 text-gray-600">{a.maxMarks}</td>
                    <td className="px-6 py-4">{dueBadge(a.dueAt)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
