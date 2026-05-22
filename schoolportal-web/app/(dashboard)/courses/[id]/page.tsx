"use client";
import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import { api } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

interface Lesson {
  lessonId: string;
  title: string;
  type: string;
  content?: string;
  videoUrl?: string;
  fileUrl?: string;
  externalUrl?: string;
  order: number;
  durationMinutes?: number;
  isPublished: boolean;
}

interface Module {
  moduleId: string;
  title: string;
  description?: string;
  order: number;
  lessons: Lesson[];
}

interface Course {
  courseId: string;
  title: string;
  description?: string;
  isPublished: boolean;
  createdByName: string;
  createdAt: string;
  moduleCount: number;
  lessonCount: number;
  modules: Module[];
}

const LESSON_ICONS: Record<string, string> = {
  RichText: "📄", Video: "▶️", PDF: "📋", Link: "🔗"
};

export default function CourseDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [course, setCourse] = useState<Course | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [addingModule, setAddingModule] = useState(false);
  const [moduleTitle, setModuleTitle] = useState("");
  const [expandedModule, setExpandedModule] = useState<string | null>(null);
  const [addingLesson, setAddingLesson] = useState<string | null>(null);
  const [lessonForm, setLessonForm] = useState({ title: "", type: "RichText", content: "", videoUrl: "", externalUrl: "" });

  async function load() {
    setLoading(true);
    try {
      const c = await api.courses.get(id);
      setCourse(c as Course);
      if ((c as Course).modules.length > 0) setExpandedModule((c as Course).modules[0].moduleId);
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Failed to load");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { load(); }, [id]);

  async function addModule() {
    if (!moduleTitle.trim() || !course) return;
    await api.courses.addModule(id, { title: moduleTitle, order: course.modules.length });
    setModuleTitle(""); setAddingModule(false);
    load();
  }

  async function addLesson(moduleId: string) {
    if (!lessonForm.title.trim()) return;
    const mod = course?.modules.find(m => m.moduleId === moduleId);
    await api.courses.addLesson(moduleId, {
      ...lessonForm,
      order: mod?.lessons.length ?? 0,
      isPublished: true,
    });
    setAddingLesson(null);
    setLessonForm({ title: "", type: "RichText", content: "", videoUrl: "", externalUrl: "" });
    load();
  }

  async function togglePublish() {
    if (!course) return;
    await api.courses.publish(id, !course.isPublished);
    load();
  }

  if (loading) return <div className="p-8 text-gray-400">Loading course…</div>;
  if (error) return <div className="p-8 text-red-600">{error}</div>;
  if (!course) return null;

  return (
    <div className="p-8 max-w-4xl mx-auto">
      {/* Header */}
      <div className="mb-6 flex items-start justify-between gap-4">
        <div>
          <div className="flex items-center gap-3 mb-1">
            <h1 className="text-3xl font-bold text-gray-900">{course.title}</h1>
            <Badge variant={course.isPublished ? "success" : "outline"}>
              {course.isPublished ? "Published" : "Draft"}
            </Badge>
          </div>
          {course.description && <p className="text-gray-500">{course.description}</p>}
          <p className="text-sm text-gray-400 mt-1">
            {course.moduleCount} modules · {course.lessonCount} lessons · by {course.createdByName}
          </p>
        </div>
        <Button variant={course.isPublished ? "outline" : "default"} onClick={togglePublish}>
          {course.isPublished ? "Unpublish" : "Publish"}
        </Button>
      </div>

      {/* Modules */}
      <div className="space-y-4">
        {course.modules.map((mod) => (
          <Card key={mod.moduleId}>
            <CardHeader
              className="cursor-pointer select-none py-4"
              onClick={() => setExpandedModule(expandedModule === mod.moduleId ? null : mod.moduleId)}
            >
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <span className="text-gray-400">{expandedModule === mod.moduleId ? "▼" : "▶"}</span>
                  <CardTitle className="text-base">{mod.title}</CardTitle>
                  <span className="text-xs text-gray-400">{mod.lessons.length} lessons</span>
                </div>
              </div>
            </CardHeader>

            {expandedModule === mod.moduleId && (
              <CardContent className="pt-0 space-y-2">
                {mod.lessons.map((lesson) => (
                  <div key={lesson.lessonId}
                    className="flex items-center gap-3 p-3 rounded-md bg-gray-50 hover:bg-gray-100 transition-colors">
                    <span className="text-lg">{LESSON_ICONS[lesson.type] ?? "📄"}</span>
                    <div className="flex-1 min-w-0">
                      <p className="text-sm font-medium text-gray-900 truncate">{lesson.title}</p>
                      <p className="text-xs text-gray-400">{lesson.type}{lesson.durationMinutes ? ` · ${lesson.durationMinutes} min` : ""}</p>
                    </div>
                    <Badge variant={lesson.isPublished ? "success" : "outline"} className="text-xs">
                      {lesson.isPublished ? "Live" : "Draft"}
                    </Badge>
                  </div>
                ))}

                {addingLesson === mod.moduleId ? (
                  <div className="border border-blue-200 rounded-md p-4 bg-blue-50 space-y-3">
                    <Input placeholder="Lesson title" value={lessonForm.title}
                      onChange={e => setLessonForm(f => ({ ...f, title: e.target.value }))} />
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
                        className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" rows={4} />
                    )}
                    {(lessonForm.type === "PDF" || lessonForm.type === "Link") && (
                      <Input placeholder="URL" value={lessonForm.externalUrl}
                        onChange={e => setLessonForm(f => ({ ...f, externalUrl: e.target.value }))} />
                    )}
                    <div className="flex gap-2">
                      <Button size="sm" onClick={() => addLesson(mod.moduleId)}>Add Lesson</Button>
                      <Button size="sm" variant="outline" onClick={() => setAddingLesson(null)}>Cancel</Button>
                    </div>
                  </div>
                ) : (
                  <button onClick={() => setAddingLesson(mod.moduleId)}
                    className="w-full text-left px-3 py-2 text-sm text-blue-600 hover:bg-blue-50 rounded-md transition-colors">
                    + Add lesson
                  </button>
                )}
              </CardContent>
            )}
          </Card>
        ))}

        {/* Add Module */}
        {addingModule ? (
          <Card>
            <CardContent className="p-4 space-y-3">
              <Input placeholder="Module title" value={moduleTitle}
                onChange={e => setModuleTitle(e.target.value)} autoFocus />
              <div className="flex gap-2">
                <Button size="sm" onClick={addModule}>Add Module</Button>
                <Button size="sm" variant="outline" onClick={() => setAddingModule(false)}>Cancel</Button>
              </div>
            </CardContent>
          </Card>
        ) : (
          <button onClick={() => setAddingModule(true)}
            className="w-full rounded-lg border-2 border-dashed border-gray-300 py-4 text-sm text-gray-500 hover:border-blue-400 hover:text-blue-600 transition-colors">
            + Add Module
          </button>
        )}
      </div>
    </div>
  );
}
