"use client";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { cn } from "@/lib/utils";
import type { SchoolFeatures, SchoolTheme } from "@/lib/theme";

const allNavItems = [
  { href: "/dashboard",     label: "Dashboard",     icon: "🏠", roles: ["Admin","Teacher","Student","Parent"], feature: null },
  { href: "/courses",       label: "Courses",       icon: "📚", roles: ["Admin","Teacher","Student"],          feature: "courses" },
  { href: "/classes",       label: "Classes",       icon: "🏫", roles: ["Admin","Teacher","Student"],          feature: null },
  { href: "/assignments",   label: "Assignments",   icon: "📝", roles: ["Admin","Teacher","Student"],          feature: null },
  { href: "/quizzes",       label: "Quizzes",       icon: "🧠", roles: ["Admin","Teacher","Student"],          feature: "quizzes" },
  { href: "/gradebook",     label: "Gradebook",     icon: "📊", roles: ["Admin","Teacher"],                    feature: null },
  { href: "/attendance",    label: "Attendance",    icon: "✅", roles: ["Admin","Teacher"],                    feature: "attendance" },
  { href: "/calendar",      label: "Calendar",      icon: "📅", roles: ["Admin","Teacher","Student","Parent"], feature: null },
  { href: "/messages",      label: "Messages",      icon: "💬", roles: ["Admin","Teacher","Student","Parent"], feature: "messaging" },
  { href: "/announcements", label: "Announcements", icon: "📢", roles: ["Admin","Teacher","Student","Parent"], feature: null },
  { href: "/analytics",     label: "Analytics",     icon: "📈", roles: ["Admin"],                             feature: "analytics" },
  { href: "/users",         label: "Users",         icon: "👥", roles: ["Admin"],                             feature: null },
  { href: "/settings",      label: "Settings",      icon: "⚙️",  roles: ["Admin"],                             feature: null },
];

interface SidebarProps {
  user: { firstName: string; lastName: string; role: string; email: string };
  school: { name: string; theme?: SchoolTheme; features?: SchoolFeatures };
}

export function Sidebar({ user, school }: SidebarProps) {
  const pathname = usePathname();
  const router = useRouter();
  const features = school.features ?? {};
  const theme = school.theme;
  const primaryColor = theme?.primaryColor ?? "#1E40AF";

  function logout() {
    document.cookie = "sp_token=; path=/; max-age=0";
    router.push("/login");
  }

  const visibleItems = allNavItems.filter((item) => {
    if (!item.roles.includes(user.role)) return false;
    if (item.feature && !(features as Record<string, boolean>)[item.feature]) return false;
    return true;
  });

  return (
    <aside className="flex h-screen w-64 flex-col border-r border-gray-200 bg-gray-900 text-white flex-shrink-0">
      {/* School header */}
      <div className="border-b border-gray-700 p-5">
        <div className="flex items-center gap-3">
          {theme?.logoUrl ? (
            <img src={theme.logoUrl} alt="Logo" className="h-8 w-8 rounded object-contain bg-white p-0.5" />
          ) : (
            <div className="h-8 w-8 rounded flex items-center justify-center text-white text-sm font-bold"
              style={{ backgroundColor: primaryColor }}>
              {school.name.charAt(0)}
            </div>
          )}
          <div className="min-w-0">
            <p className="text-xs font-medium uppercase tracking-wider text-gray-400">School Portal</p>
            <h1 className="text-sm font-bold truncate">{school.name}</h1>
          </div>
        </div>
      </div>

      {/* Navigation */}
      <nav className="flex-1 space-y-0.5 p-3 overflow-y-auto">
        {visibleItems.map((item) => {
          const active = pathname === item.href || (item.href !== "/dashboard" && pathname.startsWith(item.href));
          return (
            <Link
              key={item.href}
              href={item.href}
              className={cn(
                "flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium transition-colors",
                active ? "text-white" : "text-gray-300 hover:bg-gray-800 hover:text-white"
              )}
              style={active ? { backgroundColor: primaryColor } : undefined}
            >
              <span className="text-base">{item.icon}</span>
              {item.label}
            </Link>
          );
        })}
      </nav>

      {/* User footer */}
      <div className="border-t border-gray-700 p-4">
        <div className="mb-3">
          <p className="text-sm font-medium truncate">{user.firstName} {user.lastName}</p>
          <p className="text-xs text-gray-400 truncate">{user.role} · {user.email}</p>
        </div>
        <button
          onClick={logout}
          className="w-full rounded-md bg-gray-800 px-3 py-2 text-left text-sm text-gray-300 hover:bg-gray-700 hover:text-white transition-colors"
        >
          Sign out
        </button>
      </div>
    </aside>
  );
}
