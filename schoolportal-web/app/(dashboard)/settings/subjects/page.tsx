"use client";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { api, type Subject } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Plus, Trash2, Pencil, X, Check, BookOpen, ChevronLeft, Loader2, Sparkles,
} from "lucide-react";

type PhaseFilter = "all" | "SeniorPhase" | "FET" | "none";
type SubjectForm = { name: string; code: string; description: string; capsPhase: string };

const PHASE_LABELS: Record<string, string> = {
  SeniorPhase: "Senior Phase (Gr 7–9)",
  FET: "FET (Gr 10–12)",
};

function PhaseBadge({ phase }: { phase?: string }) {
  if (phase === "SeniorPhase")
    return <span className="inline-flex items-center rounded-full bg-purple-50 px-2 py-0.5 text-[10px] font-medium text-purple-700 ring-1 ring-inset ring-purple-200">Senior Phase</span>;
  if (phase === "FET")
    return <span className="inline-flex items-center rounded-full bg-blue-50 px-2 py-0.5 text-[10px] font-medium text-blue-700 ring-1 ring-inset ring-blue-200">FET</span>;
  return <span className="inline-flex items-center rounded-full bg-gray-50 px-2 py-0.5 text-[10px] font-medium text-gray-500 ring-1 ring-inset ring-gray-200">All phases</span>;
}

const EMPTY_FORM: SubjectForm = { name: "", code: "", description: "", capsPhase: "" };

export default function SubjectsPage() {
  const router = useRouter();
  const [subjects, setSubjects]         = useState<Subject[]>([]);
  const [loading, setLoading]           = useState(true);
  const [search, setSearch]             = useState("");
  const [phaseFilter, setPhaseFilter]   = useState<PhaseFilter>("all");
  const [form, setForm]                 = useState<SubjectForm | null>(null);
  const [editingId, setEditingId]       = useState<string | null>(null);
  const [deletingId, setDeletingId]     = useState<string | null>(null);
  const [saving, setSaving]             = useState(false);
  const [seeding, setSeeding]           = useState(false);
  const [seedResult, setSeedResult]     = useState<{ created: number; skipped: number } | null>(null);
  const [error, setError]               = useState("");

  useEffect(() => {
    load();
  }, []);

  async function load() {
    setLoading(true);
    try {
      const data = await api.subjects.list();
      setSubjects(data);
    } finally {
      setLoading(false);
    }
  }

  const filtered = subjects.filter(s => {
    const matchSearch = !search || s.name.toLowerCase().includes(search.toLowerCase()) || s.code?.toLowerCase().includes(search.toLowerCase());
    const matchPhase =
      phaseFilter === "all" ? true :
      phaseFilter === "none" ? !s.capsPhase :
      s.capsPhase === phaseFilter;
    return matchSearch && matchPhase;
  });

  function openAdd() {
    setEditingId(null);
    setForm({ ...EMPTY_FORM });
    setError("");
  }

  function openEdit(s: Subject) {
    setEditingId(s.subjectId);
    setForm({ name: s.name, code: s.code ?? "", description: s.description ?? "", capsPhase: s.capsPhase ?? "" });
    setError("");
  }

  function cancelForm() {
    setForm(null);
    setEditingId(null);
    setError("");
  }

  async function save() {
    if (!form || !form.name.trim()) { setError("Name is required"); return; }
    setSaving(true); setError("");
    try {
      const body = {
        name: form.name.trim(),
        code: form.code.trim() || undefined,
        description: form.description.trim() || undefined,
        capsPhase: form.capsPhase || undefined,
      };
      if (editingId) {
        const updated = await api.subjects.update(editingId, body);
        setSubjects(ss => ss.map(s => s.subjectId === editingId ? updated : s));
      } else {
        const created = await api.subjects.create(body);
        setSubjects(ss => [...ss, created].sort((a, b) => a.name.localeCompare(b.name)));
      }
      cancelForm();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Save failed");
    } finally {
      setSaving(false);
    }
  }

  async function confirmDelete(id: string) {
    try {
      await api.subjects.delete(id);
      setSubjects(ss => ss.filter(s => s.subjectId !== id));
    } catch (e) {
      setError(e instanceof Error ? e.message : "Delete failed");
    } finally {
      setDeletingId(null);
    }
  }

  async function seedCaps() {
    setSeeding(true); setSeedResult(null); setError("");
    try {
      const result = await api.schools.seedCapsSubjects();
      setSeedResult(result);
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Seeding failed");
    } finally {
      setSeeding(false);
    }
  }

  const PHASE_TABS: { key: PhaseFilter; label: string }[] = [
    { key: "all",        label: "All" },
    { key: "SeniorPhase",label: "Senior Phase" },
    { key: "FET",        label: "FET" },
    { key: "none",       label: "No phase" },
  ];

  return (
    <div className="p-4 md:p-6 lg:p-8 max-w-3xl mx-auto space-y-6">
      {/* Header */}
      <div className="flex items-start justify-between gap-4">
        <div>
          <button onClick={() => router.push("/settings")}
            className="flex items-center gap-1 text-sm text-gray-400 hover:text-gray-600 mb-2 transition-colors">
            <ChevronLeft className="h-3.5 w-3.5" /> Settings
          </button>
          <h1 className="text-2xl font-semibold text-gray-900 tracking-tight">Subjects</h1>
          <p className="text-sm text-gray-500 mt-1">
            Manage your school's subject list. {subjects.length} subject{subjects.length !== 1 ? "s" : ""} configured.
          </p>
        </div>
        <div className="flex items-center gap-2 shrink-0 pt-6">
          <Button variant="outline" onClick={seedCaps} loading={seeding} className="gap-2 text-sm">
            <Sparkles className="h-3.5 w-3.5" />
            Seed CAPS subjects
          </Button>
          <Button onClick={openAdd} className="gap-2 text-sm">
            <Plus className="h-3.5 w-3.5" />
            Add subject
          </Button>
        </div>
      </div>

      {/* Seed result banner */}
      {seedResult && (
        <div className="flex items-center justify-between rounded-lg bg-emerald-50 border border-emerald-200 px-4 py-3 text-sm text-emerald-800">
          <span>
            <span className="font-semibold">{seedResult.created}</span> subjects added
            {seedResult.skipped > 0 && <span className="text-emerald-600"> · {seedResult.skipped} already existed (skipped)</span>}
          </span>
          <button onClick={() => setSeedResult(null)} className="text-emerald-600 hover:text-emerald-800">
            <X className="h-4 w-4" />
          </button>
        </div>
      )}

      {/* Error */}
      {error && (
        <div className="rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700">{error}</div>
      )}

      {/* Add / Edit form */}
      {form && (
        <div className="rounded-2xl bg-white border border-gray-200 shadow-sm overflow-hidden">
          <div className="px-6 py-4 border-b border-gray-100 flex items-center justify-between">
            <h2 className="text-base font-semibold text-gray-900">{editingId ? "Edit subject" : "New subject"}</h2>
            <button onClick={cancelForm} className="text-gray-400 hover:text-gray-600"><X className="h-4 w-4" /></button>
          </div>
          <div className="px-6 py-5 space-y-4">
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div className="space-y-1.5">
                <label className="text-sm font-medium text-gray-700">Name <span className="text-red-500">*</span></label>
                <Input
                  value={form.name}
                  onChange={e => setForm(f => f && ({ ...f, name: e.target.value }))}
                  placeholder="e.g. Mathematics"
                  autoFocus
                />
              </div>
              <div className="space-y-1.5">
                <label className="text-sm font-medium text-gray-700">Code</label>
                <Input
                  value={form.code}
                  onChange={e => setForm(f => f && ({ ...f, code: e.target.value }))}
                  placeholder="e.g. MATH"
                />
              </div>
            </div>
            <div className="space-y-1.5">
              <label className="text-sm font-medium text-gray-700">CAPS Phase</label>
              <select
                value={form.capsPhase}
                onChange={e => setForm(f => f && ({ ...f, capsPhase: e.target.value }))}
                className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              >
                <option value="">All phases / Languages</option>
                <option value="SeniorPhase">Senior Phase (Gr 7–9)</option>
                <option value="FET">FET (Gr 10–12)</option>
              </select>
            </div>
            <div className="space-y-1.5">
              <label className="text-sm font-medium text-gray-700">Description</label>
              <Input
                value={form.description}
                onChange={e => setForm(f => f && ({ ...f, description: e.target.value }))}
                placeholder="Optional description"
              />
            </div>
            {error && <p className="text-sm text-red-600">{error}</p>}
            <div className="flex items-center gap-3 pt-1">
              <Button onClick={save} loading={saving} className="gap-1.5">
                <Check className="h-3.5 w-3.5" />
                {editingId ? "Save changes" : "Create subject"}
              </Button>
              <button onClick={cancelForm} className="text-sm text-gray-500 hover:text-gray-700">Cancel</button>
            </div>
          </div>
        </div>
      )}

      {/* Filter bar */}
      <div className="flex flex-col sm:flex-row gap-3">
        <Input
          value={search}
          onChange={e => setSearch(e.target.value)}
          placeholder="Search subjects…"
          className="sm:max-w-xs"
        />
        <div className="flex items-center gap-1 rounded-lg border border-gray-200 bg-white p-1 self-start">
          {PHASE_TABS.map(tab => (
            <button
              key={tab.key}
              onClick={() => setPhaseFilter(tab.key)}
              className={`rounded-md px-3 py-1.5 text-xs font-medium transition-colors ${
                phaseFilter === tab.key
                  ? "bg-blue-600 text-white shadow-sm"
                  : "text-gray-600 hover:bg-gray-100"
              }`}
            >
              {tab.label}
            </button>
          ))}
        </div>
      </div>

      {/* Subject list */}
      <div className="rounded-2xl bg-white border border-gray-200 shadow-sm overflow-hidden">
        {loading ? (
          <div className="flex items-center justify-center py-16 text-gray-400">
            <Loader2 className="h-6 w-6 animate-spin" />
          </div>
        ) : filtered.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-16 text-center">
            <BookOpen className="h-10 w-10 text-gray-200 mb-3" />
            <p className="text-sm font-medium text-gray-500">
              {subjects.length === 0 ? "No subjects yet" : "No subjects match your filter"}
            </p>
            {subjects.length === 0 && (
              <p className="text-xs text-gray-400 mt-1">
                Use "Seed CAPS subjects" to add the standard South African curriculum, or add subjects manually.
              </p>
            )}
          </div>
        ) : (
          <>
            <div className="grid grid-cols-[1fr_80px_120px_80px] gap-2 px-5 py-2.5 border-b border-gray-100 text-xs font-medium text-gray-400 uppercase tracking-wider">
              <span>Name</span>
              <span>Code</span>
              <span>Phase</span>
              <span />
            </div>
            {filtered.map(s => (
              <div key={s.subjectId}
                className="grid grid-cols-[1fr_80px_120px_80px] gap-2 items-center px-5 py-3 border-b border-gray-100 last:border-0 hover:bg-gray-50 group">
                <div>
                  <p className="text-sm font-medium text-gray-900">{s.name}</p>
                  {s.description && <p className="text-xs text-gray-400 truncate">{s.description}</p>}
                </div>
                <span className="font-mono text-xs text-gray-500">{s.code ?? "—"}</span>
                <PhaseBadge phase={s.capsPhase} />
                <div className="flex items-center gap-1 justify-end opacity-0 group-hover:opacity-100 transition-opacity">
                  {deletingId === s.subjectId ? (
                    <>
                      <button onClick={() => confirmDelete(s.subjectId)}
                        className="h-7 px-2 rounded text-xs font-medium text-white bg-red-500 hover:bg-red-600 transition-colors">
                        Delete
                      </button>
                      <button onClick={() => setDeletingId(null)}
                        className="h-7 px-2 rounded text-xs font-medium text-gray-600 hover:bg-gray-100 transition-colors">
                        Cancel
                      </button>
                    </>
                  ) : (
                    <>
                      <button onClick={() => openEdit(s)}
                        className="h-7 w-7 rounded flex items-center justify-center text-gray-400 hover:text-blue-600 hover:bg-blue-50 transition-colors">
                        <Pencil className="h-3.5 w-3.5" />
                      </button>
                      <button onClick={() => setDeletingId(s.subjectId)}
                        className="h-7 w-7 rounded flex items-center justify-center text-gray-400 hover:text-red-500 hover:bg-red-50 transition-colors">
                        <Trash2 className="h-3.5 w-3.5" />
                      </button>
                    </>
                  )}
                </div>
              </div>
            ))}
          </>
        )}
      </div>

      {filtered.length > 0 && (
        <p className="text-xs text-gray-400 text-center">
          Showing {filtered.length} of {subjects.length} subject{subjects.length !== 1 ? "s" : ""}
          {phaseFilter !== "all" && ` · filtered by ${phaseFilter === "none" ? "no phase" : PHASE_LABELS[phaseFilter] ?? phaseFilter}`}
        </p>
      )}
    </div>
  );
}
