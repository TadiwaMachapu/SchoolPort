"use client";
import { useEffect, useState } from "react";
import { api, type RiskDashboard, type LearnerRisk } from "@/lib/api";
import { Loader2, AlertTriangle, TrendingDown, TrendingUp, Minus, FileText } from "lucide-react";

const RISK_STYLES: Record<string, { border: string; chip: string; label: string }> = {
  red:     { border: "border-l-4 border-l-red-500",   chip: "bg-red-50 text-red-700 ring-red-200",         label: "Red" },
  amber:   { border: "border-l-4 border-l-amber-400", chip: "bg-amber-50 text-amber-700 ring-amber-200",   label: "Amber" },
  green:   { border: "",                              chip: "bg-emerald-50 text-emerald-700 ring-emerald-200", label: "Green" },
  no_data: { border: "",                              chip: "bg-gray-50 text-gray-500 ring-gray-200",      label: "No data" },
};

export function RiskChip({ risk }: { risk: string }) {
  const s = RISK_STYLES[risk] ?? RISK_STYLES.no_data;
  return (
    <span className={`inline-flex items-center rounded-md px-2 py-0.5 text-xs font-semibold ring-1 ${s.chip}`}>
      {s.label}
    </span>
  );
}

export function TrendIcon({ trend }: { trend: string }) {
  if (trend === "declining") return <TrendingDown className="h-3.5 w-3.5 text-red-500" aria-label="Declining" />;
  if (trend === "improving") return <TrendingUp className="h-3.5 w-3.5 text-emerald-500" aria-label="Improving" />;
  if (trend === "stable") return <Minus className="h-3.5 w-3.5 text-gray-400" aria-label="Stable" />;
  return null;
}

export function ExportNote() {
  return (
    <p className="flex items-center gap-1.5 text-xs text-gray-400">
      <FileText className="h-3.5 w-3.5 shrink-0" />
      For a printable report for pastoral discussions, use Smart Reports — this view is a live triage board.
    </p>
  );
}

function LearnerRow({ learner }: { learner: LearnerRisk }) {
  const [open, setOpen] = useState(false);
  const s = RISK_STYLES[learner.overallRisk] ?? RISK_STYLES.no_data;

  return (
    <div className={`rounded-xl bg-white border border-gray-200 shadow-sm overflow-hidden ${s.border}`}>
      <button
        onClick={() => setOpen(o => !o)}
        className="w-full flex items-center justify-between px-5 py-3 text-left hover:bg-gray-50 transition-colors"
      >
        <div className="flex items-center gap-3">
          <div>
            <p className="font-semibold text-gray-900">{learner.name}</p>
            <p className="text-xs text-gray-400">{learner.studentNumber} · {learner.className}</p>
          </div>
          <RiskChip risk={learner.overallRisk} />
        </div>
        <span className="text-xs text-gray-400">
          {learner.redCount}R · {learner.amberCount}A · {learner.greenCount}G
        </span>
      </button>

      {open && learner.subjects.length > 0 && (
        <div className="border-t border-gray-100">
          <table className="w-full text-sm">
            <thead className="bg-gray-50">
              <tr className="text-xs font-semibold text-gray-400 uppercase tracking-wider">
                <th className="px-5 py-2.5 text-left">Subject</th>
                <th className="px-4 py-2.5 text-center">Average</th>
                <th className="px-4 py-2.5 text-center">Missing</th>
                <th className="px-4 py-2.5 text-center">Trend</th>
                <th className="px-4 py-2.5 text-center">Risk</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-50">
              {learner.subjects.map(sub => (
                <tr key={sub.subjectName} className="hover:bg-gray-50">
                  <td className="px-5 py-2.5 font-medium text-gray-800">{sub.subjectName}</td>
                  <td className="px-4 py-2.5 text-center font-semibold text-gray-800">{sub.average}%</td>
                  <td className="px-4 py-2.5 text-center text-gray-600">{sub.missingAssessments || "—"}</td>
                  <td className="px-4 py-2.5"><div className="flex justify-center"><TrendIcon trend={sub.trend} /></div></td>
                  <td className="px-4 py-2.5 text-center"><RiskChip risk={sub.risk} /></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

export default function RiskDashboardTab() {
  const [data, setData] = useState<RiskDashboard | null>(null);
  const [classId, setClassId] = useState("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    setLoading(true);
    setError("");
    api.matric.riskDashboard(classId || undefined)
      .then(setData)
      .catch(e => setError(e instanceof Error ? e.message : "Failed to load"))
      .finally(() => setLoading(false));
  }, [classId]);

  if (loading && !data) return <div className="flex justify-center py-16"><Loader2 className="h-7 w-7 animate-spin text-gray-400" /></div>;
  if (error) {
    return (
      <div className="flex items-center gap-2 rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700">
        <AlertTriangle className="h-4 w-4 shrink-0" /> {error}
      </div>
    );
  }
  if (!data) return null;

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between gap-3 flex-wrap">
        <div className="flex items-center gap-3">
          <label className="text-xs font-medium text-gray-600">Grade 12 Class</label>
          <select
            value={classId}
            onChange={e => setClassId(e.target.value)}
            className="rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          >
            <option value="">All my classes</option>
            {data.classes.map(c => <option key={c.classId} value={c.classId}>{c.name}</option>)}
          </select>
        </div>
        <ExportNote />
      </div>

      <div className="grid grid-cols-3 gap-3">
        {[
          { label: "Red — act now",    value: data.summary.red,   colour: "text-red-600 bg-red-50 border-red-200" },
          { label: "Amber — watch",    value: data.summary.amber, colour: "text-amber-600 bg-amber-50 border-amber-200" },
          { label: "Green — on track", value: data.summary.green, colour: "text-emerald-600 bg-emerald-50 border-emerald-200" },
        ].map(k => (
          <div key={k.label} className={`rounded-xl border px-4 py-3 text-center ${k.colour}`}>
            <p className="text-2xl font-bold">{k.value}</p>
            <p className="text-xs font-medium mt-0.5">{k.label}</p>
          </div>
        ))}
      </div>

      {data.learners.length === 0 ? (
        <div className="rounded-xl border-2 border-dashed border-gray-200 py-16 text-center">
          <p className="text-gray-500">No Grade 12 learners in your scope yet.</p>
        </div>
      ) : (
        <div className="space-y-2">
          {data.learners.map(l => <LearnerRow key={l.studentId} learner={l} />)}
        </div>
      )}

      <p className="text-xs text-gray-400">
        Red: average below 40%, 3+ missing assessments, or declining below 60% · Amber: 40–49% or 1–2 missing ·
        Green: 50%+ with nothing missing. Trend compares this term to last term (±5%).
      </p>
    </div>
  );
}
