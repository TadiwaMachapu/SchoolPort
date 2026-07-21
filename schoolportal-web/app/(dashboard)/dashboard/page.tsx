"use client";
import Link from "next/link";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { StatCard } from "@/components/ui/stat-card";
import {
  GraduationCap, Users, BookOpen, CheckCircle2, ClipboardList, Clock,
  BarChart2, Megaphone, Settings, AlertTriangle, ChevronRight,
  CheckSquare, FileCheck, Send, ArrowRight, type LucideIcon,
} from "lucide-react";
import { PageWithRail } from "@/components/ui/page-with-rail";
import { EmptyState } from "@/components/ui/empty-state";
import {
  useMe, useAdminOverview, useMyClasses, usePendingSubmissions,
  useMyAssignments, useMyGrades, useMyCourses, useParentChildren,
  useRecentAnnouncements, useCreateAnnouncement, useMyAcademics,
} from "@/features/dashboard/api/hooks";
import { useState, useEffect } from "react";
import { useRouter } from "next/navigation";
import { useIdentity, usePosition, useAnyPosition, useAuthUser } from "@/lib/auth-context";

export default function DashboardPage() {
  const { data: me, isLoading } = useMe();
  const router = useRouter();

  // Step 8 (P-1): route by identity + positions, not a role string. Finance/IT get a home redirect.
  const identity  = useIdentity();
  const authUser  = useAuthUser(); // server-sourced name (same as the header) — never a stale cache
  const isSMT     = useAnyPosition(["Principal", "DeputyPrincipal"]);
  const isFinance = useAnyPosition(["FinanceManager", "BursarDebtorsClerk", "Cashier"]);
  const isIT      = usePosition("ITAdministrator");

  useEffect(() => {
    if (isFinance) router.replace("/school-pay"); // Finance home
    else if (isIT) router.replace("/users");      // System home
  }, [isFinance, isIT, router]);

  if (isLoading) return <div className="p-8 text-text-muted text-center py-16">Loading…</div>;
  if (!me) return null;

  const user = (me as { user: { firstName: string }; school: { name: string } }).user;
  const school = (me as { user: { firstName: string }; school: { name: string } }).school;

  return (
    <div className="px-4 md:px-6 pb-6">
      {/* Non-staff keep the simple greeting; the teacher view renders its own welcome banner. */}
      {identity !== "Staff" && (
        <div className="mb-5 md:mb-6">
          <h1 className="text-xl md:text-[20px] font-semibold text-text-primary tracking-tight">
            Welcome back, {authUser.firstName || user.firstName}
          </h1>
          <p className="text-xs md:text-sm text-text-secondary mt-0.5">{school.name}</p>
        </div>
      )}
      {isFinance || isIT ? null /* redirecting to workspace home */
        : isSMT               ? <AdminDashboard />
        : identity === "Staff"   ? <TeacherDashboard />
        : identity === "Learner" ? <StudentDashboard />
        : identity === "Parent"  ? <ParentDashboardHome />
        : null}
    </div>
  );
}

/* ─── Admin ─────────────────────────────────────────────────── */
function AdminDashboard() {
  const { data: overview }       = useAdminOverview();
  const { data: announcements }  = useRecentAnnouncements(5);
  const annItems = announcements?.items ?? [];

  return (
    <div className="space-y-5 md:space-y-6">
      {overview && (
        <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-3 md:gap-4">
          <StatCard icon={GraduationCap} label="Students"        value={overview.totalStudents ?? "—"}               color="blue" />
          <StatCard icon={Users}         label="Teachers"        value={overview.totalTeachers ?? "—"}               color="purple" />
          <StatCard icon={BookOpen}      label="Classes"         value={overview.totalClasses ?? "—"}                color="green" />
          <StatCard icon={CheckCircle2}  label="Attendance"      value={`${overview.attendanceRateThisMonth ?? 0}%`} color="teal" />
          <StatCard icon={ClipboardList} label="Assignments"     value={overview.totalAssignments ?? "—"}            color="orange" />
          <StatCard icon={Clock}         label="Pending Grading" value={overview.pendingSubmissions ?? "—"}          color="red" />
        </div>
      )}

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-5 md:gap-6">
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center justify-between">
              Recent Announcements
              <Link href="/announcements" className="text-xs text-primary font-normal hover:underline">View all</Link>
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            {annItems.length === 0 ? (
              <p className="text-sm text-text-muted">No announcements yet</p>
            ) : annItems.map((a) => (
              <div key={a.announcementId} className="border-l-4 border-primary pl-3">
                <p className="text-sm font-medium text-text-primary">{a.title}</p>
                <p className="text-xs text-text-secondary mt-0.5 line-clamp-2">{a.content}</p>
                <p className="text-xs text-text-muted mt-1">{a.createdByName} · {new Date(a.createdAt).toLocaleDateString()}</p>
              </div>
            ))}
          </CardContent>
        </Card>

        <Card>
          <CardHeader><CardTitle>Quick Actions</CardTitle></CardHeader>
          <CardContent className="grid grid-cols-2 gap-3">
            {([
              { label: "Add User",      href: "/users",         Icon: Users },
              { label: "New Class",     href: "/classes",       Icon: BookOpen },
              { label: "Analytics",     href: "/analytics",     Icon: BarChart2 },
              { label: "Announcements", href: "/announcements", Icon: Megaphone },
              { label: "Settings",      href: "/settings",      Icon: Settings },
              { label: "Courses",       href: "/courses",       Icon: BookOpen },
            ] as { label: string; href: string; Icon: LucideIcon }[]).map(a => (
              <Link key={a.label} href={a.href}
                className="flex items-center gap-2 p-3 rounded-lg border border-border hover:border-primary-300 hover:bg-primary-50 active:scale-95 transition-all text-sm font-medium text-text-secondary">
                <a.Icon className="h-4 w-4 text-text-muted shrink-0" />
                {a.label}
                <ChevronRight className="h-3 w-3 text-text-muted ml-auto" />
              </Link>
            ))}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}

/* ─── Teacher ────────────────────────────────────────────────── */
// Saturated feature-card palette for "My classes" — brand primary shades + coral for
// variety (secondary is decorative variety only, never a status signal).
const CLASS_CARD_TONES = [
  "bg-primary-700",
  "bg-primary-500",
  "bg-secondary-500",
];

function TeacherDashboard() {
  const { data: me }           = useMe();
  const authUser               = useAuthUser();
  const { data: classData }    = useMyClasses();
  const { data: pending = [] } = usePendingSubmissions(8);
  const { data: assignData }   = useMyAssignments();
  const [announcement, setAnnouncement] = useState("");
  const [annAudience,  setAnnAudience]  = useState<"All" | "Teachers" | "Students">("All");
  const [posted,       setPosted]       = useState(false);
  const createAnn = useCreateAnnouncement();

  const schoolName = (me as { school?: { name?: string } } | undefined)?.school?.name ?? "";
  const classes     = classData?.items ?? [];
  const assignments = assignData?.items ?? [];
  const upcoming    = assignments
    .filter(a => new Date(a.dueAt) > new Date())
    .sort((a, b) => new Date(a.dueAt).getTime() - new Date(b.dueAt).getTime())
    .slice(0, 5);

  async function postAnnouncement() {
    if (!announcement.trim()) return;
    try {
      await createAnn.mutateAsync({ title: announcement.slice(0, 80), content: announcement, audience: annAudience });
      setAnnouncement("");
      setPosted(true);
      setTimeout(() => setPosted(false), 3000);
    } catch { /* ignore */ }
  }

  const initials = `${authUser.firstName?.[0] ?? ""}${authUser.lastName?.[0] ?? ""}`.toUpperCase();

  return (
    <div className="space-y-4">
      {/* Welcome banner — light primary tint; only the CTA carries solid primary. */}
      <div className="rounded-lg bg-primary-100 px-5 py-4 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3">
        <div className="min-w-0">
          <h1 className="text-lg md:text-xl font-semibold text-primary-900 tracking-tight">
            Welcome back, {authUser.firstName}
          </h1>
          <p className="text-[13px] text-primary-700 mt-0.5">{schoolName} · Ready to capture today&apos;s marks?</p>
        </div>
        <Link href="/gradebook"
          className="inline-flex items-center justify-center gap-2 rounded-pill bg-primary px-4 h-9 text-[13px] font-medium text-white hover:brightness-95 transition-all shrink-0">
          Capture marks <ArrowRight className="h-4 w-4" />
        </Link>
      </div>

      <PageWithRail
        rail={
          <>
            {/* Profile card */}
            <Card>
              <CardContent className="pt-5 flex items-center gap-3">
                <div className="h-12 w-12 rounded-full flex items-center justify-center text-white text-sm font-bold shrink-0 bg-primary">
                  {initials}
                </div>
                <div className="min-w-0">
                  <p className="text-sm font-semibold text-text-primary truncate">{authUser.firstName} {authUser.lastName}</p>
                  <p className="text-xs text-text-secondary truncate">Teacher · {schoolName}</p>
                </div>
              </CardContent>
            </Card>

            {/* Calendar */}
            <Card>
              <CardHeader><CardTitle>Calendar</CardTitle></CardHeader>
              <CardContent><MiniCalendar /></CardContent>
            </Card>

            {/* Needs attention — teacher-scoped signals already loaded on this page.
                (School-wide at-risk lists are oversight-only — analytics.view_school —
                 and belong on the Principal/HOD dashboards, not here.) */}
            <Card>
              <CardHeader><CardTitle>Needs attention</CardTitle></CardHeader>
              <CardContent className="space-y-2">
                {pending.length === 0 && upcoming.length === 0 ? (
                  <EmptyState
                    icon={CheckCircle2}
                    tone="positive"
                    size="compact"
                    heading="All caught up"
                    body="Nothing needs your attention right now."
                  />
                ) : (
                  <>
                    {pending.length > 0 && (
                      <Link href="/assignments" className="flex items-center gap-3 rounded-md p-2.5 hover:bg-surface-subtle transition-colors">
                        <span className="flex h-8 w-8 items-center justify-center rounded-md bg-warning-100 text-warning-700 shrink-0">
                          <FileCheck className="h-4 w-4" />
                        </span>
                        <span className="min-w-0 flex-1 text-[13px] text-text-primary">{pending.length} submission{pending.length > 1 ? "s" : ""} to grade</span>
                        <ChevronRight className="h-4 w-4 text-text-muted" />
                      </Link>
                    )}
                    {upcoming.length > 0 && (
                      <Link href="/assignments" className="flex items-center gap-3 rounded-md p-2.5 hover:bg-surface-subtle transition-colors">
                        <span className="flex h-8 w-8 items-center justify-center rounded-md bg-primary-100 text-primary-700 shrink-0">
                          <ClipboardList className="h-4 w-4" />
                        </span>
                        <span className="min-w-0 flex-1 text-[13px] text-text-primary">{upcoming.length} assessment{upcoming.length > 1 ? "s" : ""} due soon</span>
                        <ChevronRight className="h-4 w-4 text-text-muted" />
                      </Link>
                    )}
                  </>
                )}
              </CardContent>
            </Card>
          </>
        }
      >
       <div className="space-y-4">
        {/* My classes — saturated cards */}
        <section>
          <div className="flex items-center justify-between mb-2.5">
            <h2 className="text-sm font-semibold text-text-primary">My classes</h2>
            <Link href="/classes" className="text-xs text-primary hover:underline">View all</Link>
          </div>
          {classes.length === 0 ? (
            <Card><CardContent className="pt-5"><p className="text-sm text-text-muted">No classes assigned yet</p></CardContent></Card>
          ) : (
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
              {classes.slice(0, 6).map((c, i) => (
                <Link key={c.classId} href={`/attendance?classId=${c.classId}`}
                  className={`group flex items-center justify-between rounded-lg px-4 py-4 text-white transition-all hover:brightness-105 ${CLASS_CARD_TONES[i % CLASS_CARD_TONES.length]}`}>
                  <div className="min-w-0 flex-1 mr-3">
                    <p className="font-semibold truncate text-sm">{c.name}</p>
                    <p className="text-xs text-white/75">{c.studentCount} learners</p>
                  </div>
                  <span className="flex items-center gap-1.5 rounded-pill bg-white/20 px-3 py-1.5 text-xs font-semibold shrink-0 min-h-[32px]">
                    <CheckSquare className="h-3.5 w-3.5" /> Attend
                  </span>
                </Link>
              ))}
            </div>
          )}
        </section>

        {/* KPIs — plain counts are NEUTRAL (surface-subtle grey), not primary-tinted: a
            primary tint would read as green (primary IS green) and collide with success.
            Only "Needs grading" is state-coloured (success when clear, warning when work is
            waiting). Status colours are NEVER decorative — see CLAUDE.md. */}
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
          <StatCard icon={BookOpen}      label="My classes"    value={classes.length}     color="neutral" />
          <StatCard icon={ClipboardList} label="Assignments"   value={assignments.length} color="neutral" />
          <StatCard icon={FileCheck}     label="Needs grading" value={pending.length}     color={pending.length > 0 ? "orange" : "green"}
            trend={pending.length > 0 ? "Waiting for review" : "All caught up"} />
          <StatCard icon={CheckSquare}   label="Upcoming"      value={upcoming.length}    color="neutral" />
        </div>

        {/* Grading queue */}
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center justify-between">
              Needs grading
              {pending.length > 0 && (
                <span className="inline-flex items-center rounded-pill bg-warning-100 px-2.5 py-0.5 text-[11px] font-semibold text-warning-700">
                  {pending.length}
                </span>
              )}
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-1 pb-4">
            {pending.length === 0 ? (
              <EmptyState icon={FileCheck} tone="positive" size="compact" heading="All caught up!" body="No submissions waiting for grades." />
            ) : pending.slice(0, 5).map(s => (
              <Link key={s.submissionId} href={`/assignments/${s.assignmentId}`}
                className="flex items-start justify-between rounded-md px-3 py-2.5 hover:bg-surface-subtle transition-colors group">
                <div className="flex-1 min-w-0 mr-2">
                  <p className="text-[13px] font-medium text-text-primary group-hover:text-primary truncate">{s.studentName}</p>
                  <p className="text-xs text-text-muted truncate">{s.assignmentTitle} · {s.className}</p>
                </div>
                <div className="flex flex-col items-end gap-0.5 shrink-0">
                  <span className="text-xs text-text-muted">
                    {new Date(s.submittedAt).toLocaleDateString("en-ZA", { month: "short", day: "numeric" })}
                  </span>
                  <ChevronRight className="h-3.5 w-3.5 text-text-muted group-hover:text-primary" />
                </div>
              </Link>
            ))}
            {pending.length > 5 && (
              <p className="text-xs text-text-muted px-3 pt-1">+{pending.length - 5} more</p>
            )}
          </CardContent>
        </Card>

        {/* Upcoming assessments — table */}
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center justify-between">
              Upcoming assessments
              <Link href="/assignments" className="text-xs text-primary font-normal hover:underline">All</Link>
            </CardTitle>
          </CardHeader>
          <CardContent className="pb-4">
            {upcoming.length === 0 ? (
              <p className="text-sm text-text-muted py-2 text-center">No upcoming deadlines</p>
            ) : (
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b border-border">
                    <th className="text-left text-[11px] font-semibold text-text-muted uppercase tracking-wider pb-2">Task</th>
                    <th className="text-left text-[11px] font-semibold text-text-muted uppercase tracking-wider pb-2">Class</th>
                    <th className="text-right text-[11px] font-semibold text-text-muted uppercase tracking-wider pb-2">Due</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {upcoming.map((a) => {
                    const days = Math.ceil((new Date(a.dueAt).getTime() - Date.now()) / 86400000);
                    return (
                      <tr key={a.assignmentId} className="hover:bg-surface-subtle transition-colors">
                        <td className="py-2.5 pr-2">
                          <Link href={`/assignments/${a.assignmentId}`} className="text-[13px] font-medium text-text-primary hover:text-primary truncate block max-w-[220px]">{a.title}</Link>
                        </td>
                        <td className="py-2.5 pr-2 text-xs text-text-secondary truncate max-w-[120px]">{a.className}</td>
                        <td className="py-2.5 text-right">
                          <Badge variant={days <= 1 ? "destructive" : days <= 3 ? "warning" : "outline"} className="shrink-0 text-xs">
                            {days === 0 ? "Today" : days === 1 ? "Tomorrow" : `${days}d`}
                          </Badge>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            )}
          </CardContent>
        </Card>

        {/* Quick announcement */}
        <Card>
          <CardHeader><CardTitle>Quick announcement</CardTitle></CardHeader>
          <CardContent className="space-y-3 pb-4">
            <textarea
              value={announcement}
              onChange={e => setAnnouncement(e.target.value)}
              placeholder="Share something with your school…"
              rows={3}
              className="w-full rounded-md border border-transparent bg-surface-subtle px-3 py-2 text-[13px] text-text-primary placeholder:text-text-muted resize-none focus:outline-none focus:ring-2 focus:ring-primary/30"
            />
            <div className="flex items-center gap-2">
              <select value={annAudience} onChange={e => setAnnAudience(e.target.value as "All" | "Teachers" | "Students")}
                className="flex-1 rounded-md border border-transparent bg-surface-subtle px-2 py-1.5 text-xs text-text-secondary focus:outline-none focus:ring-2 focus:ring-primary/30">
                <option value="All">Whole school</option>
                <option value="Teachers">Teachers only</option>
                <option value="Students">Students only</option>
              </select>
              {posted && <span className="text-xs text-success-700 font-medium">Posted!</span>}
              <button
                onClick={postAnnouncement}
                disabled={createAnn.isPending || !announcement.trim()}
                className="flex items-center gap-1.5 rounded-pill bg-primary px-4 py-1.5 text-[13px] font-medium text-white hover:brightness-95 disabled:opacity-40 transition-all shrink-0 min-h-[36px]">
                <Send className="h-3.5 w-3.5" />
                {createAnn.isPending ? "Posting…" : "Post"}
              </button>
            </div>
          </CardContent>
        </Card>
       </div>
      </PageWithRail>
    </div>
  );
}

/* Compact current-month calendar — presentational, today highlighted. No data/behaviour. */
function MiniCalendar() {
  const now = new Date();
  const year = now.getFullYear();
  const month = now.getMonth();
  const first = new Date(year, month, 1);
  const startWeekday = (first.getDay() + 6) % 7; // Monday-first
  const daysInMonth = new Date(year, month + 1, 0).getDate();
  const cells: (number | null)[] = [
    ...Array.from({ length: startWeekday }, () => null),
    ...Array.from({ length: daysInMonth }, (_, i) => i + 1),
  ];
  const monthLabel = now.toLocaleDateString("en-ZA", { month: "long", year: "numeric" });

  return (
    <div>
      <p className="text-[13px] font-semibold text-text-primary mb-2">{monthLabel}</p>
      <div className="grid grid-cols-7 gap-1 text-center">
        {["M", "T", "W", "T", "F", "S", "S"].map((d, i) => (
          <span key={i} className="text-[10px] font-medium text-text-muted py-1">{d}</span>
        ))}
        {cells.map((day, i) => {
          const isToday = day === now.getDate();
          return (
            <span key={i}
              className={`text-xs py-1.5 rounded-md ${
                day == null ? "" : isToday ? "bg-primary text-white font-semibold" : "text-text-secondary"
              }`}>
              {day ?? ""}
            </span>
          );
        })}
      </div>
    </div>
  );
}

/* ─── Student ────────────────────────────────────────────────── */
function StudentDashboard() {
  const { data: academics }        = useMyAcademics();
  const { data: grades = [] }      = useMyGrades();
  const { data: courseData }       = useMyCourses();
  const { data: annData }          = useRecentAnnouncements(3);

  const tasks         = academics?.tasks ?? [];
  const courses       = courseData?.items ?? [];
  const announcements = annData?.items ?? [];

  // Overdue/Upcoming count only OUTSTANDING (not-yet-submitted) tasks — submitted/graded tasks are
  // done, not overdue. Same status-aware source as the My Academics Assignments tab, so the two
  // views always agree.
  const now = Date.now();
  const outstanding = tasks.filter(t => t.status === "not_submitted" && t.dueAt);
  const overdue = outstanding.filter(t => new Date(t.dueAt!).getTime() < now);
  const upcoming = outstanding
    .filter(t => new Date(t.dueAt!).getTime() >= now)
    .sort((a, b) => new Date(a.dueAt!).getTime() - new Date(b.dueAt!).getTime())
    .slice(0, 5);
  const avg = grades.length > 0
    ? Math.round(grades.reduce((s, g) => s + ((g as { percentage?: number }).percentage ?? 0), 0) / grades.length)
    : null;

  return (
    <div className="space-y-5 md:space-y-6">
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 md:gap-4">
        <StatCard icon={BarChart2}     label="Overall Average" value={avg !== null ? `${avg}%` : "—"} color="blue" />
        <StatCard icon={CheckCircle2}  label="Graded"          value={grades.length}                  color="green" />
        <StatCard icon={ClipboardList} label="Upcoming"        value={upcoming.length}                color="purple" />
        <StatCard icon={AlertTriangle} label="Overdue"         value={overdue.length}                 color={overdue.length > 0 ? "red" : "teal"} />
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-5 md:gap-6">
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center justify-between">
              Upcoming Assignments
              <Link href="/assignments" className="text-xs text-primary font-normal hover:underline">View all</Link>
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-2">
            {overdue.length > 0 && (
              <div className="bg-danger-100 rounded-lg px-3 py-2 mb-3">
                <p className="text-xs font-medium text-danger-700">{overdue.length} overdue assignment{overdue.length > 1 ? "s" : ""}</p>
              </div>
            )}
            {upcoming.length === 0 ? (
              <p className="text-sm text-text-muted">No upcoming assignments</p>
            ) : upcoming.map((t) => {
              const days = Math.ceil((new Date(t.dueAt!).getTime() - Date.now()) / 86400000);
              return (
                <Link key={`${t.source}-${t.taskId}`} href={t.source === "assignment" ? `/assignments/${t.taskId}` : "/quizzes"}
                  className="flex items-center justify-between p-3 rounded-lg hover:bg-surface-subtle active:scale-98 transition-all">
                  <div>
                    <p className="text-sm font-medium text-text-primary">{t.title}</p>
                    <p className="text-xs text-text-muted">{t.subjectName}</p>
                  </div>
                  <Badge variant={days <= 2 ? "destructive" : days <= 5 ? "warning" : "outline"} className="text-xs shrink-0">
                    {days === 0 ? "Today" : days === 1 ? "Tomorrow" : `${days}d`}
                  </Badge>
                </Link>
              );
            })}
          </CardContent>
        </Card>

        <div className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center justify-between">
                Recent Grades
                <Link href="/gradebook" className="text-xs text-primary font-normal hover:underline">View all</Link>
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-2">
              {(grades as { gradeId: string; assignmentTitle: string; subject: string; percentage: number; score: number; maxMarks: number }[]).slice(0, 4).map((g) => (
                <div key={g.gradeId} className="flex items-center justify-between py-1">
                  <div>
                    <p className="text-sm font-medium text-text-primary truncate max-w-[180px]">{g.assignmentTitle}</p>
                    <p className="text-xs text-text-muted">{g.subject}</p>
                  </div>
                  <div className="text-right shrink-0">
                    <p className="text-sm font-bold text-text-primary">{g.percentage}%</p>
                    <p className="text-xs text-text-muted">{g.score}/{g.maxMarks}</p>
                  </div>
                </div>
              ))}
              {grades.length === 0 && <p className="text-sm text-text-muted">No grades yet</p>}
            </CardContent>
          </Card>

          {announcements.length > 0 && (
            <Card>
              <CardHeader><CardTitle>Announcements</CardTitle></CardHeader>
              <CardContent className="space-y-2">
                {announcements.map((a) => (
                  <div key={a.announcementId} className="border-l-2 border-primary-300 pl-2">
                    <p className="text-sm font-medium text-text-primary">{a.title}</p>
                    <p className="text-xs text-text-muted">{new Date(a.createdAt).toLocaleDateString()}</p>
                  </div>
                ))}
              </CardContent>
            </Card>
          )}
        </div>
      </div>

      {courses.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center justify-between">
              Courses
              <Link href="/courses" className="text-xs text-primary font-normal hover:underline">View all</Link>
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
              {courses.map((c) => (
                <Link key={c.courseId} href={`/courses/${c.courseId}`}
                  className="group rounded-lg border border-border overflow-hidden hover:border-primary-300 hover:shadow-card active:scale-98 transition-all">
                  <div className="h-20 bg-primary flex items-center justify-center">
                    <BookOpen className="h-8 w-8 text-white/80" />
                  </div>
                  <div className="p-2">
                    <p className="text-xs font-medium text-text-primary truncate group-hover:text-primary">{c.title}</p>
                    <p className="text-[10px] text-text-muted">{c.lessonCount} lessons</p>
                  </div>
                </Link>
              ))}
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}

/* ─── Parent ─────────────────────────────────────────────────── */
function ParentDashboardHome() {
  const { data: children = [] } = useParentChildren();

  return (
    <div className="space-y-5 md:space-y-6 max-w-2xl">
      <Card>
        <CardHeader><CardTitle>Your Children</CardTitle></CardHeader>
        <CardContent className="space-y-3">
          {children.length === 0 ? (
            <p className="text-sm text-text-muted">No children linked to your account. Contact the school administrator.</p>
          ) : (children as { studentId: string; name: string; studentNumber: string; gradeLevel?: number }[]).map((child) => (
            <Link key={child.studentId} href="/parent"
              className="flex items-center justify-between p-4 rounded-lg border border-border hover:border-primary-300 hover:bg-primary-50 active:scale-98 transition-all group">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-full bg-primary-100 text-primary-700 font-bold flex items-center justify-center text-sm shrink-0">
                  {child.name.split(" ").map((n: string) => n[0]).join("").slice(0, 2)}
                </div>
                <div>
                  <p className="font-medium text-text-primary group-hover:text-primary">{child.name}</p>
                  <p className="text-xs text-text-muted">{child.studentNumber}{child.gradeLevel ? ` · Grade ${child.gradeLevel}` : ""}</p>
                </div>
              </div>
              <ChevronRight className="h-4 w-4 text-text-muted group-hover:text-primary" />
            </Link>
          ))}
        </CardContent>
      </Card>
      <p className="text-sm text-text-secondary">
        Visit the <Link href="/parent" className="text-primary hover:underline">Parent Portal</Link> for detailed reports.
      </p>
    </div>
  );
}
