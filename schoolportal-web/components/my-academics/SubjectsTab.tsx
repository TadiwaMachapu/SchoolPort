"use client";
import { BookOpen, Minus, TrendingDown, TrendingUp } from "lucide-react";
import type { MyAcademicsSubject } from "@/lib/api";
import { cn } from "@/lib/utils";
import { CapsBadge, percentColor } from "./badges";
import { EmptyState } from "./EmptyState";

// Tab 1 — Subjects. Card grid (2 cols mobile / 3 desktop). The big colour-coded percentage is the
// hero of each card: a learner should know in 3 seconds which subject needs attention. Tapping a
// card opens that subject's detailed marks (Tab 2).

function TrendIcon({ trend }: { trend: MyAcademicsSubject["trend"] }) {
  if (trend === "up") return <TrendingUp className="h-4 w-4 text-success-500" aria-label="Improving" />;
  if (trend === "down") return <TrendingDown className="h-4 w-4 text-danger-500" aria-label="Declining" />;
  if (trend === "flat") return <Minus className="h-4 w-4 text-text-muted" aria-label="Steady" />;
  return null;
}

export function SubjectsTab({
  subjects, onSelectSubject,
}: {
  subjects: MyAcademicsSubject[];
  onSelectSubject: (classSubjectId: string) => void;
}) {
  if (subjects.length === 0) {
    return (
      <EmptyState
        icon={BookOpen}
        title="No subjects enrolled for this term yet"
        description="Contact your teacher if this looks wrong."
      />
    );
  }

  return (
    <div className="grid grid-cols-2 gap-3 sm:gap-4 lg:grid-cols-3">
      {subjects.map((s) => {
        const avg = s.termAveragePercent;
        const hasAvg = avg != null;
        const pct = Math.max(0, Math.min(100, s.tasksTotal > 0 ? (s.tasksAssessed / s.tasksTotal) * 100 : 0));
        return (
          <button
            key={s.classSubjectId}
            onClick={() => onSelectSubject(s.classSubjectId)}
            className="group flex flex-col rounded-xl border border-border bg-surface-card p-4 text-left shadow-sm ring-1 ring-border/50 transition-all hover:border-border hover:shadow-md focus:outline-none focus-visible:ring-2 focus-visible:ring-primary"
          >
            <div className="min-w-0">
              <h3 className="truncate text-base font-semibold leading-tight text-text-primary">{s.subjectName}</h3>
              <p className="mt-0.5 truncate text-xs text-text-secondary">{s.teacherName ?? "Unassigned"}</p>
            </div>

            <div className="mt-3 flex min-h-8 items-end gap-2">
              {hasAvg ? (
                <>
                  <span className={cn("text-3xl font-bold leading-none tabular-nums", percentColor(avg!))}>{Math.round(avg!)}%</span>
                  <TrendIcon trend={s.trend} />
                  <CapsBadge percentage={avg!} compact className="ml-auto" />
                </>
              ) : (
                <span className="text-sm font-medium text-text-muted">No marks yet</span>
              )}
            </div>

            <div className="mt-3">
              {/* 4px track, light-grey, always visible so it reads as a progress bar even at 0%. */}
              <div className="h-1 w-full overflow-hidden rounded-full bg-surface-subtle">
                <div
                  className={cn("h-full rounded-full transition-all", hasAvg ? "bg-primary" : "bg-text-muted")}
                  style={{ width: `${pct}%` }}
                />
              </div>
              <p className="mt-1.5 text-[11px] text-text-secondary">
                {s.tasksAssessed} of {s.tasksTotal} {s.tasksTotal === 1 ? "task" : "tasks"} assessed
              </p>
            </div>
          </button>
        );
      })}
    </div>
  );
}
