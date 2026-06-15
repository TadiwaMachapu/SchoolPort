"use client";
import { useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { api } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { usePermission } from "@/lib/auth-context";
import { Layers, PlayCircle } from "lucide-react";

interface Lesson {
  lessonId: string; title: string; type: string;
  content?: string; videoUrl?: string; fileUrl?: string; externalUrl?: string;
  order: number; durationMinutes?: number; isPublished: boolean;
}
interface Module {
  moduleId: string; title: string; description?: string; order: number; lessons: Lesson[];
}
interface Course {
  courseId: string; title: string; description?: string; isPublished: boolean;
  createdByName: string; createdAt: string; moduleCount: number; lessonCount: number; modules: Module[];
}

const LESSON_ICONS: Record<string, string> = { RichText: "📄", Video: "▶️", PDF: "📋", Link: "🔗" };
const LESSON_TYPES = ["RichText", "Video", "PDF", "Link"] as const;

export default function CourseDetailPage() {
  const { id }    = useParams<{ id: string }>();
  const router    = useRouter();
  const [course,          setCourse]         = useState<Course | null>(null);
  const [loading,         setLoading]        = useState(true);
  const [error,           setError]          = useState("");
  const canEdit = usePermission("courses.manage"); // Step 8
  const [addingModule,    setAddingModule]   = useState(false);
  const [moduleTitle,     setModuleTitle]    = useState("");
  const [expandedModule,  setExpandedModule] = useState<string | null>(null);
  const [addingLesson,    setAddingLesson]   = useState<string | null>(null);
  const [lessonForm, setLessonForm]          = useState({ title: "", type: "RichText", content: "", videoUrl: "", externalUrl: "" });
  const [saving, setSaving] = useState(false);


  async function load() {
    setLoading(true);
    try {
      const c = await api.courses.get(id) as Course;
      setCourse(c);
      if (c.modules.length > 0) setExpandedModule(c.modules[0].moduleId);
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Failed to load");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { load(); }, [id]);

  async function addModule() {
    if (!moduleTitle.trim() || !course) return;
    setSaving(true);
    try {
      await api.courses.addModule(id, { title: moduleTitle, order: course.modules.length });
      setModuleTitle(""); setAddingModule(false);
      await load();
    } finally { setSaving(false); }
  }

  async function addLesson(moduleId: string) {
    if (!lessonForm.title.trim()) return;
    const mod = course?.modules.find(m => m.moduleId === moduleId);
    setSaving(true);
    try {
      await api.courses.addLesson(moduleId, { ...lessonForm, order: mod?.lessons.length ?? 0, isPublished: true });
      setAddingLesson(null);
      setLessonForm({ title: "", type: "RichText", content: "", videoUrl: "", externalUrl: "" });
      await load();
    } finally { setSaving(false); }
  }

  async function togglePublish() {
    if (!course) return;
    await api.courses.publish(id, !course.isPublished);
    load();
  }


  if (loading) return (
    <div className="p-8 max-w-4xl mx-auto space-y-6">
      <div className="flex items-center gap-3">
        <Skeleton className="h-4 w-16" />
        <Skeleton className="h-8 w-64" />
      </div>
      <Skeleton className="h-4 w-96" />
      {[1, 2, 3].map(i => <Skeleton key={i} className="h-16 rounded-lg" />)}
    </div>
  );
  if (error) return <div className="p-8 text-red-600">{error}</div>;
  if (!course) return null;

  return (
    <div className="p-8 max-w-4xl mx-auto">
      {/* Back + header */}
      <button onClick={() => router.back()} className="flex items-center gap-1.5 text-sm text-gray-400 hover:text-gray-700 mb-4 transition-colors">
        <svg className="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
        </svg>
        Back to Courses
      </button>

      <div className="mb-8 flex items-start justify-between gap-4">
        <div>
          <div className="flex items-center gap-3 mb-1">
            <h1 className="text-2xl font-semibold text-gray-900 tracking-tight">{course.title}</h1>
            <Badge variant={course.isPublished ? "success" : "outline"}>
              {course.isPublished ? "Published" : "Draft"}
            </Badge>
          </div>
          {course.description && <p className="text-sm text-gray-500 mt-1">{course.description}</p>}
          <div className="flex items-center gap-3 mt-2 text-sm text-gray-400">
            <span className="flex items-center gap-1"><Layers className="h-3.5 w-3.5" /> {course.moduleCount} modules</span>
            <span className="flex items-center gap-1"><PlayCircle className="h-3.5 w-3.5" /> {course.lessonCount} lessons</span>
            <span>by {course.createdByName}</span>
          </div>
        </div>
        {canEdit && (
          <Button variant={course.isPublished ? "outline" : "default"} onClick={togglePublish}>
            {course.isPublished ? "Unpublish" : "Publish"}
          </Button>
        )}
      </div>

      {/* Modules */}
      <div className="space-y-3">
        {course.modules.map((mod, idx) => {
          const isOpen = expandedModule === mod.moduleId;
          return (
            <Card key={mod.moduleId} className={isOpen ? "ring-1 ring-blue-200" : ""}>
              <CardHeader
                className="cursor-pointer select-none py-4 hover:bg-gray-50 rounded-t-lg transition-colors"
                onClick={() => setExpandedModule(isOpen ? null : mod.moduleId)}
              >
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-3">
                    <div className="flex h-7 w-7 items-center justify-center rounded-full bg-blue-100 text-blue-700 text-xs font-bold">
                      {idx + 1}
                    </div>
                    <CardTitle className="text-base">{mod.title}</CardTitle>
                    <span className="text-xs text-gray-400">{mod.lessons.length} lesson{mod.lessons.length !== 1 ? "s" : ""}</span>
                  </div>
                  <svg className={`h-4 w-4 text-gray-400 transition-transform ${isOpen ? "rotate-180" : ""}`}
                    fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
                  </svg>
                </div>
              </CardHeader>

              {isOpen && (
                <CardContent className="pt-0 pb-4 space-y-1.5">
                  {mod.lessons.map((lesson, li) => (
                    <div key={lesson.lessonId}
                      className="flex items-center gap-3 rounded-lg p-3 hover:bg-gray-50 transition-colors group">
                      <span className="text-lg w-6 text-center">{LESSON_ICONS[lesson.type] ?? "📄"}</span>
                      <div className="flex-1 min-w-0">
                        <p className="text-sm font-medium text-gray-900 truncate">{lesson.title}</p>
                        <p className="text-xs text-gray-400">
                          {lesson.type}{lesson.durationMinutes ? ` · ${lesson.durationMinutes} min` : ""}
                        </p>
                      </div>
                      <Badge variant={lesson.isPublished ? "success" : "outline"} className="text-xs shrink-0">
                        {lesson.isPublished ? "Live" : "Draft"}
                      </Badge>
                    </div>
                  ))}

                  {canEdit && (
                    addingLesson === mod.moduleId ? (
                      <div className="mt-2 rounded-xl border border-blue-200 bg-blue-50 p-4 space-y-3">
                        <Input placeholder="Lesson title" value={lessonForm.title}
                          onChange={e => setLessonForm(f => ({ ...f, title: e.target.value }))} autoFocus />
                        <select value={lessonForm.type}
                          onChange={e => setLessonForm(f => ({ ...f, type: e.target.value }))}
                          className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500">
                          <option value="RichText">📄 Rich Text</option>
                          <option value="Video">▶️ Video (YouTube)</option>
                          <option value="PDF">📋 PDF Document</option>
                          <option value="Link">🔗 External Link</option>
                        </select>
                        {lessonForm.type === "Video" && (
                          <Input placeholder="YouTube URL" value={lessonForm.videoUrl}
                            onChange={e => setLessonForm(f => ({ ...f, videoUrl: e.target.value }))} />
                        )}
                        {lessonForm.type === "RichText" && (
                          <textarea placeholder="Lesson content…" value={lessonForm.content}
                            onChange={e => setLessonForm(f => ({ ...f, content: e.target.value }))}
                            className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 resize-none" rows={4} />
                        )}
                        {(lessonForm.type === "PDF" || lessonForm.type === "Link") && (
                          <Input placeholder="URL" value={lessonForm.externalUrl}
                            onChange={e => setLessonForm(f => ({ ...f, externalUrl: e.target.value }))} />
                        )}
                        <div className="flex gap-2">
                          <Button size="sm" onClick={() => addLesson(mod.moduleId)} loading={saving}>Add Lesson</Button>
                          <Button size="sm" variant="outline" onClick={() => setAddingLesson(null)}>Cancel</Button>
                        </div>
                      </div>
                    ) : (
                      <button onClick={() => setAddingLesson(mod.moduleId)}
                        className="mt-1 w-full rounded-lg border border-dashed border-gray-300 py-2 text-sm text-gray-400 hover:border-blue-400 hover:text-blue-600 transition-colors">
                        + Add lesson
                      </button>
                    )
                  )}
                </CardContent>
              )}
            </Card>
          );
        })}

        {canEdit && (
          addingModule ? (
            <Card>
              <CardContent className="p-4 space-y-3">
                <Input placeholder="Module title" value={moduleTitle}
                  onChange={e => setModuleTitle(e.target.value)} autoFocus />
                <div className="flex gap-2">
                  <Button size="sm" onClick={addModule} loading={saving}>Add Module</Button>
                  <Button size="sm" variant="outline" onClick={() => setAddingModule(false)}>Cancel</Button>
                </div>
              </CardContent>
            </Card>
          ) : (
            <button onClick={() => setAddingModule(true)}
              className="w-full rounded-xl border-2 border-dashed border-gray-300 py-4 text-sm text-gray-400 hover:border-blue-400 hover:text-blue-600 transition-colors">
              + Add Module
            </button>
          )
        )}

        {course.modules.length === 0 && !canEdit && (
          <div className="rounded-xl border-2 border-dashed border-gray-300 py-16 text-center">
            <div className="flex justify-center mb-3">
              <Layers className="h-10 w-10 text-gray-300" />
            </div>
            <p className="text-gray-500">No modules yet</p>
          </div>
        )}
      </div>
    </div>
  );
}
