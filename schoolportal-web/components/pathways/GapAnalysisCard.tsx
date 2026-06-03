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
        className="w-full flex items-center justify-center gap-2 rounded-xl border-2 border-dashed border-purple-200 bg-purple-50 py-5 text-sm font-medium text-purple-700 hover:bg-purple-100 hover:border-purple-300 transition-colors"
      >
        <Sparkles className="h-4 w-4" />
        Get AI gap analysis for {courseName}
      </button>
    );
  }

  if (loading) {
    return (
      <div className="flex items-center justify-center gap-2 rounded-xl border border-gray-200 bg-gray-50 py-6 text-sm text-gray-500">
        <Loader2 className="h-4 w-4 animate-spin" />
        Analysing your marks…
      </div>
    );
  }

  if (error) {
    return (
      <div className="flex items-center gap-2 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
        <AlertTriangle className="h-4 w-4 shrink-0" />
        {error}
        <button onClick={() => fetchAnalysis()} className="ml-auto text-xs underline">Retry</button>
      </div>
    );
  }

  if (unavailable) {
    return (
      <div className="rounded-xl border border-gray-200 bg-gray-50 px-4 py-4 text-sm text-gray-500 flex items-start gap-3">
        <Sparkles className="h-4 w-4 mt-0.5 text-gray-400 shrink-0" />
        <div>
          <p className="font-medium text-gray-600">AI analysis not available</p>
          <p className="text-xs mt-0.5">AI features may not be configured for your school, or the monthly usage limit has been reached.</p>
        </div>
      </div>
    );
  }

  if (!result) return null;

  return (
    <div className="rounded-xl border border-purple-200 bg-white shadow-sm overflow-hidden">
      {/* Header */}
      <div className="bg-purple-50 border-b border-purple-100 px-4 py-3 flex items-center justify-between">
        <div className="flex items-center gap-2 text-sm font-semibold text-purple-800">
          <Sparkles className="h-4 w-4" />
          AI Gap Analysis
          {result.fromCache && (
            <span className="text-[10px] font-normal text-purple-500 bg-purple-100 rounded-full px-2 py-0.5">cached</span>
          )}
        </div>
        <button
          onClick={() => fetchAnalysis(true)}
          className="flex items-center gap-1 text-xs text-purple-600 hover:text-purple-800 transition-colors"
        >
          <RefreshCw className="h-3 w-3" /> Regenerate
        </button>
      </div>

      <div className="p-4 space-y-4">
        {/* Summary */}
        <p className="text-sm text-gray-800 font-medium">{result.summary}</p>

        {/* Subject gaps */}
        {result.subjectGaps.length > 0 && (
          <div className="space-y-3">
            <p className="text-xs font-semibold text-gray-500 uppercase tracking-wider">Subject Gaps</p>
            {result.subjectGaps.map((gap, i) => (
              <div key={i} className="rounded-lg border border-amber-100 bg-amber-50 px-3 py-3 space-y-1">
                <div className="flex items-center justify-between">
                  <span className="text-sm font-medium text-gray-900">{gap.subject}</span>
                  <div className="flex items-center gap-2 text-xs">
                    {gap.currentPercent !== undefined && (
                      <span className="text-gray-500">{gap.currentPercent.toFixed(0)}%</span>
                    )}
                    <span className="text-amber-700 font-semibold">→ need {gap.requiredPercent}%</span>
                  </div>
                </div>
                <p className="text-xs text-gray-600">{gap.advice}</p>
              </div>
            ))}
          </div>
        )}

        {result.subjectGaps.length === 0 && result.apsGap === 0 && (
          <div className="flex items-center gap-2 text-sm text-emerald-700 bg-emerald-50 rounded-lg px-3 py-2">
            <CheckCircle2 className="h-4 w-4 shrink-0" />
            You meet all subject requirements for this course!
          </div>
        )}

        {/* Overall advice */}
        <div className="space-y-1">
          <div className="flex items-center gap-1.5 text-xs font-semibold text-gray-500 uppercase tracking-wider">
            <TrendingUp className="h-3 w-3" /> Overall Advice
          </div>
          <p className="text-sm text-gray-700">{result.overallAdvice}</p>
        </div>

        {/* Study suggestions */}
        {result.studySuggestions.length > 0 && (
          <div className="space-y-2">
            <div className="flex items-center gap-1.5 text-xs font-semibold text-gray-500 uppercase tracking-wider">
              <BookOpen className="h-3 w-3" /> Study Tips
            </div>
            <ul className="space-y-1">
              {result.studySuggestions.map((tip, i) => (
                <li key={i} className="flex items-start gap-2 text-xs text-gray-600">
                  <span className="mt-1 h-1.5 w-1.5 rounded-full bg-purple-400 shrink-0" />
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
