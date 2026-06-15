import {
  LayoutDashboard, BookOpen, BookOpenCheck, GraduationCap, ClipboardList, BarChart2,
  CheckSquare, CalendarDays, MessageSquare, MessageCircle, Megaphone, TrendingUp, Users,
  Settings, Rocket, FileText, CreditCard, Route, Star, Trophy, ShieldCheck, Award, Download,
  Wallet, Percent, Plug, type LucideIcon,
} from "lucide-react";

// Sprint 1.5.0 Step 8 — string icon keys from lib/nav.ts → lucide components, shared by the
// desktop Sidebar and the MobileNav so deriveNav stays React-free and the two navs never drift.
export const NAV_ICONS: Record<string, LucideIcon> = {
  home: LayoutDashboard,
  cap: GraduationCap,
  academics: BookOpenCheck,
  clipboard: ClipboardList,
  check: CheckSquare,
  mega: Megaphone,
  chart: BarChart2,
  file: FileText,
  route: Route,
  book: BookOpen,
  star: Star,
  trophy: Trophy,
  award: Award,
  msg: MessageSquare,
  calendar: CalendarDays,
  card: CreditCard,
  shield: ShieldCheck,
  trend: TrendingUp,
  users: Users,
  settings: Settings,
  rocket: Rocket,
  wallet: Wallet,
  percent: Percent,
  plug: Plug,
  whatsapp: MessageCircle,
  download: Download,
};
