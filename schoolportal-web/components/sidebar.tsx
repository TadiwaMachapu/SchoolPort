"use client";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { cn, clearSession, identityLabel } from "@/lib/utils";
import { queryClient } from "@/shared/api/queryClient";
import { deriveNav, type NavContext } from "@/lib/nav";
import { NAV_ICONS } from "@/lib/nav-icons";
import type { SchoolFeatures, SchoolTheme } from "@/lib/theme";
import { LayoutDashboard, LogOut, LifeBuoy } from "lucide-react";

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
    queryClient.clear(); // drop cached queries so the next user starts clean (no cross-account leakage).
    router.push("/login");
  }

  // Sidebar is derived from identity + positions + resolved permissions + flags, not a role string.
  const sections = deriveNav(identity, positions, permissions, features, context);
  const userInitials = `${user.firstName?.[0] ?? ""}${user.lastName?.[0] ?? ""}`.toUpperCase();

  return (
    <aside className="flex h-full w-60 flex-col flex-shrink-0 rounded-lg bg-surface-card overflow-hidden">
      {/* School header — logo + wordmark (brand layer uses the school primary colour) */}
      <div className="px-4 py-4">
        <div className="flex items-center gap-3">
          {theme?.logoUrl ? (
            <img src={theme.logoUrl} alt="Logo" className="h-9 w-9 rounded-md object-contain bg-surface-subtle p-0.5 shrink-0" />
          ) : (
            <div
              className="h-9 w-9 rounded-md flex items-center justify-center text-white text-sm font-bold shrink-0"
              style={{ backgroundColor: primaryColor }}
            >
              {school.name.charAt(0)}
            </div>
          )}
          <div className="min-w-0">
            <p className="text-[10px] font-semibold uppercase tracking-widest text-text-muted">School Portal</p>
            <h1 className="text-sm font-semibold text-text-primary truncate leading-tight mt-0.5">{school.name}</h1>
          </div>
        </div>
      </div>

      {/* Navigation */}
      <nav className="flex-1 overflow-y-auto px-3 py-2 space-y-4">
        {sections.map((section, idx) => (
          <div key={section.label || `s${idx}`}>
            {section.label && (
              <p className="px-2.5 mb-1.5 text-[10px] font-semibold uppercase tracking-widest text-text-muted select-none">
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
                      "flex items-center gap-2.5 rounded-md px-2.5 py-2 text-xs font-medium transition-colors group",
                      active
                        ? "bg-primary-100 text-primary-800"
                        : "text-text-muted hover:bg-surface-subtle hover:text-text-secondary"
                    )}
                  >
                    <Icon
                      className={cn(
                        "h-[15px] w-[15px] shrink-0 transition-colors",
                        active ? "text-primary-700" : "text-text-muted group-hover:text-text-secondary"
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

      {/* Help card pinned bottom */}
      <div className="px-3 pt-1 pb-2">
        <div className="rounded-md bg-primary-50 px-3 py-3">
          <div className="flex items-center gap-2 text-primary-800">
            <LifeBuoy className="h-4 w-4 shrink-0" />
            <p className="text-xs font-semibold">Need a hand?</p>
          </div>
          <p className="mt-1 text-[11px] text-text-secondary leading-snug">Visit the help centre or contact your school admin.</p>
        </div>
      </div>

      {/* User footer */}
      <div className="px-3 pb-3">
        <div className="flex items-center gap-2.5 rounded-md px-2.5 py-2 mb-1">
          <div
            className="h-8 w-8 rounded-full flex items-center justify-center text-white text-xs font-bold shrink-0"
            style={{ backgroundColor: primaryColor }}
          >
            {userInitials}
          </div>
          <div className="min-w-0 flex-1">
            <p className="text-xs font-medium text-text-primary truncate leading-none">{user.firstName} {user.lastName}</p>
            <p className="text-[11px] text-text-muted truncate mt-0.5">{user.firstName ? identityLabel(identity) : user.role}</p>
          </div>
        </div>
        <button
          onClick={logout}
          className="w-full flex items-center gap-2.5 rounded-md px-2.5 py-2 text-xs text-text-muted hover:bg-surface-subtle hover:text-text-secondary transition-colors"
        >
          <LogOut className="h-4 w-4 shrink-0" />
          Sign out
        </button>
      </div>
    </aside>
  );
}
