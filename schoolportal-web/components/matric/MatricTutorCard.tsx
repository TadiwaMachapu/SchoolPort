"use client";
import { useState } from "react";
import { api } from "@/lib/api";
import { Sparkles, Loader2, AlertTriangle, RefreshCw, Send } from "lucide-react";

interface Props {
  subjects: string[];
}

export default function MatricTutorCard({ subjects }: Props) {
  const [subject, setSubject] = useState(subjects[0] ?? "");
  const [question, setQuestion] = useState("");
  const [answer, setAnswer] = useState<string | null>(null);
  const [fromCache, setFromCache] = useState(false);
  const [unavailable, setUnavailable] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  async function ask(forceRefresh = false) {
    if (!question.trim() || !subject) return;
    setLoading(true);
    setError("");
    setAnswer(null);
    setUnavailable(false);
    try {
      const res = await api.matric.tutor(subject, question.trim(), forceRefresh);
      if (!res.available || !res.answer) {
        setUnavailable(true);
      } else {
        setAnswer(res.answer);
        setFromCache(res.fromCache ?? false);
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : "Something went wrong");
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="space-y-3">
      {/* Controls */}
      <div className="flex gap-2 flex-wrap">
        <select
          value={subject}
          onChange={e => { setSubject(e.target.value); setAnswer(null); setUnavailable(false); }}
          className="rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-purple-400"
        >
          {subjects.map(s => <option key={s} value={s}>{s}</option>)}
        </select>
      </div>

      <div className="flex gap-2">
        <textarea
          value={question}
          onChange={e => setQuestion(e.target.value)}
          placeholder={`Ask a ${subject} question…`}
          rows={3}
          className="flex-1 rounded-lg border border-gray-300 px-3 py-2 text-sm resize-none focus:outline-none focus:ring-2 focus:ring-purple-400"
          onKeyDown={e => { if (e.key === "Enter" && e.ctrlKey) ask(); }}
        />
        <button
          onClick={() => ask()}
          disabled={loading || !question.trim()}
          className="self-end flex items-center gap-1.5 rounded-lg bg-purple-600 px-4 py-2 text-sm font-medium text-white hover:bg-purple-700 disabled:opacity-50 transition-colors"
        >
          {loading ? <Loader2 className="h-4 w-4 animate-spin" /> : <Send className="h-4 w-4" />}
          Ask
        </button>
      </div>

      {/* Error */}
      {error && (
        <div className="flex items-center gap-2 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          <AlertTriangle className="h-4 w-4 shrink-0" /> {error}
          <button onClick={() => ask()} className="ml-auto text-xs underline">Retry</button>
        </div>
      )}

      {/* Unavailable */}
      {unavailable && (
        <div className="rounded-xl border border-gray-200 bg-gray-50 px-4 py-4 text-sm text-gray-500 flex items-start gap-3">
          <Sparkles className="h-4 w-4 mt-0.5 text-gray-400 shrink-0" />
          <div>
            <p className="font-medium text-gray-600">AI tutor not available</p>
            <p className="text-xs mt-0.5">AI features may not be configured, or the monthly usage limit has been reached.</p>
          </div>
        </div>
      )}

      {/* Answer */}
      {answer && (
        <div className="rounded-xl border border-purple-200 bg-white shadow-sm overflow-hidden">
          <div className="bg-purple-50 border-b border-purple-100 px-4 py-3 flex items-center justify-between">
            <div className="flex items-center gap-2 text-sm font-semibold text-purple-800">
              <Sparkles className="h-4 w-4" />
              AI Tutor Answer
              {fromCache && (
                <span className="text-[10px] font-normal text-purple-500 bg-purple-100 rounded-full px-2 py-0.5">cached</span>
              )}
            </div>
            <button
              onClick={() => ask(true)}
              className="flex items-center gap-1 text-xs text-purple-600 hover:text-purple-800 transition-colors"
            >
              <RefreshCw className="h-3 w-3" /> Regenerate
            </button>
          </div>
          <div className="p-4">
            {/* Render answer as formatted text — split on markdown headers/bullets */}
            <div className="text-sm text-gray-800 whitespace-pre-wrap leading-relaxed">
              {answer}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
