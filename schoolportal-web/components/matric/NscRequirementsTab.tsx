"use client";
import { useEffect, useState } from "react";
import { api, type NscRequirements } from "@/lib/api";
import { GraduationCap, Loader2, AlertTriangle, CheckCircle2 } from "lucide-react";

export default function NscRequirementsTab() {
  const [data, setData] = useState<NscRequirements | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    api.matric.nscRequirements()
      .then(setData)
      .catch(e => setError(e instanceof Error ? e.message : "Failed to load NSC requirements"))
      .finally(() => setLoading(false));
  }, []);

  if (loading) return <div className="flex justify-center py-16"><Loader2 className="h-6 w-6 animate-spin text-text-muted" /></div>;
  if (error || !data) {
    return (
      <div className="flex items-center gap-2 rounded-lg bg-danger-100 px-4 py-3 text-sm text-danger-700">
        <AlertTriangle className="h-4 w-4 shrink-0" /> {error || "Failed to load"}
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Pass levels */}
      <div>
        <h3 className="text-sm font-semibold text-text-primary mb-2">Pass levels</h3>
        <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
          {data.passLevels.map(level => (
            <div key={level.level} className="rounded-xl bg-surface-card border border-border shadow-sm p-4">
              <div className="flex items-center gap-2 mb-1">
                <GraduationCap className="h-4 w-4 text-primary shrink-0" />
                <p className="text-sm font-bold text-text-primary">{level.level}</p>
              </div>
              <p className="text-xs text-text-secondary mb-3">{level.description}</p>
              <ul className="space-y-1.5">
                {level.requirements.map((r, i) => (
                  <li key={i} className="flex items-start gap-1.5 text-xs text-text-secondary">
                    <CheckCircle2 className="h-3.5 w-3.5 text-primary shrink-0 mt-px" /> {r}
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </div>
      </div>

      {/* Subject composition */}
      <div>
        <h3 className="text-sm font-semibold text-text-primary mb-2">Your 7 NSC subjects</h3>
        <div className="rounded-xl bg-surface-card border border-border shadow-sm overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-surface-subtle border-b border-border">
              <tr className="text-xs font-semibold text-text-muted uppercase tracking-wider">
                <th className="px-4 py-2.5 text-left">Subject</th>
                <th className="px-4 py-2.5 text-left">Requirement</th>
                <th className="px-4 py-2.5 text-left">Credits</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {data.subjectRules.map(rule => (
                <tr key={rule.subject} className="hover:bg-surface-subtle align-top">
                  <td className="px-4 py-3 font-medium text-text-primary whitespace-nowrap">{rule.subject}</td>
                  <td className="px-4 py-3 text-text-secondary">
                    {rule.requirement}
                    <p className="text-xs text-text-muted mt-1">{rule.notes}</p>
                  </td>
                  <td className="px-4 py-3 text-text-secondary whitespace-nowrap">{rule.credits}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {/* Achievement scale */}
      <div>
        <h3 className="text-sm font-semibold text-text-primary mb-2">Achievement levels (APS points)</h3>
        <div className="rounded-xl bg-surface-card border border-border shadow-sm overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-surface-subtle border-b border-border">
              <tr className="text-xs font-semibold text-text-muted uppercase tracking-wider">
                <th className="px-4 py-2.5 text-center w-20">Level</th>
                <th className="px-4 py-2.5 text-left">Descriptor</th>
                <th className="px-4 py-2.5 text-center">Percentage</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {data.achievementLevels.map(l => (
                <tr key={l.level} className="hover:bg-surface-subtle">
                  <td className="px-4 py-2.5 text-center font-bold text-text-primary">{l.level}</td>
                  <td className="px-4 py-2.5 text-text-primary">{l.descriptor}</td>
                  <td className="px-4 py-2.5 text-center text-text-secondary">{l.percentBand}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <div className="px-4 py-2.5 bg-surface-subtle border-t border-border text-xs text-text-muted">
            The same 7-point scale drives your APS in Pathways — track university goals there.
          </div>
        </div>
      </div>
    </div>
  );
}
