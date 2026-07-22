"use client";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { api, type ConsentRecord, type DataSubjectRequest, type AdminRequestRow, type AdminConsentRow } from "@/lib/api";
import { useFeature } from "@/lib/use-feature";
import { Button } from "@/components/ui/button";
import { usePermission } from "@/lib/auth-context";
import { ShieldCheck, AlertTriangle, Loader2, CheckCircle2, Clock, XCircle } from "lucide-react";

const REQUEST_TYPES = ["Access", "Deletion", "Correction", "Portability"];

const STATUS_STYLES: Record<string, string> = {
  Pending:    "bg-yellow-100 text-yellow-700",
  InProgress: "bg-blue-100 text-blue-700",
  Completed:  "bg-emerald-100 text-emerald-700",
  Rejected:   "bg-red-100 text-red-700",
};

function StatusBadge({ status }: { status: string }) {
  const icons: Record<string, React.ReactNode> = {
    Pending:    <Clock className="h-3 w-3" />,
    InProgress: <Loader2 className="h-3 w-3 animate-spin" />,
    Completed:  <CheckCircle2 className="h-3 w-3" />,
    Rejected:   <XCircle className="h-3 w-3" />,
  };
  return (
    <span className={`inline-flex items-center gap-1 rounded-full px-2.5 py-0.5 text-[11px] font-semibold ${STATUS_STYLES[status] ?? "bg-gray-100 text-gray-600"}`}>
      {icons[status]}
      {status}
    </span>
  );
}

// ─── User view ────────────────────────────────────────────────────────────────

const CONSENT_LABELS: { key: keyof ConsentRecord; label: string; desc: string }[] = [
  { key: "dataProcessing",        label: "Data Processing",         desc: "I consent to the school processing my personal data for educational purposes." },
  { key: "marketingCommunications",label: "Marketing Communications",desc: "I consent to receiving newsletters and promotional communications from the school." },
  { key: "thirdPartySharing",     label: "Third-Party Sharing",     desc: "I consent to sharing my data with third-party service providers used by the school." },
  { key: "photography",           label: "Photography & Media",     desc: "I consent to being photographed or filmed at school events for publication purposes." },
];

function UserConsentPanel() {
  const [consents, setConsents] = useState<ConsentRecord | null>(null);
  const [saving,   setSaving]   = useState(false);
  const [saved,    setSaved]    = useState(false);
  const [error,    setError]    = useState("");

  useEffect(() => { api.popia.myConsents().then(setConsents).catch(() => {}); }, []);

  async function save() {
    if (!consents) return;
    setSaving(true); setError(""); setSaved(false);
    try {
      await api.popia.updateConsents({
        dataProcessing:          consents.dataProcessing,
        marketingCommunications: consents.marketingCommunications,
        thirdPartySharing:       consents.thirdPartySharing,
        photography:             consents.photography,
      });
      setSaved(true);
      setTimeout(() => setSaved(false), 3000);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to save");
    } finally {
      setSaving(false);
    }
  }

  if (!consents) return <div className="flex items-center justify-center py-10"><Loader2 className="h-6 w-6 animate-spin text-text-muted" /></div>;

  return (
    <div className="rounded-xl border border-border bg-surface-card shadow-sm p-5 space-y-4">
      <div>
        <h3 className="font-semibold text-text-primary">My Consents</h3>
        <p className="text-xs text-text-secondary mt-0.5">You can update your consent preferences at any time. Changes take effect immediately.</p>
      </div>
      {error && (
        <div className="flex items-center gap-2 rounded bg-danger-100 px-3 py-2 text-sm text-danger-700">
          <AlertTriangle className="h-4 w-4 shrink-0" /> {error}
        </div>
      )}
      <div className="space-y-3">
        {CONSENT_LABELS.map(cl => (
          <label key={cl.key as string} className="flex items-start gap-3 cursor-pointer">
            <input type="checkbox"
              checked={!!consents[cl.key as keyof ConsentRecord]}
              onChange={e => setConsents(c => c && ({ ...c, [cl.key]: e.target.checked }))}
              className="mt-0.5 h-4 w-4 rounded border-border text-primary focus:ring-primary" />
            <div>
              <p className="text-sm font-medium text-text-primary">{cl.label}</p>
              <p className="text-xs text-text-secondary">{cl.desc}</p>
            </div>
          </label>
        ))}
      </div>
      <div className="flex items-center gap-3 pt-1">
        <Button onClick={save} loading={saving}>Save Preferences</Button>
        {saved && <span className="text-sm text-success-700 flex items-center gap-1"><CheckCircle2 className="h-4 w-4" /> Saved</span>}
      </div>
    </div>
  );
}

function UserRequestPanel() {
  const [requests, setRequests] = useState<DataSubjectRequest[]>([]);
  const [loading,  setLoading]  = useState(true);
  const [showForm, setShowForm] = useState(false);
  const [form,     setForm]     = useState({ requestType: REQUEST_TYPES[0], description: "" });
  const [submitting, setSubmitting] = useState(false);
  const [error,    setError]    = useState("");

  async function load() {
    try { setRequests(await api.popia.myRequests()); } catch { /**/ } finally { setLoading(false); }
  }
  useEffect(() => { load(); }, []);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setSubmitting(true); setError("");
    try {
      await api.popia.submitRequest({ requestType: form.requestType, description: form.description || undefined });
      setForm({ requestType: REQUEST_TYPES[0], description: "" });
      setShowForm(false);
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to submit");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="rounded-xl border border-border bg-surface-card shadow-sm p-5 space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h3 className="font-semibold text-text-primary">Data Subject Requests</h3>
          <p className="text-xs text-text-secondary mt-0.5">Request access to, deletion of, or correction of your personal data.</p>
        </div>
        <Button onClick={() => setShowForm(v => !v)} variant="outline" size="sm">
          {showForm ? "Cancel" : "New Request"}
        </Button>
      </div>

      {showForm && (
        <form onSubmit={submit} className="rounded-lg bg-primary-50 border border-primary-100 p-4 space-y-3">
          {error && <p className="text-xs text-danger-700">{error}</p>}
          <div className="space-y-1">
            <label className="text-xs font-medium text-text-secondary">Request type</label>
            <select value={form.requestType} onChange={e => setForm(f => ({ ...f, requestType: e.target.value }))}
              className="w-full rounded-md border border-border px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary">
              {REQUEST_TYPES.map(t => <option key={t}>{t}</option>)}
            </select>
          </div>
          <div className="space-y-1">
            <label className="text-xs font-medium text-text-secondary">Details <span className="text-text-muted font-normal">(optional)</span></label>
            <textarea value={form.description} onChange={e => setForm(f => ({ ...f, description: e.target.value }))}
              rows={2} placeholder="Describe your request in detail"
              className="w-full rounded-md border border-border px-3 py-2 text-sm resize-none focus:outline-none focus:ring-2 focus:ring-primary" />
          </div>
          <Button type="submit" loading={submitting} size="sm">Submit Request</Button>
        </form>
      )}

      {loading ? (
        <div className="flex items-center justify-center py-6"><Loader2 className="h-5 w-5 animate-spin text-text-muted" /></div>
      ) : requests.length === 0 ? (
        <p className="text-sm text-text-muted text-center py-4">No requests submitted yet.</p>
      ) : (
        <div className="space-y-2">
          {requests.map(r => (
            <div key={r.requestId} className="rounded-lg border border-border px-4 py-3 flex items-start justify-between gap-3">
              <div>
                <p className="text-sm font-medium text-text-primary">{r.requestType} request</p>
                {r.description && <p className="text-xs text-text-secondary mt-0.5 line-clamp-1">{r.description}</p>}
                {r.adminNotes && <p className="text-xs text-primary mt-0.5">Note: {r.adminNotes}</p>}
                <p className="text-xs text-text-muted mt-1">{new Date(r.createdAt).toLocaleDateString("en-ZA")}</p>
              </div>
              <StatusBadge status={r.status} />
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

// ─── Admin view ───────────────────────────────────────────────────────────────

function AdminRequestsPanel() {
  const [requests,  setRequests]  = useState<AdminRequestRow[]>([]);
  const [filter,    setFilter]    = useState("");
  const [loading,   setLoading]   = useState(true);
  const [updating,  setUpdating]  = useState<string | null>(null);
  const [error,     setError]     = useState("");

  async function load() {
    try { setRequests(await api.popia.adminRequests(filter || undefined)); } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to load");
    } finally { setLoading(false); }
  }

  useEffect(() => { setLoading(true); load(); }, [filter]);

  async function updateStatus(id: string, status: string, adminNotes?: string) {
    setUpdating(id);
    try {
      await api.popia.adminUpdateRequest(id, { status, adminNotes });
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to update");
    } finally {
      setUpdating(null);
    }
  }

  const pending = requests.filter(r => r.status === "Pending");
  const others  = requests.filter(r => r.status !== "Pending");

  return (
    <div className="space-y-4">
      {error && <div className="flex items-center gap-2 rounded bg-danger-100 px-4 py-3 text-sm text-danger-700"><AlertTriangle className="h-4 w-4 shrink-0" /> {error}</div>}

      <div className="flex items-center gap-2">
        {["", "Pending", "InProgress", "Completed", "Rejected"].map(s => (
          <button key={s} onClick={() => setFilter(s)}
            className={`px-3 py-1.5 rounded-full text-xs font-medium transition-colors ${filter === s ? "bg-primary text-white" : "bg-surface-subtle text-text-secondary hover:bg-border"}`}>
            {s || "All"}
          </button>
        ))}
      </div>

      {loading ? (
        <div className="flex items-center justify-center py-10"><Loader2 className="h-6 w-6 animate-spin text-text-muted" /></div>
      ) : requests.length === 0 ? (
        <div className="text-center py-12 text-text-muted text-sm">No requests found.</div>
      ) : (
        <div className="space-y-2">
          {[...pending, ...others].map(r => (
            <div key={r.requestId} className="rounded-xl bg-surface-card border border-border shadow-sm p-4 flex items-start justify-between gap-4">
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-2 flex-wrap">
                  <p className="font-semibold text-text-primary text-sm">{r.name}</p>
                  <span className="text-xs text-text-muted">{r.email}</span>
                </div>
                <p className="text-sm text-text-primary mt-0.5">{r.requestType} request</p>
                {r.description && <p className="text-xs text-text-secondary mt-0.5 line-clamp-2">{r.description}</p>}
                <p className="text-xs text-text-muted mt-1">{new Date(r.createdAt).toLocaleDateString("en-ZA")}</p>
              </div>
              <div className="flex flex-col items-end gap-2 shrink-0">
                <StatusBadge status={r.status} />
                {r.status === "Pending" && (
                  <div className="flex gap-1.5">
                    <button onClick={() => updateStatus(r.requestId, "InProgress")}
                      disabled={updating === r.requestId}
                      className="text-xs text-primary hover:underline disabled:opacity-50">
                      {updating === r.requestId ? "..." : "In Progress"}
                    </button>
                    <button onClick={() => updateStatus(r.requestId, "Completed")}
                      disabled={updating === r.requestId}
                      className="text-xs text-success-700 hover:underline disabled:opacity-50">Complete</button>
                    <button onClick={() => updateStatus(r.requestId, "Rejected")}
                      disabled={updating === r.requestId}
                      className="text-xs text-danger-500 hover:underline disabled:opacity-50">Reject</button>
                  </div>
                )}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

function AdminConsentsPanel() {
  const [consents, setConsents] = useState<AdminConsentRow[]>([]);
  const [loading,  setLoading]  = useState(true);

  useEffect(() => {
    api.popia.adminConsents().then(setConsents).catch(() => {}).finally(() => setLoading(false));
  }, []);

  if (loading) return <div className="flex items-center justify-center py-10"><Loader2 className="h-6 w-6 animate-spin text-text-muted" /></div>;

  return (
    <div className="overflow-x-auto rounded-xl border border-border shadow-sm">
      <table className="min-w-full text-sm">
        <thead className="bg-surface-subtle border-b border-border">
          <tr className="text-xs font-semibold text-text-muted uppercase tracking-wider">
            <th className="px-4 py-3 text-left">Name</th>
            <th className="px-4 py-3 text-center">Data Processing</th>
            <th className="px-4 py-3 text-center">Marketing</th>
            <th className="px-4 py-3 text-center">3rd Party</th>
            <th className="px-4 py-3 text-center">Photography</th>
            <th className="px-4 py-3 text-left">Updated</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-border bg-surface-card">
          {consents.length === 0 ? (
            <tr><td colSpan={6} className="px-4 py-8 text-center text-text-muted text-sm">No consent records yet.</td></tr>
          ) : consents.map(c => (
            <tr key={c.consentRecordId} className="hover:bg-surface-subtle">
              <td className="px-4 py-3">
                <p className="font-medium text-text-primary">{c.name}</p>
                <p className="text-xs text-text-muted">{c.role}</p>
              </td>
              {[c.dataProcessing, c.marketingCommunications, c.thirdPartySharing, c.photography].map((v, i) => (
                <td key={i} className="px-4 py-3 text-center">
                  {v ? <CheckCircle2 className="h-4 w-4 text-success-500 mx-auto" /> : <XCircle className="h-4 w-4 text-text-muted mx-auto" />}
                </td>
              ))}
              <td className="px-4 py-3 text-xs text-text-muted">{new Date(c.updatedAt).toLocaleDateString("en-ZA")}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

// ─── Page ─────────────────────────────────────────────────────────────────────

export default function PopiaPage() {
  const router = useRouter();
  const hasPopia = useFeature("popiaCentre");
  const isAdmin = usePermission("system.popia_admin"); // Step 8
  const [tab,  setTab]  = useState<"consents" | "requests">("consents");

  if (!hasPopia) {
    return (
      <div className="flex flex-col items-center justify-center h-96 text-center px-4">
        <ShieldCheck className="h-12 w-12 text-text-muted mb-4" />
        <h2 className="text-lg font-semibold text-text-primary">POPIA Centre not enabled</h2>
        <p className="text-sm text-text-muted mt-1">Enable the POPIA Centre in Settings.</p>
        <button onClick={() => router.push("/settings")} className="mt-4 text-sm text-primary hover:underline">Go to Settings</button>
      </div>
    );
  }

  return (
    <div className="p-4 md:p-6 lg:p-8 max-w-4xl mx-auto space-y-6">
      <div>
        <h1 className="text-2xl font-semibold text-text-primary tracking-tight">POPIA Centre</h1>
        <p className="text-sm text-text-secondary mt-1">
          {isAdmin ? "Manage data subject consents and requests in compliance with POPIA." : "Manage your privacy preferences and data rights under POPIA."}
        </p>
      </div>

      {isAdmin && (
        <div className="flex gap-1 border-b border-border">
          {[{ id: "consents", label: "Consent Register" }, { id: "requests", label: "Data Subject Requests" }].map(t => (
            <button key={t.id} onClick={() => setTab(t.id as "consents" | "requests")}
              className={`px-4 py-2.5 text-sm font-medium border-b-2 transition-colors ${tab === t.id ? "border-primary text-primary" : "border-transparent text-text-secondary hover:text-text-primary"}`}>
              {t.label}
            </button>
          ))}
        </div>
      )}

      {isAdmin ? (
        tab === "consents" ? <AdminConsentsPanel /> : <AdminRequestsPanel />
      ) : (
        <div className="space-y-4">
          <UserConsentPanel />
          <UserRequestPanel />
        </div>
      )}
    </div>
  );
}
