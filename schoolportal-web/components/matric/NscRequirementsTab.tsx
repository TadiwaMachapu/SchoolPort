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

  if (loading) return <div className="flex justify-center py-16"><Loader2 className="h-6 w-6 animate-spin text-gray-400" /></div>;
  if (error || !data) {
    return (
      <div className="flex items-center gap-2 rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700">
        <AlertTriangle className="h-4 w-4 shrink-0" /> {error || "Failed to load"}
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Pass levels */}
      <div>
        <h3 className="text-sm font-semibold text-gray-700 mb-2">Pass levels</h3>
        <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
          {data.passLevels.map(level => (
            <div key={level.level} className="rounded-xl bg-white border border-gray-200 shadow-sm p-4">
              <div className="flex items-center gap-2 mb-1">
                <GraduationCap className="h-4 w-4 text-blue-500 shrink-0" />
                <p className="text-sm font-bold text-gray-900">{level.level}</p>
              </div>
              <p className="text-xs text-gray-500 mb-3">{level.description}</p>
              <ul className="space-y-1.5">
                {level.requirements.map((r, i) => (
                  <li key={i} className="flex items-start gap-1.5 text-xs text-gray-600">
                    <CheckCircle2 className="h-3.5 w-3.5 text-emerald-500 shrink-0 mt-px" /> {r}
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </div>
      </div>

      {/* Subject composition */}
      <div>
        <h3 className="text-sm font-semibold text-gray-700 mb-2">Your 7 NSC subjects</h3>
        <div className="rounded-xl bg-white border border-gray-200 shadow-sm overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-gray-50 border-b border-gray-100">
              <tr className="text-xs font-semibold text-gray-400 uppercase tracking-wider">
                <th className="px-4 py-2.5 text-left">Subject</th>
                <th className="px-4 py-2.5 text-left">Requirement</th>
                <th className="px-4 py-2.5 text-left">Credits</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-50">
              {data.subjectRules.map(rule => (
                <tr key={rule.subject} className="hover:bg-gray-50 align-top">
                  <td className="px-4 py-3 font-medium text-gray-800 whitespace-nowrap">{rule.subject}</td>
                  <td className="px-4 py-3 text-gray-600">
                    {rule.requirement}
                    <p className="text-xs text-gray-400 mt-1">{rule.notes}</p>
                  </td>
                  <td className="px-4 py-3 text-gray-600 whitespace-nowrap">{rule.credits}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {/* Achievement scale */}
      <div>
        <h3 className="text-sm font-semibold text-gray-700 mb-2">Achievement levels (APS points)</h3>
        <div className="rounded-xl bg-white border border-gray-200 shadow-sm overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-gray-50 border-b border-gray-100">
              <tr className="text-xs font-semibold text-gray-400 uppercase tracking-wider">
                <th className="px-4 py-2.5 text-center w-20">Level</th>
                <th className="px-4 py-2.5 text-left">Descriptor</th>
                <th className="px-4 py-2.5 text-center">Percentage</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-50">
              {data.achievementLevels.map(l => (
                <tr key={l.level} className="hover:bg-gray-50">
                  <td className="px-4 py-2.5 text-center font-bold text-gray-900">{l.level}</td>
                  <td className="px-4 py-2.5 text-gray-700">{l.descriptor}</td>
                  <td className="px-4 py-2.5 text-center text-gray-600">{l.percentBand}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <div className="px-4 py-2.5 bg-gray-50 border-t border-gray-100 text-xs text-gray-500">
            The same 7-point scale drives your APS in Pathways — track university goals there.
          </div>
        </div>
      </div>
    </div>
  );
}
