"use client";
import { useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { api, type Assignment, type Submission } from "@/lib/api";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { useIdentity, usePermission } from "@/lib/auth-context";
import { Inbox } from "lucide-react";

const STATUS_COLORS: Record<string, string> = {
  submitted: "bg-warning-100 text-warning-700",
  graded:    "bg-success-100 text-success-700",
  late:      "bg-danger-100 text-danger-700",
};

export default function AssignmentDetailPage() {
  const { id }  = useParams<{ id: string }>();
  const router  = useRouter();
  const [assignment,   setAssignment]   = useState<Assignment | null>(null);
  const [submission,   setSubmission]   = useState<Submission | null>(null);
  const [submissions,  setSubmissions]  = useState<Submission[]>([]);
  const [content,      setContent]      = useState("");
  const [submitting,   setSubmitting]   = useState(false);
  const [grading,      setGrading]      = useState<string | null>(null);
  const [aiSuggestion, setAiSuggestion] = useState<{ suggestedScore: number; feedback: string } | null>(null);
  const [gradeForm,    setGradeForm]    = useState({ score: "", feedback: "" });
  const [loading,      setLoading]      = useState(true);
  const [error,        setError]        = useState("");
  const identity = useIdentity(); // Step 8
  const canGrade = usePermission("marks.capture"); // Step 8

  useEffect(() => {
    if (!identity) return;
    Promise.allSettled([api.assignments.get(id)]).then(([asgn]) => {
      if (asgn.status === "fulfilled") setAssignment(asgn.value as Assignment);
      if (identity === "Learner") {
        api.submissions.mySubmission(id)
          .then(s => setSubmission(s as Submission | null))
          .catch(() => {});
      } else {
        api.submissions.list(id)
          .then(s => setSubmissions(s as Submission[]))
          .catch(() => {});
      }
    }).catch(e => setError(String(e)))
      .finally(() => setLoading(false));
  }, [id, identity]);

  async function handleSubmit() {
    if (!content.trim()) return;
    setSubmitting(true);
    try {
      await api.submissions.submit(id, { content });
      const s = await api.submissions.mySubmission(id) as Submission;
      setSubmission(s); setContent("");
    } catch (e) { setError(String(e)); }
    finally { setSubmitting(false); }
  }

  async function loadAiSuggestion(submissionId: string) {
    try {
      const s = await api.submissions.aiGrade(submissionId) as { suggestedScore: number; feedback: string };
      setAiSuggestion(s);
      setGradeForm({ score: String(s.suggestedScore), feedback: s.feedback });
    } catch { setAiSuggestion(null); }
  }

  async function submitGrade(submissionId: string) {
    const score = parseFloat(gradeForm.score);
    if (isNaN(score)) return;
    try {
      await api.submissions.grade(submissionId, { score, feedback: gradeForm.feedback });
      const updated = await api.submissions.list(id) as Submission[];
      setSubmissions(updated);
      setGrading(null); setAiSuggestion(null);
    } catch (e) { setError(String(e)); }
  }

  if (loading) return (
    <div className="p-6 lg:p-8 max-w-4xl mx-auto space-y-6">
      <Skeleton className="h-4 w-24" />
      <Skeleton className="h-10 w-80" />
      <Skeleton className="h-4 w-48" />
      <Skeleton className="h-40 rounded-xl" />
    </div>
  );
  if (error) return <div className="p-6 lg:p-8 text-danger-700">{error}</div>;
  if (!assignment) return <div className="p-6 lg:p-8 text-text-muted">Assignment not found</div>;

  const isPastDue = new Date(assignment.dueAt) < new Date();

  return (
    <div className="p-6 lg:p-8 space-y-6 max-w-4xl mx-auto">
      {/* Back */}
      <button onClick={() => router.back()} className="flex items-center gap-1.5 text-sm text-text-muted hover:text-text-primary transition-colors">
        <svg className="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
        </svg>
        Back to Assignments
      </button>

      {/* Header */}
      <div>
        <div className="flex items-center gap-2 text-sm text-text-muted mb-1">
          {assignment.className && <span>{assignment.className}</span>}
          {assignment.className && assignment.subjectName && <span>·</span>}
          {assignment.subjectName && <span>{assignment.subjectName}</span>}
        </div>
        <h1 className="text-2xl font-semibold text-text-primary tracking-tight">{assignment.title}</h1>
        <div className="flex flex-wrap items-center gap-3 mt-2">
          <span className="text-sm text-text-secondary">
            Due:{" "}
            <span className={`font-medium ${isPastDue ? "text-danger-700" : "text-text-primary"}`}>
              {new Date(assignment.dueAt).toLocaleDateString("en-US", { weekday: "short", month: "short", day: "numeric", hour: "2-digit", minute: "2-digit" })}
            </span>
          </span>
          <span className="text-sm text-text-secondary">
            Max marks: <span className="font-semibold text-text-primary">{assignment.maxMarks}</span>
          </span>
          {isPastDue && <Badge variant="destructive">Past Due</Badge>}
        </div>
      </div>

      {/* Instructions */}
      {assignment.description && (
        <Card>
          <CardHeader><CardTitle className="text-base">Instructions</CardTitle></CardHeader>
          <CardContent>
            <p className="text-text-primary whitespace-pre-wrap text-sm leading-relaxed">{assignment.description}</p>
          </CardContent>
        </Card>
      )}

      {/* Student: submission */}
      {identity === "Learner" && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base flex items-center gap-2">
              Your Submission
              {submission && (
                <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${STATUS_COLORS[submission.status ?? "submitted"] ?? STATUS_COLORS.submitted}`}>
                  {submission.status ?? "submitted"}
                </span>
              )}
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            {submission ? (
              <>
                <div className="rounded-lg bg-surface-subtle border border-border p-4 text-sm text-text-primary whitespace-pre-wrap min-h-[80px]">
                  {submission.comments ?? submission.content ?? "(no content)"}
                </div>
                <p className="text-xs text-text-muted">
                  Submitted {new Date(submission.submittedAt).toLocaleString()}
                </p>
                {submission.grade && (
                  <div className="rounded-lg bg-success-100 border border-success-500/20 p-4">
                    <div className="flex items-center justify-between">
                      <p className="font-semibold text-success-700">
                        {submission.grade.score} / {assignment.maxMarks}
                      </p>
                      <span className="text-lg font-bold text-success-700">
                        {Math.round((submission.grade.score / assignment.maxMarks) * 100)}%
                      </span>
                    </div>
                    {submission.grade.feedback && (
                      <p className="text-sm text-success-700 mt-2 pt-2 border-t border-success-500/20">{submission.grade.feedback}</p>
                    )}
                  </div>
                )}
              </>
            ) : (
              <>
                <textarea value={content} onChange={e => setContent(e.target.value)}
                  placeholder="Write your answer here…" rows={8}
                  disabled={submitting || isPastDue}
                  className="w-full rounded-lg border border-border p-3 text-sm resize-none focus:outline-none focus:ring-2 focus:ring-primary disabled:bg-surface-subtle disabled:text-text-muted" />
                {isPastDue && (
                  <p className="text-sm text-danger-700">The due date has passed. You can no longer submit.</p>
                )}
                <Button onClick={handleSubmit} disabled={!content.trim() || isPastDue} loading={submitting}>
                  Submit Assignment
                </Button>
              </>
            )}
          </CardContent>
        </Card>
      )}

      {/* Staff with mark-capture permission: submissions list */}
      {canGrade && (
        <Card>
          <CardHeader>
            <div className="flex items-center justify-between">
              <CardTitle className="text-base">
                Submissions
                <span className="ml-2 inline-flex items-center rounded-full bg-surface-subtle px-2.5 py-0.5 text-xs font-medium text-text-secondary">
                  {submissions.length}
                </span>
              </CardTitle>
              <p className="text-xs text-text-muted">
                {submissions.filter(s => s.grade).length} graded · {submissions.filter(s => !s.grade).length} pending
              </p>
            </div>
          </CardHeader>
          <CardContent className="p-0">
            {submissions.length === 0 ? (
              <div className="py-16 text-center text-text-muted">
                <div className="flex justify-center mb-3">
                  <Inbox className="h-10 w-10 text-text-muted" />
                </div>
                <p className="text-sm font-medium text-text-secondary">No submissions yet</p>
                <p className="text-xs text-text-muted mt-1">Students haven't submitted this assignment</p>
              </div>
            ) : (
              <table className="w-full text-sm">
                <thead className="border-b bg-surface-subtle">
                  <tr>
                    {["Student", "Submitted", "Status", "Score", "Actions"].map(h => (
                      <th key={h} className="px-4 py-3 text-left text-xs font-semibold text-text-muted uppercase tracking-wider">{h}</th>
                    ))}
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {submissions.map(s => (
                    <>
                      <tr key={s.submissionId} className="hover:bg-surface-subtle">
                        <td className="px-4 py-3">
                          <div className="flex items-center gap-2">
                            <div className="h-7 w-7 rounded-full bg-primary-100 text-primary-700 text-xs font-bold flex items-center justify-center shrink-0">
                              {s.studentName.split(" ").map(n => n[0]).join("").slice(0, 2)}
                            </div>
                            <span className="font-medium text-text-primary">{s.studentName}</span>
                          </div>
                        </td>
                        <td className="px-4 py-3 text-text-secondary text-xs">
                          {new Date(s.submittedAt).toLocaleString("en-US", { month: "short", day: "numeric", hour: "2-digit", minute: "2-digit" })}
                        </td>
                        <td className="px-4 py-3">
                          <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${s.grade ? STATUS_COLORS.graded : STATUS_COLORS.submitted}`}>
                            {s.grade ? "Graded" : "Submitted"}
                          </span>
                        </td>
                        <td className="px-4 py-3 font-semibold">
                          {s.grade ? (
                            <span className="text-text-primary">{s.grade.score}<span className="text-text-muted font-normal">/{assignment.maxMarks}</span></span>
                          ) : <span className="text-text-muted">—</span>}
                        </td>
                        <td className="px-4 py-3">
                          <button onClick={() => setGrading(grading === s.submissionId ? null : s.submissionId)}
                            className="text-xs font-medium text-primary hover:text-primary-800 hover:underline">
                            {grading === s.submissionId ? "Close" : s.grade ? "Re-grade" : "Grade"}
                          </button>
                        </td>
                      </tr>

                      {grading === s.submissionId && (
                        <tr key={`grade-${s.submissionId}`}>
                          <td colSpan={5} className="bg-primary-50 px-6 py-5 border-b border-primary-100">
                            <div className="max-w-2xl space-y-4">
                              <div>
                                <p className="text-xs font-semibold text-text-secondary uppercase tracking-wider mb-2">Submission</p>
                                <div className="rounded-lg bg-surface-card border border-border p-4 text-sm text-text-primary whitespace-pre-wrap max-h-48 overflow-y-auto">
                                  {s.comments ?? s.content ?? "(no content)"}
                                </div>
                              </div>

                              <div className="flex flex-wrap gap-3 items-end">
                                <div className="space-y-1">
                                  <label className="text-xs font-medium text-text-secondary">Score / {assignment.maxMarks}</label>
                                  <input type="number" min={0} max={assignment.maxMarks}
                                    value={gradeForm.score}
                                    onChange={e => setGradeForm(f => ({ ...f, score: e.target.value }))}
                                    placeholder="0"
                                    className="w-28 rounded-md border border-border px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary" />
                                </div>
                                <Button size="sm" variant="outline" onClick={() => loadAiSuggestion(s.submissionId)} className="gap-1.5">
                                  ✨ AI Suggest
                                </Button>
                              </div>

                              {aiSuggestion && (
                                <div className="rounded-lg bg-primary-50 border border-primary-200 p-3">
                                  <p className="text-sm font-semibold text-primary-800">
                                    AI suggests: {aiSuggestion.suggestedScore}/{assignment.maxMarks}
                                  </p>
                                  <p className="text-xs text-primary mt-1">{aiSuggestion.feedback}</p>
                                </div>
                              )}

                              <div className="space-y-1">
                                <label className="text-xs font-medium text-text-secondary">Feedback for student</label>
                                <textarea value={gradeForm.feedback}
                                  onChange={e => setGradeForm(f => ({ ...f, feedback: e.target.value }))}
                                  placeholder="Optional feedback…" rows={3}
                                  className="w-full rounded-md border border-border px-3 py-2 text-sm resize-none focus:outline-none focus:ring-2 focus:ring-primary" />
                              </div>

                              <div className="flex gap-2">
                                <Button size="sm" onClick={() => submitGrade(s.submissionId)}>Save Grade</Button>
                                <Button size="sm" variant="outline" onClick={() => { setGrading(null); setAiSuggestion(null); }}>Cancel</Button>
                              </div>
                            </div>
                          </td>
                        </tr>
                      )}
                    </>
                  ))}
                </tbody>
              </table>
            )}
          </CardContent>
        </Card>
      )}
    </div>
  );
}
