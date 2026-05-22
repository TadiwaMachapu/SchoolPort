"use client";
import { useEffect, useState } from "react";
import { api } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";
import { SkeletonCards } from "@/components/ui/skeleton";

interface Quiz {
  quizId: string;
  title: string;
  description?: string;
  timeLimitMinutes?: number;
  maxAttempts: number;
  isPublished: boolean;
  createdByName: string;
  createdAt: string;
  questionCount: number;
}

interface Attempt {
  attemptId: string;
  quizId: string;
  quizTitle: string;
  startedAt: string;
  submittedAt?: string;
  score?: number;
  maxScore?: number;
  isCompleted: boolean;
  percentage?: number;
}

export default function QuizzesPage() {
  const [quizzes, setQuizzes] = useState<Quiz[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [activeQuiz, setActiveQuiz] = useState<string | null>(null);
  const [myAttempts, setMyAttempts] = useState<Record<string, Attempt[]>>({});

  async function load() {
    setLoading(true);
    setError("");
    try {
      const res = await api.quizzes.list({ page: 1, pageSize: 50 });
      setQuizzes(res.items);
      setTotal(res.total);
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Failed to load");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { load(); }, []);

  function scoreBadge(pct?: number) {
    if (pct === undefined) return null;
    const variant = pct >= 80 ? "success" : pct >= 60 ? "warning" : "destructive";
    return <Badge variant={variant}>{pct}%</Badge>;
  }

  return (
    <div className="p-8">
      <div className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold text-gray-900">Quizzes</h1>
          <p className="text-gray-500 mt-1">{total} quizzes available</p>
        </div>
      </div>

      {error && <div className="mb-4 rounded-md bg-red-50 border border-red-200 p-3 text-sm text-red-700">{error}</div>}

      {loading ? (
        <SkeletonCards count={6} />
      ) : quizzes.length === 0 ? (
        <div className="rounded-lg border-2 border-dashed border-gray-300 py-16 text-center">
          <div className="text-5xl mb-4">🧠</div>
          <p className="text-lg font-medium text-gray-700">No quizzes yet</p>
          <p className="text-sm text-gray-400 mt-1">Quizzes created by teachers will appear here</p>
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {quizzes.map((q) => (
            <Card key={q.quizId} className="hover:shadow-md transition-shadow">
              <CardContent className="p-5">
                <div className="flex items-start justify-between gap-2 mb-3">
                  <h3 className="font-semibold text-gray-900">{q.title}</h3>
                  <Badge variant={q.isPublished ? "success" : "outline"}>
                    {q.isPublished ? "Open" : "Draft"}
                  </Badge>
                </div>

                {q.description && (
                  <p className="text-sm text-gray-500 mb-3 line-clamp-2">{q.description}</p>
                )}

                <div className="flex flex-wrap gap-2 text-xs text-gray-500 mb-4">
                  <span>📝 {q.questionCount} questions</span>
                  {q.timeLimitMinutes && <span>⏱ {q.timeLimitMinutes} min</span>}
                  <span>🔄 {q.maxAttempts} attempt{q.maxAttempts !== 1 ? "s" : ""}</span>
                </div>

                <div className="flex items-center justify-between">
                  <p className="text-xs text-gray-400">by {q.createdByName}</p>
                  {q.isPublished && (
                    <Button size="sm" onClick={() => setActiveQuiz(q.quizId)}>
                      Take Quiz
                    </Button>
                  )}
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      {activeQuiz && (
        <QuizModal quizId={activeQuiz} onClose={() => { setActiveQuiz(null); load(); }} />
      )}
    </div>
  );
}

function QuizModal({ quizId, onClose }: { quizId: string; onClose: () => void }) {
  const [quiz, setQuiz] = useState<{ title: string; questions: QuizQuestion[] } | null>(null);
  const [attemptId, setAttemptId] = useState<string | null>(null);
  const [answers, setAnswers] = useState<Record<string, string>>({});
  const [result, setResult] = useState<{ score: number; maxScore: number; percentage: number } | null>(null);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");

  interface QuizQuestion {
    questionId: string;
    text: string;
    type: string;
    marks: number;
    options: { optionId: string; text: string }[];
  }

  useEffect(() => {
    async function start() {
      setLoading(true);
      try {
        const quizData = await api.quizzes.get(quizId);
        setQuiz(quizData as typeof quiz);
        const attempt = await api.quizzes.startAttempt(quizId);
        setAttemptId(attempt.attemptId);
      } catch (e: unknown) {
        setError(e instanceof Error ? e.message : "Failed to start quiz");
      } finally {
        setLoading(false);
      }
    }
    start();
  }, [quizId]);

  async function submit() {
    if (!attemptId || !quiz) return;
    setSubmitting(true);
    try {
      const answerList = Object.entries(answers).map(([questionId, selectedOptionId]) => ({
        questionId,
        selectedOptionId,
      }));
      const res = await api.quizzes.submit(attemptId, answerList);
      setResult({ score: res.score ?? 0, maxScore: res.maxScore ?? 0, percentage: res.percentage ?? 0 });
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Submission failed");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
      <div className="bg-white rounded-xl shadow-2xl w-full max-w-2xl max-h-[90vh] overflow-y-auto">
        <div className="sticky top-0 bg-white border-b border-gray-200 px-6 py-4 flex items-center justify-between">
          <h2 className="text-xl font-bold text-gray-900">{quiz?.title ?? "Loading…"}</h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600 text-2xl leading-none">&times;</button>
        </div>

        <div className="p-6">
          {loading && <div className="text-center text-gray-400 py-8">Starting quiz…</div>}

          {error && <div className="rounded-md bg-red-50 border border-red-200 p-3 text-sm text-red-700 mb-4">{error}</div>}

          {result && (
            <div className="text-center py-8">
              <div className="text-6xl mb-4">{result.percentage >= 80 ? "🎉" : result.percentage >= 60 ? "👍" : "📚"}</div>
              <h3 className="text-2xl font-bold text-gray-900 mb-2">Quiz Complete!</h3>
              <p className="text-4xl font-bold text-blue-600 mb-2">{result.percentage}%</p>
              <p className="text-gray-500">{result.score} / {result.maxScore} marks</p>
              <Button className="mt-6" onClick={onClose}>Close</Button>
            </div>
          )}

          {!loading && !result && quiz && (
            <div className="space-y-6">
              {(quiz.questions as QuizQuestion[]).map((q, i) => (
                <div key={q.questionId} className="border border-gray-200 rounded-lg p-4">
                  <p className="font-medium text-gray-900 mb-3">
                    <span className="text-gray-400 mr-2">Q{i + 1}.</span>{q.text}
                    <span className="text-xs text-gray-400 ml-2">({q.marks} mark{q.marks !== 1 ? "s" : ""})</span>
                  </p>
                  {q.type !== "ShortAnswer" ? (
                    <div className="space-y-2">
                      {q.options.map((opt) => (
                        <label key={opt.optionId} className={`flex items-center gap-3 p-3 rounded-md cursor-pointer transition-colors ${answers[q.questionId] === opt.optionId ? "bg-blue-50 border border-blue-300" : "bg-gray-50 hover:bg-gray-100"}`}>
                          <input type="radio" name={q.questionId} value={opt.optionId}
                            checked={answers[q.questionId] === opt.optionId}
                            onChange={() => setAnswers(a => ({ ...a, [q.questionId]: opt.optionId }))}
                            className="text-blue-600" />
                          <span className="text-sm text-gray-700">{opt.text}</span>
                        </label>
                      ))}
                    </div>
                  ) : (
                    <textarea placeholder="Your answer…" rows={3}
                      value={answers[q.questionId] ?? ""}
                      onChange={e => setAnswers(a => ({ ...a, [q.questionId]: e.target.value }))}
                      className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
                  )}
                </div>
              ))}

              <Button onClick={submit} loading={submitting} className="w-full">
                Submit Quiz
              </Button>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
