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
                  ? "bg-primary text-white ring-primary"
                  : "bg-surface-card text-text-secondary ring-border hover:bg-surface-subtle",
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
          className="w-full rounded-lg border border-border bg-surface-card px-3 py-2 text-sm font-medium text-text-primary shadow-sm focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary"
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
              tn === term ? "bg-text-primary text-white ring-text-primary" : "bg-surface-card text-text-secondary ring-border hover:bg-surface-subtle",
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
                  "rounded-xl border bg-surface-card p-3.5 shadow-sm ring-1 ring-border/50",
                  pending ? "border-border opacity-70" : "border-border",
                )}
              >
                <div className="flex items-start justify-between gap-2">
                  <p className={cn("text-sm font-medium leading-tight", pending ? "text-text-secondary" : "text-text-primary")}>{t.title}</p>
                  <TypeBadge type={t.type} />
                </div>
                <div className="mt-2 flex items-center justify-between gap-2">
                  <span className="text-xs text-text-muted">{fmtDate(t.date ?? t.dueAt)}</span>
                  {pending ? (
                    <span className="inline-flex items-center rounded-md bg-surface-subtle px-2 py-0.5 text-xs font-medium text-text-secondary ring-1 ring-border">Pending</span>
                  ) : (
                    <div className="flex items-center gap-2.5">
                      <span className="text-xs tabular-nums text-text-secondary">
                        {t.score}<span className="text-text-muted"> / </span>{t.outOf}
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
      <div className="sticky bottom-2 z-10 flex items-center justify-between gap-3 rounded-xl border border-border bg-surface-card/95 px-4 py-3 shadow-md ring-1 ring-border/50 backdrop-blur">
        <span className="text-sm font-medium text-text-secondary">Term {term} average</span>
        <div className="flex items-center gap-2.5">
          {termAvg != null ? (
            <>
              {isCurrent && (selected?.trend === "up" ? <TrendingUp className="h-4 w-4 text-success-500" />
                : selected?.trend === "down" ? <TrendingDown className="h-4 w-4 text-danger-500" />
                : selected?.trend === "flat" ? <Minus className="h-4 w-4 text-text-muted" /> : null)}
              <span className={cn("text-lg font-bold tabular-nums", percentColor(termAvg))}>{termAvg}%</span>
              <CapsBadge percentage={termAvg} />
            </>
          ) : (
            <span className="text-sm text-text-muted">Not yet assessed</span>
          )}
        </div>
      </div>
    </div>
  );
}
