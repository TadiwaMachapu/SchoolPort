"use client";
import { useEffect, useState } from "react";
import Link from "next/link";
import { api } from "@/lib/api";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { StatCard } from "@/components/ui/stat-card";
import {
  GraduationCap,
  Users,
  BookOpen,
  CheckCircle2,
  ClipboardList,
  Clock,
  BarChart2,
  Megaphone,
  Settings,
  AlertTriangle,
  ChevronRight,
  type LucideIcon,
} from "lucide-react";

interface MeData {
  user: { userId: string; email: string; firstName: string; lastName: string; role: string };
  school: { schoolId: string; name: string; logoUrl?: string; primaryColor?: string };
}

export default function DashboardPage() {
  const [me, setMe] = useState<MeData | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.me.get().then(d => setMe(d as MeData)).catch(() => {}).finally(() => setLoading(false));
  }, []);

  if (loading) return <div className="p-8 text-gray-400 text-center py-16">Loading…</div>;
  if (!me) return null;

  const role = me.user.role;

  return (
    <div className="p-6 lg:p-8">
      <div className="mb-6">
        <h1 className="text-2xl font-semibold text-gray-900 tracking-tight">
          Welcome back, {me.user.firstName}
        </h1>
        <p className="text-sm text-gray-500 mt-1">{me.school.name}</p>
      </div>
      {role === "Admin"   && <AdminDashboard />}
      {role === "Teacher" && <TeacherDashboard userId={me.user.userId} />}
      {role === "Student" && <StudentDashboard />}
      {role === "Parent"  && <ParentDashboardHome />}
    </div>
  );
}

/* ─── Admin ─────────────────────────────────────────────────── */
function AdminDashboard() {
  const [overview, setOverview] = useState<any>(null);
  const [announcements, setAnnouncements] = useState<any[]>([]);

  useEffect(() => {
    api.analytics.overview().then(setOverview).catch(() => {});
    api.announcements.list({ pageSize: 5 }).then(r => setAnnouncements((r as any).items ?? [])).catch(() => {});
  }, []);

  return (
    <div className="space-y-6">
      {overview && (
        <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-4">
          <StatCard icon={GraduationCap} label="Students"        value={overview.totalStudents ?? "—"}               color="blue" />
          <StatCard icon={Users}         label="Teachers"        value={overview.totalTeachers ?? "—"}               color="purple" />
          <StatCard icon={BookOpen}      label="Classes"         value={overview.totalClasses ?? "—"}                color="green" />
          <StatCard icon={CheckCircle2}  label="Attendance"      value={`${overview.attendanceRateThisMonth ?? 0}%`} color="teal" />
          <StatCard icon={ClipboardList} label="Assignments"     value={overview.totalAssignments ?? "—"}            color="orange" />
          <StatCard icon={Clock}         label="Pending Grading" value={overview.pendingSubmissions ?? "—"}          color="red" />
        </div>
      )}

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center justify-between">
              Recent Announcements
              <Link href="/announcements" className="text-xs text-blue-600 font-normal hover:underline">View all</Link>
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            {announcements.length === 0 ? (
              <p className="text-sm text-gray-400">No announcements yet</p>
            ) : announcements.map((a: any) => (
              <div key={a.announcementId} className="border-l-4 border-blue-400 pl-3">
                <p className="text-sm font-medium text-gray-900">{a.title}</p>
                <p className="text-xs text-gray-500 mt-0.5 line-clamp-2">{a.content}</p>
                <p className="text-xs text-gray-400 mt-1">{a.createdByName} · {new Date(a.createdAt).toLocaleDateString()}</p>
              </div>
            ))}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Quick Actions</CardTitle>
          </CardHeader>
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
                className="flex items-center gap-2 p-3 rounded-lg border border-gray-100 hover:border-blue-300 hover:bg-blue-50 transition-colors text-sm font-medium text-gray-700">
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
function TeacherDashboard({ userId }: { userId: string }) {
  const [classes, setClasses] = useState<any[]>([]);
  const [assignments, setAssignments] = useState<any[]>([]);
  const [announcements, setAnnouncements] = useState<any[]>([]);

  useEffect(() => {
    api.classes.list({ pageSize: 20 }).then(r => setClasses((r as any).items ?? [])).catch(() => {});
    api.assignments.list({ pageSize: 10 }).then(r => setAssignments((r as any).items ?? [])).catch(() => {});
    api.announcements.list({ pageSize: 3 }).then(r => setAnnouncements((r as any).items ?? [])).catch(() => {});
  }, []);

  const upcoming = assignments.filter(a => new Date(a.dueAt) > new Date()).sort((a, b) => new Date(a.dueAt).getTime() - new Date(b.dueAt).getTime()).slice(0, 5);
  const overdue = assignments.filter(a => new Date(a.dueAt) < new Date()).length;

  return (
    <div className="space-y-6">
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
        <StatCard icon={BookOpen}      label="My Classes"    value={classes.length}       color="blue" />
        <StatCard icon={ClipboardList} label="Assignments"   value={assignments.length}   color="purple" />
        <StatCard icon={AlertTriangle} label="Overdue"       value={overdue}              color="red" />
        <StatCard icon={Megaphone}     label="Announcements" value={announcements.length} color="green" />
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center justify-between">
              My Classes
              <Link href="/classes" className="text-xs text-blue-600 font-normal hover:underline">View all</Link>
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-2">
            {classes.slice(0, 5).map((c: any) => (
              <Link key={c.classId} href={`/classes/${c.classId}`}
                className="flex items-center justify-between p-3 rounded-lg hover:bg-gray-50 transition-colors group">
                <div>
                  <p className="text-sm font-medium text-gray-900 group-hover:text-blue-600">{c.name}</p>
                  <p className="text-xs text-gray-400">{c.studentCount} students{c.gradeLevel ? ` · Grade ${c.gradeLevel}` : ""}</p>
                </div>
                <ChevronRight className="h-4 w-4 text-gray-300 group-hover:text-blue-400" />
              </Link>
            ))}
            {classes.length === 0 && <p className="text-sm text-gray-400">No classes assigned</p>}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="flex items-center justify-between">
              Upcoming Assignments
              <Link href="/assignments" className="text-xs text-blue-600 font-normal hover:underline">View all</Link>
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-2">
            {upcoming.length === 0 ? (
              <p className="text-sm text-gray-400">No upcoming assignments</p>
            ) : upcoming.map((a: any) => {
              const days = Math.ceil((new Date(a.dueAt).getTime() - Date.now()) / 86400000);
              return (
                <Link key={a.assignmentId} href={`/assignments/${a.assignmentId}`}
                  className="flex items-center justify-between p-3 rounded-lg hover:bg-gray-50 transition-colors">
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
      </div>
    </div>
  );
}

/* ─── Student ────────────────────────────────────────────────── */
function StudentDashboard() {
  const [assignments, setAssignments] = useState<any[]>([]);
  const [grades, setGrades] = useState<any[]>([]);
  const [courses, setCourses] = useState<any[]>([]);
  const [announcements, setAnnouncements] = useState<any[]>([]);

  useEffect(() => {
    api.assignments.list({ pageSize: 20 }).then(r => setAssignments((r as any).items ?? [])).catch(() => {});
    api.gradebook.myGrades().then(g => setGrades(g as any[])).catch(() => {});
    api.courses.list({ pageSize: 6, publishedOnly: true }).then(r => setCourses((r as any).items ?? [])).catch(() => {});
    api.announcements.list({ pageSize: 3 }).then(r => setAnnouncements((r as any).items ?? [])).catch(() => {});
  }, []);

  const upcoming = assignments
    .filter(a => new Date(a.dueAt) > new Date())
    .sort((a, b) => new Date(a.dueAt).getTime() - new Date(b.dueAt).getTime())
    .slice(0, 5);
  const overdue = assignments.filter(a => new Date(a.dueAt) < new Date());
  const avg = grades.length > 0 ? Math.round(grades.reduce((s, g) => s + (g.percentage ?? 0), 0) / grades.length) : null;

  return (
    <div className="space-y-6">
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
        <StatCard icon={BarChart2}     label="Overall Average" value={avg !== null ? `${avg}%` : "—"} color="blue" />
        <StatCard icon={CheckCircle2}  label="Graded"          value={grades.length}                  color="green" />
        <StatCard icon={ClipboardList} label="Upcoming"        value={upcoming.length}                color="purple" />
        <StatCard icon={AlertTriangle} label="Overdue"         value={overdue.length}                 color={overdue.length > 0 ? "red" : "teal"} />
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
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
            ) : upcoming.map((a: any) => {
              const days = Math.ceil((new Date(a.dueAt).getTime() - Date.now()) / 86400000);
              return (
                <Link key={a.assignmentId} href={`/assignments/${a.assignmentId}`}
                  className="flex items-center justify-between p-3 rounded-lg hover:bg-gray-50 transition-colors">
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
              {grades.slice(0, 4).map((g: any) => (
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
                {announcements.map((a: any) => (
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
              {courses.map((c: any) => (
                <Link key={c.courseId} href={`/courses/${c.courseId}`}
                  className="group rounded-lg border border-gray-200 overflow-hidden hover:border-blue-400 hover:shadow-sm transition-all">
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

/* ─── Parent (summary) ──────────────────────────────────────── */
function ParentDashboardHome() {
  const [children, setChildren] = useState<any[]>([]);

  useEffect(() => {
    api.parent.children().then(c => setChildren(c as any[])).catch(() => {});
  }, []);

  return (
    <div className="space-y-6 max-w-2xl">
      <Card>
        <CardHeader>
          <CardTitle>Your Children</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {children.length === 0 ? (
            <p className="text-sm text-gray-400">No children linked to your account. Contact the school administrator.</p>
          ) : children.map((child: any) => (
            <Link key={child.studentId} href="/parent"
              className="flex items-center justify-between p-4 rounded-lg border border-gray-200 hover:border-blue-400 hover:bg-blue-50 transition-colors group">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-full bg-blue-100 text-blue-700 font-bold flex items-center justify-center text-sm">
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
