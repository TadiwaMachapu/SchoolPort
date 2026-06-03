"use client";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { api, type WhatsAppConfig, type WhatsAppLogItem } from "@/lib/api";
import { useFeature } from "@/lib/use-feature";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Phone, Settings, Loader2, AlertTriangle, CheckCircle2, Clock, XCircle, Send } from "lucide-react";

const PROVIDERS = ["None", "Twilio", "360dialog"];

const STATUS_STYLES: Record<string, string> = {
  Queued:    "bg-blue-100 text-blue-700",
  Simulated: "bg-gray-100 text-gray-600",
  Sent:      "bg-emerald-100 text-emerald-700",
  Failed:    "bg-red-100 text-red-700",
};

const TRIGGER_LABELS: Record<string, string> = {
  Absence:      "Absence alert",
  FeeReminder:  "Fee reminder",
  Announcement: "Announcement",
  Manual:       "Manual",
  Test:         "Test",
};

function StatusBadge({ status }: { status: string }) {
  return (
    <span className={`inline-flex items-center gap-1 rounded-full px-2.5 py-0.5 text-[11px] font-semibold ${STATUS_STYLES[status] ?? "bg-gray-100 text-gray-600"}`}>
      {status === "Sent" && <CheckCircle2 className="h-3 w-3" />}
      {status === "Queued" && <Clock className="h-3 w-3" />}
      {status === "Failed" && <XCircle className="h-3 w-3" />}
      {status}
    </span>
  );
}

// ─── Settings panel ───────────────────────────────────────────────────────────

function SettingsPanel() {
  const [config,  setConfig]  = useState<WhatsAppConfig | null>(null);
  const [saving,  setSaving]  = useState(false);
  const [saved,   setSaved]   = useState(false);
  const [error,   setError]   = useState("");

  useEffect(() => {
    api.whatsapp.settings().then(setConfig).catch(() => {});
  }, []);

  async function save() {
    if (!config) return;
    setSaving(true); setError(""); setSaved(false);
    try {
      await api.whatsapp.updateSettings(config);
      setSaved(true);
      setTimeout(() => setSaved(false), 3000);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to save");
    } finally {
      setSaving(false);
    }
  }

  if (!config) return <div className="flex items-center justify-center py-10"><Loader2 className="h-6 w-6 animate-spin text-gray-400" /></div>;

  return (
    <div className="space-y-6">
      {error && (
        <div className="flex items-center gap-2 rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700">
          <AlertTriangle className="h-4 w-4 shrink-0" /> {error}
        </div>
      )}

      {/* Provider config */}
      <div className="rounded-xl border border-gray-200 bg-white shadow-sm p-5 space-y-4">
        <h3 className="font-semibold text-gray-900">Provider Configuration</h3>
        <div className="grid gap-4 sm:grid-cols-2">
          <div className="space-y-1">
            <label className="text-xs font-medium text-gray-600">Provider</label>
            <select value={config.provider} onChange={e => setConfig(c => c && ({ ...c, provider: e.target.value }))}
              className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500">
              {PROVIDERS.map(p => <option key={p}>{p}</option>)}
            </select>
          </div>
          <div className="space-y-1">
            <label className="text-xs font-medium text-gray-600">Phone Number ID</label>
            <Input value={config.phoneNumberId ?? ""} onChange={e => setConfig(c => c && ({ ...c, phoneNumberId: e.target.value }))}
              placeholder="From Meta / provider dashboard" />
          </div>
          <div className="space-y-1 sm:col-span-2">
            <label className="text-xs font-medium text-gray-600">API Key / Auth Token</label>
            <Input type="password" value={config.apiKey ?? ""} onChange={e => setConfig(c => c && ({ ...c, apiKey: e.target.value }))}
              placeholder="Keep secret — stored encrypted" />
          </div>
        </div>
        {config.provider === "None" && (
          <p className="text-xs text-amber-600 bg-amber-50 border border-amber-100 rounded px-3 py-2">
            No provider configured. Messages will be logged as <strong>Simulated</strong> — useful for testing the message flow without real delivery.
          </p>
        )}
      </div>

      {/* Triggers */}
      <div className="rounded-xl border border-gray-200 bg-white shadow-sm p-5 space-y-4">
        <h3 className="font-semibold text-gray-900">Automatic Triggers</h3>
        {[
          { key: "triggerAbsence" as const,      label: "Absent learner alert",   desc: "Notifies parent when their child is marked absent" },
          { key: "triggerFeeReminder" as const,  label: "Fee due reminder",       desc: "Sent when admin triggers a fee reminder batch" },
          { key: "triggerAnnouncement" as const, label: "Announcement broadcast", desc: "Sends school announcements targeted at parents" },
        ].map(t => (
          <label key={t.key} className="flex items-start gap-3 cursor-pointer group">
            <input type="checkbox" checked={config[t.key]} onChange={e => setConfig(c => c && ({ ...c, [t.key]: e.target.checked }))}
              className="mt-0.5 h-4 w-4 rounded border-gray-300 text-blue-600 focus:ring-blue-500" />
            <div>
              <p className="text-sm font-medium text-gray-800">{t.label}</p>
              <p className="text-xs text-gray-500">{t.desc}</p>
            </div>
          </label>
        ))}
      </div>

      {/* Message templates */}
      <div className="rounded-xl border border-gray-200 bg-white shadow-sm p-5 space-y-4">
        <div>
          <h3 className="font-semibold text-gray-900">Message Templates</h3>
          <p className="text-xs text-gray-500 mt-0.5">Available variables: {"{ParentName}"}, {"{LearnerName}"}, {"{Date}"}, {"{FeeName}"}, {"{Amount}"}, {"{DueDate}"}, {"{SchoolName}"}, {"{Title}"}, {"{Body}"}</p>
        </div>
        {[
          { key: "absenceTemplate" as const,      label: "Absence alert" },
          { key: "feeReminderTemplate" as const,  label: "Fee reminder" },
          { key: "announcementTemplate" as const, label: "Announcement" },
        ].map(t => (
          <div key={t.key} className="space-y-1">
            <label className="text-xs font-medium text-gray-600">{t.label}</label>
            <textarea value={config[t.key]} onChange={e => setConfig(c => c && ({ ...c, [t.key]: e.target.value }))}
              rows={2} className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm resize-none focus:outline-none focus:ring-2 focus:ring-blue-500" />
          </div>
        ))}
      </div>

      <div className="flex items-center gap-3">
        <Button onClick={save} loading={saving} className="gap-2">
          <Settings className="h-4 w-4" /> Save Settings
        </Button>
        {saved && <span className="text-sm text-emerald-600 flex items-center gap-1"><CheckCircle2 className="h-4 w-4" /> Saved</span>}
      </div>
    </div>
  );
}

// ─── Compose panel ────────────────────────────────────────────────────────────

function ComposePanel({ onSent }: { onSent: () => void }) {
  const [form,    setForm]    = useState({ recipientName: "", recipientPhone: "", message: "" });
  const [sending, setSending] = useState(false);
  const [result,  setResult]  = useState<{ status: string } | null>(null);
  const [error,   setError]   = useState("");
  const [testing, setTesting] = useState(false);

  async function compose(e: React.FormEvent) {
    e.preventDefault();
    setSending(true); setError(""); setResult(null);
    try {
      const r = await api.whatsapp.compose(form);
      setResult(r);
      setForm({ recipientName: "", recipientPhone: "", message: "" });
      onSent();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to send");
    } finally {
      setSending(false);
    }
  }

  async function sendTest() {
    if (!form.recipientName || !form.recipientPhone) return;
    setTesting(true); setError("");
    try {
      const r = await api.whatsapp.sendTest({ recipientName: form.recipientName, recipientPhone: form.recipientPhone });
      setResult({ status: r.status });
      onSent();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed");
    } finally {
      setTesting(false);
    }
  }

  async function sendAbsenceReminders() {
    setSending(true); setError("");
    try {
      const r = await api.whatsapp.sendAbsenceReminders(new Date().toISOString().slice(0, 10));
      setResult({ status: `${r.queued} absence alert${r.queued !== 1 ? "s" : ""} ${r.status.toLowerCase()}` });
      onSent();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed");
    } finally {
      setSending(false);
    }
  }

  return (
    <div className="space-y-4">
      {error && (
        <div className="flex items-center gap-2 rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700">
          <AlertTriangle className="h-4 w-4 shrink-0" /> {error}
        </div>
      )}
      {result && (
        <div className="flex items-center gap-2 rounded-lg bg-emerald-50 border border-emerald-200 px-4 py-3 text-sm text-emerald-700">
          <CheckCircle2 className="h-4 w-4 shrink-0" /> Message logged as <strong>{result.status}</strong>
        </div>
      )}

      <div className="rounded-xl border border-gray-200 bg-white shadow-sm p-5 space-y-4">
        <h3 className="font-semibold text-gray-900">Send Today&apos;s Absence Alerts</h3>
        <p className="text-sm text-gray-500">Queues a WhatsApp message to each absent learner&apos;s parent for today&apos;s date.</p>
        <Button onClick={sendAbsenceReminders} loading={sending} variant="outline" className="gap-2">
          <Send className="h-4 w-4" /> Send Absence Alerts
        </Button>
      </div>

      <div className="rounded-xl border border-gray-200 bg-white shadow-sm p-5">
        <h3 className="font-semibold text-gray-900 mb-4">Compose Manual Message</h3>
        <form onSubmit={compose} className="space-y-3">
          <div className="grid gap-3 sm:grid-cols-2">
            <div className="space-y-1">
              <label className="text-xs font-medium text-gray-600">Recipient name</label>
              <Input value={form.recipientName} onChange={e => setForm(f => ({ ...f, recipientName: e.target.value }))} placeholder="Parent name" required />
            </div>
            <div className="space-y-1">
              <label className="text-xs font-medium text-gray-600">WhatsApp number</label>
              <Input value={form.recipientPhone} onChange={e => setForm(f => ({ ...f, recipientPhone: e.target.value }))} placeholder="+27 82 000 0000" required />
            </div>
            <div className="space-y-1 sm:col-span-2">
              <label className="text-xs font-medium text-gray-600">Message</label>
              <textarea value={form.message} onChange={e => setForm(f => ({ ...f, message: e.target.value }))}
                rows={3} required placeholder="Type your message..."
                className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm resize-none focus:outline-none focus:ring-2 focus:ring-blue-500" />
            </div>
          </div>
          <div className="flex gap-2 flex-wrap">
            <Button type="submit" loading={sending} className="gap-2"><Send className="h-4 w-4" /> Send</Button>
            <Button type="button" onClick={sendTest} loading={testing} variant="outline" className="gap-2">Send Test</Button>
          </div>
        </form>
      </div>
    </div>
  );
}

// ─── Log panel ────────────────────────────────────────────────────────────────

function LogPanel({ refresh }: { refresh: number }) {
  const [log,     setLog]     = useState<WhatsAppLogItem[]>([]);
  const [total,   setTotal]   = useState(0);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    setLoading(true);
    api.whatsapp.log().then(r => { setLog(r.items); setTotal(r.total); }).catch(() => {}).finally(() => setLoading(false));
  }, [refresh]);

  if (loading) return <div className="flex items-center justify-center py-10"><Loader2 className="h-6 w-6 animate-spin text-gray-400" /></div>;

  return (
    <div className="space-y-3">
      <p className="text-sm text-gray-500">{total} message{total !== 1 ? "s" : ""} logged</p>
      {log.length === 0 ? (
        <div className="rounded-xl border-2 border-dashed border-gray-200 py-12 text-center">
          <Phone className="h-8 w-8 text-gray-200 mx-auto mb-2" />
          <p className="text-sm text-gray-500">No messages logged yet.</p>
        </div>
      ) : (
        <div className="overflow-x-auto rounded-xl border border-gray-200 shadow-sm">
          <table className="min-w-full text-sm">
            <thead className="bg-gray-50 border-b border-gray-200">
              <tr className="text-xs font-semibold text-gray-500 uppercase tracking-wider">
                <th className="px-4 py-3 text-left">Recipient</th>
                <th className="px-4 py-3 text-left">Trigger</th>
                <th className="px-4 py-3 text-left">Message</th>
                <th className="px-4 py-3 text-center">Status</th>
                <th className="px-4 py-3 text-left">Date</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100 bg-white">
              {log.map(l => (
                <tr key={l.whatsAppLogId} className="hover:bg-gray-50">
                  <td className="px-4 py-3">
                    <p className="font-medium text-gray-900">{l.recipientName}</p>
                    <p className="text-xs text-gray-400">{l.recipientPhone}</p>
                  </td>
                  <td className="px-4 py-3 text-gray-600 text-xs">{TRIGGER_LABELS[l.triggerType] ?? l.triggerType}</td>
                  <td className="px-4 py-3 text-gray-600 max-w-xs"><p className="line-clamp-2 text-xs">{l.messageBody}</p></td>
                  <td className="px-4 py-3 text-center"><StatusBadge status={l.status} /></td>
                  <td className="px-4 py-3 text-xs text-gray-400 whitespace-nowrap">
                    {new Date(l.createdAt).toLocaleDateString("en-ZA", { day: "2-digit", month: "short", year: "numeric" })}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

// ─── Page ─────────────────────────────────────────────────────────────────────

const TABS = ["compose", "log", "settings"] as const;
type Tab = typeof TABS[number];

export default function WhatsAppPage() {
  const router = useRouter();
  const hasWhatsApp = useFeature("whatsApp");
  const [tab, setTab] = useState<Tab>("compose");
  const [logRefresh, setLogRefresh] = useState(0);

  if (!hasWhatsApp) {
    return (
      <div className="flex flex-col items-center justify-center h-96 text-center px-4">
        <Phone className="h-12 w-12 text-gray-200 mb-4" />
        <h2 className="text-lg font-semibold text-gray-700">WhatsApp not enabled</h2>
        <p className="text-sm text-gray-400 mt-1">Enable WhatsApp Notifications in Settings.</p>
        <button onClick={() => router.push("/settings")} className="mt-4 text-sm text-blue-600 hover:underline">Go to Settings</button>
      </div>
    );
  }

  return (
    <div className="p-4 md:p-6 lg:p-8 max-w-4xl mx-auto space-y-6">
      <div>
        <h1 className="text-2xl font-semibold text-gray-900 tracking-tight">WhatsApp Notifications</h1>
        <p className="text-sm text-gray-500 mt-1">Send automated WhatsApp messages to parents. Messages are logged regardless of delivery status.</p>
      </div>

      <div className="flex gap-1 border-b border-gray-200">
        {[{ id: "compose", label: "Compose" }, { id: "log", label: "Message Log" }, { id: "settings", label: "Settings" }].map(t => (
          <button key={t.id} onClick={() => setTab(t.id as Tab)}
            className={`px-4 py-2.5 text-sm font-medium border-b-2 transition-colors ${tab === t.id ? "border-blue-600 text-blue-600" : "border-transparent text-gray-500 hover:text-gray-800"}`}>
            {t.label}
          </button>
        ))}
      </div>

      {tab === "compose"  && <ComposePanel onSent={() => setLogRefresh(n => n + 1)} />}
      {tab === "log"      && <LogPanel refresh={logRefresh} />}
      {tab === "settings" && <SettingsPanel />}
    </div>
  );
}
