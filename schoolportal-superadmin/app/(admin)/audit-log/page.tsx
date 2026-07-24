"use client";
import { useEffect, useState } from "react";
import { api, SuperAdminAuditLog, SchoolSummary } from "@/lib/api";
import { ChevronDown, ChevronUp, ScrollText, Loader2 } from "lucide-react";

const PAGE_SIZE = 25;

const ACTION_META: Record<string, { label: string; className: string }> = {
  SchoolCreated:         { label: "School Created",   className: "bg-emerald-500/15 text-emerald-400" },
  SchoolFeaturesUpdated: { label: "Features Updated", className: "bg-violet-500/15 text-violet-300"  },
  SchoolStatusChanged:   { label: "Status Changed",   className: "bg-amber-500/15 text-amber-400"    },
};

const ACTION_OPTIONS = [
  { value: "",                      label: "All actions"      },
  { value: "SchoolCreated",         label: "School Created"   },
  { value: "SchoolFeaturesUpdated", label: "Features Updated" },
  { value: "SchoolStatusChanged",   label: "Status Changed"   },
];

function parse(json: string | null): Record<string, unknown> | null {
  if (!json) return null;
  try { return JSON.parse(json); } catch { return null; }
}

// Compact before→after summary, per action type.
function summarize(row: SuperAdminAuditLog): string {
  const prev = parse(row.previousValue);
  const next = parse(row.newValue);
  if (row.actionType === "SchoolCreated") {
    const name = next?.["name"];
    const email = next?.["adminEmail"];
    return name ? `Created “${name}”${email ? ` · admin ${email}` : ""}` : "Created";
  }
  if (next) {
    return Object.keys(next)
      .map((k) => `${k}: ${String(prev?.[k] ?? "—")} → ${String(next[k])}`)
      .join(", ");
  }
  return "—";
}

function fmtDate(iso: string): string {
  const d = new Date(iso);
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${pad(d.getDate())}/${pad(d.getMonth() + 1)}/${d.getFullYear()} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

function AuditRow({ row }: { row: SuperAdminAuditLog }) {
  const [open, setOpen] = useState(false);
  const meta = ACTION_META[row.actionType] ?? { label: row.actionType, className: "bg-white/10 text-white/60" };
  const prev = parse(row.previousValue);
  const next = parse(row.newValue);

  return (
    <div className="border-b border-white/8 last:border-b-0">
      <button
        onClick={() => setOpen((o) => !o)}
        className="w-full grid grid-cols-[150px_160px_150px_1fr_20px] items-center gap-3 px-4 py-3 text-left hover:bg-white/3 transition-colors"
      >
        <span className="text-xs text-white/50 tabular-nums">{fmtDate(row.createdAt)}</span>
        <span className="min-w-0">
          <span className="block truncate text-sm text-white">{row.superAdminName}</span>
          <span className="block truncate text-xs text-white/35">{row.superAdminEmail}</span>
        </span>
        <span>
          <span className={`inline-block rounded-full px-2 py-0.5 text-xs font-medium ${meta.className}`}>{meta.label}</span>
        </span>
        <span className="min-w-0">
          <span className="block truncate text-sm text-white/80">{row.targetSchoolName ?? "—"}</span>
          <span className="block truncate text-xs text-white/40">{summarize(row)}</span>
        </span>
        {open ? <ChevronUp className="h-4 w-4 text-white/40" /> : <ChevronDown className="h-4 w-4 text-white/40" />}
      </button>

      {open && (
        <div className="px-4 pb-4 grid grid-cols-1 sm:grid-cols-2 gap-3">
          <div className="rounded-lg border border-white/8 bg-white/3 p-3">
            <p className="mb-1 text-xs font-medium text-white/40 uppercase tracking-wider">Before</p>
            <pre className="whitespace-pre-wrap break-all text-xs text-white/70">
              {prev ? JSON.stringify(prev, null, 2) : "—"}
            </pre>
          </div>
          <div className="rounded-lg border border-white/8 bg-white/3 p-3">
            <p className="mb-1 text-xs font-medium text-white/40 uppercase tracking-wider">After</p>
            <pre className="whitespace-pre-wrap break-all text-xs text-white/70">
              {next ? JSON.stringify(next, null, 2) : "—"}
            </pre>
          </div>
          {row.reason && (
            <div className="sm:col-span-2 rounded-lg border border-white/8 bg-white/3 p-3">
              <p className="mb-1 text-xs font-medium text-white/40 uppercase tracking-wider">Reason</p>
              <p className="text-sm text-white/70">{row.reason}</p>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

export default function AuditLogPage() {
  const [rows, setRows]       = useState<SuperAdminAuditLog[]>([]);
  const [total, setTotal]     = useState(0);
  const [page, setPage]       = useState(1);
  const [loading, setLoading] = useState(true);
  const [error, setError]     = useState("");
  const [schools, setSchools] = useState<SchoolSummary[]>([]);

  // Filters
  const [schoolId, setSchoolId]     = useState("");
  const [actionType, setActionType] = useState("");
  const [from, setFrom]             = useState("");
  const [to, setTo]                 = useState("");

  useEffect(() => {
    api.schools.list().then(setSchools).catch(() => {});
  }, []);

  useEffect(() => {
    setLoading(true);
    api.auditLog
      .list({
        schoolId: schoolId || undefined,
        actionType: actionType || undefined,
        from: from ? `${from}T00:00:00` : undefined,
        to: to ? `${to}T23:59:59` : undefined,
        page,
        pageSize: PAGE_SIZE,
      })
      .then((r) => { setRows(r.items); setTotal(r.totalCount); })
      .catch((e) => setError(e.message))
      .finally(() => setLoading(false));
  }, [schoolId, actionType, from, to, page]);

  // Reset to page 1 whenever a filter changes.
  useEffect(() => { setPage(1); }, [schoolId, actionType, from, to]);

  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));
  const inputCls = "rounded-xl border border-white/10 bg-white/5 px-3 py-2 text-sm text-white placeholder-white/30 focus:outline-none focus:ring-2 focus:ring-violet-500";

  return (
    <div className="p-8">
      <div className="flex items-center gap-3 mb-1">
        <ScrollText className="h-5 w-5 text-violet-400" />
        <h1 className="text-xl font-bold text-white">Audit Log</h1>
      </div>
      <p className="text-sm text-white/40 mb-6">Every platform-level action, most recent first</p>

      {/* Filters */}
      <div className="mb-4 flex flex-wrap items-center gap-2">
        <select value={schoolId} onChange={(e) => setSchoolId(e.target.value)} className={inputCls}>
          <option value="">All schools</option>
          {schools.map((s) => <option key={s.schoolId} value={s.schoolId}>{s.name}</option>)}
        </select>
        <select value={actionType} onChange={(e) => setActionType(e.target.value)} className={inputCls}>
          {ACTION_OPTIONS.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
        </select>
        <input type="date" value={from} onChange={(e) => setFrom(e.target.value)} className={inputCls} aria-label="From date" />
        <span className="text-white/30 text-sm">→</span>
        <input type="date" value={to} onChange={(e) => setTo(e.target.value)} className={inputCls} aria-label="To date" />
      </div>

      {error && (
        <div className="mb-4 rounded-lg border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm text-red-400">{error}</div>
      )}

      {/* Table */}
      <div className="rounded-2xl border border-white/8 bg-white/3 overflow-hidden">
        <div className="grid grid-cols-[150px_160px_150px_1fr_20px] gap-3 px-4 py-2.5 border-b border-white/8 text-[11px] font-semibold uppercase tracking-wider text-white/40">
          <span>Timestamp</span><span>Super Admin</span><span>Action</span><span>Target &amp; change</span><span />
        </div>

        {loading ? (
          <div className="p-8 flex items-center justify-center text-white/40 text-sm gap-2">
            <Loader2 className="h-4 w-4 animate-spin" /> Loading…
          </div>
        ) : rows.length === 0 ? (
          <div className="px-6 py-12 text-center text-white/30 text-sm">No audit entries match these filters.</div>
        ) : (
          rows.map((r) => <AuditRow key={r.auditId} row={r} />)
        )}
      </div>

      {/* Pagination */}
      {!loading && total > 0 && (
        <div className="mt-4 flex items-center justify-between text-sm text-white/40">
          <span>{total} entr{total === 1 ? "y" : "ies"} · page {page} of {totalPages}</span>
          <div className="flex gap-2">
            <button
              onClick={() => setPage((p) => Math.max(1, p - 1))}
              disabled={page <= 1}
              className="rounded-lg border border-white/10 px-3 py-1.5 text-white/60 hover:text-white hover:border-white/20 disabled:opacity-40 transition-colors"
            >
              Previous
            </button>
            <button
              onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
              disabled={page >= totalPages}
              className="rounded-lg border border-white/10 px-3 py-1.5 text-white/60 hover:text-white hover:border-white/20 disabled:opacity-40 transition-colors"
            >
              Next
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
