"use client";
import { useEffect, useState } from "react";
import Link from "next/link";
import { api, type Assignment, type Class, type ClassSubject } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";
import { SkeletonTable } from "@/components/ui/skeleton";
import { usePermission } from "@/lib/auth-context";
import { useAssignments } from "@/features/assignments/api/hooks";
import { useToastStore } from "@/stores/toast.store";
import { AnimatePresence, motion } from "framer-motion";
import { ClipboardList, ChevronLeft, ChevronRight, X, ExternalLink, Clock } from "lucide-react";

function dueBadge(dueAt: string) {
  const diff = (new Date(dueAt).getTime() - Date.now()) / 86400000;
  if (diff < 0)  return <Badge variant="destructive">Overdue</Badge>;
  if (diff < 3)  return <Badge variant="warning">Due soon</Badge>;
  return               <Badge variant="success">Upcoming</Badge>;
}

function formatDue(dueAt: string) {
  return new Date(dueAt).toLocaleDateString("en-US", { month: "short", day: "numeric", year: "numeric" });
}

export default function AssignmentsPage() {
  const [showCreate, setShowCreate] = useState(false);
  const toast = useToastStore();
  const canCreate = usePermission("assessment.create"); // Step 8

  const { data, isLoading, isError, refetch } = useAssignments({ pageSize: 50 });
  const assignments = data?.items ?? [];
  const total = data?.total ?? 0;

  return (
    <div className="p-4 sm:p-6 lg:p-8">
      <div className="mb-6 flex items-center justify-between gap-4">
        <div>
          <h1 className="text-xl md:text-2xl font-semibold text-text-primary tracking-tight">Assignments</h1>
          <p className="text-sm text-text-secondary mt-1">{total} assignment{total !== 1 ? "s" : ""}</p>
        </div>
        {canCreate && (
          <Button onClick={() => setShowCreate(true)} className="shrink-0">+ Create</Button>
        )}
      </div>

      {isError && (
        <div className="mb-4 rounded-xl bg-danger-100 p-3 text-sm text-danger-700 flex items-center justify-between">
          <span>Failed to load assignments</span>
          <button onClick={() => refetch()} className="text-danger-700 font-medium hover:underline">Retry</button>
        </div>
      )}

      {isLoading ? (
        <>
          {/* Mobile skeleton */}
          <div className="space-y-3 sm:hidden">
            {Array.from({ length: 5 }).map((_, i) => (
              <div key={i} className="h-24 animate-pulse rounded-2xl bg-surface-subtle" />
            ))}
          </div>
          {/* Desktop skeleton */}
          <div className="hidden sm:block">
            <SkeletonTable rows={6} cols={6} />
          </div>
        </>
      ) : assignments.length === 0 ? (
        <div className="rounded-2xl border-2 border-dashed border-border py-16 text-center">
          <ClipboardList className="h-10 w-10 text-text-muted mx-auto mb-3" />
          <p className="text-base font-medium text-text-primary">No assignments yet</p>
          <p className="text-sm text-text-muted mt-1 px-8">
            {canCreate ? "Create the first assignment for your class" : "Assignments from your teachers will appear here"}
          </p>
          {canCreate && (
            <Button className="mt-5" onClick={() => setShowCreate(true)}>+ Create Assignment</Button>
          )}
        </div>
      ) : (
        <>
          {/* Mobile: card list */}
          <div className="space-y-3 sm:hidden">
            {assignments.map((a) => (
              <Link key={a.assignmentId} href={`/assignments/${a.assignmentId}`}>
                <motion.div
                  whileTap={{ scale: 0.98 }}
                  className="rounded-2xl border border-border bg-surface-card p-4 shadow-sm active:shadow-none transition-shadow"
                >
                  <div className="flex items-start justify-between gap-2 mb-2">
                    <p className="font-semibold text-text-primary text-sm leading-snug">{a.title}</p>
                    {dueBadge(a.dueAt)}
                  </div>
                  <div className="flex items-center gap-3 text-xs text-text-secondary">
                    {a.subjectName && <span>{a.subjectName}</span>}
                    {a.className && <span>· {a.className}</span>}
                  </div>
                  <div className="flex items-center gap-1.5 mt-2 text-xs text-text-muted">
                    <Clock className="h-3 w-3" />
                    {formatDue(a.dueAt)} · {a.maxMarks} marks
                  </div>
                </motion.div>
              </Link>
            ))}
          </div>

          {/* Desktop: table */}
          <Card className="hidden sm:block">
            <CardContent className="p-0">
              <table className="w-full text-sm">
                <thead className="border-b border-border bg-surface-subtle">
                  <tr>
                    {["Title", "Subject", "Class", "Due Date", "Marks", "Status"].map((h) => (
                      <th key={h} className="px-4 md:px-6 py-3 text-left text-xs font-semibold text-text-muted uppercase tracking-wider">
                        {h}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {assignments.map((a) => (
                    <tr key={a.assignmentId} className="hover:bg-surface-subtle transition-colors">
                      <td className="px-4 md:px-6 py-4">
                        <Link href={`/assignments/${a.assignmentId}`}
                          className="font-medium text-text-primary hover:text-primary hover:underline flex items-center gap-1.5">
                          {a.title}
                          <ExternalLink className="h-3 w-3 opacity-40" />
                        </Link>
                      </td>
                      <td className="px-4 md:px-6 py-4 text-text-secondary">{a.subjectName ?? "—"}</td>
                      <td className="px-4 md:px-6 py-4 text-text-secondary">{a.className ?? "—"}</td>
                      <td className="px-4 md:px-6 py-4 text-text-secondary text-xs">{formatDue(a.dueAt)}</td>
                      <td className="px-4 md:px-6 py-4 text-text-secondary">{a.maxMarks}</td>
                      <td className="px-4 md:px-6 py-4">{dueBadge(a.dueAt)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </CardContent>
          </Card>
        </>
      )}

      <AnimatePresence>
        {showCreate && (
          <CreateAssignmentSheet
            onClose={() => setShowCreate(false)}
            onCreated={() => {
              setShowCreate(false);
              refetch();
              toast.success("Assignment created", "Students will be notified.");
            }}
          />
        )}
      </AnimatePresence>
    </div>
  );
}

/* ── Mobile-first step-by-step creation sheet ─────────────────── */
type Step = 1 | 2 | 3;

function CreateAssignmentSheet({ onClose, onCreated }: { onClose: () => void; onCreated: () => void }) {
  const [step,     setStep]     = useState<Step>(1);
  const [classes,  setClasses]  = useState<Class[]>([]);
  const [subjects, setSubjects] = useState<ClassSubject[]>([]);
  const [classId,  setClassId]  = useState("");
  const [form, setForm] = useState({
    classSubjectId: "",
    title:          "",
    description:    "",
    dueAt:          new Date(Date.now() + 7 * 86400000).toISOString().slice(0, 16),
    maxMarks:       "100",
  });
  const [saving,         setSaving]         = useState(false);
  const [error,          setError]          = useState("");
  const [loadingClasses, setLoadingClasses] = useState(true);

  useEffect(() => {
    api.classes.list({ pageSize: 100 })
      .then((r) => { setClasses(r.items); if (r.items.length > 0) setClassId(r.items[0].classId); })
      .catch(() => {})
      .finally(() => setLoadingClasses(false));
  }, []);

  useEffect(() => {
    if (!classId) return;
    setSubjects([]);
    setForm((f) => ({ ...f, classSubjectId: "" }));
    api.classes.subjects(classId)
      .then((s) => { setSubjects(s); if (s.length > 0) setForm((f) => ({ ...f, classSubjectId: s[0].classSubjectId })); })
      .catch(() => {});
  }, [classId]);

  async function submit() {
    if (!form.classSubjectId) { setError("Select a class and subject"); return; }
    if (!form.title.trim())   { setError("Title is required"); return; }
    setSaving(true);
    setError("");
    try {
      await api.assignments.create({
        classSubjectId: form.classSubjectId,
        title:          form.title,
        description:    form.description || undefined,
        dueAt:          new Date(form.dueAt).toISOString(),
        maxMarks:       Number(form.maxMarks),
      });
      onCreated();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : "Failed to create");
    } finally {
      setSaving(false);
    }
  }

  function canAdvance() {
    if (step === 1) return !!form.classSubjectId;
    if (step === 2) return !!form.title.trim();
    return true;
  }

  const stepLabels = ["Class & Subject", "Details", "Schedule"];

  return (
    <motion.div
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      exit={{ opacity: 0 }}
      className="fixed inset-0 z-50 flex flex-col justify-end sm:items-center sm:justify-center bg-black/40 backdrop-blur-sm"
      onClick={(e) => e.target === e.currentTarget && onClose()}
    >
      <motion.div
        initial={{ y: "100%", opacity: 0 }}
        animate={{ y: 0, opacity: 1 }}
        exit={{ y: "100%", opacity: 0 }}
        transition={{ type: "spring", damping: 30, stiffness: 300 }}
        className="w-full sm:max-w-lg rounded-t-3xl sm:rounded-3xl bg-surface-card shadow-2xl flex flex-col max-h-[92vh]"
      >
        {/* Drag handle */}
        <div className="flex justify-center pt-3 pb-1 sm:hidden shrink-0">
          <div className="h-1 w-10 rounded-full bg-border" />
        </div>

        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-border shrink-0">
          <div>
            <h2 className="text-lg font-semibold text-text-primary">New Assignment</h2>
            <p className="text-xs text-text-muted mt-0.5">{stepLabels[step - 1]}</p>
          </div>
          <button onClick={onClose} className="rounded-full p-2 text-text-muted hover:bg-surface-subtle transition-colors min-h-[44px] min-w-[44px] flex items-center justify-center">
            <X className="h-5 w-5" />
          </button>
        </div>

        {/* Step dots */}
        <div className="flex items-center justify-center gap-2 py-3 shrink-0">
          {([1, 2, 3] as Step[]).map((s) => (
            <button key={s} onClick={() => s < step && setStep(s)}
              className={`h-2 rounded-full transition-all ${s === step ? "w-6 bg-primary" : s < step ? "w-2 bg-primary-300" : "w-2 bg-border"}`}
            />
          ))}
        </div>

        {/* Step content */}
        <div className="flex-1 overflow-y-auto px-6 pb-4">
          {error && (
            <div className="mb-4 rounded-xl bg-danger-100 p-3 text-sm text-danger-700">{error}</div>
          )}

          {step === 1 && (
            <div className="space-y-4 pt-2">
              <div>
                <label className="text-sm font-medium text-text-primary mb-2 block">Class</label>
                {loadingClasses ? (
                  <div className="h-14 animate-pulse rounded-xl bg-surface-subtle" />
                ) : (
                  <div className="grid grid-cols-1 gap-2">
                    {classes.map((c) => (
                      <button key={c.classId} onClick={() => setClassId(c.classId)}
                        className={`flex items-center justify-between rounded-2xl border-2 px-4 py-3 text-left transition-all min-h-[56px] ${
                          classId === c.classId ? "border-primary bg-primary-50" : "border-border hover:border-primary-300"
                        }`}>
                        <div>
                          <p className="font-semibold text-text-primary text-sm">{c.name}</p>
                          <p className="text-xs text-text-muted">{c.studentCount} students{c.gradeLevel ? ` · Grade ${c.gradeLevel}` : ""}</p>
                        </div>
                        {classId === c.classId && <div className="h-5 w-5 rounded-full bg-primary shrink-0 flex items-center justify-center text-white text-xs">✓</div>}
                      </button>
                    ))}
                  </div>
                )}
              </div>
              {classId && (
                <div>
                  <label className="text-sm font-medium text-text-primary mb-2 block">Subject</label>
                  {subjects.length === 0 ? (
                    <p className="text-sm text-text-muted py-2">No subjects assigned to this class</p>
                  ) : (
                    <div className="grid grid-cols-2 gap-2">
                      {subjects.map((s) => (
                        <button key={s.classSubjectId} onClick={() => setForm((f) => ({ ...f, classSubjectId: s.classSubjectId }))}
                          className={`rounded-2xl border-2 px-4 py-3 text-left transition-all min-h-[56px] ${
                            form.classSubjectId === s.classSubjectId ? "border-primary bg-primary-50" : "border-border hover:border-primary-300"
                          }`}>
                          <p className="font-semibold text-text-primary text-sm">{s.subjectName}</p>
                          {s.teacherName && <p className="text-xs text-text-muted mt-0.5">{s.teacherName}</p>}
                        </button>
                      ))}
                    </div>
                  )}
                </div>
              )}
            </div>
          )}

          {step === 2 && (
            <div className="space-y-4 pt-2">
              <div className="space-y-1.5">
                <label className="text-sm font-medium text-text-primary">Title</label>
                <Input placeholder="e.g. Chapter 3 Quiz" value={form.title}
                  onChange={(e) => setForm((f) => ({ ...f, title: e.target.value }))}
                  autoFocus className="text-base py-3 min-h-[48px]" />
              </div>
              <div className="space-y-1.5">
                <label className="text-sm font-medium text-text-primary">
                  Instructions <span className="text-text-muted font-normal">(optional)</span>
                </label>
                <textarea rows={5} placeholder="Write instructions for students…" value={form.description}
                  onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))}
                  className="w-full rounded-xl border border-border px-3 py-3 text-sm placeholder:text-text-muted focus:outline-none focus:ring-2 focus:ring-primary resize-none" />
              </div>
            </div>
          )}

          {step === 3 && (
            <div className="space-y-4 pt-2">
              <div className="space-y-1.5">
                <label className="text-sm font-medium text-text-primary">Due date & time</label>
                <input type="datetime-local" value={form.dueAt}
                  onChange={(e) => setForm((f) => ({ ...f, dueAt: e.target.value }))}
                  className="w-full rounded-xl border border-border px-3 py-3 text-sm focus:outline-none focus:ring-2 focus:ring-primary min-h-[48px]" />
              </div>
              <div className="space-y-1.5">
                <label className="text-sm font-medium text-text-primary">Max marks</label>
                <Input type="number" min={1} max={1000} value={form.maxMarks}
                  onChange={(e) => setForm((f) => ({ ...f, maxMarks: e.target.value }))}
                  className="text-base py-3 min-h-[48px]" />
              </div>
              <div className="rounded-2xl bg-surface-subtle border border-border p-4 space-y-1.5 text-sm">
                <p className="font-semibold text-text-primary mb-2">Review</p>
                <p className="text-text-secondary"><span className="text-text-muted">Class:</span> {classes.find((c) => c.classId === classId)?.name}</p>
                <p className="text-text-secondary"><span className="text-text-muted">Subject:</span> {subjects.find((s) => s.classSubjectId === form.classSubjectId)?.subjectName}</p>
                <p className="text-text-secondary"><span className="text-text-muted">Title:</span> {form.title}</p>
                <p className="text-text-secondary"><span className="text-text-muted">Due:</span> {new Date(form.dueAt).toLocaleString("en-US", { month: "short", day: "numeric", hour: "2-digit", minute: "2-digit" })}</p>
                <p className="text-text-secondary"><span className="text-text-muted">Marks:</span> {form.maxMarks}</p>
              </div>
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="border-t border-border px-6 py-4 flex gap-3 shrink-0">
          {step > 1 ? (
            <button onClick={() => setStep((s) => (s - 1) as Step)}
              className="flex items-center gap-1.5 rounded-xl border border-border px-4 py-3 text-sm font-medium text-text-primary hover:bg-surface-subtle transition-colors min-h-[48px]">
              <ChevronLeft className="h-4 w-4" /> Back
            </button>
          ) : (
            <button onClick={onClose}
              className="flex items-center gap-1.5 rounded-xl border border-border px-4 py-3 text-sm font-medium text-text-primary hover:bg-surface-subtle transition-colors min-h-[48px]">
              Cancel
            </button>
          )}
          <div className="flex-1" />
          {step < 3 ? (
            <Button onClick={() => setStep((s) => (s + 1) as Step)} disabled={!canAdvance()} className="gap-1.5 min-h-[48px] px-6">
              Next <ChevronRight className="h-4 w-4" />
            </Button>
          ) : (
            <Button onClick={submit} loading={saving} className="gap-2 px-8 min-h-[48px]">
              Create Assignment
            </Button>
          )}
        </div>
      </motion.div>
    </motion.div>
  );
}
