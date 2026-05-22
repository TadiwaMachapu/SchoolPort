"use client";
import { useEffect, useState } from "react";
import Link from "next/link";
import { api, type Class } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardContent } from "@/components/ui/card";
import { SkeletonTable } from "@/components/ui/skeleton";

export default function ClassesPage() {
  const [classes, setClasses] = useState<Class[]>([]);
  const [total,   setTotal]   = useState(0);
  const [loading, setLoading] = useState(true);
  const [showAdd, setShowAdd] = useState(false);
  const [editClass, setEditClass] = useState<Class | null>(null);

  async function load() {
    setLoading(true);
    try {
      const res = await api.classes.list({ pageSize: 50 });
      setClasses(res.items);
      setTotal(res.total);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { load(); }, []);

  return (
    <div className="p-8">
      <div className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold text-gray-900">Classes</h1>
          <p className="text-gray-500 mt-1">{total} class{total !== 1 ? "es" : ""}</p>
        </div>
        <Button onClick={() => setShowAdd(true)}>+ Add Class</Button>
      </div>

      {loading ? (
        <SkeletonTable rows={6} cols={6} />
      ) : classes.length === 0 ? (
        <div className="rounded-xl border-2 border-dashed border-gray-300 py-16 text-center">
          <div className="text-5xl mb-4">🏫</div>
          <p className="text-lg font-medium text-gray-700">No classes yet</p>
          <p className="text-sm text-gray-400 mt-1">Create the first class for your school</p>
          <Button className="mt-4" onClick={() => setShowAdd(true)}>+ Add Class</Button>
        </div>
      ) : (
        <Card>
          <CardContent className="p-0">
            <table className="w-full text-sm">
              <thead className="border-b border-gray-200 bg-gray-50">
                <tr>
                  {["Class Name", "Grade", "Year", "Teacher", "Students", "Capacity", "Actions"].map(h => (
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
                        <div className="w-16 h-1.5 rounded-full bg-gray-200 overflow-hidden">
                          {c.maxCapacity && (
                            <div className="h-full rounded-full bg-blue-500"
                              style={{ width: `${Math.min(100, (c.studentCount / c.maxCapacity) * 100)}%` }} />
                          )}
                        </div>
                        <span className="text-sm text-gray-700">{c.studentCount}</span>
                      </div>
                    </td>
                    <td className="px-6 py-4 text-gray-500">{c.maxCapacity ?? "—"}</td>
                    <td className="px-6 py-4">
                      <button onClick={() => setEditClass(c)}
                        className="text-xs text-blue-600 hover:text-blue-800 font-medium hover:underline">
                        Edit
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </CardContent>
        </Card>
      )}

      {(showAdd || editClass) && (
        <ClassModal
          cls={editClass ?? undefined}
          onClose={() => { setShowAdd(false); setEditClass(null); load(); }}
        />
      )}
    </div>
  );
}

function ClassModal({ cls, onClose }: { cls?: Class; onClose: () => void }) {
  const isEdit = !!cls;
  const [form, setForm] = useState({
    name:         cls?.name         ?? "",
    gradeLevel:   cls?.gradeLevel   ? String(cls.gradeLevel)   : "",
    academicYear: cls?.academicYear ? String(cls.academicYear) : String(new Date().getFullYear()),
    maxCapacity:  cls?.maxCapacity  ? String(cls.maxCapacity)  : "",
  });
  const [saving, setSaving] = useState(false);
  const [error,  setError]  = useState("");

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setSaving(true);
    setError("");
    const body = {
      name:         form.name,
      gradeLevel:   form.gradeLevel   ? Number(form.gradeLevel)   : undefined,
      academicYear: form.academicYear ? Number(form.academicYear) : undefined,
      maxCapacity:  form.maxCapacity  ? Number(form.maxCapacity)  : undefined,
    };
    try {
      if (isEdit) {
        await api.classes.update(cls!.classId, body);
      } else {
        await api.classes.create(body);
      }
      onClose();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : "Save failed");
    } finally {
      setSaving(false);
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
          {error && (
            <div className="rounded-lg bg-red-50 border border-red-200 p-3 text-sm text-red-700">{error}</div>
          )}
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
            <Button type="submit" className="flex-1" loading={saving}>
              {isEdit ? "Save changes" : "Create class"}
            </Button>
            <Button type="button" variant="outline" onClick={onClose}>Cancel</Button>
          </div>
        </form>
      </div>
    </div>
  );
}
