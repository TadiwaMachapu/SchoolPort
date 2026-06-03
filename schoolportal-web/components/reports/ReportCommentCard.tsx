"use client";
import { useState } from "react";
import { api, type SmartAtRiskStudent } from "@/lib/api";
import { Loader2, Sparkles, Copy, Check, RefreshCw } from "lucide-react";
import { Button } from "@/components/ui/button";

const FLAG_LABELS: Record<string, { label: string; colour: string }> = {
  LowAttendance:    { label: "Low Attendance",     colour: "bg-amber-100 text-amber-800" },
  SubjectFailing:   { label: "Subject Failing",    colour: "bg-orange-100 text-orange-800" },
  MultipleFailures: { label: "Multiple Failures",  colour: "bg-red-100 text-red-800" },
  LowOverallAverage:{ label: "Low Overall Avg",    colour: "bg-red-100 text-red-800" },
};

interface Props {
  student: SmartAtRiskStudent;
  termId: string;
  termNumber: number;
  year: number;
}

export function ReportCommentCard({ student, termId, termNumber, year }: Props) {
  const [state, setState] = useState<"idle" | "loading" | "done" | "error">("idle");
  const [comment, setComment] = useState<string | null>(null);
  const [fromCache, setFromCache] = useState(false);
  const [copied, setCopied] = useState(false);
  const [error, setError] = useState("");

  async function generate(forceRefresh = false) {
    setState("loading");
    setError("");
    try {
      const res = await api.reports.reportComment(student.studentId, termId, forceRefresh);
      if (!res.available || !res.commentText) {
        setError("AI comment unavailable — check the Anthropic API key or monthly cost cap.");
        setState("error");
        return;
      }
      setComment(res.commentText);
      setFromCache(res.fromCache ?? false);
      setState("done");
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to generate comment");
      setState("error");
    }
  }

  async function copy() {
    if (!comment) return;
    await navigator.clipboard.writeText(comment);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  }

  return (
    <div className="rounded-xl bg-white border border-gray-200 shadow-sm overflow-hidden">
      <div className="flex items-start justify-between px-5 py-4 bg-gray-50 border-b border-gray-200 gap-3">
        <div className="min-w-0">
          <p className="font-semibold text-gray-900 truncate">{student.name}</p>
          <p className="text-xs text-gray-400">{student.studentNumber}</p>
          <div className="flex flex-wrap gap-1.5 mt-2">
            {student.riskFlags.map(flag => {
              const meta = FLAG_LABELS[flag] ?? { label: flag, colour: "bg-gray-100 text-gray-700" };
              return (
                <span key={flag} className={`inline-flex items-center rounded-full px-2 py-0.5 text-[10px] font-semibold ${meta.colour}`}>
                  {meta.label}
                </span>
              );
            })}
          </div>
        </div>
        <div className="flex items-center gap-2 shrink-0">
          {state === "done" && (
            <button
              onClick={() => generate(true)}
              title="Regenerate"
              className="p-1.5 text-gray-400 hover:text-gray-600 rounded"
            >
              <RefreshCw className="h-3.5 w-3.5" />
            </button>
          )}
          {state !== "loading" && (
            <Button
              size="sm"
              variant={state === "done" ? "outline" : "default"}
              onClick={() => generate(false)}
              className="gap-1.5 text-xs"
            >
              <Sparkles className="h-3.5 w-3.5" />
              {state === "done" ? "Generated" : "Generate Comment"}
            </Button>
          )}
          {state === "loading" && (
            <div className="flex items-center gap-1.5 text-xs text-gray-500">
              <Loader2 className="h-3.5 w-3.5 animate-spin" /> Generating…
            </div>
          )}
        </div>
      </div>

      {state === "error" && (
        <div className="px-5 py-3 text-sm text-red-700 bg-red-50">
          {error}
        </div>
      )}

      {state === "done" && comment && (
        <div className="px-5 py-4">
          <div className="flex items-start justify-between gap-3">
            <p className="text-sm text-gray-800 leading-relaxed flex-1 italic">"{comment}"</p>
            <button
              onClick={copy}
              title="Copy comment"
              className="shrink-0 p-1.5 text-gray-400 hover:text-gray-700 rounded mt-0.5"
            >
              {copied ? <Check className="h-4 w-4 text-emerald-500" /> : <Copy className="h-4 w-4" />}
            </button>
          </div>
          {fromCache && (
            <p className="text-[10px] text-gray-400 mt-2">Cached response · Term {termNumber} {year}</p>
          )}
        </div>
      )}
    </div>
  );
}
