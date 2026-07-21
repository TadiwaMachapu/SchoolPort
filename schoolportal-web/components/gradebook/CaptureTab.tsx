"use client";
import { useEffect, useState } from "react";
import { api, type CaptureTaskMarks, type CaptureTaskSummary, type Class, type ClassSubject } from "@/lib/api";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { SkeletonTable } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import { CaptureGrid } from "./CaptureGrid";
import { NewTaskModal } from "./NewTaskModal";
import { ClipboardList, Plus } from "lucide-react";

/* Sprint 1.5.2.5 — teacher capture flow: pick class → pick subject → pick task (or create
   one) → the grid opens. Class picker is scoped to the caller's classes (mine: true). */

export function CaptureTab() {
  const [classes, setClasses] = useState<Class[]>([]);
  const [subjects, setSubjects] = useState<ClassSubject[]>([]);
  const [classId, setClassId] = useState("");
  const [classSubjectId, setClassSubjectId] = useState("");
  const [tasks, setTasks] = useState<CaptureTaskSummary[]>([]);
  const [openMarks, setOpenMarks] = useState<CaptureTaskMarks | null>(null);
  const [showNewTask, setShowNewTask] = useState(false);
  const [loading, setLoading] = useState(true);
  const [tasksLoading, setTasksLoading] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    api.classes.list({ pageSize: 100, mine: true })
      .then((r) => { setClasses(r.items); if (r.items.length > 0) setClassId(r.items[0].classId); })
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => {
    if (!classId) return;
    setSubjects([]);
    setClassSubjectId("");
    setOpenMarks(null);
    api.classes.subjects(classId)
      .then((s) => { setSubjects(s); if (s.length > 0) setClassSubjectId(s[0].classSubjectId); })
      .catch(() => {});
  }, [classId]);

  const loadTasks = (csId: string) => {
    setTasksLoading(true);
    setError("");
    api.gradebook.captureTasks(csId)
      .then(setTasks)
      .catch((e: unknown) => setError(e instanceof Error ? e.message : "Failed to load tasks"))
      .finally(() => setTasksLoading(false));
  };

  useEffect(() => {
    if (!classSubjectId) { setTasks([]); return; }
    setOpenMarks(null);
    loadTasks(classSubjectId);
  }, [classSubjectId]);

  const openTask = async (taskId: string) => {
    setError("");
    try {
      setOpenMarks(await api.gradebook.taskMarks(classSubjectId, taskId));
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Failed to open the task");
    }
  };

  if (loading) return <SkeletonTable rows={5} cols={4} />;

  if (openMarks) {
    return <CaptureGrid marks={openMarks} onBack={() => { setOpenMarks(null); loadTasks(classSubjectId); }} onSaved={() => {}} />;
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center gap-3 flex-wrap">
        <select value={classId} onChange={(e) => setClassId(e.target.value)}
          className="rounded-md border border-transparent bg-surface-subtle px-3 py-2 text-[13px] text-text-primary focus:outline-none focus:ring-2 focus:ring-primary/40">
          {classes.map((c) => <option key={c.classId} value={c.classId}>{c.name}</option>)}
        </select>
        <select value={classSubjectId} onChange={(e) => setClassSubjectId(e.target.value)}
          className="rounded-md border border-transparent bg-surface-subtle px-3 py-2 text-[13px] text-text-primary focus:outline-none focus:ring-2 focus:ring-primary/40">
          {subjects.map((s) => <option key={s.classSubjectId} value={s.classSubjectId}>{s.subjectName}</option>)}
        </select>
        <div className="flex-1" />
        {classSubjectId && (
          <Button onClick={() => setShowNewTask(true)} className="flex items-center gap-2">
            <Plus className="h-4 w-4" /> New Task
          </Button>
        )}
      </div>

      {error && <div className="rounded-lg bg-danger-100 p-3 text-sm text-danger-700">{error}</div>}

      {tasksLoading ? (
        <SkeletonTable rows={4} cols={4} />
      ) : tasks.length === 0 ? (
        <div className="rounded-lg bg-surface-card shadow-card">
          <EmptyState
            icon={ClipboardList}
            heading="No assessment tasks yet"
            body="Create a task to start capturing marks for this subject."
          />
        </div>
      ) : (
        <div className="rounded-lg bg-surface-card shadow-card overflow-hidden">
          <table className="w-full text-sm">
            <thead className="border-b border-border bg-surface-subtle">
              <tr>
                {["Task", "Type", "Term", "Total", "Captured", "Status"].map((h) => (
                  <th key={h} className="px-4 py-3 text-left text-[11px] font-semibold text-text-muted uppercase tracking-wider">{h}</th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {tasks.map((t) => (
                <tr key={t.assignmentId} className="hover:bg-surface-subtle cursor-pointer" onClick={() => openTask(t.assignmentId)}>
                  <td className="px-4 py-3 font-medium text-text-primary">{t.title}</td>
                  <td className="px-4 py-3">
                    <Badge variant={t.taskType === "PAT" ? "warning" : "outline"}>{t.taskType}</Badge>
                  </td>
                  <td className="px-4 py-3 text-text-secondary">{t.termNumber ? `Term ${t.termNumber}` : "—"}</td>
                  <td className="px-4 py-3 text-text-secondary">/{t.maxMarks}{t.hasRubric && <span className="text-xs text-text-muted ml-1">(rubric)</span>}</td>
                  <td className="px-4 py-3 text-text-secondary">{t.capturedCount}/{t.classSize}</td>
                  <td className="px-4 py-3">
                    {t.approvalStatus
                      ? <Badge variant={t.approvalStatus === "Approved" ? "success" : t.approvalStatus === "Rejected" ? "destructive" : "default"}>{t.approvalStatus}</Badge>
                      : <span className="text-text-muted text-xs">—</span>}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {showNewTask && classSubjectId && (
        <NewTaskModal
          classSubjectId={classSubjectId}
          onClose={() => setShowNewTask(false)}
          onCreated={(task) => { setShowNewTask(false); loadTasks(classSubjectId); void openTask(task.assignmentId); }}
        />
      )}
    </div>
  );
}
