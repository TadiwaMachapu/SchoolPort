"use client";
import Link from "next/link";
import { type Class } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardContent } from "@/components/ui/card";
import { getClientRole } from "@/lib/utils";
import { GraduationCap, BookOpen, Users, ChevronRight } from "lucide-react";
import { useClassesList, useCreateClass, useUpdateClass } from "@/features/classes/api/hooks";
import { useToastStore } from "@/stores/toast.store";
import { useState } from "react";

export default function ClassesPage() {
  const role    = getClientRole();
  const isAdmin = role === "Admin";
  const toast   = useToastStore();

  const [showAdd,   setShowAdd]   = useState(false);
  const [editClass, setEditClass] = useState<Class | null>(null);

  const { data, isLoading } = useClassesList({ pageSize: 50 });
  const classes = data?.items ?? [];
  const total   = data?.total ?? 0;

  return (
    <div className="p-4 md:p-6 lg:p-8">
      <div className="mb-5 md:mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-xl md:text-2xl font-semibold text-gray-900 tracking-tight">Classes</h1>
          <p className="text-xs md:text-sm text-gray-500 mt-0.5">{total} class{total !== 1 ? "es" : ""}</p>
        </div>
        {isAdmin && <Button onClick={() => setShowAdd(true)}>+ Add Class</Button>}
      </div>

      {isLoading ? (
        <div className="space-y-3">
          {Array.from({ length: 6 }).map((_, i) => (
            <div key={i} className="h-20 animate-pulse rounded-2xl bg-gray-100" />
          ))}
        </div>
      ) : classes.length === 0 ? (
        <div className="rounded-xl border-2 border-dashed border-gray-300 py-16 text-center">
          <div className="flex justify-center mb-4">
            <GraduationCap className="h-10 w-10 text-gray-300" />
          </div>
          <p className="text-lg font-medium text-gray-700">No classes yet</p>
          <p className="text-sm text-gray-400 mt-1">
            {isAdmin ? "Create the first class for your school" : "Classes created by admins will appear here"}
          </p>
          {isAdmin && <Button className="mt-4" onClick={() => setShowAdd(true)}>+ Add Class</Button>}
        </div>
      ) : (
        <>
          {/* Mobile card list */}
          <div className="md:hidden space-y-2">
            {classes.map(c => (
              <div key={c.classId}
                className="flex items-center gap-3 rounded-xl border border-gray-200 bg-white px-4 py-3">
                <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-blue-50 text-blue-600">
                  <BookOpen className="h-5 w-5" />
                </div>
                <div className="flex-1 min-w-0">
                  <Link href={`/classes/${c.classId}`}
                    className="font-medium text-gray-900 hover:text-blue-600 truncate block">
                    {c.name}
                  </Link>
                  <div className="flex items-center gap-2 text-xs text-gray-400 mt-0.5">
                    {c.gradeLevel && <span>Grade {c.gradeLevel}</span>}
                    {c.gradeLevel && c.teacherName && <span>·</span>}
                    {c.teacherName && <span>{c.teacherName}</span>}
                  </div>
                  <div className="flex items-center gap-1 text-xs text-gray-500 mt-0.5">
                    <Users className="h-3 w-3" />
                    {c.studentCount}{c.maxCapacity ? ` / ${c.maxCapacity}` : ""} students
                  </div>
                </div>
                {isAdmin && (
                  <button onClick={() => setEditClass(c)}
                    className="shrink-0 rounded-lg border border-gray-200 px-3 py-1.5 text-xs font-medium text-gray-600 hover:border-blue-300 hover:text-blue-600 transition-colors min-h-[36px]">
                    Edit
                  </button>
                )}
              </div>
            ))}
          </div>

          {/* Desktop table */}
          <Card className="hidden md:block">
            <CardContent className="p-0">
              <table className="w-full text-sm">
                <thead className="border-b border-gray-200 bg-gray-50">
                  <tr>
                    {["Class Name", "Grade", "Year", "Teacher", "Students", "Capacity", ...(isAdmin ? ["Actions"] : [])].map(h => (
                      <th key={h} className="px-6 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wider">{h}</th>
                    ))}
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {classes.map(c => (
                    <tr key={c.classId} className="hover:bg-gray-50 transition-colors">
                      <td className="px-6 py-4">
                        <Link href={`/classes/${c.classId}`}
                          className="font-medium text-gray-900 hover:text-blue-600 hover:underline">
                          {c.name}
                        </Link>
                      </td>
                      <td className="px-6 py-4 text-gray-500">{c.gradeLevel ? `Grade ${c.gradeLevel}` : "—"}</td>
                      <td className="px-6 py-4 text-gray-500">{c.academicYear ?? "—"}</td>
                      <td className="px-6 py-4 text-gray-600">{c.teacherName ?? <span className="text-gray-400 italic">Unassigned</span>}</td>
                      <td className="px-6 py-4">
                        <div className="flex items-center gap-2">
                          {c.maxCapacity && (
                            <div className="w-16 h-1.5 rounded-full bg-gray-200 overflow-hidden">
                              <div className="h-full rounded-full bg-blue-500"
                                style={{ width: `${Math.min(100, (c.studentCount / c.maxCapacity) * 100)}%` }} />
                            </div>
                          )}
                          <span className="text-sm text-gray-700">{c.studentCount}</span>
                        </div>
                      </td>
                      <td className="px-6 py-4 text-gray-500">{c.maxCapacity ?? "—"}</td>
                      {isAdmin && (
                        <td className="px-6 py-4">
                          <button onClick={() => setEditClass(c)}
                            className="text-xs text-blue-600 hover:text-blue-800 font-medium hover:underline">
                            Edit
                          </button>
                        </td>
                      )}
                    </tr>
                  ))}
                </tbody>
              </table>
            </CardContent>
          </Card>
        </>
      )}

      {isAdmin && (showAdd || editClass) && (
        <ClassModal
          cls={editClass ?? undefined}
          onClose={() => { setShowAdd(false); setEditClass(null); }}
          onSaved={(msg) => toast.success(msg, "")}
        />
      )}
    </div>
  );
}

function ClassModal({ cls, onClose, onSaved }: { cls?: Class; onClose: () => void; onSaved: (msg: string) => void }) {
  const isEdit     = !!cls;
  const createMut  = useCreateClass();
  const updateMut  = useUpdateClass();
  const isSaving   = createMut.isPending || updateMut.isPending;

  const [form, setForm] = useState({
    name:         cls?.name         ?? "",
    gradeLevel:   cls?.gradeLevel   ? String(cls.gradeLevel)   : "",
    academicYear: cls?.academicYear ? String(cls.academicYear) : String(new Date().getFullYear()),
    maxCapacity:  cls?.maxCapacity  ? String(cls.maxCapacity)  : "",
  });
  const [error, setError] = useState("");

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setError("");
    const body = {
      name:         form.name,
      gradeLevel:   form.gradeLevel   ? Number(form.gradeLevel)   : undefined,
      academicYear: form.academicYear ? Number(form.academicYear) : undefined,
      maxCapacity:  form.maxCapacity  ? Number(form.maxCapacity)  : undefined,
    };
    try {
      if (isEdit) {
        await updateMut.mutateAsync({ id: cls!.classId, body });
        onSaved("Class updated");
      } else {
        await createMut.mutateAsync(body);
        onSaved("Class created");
      }
      onClose();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : "Save failed");
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm p-4">
      <div className="w-full max-w-md rounded-2xl bg-white shadow-2xl">
        <div className="flex items-center justify-between border-b border-gray-100 px-6 py-4">
          <h2 className="text-lg font-semibold text-gray-900">{isEdit ? "Edit Class" : "Add Class"}</h2>
          <button onClick={onClose} className="rounded-full p-1 text-gray-400 hover:bg-gray-100 hover:text-gray-600 transition-colors">
            <svg className="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>
        <form onSubmit={submit} className="p-6 space-y-4">
          {error && <div className="rounded-lg bg-red-50 border border-red-200 p-3 text-sm text-red-700">{error}</div>}
          <div className="space-y-1.5">
            <label className="text-sm font-medium text-gray-700">Class name</label>
            <Input placeholder="e.g. Grade 10A" value={form.name}
              onChange={e => setForm(f => ({ ...f, name: e.target.value }))} required autoFocus />
          </div>
          <div className="grid grid-cols-3 gap-3">
            <div className="space-y-1.5">
              <label className="text-sm font-medium text-gray-700">Grade</label>
              <Input type="number" placeholder="10" min={1} max={13}
                value={form.gradeLevel} onChange={e => setForm(f => ({ ...f, gradeLevel: e.target.value }))} />
            </div>
            <div className="space-y-1.5">
              <label className="text-sm font-medium text-gray-700">Year</label>
              <Input type="number" placeholder="2024" min={2020} max={2030}
                value={form.academicYear} onChange={e => setForm(f => ({ ...f, academicYear: e.target.value }))} />
            </div>
            <div className="space-y-1.5">
              <label className="text-sm font-medium text-gray-700">Capacity</label>
              <Input type="number" placeholder="30" min={1}
                value={form.maxCapacity} onChange={e => setForm(f => ({ ...f, maxCapacity: e.target.value }))} />
            </div>
          </div>
          <div className="flex gap-3 pt-2">
            <Button type="submit" className="flex-1" loading={isSaving}>
              {isEdit ? "Save changes" : "Create class"}
            </Button>
            <Button type="button" variant="outline" onClick={onClose}>Cancel</Button>
          </div>
        </form>
      </div>
    </div>
  );
}
