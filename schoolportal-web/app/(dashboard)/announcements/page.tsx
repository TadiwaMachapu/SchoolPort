"use client";
import { useEffect, useState } from "react";
import { api, type Announcement } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Megaphone, Trash2 } from "lucide-react";

const AUDIENCE_OPTIONS = ["All", "Teachers", "Students", "Parents", "Class"];

export default function AnnouncementsPage() {
  const [items,   setItems]   = useState<Announcement[]>([]);
  const [loading, setLoading] = useState(true);
  const [error,   setError]   = useState("");
  const [showNew, setShowNew] = useState(false);

  async function load() {
    setLoading(true);
    setError("");
    try {
      const res = await api.announcements.list({ pageSize: 50 });
      setItems(res.items);
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Failed to load");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { load(); }, []);

  async function remove(id: string) {
    if (!confirm("Delete this announcement?")) return;
    try { await api.announcements.delete(id); } catch { /* ignore */ }
    load();
  }

  const AUDIENCE_COLORS: Record<string, string> = {
    All:      "bg-blue-50 text-blue-700 ring-1 ring-blue-200/60",
    Teachers: "bg-violet-50 text-violet-700 ring-1 ring-violet-200/60",
    Students: "bg-emerald-50 text-emerald-700 ring-1 ring-emerald-200/60",
    Parents:  "bg-amber-50 text-amber-700 ring-1 ring-amber-200/60",
    Class:    "bg-teal-50 text-teal-700 ring-1 ring-teal-200/60",
  };

  return (
    <div className="p-6 lg:p-8">
      <div className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-gray-900 tracking-tight">Announcements</h1>
          <p className="text-sm text-gray-500 mt-1">{items.length} announcement{items.length !== 1 ? "s" : ""}</p>
        </div>
        <Button onClick={() => setShowNew(true)}>+ New Announcement</Button>
      </div>

      {error && (
        <div className="mb-4 rounded-lg bg-red-50 border border-red-200 p-3 text-sm text-red-700">{error}</div>
      )}

      {loading ? (
        <div className="space-y-4">
          {[1, 2, 3].map(i => (
            <Card key={i}>
              <CardContent className="p-6 space-y-3">
                <div className="flex items-center gap-3">
                  <Skeleton className="h-5 w-48" />
                  <Skeleton className="h-5 w-16 rounded-full" />
                </div>
                <Skeleton className="h-4 w-full" />
                <Skeleton className="h-4 w-3/4" />
                <Skeleton className="h-3 w-32" />
              </CardContent>
            </Card>
          ))}
        </div>
      ) : items.length === 0 ? (
        <div className="rounded-xl border-2 border-dashed border-gray-300 py-16 text-center">
          <div className="flex justify-center mb-4">
            <Megaphone className="h-10 w-10 text-gray-300" />
          </div>
          <p className="text-lg font-medium text-gray-700">No announcements yet</p>
          <p className="text-sm text-gray-400 mt-1">Create the first announcement for your school</p>
          <Button className="mt-4" onClick={() => setShowNew(true)}>+ New Announcement</Button>
        </div>
      ) : (
        <div className="space-y-3">
          {items.map(a => (
            <Card key={a.announcementId} className={`transition-all hover:shadow-md ${!a.isActive ? "opacity-60" : ""}`}>
              <CardContent className="p-6">
                <div className="flex items-start justify-between gap-4">
                  <div className="flex-1 min-w-0">
                    <div className="flex flex-wrap items-center gap-2 mb-2">
                      <h3 className="font-semibold text-gray-900">{a.title}</h3>
                      <span className={`inline-flex items-center rounded-md px-2 py-0.5 text-xs font-medium ${AUDIENCE_COLORS[a.audience] ?? "bg-gray-100 text-gray-700"}`}>
                        {a.audience}
                      </span>
                      {!a.isActive && (
                        <span className="inline-flex items-center rounded-md px-2 py-0.5 text-xs font-medium bg-gray-100 text-gray-500 ring-1 ring-gray-200">
                          Expired
                        </span>
                      )}
                    </div>
                    <p className="text-sm text-gray-600 leading-relaxed">{a.content}</p>
                    <div className="flex items-center gap-2 mt-3 text-xs text-gray-400">
                      <span>By {a.createdByName}</span>
                      <span>·</span>
                      <span>{new Date(a.createdAt).toLocaleDateString("en-US", { month: "short", day: "numeric", year: "numeric" })}</span>
                      {a.expiresAt && (
                        <>
                          <span>·</span>
                          <span>Expires {new Date(a.expiresAt).toLocaleDateString("en-US", { month: "short", day: "numeric" })}</span>
                        </>
                      )}
                    </div>
                  </div>
                  <button onClick={() => remove(a.announcementId)}
                    className="shrink-0 p-1.5 rounded-md text-gray-300 hover:text-red-500 hover:bg-red-50 transition-colors"
                    title="Delete">
                    <Trash2 className="h-4 w-4" />
                  </button>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      {showNew && <NewAnnouncementModal onClose={() => { setShowNew(false); load(); }} />}
    </div>
  );
}

function NewAnnouncementModal({ onClose }: { onClose: () => void }) {
  const [form, setForm] = useState({
    title: "", content: "", audience: "All", expiresAt: "",
  });
  const [saving, setSaving] = useState(false);
  const [error,  setError]  = useState("");

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setSaving(true);
    setError("");
    try {
      await api.announcements.create({
        title:     form.title,
        content:   form.content,
        audience:  form.audience,
        expiresAt: form.expiresAt || undefined,
      });
      onClose();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : "Failed to create");
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm p-4">
      <div className="w-full max-w-lg rounded-2xl bg-white shadow-2xl">
        <div className="flex items-center justify-between border-b border-gray-100 px-6 py-4">
          <h2 className="text-lg font-semibold text-gray-900">New Announcement</h2>
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
            <Input placeholder="e.g. School closed on Monday" value={form.title}
              onChange={e => setForm(f => ({ ...f, title: e.target.value }))} required autoFocus />
          </div>
          <div className="space-y-1.5">
            <label className="text-sm font-medium text-gray-700">Message</label>
            <textarea rows={4} placeholder="Write your announcement…" value={form.content}
              onChange={e => setForm(f => ({ ...f, content: e.target.value }))} required
              className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-blue-500 resize-none" />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <label className="text-sm font-medium text-gray-700">Audience</label>
              <select value={form.audience} onChange={e => setForm(f => ({ ...f, audience: e.target.value }))}
                className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500">
                {AUDIENCE_OPTIONS.map(a => <option key={a}>{a}</option>)}
              </select>
            </div>
            <div className="space-y-1.5">
              <label className="text-sm font-medium text-gray-700">Expires (optional)</label>
              <input type="date" value={form.expiresAt}
                onChange={e => setForm(f => ({ ...f, expiresAt: e.target.value }))}
                className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
            </div>
          </div>
          <div className="flex gap-3 pt-2">
            <Button type="submit" className="flex-1" loading={saving}>Post Announcement</Button>
            <Button type="button" variant="outline" onClick={onClose}>Cancel</Button>
          </div>
        </form>
      </div>
    </div>
  );
}
