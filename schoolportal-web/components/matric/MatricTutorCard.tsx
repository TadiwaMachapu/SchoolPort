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
  const [reason, setReason] = useState<string | null>(null);
  const [remaining, setRemaining] = useState<number | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  async function ask(forceRefresh = false) {
    if (!question.trim() || !subject) return;
    setLoading(true);
    setError("");
    setAnswer(null);
    setUnavailable(false);
    setReason(null);
    try {
      const res = await api.matric.tutor(subject, question.trim(), forceRefresh);
      if (typeof res.remainingToday === "number" && res.remainingToday >= 0) setRemaining(res.remainingToday);
      if (!res.available || !res.answer) {
        setUnavailable(true);
        setReason(res.reason ?? null);
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

  const unavailableCopy =
    reason === "rate_limited"
      ? { title: "Daily question limit reached", body: "You have used all your tutor questions for today. Your quota resets tomorrow — until then, try past papers or a quiz." }
      : reason === "api_error"
      ? { title: "The tutor hit a snag", body: "The AI service didn't respond properly. Your question didn't count against your daily limit — try again in a moment." }
      : { title: "AI tutor not available", body: "AI features may not be configured yet. Ask your school's administrator, or try again later." };

  return (
    <div className="space-y-3">
      {/* Controls */}
      <div className="flex gap-2 flex-wrap">
        <select
          value={subject}
          onChange={e => { setSubject(e.target.value); setAnswer(null); setUnavailable(false); }}
          className="rounded-lg border border-border px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary"
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
          className="flex-1 rounded-lg border border-border px-3 py-2 text-sm resize-none focus:outline-none focus:ring-2 focus:ring-primary"
          onKeyDown={e => { if (e.key === "Enter" && e.ctrlKey) ask(); }}
        />
        <button
          onClick={() => ask()}
          disabled={loading || !question.trim()}
          className="self-end flex items-center gap-1.5 rounded-lg bg-primary px-4 py-2 text-sm font-medium text-white hover:bg-primary-700 disabled:opacity-50 transition-colors"
        >
          {loading ? <Loader2 className="h-4 w-4 animate-spin" /> : <Send className="h-4 w-4" />}
          Ask
        </button>
      </div>

      {/* Error */}
      {error && (
        <div className="flex items-center gap-2 rounded-xl bg-danger-100 px-4 py-3 text-sm text-danger-700">
          <AlertTriangle className="h-4 w-4 shrink-0" /> {error}
          <button onClick={() => ask()} className="ml-auto text-xs underline">Retry</button>
        </div>
      )}

      {/* Remaining quota */}
      {remaining !== null && !unavailable && (
        <p className="text-xs text-text-muted">
          {remaining} tutor question{remaining !== 1 ? "s" : ""} left today
        </p>
      )}

      {/* Unavailable */}
      {unavailable && (
        <div className="rounded-xl border border-border bg-surface-subtle px-4 py-4 text-sm text-text-secondary flex items-start gap-3">
          <Sparkles className="h-4 w-4 mt-0.5 text-text-muted shrink-0" />
          <div>
            <p className="font-medium text-text-primary">{unavailableCopy.title}</p>
            <p className="text-xs mt-0.5">{unavailableCopy.body}</p>
          </div>
        </div>
      )}

      {/* Answer */}
      {answer && (
        <div className="rounded-xl border border-primary-200 bg-surface-card shadow-sm overflow-hidden">
          <div className="bg-primary-50 border-b border-primary-100 px-4 py-3 flex items-center justify-between">
            <div className="flex items-center gap-2 text-sm font-semibold text-primary-800">
              <Sparkles className="h-4 w-4" />
              AI Tutor Answer
              {fromCache && (
                <span className="text-[10px] font-normal text-primary bg-primary-100 rounded-full px-2 py-0.5">cached</span>
              )}
            </div>
            <button
              onClick={() => ask(true)}
              className="flex items-center gap-1 text-xs text-primary hover:text-primary-800 transition-colors"
            >
              <RefreshCw className="h-3 w-3" /> Regenerate
            </button>
          </div>
          <div className="p-4">
            {/* Render answer as formatted text — split on markdown headers/bullets */}
            <div className="text-sm text-text-primary whitespace-pre-wrap leading-relaxed">
              {answer}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
