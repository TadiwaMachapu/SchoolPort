"use client";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { api, type ActivityItem, type ActivityParticipantItem, type MyActivityItem, type User } from "@/lib/api";
import { useFeature } from "@/lib/use-feature";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { getClientRole } from "@/lib/utils";
import { Trophy, Plus, Trash2, Users, ChevronDown, ChevronUp, Loader2, AlertTriangle, X, UserMinus } from "lucide-react";

const ACTIVITY_TYPES = ["Sport", "Cultural", "Academic", "Community", "Other"];

const TYPE_COLOURS: Record<string, string> = {
  Sport:     "bg-green-100 text-green-700",
  Cultural:  "bg-pink-100 text-pink-700",
  Academic:  "bg-blue-100 text-blue-700",
  Community: "bg-orange-100 text-orange-700",
  Other:     "bg-gray-100 text-gray-600",
};

function TypeBadge({ type }: { type: string }) {
  return (
    <span className={`inline-block rounded-full px-2.5 py-0.5 text-[11px] font-semibold ${TYPE_COLOURS[type] ?? "bg-gray-100 text-gray-600"}`}>
      {type}
    </span>
  );
}

// ─── Admin / Teacher view ─────────────────────────────────────────────────────

function AdminActivityRow({
  activity,
  role,
  onDelete,
}: {
  activity: ActivityItem;
  role: string;
  onDelete: (id: string) => void;
}) {
  const [expanded,     setExpanded]     = useState(false);
  const [participants, setParticipants] = useState<ActivityParticipantItem[]>([]);
  const [loadingP,     setLoadingP]     = useState(false);
  const [students,     setStudents]     = useState<User[]>([]);
  const [addUserId,    setAddUserId]    = useState("");
  const [addNotes,     setAddNotes]     = useState("");
  const [adding,       setAdding]       = useState(false);
  const [removing,     setRemoving]     = useState<string | null>(null);
  const [error,        setError]        = useState("");

  async function toggle() {
    if (!expanded && participants.length === 0) {
      setLoadingP(true);
      try {
        const [p, u] = await Promise.all([
          api.activities.participants(activity.activityId),
          api.users.list({ role: "Student", pageSize: 200 }),
        ]);
        setParticipants(p);
        setStudents(u.items);
        if (u.items.length) setAddUserId(u.items[0].userId);
      } catch (e) {
        setError(e instanceof Error ? e.message : "Failed to load participants");
      } finally {
        setLoadingP(false);
      }
    }
    setExpanded(v => !v);
  }

  async function addParticipant() {
    if (!addUserId) return;
    setAdding(true);
    try {
      await api.activities.addParticipant(activity.activityId, { userId: addUserId, notes: addNotes || undefined });
      const fresh = await api.activities.participants(activity.activityId);
      setParticipants(fresh);
      setAddNotes("");
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to add participant");
    } finally {
      setAdding(false);
    }
  }

  async function removeParticipant(participantId: string) {
    setRemoving(participantId);
    try {
      await api.activities.removeParticipant(activity.activityId, participantId);
      setParticipants(prev => prev.filter(p => p.activityParticipantId !== participantId));
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to remove participant");
    } finally {
      setRemoving(null);
    }
  }

  const participantUserIds = new Set(participants.map(p => p.studentId));
  const availableStudents = students.filter(u => !participantUserIds.has(u.userId));

  return (
    <div className="rounded-xl bg-white border border-gray-200 shadow-sm overflow-hidden">
      <div className="flex items-center gap-3 px-5 py-4">
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2 flex-wrap">
            <p className="font-semibold text-gray-900">{activity.name}</p>
            <TypeBadge type={activity.activityType} />
          </div>
          <p className="text-xs text-gray-400 mt-0.5">
            {new Date(activity.date).toLocaleDateString("en-ZA", { day: "2-digit", month: "long", year: "numeric" })}
            {" · "}{activity.participantCount} participant{activity.participantCount !== 1 ? "s" : ""}
          </p>
          {activity.description && <p className="text-xs text-gray-500 mt-1 line-clamp-1">{activity.description}</p>}
        </div>
        <div className="flex items-center gap-2 shrink-0">
          <button onClick={toggle}
            className="flex items-center gap-1 text-xs text-gray-500 hover:text-blue-600 transition-colors">
            <Users className="h-4 w-4" />
            {expanded ? <ChevronUp className="h-3.5 w-3.5" /> : <ChevronDown className="h-3.5 w-3.5" />}
          </button>
          {role === "Admin" && (
            <button onClick={() => onDelete(activity.activityId)}
              className="text-gray-300 hover:text-red-400 transition-colors ml-2">
              <Trash2 className="h-4 w-4" />
            </button>
          )}
        </div>
      </div>

      {expanded && (
        <div className="border-t border-gray-100 px-5 py-4 bg-gray-50 space-y-3">
          {error && (
            <div className="flex items-center gap-2 rounded bg-red-50 border border-red-200 px-3 py-2 text-xs text-red-700">
              <AlertTriangle className="h-3.5 w-3.5 shrink-0" /> {error}
            </div>
          )}

          {loadingP ? (
            <div className="flex items-center justify-center py-4"><Loader2 className="h-5 w-5 animate-spin text-gray-400" /></div>
          ) : (
            <>
              {participants.length === 0 ? (
                <p className="text-xs text-gray-400">No participants yet.</p>
              ) : (
                <div className="space-y-1.5">
                  {participants.map(p => (
                    <div key={p.activityParticipantId} className="flex items-center justify-between rounded-md bg-white border border-gray-200 px-3 py-2">
                      <div>
                        <span className="text-sm font-medium text-gray-800">{p.name}</span>
                        <span className="text-xs text-gray-400 ml-2">{p.studentNumber}</span>
                        {p.notes && <span className="text-xs text-gray-400 ml-2">— {p.notes}</span>}
                      </div>
                      <button onClick={() => removeParticipant(p.activityParticipantId)}
                        disabled={removing === p.activityParticipantId}
                        className="text-gray-300 hover:text-red-400 transition-colors disabled:opacity-50">
                        {removing === p.activityParticipantId ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <UserMinus className="h-3.5 w-3.5" />}
                      </button>
                    </div>
                  ))}
                </div>
              )}

              {availableStudents.length > 0 && (
                <div className="flex items-end gap-2 flex-wrap pt-1">
                  <div className="space-y-1 flex-1 min-w-[160px]">
                    <label className="text-[10px] font-medium text-gray-500 uppercase tracking-wider">Add learner</label>
                    <select value={addUserId} onChange={e => setAddUserId(e.target.value)}
                      className="w-full rounded-md border border-gray-300 px-2 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500">
                      {availableStudents.map(u => (
                        <option key={u.userId} value={u.userId}>{u.firstName} {u.lastName}</option>
                      ))}
                    </select>
                  </div>
                  <div className="space-y-1 flex-1 min-w-[120px]">
                    <label className="text-[10px] font-medium text-gray-500 uppercase tracking-wider">Notes (optional)</label>
                    <Input value={addNotes} onChange={e => setAddNotes(e.target.value)} placeholder="e.g. Captain" className="py-1.5 text-sm" />
                  </div>
                  <Button onClick={addParticipant} loading={adding} className="gap-1.5 py-1.5 text-sm h-auto">
                    <Plus className="h-3.5 w-3.5" /> Add
                  </Button>
                </div>
              )}
            </>
          )}
        </div>
      )}
    </div>
  );
}

function StaffActivitiesView({ role }: { role: string }) {
  const [activities, setActivities] = useState<ActivityItem[]>([]);
  const [loading,    setLoading]    = useState(true);
  const [error,      setError]      = useState("");
  const [showForm,   setShowForm]   = useState(false);
  const [form,       setForm]       = useState({ name: "", description: "", activityType: ACTIVITY_TYPES[0], date: new Date().toISOString().slice(0, 10) });
  const [saving,     setSaving]     = useState(false);

  async function load() {
    try {
      setActivities(await api.activities.list());
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to load");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { load(); }, []);

  async function create(e: React.FormEvent) {
    e.preventDefault();
    setSaving(true);
    try {
      await api.activities.create({
        name: form.name,
        description: form.description || undefined,
        activityType: form.activityType,
        date: new Date(form.date).toISOString(),
      });
      setForm({ name: "", description: "", activityType: ACTIVITY_TYPES[0], date: new Date().toISOString().slice(0, 10) });
      setShowForm(false);
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to create");
    } finally {
      setSaving(false);
    }
  }

  async function remove(id: string) {
    try {
      await api.activities.delete(id);
      setActivities(prev => prev.filter(a => a.activityId !== id));
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to delete");
    }
  }

  if (loading) return <div className="flex items-center justify-center py-20"><Loader2 className="h-8 w-8 animate-spin text-gray-400" /></div>;

  return (
    <div className="space-y-4">
      {error && (
        <div className="flex items-center gap-2 rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700">
          <AlertTriangle className="h-4 w-4 shrink-0" /> {error}
        </div>
      )}

      {role === "Admin" && (
        <div className="flex items-center justify-between">
          <p className="text-sm text-gray-500">{activities.length} activit{activities.length !== 1 ? "ies" : "y"}</p>
          <Button onClick={() => setShowForm(v => !v)} variant={showForm ? "outline" : "default"} className="gap-2">
            {showForm ? <X className="h-4 w-4" /> : <Plus className="h-4 w-4" />}
            {showForm ? "Cancel" : "New Activity"}
          </Button>
        </div>
      )}

      {showForm && (
        <form onSubmit={create} className="rounded-xl border border-blue-100 bg-blue-50 p-5 space-y-3">
          <h3 className="font-semibold text-gray-900 text-sm">New Activity</h3>
          <div className="grid gap-3 sm:grid-cols-2">
            <div className="space-y-1 sm:col-span-2">
              <label className="text-xs font-medium text-gray-600">Activity name</label>
              <Input value={form.name} onChange={e => setForm(f => ({ ...f, name: e.target.value }))} placeholder="e.g. Inter-school Athletics Day" required />
            </div>
            <div className="space-y-1">
              <label className="text-xs font-medium text-gray-600">Type</label>
              <select value={form.activityType} onChange={e => setForm(f => ({ ...f, activityType: e.target.value }))}
                className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500">
                {ACTIVITY_TYPES.map(t => <option key={t}>{t}</option>)}
              </select>
            </div>
            <div className="space-y-1">
              <label className="text-xs font-medium text-gray-600">Date</label>
              <Input type="date" value={form.date} onChange={e => setForm(f => ({ ...f, date: e.target.value }))} required />
            </div>
            <div className="space-y-1 sm:col-span-2">
              <label className="text-xs font-medium text-gray-600">Description <span className="text-gray-400 font-normal">(optional)</span></label>
              <textarea value={form.description} onChange={e => setForm(f => ({ ...f, description: e.target.value }))}
                rows={2} placeholder="Brief description"
                className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm resize-none focus:outline-none focus:ring-2 focus:ring-blue-500" />
            </div>
          </div>
          <Button type="submit" loading={saving} className="gap-2"><Plus className="h-4 w-4" /> Create Activity</Button>
        </form>
      )}

      {activities.length === 0 ? (
        <div className="rounded-xl border-2 border-dashed border-gray-200 py-16 text-center">
          <Trophy className="h-10 w-10 text-gray-200 mx-auto mb-3" />
          <p className="text-gray-500">No activities yet.</p>
          {role === "Admin" && <p className="text-gray-400 text-sm mt-1">Create the first activity to get started.</p>}
        </div>
      ) : (
        <div className="space-y-3">
          {activities.map(a => (
            <AdminActivityRow key={a.activityId} activity={a} role={role} onDelete={remove} />
          ))}
        </div>
      )}
    </div>
  );
}

// ─── Student view ─────────────────────────────────────────────────────────────

function StudentActivitiesView() {
  const [activities, setActivities] = useState<MyActivityItem[]>([]);
  const [loading,    setLoading]    = useState(true);
  const [error,      setError]      = useState("");

  useEffect(() => {
    api.activities.mine().then(setActivities).catch(e => {
      setError(e instanceof Error ? e.message : "Failed to load");
    }).finally(() => setLoading(false));
  }, []);

  if (loading) return <div className="flex items-center justify-center py-20"><Loader2 className="h-8 w-8 animate-spin text-gray-400" /></div>;

  return (
    <div className="space-y-4">
      {error && (
        <div className="flex items-center gap-2 rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700">
          <AlertTriangle className="h-4 w-4 shrink-0" /> {error}
        </div>
      )}
      <p className="text-sm text-gray-500">{activities.length} activit{activities.length !== 1 ? "ies" : "y"} recorded</p>

      {activities.length === 0 ? (
        <div className="rounded-xl border-2 border-dashed border-gray-200 py-16 text-center">
          <Trophy className="h-10 w-10 text-gray-200 mx-auto mb-3" />
          <p className="text-gray-500">You have not participated in any activities yet.</p>
          <p className="text-gray-400 text-sm mt-1">Your teacher will add you to activities you participate in.</p>
        </div>
      ) : (
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {activities.map(a => (
            <div key={a.activityParticipantId} className="rounded-xl bg-white border border-gray-200 shadow-sm p-4 space-y-2">
              <div className="flex items-start justify-between gap-2">
                <p className="font-semibold text-gray-900">{a.name}</p>
                <TypeBadge type={a.activityType} />
              </div>
              <p className="text-xs text-gray-400">
                {new Date(a.date).toLocaleDateString("en-ZA", { day: "2-digit", month: "long", year: "numeric" })}
              </p>
              {a.description && <p className="text-xs text-gray-500 line-clamp-2">{a.description}</p>}
              {a.notes && <p className="text-xs text-blue-600 font-medium">{a.notes}</p>}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

// ─── Page ─────────────────────────────────────────────────────────────────────

export default function ActivitiesPage() {
  const router = useRouter();
  const hasSportsCulture = useFeature("sportsCulture");
  const [role, setRole] = useState("");

  useEffect(() => { setRole(getClientRole()); }, []);

  if (!hasSportsCulture) {
    return (
      <div className="flex flex-col items-center justify-center h-96 text-center px-4">
        <Trophy className="h-12 w-12 text-gray-200 mb-4" />
        <h2 className="text-lg font-semibold text-gray-700">Sports & Culture not enabled</h2>
        <p className="text-sm text-gray-400 mt-1">Enable Sports & Culture in Settings.</p>
        <button onClick={() => router.push("/settings")} className="mt-4 text-sm text-blue-600 hover:underline">Go to Settings</button>
      </div>
    );
  }

  return (
    <div className="p-4 md:p-6 lg:p-8 max-w-5xl mx-auto space-y-6">
      <div>
        <h1 className="text-2xl font-semibold text-gray-900 tracking-tight">Sports & Culture</h1>
        <p className="text-sm text-gray-500 mt-1">
          {role === "Student" ? "Your sporting and cultural activity record." : "Manage school activities and learner participation."}
        </p>
      </div>
      {role === "Student" ? <StudentActivitiesView /> : role ? <StaffActivitiesView role={role} /> : null}
    </div>
  );
}
