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
  submitted: "bg-yellow-100 text-yellow-800",
  graded:    "bg-green-100 text-green-800",
  late:      "bg-red-100 text-red-800",
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
  if (error) return <div className="p-6 lg:p-8 text-red-600">{error}</div>;
  if (!assignment) return <div className="p-6 lg:p-8 text-gray-400">Assignment not found</div>;

  const isPastDue = new Date(assignment.dueAt) < new Date();

  return (
    <div className="p-6 lg:p-8 space-y-6 max-w-4xl mx-auto">
      {/* Back */}
      <button onClick={() => router.back()} className="flex items-center gap-1.5 text-sm text-gray-400 hover:text-gray-700 transition-colors">
        <svg className="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
        </svg>
        Back to Assignments
      </button>

      {/* Header */}
      <div>
        <div className="flex items-center gap-2 text-sm text-gray-400 mb-1">
          {assignment.className && <span>{assignment.className}</span>}
          {assignment.className && assignment.subjectName && <span>·</span>}
          {assignment.subjectName && <span>{assignment.subjectName}</span>}
        </div>
        <h1 className="text-2xl font-semibold text-gray-900 tracking-tight">{assignment.title}</h1>
        <div className="flex flex-wrap items-center gap-3 mt-2">
          <span className="text-sm text-gray-500">
            Due:{" "}
            <span className={`font-medium ${isPastDue ? "text-red-600" : "text-gray-700"}`}>
              {new Date(assignment.dueAt).toLocaleDateString("en-US", { weekday: "short", month: "short", day: "numeric", hour: "2-digit", minute: "2-digit" })}
            </span>
          </span>
          <span className="text-sm text-gray-500">
            Max marks: <span className="font-semibold text-gray-800">{assignment.maxMarks}</span>
          </span>
          {isPastDue && <Badge variant="destructive">Past Due</Badge>}
        </div>
      </div>

      {/* Instructions */}
      {assignment.description && (
        <Card>
          <CardHeader><CardTitle className="text-base">Instructions</CardTitle></CardHeader>
          <CardContent>
            <p className="text-gray-700 whitespace-pre-wrap text-sm leading-relaxed">{assignment.description}</p>
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
                <div className="rounded-lg bg-gray-50 border border-gray-200 p-4 text-sm text-gray-800 whitespace-pre-wrap min-h-[80px]">
                  {submission.comments ?? submission.content ?? "(no content)"}
                </div>
                <p className="text-xs text-gray-400">
                  Submitted {new Date(submission.submittedAt).toLocaleString()}
                </p>
                {submission.grade && (
                  <div className="rounded-lg bg-green-50 border border-green-200 p-4">
                    <div className="flex items-center justify-between">
                      <p className="font-semibold text-green-800">
                        {submission.grade.score} / {assignment.maxMarks}
                      </p>
                      <span className="text-lg font-bold text-green-700">
                        {Math.round((submission.grade.score / assignment.maxMarks) * 100)}%
                      </span>
                    </div>
                    {submission.grade.feedback && (
                      <p className="text-sm text-green-700 mt-2 pt-2 border-t border-green-200">{submission.grade.feedback}</p>
                    )}
                  </div>
                )}
              </>
            ) : (
              <>
                <textarea value={content} onChange={e => setContent(e.target.value)}
                  placeholder="Write your answer here…" rows={8}
                  disabled={submitting || isPastDue}
                  className="w-full rounded-lg border border-gray-200 p-3 text-sm resize-none focus:outline-none focus:ring-2 focus:ring-blue-400 disabled:bg-gray-50 disabled:text-gray-400" />
                {isPastDue && (
                  <p className="text-sm text-red-500">The due date has passed. You can no longer submit.</p>
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
                <span className="ml-2 inline-flex items-center rounded-full bg-gray-100 px-2.5 py-0.5 text-xs font-medium text-gray-600">
                  {submissions.length}
                </span>
              </CardTitle>
              <p className="text-xs text-gray-400">
                {submissions.filter(s => s.grade).length} graded · {submissions.filter(s => !s.grade).length} pending
              </p>
            </div>
          </CardHeader>
          <CardContent className="p-0">
            {submissions.length === 0 ? (
              <div className="py-16 text-center text-gray-400">
                <div className="flex justify-center mb-3">
                  <Inbox className="h-10 w-10 text-gray-300" />
                </div>
                <p className="text-sm font-medium text-gray-500">No submissions yet</p>
                <p className="text-xs text-gray-400 mt-1">Students haven't submitted this assignment</p>
              </div>
            ) : (
              <table className="w-full text-sm">
                <thead className="border-b bg-gray-50">
                  <tr>
                    {["Student", "Submitted", "Status", "Score", "Actions"].map(h => (
                      <th key={h} className="px-4 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wider">{h}</th>
                    ))}
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {submissions.map(s => (
                    <>
                      <tr key={s.submissionId} className="hover:bg-gray-50">
                        <td className="px-4 py-3">
                          <div className="flex items-center gap-2">
                            <div className="h-7 w-7 rounded-full bg-blue-100 text-blue-700 text-xs font-bold flex items-center justify-center shrink-0">
                              {s.studentName.split(" ").map(n => n[0]).join("").slice(0, 2)}
                            </div>
                            <span className="font-medium text-gray-900">{s.studentName}</span>
                          </div>
                        </td>
                        <td className="px-4 py-3 text-gray-500 text-xs">
                          {new Date(s.submittedAt).toLocaleString("en-US", { month: "short", day: "numeric", hour: "2-digit", minute: "2-digit" })}
                        </td>
                        <td className="px-4 py-3">
                          <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${s.grade ? STATUS_COLORS.graded : STATUS_COLORS.submitted}`}>
                            {s.grade ? "Graded" : "Submitted"}
                          </span>
                        </td>
                        <td className="px-4 py-3 font-semibold">
                          {s.grade ? (
                            <span className="text-gray-900">{s.grade.score}<span className="text-gray-400 font-normal">/{assignment.maxMarks}</span></span>
                          ) : <span className="text-gray-300">—</span>}
                        </td>
                        <td className="px-4 py-3">
                          <button onClick={() => setGrading(grading === s.submissionId ? null : s.submissionId)}
                            className="text-xs font-medium text-blue-600 hover:text-blue-800 hover:underline">
                            {grading === s.submissionId ? "Close" : s.grade ? "Re-grade" : "Grade"}
                          </button>
                        </td>
                      </tr>

                      {grading === s.submissionId && (
                        <tr key={`grade-${s.submissionId}`}>
                          <td colSpan={5} className="bg-blue-50 px-6 py-5 border-b border-blue-100">
                            <div className="max-w-2xl space-y-4">
                              <div>
                                <p className="text-xs font-semibold text-gray-500 uppercase tracking-wider mb-2">Submission</p>
                                <div className="rounded-lg bg-white border border-gray-200 p-4 text-sm text-gray-800 whitespace-pre-wrap max-h-48 overflow-y-auto">
                                  {s.comments ?? s.content ?? "(no content)"}
                                </div>
                              </div>

                              <div className="flex flex-wrap gap-3 items-end">
                                <div className="space-y-1">
                                  <label className="text-xs font-medium text-gray-600">Score / {assignment.maxMarks}</label>
                                  <input type="number" min={0} max={assignment.maxMarks}
                                    value={gradeForm.score}
                                    onChange={e => setGradeForm(f => ({ ...f, score: e.target.value }))}
                                    placeholder="0"
                                    className="w-28 rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
                                </div>
                                <Button size="sm" variant="outline" onClick={() => loadAiSuggestion(s.submissionId)} className="gap-1.5">
                                  ✨ AI Suggest
                                </Button>
                              </div>

                              {aiSuggestion && (
                                <div className="rounded-lg bg-purple-50 border border-purple-200 p-3">
                                  <p className="text-sm font-semibold text-purple-800">
                                    AI suggests: {aiSuggestion.suggestedScore}/{assignment.maxMarks}
                                  </p>
                                  <p className="text-xs text-purple-600 mt-1">{aiSuggestion.feedback}</p>
                                </div>
                              )}

                              <div className="space-y-1">
                                <label className="text-xs font-medium text-gray-600">Feedback for student</label>
                                <textarea value={gradeForm.feedback}
                                  onChange={e => setGradeForm(f => ({ ...f, feedback: e.target.value }))}
                                  placeholder="Optional feedback…" rows={3}
                                  className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm resize-none focus:outline-none focus:ring-2 focus:ring-blue-500" />
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
