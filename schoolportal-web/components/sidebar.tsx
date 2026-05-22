"use client";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { cn } from "@/lib/utils";
import type { SchoolFeatures, SchoolTheme } from "@/lib/theme";
import {
  LayoutDashboard,
  BookOpen,
  GraduationCap,
  ClipboardList,
  Brain,
  BarChart2,
  CheckSquare,
  CalendarDays,
  MessageSquare,
  Megaphone,
  TrendingUp,
  Users,
  Settings,
  LogOut,
  type LucideIcon,
} from "lucide-react";

interface NavItem {
  href: string;
  label: string;
  Icon: LucideIcon;
  roles: string[];
  feature: string | null;
  group?: string;
}

const allNavItems: NavItem[] = [
  { href: "/dashboard",     label: "Dashboard",     Icon: LayoutDashboard, roles: ["Admin","Teacher","Student","Parent"], feature: null,        group: "main" },
  { href: "/courses",       label: "Courses",        Icon: BookOpen,        roles: ["Admin","Teacher","Student"],          feature: "courses",   group: "learn" },
  { href: "/classes",       label: "Classes",        Icon: GraduationCap,   roles: ["Admin","Teacher","Student"],          feature: null,        group: "learn" },
  { href: "/assignments",   label: "Assignments",    Icon: ClipboardList,   roles: ["Admin","Teacher","Student"],          feature: null,        group: "learn" },
  { href: "/quizzes",       label: "Quizzes",        Icon: Brain,           roles: ["Admin","Teacher","Student"],          feature: "quizzes",   group: "learn" },
  { href: "/gradebook",     label: "Gradebook",      Icon: BarChart2,       roles: ["Admin","Teacher"],                    feature: null,        group: "learn" },
  { href: "/attendance",    label: "Attendance",     Icon: CheckSquare,     roles: ["Admin","Teacher"],                    feature: "attendance",group: "learn" },
  { href: "/calendar",      label: "Calendar",       Icon: CalendarDays,    roles: ["Admin","Teacher","Student","Parent"], feature: null,        group: "tools" },
  { href: "/messages",      label: "Messages",       Icon: MessageSquare,   roles: ["Admin","Teacher","Student","Parent"], feature: "messaging", group: "tools" },
  { href: "/announcements", label: "Announcements",  Icon: Megaphone,       roles: ["Admin","Teacher","Student","Parent"], feature: null,        group: "tools" },
  { href: "/analytics",     label: "Analytics",      Icon: TrendingUp,      roles: ["Admin"],                             feature: "analytics", group: "admin" },
  { href: "/users",         label: "Users",          Icon: Users,           roles: ["Admin"],                             feature: null,        group: "admin" },
  { href: "/settings",      label: "Settings",       Icon: Settings,        roles: ["Admin"],                             feature: null,        group: "admin" },
];

const GROUP_LABELS: Record<string, string> = {
  main: "",
  learn: "Learning",
  tools: "Communication",
  admin: "Administration",
};

interface SidebarProps {
  user: { firstName: string; lastName: string; role: string; email: string };
  school: { name: string; theme?: SchoolTheme; features?: SchoolFeatures };
}

export function Sidebar({ user, school }: SidebarProps) {
  const pathname = usePathname();
  const router = useRouter();
  const features = school.features ?? {};
  const theme = school.theme;
  const primaryColor = theme?.primaryColor ?? "#2563eb";

  function logout() {
    document.cookie = "sp_token=; path=/; max-age=0";
    router.push("/login");
  }

  const visibleItems = allNavItems.filter((item) => {
    if (!item.roles.includes(user.role)) return false;
    if (item.feature && !(features as Record<string, boolean>)[item.feature]) return false;
    return true;
  });

  // Group items
  const groupOrder = ["main", "learn", "tools", "admin"];
  const grouped = groupOrder.map(g => ({
    group: g,
    label: GROUP_LABELS[g],
    items: visibleItems.filter(i => i.group === g),
  })).filter(g => g.items.length > 0);

  const userInitials = `${user.firstName?.[0] ?? ""}${user.lastName?.[0] ?? ""}`.toUpperCase();

  return (
    <aside
      className="flex h-screen w-64 flex-col flex-shrink-0"
      style={{ backgroundColor: "var(--sidebar-bg)", borderRight: "1px solid var(--sidebar-border)" }}
    >
      {/* School header */}
      <div className="px-4 py-5" style={{ borderBottom: "1px solid var(--sidebar-border)" }}>
        <div className="flex items-center gap-3">
          {theme?.logoUrl ? (
            <img src={theme.logoUrl} alt="Logo" className="h-8 w-8 rounded-md object-contain bg-white p-0.5 shrink-0" />
          ) : (
            <div
              className="h-8 w-8 rounded-md flex items-center justify-center text-white text-sm font-bold shrink-0"
              style={{ backgroundColor: primaryColor }}
            >
              {school.name.charAt(0)}
            </div>
          )}
          <div className="min-w-0">
            <p className="text-[10px] font-semibold uppercase tracking-widest text-slate-500">School Portal</p>
            <h1 className="text-sm font-semibold text-white truncate leading-tight mt-0.5">{school.name}</h1>
          </div>
        </div>
      </div>

      {/* Navigation */}
      <nav className="flex-1 overflow-y-auto px-3 py-3 space-y-4">
        {grouped.map(({ group, label, items }) => (
          <div key={group}>
            {label && (
              <p className="px-3 mb-1.5 text-[10px] font-semibold uppercase tracking-widest text-slate-500 select-none">
                {label}
              </p>
            )}
            <div className="space-y-0.5">
              {items.map((item) => {
                const active = pathname === item.href || (item.href !== "/dashboard" && pathname.startsWith(item.href));
                const { Icon } = item;
                return (
                  <Link
                    key={item.href}
                    href={item.href}
                    className={cn(
                      "flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium transition-colors group",
                      active
                        ? "text-white"
                        : "text-slate-400 hover:bg-slate-800 hover:text-slate-100"
                    )}
                    style={active ? { backgroundColor: primaryColor } : undefined}
                  >
                    <Icon
                      className={cn(
                        "h-4 w-4 shrink-0 transition-colors",
                        active ? "text-white" : "text-slate-500 group-hover:text-slate-300"
                      )}
                    />
                    {item.label}
                  </Link>
                );
              })}
            </div>
          </div>
        ))}
      </nav>

      {/* User footer */}
      <div className="px-3 py-3" style={{ borderTop: "1px solid var(--sidebar-border)" }}>
        <div className="flex items-center gap-3 rounded-md px-3 py-2.5 mb-1">
          <div
            className="h-8 w-8 rounded-full flex items-center justify-center text-white text-xs font-bold shrink-0"
            style={{ backgroundColor: primaryColor }}
          >
            {userInitials}
          </div>
          <div className="min-w-0 flex-1">
            <p className="text-sm font-medium text-slate-200 truncate leading-none">{user.firstName} {user.lastName}</p>
            <p className="text-xs text-slate-500 truncate mt-0.5">{user.role}</p>
          </div>
        </div>
        <button
          onClick={logout}
          className="w-full flex items-center gap-3 rounded-md px-3 py-2 text-sm text-slate-400 hover:bg-slate-800 hover:text-slate-100 transition-colors"
        >
          <LogOut className="h-4 w-4 shrink-0" />
          Sign out
        </button>
      </div>
    </aside>
  );
}
