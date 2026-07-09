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

  if (loading) return <div className="flex justify-center py-16"><Loader2 className="h-6 w-6 animate-spin text-gray-400" /></div>;
  if (error || !plan) {
    return (
      <div className="flex items-center gap-2 rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700">
        <AlertTriangle className="h-4 w-4 shrink-0" /> {error || "Failed to load"}
      </div>
    );
  }

  return (
    <div className="space-y-5">
      {/* Countdown */}
      <div className="rounded-xl border border-blue-200 bg-blue-50 px-5 py-4 flex items-center gap-4">
        <CalendarClock className="h-8 w-8 text-blue-500 shrink-0" />
        <div>
          <p className="text-lg font-bold text-blue-900">
            {plan.daysToExams === 0
              ? "The November NSC exams are here"
              : `${plan.daysToExams} days to the November NSC exams`}
          </p>
          <p className="text-sm text-blue-700">
            {plan.weeksToExams} week{plan.weeksToExams !== 1 ? "s" : ""} of preparation left
            {plan.suggestedWeeklySessions > 0 &&
              ` · suggested ${plan.suggestedWeeklySessions} study sessions a week (±1 hour each)`}
          </p>
        </div>
      </div>

      {plan.subjects.length === 0 ? (
        <div className="rounded-xl border-2 border-dashed border-gray-200 py-12 text-center">
          <Target className="h-10 w-10 text-gray-200 mx-auto mb-2" />
          <p className="text-sm text-gray-400">No graded assessments yet — your plan appears once marks come in.</p>
        </div>
      ) : (
        <div className="space-y-2">
          {plan.subjects.map(s => (
            <div key={s.subjectName} className="rounded-xl bg-white border border-gray-200 shadow-sm px-5 py-4">
              <div className="flex items-center justify-between gap-3 flex-wrap">
                <div className="flex items-center gap-2">
                  <span className={`inline-block h-2.5 w-2.5 rounded-full ${STATUS_DOT[s.status] ?? "bg-gray-300"}`} />
                  <p className="font-semibold text-gray-900">{s.subjectName}</p>
                  <span className="text-sm text-gray-500">{s.average}%</span>
                </div>
                <span className="text-xs font-semibold text-gray-600 bg-gray-100 rounded-full px-2.5 py-1">
                  {s.weeklySessions} session{s.weeklySessions !== 1 ? "s" : ""}/week
                </span>
              </div>
              <p className="text-xs text-gray-500 mt-2">{s.focusHint}</p>
            </div>
          ))}
          <p className="text-xs text-gray-400 px-1 pt-1">
            Weakest subjects first — that is where your hours count most. Goals update as new marks are captured.
          </p>
        </div>
      )}
    </div>
  );
}
