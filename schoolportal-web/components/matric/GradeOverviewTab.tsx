"use client";
import { useEffect, useState } from "react";
import { api, type GradeOverview } from "@/lib/api";
import { Loader2, AlertTriangle, Flag } from "lucide-react";
import { RiskChip, ExportNote } from "@/components/matric/RiskDashboardTab";

const BORDER: Record<string, string> = {
  red: "border-l-4 border-l-red-500",
  amber: "border-l-4 border-l-amber-400",
};

export default function GradeOverviewTab() {
  const [data, setData] = useState<GradeOverview | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    api.matric.gradeOverview()
      .then(setData)
      .catch(e => setError(e instanceof Error ? e.message : "Failed to load"))
      .finally(() => setLoading(false));
  }, []);

  if (loading) return <div className="flex justify-center py-16"><Loader2 className="h-7 w-7 animate-spin text-gray-400" /></div>;
  if (error || !data) {
    return (
      <div className="flex items-center gap-2 rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700">
        <AlertTriangle className="h-4 w-4 shrink-0" /> {error || "Failed to load"}
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between gap-3 flex-wrap">
        <p className="text-sm text-gray-600">
          <span className="font-semibold text-gray-900">{data.totalLearners}</span> Grade 12 learner{data.totalLearners !== 1 ? "s" : ""} ·{" "}
          <span className="text-red-600 font-semibold">{data.summary.red} red</span> ·{" "}
          <span className="text-amber-600 font-semibold">{data.summary.amber} amber</span> ·{" "}
          <span className="text-emerald-600 font-semibold">{data.summary.green} green</span>
        </p>
        <ExportNote />
      </div>

      {data.learners.length === 0 ? (
        <div className="rounded-xl border-2 border-dashed border-gray-200 py-16 text-center">
          <p className="text-gray-500">No Grade 12 learners in your scope yet.</p>
        </div>
      ) : (
        <div className="space-y-2">
          {data.learners.map(l => (
            <div key={l.studentId}
              className={`rounded-xl bg-white border border-gray-200 shadow-sm px-5 py-3.5 ${BORDER[l.overallRisk] ?? ""}`}>
              <div className="flex items-center justify-between gap-3 flex-wrap">
                <div className="flex items-center gap-3">
                  <div>
                    <p className="font-semibold text-gray-900">{l.name}</p>
                    <p className="text-xs text-gray-400">{l.studentNumber} · {l.className}</p>
                  </div>
                  <RiskChip risk={l.overallRisk} />
                </div>
                {l.redSubjects.length + l.amberSubjects.length > 0 && (
                  <p className="text-xs text-gray-500">
                    {l.redSubjects.length > 0 && <span className="text-red-600 font-medium">{l.redSubjects.join(", ")}</span>}
                    {l.redSubjects.length > 0 && l.amberSubjects.length > 0 && " · "}
                    {l.amberSubjects.length > 0 && <span className="text-amber-600">{l.amberSubjects.join(", ")}</span>}
                  </p>
                )}
              </div>
              {l.priorityFlags.length > 0 && (
                <div className="flex items-center gap-2 flex-wrap mt-2">
                  {l.priorityFlags.map(f => (
                    <span key={f} className="inline-flex items-center gap-1 rounded-md bg-red-50 px-2 py-0.5 text-xs font-medium text-red-700 ring-1 ring-red-200">
                      <Flag className="h-3 w-3" /> {f}
                    </span>
                  ))}
                </div>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
