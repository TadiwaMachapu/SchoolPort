"use client";
import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { api } from "@/lib/api";
import { useFeature } from "@/lib/use-feature";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";
import { SkeletonCards } from "@/components/ui/skeleton";
import { getClientRole } from "@/lib/utils";
import { BookOpen, Layers, PlayCircle } from "lucide-react";

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

const GRADIENTS = [
  "from-blue-500 to-purple-600",
  "from-emerald-500 to-teal-600",
  "from-orange-500 to-red-500",
  "from-pink-500 to-rose-600",
  "from-indigo-500 to-blue-600",
  "from-amber-500 to-orange-600",
];

export default function CoursesPage() {
  const router = useRouter();
  const hasVirtualClassroom = useFeature("virtualClassroom");
  const [courses,    setCourses]    = useState<Course[]>([]);
  const [total,      setTotal]      = useState(0);
  const [loading,    setLoading]    = useState(true);
  const [error,      setError]      = useState("");
  const [showCreate, setShowCreate] = useState(false);
  const [role,       setRole]       = useState("");

  useEffect(() => { setRole(getClientRole()); }, []);

  if (!hasVirtualClassroom) {
    return (
      <div className="flex flex-col items-center justify-center h-96 text-center px-4">
        <BookOpen className="h-12 w-12 text-gray-200 mb-4" />
        <h2 className="text-lg font-semibold text-gray-700">Virtual Classroom not enabled</h2>
        <p className="text-sm text-gray-400 mt-1">Enable the Virtual Classroom feature in Settings.</p>
        <button onClick={() => router.push("/settings")} className="mt-4 text-sm text-blue-600 hover:underline">Go to Settings</button>
      </div>
    );
  }

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

  const canCreate = role === "Admin" || role === "Teacher";

  return (
    <div className="p-6 lg:p-8">
      <div className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-gray-900 tracking-tight">Courses</h1>
          <p className="text-sm text-gray-500 mt-1">{total} course{total !== 1 ? "s" : ""}</p>
        </div>
        {canCreate && <Button onClick={() => setShowCreate(true)}>+ New Course</Button>}
      </div>

      {error && (
        <div className="mb-4 rounded-lg bg-red-50 border border-red-200 p-3 text-sm text-red-700">{error}</div>
      )}

      {loading ? (
        <SkeletonCards count={6} />
      ) : courses.length === 0 ? (
        <div className="rounded-xl border-2 border-dashed border-gray-300 py-16 text-center">
          <div className="flex justify-center mb-4">
            <BookOpen className="h-10 w-10 text-gray-300" />
          </div>
          <p className="text-lg font-medium text-gray-700">No courses yet</p>
          <p className="text-sm text-gray-400 mt-1">
            {canCreate ? "Create your first course to get started" : "Published courses from your teachers will appear here"}
          </p>
          {canCreate && <Button className="mt-4" onClick={() => setShowCreate(true)}>Create Course</Button>}
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3">
          {courses.map((c, i) => (
            <Link key={c.courseId} href={`/courses/${c.courseId}`} className="group">
              <Card className="overflow-hidden hover:shadow-lg transition-all duration-200 h-full">
                {c.thumbnailUrl ? (
                  <img src={c.thumbnailUrl} alt={c.title} className="w-full h-40 object-cover" />
                ) : (
                  <div className={`w-full h-40 bg-gradient-to-br ${GRADIENTS[i % GRADIENTS.length]} flex items-center justify-center`}>
                    <BookOpen className="h-10 w-10 text-white/80" />
                  </div>
                )}
                <CardContent className="p-5">
                  <div className="flex items-start justify-between gap-2 mb-2">
                    <h3 className="font-semibold text-gray-900 group-hover:text-blue-600 transition-colors line-clamp-2">{c.title}</h3>
                    <Badge variant={c.isPublished ? "success" : "outline"} className="shrink-0">
                      {c.isPublished ? "Live" : "Draft"}
                    </Badge>
                  </div>
                  {c.description && (
                    <p className="text-sm text-gray-500 line-clamp-2 mb-3">{c.description}</p>
                  )}
                  <div className="flex items-center justify-between text-xs text-gray-400 mt-auto pt-2 border-t border-gray-100">
                    <div className="flex items-center gap-3">
                      <span className="flex items-center gap-1">
                        <Layers className="h-3.5 w-3.5" />
                        {c.moduleCount} modules
                      </span>
                      <span className="flex items-center gap-1">
                        <PlayCircle className="h-3.5 w-3.5" />
                        {c.lessonCount} lessons
                      </span>
                    </div>
                    <span>{c.createdByName}</span>
                  </div>
                </CardContent>
              </Card>
            </Link>
          ))}
        </div>
      )}

      {showCreate && (
        <CreateCourseModal onClose={() => { setShowCreate(false); load(); }} />
      )}
    </div>
  );
}

function CreateCourseModal({ onClose }: { onClose: () => void }) {
  const [form,    setForm]    = useState({ title: "", description: "" });
  const [saving,  setSaving]  = useState(false);
  const [error,   setError]   = useState("");

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    if (!form.title.trim()) return;
    setSaving(true);
    setError("");
    try {
      await api.courses.create({ title: form.title, description: form.description || undefined });
      onClose();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : "Failed to create");
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm p-4">
      <div className="w-full max-w-md rounded-2xl bg-white shadow-2xl">
        <div className="flex items-center justify-between border-b border-gray-100 px-6 py-4">
          <h2 className="text-lg font-semibold text-gray-900">New Course</h2>
          <button onClick={onClose} className="rounded-full p-1 text-gray-400 hover:bg-gray-100 hover:text-gray-600 transition-colors">
            <svg className="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>
        <form onSubmit={submit} className="p-6 space-y-4">
          {error && <div className="rounded-lg bg-red-50 border border-red-200 p-3 text-sm text-red-700">{error}</div>}
          <div className="space-y-1.5">
            <label className="text-sm font-medium text-gray-700">Course title</label>
            <Input placeholder="e.g. Introduction to Algebra" value={form.title}
              onChange={e => setForm(f => ({ ...f, title: e.target.value }))} required autoFocus />
          </div>
          <div className="space-y-1.5">
            <label className="text-sm font-medium text-gray-700">Description <span className="text-gray-400 font-normal">(optional)</span></label>
            <textarea rows={3} placeholder="What will students learn?" value={form.description}
              onChange={e => setForm(f => ({ ...f, description: e.target.value }))}
              className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-blue-500 resize-none" />
          </div>
          <div className="flex gap-3 pt-1">
            <Button type="submit" className="flex-1" loading={saving}>Create Course</Button>
            <Button type="button" variant="outline" onClick={onClose}>Cancel</Button>
          </div>
        </form>
      </div>
    </div>
  );
}
