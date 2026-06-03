"use client";
import Link from "next/link";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { StatCard } from "@/components/ui/stat-card";
import {
  GraduationCap, Users, BookOpen, CheckCircle2, ClipboardList, Clock,
  BarChart2, Megaphone, Settings, AlertTriangle, ChevronRight,
  CheckSquare, FileCheck, Send, type LucideIcon,
} from "lucide-react";
import {
  useMe, useAdminOverview, useMyClasses, usePendingSubmissions,
  useMyAssignments, useMyGrades, useMyCourses, useParentChildren,
  useRecentAnnouncements, useCreateAnnouncement,
} from "@/features/dashboard/api/hooks";
import { useState } from "react";

export default function DashboardPage() {
  const { data: me, isLoading } = useMe();

  if (isLoading) return <div className="p-8 text-gray-400 text-center py-16">Loading…</div>;
  if (!me) return null;

  const role = (me as { user: { role: string; firstName: string; lastName: string; userId: string }; school: { name: string } }).user.role;
  const user = (me as { user: { role: string; firstName: string; lastName: string; userId: string }; school: { name: string } }).user;
  const school = (me as { user: { role: string; firstName: string; lastName: string; userId: string }; school: { name: string } }).school;

  return (
    <div className="p-4 md:p-6 lg:p-8">
      <div className="mb-5 md:mb-6">
        <h1 className="text-xl md:text-2xl font-semibold text-gray-900 tracking-tight">
          Welcome back, {user.firstName}
        </h1>
        <p className="text-xs md:text-sm text-gray-500 mt-0.5">{school.name}</p>
      </div>
      {role === "Admin"   && <AdminDashboard />}
      {role === "Teacher" && <TeacherDashboard />}
      {role === "Student" && <StudentDashboard />}
      {role === "Parent"  && <ParentDashboardHome />}
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
              <Link href="/announcements" className="text-xs text-blue-600 font-normal hover:underline">View all</Link>
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            {annItems.length === 0 ? (
              <p className="text-sm text-gray-400">No announcements yet</p>
            ) : annItems.map((a) => (
              <div key={a.announcementId} className="border-l-4 border-blue-400 pl-3">
                <p className="text-sm font-medium text-gray-900">{a.title}</p>
                <p className="text-xs text-gray-500 mt-0.5 line-clamp-2">{a.content}</p>
                <p className="text-xs text-gray-400 mt-1">{a.createdByName} · {new Date(a.createdAt).toLocaleDateString()}</p>
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
                className="flex items-center gap-2 p-3 rounded-lg border border-gray-100 hover:border-blue-300 hover:bg-blue-50 active:scale-95 transition-all text-sm font-medium text-gray-700">
                <a.Icon className="h-4 w-4 text-gray-400 shrink-0" />
                {a.label}
                <ChevronRight className="h-3 w-3 text-gray-300 ml-auto" />
              </Link>
            ))}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}

/* ─── Teacher ────────────────────────────────────────────────── */
function TeacherDashboard() {
  const { data: classData }    = useMyClasses();
  const { data: pending = [] } = usePendingSubmissions(8);
  const { data: assignData }   = useMyAssignments();
  const [announcement, setAnnouncement] = useState("");
  const [annAudience,  setAnnAudience]  = useState<"All" | "Teachers" | "Students">("All");
  const [posted,       setPosted]       = useState(false);
  const createAnn = useCreateAnnouncement();

  const classes     = classData?.items ?? [];
  const assignments = assignData?.items ?? [];
  const upcoming    = assignments
    .filter(a => new Date(a.dueAt) > new Date())
    .sort((a, b) => new Date(a.dueAt).getTime() - new Date(b.dueAt).getTime())
    .slice(0, 4);

  const today = new Date().toLocaleDateString("en-US", { weekday: "long", month: "long", day: "numeric" });

  async function postAnnouncement() {
    if (!announcement.trim()) return;
    try {
      await createAnn.mutateAsync({ title: announcement.slice(0, 80), content: announcement, audience: annAudience });
      setAnnouncement("");
      setPosted(true);
      setTimeout(() => setPosted(false), 3000);
    } catch { /* ignore */ }
  }

  return (
    <div className="space-y-5 md:space-y-6">
      {/* Today's Classes — quick-attend */}
      <div>
        <div className="flex items-center justify-between mb-3">
          <div>
            <p className="text-xs font-semibold text-gray-400 uppercase tracking-wider">{today}</p>
            <h2 className="text-base font-semibold text-gray-900 mt-0.5">Today&apos;s Classes</h2>
          </div>
          <Link href="/classes" className="text-xs text-blue-600 hover:underline">View all</Link>
        </div>
        {classes.length === 0 ? (
          <p className="text-sm text-gray-400 py-2">No classes assigned yet</p>
        ) : (
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
            {classes.slice(0, 6).map((c) => (
              <Link key={c.classId} href={`/attendance?classId=${c.classId}`}
                className="flex items-center justify-between rounded-xl border border-gray-200 bg-white px-4 py-3 hover:border-emerald-400 hover:shadow-sm active:scale-98 transition-all group">
                <div className="min-w-0 flex-1 mr-3">
                  <p className="font-medium text-gray-900 group-hover:text-emerald-700 truncate text-sm">{c.name}</p>
                  <p className="text-xs text-gray-400">{c.studentCount} students</p>
                </div>
                <div className="flex items-center gap-1.5 rounded-lg bg-emerald-50 px-3 py-1.5 text-xs font-semibold text-emerald-700 group-hover:bg-emerald-100 shrink-0 transition-colors min-h-[36px]">
                  <CheckSquare className="h-3.5 w-3.5" />
                  Attend
                </div>
              </Link>
            ))}
          </div>
        )}
      </div>

      {/* KPIs */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 md:gap-4">
        <StatCard icon={BookOpen}      label="My Classes"    value={classes.length}     color="blue" />
        <StatCard icon={ClipboardList} label="Assignments"   value={assignments.length} color="purple" />
        <StatCard icon={FileCheck}     label="Needs Grading" value={pending.length}     color="orange"
          trend={pending.length > 0 ? "Waiting for review" : "All caught up"} />
        <StatCard icon={CheckSquare}   label="Upcoming"      value={upcoming.length}    color="green" />
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-5 md:gap-6">
        {/* Grading queue */}
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center justify-between">
              Needs Grading
              {pending.length > 0 && (
                <span className="inline-flex items-center rounded-full bg-orange-100 px-2 py-0.5 text-xs font-semibold text-orange-700">
                  {pending.length}
                </span>
              )}
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-1 pb-4">
            {pending.length === 0 ? (
              <div className="flex flex-col items-center gap-2 py-6 text-center">
                <FileCheck className="h-8 w-8 text-emerald-300" />
                <p className="text-sm font-medium text-gray-500">All caught up!</p>
                <p className="text-xs text-gray-400">No submissions waiting for grades</p>
              </div>
            ) : pending.slice(0, 5).map(s => (
              <Link key={s.submissionId} href={`/assignments/${s.assignmentId}`}
                className="flex items-start justify-between rounded-lg px-3 py-2.5 hover:bg-gray-50 transition-colors group">
                <div className="flex-1 min-w-0 mr-2">
                  <p className="text-sm font-medium text-gray-900 group-hover:text-blue-600 truncate">{s.studentName}</p>
                  <p className="text-xs text-gray-400 truncate">{s.assignmentTitle} · {s.className}</p>
                </div>
                <div className="flex flex-col items-end gap-0.5 shrink-0">
                  <span className="text-xs text-gray-400">
                    {new Date(s.submittedAt).toLocaleDateString("en-US", { month: "short", day: "numeric" })}
                  </span>
                  <ChevronRight className="h-3.5 w-3.5 text-gray-300 group-hover:text-blue-400" />
                </div>
              </Link>
            ))}
            {pending.length > 5 && (
              <p className="text-xs text-gray-400 px-3 pt-1">+{pending.length - 5} more</p>
            )}
          </CardContent>
        </Card>

        <div className="space-y-4">
          {/* Quick announcement */}
          <Card>
            <CardHeader><CardTitle>Quick Announcement</CardTitle></CardHeader>
            <CardContent className="space-y-3 pb-4">
              <textarea
                value={announcement}
                onChange={e => setAnnouncement(e.target.value)}
                placeholder="Share something with your school…"
                rows={3}
                className="w-full rounded-lg border border-gray-200 px-3 py-2 text-sm text-gray-800 placeholder:text-gray-400 resize-none focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
              <div className="flex items-center gap-2">
                <select value={annAudience} onChange={e => setAnnAudience(e.target.value as "All" | "Teachers" | "Students")}
                  className="flex-1 rounded-md border border-gray-200 px-2 py-1.5 text-xs text-gray-700 focus:outline-none focus:ring-2 focus:ring-blue-500">
                  <option value="All">Whole school</option>
                  <option value="Teachers">Teachers only</option>
                  <option value="Students">Students only</option>
                </select>
                {posted && <span className="text-xs text-emerald-600 font-medium">Posted!</span>}
                <button
                  onClick={postAnnouncement}
                  disabled={createAnn.isPending || !announcement.trim()}
                  className="flex items-center gap-1.5 rounded-lg bg-blue-600 px-4 py-1.5 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-40 transition-colors shrink-0 min-h-[36px]">
                  <Send className="h-3.5 w-3.5" />
                  {createAnn.isPending ? "Posting…" : "Post"}
                </button>
              </div>
            </CardContent>
          </Card>

          {/* Upcoming assignments */}
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center justify-between">
                Upcoming Due
                <Link href="/assignments" className="text-xs text-blue-600 font-normal hover:underline">All</Link>
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-1 pb-4">
              {upcoming.length === 0 ? (
                <p className="text-sm text-gray-400 py-2 text-center">No upcoming deadlines</p>
              ) : upcoming.map((a) => {
                const days = Math.ceil((new Date(a.dueAt).getTime() - Date.now()) / 86400000);
                return (
                  <Link key={a.assignmentId} href={`/assignments/${a.assignmentId}`}
                    className="flex items-center justify-between rounded-lg px-3 py-2.5 hover:bg-gray-50 transition-colors">
                    <div className="min-w-0 flex-1 mr-2">
                      <p className="text-sm font-medium text-gray-900 truncate">{a.title}</p>
                      <p className="text-xs text-gray-400 truncate">{a.className}</p>
                    </div>
                    <Badge variant={days <= 1 ? "destructive" : days <= 3 ? "warning" : "outline"} className="shrink-0 text-xs">
                      {days === 0 ? "Today" : days === 1 ? "Tomorrow" : `${days}d`}
                    </Badge>
                  </Link>
                );
              })}
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}

/* ─── Student ────────────────────────────────────────────────── */
function StudentDashboard() {
  const { data: assignData }       = useMyAssignments();
  const { data: grades = [] }      = useMyGrades();
  const { data: courseData }       = useMyCourses();
  const { data: annData }          = useRecentAnnouncements(3);

  const assignments   = assignData?.items ?? [];
  const courses       = courseData?.items ?? [];
  const announcements = annData?.items ?? [];

  const upcoming = assignments
    .filter(a => new Date(a.dueAt) > new Date())
    .sort((a, b) => new Date(a.dueAt).getTime() - new Date(b.dueAt).getTime())
    .slice(0, 5);
  const overdue = assignments.filter(a => new Date(a.dueAt) < new Date());
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
              <Link href="/assignments" className="text-xs text-blue-600 font-normal hover:underline">View all</Link>
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-2">
            {overdue.length > 0 && (
              <div className="bg-red-50 border border-red-200 rounded-lg px-3 py-2 mb-3">
                <p className="text-xs font-medium text-red-700">{overdue.length} overdue assignment{overdue.length > 1 ? "s" : ""}</p>
              </div>
            )}
            {upcoming.length === 0 ? (
              <p className="text-sm text-gray-400">No upcoming assignments</p>
            ) : upcoming.map((a) => {
              const days = Math.ceil((new Date(a.dueAt).getTime() - Date.now()) / 86400000);
              return (
                <Link key={a.assignmentId} href={`/assignments/${a.assignmentId}`}
                  className="flex items-center justify-between p-3 rounded-lg hover:bg-gray-50 active:scale-98 transition-all">
                  <div>
                    <p className="text-sm font-medium text-gray-900">{a.title}</p>
                    <p className="text-xs text-gray-400">{a.className} · {a.subjectName}</p>
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
                <Link href="/gradebook" className="text-xs text-blue-600 font-normal hover:underline">View all</Link>
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-2">
              {(grades as { gradeId: string; assignmentTitle: string; subject: string; percentage: number; score: number; maxMarks: number }[]).slice(0, 4).map((g) => (
                <div key={g.gradeId} className="flex items-center justify-between py-1">
                  <div>
                    <p className="text-sm font-medium text-gray-900 truncate max-w-[180px]">{g.assignmentTitle}</p>
                    <p className="text-xs text-gray-400">{g.subject}</p>
                  </div>
                  <div className="text-right shrink-0">
                    <p className="text-sm font-bold text-gray-900">{g.percentage}%</p>
                    <p className="text-xs text-gray-400">{g.score}/{g.maxMarks}</p>
                  </div>
                </div>
              ))}
              {grades.length === 0 && <p className="text-sm text-gray-400">No grades yet</p>}
            </CardContent>
          </Card>

          {announcements.length > 0 && (
            <Card>
              <CardHeader><CardTitle>Announcements</CardTitle></CardHeader>
              <CardContent className="space-y-2">
                {announcements.map((a) => (
                  <div key={a.announcementId} className="border-l-2 border-blue-300 pl-2">
                    <p className="text-sm font-medium text-gray-900">{a.title}</p>
                    <p className="text-xs text-gray-400">{new Date(a.createdAt).toLocaleDateString()}</p>
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
              <Link href="/courses" className="text-xs text-blue-600 font-normal hover:underline">View all</Link>
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
              {courses.map((c) => (
                <Link key={c.courseId} href={`/courses/${c.courseId}`}
                  className="group rounded-lg border border-gray-200 overflow-hidden hover:border-blue-400 hover:shadow-sm active:scale-98 transition-all">
                  <div className="h-20 bg-gradient-to-br from-blue-500 to-purple-600 flex items-center justify-center">
                    <BookOpen className="h-8 w-8 text-white/80" />
                  </div>
                  <div className="p-2">
                    <p className="text-xs font-medium text-gray-900 truncate group-hover:text-blue-600">{c.title}</p>
                    <p className="text-[10px] text-gray-400">{c.lessonCount} lessons</p>
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
            <p className="text-sm text-gray-400">No children linked to your account. Contact the school administrator.</p>
          ) : (children as { studentId: string; name: string; studentNumber: string; gradeLevel?: number }[]).map((child) => (
            <Link key={child.studentId} href="/parent"
              className="flex items-center justify-between p-4 rounded-lg border border-gray-200 hover:border-blue-400 hover:bg-blue-50 active:scale-98 transition-all group">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-full bg-blue-100 text-blue-700 font-bold flex items-center justify-center text-sm shrink-0">
                  {child.name.split(" ").map((n: string) => n[0]).join("").slice(0, 2)}
                </div>
                <div>
                  <p className="font-medium text-gray-900 group-hover:text-blue-700">{child.name}</p>
                  <p className="text-xs text-gray-400">{child.studentNumber}{child.gradeLevel ? ` · Grade ${child.gradeLevel}` : ""}</p>
                </div>
              </div>
              <ChevronRight className="h-4 w-4 text-gray-300 group-hover:text-blue-500" />
            </Link>
          ))}
        </CardContent>
      </Card>
      <p className="text-sm text-gray-500">
        Visit the <Link href="/parent" className="text-blue-600 hover:underline">Parent Portal</Link> for detailed reports.
      </p>
    </div>
  );
}
