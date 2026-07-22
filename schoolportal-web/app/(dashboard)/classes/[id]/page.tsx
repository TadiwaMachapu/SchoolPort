"use client";
import { useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { api, type Class, type User, type ClassSubject, type TeacherOption } from "@/lib/api";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { usePermission } from "@/lib/auth-context";
import { useToastStore } from "@/stores/toast.store";
import { User as UserIcon, BookOpen, Pencil, Check, X, Loader2 } from "lucide-react";

export default function ClassDetailPage() {
  const { id }  = useParams<{ id: string }>();
  const router  = useRouter();
  const [cls,      setCls]      = useState<Class | null>(null);
  const [students, setStudents] = useState<User[]>([]);
  const [subjects, setSubjects] = useState<ClassSubject[]>([]);
  const [loading,  setLoading]  = useState(true);
  const [error,    setError]    = useState("");
  const [tab,      setTab]      = useState<"students" | "subjects">("students");

  // Step 9.5 (Build #6b): admins/HOD (academics.manage) can assign a teacher per class-subject.
  const canManage = usePermission("academics.manage");
  const [teachers, setTeachers] = useState<TeacherOption[]>([]);

  useEffect(() => {
    Promise.allSettled([
      api.classes.get(id),
      api.classes.students(id),
      api.classes.subjects(id),
    ]).then(([c, s, sub]) => {
      if (c.status === "fulfilled") setCls(c.value as Class);
      else setError("Class not found");
      if (s.status === "fulfilled") setStudents(s.value as User[]);
      if (sub.status === "fulfilled") setSubjects(sub.value as ClassSubject[]);
    }).catch(e => setError(String(e)))
      .finally(() => setLoading(false));
  }, [id]);

  // Load the assignable teacher roster only for users who can manage (others would get a 403).
  useEffect(() => {
    if (!canManage) return;
    api.classSubjects.teachers().then(setTeachers).catch(() => {});
  }, [canManage]);

  // Lets the caller distinguish a save failure from a post-save refresh failure (M1) — does not swallow.
  const reloadSubjects = () =>
    api.classes.subjects(id).then(s => setSubjects(s as ClassSubject[]));

  if (loading) return (
    <div className="p-6 lg:p-8 max-w-5xl mx-auto space-y-6">
      <Skeleton className="h-4 w-20" />
      <Skeleton className="h-10 w-64" />
      <div className="grid grid-cols-4 gap-4">
        {[1,2,3,4].map(i => <Skeleton key={i} className="h-20 rounded-lg" />)}
      </div>
      <Skeleton className="h-64 rounded-lg" />
    </div>
  );
  if (error || !cls) return <div className="p-6 lg:p-8 text-danger-700">{error || "Class not found"}</div>;

  const capacity  = cls.maxCapacity ?? 0;
  const fillPct   = capacity > 0 ? Math.round((cls.studentCount / capacity) * 100) : 0;
  const fillColor = fillPct >= 90 ? "bg-danger-500" : fillPct >= 75 ? "bg-warning-500" : "bg-primary";

  return (
    <div className="p-6 lg:p-8 space-y-6 max-w-5xl mx-auto">
      {/* Back */}
      <button onClick={() => router.back()} className="flex items-center gap-1.5 text-sm text-text-muted hover:text-text-primary transition-colors">
        <svg className="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
        </svg>
        Back to Classes
      </button>

      {/* Header */}
      <div className="flex items-start justify-between flex-wrap gap-4">
        <div>
          <h1 className="text-2xl font-semibold text-text-primary tracking-tight">{cls.name}</h1>
          <div className="flex flex-wrap items-center gap-2 mt-2">
            {cls.gradeLevel && <Badge variant="outline">Grade {cls.gradeLevel}</Badge>}
            {cls.academicYear && <Badge variant="outline">{cls.academicYear}</Badge>}
            {cls.teacherName && (
              <span className="text-sm text-text-secondary">
                Teacher: <span className="font-medium text-text-primary">{cls.teacherName}</span>
              </span>
            )}
          </div>
        </div>
      </div>

      {/* Stats */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
        {[
          { label: "Students",  value: cls.studentCount, color: "text-text-primary" },
          { label: "Subjects",  value: subjects.length,  color: "text-text-primary" },
        ].map(s => (
          <Card key={s.label}>
            <CardContent className="p-4 text-center">
              <p className={`text-3xl font-bold ${s.color}`}>{s.value}</p>
              <p className="text-xs text-text-secondary mt-1">{s.label}</p>
            </CardContent>
          </Card>
        ))}
        {capacity > 0 && (
          <Card className="sm:col-span-2">
            <CardContent className="p-4">
              <div className="flex items-center justify-between mb-2">
                <p className="text-sm font-medium text-text-primary">Capacity</p>
                <p className="text-sm font-bold text-text-primary">{cls.studentCount} / {capacity}</p>
              </div>
              <div className="h-2.5 bg-surface-subtle rounded-full overflow-hidden">
                <div className={`h-full rounded-full transition-all ${fillColor}`}
                  style={{ width: `${Math.min(fillPct, 100)}%` }} />
              </div>
              <p className="text-xs text-text-muted mt-1.5">{fillPct}% full</p>
            </CardContent>
          </Card>
        )}
      </div>

      {/* Tabs */}
      <div className="flex gap-2 border-b border-border pb-0">
        {(["students", "subjects"] as const).map(t => (
          <button key={t} onClick={() => setTab(t)}
            className={`pb-3 px-1 text-sm font-medium capitalize transition-colors border-b-2 -mb-px ${
              tab === t ? "border-primary text-primary" : "border-transparent text-text-secondary hover:text-text-primary"
            }`}>
            {t} ({t === "students" ? students.length : subjects.length})
          </button>
        ))}
      </div>

      {tab === "students" && (
        <Card>
          <CardContent className="p-0">
            {students.length === 0 ? (
              <div className="py-16 text-center text-text-muted">
                <div className="flex justify-center mb-3">
                  <UserIcon className="h-10 w-10 text-text-muted" />
                </div>
                <p className="text-sm font-medium text-text-secondary">No students enrolled yet</p>
                <p className="text-xs text-text-muted mt-1">Students can be enrolled from the Users page</p>
              </div>
            ) : (
              <table className="w-full text-sm">
                <thead className="border-b bg-surface-subtle">
                  <tr>
                    {["Name", "Email", "Status"].map(h => (
                      <th key={h} className="px-6 py-3 text-left text-xs font-semibold text-text-muted uppercase tracking-wider">{h}</th>
                    ))}
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {students.map(s => (
                    <tr key={s.userId} className="hover:bg-surface-subtle">
                      <td className="px-6 py-3">
                        <div className="flex items-center gap-3">
                          <div className="h-8 w-8 rounded-full bg-primary-100 text-primary-700 text-xs font-bold flex items-center justify-center">
                            {s.firstName[0]}{s.lastName[0]}
                          </div>
                          <p className="font-medium text-text-primary">{s.firstName} {s.lastName}</p>
                        </div>
                      </td>
                      <td className="px-6 py-3 text-text-secondary">{s.email}</td>
                      <td className="px-6 py-3">
                        <span className={`inline-flex items-center gap-1 text-xs font-medium ${s.isActive ? "text-success-700" : "text-text-muted"}`}>
                          <span className={`h-1.5 w-1.5 rounded-full ${s.isActive ? "bg-success-500" : "bg-text-muted"}`} />
                          {s.isActive ? "Active" : "Inactive"}
                        </span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </CardContent>
        </Card>
      )}

      {tab === "subjects" && (
        subjects.length === 0 ? (
          <div className="rounded-xl border-2 border-dashed border-border py-16 text-center">
            <div className="flex justify-center mb-3">
              <BookOpen className="h-10 w-10 text-text-muted" />
            </div>
            <p className="text-sm font-medium text-text-secondary">No subjects assigned</p>
            <p className="text-xs text-text-muted mt-1">Subjects can be assigned to this class by an admin</p>
          </div>
        ) : (
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            {subjects.map(sub => (
              <Card key={sub.classSubjectId} className="hover:shadow-md transition-shadow">
                <CardContent className="p-5 flex items-start gap-4">
                  <div className="h-10 w-10 rounded-lg bg-primary-100 text-primary-700 text-lg font-bold flex items-center justify-center shrink-0">
                    {sub.subjectName[0]}
                  </div>
                  <div className="min-w-0">
                    <p className="font-semibold text-text-primary">{sub.subjectName}</p>
                    <TeacherCell
                      sub={sub}
                      teachers={teachers}
                      canManage={canManage}
                      classId={id}
                      onAssigned={reloadSubjects}
                    />
                  </div>
                </CardContent>
              </Card>
            ))}
          </div>
        )
      )}
    </div>
  );
}

/* ─── Per-class-subject teacher assignment (Build #6b) ─────────────────
   Read-only viewers see just the teacher name. Managers (academics.manage) get an inline
   edit affordance → native <select> of teachers → save (POST /class-subjects/bulk) → refresh.
   Assign/reassign only; the bulk endpoint can't clear a teacher (logged as a follow-up). */
function TeacherCell({
  sub, teachers, canManage, classId, onAssigned,
}: {
  sub: ClassSubject;
  teachers: TeacherOption[];
  canManage: boolean;
  classId: string;
  onAssigned: () => Promise<void> | void;
}) {
  const toast = useToastStore();
  const [editing, setEditing] = useState(false);
  const [saving,  setSaving]  = useState(false);
  const [sel,     setSel]     = useState(sub.teacherId ?? "");

  const nameOrEmpty = sub.teacherName
    ? <span className="text-text-secondary">{sub.teacherName}</span>
    : <span className="italic text-text-muted">No teacher assigned</span>;

  if (!canManage) return <p className="text-sm mt-0.5">{nameOrEmpty}</p>;

  // A3: the pencil cue reveals on hover AND keyboard focus (focus-visible), not hover-only.
  if (!editing) {
    return (
      <button
        onClick={() => { setSel(sub.teacherId ?? ""); setEditing(true); }}
        aria-label={`${sub.teacherName ? "Change" : "Assign"} teacher for ${sub.subjectName}`}
        className="group/edit mt-0.5 inline-flex items-center gap-1.5 text-sm hover:text-primary transition-colors"
      >
        {nameOrEmpty}
        <Pencil className="h-3.5 w-3.5 text-text-muted opacity-0 transition-opacity group-hover/edit:opacity-100 group-focus-visible/edit:opacity-100" />
      </button>
    );
  }

  // M2: no teachers to choose from — explain why instead of showing an empty dropdown.
  if (teachers.length === 0) {
    return (
      <div className="mt-1 flex items-center gap-2">
        <span className="text-sm italic text-text-muted">No teachers found — add staff first</span>
        <button
          onClick={() => setEditing(false)}
          aria-label="Cancel"
          className="shrink-0 rounded-md p-1 text-text-muted hover:bg-surface-subtle"
        >
          <X className="h-4 w-4" />
        </button>
      </div>
    );
  }

  async function save() {
    if (!sel) return;
    setSaving(true);
    // Phase 1: the assignment itself. A failure here means nothing was saved — stay in edit.
    try {
      await api.classSubjects.bulkAssign([{ classId, subjectId: sub.subjectId, teacherId: sel }]);
    } catch (e) {
      toast.error("Could not assign teacher", e instanceof Error ? e.message : "");
      setSaving(false);
      return;
    }
    // Phase 2: assignment succeeded. A refresh failure is a DIFFERENT, non-destructive condition (M1) —
    // the teacher IS saved; we just couldn't repaint, so say exactly that rather than "assign failed".
    setEditing(false);
    toast.success("Teacher assigned", `${sub.subjectName} updated`);
    try {
      await onAssigned();
    } catch {
      toast.error("List didn't refresh", "The teacher was saved — reload the page to see it.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="mt-1 flex items-center gap-1.5">
      <select
        value={sel}
        onChange={e => setSel(e.target.value)}
        disabled={saving}
        aria-label={`Teacher for ${sub.subjectName}`}
        className="min-w-0 rounded-md border border-border px-2 py-1 text-sm text-text-primary focus:border-primary focus:outline-none disabled:opacity-50"
      >
        <option value="">Select teacher…</option>
        {teachers.map(t => <option key={t.teacherId} value={t.teacherId}>{t.name}</option>)}
      </select>
      <button
        onClick={save}
        disabled={saving || !sel}
        aria-label="Save teacher assignment"
        title="Save"
        className="shrink-0 rounded-md p-1 text-success-700 hover:bg-success-100 disabled:opacity-40"
      >
        {saving ? <Loader2 className="h-4 w-4 animate-spin" /> : <Check className="h-4 w-4" />}
      </button>
      <button
        onClick={() => setEditing(false)}
        disabled={saving}
        aria-label="Cancel"
        title="Cancel"
        className="shrink-0 rounded-md p-1 text-text-muted hover:bg-surface-subtle disabled:opacity-40"
      >
        <X className="h-4 w-4" />
      </button>
    </div>
  );
}
