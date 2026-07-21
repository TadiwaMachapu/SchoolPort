"use client";
import { useMemo, useState } from "react";
import Link from "next/link";
import {
  AlertTriangle, CalendarClock, CheckCircle2, ChevronDown, PartyPopper, X,
} from "lucide-react";
import { api, type MyAcademicsTask } from "@/lib/api";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { TypeBadge } from "./badges";
import { EmptyState } from "./EmptyState";

// Tab 3 — Assignments. Unified assignments + quizzes, list view, sorted by due date. Urgency is
// immediately obvious: overdue cards are tinted red. Sections: Overdue / Due Soon / Upcoming /
// Completed (collapsible, closed by default). This replaces the separate Assignments page for
// learners.

type Bucket = "overdue" | "soon" | "upcoming" | "completed";

function fmtDate(iso?: string | null): string {
  if (!iso) return "No due date";
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? "No due date" : d.toLocaleDateString("en-GB");
}

function bucketOf(t: MyAcademicsTask): Bucket {
  if (t.status === "graded" || t.status === "submitted") return "completed";
  if (!t.dueAt) return "upcoming";
  const due = new Date(t.dueAt).getTime();
  const now = Date.now();
  if (due < now) return "overdue";
  if (due - now < 3 * 24 * 3600 * 1000) return "soon";
  return "upcoming";
}

function StatusBadge({ t }: { t: MyAcademicsTask }) {
  if (t.status === "graded") {
    return (
      <span className="inline-flex items-center rounded-md bg-blue-50 px-2 py-0.5 text-xs font-semibold text-blue-700 ring-1 ring-blue-200/70">
        Graded{t.percent != null ? ` · ${Math.round(t.percent)}%` : ""}
      </span>
    );
  }
  if (t.status === "submitted") {
    return <span className="inline-flex items-center rounded-md bg-emerald-50 px-2 py-0.5 text-xs font-semibold text-emerald-700 ring-1 ring-emerald-200/70">Submitted</span>;
  }
  return <span className="inline-flex items-center rounded-md bg-white px-2 py-0.5 text-xs font-medium text-gray-500 ring-1 ring-gray-200">Not submitted</span>;
}

function TaskCard({ t, onSubmit }: { t: MyAcademicsTask; onSubmit: (t: MyAcademicsTask) => void }) {
  const bucket = bucketOf(t);
  const overdue = bucket === "overdue";
  const outstanding = t.status === "not_submitted";
  return (
    <div className={cn(
      "rounded-xl border p-3.5 shadow-sm ring-1",
      overdue ? "border-danger-500/30 bg-danger-100/60 ring-danger-500/20" : "border-border bg-surface-card ring-border/50",
    )}>
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          <p className="truncate text-sm font-semibold text-text-primary">{t.title}</p>
          <p className="truncate text-xs text-text-secondary">{t.subjectName}</p>
        </div>
        <TypeBadge type={t.type} />
      </div>

      <div className="mt-2.5 flex items-center justify-between gap-2">
        <div className="flex items-center gap-2 text-xs">
          <CalendarClock className={cn("h-3.5 w-3.5", overdue ? "text-danger-500" : bucket === "soon" ? "text-warning-500" : "text-text-muted")} />
          <span className={cn(overdue ? "font-semibold text-danger-700" : bucket === "soon" ? "font-medium text-warning-700" : "text-text-secondary")}>
            {overdue ? "Overdue" : bucket === "soon" ? "Due soon" : "Due"} · {fmtDate(t.dueAt)}
          </span>
        </div>
        <StatusBadge t={t} />
      </div>

      {outstanding && (
        <div className="mt-3 flex justify-end">
          {t.source === "assignment" ? (
            <Button size="sm" variant={overdue ? "destructive" : "default"} onClick={() => onSubmit(t)}>Submit</Button>
          ) : (
            <Link href="/quizzes"><Button size="sm" variant="outline">Start quiz</Button></Link>
          )}
        </div>
      )}
    </div>
  );
}

function Section({
  title, icon: Icon, tone, tasks, onSubmit, collapsible = false,
}: {
  title: string;
  icon: typeof AlertTriangle;
  tone: "red" | "amber" | "gray" | "green";
  tasks: MyAcademicsTask[];
  onSubmit: (t: MyAcademicsTask) => void;
  collapsible?: boolean;
}) {
  const [open, setOpen] = useState(!collapsible);
  if (tasks.length === 0) return null;
  const toneCls = {
    red: "text-danger-700", amber: "text-warning-700", gray: "text-text-secondary", green: "text-success-700",
  }[tone];
  return (
    <section>
      <button
        onClick={() => collapsible && setOpen((o) => !o)}
        className={cn("mb-2 flex w-full items-center gap-2", collapsible ? "cursor-pointer" : "cursor-default")}
        aria-expanded={open}
      >
        <Icon className={cn("h-4 w-4", toneCls)} />
        <h2 className={cn("text-xs font-bold uppercase tracking-wider", toneCls)}>{title}</h2>
        <span className="rounded-full bg-surface-subtle px-1.5 py-0.5 text-[10px] font-semibold text-text-secondary">{tasks.length}</span>
        {collapsible && <ChevronDown className={cn("ml-auto h-4 w-4 text-text-muted transition-transform", open && "rotate-180")} />}
      </button>
      {open && (
        <div className="space-y-2">
          {tasks.map((t) => <TaskCard key={`${t.source}-${t.taskId}`} t={t} onSubmit={onSubmit} />)}
        </div>
      )}
    </section>
  );
}

function SubmitModal({ task, onClose, onDone }: { task: MyAcademicsTask; onClose: () => void; onDone: () => void }) {
  const [comments, setComments] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit() {
    setBusy(true);
    setError(null);
    try {
      await api.submissions.submit(task.taskId, { content: comments || undefined });
      onDone();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Submission failed. Please try again.");
      setBusy(false);
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-end justify-center bg-black/40 p-0 backdrop-blur-sm sm:items-center sm:p-4" onClick={onClose}>
      <div className="w-full max-w-md rounded-t-2xl bg-surface-card shadow-2xl sm:rounded-2xl" onClick={(e) => e.stopPropagation()}>
        <div className="flex items-center justify-between border-b border-border px-5 py-4">
          <h3 className="text-base font-semibold text-text-primary">Submit task</h3>
          <button onClick={onClose} className="rounded-md p-1 text-text-muted hover:bg-surface-subtle hover:text-text-secondary"><X className="h-5 w-5" /></button>
        </div>
        <div className="space-y-3 px-5 py-4">
          <div>
            <p className="text-sm font-medium text-text-primary">{task.title}</p>
            <p className="text-xs text-text-secondary">{task.subjectName}</p>
          </div>
          <textarea
            value={comments}
            onChange={(e) => setComments(e.target.value)}
            placeholder="Add a comment for your teacher (optional)…"
            rows={4}
            className="w-full resize-none rounded-lg border border-border px-3 py-2 text-sm text-text-primary focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary"
          />
          {error && <p className="text-sm text-danger-700">{error}</p>}
        </div>
        <div className="flex justify-end gap-2 border-t border-border px-5 py-4">
          <Button variant="ghost" onClick={onClose} disabled={busy}>Cancel</Button>
          <Button onClick={submit} loading={busy}>Submit task</Button>
        </div>
      </div>
    </div>
  );
}

export function AssignmentsTab({ tasks, onChanged }: { tasks: MyAcademicsTask[]; onChanged: () => void }) {
  const [submitTask, setSubmitTask] = useState<MyAcademicsTask | null>(null);

  const groups = useMemo(() => {
    const by: Record<Bucket, MyAcademicsTask[]> = { overdue: [], soon: [], upcoming: [], completed: [] };
    for (const t of tasks) by[bucketOf(t)].push(t);
    const byDue = (a: MyAcademicsTask, b: MyAcademicsTask) =>
      new Date(a.dueAt ?? a.date ?? 0).getTime() - new Date(b.dueAt ?? b.date ?? 0).getTime();
    by.overdue.sort(byDue);
    by.soon.sort(byDue);
    by.upcoming.sort(byDue);
    by.completed.sort((a, b) => -byDue(a, b)); // most recent first
    return by;
  }, [tasks]);

  const outstanding = groups.overdue.length + groups.soon.length + groups.upcoming.length;

  if (tasks.length === 0) {
    return <EmptyState icon={PartyPopper} title="You're all caught up!" description="No outstanding assignments." />;
  }

  return (
    <div className="space-y-5">
      {outstanding === 0 && (
        <EmptyState icon={PartyPopper} title="You're all caught up!" description="No outstanding assignments." />
      )}
      <Section title="Overdue" icon={AlertTriangle} tone="red" tasks={groups.overdue} onSubmit={setSubmitTask} />
      <Section title="Due soon" icon={CalendarClock} tone="amber" tasks={groups.soon} onSubmit={setSubmitTask} />
      <Section title="Upcoming" icon={CalendarClock} tone="gray" tasks={groups.upcoming} onSubmit={setSubmitTask} />
      <Section title="Completed" icon={CheckCircle2} tone="green" tasks={groups.completed} onSubmit={setSubmitTask} collapsible />

      {submitTask && (
        <SubmitModal
          task={submitTask}
          onClose={() => setSubmitTask(null)}
          onDone={() => { setSubmitTask(null); onChanged(); }}
        />
      )}
    </div>
  );
}
