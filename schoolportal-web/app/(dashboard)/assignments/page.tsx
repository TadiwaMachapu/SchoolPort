"use client";
import { useEffect, useState } from "react";
import Link from "next/link";
import { api, type Assignment, type Class, type ClassSubject } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";
import { SkeletonTable } from "@/components/ui/skeleton";
import { getClientRole } from "@/lib/utils";

function dueBadge(dueAt: string) {
  const due  = new Date(dueAt);
  const now  = new Date();
  const diff = (due.getTime() - now.getTime()) / 86400000;
  if (diff < 0)  return <Badge variant="destructive">Overdue</Badge>;
  if (diff < 3)  return <Badge variant="warning">Due soon</Badge>;
  return               <Badge variant="success">Upcoming</Badge>;
}

export default function AssignmentsPage() {
  const [assignments, setAssignments] = useState<Assignment[]>([]);
  const [total,       setTotal]       = useState(0);
  const [loading,     setLoading]     = useState(true);
  const [error,       setError]       = useState("");
  const [showCreate,  setShowCreate]  = useState(false);
  const [role,        setRole]        = useState("");

  useEffect(() => { setRole(getClientRole()); }, []);

  async function load() {
    setLoading(true);
    setError("");
    try {
      const res = await api.assignments.list({ pageSize: 50 });
      setAssignments(res.items);
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
    <div className="p-8">
      <div className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold text-gray-900">Assignments</h1>
          <p className="text-gray-500 mt-1">{total} assignment{total !== 1 ? "s" : ""}</p>
        </div>
        {canCreate && (
          <Button onClick={() => setShowCreate(true)}>+ Create Assignment</Button>
        )}
      </div>

      {error && (
        <div className="mb-4 rounded-lg bg-red-50 border border-red-200 p-3 text-sm text-red-700">{error}</div>
      )}

      {loading ? (
        <SkeletonTable rows={6} cols={6} />
      ) : assignments.length === 0 ? (
        <div className="rounded-xl border-2 border-dashed border-gray-300 py-16 text-center">
          <div className="text-5xl mb-4">📝</div>
          <p className="text-lg font-medium text-gray-700">No assignments yet</p>
          <p className="text-sm text-gray-400 mt-1">
            {canCreate ? "Create the first assignment for your class" : "Assignments from your teachers will appear here"}
          </p>
          {canCreate && (
            <Button className="mt-4" onClick={() => setShowCreate(true)}>+ Create Assignment</Button>
          )}
        </div>
      ) : (
        <Card>
          <CardContent className="p-0">
            <table className="w-full text-sm">
              <thead className="border-b border-gray-200 bg-gray-50">
                <tr>
                  {["Title", "Subject", "Class", "Due Date", "Marks", "Status"].map(h => (
                    <th key={h} className="px-6 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wider">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {assignments.map(a => (
                  <tr key={a.assignmentId} className="hover:bg-gray-50 transition-colors">
                    <td className="px-6 py-4">
                      <Link href={`/assignments/${a.assignmentId}`}
                        className="font-medium text-gray-900 hover:text-blue-600 hover:underline">
                        {a.title}
                      </Link>
                    </td>
                    <td className="px-6 py-4 text-gray-500">{a.subjectName ?? "—"}</td>
                    <td className="px-6 py-4 text-gray-500">{a.className ?? "—"}</td>
                    <td className="px-6 py-4 text-gray-600 text-xs">
                      {new Date(a.dueAt).toLocaleDateString("en-US", { month: "short", day: "numeric", year: "numeric" })}
                    </td>
                    <td className="px-6 py-4 text-gray-600">{a.maxMarks}</td>
                    <td className="px-6 py-4">{dueBadge(a.dueAt)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </CardContent>
        </Card>
      )}

      {showCreate && (
        <CreateAssignmentModal onClose={() => { setShowCreate(false); load(); }} />
      )}
    </div>
  );
}

function CreateAssignmentModal({ onClose }: { onClose: () => void }) {
  const [classes,      setClasses]      = useState<Class[]>([]);
  const [subjects,     setSubjects]     = useState<ClassSubject[]>([]);
  const [classId,      setClassId]      = useState("");
  const [form, setForm] = useState({
    classSubjectId: "",
    title:          "",
    description:    "",
    dueAt:          "",
    maxMarks:       "100",
  });
  const [saving,  setSaving]  = useState(false);
  const [error,   setError]   = useState("");
  const [loadingClasses, setLoadingClasses] = useState(true);

  useEffect(() => {
    api.classes.list({ pageSize: 100 })
      .then(r => { setClasses(r.items); if (r.items.length > 0) setClassId(r.items[0].classId); })
      .catch(() => {})
      .finally(() => setLoadingClasses(false));
  }, []);

  useEffect(() => {
    if (!classId) return;
    setSubjects([]);
    setForm(f => ({ ...f, classSubjectId: "" }));
    api.classes.subjects(classId)
      .then(s => { setSubjects(s); if (s.length > 0) setForm(f => ({ ...f, classSubjectId: s[0].classSubjectId })); })
      .catch(() => {});
  }, [classId]);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    if (!form.classSubjectId) { setError("Please select a class and subject"); return; }
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
      onClose();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : "Failed to create");
    } finally {
      setSaving(false);
    }
  }

  // Default due date to one week from now
  const defaultDue = new Date(Date.now() + 7 * 86400000).toISOString().slice(0, 16);

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm p-4">
      <div className="w-full max-w-lg rounded-2xl bg-white shadow-2xl">
        <div className="flex items-center justify-between border-b border-gray-100 px-6 py-4">
          <h2 className="text-lg font-semibold text-gray-900">Create Assignment</h2>
          <button onClick={onClose} className="rounded-full p-1 text-gray-400 hover:bg-gray-100 hover:text-gray-600 transition-colors">
            <svg className="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        <form onSubmit={submit} className="p-6 space-y-4">
          {error && (
            <div className="rounded-lg bg-red-50 border border-red-200 p-3 text-sm text-red-700">{error}</div>
          )}

          <div className="space-y-1.5">
            <label className="text-sm font-medium text-gray-700">Title</label>
            <Input placeholder="e.g. Chapter 3 Quiz" value={form.title}
              onChange={e => setForm(f => ({ ...f, title: e.target.value }))} required autoFocus />
          </div>

          <div className="space-y-1.5">
            <label className="text-sm font-medium text-gray-700">Description <span className="text-gray-400 font-normal">(optional)</span></label>
            <textarea rows={2} placeholder="Instructions for students…" value={form.description}
              onChange={e => setForm(f => ({ ...f, description: e.target.value }))}
              className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-blue-500 resize-none" />
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <label className="text-sm font-medium text-gray-700">Class</label>
              {loadingClasses ? (
                <div className="h-10 animate-pulse rounded-md bg-gray-200" />
              ) : (
                <select value={classId} onChange={e => setClassId(e.target.value)}
                  className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500">
                  {classes.map(c => <option key={c.classId} value={c.classId}>{c.name}</option>)}
                </select>
              )}
            </div>
            <div className="space-y-1.5">
              <label className="text-sm font-medium text-gray-700">Subject</label>
              {subjects.length === 0 ? (
                <div className="h-10 flex items-center px-3 text-sm text-gray-400 border border-gray-200 rounded-md bg-gray-50">
                  {classId ? "No subjects" : "Select class first"}
                </div>
              ) : (
                <select value={form.classSubjectId} onChange={e => setForm(f => ({ ...f, classSubjectId: e.target.value }))}
                  className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500">
                  {subjects.map(s => <option key={s.classSubjectId} value={s.classSubjectId}>{s.subjectName}</option>)}
                </select>
              )}
            </div>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <label className="text-sm font-medium text-gray-700">Due date & time</label>
              <input type="datetime-local" required defaultValue={defaultDue}
                onChange={e => setForm(f => ({ ...f, dueAt: e.target.value }))}
                className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
            </div>
            <div className="space-y-1.5">
              <label className="text-sm font-medium text-gray-700">Max marks</label>
              <Input type="number" min={1} max={1000} value={form.maxMarks}
                onChange={e => setForm(f => ({ ...f, maxMarks: e.target.value }))} required />
            </div>
          </div>

          <div className="flex gap-3 pt-2">
            <Button type="submit" className="flex-1" loading={saving}>Create Assignment</Button>
            <Button type="button" variant="outline" onClick={onClose}>Cancel</Button>
          </div>
        </form>
      </div>
    </div>
  );
}
