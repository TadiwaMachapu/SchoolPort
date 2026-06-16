import type { SchoolFeatures } from "@/lib/theme";

// Sprint 1.5.0 Step 8 (sidebar rebuild) — pure, React-free sidebar derivation and the SINGLE
// source of truth for nav visibility. Every item is gated by BOTH its feature flag AND its
// identity/position/permission rule; if either fails the item is omitted (flag off = hidden for
// everyone, regardless of position). Icons are string keys mapped to lucide in lib/nav-icons.
// Finance-only and IT-only staff get a dedicated workspace that REPLACES the general nav.

export interface NavItem { href: string; label: string; icon: string; }
export interface NavSection { label: string; items: NavItem[]; }

/** Grade context the Matric Hub rule needs (can't be derived from identity/positions alone). */
export interface NavContext {
  gradeLevel?: number | null;   // the learner's own grade
  hasGrade12Child?: boolean;    // a parent has any linked child in Grade 12
}

const FINANCE = ["FinanceManager", "BursarDebtorsClerk", "Cashier"];
const SMT = ["Principal", "DeputyPrincipal"];
const SETTINGS_POS = ["Principal", "DeputyPrincipal", "ITAdministrator"];

const n = (href: string, label: string, icon: string): NavItem => ({ href, label, icon });

/**
 * Derives the sidebar sections for a user from Layer-1 identity, the positions held, the resolved
 * permission set, the school's feature flags, and grade context. Returns one general section, or a
 * single replacement workspace for finance-only / IT-only staff. No role strings, no hard caps —
 * flag + permission composition naturally bounds the list.
 */
export function deriveNav(
  identity: string,
  positions: string[],
  permissions: string[],
  features: Partial<SchoolFeatures>,
  ctx: NavContext = {},
): NavSection[] {
  const has = (k: string) => positions.includes(k);
  const hasAny = (ks: string[]) => ks.some((k) => positions.includes(k));
  const can = (p: string) => permissions.includes(p);
  const f = (k: keyof SchoolFeatures) => features[k] === true;

  const isStaff = identity === "Staff";
  const isLearner = identity === "Learner";
  const isParent = identity === "Parent";

  // A staff member is "finance-only" / "IT-only" when EVERY position they hold is in that set.
  // Anyone who also holds a teaching/oversight position falls through to the general nav (union).
  const financeOnly = isStaff && hasAny(FINANCE) && positions.length > 0 && positions.every((p) => FINANCE.includes(p));
  const itOnly = isStaff && has("ITAdministrator") && positions.every((p) => p === "ITAdministrator");

  if (financeOnly) {
    return [{ label: "Finance", items: [
      n("/dashboard", "Dashboard", "home"),
      n("/finance/accounts", "Accounts", "wallet"),
      n("/finance/invoices", "Invoices", "file"),
      n("/finance/payments", "Payments", "card"),
      n("/finance/exemptions", "Exemptions", "percent"),
      n("/finance/reports", "Reports", "chart"),
      ...(f("schoolPay") ? [n("/school-pay", "SchoolPay", "card")] : []),
      n("/finance/settings", "Settings", "settings"),
    ] }];
  }
  if (itOnly) {
    return [{ label: "System", items: [
      n("/users", "Users", "users"),
      n("/settings/integrations", "Integrations", "plug"),
      n("/settings/audit", "Audit Logs", "shield"),
      ...(f("whatsApp") && can("communications.whatsapp_admin") ? [n("/whatsapp", "WhatsApp", "whatsapp")] : []),
      ...(f("saSamsExport") && can("system.data_export") ? [n("/sasams", "SA-SAMS", "download")] : []),
      ...(f("popiaCentre") && can("system.popia_admin") ? [n("/popia", "POPIA Centre", "shield")] : []),
      n("/settings", "Settings", "settings"),
    ] }];
  }

  // General population (Staff teaching/SMT, Learner, Parent). One ordered candidate list; each
  // entry resolves to a NavItem only when its flag AND identity/position/permission rule pass.
  const pathwaysHref = isParent ? "/parent" : "/pathways";
  const candidates: (NavItem | false | "" | undefined)[] = [
    n("/dashboard", "Dashboard", "home"),
    isStaff && n("/classes", "Classes", "cap"),
    isLearner && f("gradebook") && n("/my-academics", "My Academics", "academics"),
    (isLearner || isParent || isStaff) && n("/assignments", "Assignments", "clipboard"),
    isStaff && n("/attendance", "Attendance", "check"),
    n("/calendar", "Calendar", "calendar"),
    n("/announcements", "Announcements", "mega"),
    f("pathways") && (isLearner || isParent || (isStaff && (can("pathways.advise") || can("pathways.cohort_view")))) && n(pathwaysHref, "Pathways", "route"),
    (isLearner || isStaff) && f("virtualClassroom") && n("/courses", "Courses", "book"),
    f("skillsProfile") && (isLearner || (isStaff && can("skills.endorse"))) && n("/skills", "Skills", "star"),
    (isLearner || isParent || isStaff) && f("sportsCulture") && n("/sports-culture", "Sports & Culture", "trophy"),
    f("matricHub") && ((isLearner && ctx.gradeLevel === 12) || (isParent && ctx.hasGrade12Child === true) || isStaff) && n("/matric", "Matric Hub", "award"),
    f("schoolPay") && (isParent || hasAny(FINANCE) || hasAny(SMT)) && n("/school-pay", "SchoolPay", "card"),
    f("schoolChat") && n("/messages", "Messages", "msg"),
    isStaff && f("gradebook") && can("marks.view_class") && n("/gradebook", "Gradebook", "chart"),
    isStaff && f("smartReports") && can("reporting.view") && n("/reports", "Reports", "file"),
    isStaff && f("smartReports") && can("analytics.view_school") && n("/analytics", "Analytics", "trend"),
    f("whatsApp") && can("communications.whatsapp_admin") && n("/whatsapp", "WhatsApp", "whatsapp"),
    f("popiaCentre") && can("system.popia_admin") && n("/popia", "POPIA Centre", "shield"),
    f("saSamsExport") && can("system.data_export") && n("/sasams", "SA-SAMS", "download"),
    can("system.users_manage") && n("/users", "Users", "users"),
    can("system.positions_assign") && n("/positions", "Positions", "idcard"),
    hasAny(SMT) && n("/onboarding", "Setup Wizard", "rocket"),
    hasAny(SETTINGS_POS) && n("/settings", "Settings", "settings"),
  ];

  const items = candidates.filter((i): i is NavItem => Boolean(i) && typeof i === "object");
  return items.length ? [{ label: "", items }] : [];
}
