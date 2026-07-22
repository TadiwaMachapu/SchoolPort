import { cookies, headers } from "next/headers";
import { redirect } from "next/navigation";
import { Search } from "lucide-react";
import { Sidebar } from "@/components/sidebar";
import { MobileNav } from "@/components/mobile-nav";
import { NotificationBell } from "@/components/notification-bell";
import { PwaInstallPrompt } from "@/components/pwa-install-prompt";
import { FeaturesProvider } from "@/lib/features-context";
import { AuthProvider, type AuthPosition } from "@/lib/auth-context";
import { FinanceSessionGuard } from "@/components/finance-session-guard";
import type { SchoolFeatures, SchoolTheme } from "@/lib/theme";

const PAGE_TITLES: Record<string, string> = {
  dashboard:       "Dashboard",
  courses:         "Courses",
  classes:         "Classes",
  "my-academics":  "My Academics",
  assignments:     "Assignments",
  quizzes:         "Quizzes",
  gradebook:       "Gradebook",
  attendance:      "Attendance",
  calendar:        "Calendar",
  messages:        "Messages",
  announcements:   "Announcements",
  analytics:       "Analytics",
  "sports-culture":"Sports & Culture",
  matric:          "Matric Hub",
  pathways:        "Pathways",
  skills:          "Skills",
  "school-pay":    "SchoolPay",
  popia:           "POPIA Centre",
  sasams:          "SA-SAMS",
  whatsapp:        "WhatsApp",
  reports:         "Reports",
  users:           "Users",
  positions:       "Positions",
  settings:        "Settings",
  parent:          "Parent Portal",
};

function getPageTitle(pathname: string): string {
  const segment = pathname.replace(/^\//, "").split("/")[0];
  return PAGE_TITLES[segment] ?? segment.charAt(0).toUpperCase() + segment.slice(1);
}

const API = process.env.NEXT_PUBLIC_API_URL;

async function getMe(token: string) {
  try {
    const res = await fetch(`${API}/api/me`, {
      headers: { Authorization: `Bearer ${token}` },
      cache: "no-store",
    });
    if (!res.ok) return null;
    return res.json();
  } catch {
    return null;
  }
}

async function getSchool(token: string) {
  try {
    const res = await fetch(`${API}/api/schools/current`, {
      headers: { Authorization: `Bearer ${token}` },
      cache: "no-store",
    });
    if (!res.ok) return null;
    return res.json();
  } catch {
    return null;
  }
}

export default async function DashboardLayout({ children }: { children: React.ReactNode }) {
  const cookieStore = await cookies();
  const token = cookieStore.get("sp_token")?.value;
  if (!token) redirect("/login");

  const headersList = await headers();
  const pathname = headersList.get("x-pathname") ?? "";
  const pageTitle = getPageTitle(pathname);

  const [me, school] = await Promise.all([getMe(token), getSchool(token)]);

  if (!me) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-text-primary p-8">
        <div className="text-center text-white max-w-md">
          <div className="flex justify-center mb-4">
            <svg className="h-14 w-14 text-white/40" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M13.828 10.172a4 4 0 00-5.656 0l-4 4a4 4 0 105.656 5.656l1.102-1.101m-.758-4.899a4 4 0 005.656 0l4-4a4 4 0 00-5.656-5.656l-1.1 1.1" /></svg>
          </div>
          <h1 className="text-2xl font-bold mb-2">API server not reachable</h1>
          <p className="text-white/70 mb-6">Make sure the backend is running:</p>
          <code className="block bg-white/5 rounded-lg p-4 text-left text-sm text-success-500 mb-6">
            cd C:\Projects\SchoolPort\SchoolPortal.Server{"\n"}
            dotnet run
          </code>
          <p className="text-white/50 text-sm">
            Then refresh this page. The API should be at{" "}
            <span className="text-white/80">{API}</span>
          </p>
        </div>
      </div>
    );
  }

  const theme: SchoolTheme   = school?.theme    ?? { primaryColor: "#1E40AF", fontFamily: "Inter" };
  const features: SchoolFeatures = school?.features ?? {};

  // Step 8: identity / positions / resolved permissions from /api/me (authoritative).
  const identity: string = me.identity ?? "";
  const positions: AuthPosition[] = me.positions ?? [];
  const permissions: string[] = me.permissions ?? [];
  const positionKeys = positions.map((p) => p.key);
  // Grade context for the Matric Hub nav gate (learner's own grade / parent's Grade-12 child).
  const gradeLevel: number | null = me.gradeLevel ?? null;
  const hasGrade12Child: boolean = me.hasGrade12Child ?? false;
  const navContext = { gradeLevel, hasGrade12Child };

  const today = new Date().toLocaleDateString("en-ZA", {
    weekday: "long", day: "numeric", month: "long", year: "numeric",
  });

  return (
    <div
      className="flex h-screen overflow-hidden bg-surface-page"
      style={{ "--color-primary": theme.primaryColor } as React.CSSProperties}
    >
      {/* Sidebar — desktop only; floats as its own card via the wrapper padding */}
      <div className="hidden md:flex p-3 pr-0">
        <Sidebar user={me.user} school={{ ...me.school, theme, features }} identity={identity} positions={positionKeys} permissions={permissions} context={navContext} />
      </div>

      {/* Right column: header + scrollable content */}
      <div className="flex flex-1 flex-col overflow-hidden min-w-0">

        {/* Top bar */}
        <header className="flex h-16 shrink-0 items-center justify-between px-4 md:px-6 gap-4">
          {/* Left: breadcrumb */}
          <div className="flex items-center gap-2 min-w-0">
            <span className="text-[11px] text-text-muted font-medium hidden sm:block truncate">{me.school?.name ?? "School Portal"}</span>
            <span className="text-text-muted hidden sm:block">/</span>
            <h2 className="text-sm font-semibold text-text-primary truncate">{pageTitle}</h2>
          </div>

          {/* Right: search pill + date + bell + user */}
          <div className="flex items-center gap-3 shrink-0">
            <div className="hidden lg:flex items-center gap-2 rounded-pill bg-surface-card px-3.5 h-9 w-56 shadow-card">
              <Search className="h-4 w-4 text-text-muted shrink-0" />
              <input
                type="search"
                placeholder="Search"
                aria-label="Search"
                className="w-full bg-transparent text-[13px] text-text-primary placeholder:text-text-muted focus:outline-none"
              />
            </div>
            <span className="hidden xl:block text-[11px] text-text-muted whitespace-nowrap">{today}</span>
            <NotificationBell />
            <div className="flex items-center gap-2.5 pl-3 border-l border-border">
              <div
                className="h-8 w-8 rounded-full flex items-center justify-center text-white text-xs font-bold shrink-0"
                style={{ backgroundColor: theme.primaryColor }}
              >
                {me.user.firstName?.[0]}{me.user.lastName?.[0]}
              </div>
              <div className="hidden sm:block leading-tight">
                <p className="text-[13px] font-semibold text-text-primary leading-none">
                  {me.user.firstName} {me.user.lastName}
                </p>
                <p className="text-[11px] text-text-muted mt-0.5">{me.user.role}</p>
              </div>
            </div>
          </div>
        </header>

        {/* Main content — extra bottom padding on mobile for the nav bar */}
        <main className="flex-1 overflow-y-auto pb-16 md:pb-3">
          <AuthProvider value={{ identity, positions, permissions, gradeLevel, hasGrade12Child, user: { firstName: me.user?.firstName ?? "", lastName: me.user?.lastName ?? "" } }}>
            <FeaturesProvider features={features}>{children}</FeaturesProvider>
            <FinanceSessionGuard />
          </AuthProvider>
        </main>
      </div>

      {/* Mobile bottom navigation */}
      <MobileNav identity={identity} positions={positionKeys} permissions={permissions} features={features} context={navContext} primaryColor={theme.primaryColor} />
      <PwaInstallPrompt />
    </div>
  );
}
