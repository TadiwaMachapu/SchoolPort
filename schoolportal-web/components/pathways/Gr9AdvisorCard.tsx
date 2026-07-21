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
        className="w-full flex items-center justify-center gap-2 rounded-xl border-2 border-dashed border-primary-200 bg-primary-50 py-5 text-sm font-medium text-primary-700 hover:bg-primary-100 hover:border-primary-300 transition-colors"
      >
        <Sparkles className="h-4 w-4" />
        Get AI subject selection advice
      </button>
    );
  }

  if (loading) {
    return (
      <div className="flex items-center justify-center gap-2 rounded-xl border border-border bg-surface-subtle py-6 text-sm text-text-secondary">
        <Loader2 className="h-4 w-4 animate-spin" />
        Analysing your profile…
      </div>
    );
  }

  if (error) {
    return (
      <div className="flex items-center gap-2 rounded-xl bg-danger-100 px-4 py-3 text-sm text-danger-700">
        <AlertTriangle className="h-4 w-4 shrink-0" />
        {error}
        <button onClick={() => fetchAdvice()} className="ml-auto text-xs underline">Retry</button>
      </div>
    );
  }

  if (unavailable) {
    return (
      <div className="rounded-xl border border-border bg-surface-subtle px-4 py-4 text-sm text-text-secondary flex items-start gap-3">
        <Sparkles className="h-4 w-4 mt-0.5 text-text-muted shrink-0" />
        <div>
          <p className="font-medium text-text-primary">AI advice not available</p>
          <p className="text-xs mt-0.5">AI features may not be configured for your school, or the monthly usage limit has been reached.</p>
        </div>
      </div>
    );
  }

  if (!advice) return null;

  return (
    <div className="rounded-xl border border-primary-200 bg-surface-card shadow-sm overflow-hidden">
      {/* Header */}
      <div className="bg-primary-50 border-b border-primary-100 px-4 py-3 flex items-center justify-between">
        <div className="flex items-center gap-2 text-sm font-semibold text-primary-800">
          <Sparkles className="h-4 w-4" />
          AI Subject Advisor
          {advice.fromCache && (
            <span className="text-[10px] font-normal text-primary bg-primary-100 rounded-full px-2 py-0.5">cached</span>
          )}
        </div>
        <button
          onClick={() => fetchAdvice(true)}
          className="flex items-center gap-1 text-xs text-primary hover:text-primary-800 transition-colors"
        >
          <RefreshCw className="h-3 w-3" /> Regenerate
        </button>
      </div>

      <div className="p-4 space-y-4">
        {/* Summary */}
        <p className="text-sm text-text-primary font-medium">{advice.summary}</p>

        {/* Recommended subjects */}
        {advice.recommendedSubjects.length > 0 && (
          <div className="space-y-2">
            <div className="flex items-center gap-1.5 text-xs font-semibold text-text-secondary uppercase tracking-wider">
              <CheckCircle2 className="h-3 w-3" /> Recommended Subjects
            </div>
            <div className="space-y-2">
              {advice.recommendedSubjects.map((s, i) => (
                <div key={i} className="rounded-lg bg-success-100 px-3 py-3 space-y-1">
                  <div className="flex items-center justify-between">
                    <span className="text-sm font-semibold text-text-primary">{s.name}</span>
                    {s.careerLinks.length > 0 && (
                      <span className="text-xs text-success-700 font-medium">
                        → {s.careerLinks.slice(0, 2).join(", ")}
                        {s.careerLinks.length > 2 ? ` +${s.careerLinks.length - 2}` : ""}
                      </span>
                    )}
                  </div>
                  <p className="text-xs text-text-secondary">{s.reason}</p>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Improvement areas */}
        {advice.improvementAreas.length > 0 && (
          <div className="space-y-2">
            <div className="flex items-center gap-1.5 text-xs font-semibold text-text-secondary uppercase tracking-wider">
              <TrendingUp className="h-3 w-3" /> Areas to Improve Before Choosing
            </div>
            {advice.improvementAreas.map((area, i) => (
              <div key={i} className="rounded-lg bg-warning-100 px-3 py-2 space-y-0.5">
                <div className="flex items-center justify-between">
                  <span className="text-sm font-medium text-text-primary">{area.subject}</span>
                  <span className="text-xs text-warning-700">{area.currentPercent.toFixed(0)}% currently</span>
                </div>
                <p className="text-xs text-text-secondary">{area.advice}</p>
              </div>
            ))}
          </div>
        )}

        {/* Career paths enabled */}
        {advice.careerPathsEnabled.length > 0 && (
          <div className="space-y-2">
            <div className="flex items-center gap-1.5 text-xs font-semibold text-text-secondary uppercase tracking-wider">
              <Briefcase className="h-3 w-3" /> Career Paths You Can Unlock
            </div>
            <div className="flex flex-wrap gap-1.5">
              {advice.careerPathsEnabled.map((career, i) => (
                <span key={i} className="inline-flex items-center rounded-full bg-primary-50 border border-primary-200 px-2.5 py-0.5 text-xs font-medium text-primary">
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
