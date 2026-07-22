"use client";
import { useEffect, useState } from "react";
import { api } from "@/lib/api";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import {
  BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer,
  PieChart, Pie, Cell, LineChart, Line, Legend
} from "recharts";
import { GraduationCap, Users, BookOpen, CheckCircle2, TrendingUp, AlertTriangle } from "lucide-react";

interface Overview {
  totalStudents: number;
  totalTeachers: number;
  totalClasses: number;
  totalCourses: number;
  totalAssignments: number;
  pendingSubmissions: number;
  attendanceRateThisMonth: number;
}

interface GradeDistribution {
  aPlus: number; a: number; b: number; c: number; d: number; f: number;
  average: number; total: number;
}

interface AtRiskStudent {
  studentId: string;
  name: string;
  studentNumber: string;
  attendanceRate: number;
  risk: string;
}

interface ClassPerformance {
  classId: string;
  name: string;
  studentCount: number;
  averageGrade?: number;
}

const GRADE_COLORS = ["#22c55e", "#86efac", "#3b82f6", "#f59e0b", "#f97316", "#ef4444"];

export default function AnalyticsPage() {
  const [overview, setOverview] = useState<Overview | null>(null);
  const [gradeDist, setGradeDist] = useState<GradeDistribution | null>(null);
  const [atRisk, setAtRisk] = useState<AtRiskStudent[]>([]);
  const [classPerf, setClassPerf] = useState<ClassPerformance[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    Promise.allSettled([
      api.analytics.overview(),
      api.analytics.gradeDistribution(),
      api.analytics.atRiskStudents(),
      api.analytics.classPerformance(),
    ]).then(([ov, gd, ar, cp]) => {
      if (ov.status === "fulfilled") setOverview(ov.value as Overview);
      if (gd.status === "fulfilled") setGradeDist(gd.value as GradeDistribution);
      if (ar.status === "fulfilled") setAtRisk(ar.value as AtRiskStudent[]);
      if (cp.status === "fulfilled") setClassPerf(cp.value as ClassPerformance[]);
    }).catch(e => setError(String(e)))
      .finally(() => setLoading(false));
  }, []);

  if (loading) return (
    <div className="p-8 space-y-6">
      <div className="h-8 w-48 animate-pulse rounded-md bg-surface-subtle" />
      <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
        {[1,2,3,4].map(i => <div key={i} className="h-24 animate-pulse rounded-lg bg-surface-subtle" />)}
      </div>
      <div className="grid grid-cols-2 gap-6">
        <div className="h-64 animate-pulse rounded-lg bg-surface-subtle" />
        <div className="h-64 animate-pulse rounded-lg bg-surface-subtle" />
      </div>
    </div>
  );
  if (error) return <div className="p-8 text-danger-700">{error}</div>;

  const gradeData = gradeDist ? [
    { name: "A+", value: gradeDist.aPlus },
    { name: "A",  value: gradeDist.a },
    { name: "B",  value: gradeDist.b },
    { name: "C",  value: gradeDist.c },
    { name: "D",  value: gradeDist.d },
    { name: "F",  value: gradeDist.f },
  ].filter(d => d.value > 0) : [];

  const classPerfData = classPerf
    .filter(c => c.averageGrade !== null && c.averageGrade !== undefined)
    .map(c => ({ name: c.name, average: Math.round(c.averageGrade!), students: c.studentCount }));

  return (
    <div className="p-6 lg:p-8 space-y-6">
      <div>
        <h1 className="text-2xl font-semibold text-text-primary tracking-tight">Analytics</h1>
        <p className="text-sm text-text-secondary mt-1">School-wide performance overview</p>
      </div>

      {/* KPI Cards */}
      {overview && (
        <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
          {[
            { label: "Students",        value: overview.totalStudents,                    Icon: GraduationCap, bg: "bg-surface-subtle",  color: "text-text-secondary" },
            { label: "Teachers",        value: overview.totalTeachers,                    Icon: Users,         bg: "bg-surface-subtle",  color: "text-text-secondary" },
            { label: "Classes",         value: overview.totalClasses,                     Icon: BookOpen,      bg: "bg-surface-subtle", color: "text-text-secondary" },
            { label: "Attendance Rate", value: `${overview.attendanceRateThisMonth}%`,    Icon: CheckCircle2,  bg: "bg-surface-subtle",    color: "text-text-secondary" },
          ].map(stat => (
            <Card key={stat.label}>
              <CardContent className="p-4 flex items-center gap-3">
                <div className={`h-10 w-10 rounded-lg ${stat.bg} flex items-center justify-center shrink-0`}>
                  <stat.Icon className={`h-5 w-5 ${stat.color}`} />
                </div>
                <div>
                  <p className="text-xs text-text-secondary uppercase tracking-wide">{stat.label}</p>
                  <p className="text-2xl font-bold text-text-primary">{stat.value}</p>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        {/* Grade Distribution */}
        {gradeData.length > 0 && (
          <Card>
            <CardHeader>
              <CardTitle>Grade Distribution</CardTitle>
              {gradeDist && (
                <p className="text-sm text-text-secondary">
                  School average: <span className="font-semibold text-primary">{gradeDist.average}%</span> across {gradeDist.total} grades
                </p>
              )}
            </CardHeader>
            <CardContent>
              <ResponsiveContainer width="100%" height={220}>
                <PieChart>
                  <Pie data={gradeData} dataKey="value" nameKey="name" cx="50%" cy="50%" outerRadius={80} label={({ name, value }) => `${name}: ${value}`}>
                    {gradeData.map((_, i) => (
                      <Cell key={i} fill={GRADE_COLORS[i % GRADE_COLORS.length]} />
                    ))}
                  </Pie>
                  <Tooltip />
                </PieChart>
              </ResponsiveContainer>
            </CardContent>
          </Card>
        )}

        {/* Class Performance */}
        {classPerfData.length > 0 && (
          <Card>
            <CardHeader><CardTitle>Class Average Grades</CardTitle></CardHeader>
            <CardContent>
              <ResponsiveContainer width="100%" height={220}>
                <BarChart data={classPerfData} layout="vertical">
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis type="number" domain={[0, 100]} tickFormatter={v => `${v}%`} />
                  <YAxis dataKey="name" type="category" width={80} tick={{ fontSize: 12 }} />
                  <Tooltip formatter={(v) => [`${v}%`, "Average"]} />
                  <Bar dataKey="average" fill="#3b82f6" radius={[0, 4, 4, 0]} />
                </BarChart>
              </ResponsiveContainer>
            </CardContent>
          </Card>
        )}
      </div>

      {/* Overview stats row */}
      {overview && (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
          <Card>
            <CardContent className="p-4">
              <p className="text-sm text-text-secondary">Total Assignments</p>
              <p className="text-2xl font-bold text-text-primary">{overview.totalAssignments}</p>
            </CardContent>
          </Card>
          <Card>
            <CardContent className="p-4">
              <p className="text-sm text-text-secondary">Pending Grading</p>
              <p className="text-2xl font-bold text-text-primary">{overview.pendingSubmissions}</p>
            </CardContent>
          </Card>
          <Card>
            <CardContent className="p-4">
              <p className="text-sm text-text-secondary">Published Courses</p>
              <p className="text-2xl font-bold text-text-primary">{overview.totalCourses}</p>
            </CardContent>
          </Card>
        </div>
      )}

      {/* At-Risk Students */}
      {atRisk.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <AlertTriangle className="h-4 w-4 text-danger-500" />
              At-Risk Students
              <Badge variant="destructive">{atRisk.length}</Badge>
            </CardTitle>
            <p className="text-sm text-text-secondary">Students with attendance below 75% in the last 30 days</p>
          </CardHeader>
          <CardContent className="p-0">
            <table className="w-full text-sm">
              <thead className="border-b border-border bg-surface-subtle">
                <tr>
                  {["Student", "ID", "Attendance Rate", "Risk Level"].map(h => (
                    <th key={h} className="px-6 py-3 text-left font-medium text-text-muted">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {atRisk.map(s => (
                  <tr key={s.studentId} className="hover:bg-surface-subtle">
                    <td className="px-6 py-3 font-medium text-text-primary">{s.name}</td>
                    <td className="px-6 py-3 text-text-secondary">{s.studentNumber}</td>
                    <td className="px-6 py-3">
                      <div className="flex items-center gap-2">
                        <div className="flex-1 bg-surface-subtle rounded-full h-2 max-w-24">
                          <div className="bg-danger-500 h-2 rounded-full" style={{ width: `${s.attendanceRate}%` }} />
                        </div>
                        <span className="text-danger-700 font-medium">{s.attendanceRate}%</span>
                      </div>
                    </td>
                    <td className="px-6 py-3"><Badge variant="destructive">{s.risk}</Badge></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </CardContent>
        </Card>
      )}

      {!gradeDist && !atRisk.length && !classPerfData.length && (
        <div className="rounded-lg border-2 border-dashed border-border py-16 text-center">
          <div className="flex justify-center mb-4">
            <TrendingUp className="h-10 w-10 text-text-muted" />
          </div>
          <p className="text-lg font-medium text-text-primary">No data yet</p>
          <p className="text-sm text-text-muted mt-1">Analytics will populate as students submit work and attend classes</p>
        </div>
      )}
    </div>
  );
}
