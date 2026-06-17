"use client";
import { useMemo, useState } from "react";
import { ClipboardList, Minus, TrendingDown, TrendingUp } from "lucide-react";
import type { MyAcademics, MyAcademicsSubject, MyAcademicsTask } from "@/lib/api";
import { cn, getCapsCode } from "@/lib/utils";
import { CapsBadge, TypeBadge, percentColor } from "./badges";
import { EmptyState } from "./EmptyState";

// Tab 2 — My Marks. Subject selector (pills when ≤4 subjects, dropdown otherwise) + term selector,
// then each assessment as a CARD (never a table on mobile). Pending tasks are greyed. A sticky
// summary bar shows the term average + CAPS code + trend, visible without scrolling the list.

function fmtDate(iso?: string | null): string {
  if (!iso) return "—";
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? "—" : d.toLocaleDateString("en-GB"); // dd/mm/yyyy
}

export function MarksTab({
  data, selectedSubjectId, onSelectSubject,
}: {
  data: MyAcademics;
  selectedSubjectId: string | null;
  onSelectSubject: (id: string) => void;
}) {
  const { subjects, tasks, terms, currentTerm } = data;
  const selected: MyAcademicsSubject | undefined =
    subjects.find((s) => s.classSubjectId === selectedSubjectId) ?? subjects[0];

  const termNumbers = terms.length ? terms.map((t) => t.termNumber) : [1, 2, 3, 4];
  const [term, setTerm] = useState<number>(currentTerm?.termNumber ?? termNumbers[termNumbers.length - 1]);

  const subjectTasks = useMemo(() => {
    if (!selected) return [];
    return tasks
      .filter((t) => t.classSubjectId === selected.classSubjectId && t.termNumber === term)
      .sort((a, b) => (new Date(a.date ?? a.dueAt ?? 0).getTime()) - (new Date(b.date ?? b.dueAt ?? 0).getTime()));
  }, [tasks, selected, term]);

  // Term average for the bar = graded ASSIGNMENT-source tasks (matches the Subjects-tab figure).
  const gradedPercents = subjectTasks
    .filter((t) => t.source === "assignment" && t.percent != null)
    .map((t) => t.percent!);
  const termAvg = gradedPercents.length
    ? Math.round((gradedPercents.reduce((a, b) => a + b, 0) / gradedPercents.length))
    : null;
  const isCurrent = currentTerm?.termNumber === term;

  if (subjects.length === 0) {
    return <EmptyState icon={ClipboardList} title="No subjects yet" description="Your marks will appear here once you're enrolled in subjects." />;
  }

  return (
    <div className="space-y-4">
      {/* Subject selector */}
      {subjects.length <= 4 ? (
        <div className="flex flex-wrap gap-2">
          {subjects.map((s) => (
            <button
              key={s.classSubjectId}
              onClick={() => onSelectSubject(s.classSubjectId)}
              className={cn(
                "rounded-full px-3.5 py-1.5 text-sm font-medium ring-1 transition-colors",
                s.classSubjectId === selected?.classSubjectId
                  ? "bg-blue-600 text-white ring-blue-600"
                  : "bg-white text-gray-600 ring-gray-200 hover:bg-gray-50",
              )}
            >
              {s.subjectName}
            </button>
          ))}
        </div>
      ) : (
        <select
          value={selected?.classSubjectId}
          onChange={(e) => onSelectSubject(e.target.value)}
          className="w-full rounded-lg border border-gray-200 bg-white px-3 py-2 text-sm font-medium text-gray-900 shadow-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
        >
          {subjects.map((s) => (
            <option key={s.classSubjectId} value={s.classSubjectId}>{s.subjectName}</option>
          ))}
        </select>
      )}

      {/* Term selector */}
      <div className="flex gap-1.5 overflow-x-auto pb-0.5">
        {termNumbers.map((tn) => (
          <button
            key={tn}
            onClick={() => setTerm(tn)}
            className={cn(
              "shrink-0 rounded-md px-3 py-1 text-xs font-semibold ring-1 transition-colors",
              tn === term ? "bg-gray-900 text-white ring-gray-900" : "bg-white text-gray-500 ring-gray-200 hover:bg-gray-50",
            )}
          >
            Term {tn}
          </button>
        ))}
      </div>

      {/* Task cards */}
      {subjectTasks.length === 0 ? (
        <EmptyState
          icon={ClipboardList}
          title="No marks captured yet"
          description={`No marks captured yet for ${selected?.subjectName ?? "this subject"} this term.`}
        />
      ) : (
        <div className="space-y-2">
          {subjectTasks.map((t) => {
            const pending = t.percent == null;
            return (
              <div
                key={`${t.source}-${t.taskId}`}
                className={cn(
                  "rounded-xl border bg-white p-3.5 shadow-sm ring-1 ring-gray-100/50",
                  pending ? "border-gray-100 opacity-70" : "border-gray-100",
                )}
              >
                <div className="flex items-start justify-between gap-2">
                  <p className={cn("text-sm font-medium leading-tight", pending ? "text-gray-500" : "text-gray-900")}>{t.title}</p>
                  <TypeBadge type={t.type} />
                </div>
                <div className="mt-2 flex items-center justify-between gap-2">
                  <span className="text-xs text-gray-400">{fmtDate(t.date ?? t.dueAt)}</span>
                  {pending ? (
                    <span className="inline-flex items-center rounded-md bg-gray-100 px-2 py-0.5 text-xs font-medium text-gray-500 ring-1 ring-gray-200/70">Pending</span>
                  ) : (
                    <div className="flex items-center gap-2.5">
                      <span className="text-xs tabular-nums text-gray-500">
                        {t.score}<span className="text-gray-300"> / </span>{t.outOf}
                      </span>
                      <span className={cn("text-sm font-bold tabular-nums", percentColor(t.percent!))}>{Math.round(t.percent!)}%</span>
                      <CapsBadge percentage={t.percent!} compact />
                    </div>
                  )}
                </div>
              </div>
            );
          })}
        </div>
      )}

      {/* Sticky term summary bar */}
      <div className="sticky bottom-2 z-10 flex items-center justify-between gap-3 rounded-xl border border-gray-100 bg-white/95 px-4 py-3 shadow-md ring-1 ring-gray-100/50 backdrop-blur">
        <span className="text-sm font-medium text-gray-600">Term {term} average</span>
        <div className="flex items-center gap-2.5">
          {termAvg != null ? (
            <>
              {isCurrent && (selected?.trend === "up" ? <TrendingUp className="h-4 w-4 text-emerald-600" />
                : selected?.trend === "down" ? <TrendingDown className="h-4 w-4 text-rose-600" />
                : selected?.trend === "flat" ? <Minus className="h-4 w-4 text-gray-400" /> : null)}
              <span className={cn("text-lg font-bold tabular-nums", percentColor(termAvg))}>{termAvg}%</span>
              <CapsBadge percentage={termAvg} />
            </>
          ) : (
            <span className="text-sm text-gray-400">Not yet assessed</span>
          )}
        </div>
      </div>
    </div>
  );
}
