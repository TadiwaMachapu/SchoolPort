"use client";
import { useEffect, useState } from "react";
import { api, Quiz, QuizAttempt, CreateQuizRequest, CreateQuizQuestionRequest } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";
import { SkeletonCards } from "@/components/ui/skeleton";
import { usePermission, useIdentity } from "@/lib/auth-context";
import {
  Brain, FileQuestion, Clock, RefreshCw, Plus, Trash2,
  ChevronLeft, ChevronRight, Eye, EyeOff, BarChart2, X,
  CheckCircle2, XCircle, Loader2,
} from "lucide-react";

// ── Types ─────────────────────────────────────────────────────────
type Role = "Admin" | "Teacher" | "Student" | "Parent" | string;

// ── Helpers ───────────────────────────────────────────────────────
function scoreBadge(pct?: number) {
  if (pct == null) return null;
  const variant = pct >= 80 ? "success" : pct >= 60 ? "warning" : "destructive";
  return <Badge variant={variant}>{pct}%</Badge>;
}

function fmtDate(d: string) {
  return new Date(d).toLocaleDateString("en-US", { month: "short", day: "numeric", year: "numeric" });
}

// ── Results modal (teacher) ────────────────────────────────────────
function ResultsModal({ quiz, onClose }: { quiz: Quiz; onClose: () => void }) {
  const [attempts, setAttempts] = useState<QuizAttempt[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.quizzes.allAttempts(quiz.quizId)
      .then(setAttempts)
      .finally(() => setLoading(false));
  }, [quiz.quizId]);

  const submitted = attempts.filter(a => a.isCompleted);
  const avgPct = submitted.length
    ? Math.round(submitted.reduce((s, a) => s + (a.percentage ?? 0), 0) / submitted.length)
    : null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm p-4">
      <div className="w-full max-w-2xl rounded-2xl bg-surface-card shadow-2xl flex flex-col max-h-[85vh]">
        <div className="flex items-center justify-between border-b border-border px-6 py-4 shrink-0">
          <div>
            <h2 className="font-semibold text-text-primary">{quiz.title} — Results</h2>
            <p className="text-xs text-text-muted mt-0.5">
              {submitted.length} submitted{avgPct != null ? ` · avg ${avgPct}%` : ""}
            </p>
          </div>
          <button onClick={onClose} className="rounded-full p-1.5 text-text-muted hover:bg-surface-subtle">
            <X className="h-5 w-5" />
          </button>
        </div>
        <div className="flex-1 overflow-y-auto p-6">
          {loading ? (
            <div className="space-y-3">
              {[...Array(4)].map((_, i) => <div key={i} className="h-12 animate-pulse rounded-xl bg-surface-subtle" />)}
            </div>
          ) : attempts.length === 0 ? (
            <div className="py-12 text-center text-text-muted">No attempts yet</div>
          ) : (
            <table className="w-full text-sm">
              <thead className="border-b border-border">
                <tr className="text-left">
                  <th className="pb-2 font-medium text-text-muted">Started</th>
                  <th className="pb-2 font-medium text-text-muted">Submitted</th>
                  <th className="pb-2 font-medium text-text-muted">Score</th>
                  <th className="pb-2 font-medium text-text-muted">Result</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {attempts.map(a => (
                  <tr key={a.attemptId}>
                    <td className="py-3 text-text-secondary">{fmtDate(a.startedAt)}</td>
                    <td className="py-3 text-text-secondary">{a.submittedAt ? fmtDate(a.submittedAt) : "—"}</td>
                    <td className="py-3 text-text-secondary">
                      {a.isCompleted ? `${a.score ?? 0} / ${a.maxScore ?? 0}` : "In progress"}
                    </td>
                    <td className="py-3">{scoreBadge(a.percentage)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      </div>
    </div>
  );
}

// ── Quiz builder (teacher/admin) ───────────────────────────────────
type BuildStep = 1 | 2 | 3;

const EMPTY_QUESTION = (): CreateQuizQuestionRequest => ({
  text: "", type: "MultipleChoice", order: 0, marks: 1, explanation: "",
  options: [
    { text: "", isCorrect: true,  order: 0 },
    { text: "", isCorrect: false, order: 1 },
    { text: "", isCorrect: false, order: 2 },
    { text: "", isCorrect: false, order: 3 },
  ],
});

const EMPTY_DRAFT: CreateQuizRequest = {
  title: "", description: "", timeLimitMinutes: undefined, maxAttempts: 1,
  shuffleQuestions: false, showResultsImmediately: true,
  questions: [EMPTY_QUESTION()],
};

function QuizBuilder({ onClose, onCreate }: { onClose: () => void; onCreate: (q: Quiz) => void }) {
  const [step, setStep] = useState<BuildStep>(1);
  const [draft, setDraft] = useState<CreateQuizRequest>(EMPTY_DRAFT);
  const [activeQ, setActiveQ] = useState(0);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  function updateField<K extends keyof CreateQuizRequest>(k: K, v: CreateQuizRequest[K]) {
    setDraft(d => ({ ...d, [k]: v }));
  }

  function updateQuestion(i: number, patch: Partial<CreateQuizQuestionRequest>) {
    setDraft(d => {
      const qs = [...d.questions];
      qs[i] = { ...qs[i], ...patch };
      return { ...d, questions: qs };
    });
  }

  function updateOption(qi: number, oi: number, text: string) {
    setDraft(d => {
      const qs = [...d.questions];
      const opts = [...qs[qi].options];
      opts[oi] = { ...opts[oi], text };
      qs[qi] = { ...qs[qi], options: opts };
      return { ...d, questions: qs };
    });
  }

  function setCorrect(qi: number, oi: number) {
    setDraft(d => {
      const qs = [...d.questions];
      const opts = qs[qi].options.map((o, j) => ({ ...o, isCorrect: j === oi }));
      qs[qi] = { ...qs[qi], options: opts };
      return { ...d, questions: qs };
    });
  }

  function addQuestion() {
    const q = { ...EMPTY_QUESTION(), order: draft.questions.length };
    setDraft(d => ({ ...d, questions: [...d.questions, q] }));
    setActiveQ(draft.questions.length);
  }

  function removeQuestion(i: number) {
    if (draft.questions.length <= 1) return;
    setDraft(d => ({ ...d, questions: d.questions.filter((_, j) => j !== i).map((q, j) => ({ ...q, order: j })) }));
    setActiveQ(prev => Math.min(prev, draft.questions.length - 2));
  }

  async function submit(publish: boolean) {
    setSaving(true); setError("");
    try {
      const quiz = await api.quizzes.create({ ...draft, timeLimitMinutes: draft.timeLimitMinutes || undefined });
      if (publish) await api.quizzes.publish(quiz.quizId, true);
      onCreate({ ...quiz, isPublished: publish });
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Failed to create quiz");
    } finally { setSaving(false); }
  }

  const currentQ = draft.questions[activeQ];
  const stepLabels: Record<BuildStep, string> = { 1: "Quiz Settings", 2: "Questions", 3: "Review" };

  return (
    <div className="fixed inset-0 z-50 flex flex-col justify-end sm:items-center sm:justify-center bg-black/50 backdrop-blur-sm p-0 sm:p-4">
      <div className="w-full sm:max-w-2xl rounded-t-3xl sm:rounded-3xl bg-surface-card shadow-2xl flex flex-col max-h-[95vh] sm:max-h-[90vh]">
        {/* Handle */}
        <div className="flex justify-center pt-3 pb-1 sm:hidden shrink-0">
          <div className="h-1 w-10 rounded-full bg-border" />
        </div>

        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-border shrink-0">
          <div>
            <h2 className="text-lg font-semibold text-text-primary">New Quiz</h2>
            <p className="text-xs text-text-muted mt-0.5">{stepLabels[step]}</p>
          </div>
          <button onClick={onClose} className="rounded-full p-2 text-text-muted hover:bg-surface-subtle min-h-[44px] min-w-[44px] flex items-center justify-center">
            <X className="h-5 w-5" />
          </button>
        </div>

        {/* Step dots */}
        <div className="flex items-center justify-center gap-2 py-3 shrink-0">
          {([1, 2, 3] as BuildStep[]).map(s => (
            <button key={s} onClick={() => s < step && setStep(s)}
              className={`h-2 rounded-full transition-all ${s === step ? "w-6 bg-primary" : s < step ? "w-2 bg-primary-300" : "w-2 bg-border"}`}
            />
          ))}
        </div>

        {/* Content */}
        <div className="flex-1 overflow-y-auto px-6 pb-4">
          {error && <div className="mb-4 rounded-xl bg-danger-100 p-3 text-sm text-danger-700">{error}</div>}

          {/* Step 1: Settings */}
          {step === 1 && (
            <div className="space-y-4 pt-2">
              <div className="space-y-1.5">
                <label className="text-sm font-medium text-text-primary">Quiz Title</label>
                <Input value={draft.title} autoFocus placeholder="e.g. Chapter 5 Quiz"
                  onChange={e => updateField("title", e.target.value)} className="min-h-[48px] text-base" />
              </div>
              <div className="space-y-1.5">
                <label className="text-sm font-medium text-text-primary">Description <span className="text-text-muted font-normal">(optional)</span></label>
                <textarea rows={3} value={draft.description} placeholder="What is this quiz about?"
                  onChange={e => updateField("description", e.target.value)}
                  className="w-full rounded-xl border border-border px-3 py-3 text-sm placeholder:text-text-muted focus:outline-none focus:ring-2 focus:ring-primary resize-none" />
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-1.5">
                  <label className="text-sm font-medium text-text-primary">Time limit (min)</label>
                  <Input type="number" min={1} max={300} value={draft.timeLimitMinutes ?? ""}
                    placeholder="None"
                    onChange={e => updateField("timeLimitMinutes", e.target.value ? Number(e.target.value) : undefined)}
                    className="min-h-[48px]" />
                </div>
                <div className="space-y-1.5">
                  <label className="text-sm font-medium text-text-primary">Max attempts</label>
                  <Input type="number" min={1} max={10} value={draft.maxAttempts}
                    onChange={e => updateField("maxAttempts", Number(e.target.value))}
                    className="min-h-[48px]" />
                </div>
              </div>
              <div className="space-y-2.5 pt-1">
                {([
                  ["shuffleQuestions", "Shuffle question order"],
                  ["showResultsImmediately", "Show results immediately after submit"],
                ] as [keyof CreateQuizRequest, string][]).map(([key, label]) => (
                  <label key={key} className="flex items-center gap-3 cursor-pointer min-h-[44px]">
                    <input type="checkbox" checked={!!draft[key]}
                      onChange={e => updateField(key, e.target.checked as CreateQuizRequest[typeof key])}
                      className="h-4 w-4 rounded border-border text-primary focus:ring-primary" />
                    <span className="text-sm text-text-primary">{label}</span>
                  </label>
                ))}
              </div>
            </div>
          )}

          {/* Step 2: Questions */}
          {step === 2 && (
            <div className="flex gap-4 pt-2">
              {/* Question list sidebar */}
              <div className="w-28 shrink-0 space-y-1.5">
                {draft.questions.map((_, i) => (
                  <button key={i} onClick={() => setActiveQ(i)}
                    className={`w-full rounded-xl px-3 py-2 text-sm font-medium text-left transition-colors ${
                      activeQ === i ? "bg-primary text-white" : "bg-surface-subtle text-text-secondary hover:bg-border"
                    }`}>
                    Q{i + 1}
                  </button>
                ))}
                <button onClick={addQuestion}
                  className="w-full rounded-xl border-2 border-dashed border-border px-3 py-2 text-sm text-text-muted hover:border-primary-400 hover:text-primary transition-colors flex items-center justify-center gap-1">
                  <Plus className="h-3.5 w-3.5" />
                </button>
              </div>

              {/* Active question editor */}
              {currentQ && (
                <div className="flex-1 space-y-3">
                  <div className="flex items-start justify-between gap-2">
                    <label className="text-sm font-medium text-text-primary">Question {activeQ + 1}</label>
                    {draft.questions.length > 1 && (
                      <button onClick={() => removeQuestion(activeQ)}
                        className="text-text-muted hover:text-danger-500 transition-colors">
                        <Trash2 className="h-4 w-4" />
                      </button>
                    )}
                  </div>
                  <textarea rows={3} value={currentQ.text}
                    placeholder="Enter your question…"
                    onChange={e => updateQuestion(activeQ, { text: e.target.value })}
                    className="w-full rounded-xl border border-border px-3 py-2.5 text-sm placeholder:text-text-muted focus:outline-none focus:ring-2 focus:ring-primary resize-none" />

                  <div className="grid grid-cols-2 gap-3">
                    <div>
                      <label className="text-xs font-medium text-text-secondary mb-1 block">Type</label>
                      <select value={currentQ.type}
                        onChange={e => updateQuestion(activeQ, { type: e.target.value })}
                        className="w-full rounded-xl border border-border px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary">
                        <option value="MultipleChoice">Multiple Choice</option>
                        <option value="ShortAnswer">Short Answer</option>
                      </select>
                    </div>
                    <div>
                      <label className="text-xs font-medium text-text-secondary mb-1 block">Marks</label>
                      <Input type="number" min={0.5} step={0.5} value={currentQ.marks}
                        onChange={e => updateQuestion(activeQ, { marks: Number(e.target.value) })} />
                    </div>
                  </div>

                  {currentQ.type === "MultipleChoice" && (
                    <div className="space-y-2">
                      <label className="text-xs font-medium text-text-secondary block">Options — click circle to mark correct</label>
                      {currentQ.options.map((opt, oi) => (
                        <div key={oi} className="flex items-center gap-2">
                          <button onClick={() => setCorrect(activeQ, oi)}
                            className={`flex-shrink-0 h-6 w-6 rounded-full border-2 flex items-center justify-center transition-colors ${
                              opt.isCorrect ? "border-success-500 bg-success-500" : "border-border hover:border-primary-400"
                            }`}>
                            {opt.isCorrect && <CheckCircle2 className="h-4 w-4 text-white" />}
                          </button>
                          <Input value={opt.text} placeholder={`Option ${oi + 1}`}
                            onChange={e => updateOption(activeQ, oi, e.target.value)}
                            className={opt.isCorrect ? "border-success-500/30 bg-success-100" : ""} />
                        </div>
                      ))}
                    </div>
                  )}

                  <div className="space-y-1.5">
                    <label className="text-xs font-medium text-text-secondary">Explanation <span className="text-text-muted font-normal">(shown after submit)</span></label>
                    <Input value={currentQ.explanation ?? ""} placeholder="Why is this the correct answer?"
                      onChange={e => updateQuestion(activeQ, { explanation: e.target.value })} />
                  </div>
                </div>
              )}
            </div>
          )}

          {/* Step 3: Review */}
          {step === 3 && (
            <div className="space-y-4 pt-2">
              <div className="rounded-2xl bg-surface-subtle border border-border p-4 space-y-2 text-sm">
                <p className="font-semibold text-text-primary mb-3">Quiz Summary</p>
                <p className="text-text-secondary"><span className="text-text-muted">Title:</span> {draft.title}</p>
                {draft.description && <p className="text-text-secondary"><span className="text-text-muted">Description:</span> {draft.description}</p>}
                <p className="text-text-secondary"><span className="text-text-muted">Questions:</span> {draft.questions.length}</p>
                <p className="text-text-secondary"><span className="text-text-muted">Total marks:</span> {draft.questions.reduce((s, q) => s + q.marks, 0)}</p>
                <p className="text-text-secondary"><span className="text-text-muted">Time limit:</span> {draft.timeLimitMinutes ? `${draft.timeLimitMinutes} min` : "None"}</p>
                <p className="text-text-secondary"><span className="text-text-muted">Max attempts:</span> {draft.maxAttempts}</p>
              </div>

              <div className="space-y-3">
                {draft.questions.map((q, i) => (
                  <div key={i} className="rounded-xl border border-border p-4">
                    <p className="font-medium text-text-primary text-sm mb-2">
                      Q{i + 1}. {q.text || <span className="text-text-muted italic">No question text</span>}
                      <span className="ml-2 text-xs text-text-muted">({q.marks} mark{q.marks !== 1 ? "s" : ""})</span>
                    </p>
                    {q.type === "MultipleChoice" && (
                      <div className="space-y-1 pl-2">
                        {q.options.map((o, j) => (
                          <div key={j} className={`flex items-center gap-2 text-sm ${o.isCorrect ? "text-success-700 font-medium" : "text-text-secondary"}`}>
                            {o.isCorrect ? <CheckCircle2 className="h-3.5 w-3.5 shrink-0" /> : <XCircle className="h-3.5 w-3.5 shrink-0 text-text-muted" />}
                            {o.text || <span className="italic text-text-muted">empty</span>}
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="border-t border-border px-6 py-4 flex gap-3 shrink-0">
          {step > 1 ? (
            <button onClick={() => setStep(s => (s - 1) as BuildStep)}
              className="flex items-center gap-1.5 rounded-xl border border-border px-4 py-3 text-sm font-medium text-text-primary hover:bg-surface-subtle transition-colors min-h-[48px]">
              <ChevronLeft className="h-4 w-4" /> Back
            </button>
          ) : (
            <button onClick={onClose}
              className="flex items-center gap-1.5 rounded-xl border border-border px-4 py-3 text-sm font-medium text-text-primary hover:bg-surface-subtle transition-colors min-h-[48px]">
              Cancel
            </button>
          )}
          <div className="flex-1" />
          {step < 3 ? (
            <Button onClick={() => setStep(s => (s + 1) as BuildStep)}
              disabled={step === 1 && !draft.title.trim()}
              className="gap-1.5 min-h-[48px] px-6">
              Next <ChevronRight className="h-4 w-4" />
            </Button>
          ) : (
            <div className="flex gap-2">
              <Button variant="outline" onClick={() => submit(false)} loading={saving} className="min-h-[48px]">
                Save as Draft
              </Button>
              <Button onClick={() => submit(true)} loading={saving} className="min-h-[48px] px-6">
                Publish Quiz
              </Button>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

// ── Student take-quiz modal ────────────────────────────────────────
function TakeQuizModal({ quiz, onClose }: { quiz: Quiz; onClose: () => void }) {
  interface QuizQuestion { questionId: string; text: string; type: string; marks: number; options: { optionId: string; text: string }[]; }
  const [fullQuiz, setFullQuiz] = useState<{ title: string; questions: QuizQuestion[] } | null>(null);
  const [attemptId, setAttemptId] = useState<string | null>(null);
  const [answers, setAnswers] = useState<Record<string, string>>({});
  const [result, setResult] = useState<{ score: number; maxScore: number; percentage: number } | null>(null);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    (async () => {
      try {
        const [q, a] = await Promise.all([
          api.quizzes.get(quiz.quizId),
          api.quizzes.startAttempt(quiz.quizId),
        ]);
        setFullQuiz(q as typeof fullQuiz);
        setAttemptId(a.attemptId);
      } catch (e: unknown) {
        setError(e instanceof Error ? e.message : "Failed to start quiz");
      } finally { setLoading(false); }
    })();
  }, [quiz.quizId]);

  async function submit() {
    if (!attemptId) return;
    setSubmitting(true);
    try {
      const answerList = Object.entries(answers).map(([questionId, selectedOptionId]) => ({ questionId, selectedOptionId }));
      const res = await api.quizzes.submit(attemptId, answerList);
      setResult({ score: res.score ?? 0, maxScore: res.maxScore ?? 0, percentage: res.percentage ?? 0 });
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Submission failed");
    } finally { setSubmitting(false); }
  }

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
      <div className="bg-surface-card rounded-2xl shadow-2xl w-full max-w-2xl max-h-[90vh] flex flex-col">
        <div className="sticky top-0 bg-surface-card border-b border-border px-6 py-4 flex items-center justify-between shrink-0 rounded-t-2xl">
          <h2 className="text-lg font-semibold text-text-primary">{quiz.title}</h2>
          <button onClick={onClose} className="rounded-full p-1.5 text-text-muted hover:bg-surface-subtle">
            <X className="h-5 w-5" />
          </button>
        </div>
        <div className="flex-1 overflow-y-auto p-6">
          {loading && <div className="text-center text-text-muted py-8">Starting quiz…</div>}
          {error && <div className="rounded-xl bg-danger-100 p-3 text-sm text-danger-700 mb-4">{error}</div>}

          {result && (
            <div className="text-center py-8">
              <Brain className={`h-14 w-14 mx-auto mb-4 ${result.percentage >= 80 ? "text-success-500" : result.percentage >= 60 ? "text-warning-500" : "text-danger-500"}`} />
              <h3 className="text-2xl font-bold text-text-primary mb-2">Quiz Complete!</h3>
              <p className="text-4xl font-bold text-primary mb-2">{result.percentage}%</p>
              <p className="text-text-secondary mb-6">{result.score} / {result.maxScore} marks</p>
              <Button onClick={onClose}>Close</Button>
            </div>
          )}

          {!loading && !result && fullQuiz && (
            <div className="space-y-6">
              {(fullQuiz.questions as QuizQuestion[]).map((q, i) => (
                <div key={q.questionId} className="border border-border rounded-xl p-4">
                  <p className="font-medium text-text-primary mb-3">
                    <span className="text-text-muted mr-2">Q{i + 1}.</span>{q.text}
                    <span className="text-xs text-text-muted ml-2">({q.marks} mark{q.marks !== 1 ? "s" : ""})</span>
                  </p>
                  {q.type !== "ShortAnswer" ? (
                    <div className="space-y-2">
                      {q.options.map(opt => (
                        <label key={opt.optionId}
                          className={`flex items-center gap-3 p-3 rounded-xl cursor-pointer transition-colors ${
                            answers[q.questionId] === opt.optionId ? "bg-primary-50 border border-primary-300" : "bg-surface-subtle hover:bg-border border border-transparent"
                          }`}>
                          <input type="radio" name={q.questionId} value={opt.optionId}
                            checked={answers[q.questionId] === opt.optionId}
                            onChange={() => setAnswers(a => ({ ...a, [q.questionId]: opt.optionId }))}
                            className="text-primary" />
                          <span className="text-sm text-text-primary">{opt.text}</span>
                        </label>
                      ))}
                    </div>
                  ) : (
                    <textarea placeholder="Your answer…" rows={3}
                      value={answers[q.questionId] ?? ""}
                      onChange={e => setAnswers(a => ({ ...a, [q.questionId]: e.target.value }))}
                      className="w-full rounded-xl border border-border px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary resize-none" />
                  )}
                </div>
              ))}
              <Button onClick={submit} loading={submitting} className="w-full min-h-[48px]">Submit Quiz</Button>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

// ── Quiz card ──────────────────────────────────────────────────────
function QuizCard({ quiz, onPublishToggle, onDelete, onShowResults }: {
  quiz: Quiz;
  onPublishToggle: (q: Quiz) => void;
  onDelete: (id: string) => void;
  onShowResults: (q: Quiz) => void;
}) {
  const [activeQuiz, setActiveQuiz] = useState(false);
  const [toggling, setToggling] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const canManage = usePermission("assessment.create"); // Step 8
  const isLearner = useIdentity() === "Learner";

  async function togglePublish() {
    setToggling(true);
    try {
      const updated = await api.quizzes.publish(quiz.quizId, !quiz.isPublished);
      onPublishToggle(updated);
    } finally { setToggling(false); }
  }

  async function del() {
    if (!confirm("Delete this quiz? This cannot be undone.")) return;
    setDeleting(true);
    try { await api.quizzes.delete(quiz.quizId); onDelete(quiz.quizId); }
    finally { setDeleting(false); }
  }

  return (
    <>
      <Card className="hover:shadow-md transition-shadow">
        <CardContent className="p-5">
          <div className="flex items-start justify-between gap-2 mb-3">
            <h3 className="font-semibold text-text-primary leading-snug">{quiz.title}</h3>
            <Badge variant={quiz.isPublished ? "success" : "outline"} className="shrink-0">
              {quiz.isPublished ? "Open" : "Draft"}
            </Badge>
          </div>

          {quiz.description && (
            <p className="text-sm text-text-secondary mb-3 line-clamp-2">{quiz.description}</p>
          )}

          <div className="flex flex-wrap items-center gap-3 text-xs text-text-secondary mb-4">
            <span className="flex items-center gap-1"><FileQuestion className="h-3.5 w-3.5" />{quiz.questionCount} q</span>
            {quiz.timeLimitMinutes && <span className="flex items-center gap-1"><Clock className="h-3.5 w-3.5" />{quiz.timeLimitMinutes} min</span>}
            <span className="flex items-center gap-1"><RefreshCw className="h-3.5 w-3.5" />{quiz.maxAttempts} attempt{quiz.maxAttempts !== 1 ? "s" : ""}</span>
          </div>

          <div className="flex items-center justify-between gap-2">
            <p className="text-xs text-text-muted">by {quiz.createdByName}</p>
            <div className="flex items-center gap-1.5">
              {canManage && (
                <>
                  <button onClick={() => onShowResults(quiz)}
                    className="flex items-center gap-1 rounded-lg px-2.5 py-1.5 text-xs text-text-secondary hover:bg-surface-subtle transition-colors">
                    <BarChart2 className="h-3.5 w-3.5" /> Results
                  </button>
                  <button onClick={togglePublish} disabled={toggling}
                    className="flex items-center gap-1 rounded-lg px-2.5 py-1.5 text-xs text-text-secondary hover:bg-surface-subtle transition-colors disabled:opacity-50">
                    {toggling ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : quiz.isPublished ? <EyeOff className="h-3.5 w-3.5" /> : <Eye className="h-3.5 w-3.5" />}
                    {quiz.isPublished ? "Unpublish" : "Publish"}
                  </button>
                  <button onClick={del} disabled={deleting}
                    className="flex items-center gap-1 rounded-lg px-2 py-1.5 text-xs text-danger-500 hover:bg-danger-100 transition-colors disabled:opacity-50">
                    {deleting ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Trash2 className="h-3.5 w-3.5" />}
                  </button>
                </>
              )}
              {isLearner && quiz.isPublished && (
                <Button size="sm" onClick={() => setActiveQuiz(true)}>Take Quiz</Button>
              )}
            </div>
          </div>
        </CardContent>
      </Card>

      {activeQuiz && <TakeQuizModal quiz={quiz} onClose={() => setActiveQuiz(false)} />}
    </>
  );
}

// ── Page ───────────────────────────────────────────────────────────
export default function QuizzesPage() {
  const [quizzes,      setQuizzes]      = useState<Quiz[]>([]);
  const [total,        setTotal]        = useState(0);
  const [loading,      setLoading]      = useState(true);
  const [error,        setError]        = useState("");
  const [showBuilder,  setShowBuilder]  = useState(false);
  const [resultsQuiz,  setResultsQuiz]  = useState<Quiz | null>(null);
  const canManage = usePermission("assessment.create"); // Step 8

  async function load() {
    setLoading(true); setError("");
    try {
      const res = await api.quizzes.list({ page: 1, pageSize: 50, teacherView: canManage });
      setQuizzes(res.items);
      setTotal(res.total);
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Failed to load");
    } finally { setLoading(false); }
  }

  useEffect(() => {
    load();
  }, [canManage]); // eslint-disable-line react-hooks/exhaustive-deps

  function handlePublishToggle(updated: Quiz) {
    setQuizzes(prev => prev.map(q => q.quizId === updated.quizId ? updated : q));
  }
  function handleDelete(id: string) {
    setQuizzes(prev => prev.filter(q => q.quizId !== id));
    setTotal(t => t - 1);
  }
  function handleCreate(q: Quiz) {
    setQuizzes(prev => [q, ...prev]);
    setTotal(t => t + 1);
    setShowBuilder(false);
  }

  return (
    <div className="p-4 md:p-6 lg:p-8">
      <div className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-xl md:text-2xl font-semibold text-text-primary tracking-tight">Quizzes</h1>
          <p className="text-sm text-text-secondary mt-1">{total} quiz{total !== 1 ? "zes" : ""}</p>
        </div>
        {canManage && (
          <Button onClick={() => setShowBuilder(true)} className="gap-1.5">
            <Plus className="h-4 w-4" /> Create Quiz
          </Button>
        )}
      </div>

      {error && <div className="mb-4 rounded-xl bg-danger-100 p-3 text-sm text-danger-700">{error}</div>}

      {loading ? (
        <SkeletonCards count={6} />
      ) : quizzes.length === 0 ? (
        <div className="rounded-2xl border-2 border-dashed border-border py-16 text-center">
          <Brain className="h-10 w-10 text-text-muted mx-auto mb-3" />
          <p className="text-base font-medium text-text-primary">No quizzes yet</p>
          <p className="text-sm text-text-muted mt-1 px-8">
            {canManage ? "Create your first quiz to assess students" : "Published quizzes from your teachers will appear here"}
          </p>
          {canManage && (
            <Button className="mt-5 gap-1.5" onClick={() => setShowBuilder(true)}>
              <Plus className="h-4 w-4" /> Create Quiz
            </Button>
          )}
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {quizzes.map(q => (
            <QuizCard
              key={q.quizId}
              quiz={q}
              onPublishToggle={handlePublishToggle}
              onDelete={handleDelete}
              onShowResults={setResultsQuiz}
            />
          ))}
        </div>
      )}

      {showBuilder && <QuizBuilder onClose={() => setShowBuilder(false)} onCreate={handleCreate} />}
      {resultsQuiz && <ResultsModal quiz={resultsQuiz} onClose={() => setResultsQuiz(null)} />}
    </div>
  );
}
