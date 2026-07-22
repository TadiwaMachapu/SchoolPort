"use client";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { api, type MatricDashboard, type MatricStudentResult, type MatricPastPaper, type MatricQuizQuestion, type Class } from "@/lib/api";
import { useFeature } from "@/lib/use-feature";
import { useIdentity, usePermission, usePosition } from "@/lib/auth-context";
import RiskDashboardTab from "@/components/matric/RiskDashboardTab";
import GradeOverviewTab from "@/components/matric/GradeOverviewTab";
import MatricTutorCard from "@/components/matric/MatricTutorCard";
import StudyPlannerTab from "@/components/matric/StudyPlannerTab";
import NscRequirementsTab from "@/components/matric/NscRequirementsTab";
import { Award, Loader2, AlertTriangle, CheckCircle2, AlertCircle, XCircle, BarChart2, FileText, Brain, Sparkles, ExternalLink, CalendarClock, GraduationCap } from "lucide-react";

const STATUS_CONFIG = {
  Pass:   { label: "Pass",    colour: "text-emerald-600", bg: "bg-emerald-50 border-emerald-200", icon: CheckCircle2 },
  AtRisk: { label: "At Risk", colour: "text-amber-600",   bg: "bg-amber-50 border-amber-200",     icon: AlertCircle  },
  Fail:   { label: "Fail",    colour: "text-red-600",     bg: "bg-red-50 border-red-200",         icon: XCircle      },
  NoData: { label: "No Data", colour: "text-gray-400",    bg: "bg-gray-50 border-gray-200",       icon: Award        },
};

function OverallBadge({ status }: { status: string }) {
  const cfg = STATUS_CONFIG[status as keyof typeof STATUS_CONFIG] ?? STATUS_CONFIG.NoData;
  const Icon = cfg.icon;
  return (
    <span className={`inline-flex items-center gap-1 rounded-full px-2.5 py-0.5 text-xs font-semibold border ${cfg.bg} ${cfg.colour}`}>
      <Icon className="h-3 w-3" /> {cfg.label}
    </span>
  );
}

function SubjectStatusDot({ status }: { status: string }) {
  const colours: Record<string, string> = {
    Pass:   "bg-emerald-400",
    AtRisk: "bg-amber-400",
    Fail:   "bg-red-500",
  };
  return <span className={`inline-block h-2.5 w-2.5 rounded-full ${colours[status] ?? "bg-gray-300"}`} />;
}

// ─── Staff view ───────────────────────────────────────────────────────────────

function StaffDashboard() {
  const [classes,  setClasses]  = useState<Class[]>([]);
  const [classId,  setClassId]  = useState("");
  const [data,     setData]     = useState<MatricDashboard | null>(null);
  const [expanded, setExpanded] = useState<string | null>(null);
  const [loading,  setLoading]  = useState(false);
  const [error,    setError]    = useState("");

  useEffect(() => {
    // Load Gr 12 classes only (gradeLevel=12)
    api.classes.list({ pageSize: 100 }).then(r => {
      const gr12 = r.items.filter(c => c.gradeLevel === 12);
      setClasses(gr12);
      if (gr12.length) setClassId(gr12[0].classId);
    }).catch(() => {});
  }, []);

  useEffect(() => {
    if (!classId) { setData(null); return; }
    setLoading(true); setError(""); setData(null); setExpanded(null);
    api.matric.dashboard(classId).then(setData).catch(e => {
      setError(e instanceof Error ? e.message : "Failed to load");
    }).finally(() => setLoading(false));
  }, [classId]);

  const summary = data ? {
    pass:   data.learners.filter(l => l.overallStatus === "Pass").length,
    atRisk: data.learners.filter(l => l.overallStatus === "AtRisk").length,
    fail:   data.learners.filter(l => l.overallStatus === "Fail").length,
  } : null;

  return (
    <div className="space-y-5">
      {/* Class selector */}
      <div className="flex items-end gap-3 flex-wrap">
        <div className="space-y-1">
          <label className="text-xs font-medium text-text-secondary">Grade 12 Class</label>
          {classes.length === 0 ? (
            <p className="text-sm text-text-muted py-2">No Grade 12 classes found. Set GradeLevel = 12 on a class to use Matric Hub.</p>
          ) : (
            <select value={classId} onChange={e => setClassId(e.target.value)}
              className="rounded-md border border-border px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary">
              {classes.map(c => <option key={c.classId} value={c.classId}>{c.name}</option>)}
            </select>
          )}
        </div>
      </div>

      {error && (
        <div className="flex items-center gap-2 rounded-lg bg-danger-100 px-4 py-3 text-sm text-danger-700">
          <AlertTriangle className="h-4 w-4 shrink-0" /> {error}
        </div>
      )}

      {loading && <div className="flex items-center justify-center py-20"><Loader2 className="h-8 w-8 animate-spin text-text-muted" /></div>}

      {data && !loading && (
        <>
          {/* Summary KPIs */}
          {summary && (
            <div className="grid grid-cols-3 gap-3">
              {[
                { label: "On Track",     value: summary.pass,   colour: "text-emerald-600 bg-emerald-50 border-emerald-200" },
                { label: "At Risk",      value: summary.atRisk, colour: "text-amber-600 bg-amber-50 border-amber-200"       },
                { label: "Failing",      value: summary.fail,   colour: "text-red-600 bg-red-50 border-red-200"             },
              ].map(k => (
                <div key={k.label} className={`rounded-xl border px-4 py-3 text-center ${k.colour}`}>
                  <p className="text-2xl font-bold">{k.value}</p>
                  <p className="text-xs font-medium mt-0.5">{k.label}</p>
                </div>
              ))}
            </div>
          )}

          {data.learners.length === 0 ? (
            <div className="rounded-xl border-2 border-dashed border-border py-16 text-center">
              <p className="text-text-secondary">No learners with grade data in this class.</p>
            </div>
          ) : (
            <div className="space-y-2">
              {data.learners.map(l => (
                <div key={l.studentId} className="rounded-xl bg-surface-card border border-border shadow-sm overflow-hidden">
                  <button
                    onClick={() => setExpanded(e => e === l.studentId ? null : l.studentId)}
                    className="w-full flex items-center justify-between px-5 py-3 text-left hover:bg-surface-subtle transition-colors"
                  >
                    <div className="flex items-center gap-3">
                      <div>
                        <p className="font-semibold text-text-primary">{l.name}</p>
                        <p className="text-xs text-text-muted">{l.studentNumber}</p>
                      </div>
                      <OverallBadge status={l.overallStatus} />
                    </div>
                    <div className="flex items-center gap-4 text-right">
                      <div className="flex gap-1.5 items-center">
                        {l.subjects.slice(0, 7).map((s, i) => <SubjectStatusDot key={i} status={s.status} />)}
                      </div>
                      <span className="text-xs text-text-muted">
                        {l.passCount}P · {l.atRiskCount}AR · {l.failCount}F
                      </span>
                    </div>
                  </button>

                  {expanded === l.studentId && (
                    <div className="border-t border-border">
                      <table className="w-full text-sm">
                        <thead className="bg-surface-subtle">
                          <tr className="text-xs font-semibold text-text-muted uppercase tracking-wider">
                            <th className="px-5 py-2.5 text-left">Subject</th>
                            <th className="px-4 py-2.5 text-center">Average</th>
                            <th className="px-4 py-2.5 text-center">Status</th>
                          </tr>
                        </thead>
                        <tbody className="divide-y divide-border">
                          {l.subjects.map((s, i) => {
                            const cfg = STATUS_CONFIG[s.status] ?? STATUS_CONFIG.NoData;
                            return (
                              <tr key={i} className="hover:bg-surface-subtle">
                                <td className="px-5 py-2.5 font-medium text-text-primary">{s.subjectName}</td>
                                <td className="px-4 py-2.5 text-center font-semibold text-text-primary">{s.average}%</td>
                                <td className="px-4 py-2.5 text-center">
                                  <span className={`inline-flex items-center gap-1 text-xs font-semibold ${cfg.colour}`}>
                                    <SubjectStatusDot status={s.status} /> {cfg.label}
                                  </span>
                                </td>
                              </tr>
                            );
                          })}
                        </tbody>
                      </table>
                      <div className="px-5 py-2.5 bg-surface-subtle border-t border-border text-xs text-text-muted">
                        NSC thresholds: Pass ≥ 40% · At Risk 30–39% · Fail &lt; 30%
                      </div>
                    </div>
                  )}
                </div>
              ))}
            </div>
          )}
        </>
      )}
    </div>
  );
}

// ─── Student NSC status tab ───────────────────────────────────────────────────

function NscStatusTab({ data }: { data: MatricStudentResult }) {
  if (!data.isGrade12) {
    return (
      <div className="rounded-xl border-2 border-dashed border-border py-16 text-center">
        <Award className="h-10 w-10 text-text-muted mx-auto mb-3" />
        <p className="text-text-secondary">Matric Hub is available for Grade 12 learners only.</p>
      </div>
    );
  }

  const cfg = STATUS_CONFIG[data.overallStatus] ?? STATUS_CONFIG.NoData;
  const Icon = cfg.icon;

  return (
    <div className="space-y-5">
      <div className={`rounded-xl border px-5 py-4 flex items-center gap-4 ${cfg.bg} ${cfg.colour}`}>
        <Icon className="h-8 w-8 shrink-0" />
        <div>
          <p className="text-lg font-bold">{cfg.label}</p>
          <p className="text-sm opacity-80">
            {data.passCount} subject{data.passCount !== 1 ? "s" : ""} passing ·{" "}
            {data.atRiskCount} at risk · {data.failCount} failing
          </p>
        </div>
      </div>

      {data.subjects.length === 0 ? (
        <div className="rounded-xl border-2 border-dashed border-border py-12 text-center">
          <p className="text-sm text-text-muted">No graded assessments yet.</p>
        </div>
      ) : (
        <div className="rounded-xl bg-surface-card border border-border shadow-sm overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-surface-subtle border-b border-border">
              <tr className="text-xs font-semibold text-text-muted uppercase tracking-wider">
                <th className="px-5 py-3 text-left">Subject</th>
                <th className="px-4 py-3 text-center">Average</th>
                <th className="px-4 py-3 text-center">NSC Status</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {data.subjects.map((s, i) => {
                const scfg = STATUS_CONFIG[s.status] ?? STATUS_CONFIG.NoData;
                return (
                  <tr key={i} className="hover:bg-surface-subtle">
                    <td className="px-5 py-3 font-medium text-text-primary">{s.subjectName}</td>
                    <td className="px-4 py-3 text-center font-bold text-text-primary">{s.average}%</td>
                    <td className="px-4 py-3 text-center">
                      <span className={`inline-flex items-center gap-1 text-xs font-semibold ${scfg.colour}`}>
                        <SubjectStatusDot status={s.status} /> {scfg.label}
                      </span>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
          <div className="px-5 py-2.5 bg-surface-subtle border-t border-border text-xs text-text-muted">
            NSC thresholds: Pass ≥ 40% · At Risk 30–39% · Fail &lt; 30%
          </div>
        </div>
      )}
    </div>
  );
}

// ─── Past papers tab ──────────────────────────────────────────────────────────

function PastPapersTab({ subjects, enrolledSubjects = [] }: { subjects: string[]; enrolledSubjects?: string[] }) {
  // The learner's own subjects lead the dropdown; the rest of the catalogue follows.
  const enrolledSet = new Set(enrolledSubjects);
  const orderedSubjects = [
    ...subjects.filter(s => enrolledSet.has(s)),
    ...subjects.filter(s => !enrolledSet.has(s)),
  ];
  const [selected, setSelected] = useState(orderedSubjects[0] ?? "");
  const [yearFilter, setYearFilter] = useState("");
  const [typeFilter, setTypeFilter] = useState("");
  const [papers, setPapers] = useState<MatricPastPaper[]>([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!selected) return;
    setLoading(true);
    api.matric.pastPapers(selected)
      .then(setPapers)
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [selected]);

  // Client-side year + paper-type filters over the loaded subject's papers.
  const availableYears = [...new Set(papers.map(p => p.year))].sort((a, b) => b - a);
  const filtered = papers.filter(p =>
    (!yearFilter || p.year === Number(yearFilter)) &&
    (!typeFilter || p.paperType === typeFilter));

  // November (and other exam-sitting) papers group by year; exemplars render as their own
  // clearly-labelled group below — the UI label is "2014 NSC Exemplars", never bare "Exemplars".
  const examPapers = filtered.filter(p => p.paperType !== "Exemplar");
  const exemplars = filtered.filter(p => p.paperType === "Exemplar");
  const byYear = examPapers.reduce<Record<number, MatricPastPaper[]>>((acc, p) => {
    (acc[p.year] ??= []).push(p);
    return acc;
  }, {});

  const paperGroup = (title: string, group: MatricPastPaper[], key: string) => (
    <div key={key} className="rounded-xl bg-surface-card border border-border shadow-sm overflow-hidden">
      <div className="bg-surface-subtle border-b border-border px-4 py-2.5">
        <p className="text-sm font-semibold text-text-primary">{title}</p>
      </div>
      <div className="divide-y divide-border">
        {group.map(p => (
          <div key={p.matricPastPaperId} className="flex items-center justify-between px-4 py-3">
            <div>
              <p className="text-sm font-medium text-text-primary">Paper {p.paperNumber}</p>
              {p.notes && <p className="text-xs text-text-muted">{p.notes}</p>}
            </div>
            <div className="flex items-center gap-2">
              {p.hasMemo && p.memoUrl && (
                <a
                  href={p.memoUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="text-xs text-text-secondary border border-border rounded-md px-2 py-1 hover:border-text-muted transition-colors"
                >
                  Memo
                </a>
              )}
              <a
                href={p.url}
                target="_blank"
                rel="noopener noreferrer"
                className="flex items-center gap-1 text-xs font-medium text-primary border border-primary-200 rounded-md px-2 py-1 hover:bg-primary-50 transition-colors"
              >
                Question Paper <ExternalLink className="h-3 w-3" />
              </a>
            </div>
          </div>
        ))}
      </div>
    </div>
  );

  return (
    <div className="space-y-4">
      <div className="flex items-center gap-3 flex-wrap">
        <label className="text-xs font-medium text-text-secondary">Subject</label>
        <select
          value={selected}
          onChange={e => setSelected(e.target.value)}
          className="rounded-lg border border-border px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary"
        >
          {enrolledSet.size > 0 ? (
            <>
              <optgroup label="My subjects">
                {orderedSubjects.filter(s => enrolledSet.has(s)).map(s => <option key={s} value={s}>{s}</option>)}
              </optgroup>
              <optgroup label="All subjects">
                {orderedSubjects.filter(s => !enrolledSet.has(s)).map(s => <option key={s} value={s}>{s}</option>)}
              </optgroup>
            </>
          ) : (
            orderedSubjects.map(s => <option key={s} value={s}>{s}</option>)
          )}
        </select>
        <label className="text-xs font-medium text-text-secondary">Year</label>
        <select
          value={yearFilter}
          onChange={e => setYearFilter(e.target.value)}
          className="rounded-lg border border-border px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary"
        >
          <option value="">All years</option>
          {availableYears.map(y => <option key={y} value={y}>{y}</option>)}
        </select>
        <label className="text-xs font-medium text-text-secondary">Type</label>
        <select
          value={typeFilter}
          onChange={e => setTypeFilter(e.target.value)}
          className="rounded-lg border border-border px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary"
        >
          <option value="">All types</option>
          <option value="NSCNovember">November NSC</option>
          <option value="Exemplar">2014 NSC Exemplars</option>
        </select>
        <span className="text-xs text-text-muted">All papers link to the official DBE website</span>
      </div>

      {loading && <div className="flex justify-center py-8"><Loader2 className="h-6 w-6 animate-spin text-text-muted" /></div>}

      {!loading && filtered.length === 0 && (
        <div className="rounded-xl border-2 border-dashed border-border py-12 text-center">
          <FileText className="h-10 w-10 text-text-muted mx-auto mb-2" />
          <p className="text-sm text-text-muted">No past papers found for this subject.</p>
        </div>
      )}

      {!loading && Object.keys(byYear).sort((a, b) => Number(b) - Number(a)).map(year =>
        paperGroup(`${year} — ${selected} (November NSC)`, byYear[Number(year)], year))}

      {!loading && exemplars.length > 0 && (
        <div className="space-y-1.5">
          {paperGroup(`2014 NSC Exemplars — ${selected}`, exemplars, "exemplars-2014")}
          <p className="text-xs text-text-muted px-1">
            Exemplars are model papers the DBE published in 2014 to show the current curriculum&apos;s
            exam format — use them as extra practice; they were never written as national exams.
          </p>
        </div>
      )}
    </div>
  );
}

// ─── Quiz tab ─────────────────────────────────────────────────────────────────

function QuizTab({ subjects }: { subjects: string[] }) {
  const [subject, setSubject] = useState(subjects[0] ?? "");
  const [questions, setQuestions] = useState<MatricQuizQuestion[]>([]);
  const [answers, setAnswers] = useState<Record<string, string>>({});
  const [revealed, setRevealed] = useState(false);
  const [loading, setLoading] = useState(false);
  // correctOptions stored separately so we don't send them in quiz DTO (backend omits them)
  // We fetch correct answers on reveal by re-fetching with a flag — or store from first load
  const [correctMap, setCorrectMap] = useState<Record<string, string>>({});

  async function startQuiz() {
    setLoading(true);
    setAnswers({});
    setRevealed(false);
    setCorrectMap({});
    try {
      const qs = await api.matric.quiz(subject, 10);
      setQuestions(qs);
    } finally {
      setLoading(false);
    }
  }

  // Note: correctOption is not returned by the API (for fairness); score is computed client-side
  // after the user submits. For the reveal, we show which one they picked and mark it.
  // Since the backend doesn't expose the answer, we show "Submit to see answers" UX
  // and reveal is done client-side once submitted.

  const score = revealed
    ? questions.filter(q => answers[q.matricQuizQuestionId] === correctMap[q.matricQuizQuestionId]).length
    : 0;

  if (questions.length === 0) {
    return (
      <div className="space-y-4">
        <div className="flex items-center gap-3 flex-wrap">
          <select
            value={subject}
            onChange={e => setSubject(e.target.value)}
            className="rounded-lg border border-border px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary"
          >
            {subjects.map(s => <option key={s} value={s}>{s}</option>)}
          </select>
          <button
            onClick={startQuiz}
            disabled={loading}
            className="flex items-center gap-2 rounded-lg bg-primary px-4 py-2 text-sm font-medium text-white hover:bg-primary-700 disabled:opacity-50 transition-colors"
          >
            {loading ? <Loader2 className="h-4 w-4 animate-spin" /> : <Brain className="h-4 w-4" />}
            Start Quiz
          </button>
        </div>
        <div className="rounded-xl border-2 border-dashed border-border py-16 text-center">
          <Brain className="h-10 w-10 text-text-muted mx-auto mb-3" />
          <p className="text-text-secondary text-sm">Select a subject and start a 10-question quiz.</p>
        </div>
      </div>
    );
  }

  const allAnswered = questions.every(q => answers[q.matricQuizQuestionId]);

  function getOption(q: MatricQuizQuestion, key: string): string {
    if (key === "A") return q.optionA;
    if (key === "B") return q.optionB;
    if (key === "C") return q.optionC;
    return q.optionD;
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <p className="text-sm font-medium text-text-primary">{subject} — {questions.length} questions</p>
        {revealed && (
          <span className={`text-sm font-bold ${score >= 7 ? "text-success-700" : score >= 5 ? "text-warning-700" : "text-danger-700"}`}>
            Score: {score}/{questions.length}
          </span>
        )}
        <button
          onClick={startQuiz}
          className="text-xs text-text-secondary hover:text-text-primary underline"
        >
          New quiz
        </button>
      </div>

      <div className="space-y-4">
        {questions.map((q, idx) => {
          const chosen = answers[q.matricQuizQuestionId];
          return (
            <div key={q.matricQuizQuestionId} className="rounded-xl bg-surface-card border border-border shadow-sm p-4 space-y-3">
              <p className="text-sm font-medium text-text-primary">
                <span className="text-text-muted mr-2">{idx + 1}.</span>
                {q.questionText}
              </p>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                {(["A", "B", "C", "D"] as const).map(key => {
                  const text = getOption(q, key);
                  const isChosen = chosen === key;
                  return (
                    <button
                      key={key}
                      onClick={() => !revealed && setAnswers(a => ({ ...a, [q.matricQuizQuestionId]: key }))}
                      disabled={revealed}
                      className={`flex items-center gap-2 rounded-lg border px-3 py-2 text-left text-sm transition-colors
                        ${isChosen
                          ? "border-primary bg-primary-50 text-primary-800 font-medium"
                          : "border-border hover:border-text-muted text-text-secondary"}`}
                    >
                      <span className="w-5 h-5 flex items-center justify-center rounded-full border border-current text-xs font-bold shrink-0">
                        {key}
                      </span>
                      {text}
                    </button>
                  );
                })}
              </div>
              <p className="text-xs text-text-muted">
                Difficulty: {q.difficulty}
              </p>
            </div>
          );
        })}
      </div>

      {!revealed && (
        <button
          onClick={() => setRevealed(true)}
          disabled={!allAnswered}
          className="w-full rounded-xl bg-primary py-3 text-sm font-semibold text-white hover:bg-primary-700 disabled:opacity-40 transition-colors"
        >
          {allAnswered ? "Submit & See Results" : `Answer all questions (${Object.keys(answers).length}/${questions.length})`}
        </button>
      )}

      {revealed && (
        <div className={`rounded-xl border px-5 py-4 text-center ${score >= 7 ? "bg-success-100 border-success-500/30 text-success-700" : score >= 5 ? "bg-warning-100 border-warning-500/30 text-warning-700" : "bg-danger-100 border-danger-500/30 text-danger-700"}`}>
          <p className="text-2xl font-bold">{score}/{questions.length}</p>
          <p className="text-sm mt-1">
            {score >= 7 ? "Excellent work! Keep it up." : score >= 5 ? "Good effort. Review the topics you missed." : "Keep practising — review the subject material and try again."}
          </p>
          <button onClick={startQuiz} className="mt-3 text-sm underline opacity-75 hover:opacity-100">
            Try another quiz
          </button>
        </div>
      )}
    </div>
  );
}

// ─── Student view (tabbed) ────────────────────────────────────────────────────

function StudentMatricView() {
  const [data,    setData]    = useState<MatricStudentResult | null>(null);
  const [subjects, setSubjects] = useState<string[]>([]);
  const [tab,     setTab]     = useState<"status" | "papers" | "planner" | "quiz" | "tutor" | "nsc">("status");
  const [loading, setLoading] = useState(true);
  const [error,   setError]   = useState("");

  useEffect(() => {
    Promise.allSettled([api.matric.mine(), api.matric.subjects()])
      .then(([mineRes, subjRes]) => {
        if (mineRes.status === "fulfilled") setData(mineRes.value);
        else setError("Failed to load your matric data");
        if (subjRes.status === "fulfilled") setSubjects(subjRes.value);
      })
      .finally(() => setLoading(false));
  }, []);

  if (loading) return <div className="flex items-center justify-center py-20"><Loader2 className="h-8 w-8 animate-spin text-text-muted" /></div>;
  if (error || !data) return <div className="flex items-center gap-2 rounded-lg bg-danger-100 px-4 py-3 text-sm text-danger-700"><AlertTriangle className="h-4 w-4 shrink-0" /> {error || "Failed to load"}</div>;
  if (!data.isGrade12) {
    return (
      <div className="rounded-xl border-2 border-dashed border-border py-16 text-center">
        <Award className="h-10 w-10 text-text-muted mx-auto mb-3" />
        <p className="text-text-secondary">Matric Hub is available for Grade 12 learners only.</p>
      </div>
    );
  }

  const tabs = [
    { key: "status"  as const, label: "NSC Status",    icon: BarChart2     },
    { key: "papers"  as const, label: "Past Papers",   icon: FileText      },
    { key: "planner" as const, label: "Study Planner", icon: CalendarClock },
    { key: "quiz"    as const, label: "Quiz Me",       icon: Brain         },
    { key: "tutor"   as const, label: "AI Tutor",      icon: Sparkles      },
    { key: "nsc"     as const, label: "NSC Rules",     icon: GraduationCap },
  ];

  return (
    <div className="space-y-4">
      {/* Tab bar */}
      <div className="flex gap-1 bg-surface-subtle p-1 rounded-xl">
        {tabs.map(t => {
          const Icon = t.icon;
          return (
            <button
              key={t.key}
              onClick={() => setTab(t.key)}
              className={`flex-1 flex items-center justify-center gap-1.5 rounded-lg px-3 py-2 text-xs font-medium transition-colors
                ${tab === t.key ? "bg-surface-card text-text-primary shadow-sm" : "text-text-secondary hover:text-text-primary"}`}
            >
              <Icon className="h-3.5 w-3.5" /> {t.label}
            </button>
          );
        })}
      </div>

      {tab === "status"  && <NscStatusTab data={data} />}
      {tab === "papers"  && <PastPapersTab subjects={subjects} enrolledSubjects={data.subjects.map(s => s.subjectName)} />}
      {tab === "planner" && <StudyPlannerTab />}
      {tab === "quiz"    && <QuizTab subjects={subjects} />}
      {tab === "tutor"   && <MatricTutorCard subjects={subjects} />}
      {tab === "nsc"     && <NscRequirementsTab />}
    </div>
  );
}

// ─── Staff view (tabbed — Sprint 1.5.2 Week 2) ───────────────────────────────

function StaffMatricView() {
  const canViewClass = usePermission("marks.view_class");
  const isGradeHead = usePosition("GradeHead"); // Grade Overview tab only for Grade Heads
  const [tab, setTab] = useState<"risk" | "status" | "overview">(canViewClass ? "risk" : "status");

  const tabs = [
    ...(canViewClass ? [{ key: "risk" as const, label: "Risk Dashboard", icon: AlertCircle }] : []),
    { key: "status" as const, label: "NSC Status", icon: BarChart2 },
    ...(isGradeHead ? [{ key: "overview" as const, label: "Grade Overview", icon: Award }] : []),
  ];

  return (
    <div className="space-y-4">
      <div className="flex gap-1 bg-surface-subtle p-1 rounded-xl max-w-md">
        {tabs.map(t => {
          const Icon = t.icon;
          return (
            <button
              key={t.key}
              onClick={() => setTab(t.key)}
              className={`flex-1 flex items-center justify-center gap-1.5 rounded-lg px-3 py-2 text-xs font-medium transition-colors
                ${tab === t.key ? "bg-surface-card text-text-primary shadow-sm" : "text-text-secondary hover:text-text-primary"}`}
            >
              <Icon className="h-3.5 w-3.5" /> {t.label}
            </button>
          );
        })}
      </div>

      {tab === "risk" && canViewClass && <RiskDashboardTab />}
      {tab === "status" && <StaffDashboard />}
      {tab === "overview" && isGradeHead && <GradeOverviewTab />}
    </div>
  );
}

// ─── Page ─────────────────────────────────────────────────────────────────────

export default function MatricPage() {
  const router = useRouter();
  const hasMatric = useFeature("matricHub");
  const identity = useIdentity(); // Step 8

  if (!hasMatric) {
    return (
      <div className="flex flex-col items-center justify-center h-96 text-center px-4">
        <Award className="h-12 w-12 text-text-muted mb-4" />
        <h2 className="text-lg font-semibold text-text-primary">Matric Hub not enabled</h2>
        <p className="text-sm text-text-muted mt-1">Enable Matric Hub in Settings.</p>
        <button onClick={() => router.push("/settings")} className="mt-4 text-sm text-primary hover:underline">Go to Settings</button>
      </div>
    );
  }

  return (
    <div className="p-4 md:p-6 lg:p-8 max-w-5xl mx-auto space-y-6">
      <div>
        <h1 className="text-2xl font-semibold text-text-primary tracking-tight">Matric Hub</h1>
        <p className="text-sm text-text-secondary mt-1">
          {identity === "Learner"
            ? "Track your Grade 12 NSC subject results and readiness."
            : "Monitor Grade 12 learner progress against NSC pass requirements."}
        </p>
      </div>
      {identity === "Learner" ? <StudentMatricView /> : identity ? <StaffMatricView /> : null}
    </div>
  );
}
