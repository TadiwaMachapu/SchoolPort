"use client";
import { useEffect, useState } from "react";
import Link from "next/link";
import { api } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";

interface Course {
  courseId: string;
  title: string;
  description?: string;
  thumbnailUrl?: string;
  isPublished: boolean;
  createdByName: string;
  createdAt: string;
  moduleCount: number;
  lessonCount: number;
}

export default function CoursesPage() {
  const [courses, setCourses] = useState<Course[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [showCreate, setShowCreate] = useState(false);
  const [newTitle, setNewTitle] = useState("");
  const [newDesc, setNewDesc] = useState("");
  const [creating, setCreating] = useState(false);

  async function load() {
    setLoading(true);
    setError("");
    try {
      const res = await api.courses.list({ page: 1, pageSize: 50 });
      setCourses(res.items);
      setTotal(res.total);
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Failed to load");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { load(); }, []);

  async function createCourse() {
    if (!newTitle.trim()) return;
    setCreating(true);
    try {
      await api.courses.create({ title: newTitle, description: newDesc });
      setNewTitle(""); setNewDesc(""); setShowCreate(false);
      load();
    } finally {
      setCreating(false);
    }
  }

  return (
    <div className="p-8">
      <div className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold text-gray-900">Courses</h1>
          <p className="text-gray-500 mt-1">{total} courses</p>
        </div>
        <Button onClick={() => setShowCreate(true)}>+ New Course</Button>
      </div>

      {error && <div className="mb-4 rounded-md bg-red-50 border border-red-200 p-3 text-sm text-red-700">{error}</div>}

      {showCreate && (
        <Card className="mb-6">
          <CardContent className="p-6 space-y-4">
            <h2 className="font-semibold text-gray-900">Create New Course</h2>
            <Input placeholder="Course title *" value={newTitle} onChange={e => setNewTitle(e.target.value)} />
            <textarea
              placeholder="Description (optional)"
              value={newDesc}
              onChange={e => setNewDesc(e.target.value)}
              className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              rows={3}
            />
            <div className="flex gap-2">
              <Button onClick={createCourse} loading={creating}>Create Course</Button>
              <Button variant="outline" onClick={() => setShowCreate(false)}>Cancel</Button>
            </div>
          </CardContent>
        </Card>
      )}

      {loading ? (
        <div className="flex justify-center py-12 text-gray-400">Loading…</div>
      ) : courses.length === 0 ? (
        <div className="rounded-lg border-2 border-dashed border-gray-300 py-16 text-center">
          <div className="text-5xl mb-4">📚</div>
          <p className="text-lg font-medium text-gray-700">No courses yet</p>
          <p className="text-sm text-gray-400 mt-1">Create your first course to get started</p>
          <Button className="mt-4" onClick={() => setShowCreate(true)}>Create Course</Button>
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {courses.map((c) => (
            <Link key={c.courseId} href={`/courses/${c.courseId}`}>
              <Card className="hover:shadow-md transition-shadow cursor-pointer h-full">
                {c.thumbnailUrl ? (
                  <img src={c.thumbnailUrl} alt={c.title} className="w-full h-36 object-cover rounded-t-lg" />
                ) : (
                  <div className="w-full h-36 bg-gradient-to-br from-blue-500 to-purple-600 rounded-t-lg flex items-center justify-center">
                    <span className="text-5xl">📚</span>
                  </div>
                )}
                <CardContent className="p-4">
                  <div className="flex items-start justify-between gap-2 mb-2">
                    <h3 className="font-semibold text-gray-900 line-clamp-2">{c.title}</h3>
                    <Badge variant={c.isPublished ? "success" : "outline"}>
                      {c.isPublished ? "Live" : "Draft"}
                    </Badge>
                  </div>
                  {c.description && <p className="text-sm text-gray-500 line-clamp-2 mb-3">{c.description}</p>}
                  <div className="flex items-center gap-4 text-xs text-gray-400">
                    <span>{c.moduleCount} modules</span>
                    <span>{c.lessonCount} lessons</span>
                  </div>
                  <p className="text-xs text-gray-400 mt-1">by {c.createdByName}</p>
                </CardContent>
              </Card>
            </Link>
          ))}
        </div>
      )}
    </div>
  );
}
