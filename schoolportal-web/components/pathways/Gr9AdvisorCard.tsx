"use client";
import { useState } from "react";
import { api, type Gr9AiAdvice } from "@/lib/api";
import { Sparkles, Loader2, AlertTriangle, RefreshCw, CheckCircle2, TrendingUp, Briefcase, BookOpen } from "lucide-react";

export default function Gr9AdvisorCard() {
  const [advice, setAdvice] = useState<Gr9AiAdvice | null>(null);
  const [unavailable, setUnavailable] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  async function fetchAdvice(forceRefresh = false) {
    setLoading(true);
    setError("");
    try {
      const res = await api.pathways.gr9Advice(forceRefresh);
      if (!res.available || !res.advice) {
        setUnavailable(true);
      } else {
        setAdvice(res.advice);
        setUnavailable(false);
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : "Something went wrong");
    } finally {
      setLoading(false);
    }
  }

  if (!advice && !unavailable && !loading && !error) {
    return (
      <button
        onClick={() => fetchAdvice()}
        className="w-full flex items-center justify-center gap-2 rounded-xl border-2 border-dashed border-purple-200 bg-purple-50 py-5 text-sm font-medium text-purple-700 hover:bg-purple-100 hover:border-purple-300 transition-colors"
      >
        <Sparkles className="h-4 w-4" />
        Get AI subject selection advice
      </button>
    );
  }

  if (loading) {
    return (
      <div className="flex items-center justify-center gap-2 rounded-xl border border-gray-200 bg-gray-50 py-6 text-sm text-gray-500">
        <Loader2 className="h-4 w-4 animate-spin" />
        Analysing your profile…
      </div>
    );
  }

  if (error) {
    return (
      <div className="flex items-center gap-2 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
        <AlertTriangle className="h-4 w-4 shrink-0" />
        {error}
        <button onClick={() => fetchAdvice()} className="ml-auto text-xs underline">Retry</button>
      </div>
    );
  }

  if (unavailable) {
    return (
      <div className="rounded-xl border border-gray-200 bg-gray-50 px-4 py-4 text-sm text-gray-500 flex items-start gap-3">
        <Sparkles className="h-4 w-4 mt-0.5 text-gray-400 shrink-0" />
        <div>
          <p className="font-medium text-gray-600">AI advice not available</p>
          <p className="text-xs mt-0.5">AI features may not be configured for your school, or the monthly usage limit has been reached.</p>
        </div>
      </div>
    );
  }

  if (!advice) return null;

  return (
    <div className="rounded-xl border border-purple-200 bg-white shadow-sm overflow-hidden">
      {/* Header */}
      <div className="bg-purple-50 border-b border-purple-100 px-4 py-3 flex items-center justify-between">
        <div className="flex items-center gap-2 text-sm font-semibold text-purple-800">
          <Sparkles className="h-4 w-4" />
          AI Subject Advisor
          {advice.fromCache && (
            <span className="text-[10px] font-normal text-purple-500 bg-purple-100 rounded-full px-2 py-0.5">cached</span>
          )}
        </div>
        <button
          onClick={() => fetchAdvice(true)}
          className="flex items-center gap-1 text-xs text-purple-600 hover:text-purple-800 transition-colors"
        >
          <RefreshCw className="h-3 w-3" /> Regenerate
        </button>
      </div>

      <div className="p-4 space-y-4">
        {/* Summary */}
        <p className="text-sm text-gray-800 font-medium">{advice.summary}</p>

        {/* Recommended subjects */}
        {advice.recommendedSubjects.length > 0 && (
          <div className="space-y-2">
            <div className="flex items-center gap-1.5 text-xs font-semibold text-gray-500 uppercase tracking-wider">
              <CheckCircle2 className="h-3 w-3" /> Recommended Subjects
            </div>
            <div className="space-y-2">
              {advice.recommendedSubjects.map((s, i) => (
                <div key={i} className="rounded-lg border border-emerald-100 bg-emerald-50 px-3 py-3 space-y-1">
                  <div className="flex items-center justify-between">
                    <span className="text-sm font-semibold text-gray-900">{s.name}</span>
                    {s.careerLinks.length > 0 && (
                      <span className="text-xs text-emerald-700 font-medium">
                        → {s.careerLinks.slice(0, 2).join(", ")}
                        {s.careerLinks.length > 2 ? ` +${s.careerLinks.length - 2}` : ""}
                      </span>
                    )}
                  </div>
                  <p className="text-xs text-gray-600">{s.reason}</p>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Improvement areas */}
        {advice.improvementAreas.length > 0 && (
          <div className="space-y-2">
            <div className="flex items-center gap-1.5 text-xs font-semibold text-gray-500 uppercase tracking-wider">
              <TrendingUp className="h-3 w-3" /> Areas to Improve Before Choosing
            </div>
            {advice.improvementAreas.map((area, i) => (
              <div key={i} className="rounded-lg border border-amber-100 bg-amber-50 px-3 py-2 space-y-0.5">
                <div className="flex items-center justify-between">
                  <span className="text-sm font-medium text-gray-900">{area.subject}</span>
                  <span className="text-xs text-amber-700">{area.currentPercent.toFixed(0)}% currently</span>
                </div>
                <p className="text-xs text-gray-600">{area.advice}</p>
              </div>
            ))}
          </div>
        )}

        {/* Career paths enabled */}
        {advice.careerPathsEnabled.length > 0 && (
          <div className="space-y-2">
            <div className="flex items-center gap-1.5 text-xs font-semibold text-gray-500 uppercase tracking-wider">
              <Briefcase className="h-3 w-3" /> Career Paths You Can Unlock
            </div>
            <div className="flex flex-wrap gap-1.5">
              {advice.careerPathsEnabled.map((career, i) => (
                <span key={i} className="inline-flex items-center rounded-full bg-blue-50 border border-blue-200 px-2.5 py-0.5 text-xs font-medium text-blue-700">
                  {career}
                </span>
              ))}
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
