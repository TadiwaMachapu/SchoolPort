"use client";
import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import { api, Assignment, Submission } from "@/lib/api";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";

export default function AssignmentDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [assignment, setAssignment] = useState<Assignment | null>(null);
  const [submission, setSubmission] = useState<Submission | null>(null);
  const [submissions, setSubmissions] = useState<Submission[]>([]);
  const [content, setContent] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [grading, setGrading] = useState<string | null>(null);
  const [aiSuggestion, setAiSuggestion] = useState<{ suggestedScore: number; feedback: string } | null>(null);
  const [gradeForm, setGradeForm] = useState<{ score: string; feedback: string }>({ score: "", feedback: "" });
  const [loading, setLoading] = useState(true);
  const [role, setRole] = useState<string | null>(null);
  const [error, setError] = useState("");

  useEffect(() => {
    Promise.allSettled([
      api.assignments.get(id),
      api.me.get(),
    ]).then(([asgn, me]) => {
      if (asgn.status === "fulfilled") setAssignment(asgn.value as Assignment);
      if (me.status === "fulfilled") {
        const r = (me.value as { user: { role: string } }).user.role;
        setRole(r);
        if (r === "Student") {
          api.submissions.mySubmission(id)
            .then(s => setSubmission(s as Submission | null))
            .catch(() => {});
        } else {
          api.submissions.list(id)
            .then(s => setSubmissions(s as Submission[]))
            .catch(() => {});
        }
      }
    }).catch(e => setError(String(e)))
      .finally(() => setLoading(false));
  }, [id]);

  const handleSubmit = async () => {
    if (!content.trim()) return;
    setSubmitting(true);
    try {
      await api.submissions.submit(id, { content });
      const s = await api.submissions.mySubmission(id) as Submission;
      setSubmission(s);
      setContent("");
    } catch (e) {
      setError(String(e));
    } finally {
      setSubmitting(false);
    }
  };

  const loadAiSuggestion = async (submissionId: string) => {
    try {
      const s = await api.submissions.aiGrade(submissionId) as { suggestedScore: number; feedback: string };
      setAiSuggestion(s);
      setGradeForm({ score: String(s.suggestedScore), feedback: s.feedback });
    } catch {
      setAiSuggestion(null);
    }
  };

  const submitGrade = async (submissionId: string) => {
    const score = parseFloat(gradeForm.score);
    if (isNaN(score)) return;
    try {
      const updated = await api.submissions.grade(submissionId, { score, feedback: gradeForm.feedback }) as Submission;
      setSubmissions(prev => prev.map(s => s.submissionId === submissionId ? updated : s));
      setGrading(null);
      setAiSuggestion(null);
    } catch (e) {
      setError(String(e));
    }
  };

  const isPastDue = assignment ? new Date(assignment.dueAt) < new Date() : false;
  const statusColor = (status: string) => ({
    submitted: "bg-yellow-100 text-yellow-800",
    graded: "bg-green-100 text-green-800",
    late: "bg-red-100 text-red-800",
  }[status] ?? "bg-gray-100 text-gray-700");

  if (loading) return <div className="p-8 text-gray-400 text-center py-16">Loading…</div>;
  if (error) return <div className="p-8 text-red-600">{error}</div>;
  if (!assignment) return <div className="p-8 text-gray-400">Assignment not found</div>;

  return (
    <div className="p-8 space-y-6 max-w-4xl mx-auto">
      <div>
        <div className="flex items-center gap-2 text-sm text-gray-400 mb-1">
          <span>{assignment.className}</span>
          <span>·</span>
          <span>{assignment.subjectName}</span>
        </div>
        <h1 className="text-3xl font-bold text-gray-900">{assignment.title}</h1>
        <div className="flex items-center gap-3 mt-2">
          <span className="text-sm text-gray-500">
            Due: <span className={`font-medium ${isPastDue ? "text-red-600" : "text-gray-700"}`}>
              {new Date(assignment.dueAt).toLocaleDateString("en-US", { weekday: "short", month: "short", day: "numeric", hour: "2-digit", minute: "2-digit" })}
            </span>
          </span>
          <span className="text-sm text-gray-500">Max marks: <span className="font-semibold text-gray-800">{assignment.maxMarks}</span></span>
          {isPastDue && <Badge variant="destructive">Past Due</Badge>}
        </div>
      </div>

      {assignment.description && (
        <Card>
          <CardHeader><CardTitle className="text-base">Instructions</CardTitle></CardHeader>
          <CardContent>
            <p className="text-gray-700 whitespace-pre-wrap text-sm leading-relaxed">{assignment.description}</p>
          </CardContent>
        </Card>
      )}

      {/* Student: submission form */}
      {role === "Student" && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base flex items-center gap-2">
              Your Submission
              {submission && <Badge className={statusColor(submission.status)}>{submission.status}</Badge>}
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            {submission ? (
              <>
                <div className="bg-gray-50 rounded-lg p-4 text-sm text-gray-800 whitespace-pre-wrap">
                  {submission.comments ?? submission.content ?? "(no content)"}
                </div>
                <p className="text-xs text-gray-400">
                  Submitted {new Date(submission.submittedAt).toLocaleString()}
                </p>
                {submission.grade && (
                  <div className="bg-green-50 border border-green-200 rounded-lg p-4">
                    <p className="font-semibold text-green-800">
                      Score: {submission.grade.score} / {assignment.maxMarks}
                      {" "}({Math.round((submission.grade.score / assignment.maxMarks) * 100)}%)
                    </p>
                    {submission.grade.feedback && (
                      <p className="text-sm text-green-700 mt-2">{submission.grade.feedback}</p>
                    )}
                  </div>
                )}
              </>
            ) : (
              <>
                <textarea
                  value={content}
                  onChange={e => setContent(e.target.value)}
                  placeholder="Write your answer here…"
                  rows={8}
                  disabled={submitting || isPastDue}
                  className="w-full border border-gray-200 rounded-lg p-3 text-sm resize-none focus:outline-none focus:ring-2 focus:ring-blue-400 disabled:bg-gray-50 disabled:text-gray-400"
                />
                {isPastDue && (
                  <p className="text-sm text-red-500">The due date has passed. You can no longer submit.</p>
                )}
                <Button
                  onClick={handleSubmit}
                  disabled={submitting || !content.trim() || isPastDue}
                >
                  {submitting ? "Submitting…" : "Submit Assignment"}
                </Button>
              </>
            )}
          </CardContent>
        </Card>
      )}

      {/* Teacher / Admin: submissions list */}
      {(role === "Teacher" || role === "Admin") && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base flex items-center gap-2">
              Submissions
              <Badge variant="outline">{submissions.length}</Badge>
            </CardTitle>
          </CardHeader>
          <CardContent className="p-0">
            {submissions.length === 0 ? (
              <div className="py-12 text-center text-gray-400 text-sm">No submissions yet</div>
            ) : (
              <table className="w-full text-sm">
                <thead className="border-b bg-gray-50">
                  <tr>
                    {["Student", "Submitted", "Status", "Score", "Actions"].map(h => (
                      <th key={h} className="px-4 py-3 text-left font-medium text-gray-500 text-xs">{h}</th>
                    ))}
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {submissions.map(s => (
                    <>
                      <tr key={s.submissionId} className="hover:bg-gray-50">
                        <td className="px-4 py-3 font-medium text-gray-900">{s.studentName}</td>
                        <td className="px-4 py-3 text-gray-500 text-xs">
                          {new Date(s.submittedAt).toLocaleString()}
                        </td>
                        <td className="px-4 py-3">
                          <Badge className={`text-xs ${statusColor(s.grade ? "graded" : "submitted")}`}>
                            {s.grade ? "graded" : "submitted"}
                          </Badge>
                        </td>
                        <td className="px-4 py-3 font-semibold">
                          {s.grade
                            ? `${s.grade.score}/${assignment.maxMarks}`
                            : <span className="text-gray-400">—</span>}
                        </td>
                        <td className="px-4 py-3">
                          <Button
                            size="sm"
                            variant="outline"
                            onClick={() => setGrading(grading === s.submissionId ? null : s.submissionId)}
                          >
                            {grading === s.submissionId ? "Close" : "Grade"}
                          </Button>
                        </td>
                      </tr>
                      {grading === s.submissionId && (
                        <tr key={`grade-${s.submissionId}`}>
                          <td colSpan={5} className="bg-blue-50 px-4 py-4">
                            <div className="space-y-3 max-w-xl">
                              <div className="bg-white border rounded p-3 text-sm text-gray-800 whitespace-pre-wrap max-h-40 overflow-y-auto">
                                {s.comments ?? s.content ?? "(no content)"}
                              </div>
                              <div className="flex gap-2 items-center">
                                <input
                                  type="number"
                                  min={0}
                                  max={assignment.maxMarks}
                                  value={gradeForm.score}
                                  onChange={e => setGradeForm(f => ({ ...f, score: e.target.value }))}
                                  placeholder={`Score / ${assignment.maxMarks}`}
                                  className="border rounded px-3 py-1.5 text-sm w-32 focus:outline-none focus:ring-2 focus:ring-blue-400"
                                />
                                <Button
                                  size="sm"
                                  variant="outline"
                                  onClick={() => loadAiSuggestion(s.submissionId)}
                                  className="gap-1"
                                >
                                  ✨ AI Grade
                                </Button>
                              </div>
                              {aiSuggestion && (
                                <div className="bg-purple-50 border border-purple-200 rounded p-3 text-sm">
                                  <p className="font-medium text-purple-800">AI Suggestion: {aiSuggestion.suggestedScore}/{assignment.maxMarks}</p>
                                  <p className="text-purple-700 text-xs mt-1">{aiSuggestion.feedback}</p>
                                </div>
                              )}
                              <textarea
                                value={gradeForm.feedback}
                                onChange={e => setGradeForm(f => ({ ...f, feedback: e.target.value }))}
                                placeholder="Feedback for student…"
                                rows={3}
                                className="w-full border rounded px-3 py-2 text-sm resize-none focus:outline-none focus:ring-2 focus:ring-blue-400"
                              />
                              <Button size="sm" onClick={() => submitGrade(s.submissionId)}>
                                Save Grade
                              </Button>
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
