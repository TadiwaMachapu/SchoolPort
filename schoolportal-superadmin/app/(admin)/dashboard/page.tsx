"use client";
import { useEffect, useState } from "react";
import { api, PlatformStats } from "@/lib/api";
import { Building2, Users, GraduationCap, BookOpen, CheckCircle } from "lucide-react";

const STATS: { key: keyof PlatformStats; label: string; icon: React.ElementType; color: string }[] = [
  { key: "totalSchools",   label: "Total Schools",   icon: Building2,      color: "text-violet-400" },
  { key: "activeSchools",  label: "Active Schools",  icon: CheckCircle,    color: "text-emerald-400" },
  { key: "totalUsers",     label: "Total Users",     icon: Users,          color: "text-sky-400"    },
  { key: "totalStudents",  label: "Students",        icon: GraduationCap,  color: "text-amber-400"  },
  { key: "totalTeachers",  label: "Teachers",        icon: BookOpen,       color: "text-rose-400"   },
];

export default function DashboardPage() {
  const [stats,   setStats]   = useState<PlatformStats | null>(null);
  const [loading, setLoading] = useState(true);
  const [error,   setError]   = useState("");

  useEffect(() => {
    api.stats()
      .then(setStats)
      .catch(e => setError(e.message))
      .finally(() => setLoading(false));
  }, []);

  return (
    <div className="p-8">
      <h1 className="text-xl font-bold text-white mb-1">Platform Overview</h1>
      <p className="text-sm text-white/40 mb-8">Live stats across all schools</p>

      {error && (
        <div className="mb-6 rounded-lg border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm text-red-400">{error}</div>
      )}

      <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-3 gap-4">
        {STATS.map(({ key, label, icon: Icon, color }) => (
          <div key={key} className="rounded-2xl border border-white/8 bg-white/4 p-6 flex items-center gap-5">
            <div className="flex h-12 w-12 flex-shrink-0 items-center justify-center rounded-xl bg-white/8">
              <Icon className={`h-6 w-6 ${color}`} />
            </div>
            <div>
              <p className="text-sm text-white/50">{label}</p>
              {loading ? (
                <div className="mt-1 h-7 w-16 animate-pulse rounded bg-white/10" />
              ) : (
                <p className="text-2xl font-bold text-white">{stats?.[key] ?? "—"}</p>
              )}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
