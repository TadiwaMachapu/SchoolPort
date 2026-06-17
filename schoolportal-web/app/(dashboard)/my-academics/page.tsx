"use client";
import { useCallback, useEffect, useState } from "react";
import { LayoutGrid, ListChecks, ClipboardList, AlertCircle } from "lucide-react";
import { api, type MyAcademics } from "@/lib/api";
import { cn } from "@/lib/utils";
import { SkeletonCards } from "@/components/ui/skeleton";
import { SubjectsTab } from "@/components/my-academics/SubjectsTab";
import { MarksTab } from "@/components/my-academics/MarksTab";
import { AssignmentsTab } from "@/components/my-academics/AssignmentsTab";

// My Academics — the learner's home for subjects, marks and tasks (replaces their access to the
// separate marks/assignments pages). Single API call (/api/gradebook/my-academics) feeds all three
// tabs. Mobile-first: cards and lists only, colour-coded for at-a-glance reading.

type Tab = "subjects" | "marks" | "assignments";

const TABS: { id: Tab; label: string; icon: typeof LayoutGrid }[] = [
  { id: "subjects", label: "Subjects", icon: LayoutGrid },
  { id: "marks", label: "My Marks", icon: ListChecks },
  { id: "assignments", label: "Assignments", icon: ClipboardList },
];

export default function MyAcademicsPage() {
  const [data, setData] = useState<MyAcademics | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const [tab, setTab] = useState<Tab>("subjects");
  const [selectedSubjectId, setSelectedSubjectId] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      const res = await api.gradebook.myAcademics();
      setData(res);
      setError(false);
    } catch {
      setError(true);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  function openSubject(id: string) {
    setSelectedSubjectId(id);
    setTab("marks");
  }

  return (
    <div className="p-6 lg:p-8">
      <h1 className="text-2xl font-semibold tracking-tight text-gray-900">My Academics</h1>
      <p className="mt-1 text-sm text-gray-500">Your subjects, marks and tasks for this term.</p>

      {/* Tab bar */}
      <div className="mt-5 flex gap-1 rounded-xl bg-gray-100 p-1">
        {TABS.map((t) => {
          const Icon = t.icon;
          const active = tab === t.id;
          return (
            <button
              key={t.id}
              onClick={() => setTab(t.id)}
              className={cn(
                "flex flex-1 items-center justify-center gap-1.5 rounded-lg px-2 py-2 text-sm font-medium transition-colors",
                active ? "bg-white text-gray-900 shadow-sm" : "text-gray-500 hover:text-gray-700",
              )}
            >
              <Icon className="h-4 w-4" />
              <span className="truncate">{t.label}</span>
            </button>
          );
        })}
      </div>

      <div className="mt-5">
        {loading ? (
          <SkeletonCards count={6} />
        ) : error ? (
          <div className="flex flex-col items-center justify-center px-6 py-16 text-center">
            <AlertCircle className="h-10 w-10 text-gray-300" />
            <h3 className="mt-4 text-base font-semibold text-gray-900">Couldn't load your academics</h3>
            <p className="mt-1 text-sm text-gray-500">Please check your connection and try again.</p>
          </div>
        ) : data ? (
          <>
            {tab === "subjects" && <SubjectsTab subjects={data.subjects} onSelectSubject={openSubject} />}
            {tab === "marks" && <MarksTab data={data} selectedSubjectId={selectedSubjectId} onSelectSubject={setSelectedSubjectId} />}
            {tab === "assignments" && <AssignmentsTab tasks={data.tasks} onChanged={load} />}
          </>
        ) : null}
      </div>
    </div>
  );
}
