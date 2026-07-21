"use client";
import { useState } from "react";
import { api, type GapAnalysisResult } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Sparkles, Loader2, AlertTriangle, RefreshCw, CheckCircle2, TrendingUp, BookOpen } from "lucide-react";

interface Props {
  goalId: string;
  courseName: string;
}

const STATUS_COLOURS = {
  Green:  { bg: "bg-emerald-50", border: "border-emerald-200", text: "text-emerald-700" },
  Amber:  { bg: "bg-amber-50",   border: "border-amber-200",   text: "text-amber-700" },
  Red:    { bg: "bg-red-50",     border: "border-red-200",     text: "text-red-700" },
};

export default function GapAnalysisCard({ goalId, courseName }: Props) {
  const [result, setResult] = useState<GapAnalysisResult | null>(null);
  const [unavailable, setUnavailable] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  async function fetchAnalysis(forceRefresh = false) {
    setLoading(true);
    setError("");
    try {
      const res = await api.pathways.gapAnalysis(goalId, forceRefresh);
      if (!res.available || !res.analysis) {
        setUnavailable(true);
      } else {
        setResult(res.analysis);
        setUnavailable(false);
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : "Something went wrong");
    } finally {
      setLoading(false);
    }
  }

  if (!result && !unavailable && !loading && !error) {
    return (
      <button
        onClick={() => fetchAnalysis()}
        className="w-full flex items-center justify-center gap-2 rounded-xl border-2 border-dashed border-primary-200 bg-primary-50 py-5 text-sm font-medium text-primary-700 hover:bg-primary-100 hover:border-primary-300 transition-colors"
      >
        <Sparkles className="h-4 w-4" />
        Get AI gap analysis for {courseName}
      </button>
    );
  }

  if (loading) {
    return (
      <div className="flex items-center justify-center gap-2 rounded-xl border border-border bg-surface-subtle py-6 text-sm text-text-secondary">
        <Loader2 className="h-4 w-4 animate-spin" />
        Analysing your marks…
      </div>
    );
  }

  if (error) {
    return (
      <div className="flex items-center gap-2 rounded-xl bg-danger-100 px-4 py-3 text-sm text-danger-700">
        <AlertTriangle className="h-4 w-4 shrink-0" />
        {error}
        <button onClick={() => fetchAnalysis()} className="ml-auto text-xs underline">Retry</button>
      </div>
    );
  }

  if (unavailable) {
    return (
      <div className="rounded-xl border border-border bg-surface-subtle px-4 py-4 text-sm text-text-secondary flex items-start gap-3">
        <Sparkles className="h-4 w-4 mt-0.5 text-text-muted shrink-0" />
        <div>
          <p className="font-medium text-text-primary">AI analysis not available</p>
          <p className="text-xs mt-0.5">AI features may not be configured for your school, or the monthly usage limit has been reached.</p>
        </div>
      </div>
    );
  }

  if (!result) return null;

  return (
    <div className="rounded-xl border border-primary-200 bg-surface-card shadow-sm overflow-hidden">
      {/* Header */}
      <div className="bg-primary-50 border-b border-primary-100 px-4 py-3 flex items-center justify-between">
        <div className="flex items-center gap-2 text-sm font-semibold text-primary-800">
          <Sparkles className="h-4 w-4" />
          AI Gap Analysis
          {result.fromCache && (
            <span className="text-[10px] font-normal text-primary bg-primary-100 rounded-full px-2 py-0.5">cached</span>
          )}
        </div>
        <button
          onClick={() => fetchAnalysis(true)}
          className="flex items-center gap-1 text-xs text-primary hover:text-primary-800 transition-colors"
        >
          <RefreshCw className="h-3 w-3" /> Regenerate
        </button>
      </div>

      <div className="p-4 space-y-4">
        {/* Summary */}
        <p className="text-sm text-text-primary font-medium">{result.summary}</p>

        {/* Subject gaps */}
        {result.subjectGaps.length > 0 && (
          <div className="space-y-3">
            <p className="text-xs font-semibold text-text-secondary uppercase tracking-wider">Subject Gaps</p>
            {result.subjectGaps.map((gap, i) => (
              <div key={i} className="rounded-lg bg-warning-100 px-3 py-3 space-y-1">
                <div className="flex items-center justify-between">
                  <span className="text-sm font-medium text-text-primary">{gap.subject}</span>
                  <div className="flex items-center gap-2 text-xs">
                    {gap.currentPercent !== undefined && (
                      <span className="text-text-secondary">{gap.currentPercent.toFixed(0)}%</span>
                    )}
                    <span className="text-warning-700 font-semibold">→ need {gap.requiredPercent}%</span>
                  </div>
                </div>
                <p className="text-xs text-text-secondary">{gap.advice}</p>
              </div>
            ))}
          </div>
        )}

        {result.subjectGaps.length === 0 && result.apsGap === 0 && (
          <div className="flex items-center gap-2 text-sm text-success-700 bg-success-100 rounded-lg px-3 py-2">
            <CheckCircle2 className="h-4 w-4 shrink-0" />
            You meet all subject requirements for this course!
          </div>
        )}

        {/* Overall advice */}
        <div className="space-y-1">
          <div className="flex items-center gap-1.5 text-xs font-semibold text-text-secondary uppercase tracking-wider">
            <TrendingUp className="h-3 w-3" /> Overall Advice
          </div>
          <p className="text-sm text-text-primary">{result.overallAdvice}</p>
        </div>

        {/* Study suggestions */}
        {result.studySuggestions.length > 0 && (
          <div className="space-y-2">
            <div className="flex items-center gap-1.5 text-xs font-semibold text-text-secondary uppercase tracking-wider">
              <BookOpen className="h-3 w-3" /> Study Tips
            </div>
            <ul className="space-y-1">
              {result.studySuggestions.map((tip, i) => (
                <li key={i} className="flex items-start gap-2 text-xs text-text-secondary">
                  <span className="mt-1 h-1.5 w-1.5 rounded-full bg-primary shrink-0" />
                  {tip}
                </li>
              ))}
            </ul>
          </div>
        )}
      </div>
    </div>
  );
}
