"use client";
import { useEffect, useReducer, useState } from "react";
import { api, SchoolSummary, SchoolFeatures, CreateSchoolPayload } from "@/lib/api";
import { Plus, ToggleLeft, ToggleRight, ChevronDown, ChevronUp, Loader2 } from "lucide-react";

// ── Feature flag labels ──────────────────────────────────────────
const FEATURE_LABELS: { key: keyof SchoolFeatures; label: string }[] = [
  { key: "gradebook",        label: "Gradebook"         },
  { key: "virtualClassroom", label: "Virtual Classroom" },
  { key: "smartReports",     label: "Smart Reports"     },
  { key: "saSamsExport",     label: "SA-SAMS Export"    },
  { key: "skillsProfile",    label: "Skills Profile"    },
  { key: "pathways",         label: "Pathways"          },
  { key: "matricHub",        label: "Matric Hub"        },
  { key: "sportsCulture",    label: "Sports & Culture"  },
  { key: "schoolPay",        label: "School Pay"        },
  { key: "schoolChat",       label: "School Chat"       },
  { key: "whatsApp",         label: "WhatsApp"          },
  { key: "popiaCentre",      label: "POPIA Centre"      },
];

// Full all-off feature set (all 12 real flags present) — used to seed the create
// form so the payload matches the backend's flat UpdateSchoolFeaturesRequest exactly.
const ALL_FEATURES_OFF: SchoolFeatures = FEATURE_LABELS.reduce(
  (acc, { key }) => ({ ...acc, [key]: false }),
  {} as SchoolFeatures,
);

// ── Create School modal ──────────────────────────────────────────
const EMPTY: CreateSchoolPayload = {
  name: "", domain: "", adminEmail: "", adminPassword: "",
  adminFirstName: "", adminLastName: "",
  features: { ...ALL_FEATURES_OFF },
};

function CreateSchoolModal({ onClose, onCreate }: {
  onClose: () => void;
  onCreate: (s: SchoolSummary) => void;
}) {
  const [form,    setForm]    = useState<CreateSchoolPayload>(EMPTY);
  const [loading, setLoading] = useState(false);
  const [error,   setError]   = useState("");

  function field(key: keyof CreateSchoolPayload) {
    return {
      value: (form[key] ?? "") as string,
      onChange: (e: React.ChangeEvent<HTMLInputElement>) =>
        setForm(prev => ({ ...prev, [key]: e.target.value })),
    };
  }

  function toggleFeature(key: keyof SchoolFeatures) {
    setForm(prev => {
      const features = prev.features as SchoolFeatures;
      return { ...prev, features: { ...features, [key]: !features[key] } };
    });
  }

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setError(""); setLoading(true);
    try {
      const school = await api.schools.create({
        ...form,
        domain: form.domain || undefined,
      });
      onCreate(school);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : "Failed");
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 backdrop-blur-sm p-4">
      <div className="w-full max-w-md rounded-2xl border border-white/10 bg-[#0f0f1c] shadow-2xl">
        <div className="border-b border-white/10 px-6 py-4 flex items-center justify-between">
          <h2 className="font-semibold text-white">Create School</h2>
          <button onClick={onClose} className="text-white/40 hover:text-white text-xl leading-none">&times;</button>
        </div>
        <form onSubmit={submit} className="p-6 space-y-4">
          {error && <div className="rounded-lg bg-red-500/10 border border-red-500/30 px-3 py-2 text-sm text-red-400">{error}</div>}

          <div className="grid grid-cols-2 gap-3">
            <Field label="Admin First Name" {...field("adminFirstName")} required />
            <Field label="Admin Last Name"  {...field("adminLastName")}  required />
          </div>
          <Field label="School Name"    {...field("name")}          required />
          <Field label="Domain (optional)" {...field("domain")}     />
          <Field label="Admin Email"    {...field("adminEmail")}    type="email"    required />
          <Field label="Admin Password" {...field("adminPassword")} type="password" required />

          <div className="space-y-1">
            <label className="text-xs font-medium text-white/60">Features</label>
            <div className="grid grid-cols-2 sm:grid-cols-3 gap-2">
              {FEATURE_LABELS.map(({ key, label }) => {
                const on = (form.features as SchoolFeatures)[key];
                return (
                  <button
                    key={key}
                    type="button"
                    onClick={() => toggleFeature(key)}
                    className={`flex items-center gap-2 rounded-lg px-3 py-2 text-xs font-medium transition-colors
                      ${on
                        ? "bg-violet-600/20 text-violet-300 hover:bg-violet-600/30"
                        : "bg-white/4 text-white/40 hover:bg-white/8 hover:text-white/60"}`}
                  >
                    {on
                      ? <ToggleRight className="h-3.5 w-3.5 flex-shrink-0" />
                      : <ToggleLeft className="h-3.5 w-3.5 flex-shrink-0" />}
                    {label}
                  </button>
                );
              })}
            </div>
          </div>

          <div className="flex gap-3 pt-2">
            <button type="button" onClick={onClose} className="flex-1 rounded-xl border border-white/10 px-4 py-2 text-sm text-white/60 hover:text-white hover:border-white/20 transition-colors">
              Cancel
            </button>
            <button type="submit" disabled={loading} className="flex-1 rounded-xl bg-violet-600 px-4 py-2 text-sm font-medium text-white hover:bg-violet-500 disabled:opacity-60 transition-colors flex items-center justify-center gap-2">
              {loading && <Loader2 className="h-3.5 w-3.5 animate-spin" />}
              Create School
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

function Field({ label, type = "text", required, ...rest }: {
  label: string; type?: string; required?: boolean;
  value: string; onChange: (e: React.ChangeEvent<HTMLInputElement>) => void;
}) {
  return (
    <div className="space-y-1">
      <label className="text-xs font-medium text-white/60">{label}</label>
      <input
        type={type}
        required={required}
        {...rest}
        className="w-full rounded-xl border border-white/10 bg-white/5 px-3 py-2 text-sm text-white placeholder-white/20 focus:outline-none focus:ring-2 focus:ring-violet-500"
      />
    </div>
  );
}

// ── Feature flags inline panel ───────────────────────────────────
function FeaturePanel({ school, onChange }: {
  school: SchoolSummary;
  onChange: (updated: SchoolSummary) => void;
}) {
  const [saving, setSaving] = useState<keyof SchoolFeatures | null>(null);

  async function toggle(key: keyof SchoolFeatures) {
    setSaving(key);
    try {
      const updated = await api.schools.updateFeatures(school.schoolId, {
        ...school.features,
        [key]: !school.features[key],
      });
      onChange(updated);
    } finally {
      setSaving(null);
    }
  }

  return (
    <div className="px-4 pb-4 grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-2">
      {FEATURE_LABELS.map(({ key, label }) => {
        const on = school.features[key];
        const busy = saving === key;
        return (
          <button
            key={key}
            onClick={() => toggle(key)}
            disabled={busy}
            className={`flex items-center gap-2 rounded-lg px-3 py-2 text-xs font-medium transition-colors
              ${on
                ? "bg-violet-600/20 text-violet-300 hover:bg-violet-600/30"
                : "bg-white/4 text-white/40 hover:bg-white/8 hover:text-white/60"}`}
          >
            {busy ? (
              <Loader2 className="h-3.5 w-3.5 animate-spin flex-shrink-0" />
            ) : on ? (
              <ToggleRight className="h-3.5 w-3.5 flex-shrink-0" />
            ) : (
              <ToggleLeft className="h-3.5 w-3.5 flex-shrink-0" />
            )}
            {label}
          </button>
        );
      })}
    </div>
  );
}

// ── School row ───────────────────────────────────────────────────
function SchoolRow({ school: initial, onUpdate }: {
  school: SchoolSummary;
  onUpdate: (s: SchoolSummary) => void;
}) {
  const [school,   setSchool]   = useState(initial);
  const [expanded, setExpanded] = useState(false);
  const [toggling, setToggling] = useState(false);

  function handleFeatureChange(updated: SchoolSummary) {
    setSchool(updated);
    onUpdate(updated);
  }

  async function toggleStatus() {
    setToggling(true);
    try {
      const updated = await api.schools.setStatus(school.schoolId, !school.isActive);
      setSchool(updated);
      onUpdate(updated);
    } finally {
      setToggling(false);
    }
  }

  const activeCount = Object.values(school.features).filter(Boolean).length;

  return (
    <div className="rounded-xl border border-white/8 bg-white/3 overflow-hidden">
      <div className="flex items-center gap-4 px-4 py-3">
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2">
            <span className="font-medium text-white text-sm truncate">{school.name}</span>
            <span className={`flex-shrink-0 rounded-full px-2 py-0.5 text-xs font-medium
              ${school.isActive ? "bg-emerald-500/15 text-emerald-400" : "bg-red-500/15 text-red-400"}`}>
              {school.isActive ? "Active" : "Inactive"}
            </span>
          </div>
          <p className="text-xs text-white/40 mt-0.5 truncate">
            {school.domain ?? "no domain"} &middot; {school.userCount} users &middot; {school.classCount} classes
          </p>
        </div>

        <div className="flex items-center gap-2 flex-shrink-0">
          <span className="text-xs text-white/30">{activeCount}/{FEATURE_LABELS.length} features</span>
          <button
            onClick={toggleStatus}
            disabled={toggling}
            className={`rounded-lg px-3 py-1.5 text-xs font-medium transition-colors disabled:opacity-60
              ${school.isActive
                ? "bg-red-500/10 text-red-400 hover:bg-red-500/20"
                : "bg-emerald-500/10 text-emerald-400 hover:bg-emerald-500/20"}`}
          >
            {toggling ? "…" : school.isActive ? "Deactivate" : "Activate"}
          </button>
          <button
            onClick={() => setExpanded(e => !e)}
            className="flex items-center gap-1 rounded-lg px-2 py-1.5 text-xs text-white/40 hover:text-white hover:bg-white/5 transition-colors"
          >
            Features
            {expanded ? <ChevronUp className="h-3.5 w-3.5" /> : <ChevronDown className="h-3.5 w-3.5" />}
          </button>
        </div>
      </div>

      {expanded && <FeaturePanel school={school} onChange={handleFeatureChange} />}
    </div>
  );
}

// ── Page ─────────────────────────────────────────────────────────
export default function SchoolsPage() {
  const [schools, setSchools] = useState<SchoolSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error,   setError]   = useState("");
  const [showCreate, setShowCreate] = useState(false);
  const [search, setSearch]   = useState("");

  useEffect(() => {
    api.schools.list()
      .then(setSchools)
      .catch(e => setError(e.message))
      .finally(() => setLoading(false));
  }, []);

  function handleUpdate(updated: SchoolSummary) {
    setSchools(prev => prev.map(s => s.schoolId === updated.schoolId ? updated : s));
  }

  function handleCreate(school: SchoolSummary) {
    setSchools(prev => [school, ...prev]);
    setShowCreate(false);
  }

  const filtered = schools.filter(s =>
    s.name.toLowerCase().includes(search.toLowerCase()) ||
    (s.domain ?? "").toLowerCase().includes(search.toLowerCase())
  );

  return (
    <div className="p-8">
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-xl font-bold text-white">Schools</h1>
          <p className="text-sm text-white/40 mt-0.5">{schools.length} school{schools.length !== 1 ? "s" : ""} on this platform</p>
        </div>
        <button
          onClick={() => setShowCreate(true)}
          className="flex items-center gap-2 rounded-xl bg-violet-600 px-4 py-2 text-sm font-medium text-white hover:bg-violet-500 transition-colors"
        >
          <Plus className="h-4 w-4" />
          Create School
        </button>
      </div>

      <input
        type="search"
        value={search}
        onChange={e => setSearch(e.target.value)}
        placeholder="Search schools…"
        className="mb-4 w-full max-w-sm rounded-xl border border-white/10 bg-white/5 px-4 py-2 text-sm text-white placeholder-white/30 focus:outline-none focus:ring-2 focus:ring-violet-500"
      />

      {error && (
        <div className="mb-4 rounded-lg border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm text-red-400">{error}</div>
      )}

      {loading ? (
        <div className="space-y-3">
          {[...Array(4)].map((_, i) => (
            <div key={i} className="h-16 animate-pulse rounded-xl bg-white/4" />
          ))}
        </div>
      ) : filtered.length === 0 ? (
        <div className="rounded-xl border border-white/8 bg-white/3 px-6 py-12 text-center text-white/30 text-sm">
          {search ? "No schools match your search." : "No schools yet. Create your first one."}
        </div>
      ) : (
        <div className="space-y-3">
          {filtered.map(s => (
            <SchoolRow key={s.schoolId} school={s} onUpdate={handleUpdate} />
          ))}
        </div>
      )}

      {showCreate && (
        <CreateSchoolModal onClose={() => setShowCreate(false)} onCreate={handleCreate} />
      )}
    </div>
  );
}
