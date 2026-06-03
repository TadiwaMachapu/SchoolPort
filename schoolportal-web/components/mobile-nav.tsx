"use client";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { cn } from "@/lib/utils";
import {
  LayoutDashboard,
  CheckSquare,
  ClipboardList,
  Megaphone,
  Settings,
  type LucideIcon,
} from "lucide-react";

interface MobileNavItem {
  href: string;
  label: string;
  Icon: LucideIcon;
  roles: string[];
}

const mobileNavItems: MobileNavItem[] = [
  { href: "/dashboard",     label: "Home",        Icon: LayoutDashboard, roles: ["Admin","Teacher","Student","Parent"] },
  { href: "/attendance",    label: "Attendance",  Icon: CheckSquare,     roles: ["Admin","Teacher"] },
  { href: "/assignments",   label: "Assignments", Icon: ClipboardList,   roles: ["Admin","Teacher","Student"] },
  { href: "/announcements", label: "Updates",     Icon: Megaphone,       roles: ["Admin","Teacher","Student","Parent"] },
  { href: "/settings",      label: "Settings",    Icon: Settings,        roles: ["Admin"] },
];

interface MobileNavProps {
  role: string;
  primaryColor?: string;
}

export function MobileNav({ role, primaryColor = "#2563eb" }: MobileNavProps) {
  const pathname = usePathname();

  const items = mobileNavItems.filter((item) => item.roles.includes(role));

  return (
    <nav className="fixed bottom-0 left-0 right-0 z-50 flex md:hidden border-t border-gray-100 bg-white safe-area-bottom">
      {items.map((item) => {
        const active = pathname === item.href || (item.href !== "/dashboard" && pathname.startsWith(item.href));
        const { Icon } = item;
        return (
          <Link
            key={item.href}
            href={item.href}
            className={cn(
              "flex flex-1 flex-col items-center justify-center gap-1 py-2 px-1 text-[10px] font-medium transition-colors min-h-[56px]",
              active ? "text-gray-900" : "text-gray-400"
            )}
          >
            <Icon
              className="h-5 w-5"
              style={active ? { color: primaryColor } : undefined}
            />
            <span style={active ? { color: primaryColor } : undefined}>{item.label}</span>
          </Link>
        );
      })}
    </nav>
  );
}
