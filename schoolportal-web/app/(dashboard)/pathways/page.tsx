"use client";
import { useEffect, useState, useCallback } from "react";
import { useRouter } from "next/navigation";
import {
  api,
  type Class, type PathwaysMatrix, type LearnerSubjectItem, type Subject,
  type UniversitySummary, type UniversityCourseDetail, type CareerItem,
  type GoalWithTracking, type GoalTracking,
} from "@/lib/api";
import { useFeature } from "@/lib/use-feature";
import { useIdentity } from "@/lib/auth-context";
import { Button } from "@/components/ui/button";
import GapAnalysisCard from "@/components/pathways/GapAnalysisCard";
import Gr9AdvisorCard from "@/components/pathways/Gr9AdvisorCard";
import {
  Route, AlertTriangle, Loader2, CheckCircle2, XCircle, BookOpen,
  Target, Trash2, ChevronDown, ChevronUp, Search, Building2, Briefcase, GraduationCap,
} from "lucide-react";

// ── Shared helpers ─────────────────────────────────────────────────────────────

const STATUS_CONFIG = {
  Green: { label: "On track",     bg: "bg-emerald-100", text: "text-emerald-700", dot: "bg-emerald-500" },
  Amber: { label: "Almost there", bg: "bg-amber-100",   text: "text-amber-700",   dot: "bg-amber-500" },
  Red:   { label: "Needs work",   bg: "bg-red-100",     text: "text-red-600",     dot: "bg-red-500" },
};

function StatusBadge({ status }: { status: "Green" | "Amber" | "Red" }) {
  const cfg = STATUS_CONFIG[status];
  return (
    <span className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-semibold ${cfg.bg} ${cfg.text}`}>
      <span className={`h-1.5 w-1.5 rounded-full ${cfg.dot}`} />
      {cfg.label}
    </span>
  );
}

const PHASE_BADGE: Record<string, string> = {
  SeniorPhase: "bg-blue-100 text-blue-700",
  FET: "bg-purple-100 text-purple-700",
};
const PHASE_LABEL: Record<string, string> = { SeniorPhase: "Gr 7–9", FET: "Gr 10–12" };

function PhaseBadge({ phase }: { phase?: string }) {
  if (!phase) return null;
  return (
    <span className={`inline-block rounded-full px-2 py-0.5 text-[10px] font-semibold ${PHASE_BADGE[phase] ?? "bg-gray-100 text-gray-600"}`}>
      {PHASE_LABEL[phase] ?? phase}
    </span>
  );
}

// ── Tab: Career Goals ─────────────────────────────────────────────────────────

function CareerGoalsTab() {
  const [goals, setGoals] = useState<GoalWithTracking[]>([]);
  const [universities, setUniversities] = useState<UniversitySummary[]>([]);
  const [careers, setCareers] = useState<CareerItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  // Browse panel
  const [browseUni, setBrowseUni] = useState<string>("");
  const [uniCourses, setUniCourses] = useState<UniversityCourseDetail[]>([]);
  const [coursesLoading, setCoursesLoading] = useState(false);
  const [searchQ, setSearchQ] = useState("");
  const [categoryFilter, setCategoryFilter] = useState("");

  // Expanded goal for detailed tracking + AI
  const [expandedGoalId, setExpandedGoalId] = useState<string | null>(null);
  const [trackingData, setTrackingData] = useState<Record<string, GoalTracking>>({});

  const [addingCourse, setAddingCourse] = useState<string | null>(null);
  const [deletingGoal, setDeletingGoal] = useState<string | null>(null);

  useEffect(() => {
    Promise.all([
      api.pathways.myGoals(),
      api.pathways.universities(),
      api.pathways.careers(),
    ]).then(([g, u, c]) => {
      setGoals(g);
      setUniversities(u);
      setCareers(c);
    }).catch(e => setError(e instanceof Error ? e.message : "Failed to load")).finally(() => setLoading(false));
  }, []);

  const loadUniCourses = useCallback(async (uniId: string) => {
    if (!uniId) { setUniCourses([]); return; }
    setCoursesLoading(true);
    try {
      setUniCourses(await api.pathways.universityCourses(uniId));
    } catch { setUniCourses([]); } finally { setCoursesLoading(false); }
  }, []);

  useEffect(() => { loadUniCourses(browseUni); }, [browseUni, loadUniCourses]);

  async function addGoal(universityCourseId: string) {
    setAddingCourse(universityCourseId);
    try {
      const goal = await api.pathways.addGoal(universityCourseId);
      setGoals(prev => [...prev, goal]);
    } catch (e) {
      alert(e instanceof Error ? e.message : "Could not add goal");
    } finally { setAddingCourse(null); }
  }

  async function removeGoal(goalId: string) {
    setDeletingGoal(goalId);
    try {
      await api.pathways.deleteGoal(goalId);
      setGoals(prev => prev.filter(g => g.learnerCareerGoalId !== goalId));
      if (expandedGoalId === goalId) setExpandedGoalId(null);
    } catch (e) {
      alert(e instanceof Error ? e.message : "Could not remove goal");
    } finally { setDeletingGoal(null); }
  }

  async function toggleExpand(goalId: string) {
    if (expandedGoalId === goalId) { setExpandedGoalId(null); return; }
    setExpandedGoalId(goalId);
    if (!trackingData[goalId]) {
      try {
        const t = await api.pathways.goalTracking(goalId);
        setTrackingData(prev => ({ ...prev, [goalId]: t }));
      } catch {}
    }
  }

  const savedCourseIds = new Set(goals.map(g => g.universityCourseId));
  const categories = [...new Set(careers.map(c => c.category).filter(Boolean))].sort() as string[];

  const filteredCourses = uniCourses.filter(c => {
    const q = searchQ.toLowerCase();
    const matchSearch = !q || c.name.toLowerCase().includes(q) || (c.careerName?.toLowerCase().includes(q) ?? false);
    const matchCat = !categoryFilter || (c.careerCategory === categoryFilter);
    return matchSearch && matchCat;
  });

  if (loading) return <div className="flex justify-center py-20"><Loader2 className="h-8 w-8 animate-spin text-gray-400" /></div>;
  if (error) return <div className="flex items-center gap-2 rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700"><AlertTriangle className="h-4 w-4 shrink-0" />{error}</div>;

  return (
    <div className="space-y-8">
      {/* ── Saved goals ─────────────────────────────────────────────── */}
      <div>
        <div className="flex items-center justify-between mb-3">
          <h2 className="text-base font-semibold text-gray-900">My Career Goals</h2>
          <span className="text-xs text-gray-400">{goals.length}/5 saved</span>
        </div>

        {goals.length === 0 ? (
          <div className="rounded-xl border-2 border-dashed border-gray-200 py-12 text-center">
            <Target className="h-10 w-10 text-gray-200 mx-auto mb-3" />
            <p className="text-sm font-medium text-gray-600">No career goals saved yet</p>
            <p className="text-xs text-gray-400 mt-1">Browse universities below and save up to 5 goals to track your progress.</p>
          </div>
        ) : (
          <div className="space-y-3">
            {goals.map(goal => {
              const expanded = expandedGoalId === goal.learnerCareerGoalId;
              const tracking = trackingData[goal.learnerCareerGoalId];

              return (
                <div key={goal.learnerCareerGoalId} className="rounded-xl border border-gray-200 bg-white shadow-sm overflow-hidden">
                  {/* Goal header */}
                  <div className="flex items-center gap-3 px-4 py-4">
                    <div className="flex-1 min-w-0">
                      <p className="font-semibold text-gray-900 truncate">{goal.courseName}</p>
                      <p className="text-xs text-gray-500 mt-0.5">{goal.universityName} · APS {goal.minimumAps} required · You: {goal.currentAps}</p>
                    </div>
                    <StatusBadge status={goal.status} />
                    <button
                      onClick={() => toggleExpand(goal.learnerCareerGoalId)}
                      className="p-1.5 rounded-lg hover:bg-gray-100 text-gray-400 hover:text-gray-600 transition-colors"
                    >
                      {expanded ? <ChevronUp className="h-4 w-4" /> : <ChevronDown className="h-4 w-4" />}
                    </button>
                    <button
                      onClick={() => removeGoal(goal.learnerCareerGoalId)}
                      disabled={deletingGoal === goal.learnerCareerGoalId}
                      className="p-1.5 rounded-lg hover:bg-red-50 text-gray-300 hover:text-red-500 transition-colors disabled:opacity-50"
                    >
                      {deletingGoal === goal.learnerCareerGoalId ? <Loader2 className="h-4 w-4 animate-spin" /> : <Trash2 className="h-4 w-4" />}
                    </button>
                  </div>

                  {/* Expanded detail */}
                  {expanded && (
                    <div className="border-t border-gray-100 px-4 pb-4 pt-3 space-y-4">
                      {!tracking ? (
                        <div className="flex justify-center py-4"><Loader2 className="h-5 w-5 animate-spin text-gray-400" /></div>
                      ) : (
                        <>
                          {/* APS progress */}
                          <div className="flex items-center gap-3">
                            <div className="flex-1">
                              <div className="flex justify-between text-xs text-gray-500 mb-1">
                                <span>APS Score</span>
                                <span>{tracking.currentAps} / {tracking.minimumAps}</span>
                              </div>
                              <div className="h-2 rounded-full bg-gray-100">
                                <div
                                  className={`h-2 rounded-full transition-all ${tracking.apsGap === 0 ? "bg-emerald-500" : tracking.apsGap <= 3 ? "bg-amber-400" : "bg-red-400"}`}
                                  style={{ width: `${Math.min(100, (tracking.currentAps / tracking.minimumAps) * 100)}%` }}
                                />
                              </div>
                            </div>
                            {tracking.apsGap === 0 ? (
                              <CheckCircle2 className="h-5 w-5 text-emerald-500 shrink-0" />
                            ) : (
                              <span className="text-xs text-gray-500 shrink-0">need +{tracking.apsGap}</span>
                            )}
                          </div>

                          {/* Subject gaps */}
                          {tracking.subjectGaps.length > 0 && (
                            <div className="space-y-2">
                              <p className="text-xs font-semibold text-gray-500 uppercase tracking-wider">Required Subjects</p>
                              {tracking.subjectGaps.map(gap => (
                                <div key={gap.subjectName} className="flex items-center gap-2 text-xs">
                                  {gap.met
                                    ? <CheckCircle2 className="h-3.5 w-3.5 text-emerald-500 shrink-0" />
                                    : <AlertTriangle className="h-3.5 w-3.5 text-amber-500 shrink-0" />}
                                  <span className="flex-1 text-gray-700">{gap.subjectName}</span>
                                  <span className={gap.met ? "text-emerald-600" : "text-amber-600"}>
                                    {gap.currentPercent !== undefined ? `${gap.currentPercent.toFixed(0)}%` : "no data"} / {gap.requiredPercent}%
                                  </span>
                                </div>
                              ))}
                            </div>
                          )}

                          {tracking.apsNotes && (
                            <p className="text-xs text-gray-500 italic">{tracking.apsNotes}</p>
                          )}

                          {/* AI gap analysis */}
                          <GapAnalysisCard goalId={goal.learnerCareerGoalId} courseName={goal.courseName} />
                        </>
                      )}
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        )}
      </div>

      {/* ── Browse universities ──────────────────────────────────────── */}
      {goals.length < 5 && (
        <div>
          <h2 className="text-base font-semibold text-gray-900 mb-3">Browse University Courses</h2>

          <div className="flex flex-wrap gap-3 mb-4">
            {/* University picker */}
            <div className="space-y-1">
              <label className="text-xs font-medium text-gray-600 flex items-center gap-1"><Building2 className="h-3 w-3" /> University</label>
              <select
                value={browseUni}
                onChange={e => setBrowseUni(e.target.value)}
                className="rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              >
                <option value="">Select a university…</option>
                {universities.map(u => (
                  <option key={u.universityId} value={u.universityId}>{u.abbreviation} — {u.name}</option>
                ))}
              </select>
            </div>

            {/* Category filter */}
            {browseUni && uniCourses.length > 0 && (
              <div className="space-y-1">
                <label className="text-xs font-medium text-gray-600 flex items-center gap-1"><Briefcase className="h-3 w-3" /> Field</label>
                <select
                  value={categoryFilter}
                  onChange={e => setCategoryFilter(e.target.value)}
                  className="rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                >
                  <option value="">All fields</option>
                  {categories.map(cat => <option key={cat} value={cat}>{cat}</option>)}
                </select>
              </div>
            )}

            {/* Search */}
            {browseUni && (
              <div className="space-y-1 flex-1 min-w-48">
                <label className="text-xs font-medium text-gray-600 flex items-center gap-1"><Search className="h-3 w-3" /> Search</label>
                <input
                  value={searchQ}
                  onChange={e => setSearchQ(e.target.value)}
                  placeholder="e.g. Medicine, Computer Science…"
                  className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                />
              </div>
            )}
          </div>

          {browseUni && (
            coursesLoading ? (
              <div className="flex justify-center py-12"><Loader2 className="h-6 w-6 animate-spin text-gray-400" /></div>
            ) : filteredCourses.length === 0 ? (
              <div className="rounded-xl border-2 border-dashed border-gray-200 py-12 text-center">
                <p className="text-sm text-gray-500">No courses match your search.</p>
              </div>
            ) : (
              <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
                {filteredCourses.map(course => {
                  const alreadySaved = savedCourseIds.has(course.universityCourseId);
                  const adding = addingCourse === course.universityCourseId;
                  return (
                    <div key={course.universityCourseId} className={`rounded-xl border ${alreadySaved ? "border-emerald-200 bg-emerald-50" : "border-gray-200 bg-white"} p-4 shadow-sm space-y-2`}>
                      <div>
                        <p className="font-semibold text-gray-900 text-sm leading-tight">{course.name}</p>
                        {course.faculty && <p className="text-xs text-gray-500 mt-0.5">{course.faculty}</p>}
                      </div>
                      <div className="flex items-center gap-2 flex-wrap">
                        <span className="text-xs bg-gray-100 text-gray-600 rounded-full px-2 py-0.5 font-medium">APS {course.minimumAps}</span>
                        {course.careerCategory && <span className="text-xs bg-blue-50 text-blue-600 rounded-full px-2 py-0.5">{course.careerCategory}</span>}
                      </div>
                      {course.subjectRequirements.length > 0 && (
                        <div className="text-[11px] text-gray-500 space-y-0.5">
                          {course.subjectRequirements.slice(0, 3).map(r => (
                            <div key={r.subjectName}>• {r.subjectName}{r.minimumPercent ? `: min ${r.minimumPercent}%` : ""}</div>
                          ))}
                        </div>
                      )}
                      <button
                        onClick={() => !alreadySaved && addGoal(course.universityCourseId)}
                        disabled={alreadySaved || adding || goals.length >= 5}
                        className={`w-full text-xs font-medium rounded-lg py-1.5 transition-colors
                          ${alreadySaved
                            ? "bg-emerald-100 text-emerald-700 cursor-default"
                            : goals.length >= 5
                              ? "bg-gray-100 text-gray-400 cursor-not-allowed"
                              : "bg-blue-600 text-white hover:bg-blue-700 disabled:opacity-60"
                          }`}
                      >
                        {adding ? <Loader2 className="h-3 w-3 animate-spin mx-auto" /> : alreadySaved ? "Saved" : "Save goal"}
                      </button>
                    </div>
                  );
                })}
              </div>
            )
          )}
        </div>
      )}
    </div>
  );
}

// ── Tab: Grade 9 Subject Advisor ──────────────────────────────────────────────

import type { Gr9Profile, FetSubjectEligibility } from "@/lib/api";

function Gr9AdvisorTab() {
  const [profile, setProfile] = useState<Gr9Profile | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    api.pathways.gr9Profile()
      .then(setProfile)
      .catch(e => setError(e instanceof Error ? e.message : "Failed to load"))
      .finally(() => setLoading(false));
  }, []);

  if (loading) return <div className="flex justify-center py-16"><Loader2 className="h-8 w-8 animate-spin text-gray-400" /></div>;
  if (error) return <div className="flex items-center gap-2 rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700"><AlertTriangle className="h-4 w-4 shrink-0" />{error}</div>;
  if (!profile?.isGrade9) return null;

  const eligibilityColour = {
    Recommended:    "bg-emerald-50 border-emerald-200 text-emerald-700",
    Borderline:     "bg-amber-50 border-amber-200 text-amber-700",
    NotRecommended: "bg-red-50 border-red-200 text-red-600",
    NoData:         "bg-gray-50 border-gray-200 text-gray-500",
  };

  const eligibilityLabel = {
    Recommended:    "✓ Recommended",
    Borderline:     "~ Borderline",
    NotRecommended: "✗ Not yet",
    NoData:         "No data",
  };

  const grouped: Record<string, FetSubjectEligibility[]> = {
    Recommended:    profile.fetEligibility.filter(e => e.eligibility === "Recommended"),
    Borderline:     profile.fetEligibility.filter(e => e.eligibility === "Borderline"),
    NotRecommended: profile.fetEligibility.filter(e => e.eligibility === "NotRecommended"),
  };

  return (
    <div className="space-y-6">
      {/* Gr 9 marks summary */}
      {profile.marks.length > 0 && (
        <div className="rounded-xl bg-white border border-gray-200 shadow-sm overflow-hidden">
          <div className="bg-gray-50 border-b border-gray-100 px-4 py-2.5">
            <p className="text-xs font-semibold text-gray-500 uppercase tracking-wider">Your Current Grade 9 Marks</p>
          </div>
          <div className="divide-y divide-gray-50">
            {profile.marks.map(m => (
              <div key={m.subjectName} className="flex items-center justify-between px-4 py-2.5">
                <span className="text-sm text-gray-800">{m.subjectName}</span>
                <span className={`text-sm font-bold ${m.averagePercent >= 60 ? "text-emerald-600" : m.averagePercent >= 40 ? "text-amber-600" : "text-red-600"}`}>
                  {m.averagePercent.toFixed(0)}%
                </span>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* FET eligibility columns */}
      <div>
        <p className="text-xs font-semibold text-gray-500 uppercase tracking-wider mb-3">FET Subject Eligibility (Grade 10–12)</p>
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
          {(["Recommended", "Borderline", "NotRecommended"] as const).map(cat => (
            <div key={cat} className="space-y-1.5">
              <p className={`text-xs font-semibold rounded-full px-2.5 py-1 inline-block border ${eligibilityColour[cat]}`}>
                {eligibilityLabel[cat]}
              </p>
              <div className="space-y-1">
                {grouped[cat].length === 0 && (
                  <p className="text-xs text-gray-400 pl-1">None</p>
                )}
                {grouped[cat].map(e => (
                  <div key={e.fetSubject} className={`rounded-lg border px-2.5 py-2 text-xs ${eligibilityColour[e.eligibility]}`}>
                    <p className="font-medium">{e.fetSubject}</p>
                    {e.gr9Subject && e.studentPercent !== undefined && (
                      <p className="opacity-70 mt-0.5">{e.gr9Subject}: {e.studentPercent.toFixed(0)}%</p>
                    )}
                    {e.careerPaths.length > 0 && (
                      <p className="opacity-60 mt-0.5 truncate">{e.careerPaths.slice(0, 2).join(", ")}</p>
                    )}
                  </div>
                ))}
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* AI advice */}
      <Gr9AdvisorCard />
    </div>
  );
}

// ── Tab: Subject Enrolment (existing, unchanged) ─────────────────────────────

function MatrixView() {
  const [classes, setClasses] = useState<Class[]>([]);
  const [classId, setClassId] = useState("");
  const [matrix, setMatrix] = useState<PathwaysMatrix | null>(null);
  const [loading, setLoading] = useState(false);
  const [toggling, setToggling] = useState<string | null>(null);
  const [error, setError] = useState("");

  useEffect(() => {
    api.classes.list({ pageSize: 100 }).then(r => {
      setClasses(r.items);
      if (r.items.length) setClassId(r.items[0].classId);
    }).catch(() => {});
  }, []);

  const loadMatrix = useCallback(async (id: string) => {
    if (!id) return;
    setLoading(true);
    setError("");
    setMatrix(null);
    try {
      setMatrix(await api.pathways.classMatrix(id));
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to load matrix");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { if (classId) loadMatrix(classId); }, [classId, loadMatrix]);

  async function toggle(studentId: string, subjectId: string, enrolledSubjectIds: string[]) {
    if (!matrix) return;
    const key = `${studentId}:${subjectId}`;
    setToggling(key);
    try {
      if (enrolledSubjectIds.includes(subjectId)) {
        const ls = await api.pathways.learnerSubjects(studentId);
        const entry = ls.find(x => x.subjectId === subjectId);
        if (entry) await api.pathways.withdraw(entry.learnerSubjectId);
      } else {
        await api.pathways.enrol({ studentId, subjectId, academicYearId: matrix.academicYearId });
      }
      await loadMatrix(classId);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Operation failed");
    } finally {
      setToggling(null);
    }
  }

  return (
    <div className="space-y-4">
      <div className="flex items-end gap-3 flex-wrap">
        <div className="space-y-1">
          <label className="text-xs font-medium text-gray-600">Class</label>
          <select
            value={classId}
            onChange={e => setClassId(e.target.value)}
            className="rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          >
            {classes.map(c => <option key={c.classId} value={c.classId}>{c.name}</option>)}
          </select>
        </div>
      </div>

      {error && <div className="flex items-center gap-2 rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700"><AlertTriangle className="h-4 w-4 shrink-0" /> {error}</div>}
      {loading && <div className="flex items-center justify-center py-20"><Loader2 className="h-8 w-8 animate-spin text-gray-400" /></div>}

      {matrix && !loading && (
        <div className="space-y-3">
          <p className="text-xs text-gray-500">Showing subject enrolments for {matrix.year} · {matrix.students.length} learner{matrix.students.length !== 1 ? "s" : ""}</p>
          {matrix.subjects.length === 0 && (
            <div className="rounded-xl border-2 border-dashed border-gray-200 py-16 text-center">
              <p className="text-gray-500 text-sm">No subject enrolments recorded for this class yet.</p>
            </div>
          )}
          {matrix.subjects.length > 0 && matrix.students.length > 0 && (
            <div className="overflow-x-auto rounded-xl border border-gray-200 shadow-sm">
              <table className="min-w-full text-sm border-collapse">
                <thead className="bg-gray-50 border-b border-gray-200">
                  <tr>
                    <th className="sticky left-0 bg-gray-50 px-4 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wider min-w-[180px]">Learner</th>
                    {matrix.subjects.map(s => (
                      <th key={s.subjectId} className="px-3 py-3 text-center text-xs font-semibold text-gray-500 uppercase tracking-wider min-w-[110px]">
                        <div>{s.subjectName}</div>
                        {s.capsPhase && <div className="mt-0.5"><PhaseBadge phase={s.capsPhase} /></div>}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100 bg-white">
                  {matrix.students.map(student => (
                    <tr key={student.studentId} className="hover:bg-gray-50">
                      <td className="sticky left-0 bg-inherit px-4 py-3">
                        <p className="font-medium text-gray-900">{student.name}</p>
                        <p className="text-xs text-gray-400">{student.studentNumber}</p>
                      </td>
                      {matrix.subjects.map(subject => {
                        const enrolled = student.enrolledSubjectIds.includes(subject.subjectId);
                        const key = `${student.studentId}:${subject.subjectId}`;
                        const busy = toggling === key;
                        return (
                          <td key={subject.subjectId} className="px-3 py-3 text-center">
                            <button onClick={() => toggle(student.studentId, subject.subjectId, student.enrolledSubjectIds)} disabled={busy} className="inline-flex items-center justify-center w-7 h-7 rounded-full transition-colors disabled:opacity-50">
                              {busy ? <Loader2 className="h-4 w-4 animate-spin text-gray-400" /> : enrolled ? <CheckCircle2 className="h-5 w-5 text-emerald-500 hover:text-red-400 transition-colors" /> : <XCircle className="h-5 w-5 text-gray-200 hover:text-emerald-400 transition-colors" />}
                            </button>
                          </td>
                        );
                      })}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

function MySubjectsView() {
  const [mySubjects, setMySubjects] = useState<LearnerSubjectItem[]>([]);
  const [allSubjects, setAllSubjects] = useState<Subject[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    Promise.all([api.pathways.mySubjects(), api.subjects.list()])
      .then(([mine, all]) => { setMySubjects(mine); setAllSubjects(all); })
      .catch(e => setError(e instanceof Error ? e.message : "Failed to load subjects"))
      .finally(() => setLoading(false));
  }, []);

  if (loading) return <div className="flex justify-center py-20"><Loader2 className="h-8 w-8 animate-spin text-gray-400" /></div>;
  if (error) return <div className="flex items-center gap-2 rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700"><AlertTriangle className="h-4 w-4 shrink-0" /> {error}</div>;

  const enrolledIds = new Set(mySubjects.map(s => s.subjectId));
  const availableFet = allSubjects.filter(s => s.capsPhase === "FET" && !enrolledIds.has(s.subjectId));

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-base font-semibold text-gray-900 mb-3">My Subjects</h2>
        {mySubjects.length === 0 ? (
          <div className="rounded-xl border-2 border-dashed border-gray-200 py-12 text-center">
            <BookOpen className="h-8 w-8 text-gray-200 mx-auto mb-2" />
            <p className="text-sm text-gray-500">You have not been enrolled in any subjects yet.</p>
            <p className="text-xs text-gray-400 mt-1">Contact your teacher or administrator to be enrolled.</p>
          </div>
        ) : (
          <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
            {mySubjects.map(s => (
              <div key={s.learnerSubjectId} className="rounded-lg bg-white border border-gray-200 px-4 py-3 shadow-sm flex items-start gap-3">
                <BookOpen className="h-5 w-5 text-blue-400 mt-0.5 shrink-0" />
                <div className="min-w-0">
                  <p className="font-medium text-gray-900 truncate">{s.subjectName}</p>
                  <div className="flex items-center gap-2 mt-1">
                    <PhaseBadge phase={s.capsPhase} />
                    <span className="text-xs text-gray-400">{s.year}</span>
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
      {availableFet.length > 0 && (
        <div>
          <h2 className="text-base font-semibold text-gray-900 mb-1">Available FET Subjects</h2>
          <p className="text-xs text-gray-500 mb-3">Contact your teacher or administrator to request enrolment.</p>
          <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
            {availableFet.map(s => (
              <div key={s.subjectId} className="rounded-lg bg-gray-50 border border-dashed border-gray-200 px-4 py-3 flex items-start gap-3">
                <BookOpen className="h-5 w-5 text-gray-300 mt-0.5 shrink-0" />
                <div className="min-w-0">
                  <p className="font-medium text-gray-600 truncate">{s.name}</p>
                  <div className="flex items-center gap-2 mt-1">
                    <PhaseBadge phase={s.capsPhase} />
                    {s.code && <span className="text-xs text-gray-400">{s.code}</span>}
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

// ── Page ─────────────────────────────────────────────────────────────────────

type PageTab = "goals" | "enrolment" | "gr9advisor";

export default function PathwaysPage() {
  const router = useRouter();
  const hasPathways = useFeature("pathways");

  const identity = useIdentity(); // Step 8
  const [tab, setTab] = useState<PageTab>("goals");
  const [isGr9, setIsGr9] = useState(false);

  useEffect(() => {
    if (identity === "Learner") {
      api.pathways.gr9Profile().then(p => setIsGr9(p.isGrade9)).catch(() => {});
    }
  }, [identity]);

  if (!hasPathways) {
    return (
      <div className="flex flex-col items-center justify-center h-96 text-center px-4">
        <Route className="h-12 w-12 text-gray-200 mb-4" />
        <h2 className="text-lg font-semibold text-gray-700">Pathways not enabled</h2>
        <p className="text-sm text-gray-400 mt-1">Enable the Pathways feature in Settings.</p>
        <button onClick={() => router.push("/settings")} className="mt-4 text-sm text-blue-600 hover:underline">Go to Settings</button>
      </div>
    );
  }

  const isStaff = identity === "Staff";

  return (
    <div className="p-4 md:p-6 lg:p-8 max-w-6xl mx-auto space-y-6">
      <div>
        <h1 className="text-2xl font-semibold text-gray-900 tracking-tight">Pathways</h1>
        <p className="text-sm text-gray-500 mt-1">
          {isStaff ? "Subject enrolment management" : "Track your career goals and subject progress"}
        </p>
      </div>

      {/* Tab bar — students and staff see different tabs */}
      {identity === "Learner" && (
        <div className="flex gap-1 border-b border-gray-200">
          {([
            { id: "goals",      label: "Career Goals",      icon: Target,         show: true    },
            { id: "enrolment",  label: "Subject Enrolment", icon: BookOpen,       show: true    },
            { id: "gr9advisor", label: "Grade 9 Advisor",   icon: GraduationCap, show: isGr9  },
          ] as { id: PageTab; label: string; icon: React.ComponentType<{ className?: string }>; show: boolean }[])
            .filter(t => t.show)
            .map(({ id, label, icon: Icon }) => (
              <button
                key={id}
                onClick={() => setTab(id)}
                className={`flex items-center gap-1.5 px-4 py-2.5 text-sm font-medium border-b-2 -mb-px transition-colors
                  ${tab === id
                    ? "border-blue-600 text-blue-700"
                    : "border-transparent text-gray-500 hover:text-gray-700"}`}
              >
                <Icon className="h-4 w-4" />
                {label}
              </button>
            ))}
        </div>
      )}

      {/* Tab content */}
      {identity === "Learner" && tab === "goals"      && <CareerGoalsTab />}
      {identity === "Learner" && tab === "enrolment"  && <MySubjectsView />}
      {identity === "Learner" && tab === "gr9advisor" && <Gr9AdvisorTab />}
      {isStaff && <MatrixView />}
    </div>
  );
}
