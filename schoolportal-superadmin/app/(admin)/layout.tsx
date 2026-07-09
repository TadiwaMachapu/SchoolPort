"use client";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { LayoutDashboard, School, LogOut } from "lucide-react";

const NAV = [
  { href: "/dashboard", label: "Dashboard", icon: LayoutDashboard },
  { href: "/schools",   label: "Schools",   icon: School },
];

function getAdminName(): string {
  if (typeof document === "undefined") return "Super Admin";
  const m = document.cookie.match(/(?:^|; )sa_name=([^;]*)/);
  return m ? decodeURIComponent(m[1]) : "Super Admin";
}

export default function AdminLayout({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const router   = useRouter();

  function signOut() {
    document.cookie = "sa_token=; path=/; max-age=0";
    document.cookie = "sa_name=; path=/; max-age=0";
    router.push("/login");
  }

  return (
    <div className="flex h-full">
      {/* Sidebar */}
      <aside className="flex w-56 flex-col border-r border-white/10 bg-[#0a0a14]">
        <div className="flex items-center gap-2 px-5 py-5 border-b border-white/10">
          <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-violet-600">
            <School className="h-4 w-4 text-white" />
          </div>
          <span className="text-sm font-bold text-white leading-tight">
            SchoolPortal<br />
            <span className="text-[10px] font-normal text-violet-400 uppercase tracking-widest">Super Admin</span>
          </span>
        </div>

        <nav className="flex-1 px-3 py-4 space-y-0.5">
          {NAV.map(({ href, label, icon: Icon }) => {
            const active = pathname.startsWith(href);
            return (
              <Link
                key={href}
                href={href}
                className={`flex items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium transition-colors
                  ${active
                    ? "bg-violet-600/20 text-violet-300"
                    : "text-white/50 hover:bg-white/5 hover:text-white"}`}
              >
                <Icon className="h-4 w-4 flex-shrink-0" />
                {label}
              </Link>
            );
          })}
        </nav>

        <div className="border-t border-white/10 px-3 py-3">
          <div className="mb-1 px-3 text-xs text-white/40 truncate">{getAdminName()}</div>
          <button
            onClick={signOut}
            className="flex w-full items-center gap-3 rounded-lg px-3 py-2 text-sm text-white/50 hover:bg-white/5 hover:text-white transition-colors"
          >
            <LogOut className="h-4 w-4" />
            Sign out
          </button>
        </div>
      </aside>

      {/* Main */}
      <main className="flex-1 overflow-y-auto bg-[#0d0d1a]">
        {children}
      </main>
    </div>
  );
}
