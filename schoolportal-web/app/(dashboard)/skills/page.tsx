"use client";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { api, type SkillEntry, type User } from "@/lib/api";
import { useFeature } from "@/lib/use-feature";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { useIdentity } from "@/lib/auth-context";
import { Star, Plus, Trash2, CheckCircle2, Loader2, AlertTriangle, X } from "lucide-react";

const CATEGORIES = ["Academic", "Leadership", "Sport", "Arts & Culture", "Community", "Technology", "Other"];

const CATEGORY_COLOURS: Record<string, string> = {
  Academic:        "bg-blue-100 text-blue-700",
  Leadership:      "bg-purple-100 text-purple-700",
  Sport:           "bg-green-100 text-green-700",
  "Arts & Culture":"bg-pink-100 text-pink-700",
  Community:       "bg-orange-100 text-orange-700",
  Technology:      "bg-cyan-100 text-cyan-700",
  Other:           "bg-gray-100 text-gray-600",
};

function CategoryBadge({ category }: { category: string }) {
  return (
    <span className={`inline-block rounded-full px-2.5 py-0.5 text-[11px] font-semibold ${CATEGORY_COLOURS[category] ?? "bg-gray-100 text-gray-600"}`}>
      {category}
    </span>
  );
}

// ─── Student view ─────────────────────────────────────────────────────────────

function MySkillsView() {
  const [skills,   setSkills]   = useState<SkillEntry[]>([]);
  const [loading,  setLoading]  = useState(true);
  const [error,    setError]    = useState("");
  const [showForm, setShowForm] = useState(false);
  const [form,     setForm]     = useState({ title: "", category: CATEGORIES[0], description: "", date: new Date().toISOString().slice(0, 10) });
  const [saving,   setSaving]   = useState(false);
  const [deleting, setDeleting] = useState<string | null>(null);

  async function load() {
    try {
      setSkills(await api.skills.mine());
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to load skills");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { load(); }, []);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setSaving(true);
    try {
      await api.skills.create({
        title: form.title,
        category: form.category,
        description: form.description || undefined,
        date: new Date(form.date).toISOString(),
      });
      setForm({ title: "", category: CATEGORIES[0], description: "", date: new Date().toISOString().slice(0, 10) });
      setShowForm(false);
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to save");
    } finally {
      setSaving(false);
    }
  }

  async function remove(id: string) {
    setDeleting(id);
    try {
      await api.skills.delete(id);
      setSkills(s => s.filter(x => x.skillEntryId !== id));
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to delete");
    } finally {
      setDeleting(null);
    }
  }

  if (loading) return <div className="flex items-center justify-center py-20"><Loader2 className="h-8 w-8 animate-spin text-text-muted" /></div>;

  return (
    <div className="space-y-4">
      {error && (
        <div className="flex items-center gap-2 rounded-lg bg-danger-100 px-4 py-3 text-sm text-danger-700">
          <AlertTriangle className="h-4 w-4 shrink-0" /> {error}
        </div>
      )}

      <div className="flex items-center justify-between">
        <p className="text-sm text-text-secondary">{skills.length} skill{skills.length !== 1 ? "s" : ""} recorded</p>
        <Button onClick={() => setShowForm(v => !v)} variant={showForm ? "outline" : "default"} className="gap-2">
          {showForm ? <X className="h-4 w-4" /> : <Plus className="h-4 w-4" />}
          {showForm ? "Cancel" : "Add Skill"}
        </Button>
      </div>

      {showForm && (
        <form onSubmit={submit} className="rounded-xl border border-primary-100 bg-primary-50 p-5 space-y-3">
          <h3 className="font-semibold text-text-primary text-sm">New Skill Entry</h3>
          <div className="grid gap-3 sm:grid-cols-2">
            <div className="space-y-1">
              <label className="text-xs font-medium text-text-secondary">Title</label>
              <Input value={form.title} onChange={e => setForm(f => ({ ...f, title: e.target.value }))} placeholder="e.g. Debate team captain" required />
            </div>
            <div className="space-y-1">
              <label className="text-xs font-medium text-text-secondary">Category</label>
              <select value={form.category} onChange={e => setForm(f => ({ ...f, category: e.target.value }))}
                className="w-full rounded-md border border-border px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary">
                {CATEGORIES.map(c => <option key={c}>{c}</option>)}
              </select>
            </div>
            <div className="space-y-1">
              <label className="text-xs font-medium text-text-secondary">Date</label>
              <Input type="date" value={form.date} onChange={e => setForm(f => ({ ...f, date: e.target.value }))} required />
            </div>
            <div className="space-y-1 sm:col-span-2">
              <label className="text-xs font-medium text-text-secondary">Description <span className="text-text-muted font-normal">(optional)</span></label>
              <textarea value={form.description} onChange={e => setForm(f => ({ ...f, description: e.target.value }))}
                rows={2} placeholder="Brief description of the achievement or skill"
                className="w-full rounded-md border border-border px-3 py-2 text-sm resize-none focus:outline-none focus:ring-2 focus:ring-primary" />
            </div>
          </div>
          <Button type="submit" loading={saving} className="gap-2"><Plus className="h-4 w-4" /> Save Skill</Button>
        </form>
      )}

      {skills.length === 0 ? (
        <div className="rounded-xl border-2 border-dashed border-border py-16 text-center">
          <Star className="h-10 w-10 text-text-muted mx-auto mb-3" />
          <p className="text-text-secondary">No skills recorded yet.</p>
          <p className="text-text-muted text-sm mt-1">Add your first skill to start building your profile.</p>
        </div>
      ) : (
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {skills.map(s => (
            <div key={s.skillEntryId} className="rounded-xl bg-surface-card border border-border shadow-sm p-4 flex flex-col gap-2">
              <div className="flex items-start justify-between gap-2">
                <div className="flex-1 min-w-0">
                  <p className="font-semibold text-text-primary truncate">{s.title}</p>
                  <p className="text-xs text-text-muted mt-0.5">{new Date(s.date).toLocaleDateString("en-ZA", { day: "2-digit", month: "long", year: "numeric" })}</p>
                </div>
                <button onClick={() => remove(s.skillEntryId)} disabled={deleting === s.skillEntryId}
                  className="text-text-muted hover:text-danger-500 transition-colors disabled:opacity-50">
                  {deleting === s.skillEntryId ? <Loader2 className="h-4 w-4 animate-spin" /> : <Trash2 className="h-4 w-4" />}
                </button>
              </div>
              <CategoryBadge category={s.category} />
              {s.description && <p className="text-xs text-text-secondary line-clamp-2">{s.description}</p>}
              {s.endorsedByName ? (
                <div className="flex items-center gap-1.5 text-xs text-success-700 mt-auto pt-1 border-t border-border">
                  <CheckCircle2 className="h-3.5 w-3.5" />
                  Endorsed by {s.endorsedByName}
                </div>
              ) : (
                <p className="text-xs text-text-muted mt-auto pt-1 border-t border-border">Not yet endorsed</p>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

// ─── Admin / Teacher view ─────────────────────────────────────────────────────

function StaffSkillsView() {
  const [users,    setUsers]    = useState<User[]>([]);
  const [userId,   setUserId]   = useState("");
  const [skills,   setSkills]   = useState<SkillEntry[]>([]);
  const [loading,  setLoading]  = useState(false);
  const [error,    setError]    = useState("");
  const [endorsing, setEndorsing] = useState<string | null>(null);

  useEffect(() => {
    api.users.list({ role: "Student", pageSize: 200 }).then(r => {
      setUsers(r.items);
      if (r.items.length) setUserId(r.items[0].userId);
    }).catch(() => {});
  }, []);

  useEffect(() => {
    if (!userId) return;
    setLoading(true);
    setError("");
    api.skills.learnerSkills(userId).then(setSkills).catch(e => {
      setError(e instanceof Error ? e.message : "Failed to load");
    }).finally(() => setLoading(false));
  }, [userId]);

  async function endorse(id: string) {
    setEndorsing(id);
    try {
      await api.skills.endorse(id);
      setSkills(prev => prev.map(s =>
        s.skillEntryId === id ? { ...s, endorsedAt: new Date().toISOString(), endorsedByName: "You" } : s
      ));
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to endorse");
    } finally {
      setEndorsing(null);
    }
  }

  const selected = users.find(u => u.userId === userId);

  return (
    <div className="space-y-4">
      <div className="flex items-end gap-3 flex-wrap">
        <div className="space-y-1">
          <label className="text-xs font-medium text-text-secondary">Learner</label>
          <select value={userId} onChange={e => setUserId(e.target.value)}
            className="rounded-md border border-border px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary min-w-[200px]">
            {users.map(u => <option key={u.userId} value={u.userId}>{u.firstName} {u.lastName}</option>)}
          </select>
        </div>
      </div>

      {error && (
        <div className="flex items-center gap-2 rounded-lg bg-danger-100 px-4 py-3 text-sm text-danger-700">
          <AlertTriangle className="h-4 w-4 shrink-0" /> {error}
        </div>
      )}

      {loading ? (
        <div className="flex items-center justify-center py-20"><Loader2 className="h-8 w-8 animate-spin text-text-muted" /></div>
      ) : skills.length === 0 ? (
        <div className="rounded-xl border-2 border-dashed border-border py-12 text-center">
          <Star className="h-8 w-8 text-text-muted mx-auto mb-2" />
          <p className="text-sm text-text-secondary">{selected?.firstName} has not recorded any skills yet.</p>
        </div>
      ) : (
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {skills.map(s => (
            <div key={s.skillEntryId} className="rounded-xl bg-surface-card border border-border shadow-sm p-4 flex flex-col gap-2">
              <div>
                <p className="font-semibold text-text-primary">{s.title}</p>
                <p className="text-xs text-text-muted">{new Date(s.date).toLocaleDateString("en-ZA", { day: "2-digit", month: "long", year: "numeric" })}</p>
              </div>
              <CategoryBadge category={s.category} />
              {s.description && <p className="text-xs text-text-secondary line-clamp-2">{s.description}</p>}
              <div className="mt-auto pt-1 border-t border-border">
                {s.endorsedByName ? (
                  <div className="flex items-center gap-1.5 text-xs text-success-700">
                    <CheckCircle2 className="h-3.5 w-3.5" /> Endorsed by {s.endorsedByName}
                  </div>
                ) : (
                  <button onClick={() => endorse(s.skillEntryId)} disabled={endorsing === s.skillEntryId}
                    className="flex items-center gap-1.5 text-xs text-primary hover:text-primary-800 disabled:opacity-50 transition-colors">
                    {endorsing === s.skillEntryId ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <CheckCircle2 className="h-3.5 w-3.5" />}
                    Endorse this skill
                  </button>
                )}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

// ─── Page ─────────────────────────────────────────────────────────────────────

export default function SkillsPage() {
  const router = useRouter();
  const hasSkillsProfile = useFeature("skillsProfile");
  const identity = useIdentity(); // Step 8

  if (!hasSkillsProfile) {
    return (
      <div className="flex flex-col items-center justify-center h-96 text-center px-4">
        <Star className="h-12 w-12 text-text-muted mb-4" />
        <h2 className="text-lg font-semibold text-text-primary">Skills Profile not enabled</h2>
        <p className="text-sm text-text-muted mt-1">Enable the Skills Profile feature in Settings.</p>
        <button onClick={() => router.push("/settings")} className="mt-4 text-sm text-primary hover:underline">Go to Settings</button>
      </div>
    );
  }

  return (
    <div className="p-4 md:p-6 lg:p-8 max-w-5xl mx-auto space-y-6">
      <div>
        <h1 className="text-2xl font-semibold text-text-primary tracking-tight">Skills Profile</h1>
        <p className="text-sm text-text-secondary mt-1">
          {identity === "Learner" ? "Record your skills, achievements, and activities." : "View and endorse learner skills and achievements."}
        </p>
      </div>
      {identity === "Learner" ? <MySkillsView /> : identity ? <StaffSkillsView /> : null}
    </div>
  );
}
