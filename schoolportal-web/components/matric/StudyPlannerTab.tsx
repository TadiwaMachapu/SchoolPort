"use client";
import { useEffect, useState } from "react";
import { api, type MatricStudyPlan } from "@/lib/api";
import { CalendarClock, Loader2, AlertTriangle, Target } from "lucide-react";

const STATUS_DOT: Record<string, string> = {
  Pass: "bg-emerald-400",
  AtRisk: "bg-amber-400",
  Fail: "bg-red-500",
};

export default function StudyPlannerTab() {
  const [plan, setPlan] = useState<MatricStudyPlan | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    api.matric.studyPlan()
      .then(setPlan)
      .catch(e => setError(e instanceof Error ? e.message : "Failed to load your study plan"))
      .finally(() => setLoading(false));
  }, []);

  if (loading) return <div className="flex justify-center py-16"><Loader2 className="h-6 w-6 animate-spin text-text-muted" /></div>;
  if (error || !plan) {
    return (
      <div className="flex items-center gap-2 rounded-lg bg-danger-100 px-4 py-3 text-sm text-danger-700">
        <AlertTriangle className="h-4 w-4 shrink-0" /> {error || "Failed to load"}
      </div>
    );
  }

  return (
    <div className="space-y-5">
      {/* Countdown */}
      <div className="rounded-xl border border-primary-200 bg-primary-50 px-5 py-4 flex items-center gap-4">
        <CalendarClock className="h-8 w-8 text-primary shrink-0" />
        <div>
          <p className="text-lg font-bold text-primary-900">
            {plan.daysToExams === 0
              ? "The November NSC exams are here"
              : `${plan.daysToExams} days to the November NSC exams`}
          </p>
          <p className="text-sm text-primary-700">
            {plan.weeksToExams} week{plan.weeksToExams !== 1 ? "s" : ""} of preparation left
            {plan.suggestedWeeklySessions > 0 &&
              ` · suggested ${plan.suggestedWeeklySessions} study sessions a week (±1 hour each)`}
          </p>
        </div>
      </div>

      {plan.subjects.length === 0 ? (
        <div className="rounded-xl border-2 border-dashed border-border py-12 text-center">
          <Target className="h-10 w-10 text-text-muted mx-auto mb-2" />
          <p className="text-sm text-text-muted">No graded assessments yet — your plan appears once marks come in.</p>
        </div>
      ) : (
        <div className="space-y-2">
          {plan.subjects.map(s => (
            <div key={s.subjectName} className="rounded-xl bg-surface-card border border-border shadow-sm px-5 py-4">
              <div className="flex items-center justify-between gap-3 flex-wrap">
                <div className="flex items-center gap-2">
                  <span className={`inline-block h-2.5 w-2.5 rounded-full ${STATUS_DOT[s.status] ?? "bg-gray-300"}`} />
                  <p className="font-semibold text-text-primary">{s.subjectName}</p>
                  <span className="text-sm text-text-secondary">{s.average}%</span>
                </div>
                <span className="text-xs font-semibold text-text-secondary bg-surface-subtle rounded-full px-2.5 py-1">
                  {s.weeklySessions} session{s.weeklySessions !== 1 ? "s" : ""}/week
                </span>
              </div>
              <p className="text-xs text-text-secondary mt-2">{s.focusHint}</p>
            </div>
          ))}
          <p className="text-xs text-text-muted px-1 pt-1">
            Weakest subjects first — that is where your hours count most. Goals update as new marks are captured.
          </p>
        </div>
      )}
    </div>
  );
}
