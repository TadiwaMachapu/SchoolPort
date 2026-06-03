"use client";
import { useState } from "react";
import { api } from "@/lib/api";
import { Loader2, Sparkles, RefreshCw, FileText } from "lucide-react";
import { Button } from "@/components/ui/button";

interface Props {
  classId: string;
  termId: string;
  className: string;
  termNumber: number;
  year: number;
}

function renderMarkdown(text: string): string {
  return text
    .replace(/\*\*(.+?)\*\*/g, "<strong>$1</strong>")
    .replace(/^## (.+)$/gm, '<p class="font-semibold text-gray-900 mt-3 mb-1">$1</p>')
    .replace(/^- (.+)$/gm, '<li class="ml-4 list-disc text-gray-700">$1</li>')
    .replace(/\n\n/g, '</p><p class="mb-2">')
    .replace(/\n/g, " ");
}

export function PrincipalSummaryCard({ classId, termId, className, termNumber, year }: Props) {
  const [state, setState] = useState<"idle" | "loading" | "done" | "error">("idle");
  const [summary, setSummary] = useState<string | null>(null);
  const [fromCache, setFromCache] = useState(false);
  const [error, setError] = useState("");

  async function generate(forceRefresh = false) {
    setState("loading");
    setError("");
    try {
      const res = await api.reports.principalSummary(classId, termId, forceRefresh);
      if (!res.available || !res.summaryMarkdown) {
        setError("AI summary unavailable — check the Anthropic API key or monthly cost cap. Only Admins can generate principal summaries.");
        setState("error");
        return;
      }
      setSummary(res.summaryMarkdown);
      setFromCache(res.fromCache ?? false);
      setState("done");
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to generate summary");
      setState("error");
    }
  }

  return (
    <div className="rounded-xl bg-white border border-gray-200 shadow-sm overflow-hidden">
      <div className="flex items-center justify-between px-5 py-4 bg-gradient-to-r from-blue-50 to-indigo-50 border-b border-blue-100">
        <div className="flex items-center gap-3">
          <div className="rounded-lg bg-blue-100 p-2">
            <FileText className="h-5 w-5 text-blue-700" />
          </div>
          <div>
            <p className="font-semibold text-gray-900">{className} — Executive Summary</p>
            <p className="text-xs text-gray-500">Term {termNumber} {year} · Admin view</p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          {state === "done" && (
            <button
              onClick={() => generate(true)}
              title="Regenerate"
              className="p-1.5 text-gray-400 hover:text-gray-600 rounded"
            >
              <RefreshCw className="h-4 w-4" />
            </button>
          )}
          {state !== "loading" && (
            <Button onClick={() => generate(false)} className="gap-2" size="sm">
              <Sparkles className="h-4 w-4" />
              {state === "done" ? "Regenerate" : "Generate Summary"}
            </Button>
          )}
          {state === "loading" && (
            <div className="flex items-center gap-2 text-sm text-gray-500">
              <Loader2 className="h-4 w-4 animate-spin" /> Generating…
            </div>
          )}
        </div>
      </div>

      {state === "idle" && (
        <div className="px-5 py-8 text-center">
          <Sparkles className="h-8 w-8 text-gray-200 mx-auto mb-3" />
          <p className="text-sm text-gray-500">Click Generate Summary to create an AI-powered executive overview of this class&apos;s performance.</p>
        </div>
      )}

      {state === "error" && (
        <div className="px-5 py-4 text-sm text-red-700 bg-red-50">
          {error}
        </div>
      )}

      {state === "done" && summary && (
        <div className="px-5 py-5">
          <div
            className="text-sm text-gray-800 leading-relaxed prose-sm"
            dangerouslySetInnerHTML={{ __html: `<p class="mb-2">${renderMarkdown(summary)}</p>` }}
          />
          {fromCache && (
            <p className="text-[10px] text-gray-400 mt-3 pt-3 border-t border-gray-100">
              Cached response · Term {termNumber} {year}
            </p>
          )}
        </div>
      )}
    </div>
  );
}
