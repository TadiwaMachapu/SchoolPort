"use client";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { cn, clearSession, identityLabel } from "@/lib/utils";
import { deriveNav, type NavContext } from "@/lib/nav";
import { NAV_ICONS } from "@/lib/nav-icons";
import type { SchoolFeatures, SchoolTheme } from "@/lib/theme";
import { LayoutDashboard, LogOut } from "lucide-react";

const ICONS = NAV_ICONS;

interface SidebarProps {
  user: { firstName: string; lastName: string; role: string; email: string };
  school: { name: string; theme?: SchoolTheme; features?: SchoolFeatures };
  identity: string;
  positions: string[];
  permissions: string[];
  context?: NavContext;
}

export function Sidebar({ user, school, identity, positions, permissions, context }: SidebarProps) {
  const pathname = usePathname();
  const router = useRouter();
  const features: Partial<SchoolFeatures> = school.features ?? {};
  const theme = school.theme;
  const primaryColor = theme?.primaryColor ?? "#2563eb";

  function logout() {
    clearSession(); // Step 8 FLAG 3: clear all five session cookies, not just sp_token.
    router.push("/login");
  }

  // Sidebar is derived from identity + positions + resolved permissions + flags, not a role string.
  const sections = deriveNav(identity, positions, permissions, features, context);
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
        {sections.map((section, idx) => (
          <div key={section.label || `s${idx}`}>
            {section.label && (
              <p className="px-3 mb-1.5 text-[10px] font-semibold uppercase tracking-widest text-slate-500 select-none">
                {section.label}
              </p>
            )}
            <div className="space-y-0.5">
              {section.items.map((item) => {
                const active = pathname === item.href || (item.href !== "/dashboard" && pathname.startsWith(item.href));
                const Icon = ICONS[item.icon] ?? LayoutDashboard;
                return (
                  <Link
                    key={`${section.label}-${item.href}`}
                    href={item.href}
                    className={cn(
                      "flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium transition-colors group",
                      active ? "text-white" : "text-slate-400 hover:bg-slate-800 hover:text-slate-100"
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
            <p className="text-xs text-slate-500 truncate mt-0.5">{user.firstName ? identityLabel(identity) : user.role}</p>
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
