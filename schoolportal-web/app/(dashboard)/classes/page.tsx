"use client";
import { useEffect, useState } from "react";
import Link from "next/link";
import { api, type Class } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";

export default function ClassesPage() {
  const [classes, setClasses] = useState<Class[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);

  async function load() {
    setLoading(true);
    try {
      const res = await api.classes.list({ pageSize: 50 });
      setClasses(res.items);
      setTotal(res.total);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { load(); }, []);

  return (
    <div className="p-8">
      <div className="mb-6">
        <h1 className="text-3xl font-bold text-gray-900">Classes</h1>
        <p className="text-gray-500 mt-1">{total} classes</p>
      </div>

      <Card>
        <CardContent className="p-0">
          {loading ? (
            <div className="flex justify-center py-12 text-gray-400">Loading…</div>
          ) : (
            <table className="w-full text-sm">
              <thead className="border-b border-gray-200 bg-gray-50">
                <tr>
                  {["Class Name", "Grade", "Year", "Teacher", "Students", "Capacity"].map((h) => (
                    <th key={h} className="px-6 py-3 text-left font-medium text-gray-500">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {classes.map((c) => (
                  <tr key={c.classId} className="hover:bg-gray-50">
                    <td className="px-6 py-4 font-medium text-gray-900">
                      <Link href={`/classes/${c.classId}`} className="hover:text-blue-600 hover:underline">
                        {c.name}
                      </Link>
                    </td>
                    <td className="px-6 py-4 text-gray-600">{c.gradeLevel ?? "—"}</td>
                    <td className="px-6 py-4 text-gray-600">{c.academicYear ?? "—"}</td>
                    <td className="px-6 py-4 text-gray-600">{c.teacherName ?? "Unassigned"}</td>
                    <td className="px-6 py-4">
                      <Badge variant="default">{c.studentCount} students</Badge>
                    </td>
                    <td className="px-6 py-4 text-gray-600">{c.maxCapacity ?? "—"}</td>
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
