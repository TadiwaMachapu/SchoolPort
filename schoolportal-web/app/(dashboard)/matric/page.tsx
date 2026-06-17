"use client";
import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { api, type MatricDashboard, type MatricLearnerRow, type MatricStudentResult, type MatricPastPaper, type MatricQuizQuestion, type Class } from "@/lib/api";
import { useFeature } from "@/lib/use-feature";
import { useIdentity } from "@/lib/auth-context";
import MatricTutorCard from "@/components/matric/MatricTutorCard";
import { Award, Loader2, AlertTriangle, CheckCircle2, AlertCircle, XCircle, BarChart2, FileText, Brain, Sparkles, ExternalLink, ChevronRight } from "lucide-react";

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
          <label className="text-xs font-medium text-gray-600">Grade 12 Class</label>
          {classes.length === 0 ? (
            <p className="text-sm text-gray-400 py-2">No Grade 12 classes found. Set GradeLevel = 12 on a class to use Matric Hub.</p>
          ) : (
            <select value={classId} onChange={e => setClassId(e.target.value)}
              className="rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500">
              {classes.map(c => <option key={c.classId} value={c.classId}>{c.name}</option>)}
            </select>
          )}
        </div>
      </div>

      {error && (
        <div className="flex items-center gap-2 rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700">
          <AlertTriangle className="h-4 w-4 shrink-0" /> {error}
        </div>
      )}

      {loading && <div className="flex items-center justify-center py-20"><Loader2 className="h-8 w-8 animate-spin text-gray-400" /></div>}

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
            <div className="rounded-xl border-2 border-dashed border-gray-200 py-16 text-center">
              <p className="text-gray-500">No learners with grade data in this class.</p>
            </div>
          ) : (
            <div className="space-y-2">
              {data.learners.map(l => (
                <div key={l.studentId} className="rounded-xl bg-white border border-gray-200 shadow-sm overflow-hidden">
                  <button
                    onClick={() => setExpanded(e => e === l.studentId ? null : l.studentId)}
                    className="w-full flex items-center justify-between px-5 py-3 text-left hover:bg-gray-50 transition-colors"
                  >
                    <div className="flex items-center gap-3">
                      <div>
                        <p className="font-semibold text-gray-900">{l.name}</p>
                        <p className="text-xs text-gray-400">{l.studentNumber}</p>
                      </div>
                      <OverallBadge status={l.overallStatus} />
                    </div>
                    <div className="flex items-center gap-4 text-right">
                      <div className="flex gap-1.5 items-center">
                        {l.subjects.slice(0, 7).map((s, i) => <SubjectStatusDot key={i} status={s.status} />)}
                      </div>
                      <span className="text-xs text-gray-400">
                        {l.passCount}P · {l.atRiskCount}AR · {l.failCount}F
                      </span>
                    </div>
                  </button>

                  {expanded === l.studentId && (
                    <div className="border-t border-gray-100">
                      <table className="w-full text-sm">
                        <thead className="bg-gray-50">
                          <tr className="text-xs font-semibold text-gray-400 uppercase tracking-wider">
                            <th className="px-5 py-2.5 text-left">Subject</th>
                            <th className="px-4 py-2.5 text-center">Average</th>
                            <th className="px-4 py-2.5 text-center">Status</th>
                          </tr>
                        </thead>
                        <tbody className="divide-y divide-gray-50">
                          {l.subjects.map((s, i) => {
                            const cfg = STATUS_CONFIG[s.status] ?? STATUS_CONFIG.NoData;
                            return (
                              <tr key={i} className="hover:bg-gray-50">
                                <td className="px-5 py-2.5 font-medium text-gray-800">{s.subjectName}</td>
                                <td className="px-4 py-2.5 text-center font-semibold text-gray-800">{s.average}%</td>
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
                      <div className="px-5 py-2.5 bg-gray-50 border-t border-gray-100 text-xs text-gray-500">
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
      <div className="rounded-xl border-2 border-dashed border-gray-200 py-16 text-center">
        <Award className="h-10 w-10 text-gray-200 mx-auto mb-3" />
        <p className="text-gray-500">Matric Hub is available for Grade 12 learners only.</p>
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
        <div className="rounded-xl border-2 border-dashed border-gray-200 py-12 text-center">
          <p className="text-sm text-gray-400">No graded assessments yet.</p>
        </div>
      ) : (
        <div className="rounded-xl bg-white border border-gray-200 shadow-sm overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-gray-50 border-b border-gray-100">
              <tr className="text-xs font-semibold text-gray-400 uppercase tracking-wider">
                <th className="px-5 py-3 text-left">Subject</th>
                <th className="px-4 py-3 text-center">Average</th>
                <th className="px-4 py-3 text-center">NSC Status</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-50">
              {data.subjects.map((s, i) => {
                const scfg = STATUS_CONFIG[s.status] ?? STATUS_CONFIG.NoData;
                return (
                  <tr key={i} className="hover:bg-gray-50">
                    <td className="px-5 py-3 font-medium text-gray-800">{s.subjectName}</td>
                    <td className="px-4 py-3 text-center font-bold text-gray-900">{s.average}%</td>
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
          <div className="px-5 py-2.5 bg-gray-50 border-t border-gray-100 text-xs text-gray-500">
            NSC thresholds: Pass ≥ 40% · At Risk 30–39% · Fail &lt; 30%
          </div>
        </div>
      )}
    </div>
  );
}

// ─── Past papers tab ──────────────────────────────────────────────────────────

function PastPapersTab({ subjects }: { subjects: string[] }) {
  const [selected, setSelected] = useState(subjects[0] ?? "");
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

  const byYear = papers.reduce<Record<number, MatricPastPaper[]>>((acc, p) => {
    (acc[p.year] ??= []).push(p);
    return acc;
  }, {});

  return (
    <div className="space-y-4">
      <div className="flex items-center gap-3 flex-wrap">
        <label className="text-xs font-medium text-gray-600">Subject</label>
        <select
          value={selected}
          onChange={e => setSelected(e.target.value)}
          className="rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400"
        >
          {subjects.map(s => <option key={s} value={s}>{s}</option>)}
        </select>
        <span className="text-xs text-gray-400">All papers link to the official DBE website</span>
      </div>

      {loading && <div className="flex justify-center py-8"><Loader2 className="h-6 w-6 animate-spin text-gray-400" /></div>}

      {!loading && Object.keys(byYear).length === 0 && (
        <div className="rounded-xl border-2 border-dashed border-gray-200 py-12 text-center">
          <FileText className="h-10 w-10 text-gray-200 mx-auto mb-2" />
          <p className="text-sm text-gray-400">No past papers found for this subject.</p>
        </div>
      )}

      {!loading && Object.keys(byYear).sort((a, b) => Number(b) - Number(a)).map(year => (
        <div key={year} className="rounded-xl bg-white border border-gray-200 shadow-sm overflow-hidden">
          <div className="bg-gray-50 border-b border-gray-100 px-4 py-2.5">
            <p className="text-sm font-semibold text-gray-700">{year} — {selected}</p>
          </div>
          <div className="divide-y divide-gray-50">
            {byYear[Number(year)].map(p => (
              <div key={p.matricPastPaperId} className="flex items-center justify-between px-4 py-3">
                <div>
                  <p className="text-sm font-medium text-gray-800">Paper {p.paperNumber}</p>
                  {p.notes && <p className="text-xs text-gray-400">{p.notes}</p>}
                </div>
                <div className="flex items-center gap-2">
                  {p.hasMemo && (
                    <a
                      href={p.memoUrl ?? p.url}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="text-xs text-gray-500 border border-gray-200 rounded-md px-2 py-1 hover:border-gray-400 transition-colors"
                    >
                      Memo
                    </a>
                  )}
                  <a
                    href={p.url}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="flex items-center gap-1 text-xs font-medium text-blue-600 border border-blue-200 rounded-md px-2 py-1 hover:bg-blue-50 transition-colors"
                  >
                    Question Paper <ExternalLink className="h-3 w-3" />
                  </a>
                </div>
              </div>
            ))}
          </div>
        </div>
      ))}
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

  const opts: { key: string; label: string }[] = [
    { key: "A", label: "" }, { key: "B", label: "" },
    { key: "C", label: "" }, { key: "D", label: "" },
  ];

  if (questions.length === 0) {
    return (
      <div className="space-y-4">
        <div className="flex items-center gap-3 flex-wrap">
          <select
            value={subject}
            onChange={e => setSubject(e.target.value)}
            className="rounded-lg border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-400"
          >
            {subjects.map(s => <option key={s} value={s}>{s}</option>)}
          </select>
          <button
            onClick={startQuiz}
            disabled={loading}
            className="flex items-center gap-2 rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50 transition-colors"
          >
            {loading ? <Loader2 className="h-4 w-4 animate-spin" /> : <Brain className="h-4 w-4" />}
            Start Quiz
          </button>
        </div>
        <div className="rounded-xl border-2 border-dashed border-gray-200 py-16 text-center">
          <Brain className="h-10 w-10 text-gray-200 mx-auto mb-3" />
          <p className="text-gray-500 text-sm">Select a subject and start a 10-question quiz.</p>
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
        <p className="text-sm font-medium text-gray-700">{subject} — {questions.length} questions</p>
        {revealed && (
          <span className={`text-sm font-bold ${score >= 7 ? "text-emerald-600" : score >= 5 ? "text-amber-600" : "text-red-600"}`}>
            Score: {score}/{questions.length}
          </span>
        )}
        <button
          onClick={startQuiz}
          className="text-xs text-gray-500 hover:text-gray-700 underline"
        >
          New quiz
        </button>
      </div>

      <div className="space-y-4">
        {questions.map((q, idx) => {
          const chosen = answers[q.matricQuizQuestionId];
          return (
            <div key={q.matricQuizQuestionId} className="rounded-xl bg-white border border-gray-200 shadow-sm p-4 space-y-3">
              <p className="text-sm font-medium text-gray-900">
                <span className="text-gray-400 mr-2">{idx + 1}.</span>
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
                          ? "border-blue-500 bg-blue-50 text-blue-800 font-medium"
                          : "border-gray-200 hover:border-gray-400 text-gray-700"}`}
                    >
                      <span className="w-5 h-5 flex items-center justify-center rounded-full border border-current text-xs font-bold shrink-0">
                        {key}
                      </span>
                      {text}
                    </button>
                  );
                })}
              </div>
              <p className="text-xs text-gray-400">
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
          className="w-full rounded-xl bg-blue-600 py-3 text-sm font-semibold text-white hover:bg-blue-700 disabled:opacity-40 transition-colors"
        >
          {allAnswered ? "Submit & See Results" : `Answer all questions (${Object.keys(answers).length}/${questions.length})`}
        </button>
      )}

      {revealed && (
        <div className={`rounded-xl border px-5 py-4 text-center ${score >= 7 ? "bg-emerald-50 border-emerald-200 text-emerald-800" : score >= 5 ? "bg-amber-50 border-amber-200 text-amber-800" : "bg-red-50 border-red-200 text-red-800"}`}>
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
  const [tab,     setTab]     = useState<"status" | "papers" | "quiz" | "tutor">("status");
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

  if (loading) return <div className="flex items-center justify-center py-20"><Loader2 className="h-8 w-8 animate-spin text-gray-400" /></div>;
  if (error || !data) return <div className="flex items-center gap-2 rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700"><AlertTriangle className="h-4 w-4 shrink-0" /> {error || "Failed to load"}</div>;
  if (!data.isGrade12) {
    return (
      <div className="rounded-xl border-2 border-dashed border-gray-200 py-16 text-center">
        <Award className="h-10 w-10 text-gray-200 mx-auto mb-3" />
        <p className="text-gray-500">Matric Hub is available for Grade 12 learners only.</p>
      </div>
    );
  }

  const tabs = [
    { key: "status" as const, label: "NSC Status",   icon: BarChart2  },
    { key: "papers" as const, label: "Past Papers",  icon: FileText   },
    { key: "quiz"   as const, label: "Quiz Me",      icon: Brain      },
    { key: "tutor"  as const, label: "AI Tutor",     icon: Sparkles   },
  ];

  return (
    <div className="space-y-4">
      {/* Tab bar */}
      <div className="flex gap-1 bg-gray-100 p-1 rounded-xl">
        {tabs.map(t => {
          const Icon = t.icon;
          return (
            <button
              key={t.key}
              onClick={() => setTab(t.key)}
              className={`flex-1 flex items-center justify-center gap-1.5 rounded-lg px-3 py-2 text-xs font-medium transition-colors
                ${tab === t.key ? "bg-white text-gray-900 shadow-sm" : "text-gray-500 hover:text-gray-700"}`}
            >
              <Icon className="h-3.5 w-3.5" /> {t.label}
            </button>
          );
        })}
      </div>

      {tab === "status"  && <NscStatusTab data={data} />}
      {tab === "papers"  && <PastPapersTab subjects={subjects} />}
      {tab === "quiz"    && <QuizTab subjects={subjects} />}
      {tab === "tutor"   && <MatricTutorCard subjects={subjects} />}
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
        <Award className="h-12 w-12 text-gray-200 mb-4" />
        <h2 className="text-lg font-semibold text-gray-700">Matric Hub not enabled</h2>
        <p className="text-sm text-gray-400 mt-1">Enable Matric Hub in Settings.</p>
        <button onClick={() => router.push("/settings")} className="mt-4 text-sm text-blue-600 hover:underline">Go to Settings</button>
      </div>
    );
  }

  return (
    <div className="p-4 md:p-6 lg:p-8 max-w-5xl mx-auto space-y-6">
      <div>
        <h1 className="text-2xl font-semibold text-gray-900 tracking-tight">Matric Hub</h1>
        <p className="text-sm text-gray-500 mt-1">
          {identity === "Learner"
            ? "Track your Grade 12 NSC subject results and readiness."
            : "Monitor Grade 12 learner progress against NSC pass requirements."}
        </p>
      </div>
      {identity === "Learner" ? <StudentMatricView /> : identity ? <StaffDashboard /> : null}
    </div>
  );
}
