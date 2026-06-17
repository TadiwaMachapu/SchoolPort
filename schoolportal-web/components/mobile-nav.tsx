"use client";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { cn } from "@/lib/utils";
import { deriveNav, type NavContext } from "@/lib/nav";
import { NAV_ICONS } from "@/lib/nav-icons";
import { LayoutDashboard } from "lucide-react";
import type { SchoolFeatures } from "@/lib/theme";

interface MobileNavProps {
  identity: string;
  positions: string[];
  permissions: string[];
  features: Partial<SchoolFeatures>;
  context?: NavContext;
  primaryColor?: string;
}

// Step 8: the bottom bar is derived from the SAME deriveNav source of truth as the desktop sidebar
// (no hardcoded role strings). It surfaces the first few items in nav order — which are the most
// important for each identity (Dashboard + the identity's core pages).
export function MobileNav({ identity, positions, permissions, features, context, primaryColor = "#2563eb" }: MobileNavProps) {
  const pathname = usePathname();

  const items = deriveNav(identity, positions, permissions, features, context ?? {})
    .flatMap((s) => s.items)
    .slice(0, 5);

  return (
    <nav className="fixed bottom-0 left-0 right-0 z-50 flex md:hidden border-t border-gray-100 bg-white safe-area-bottom">
      {items.map((item) => {
        const active = pathname === item.href || (item.href !== "/dashboard" && pathname.startsWith(item.href));
        const Icon = NAV_ICONS[item.icon] ?? LayoutDashboard;
        return (
          <Link
            key={item.href}
            href={item.href}
            className={cn(
              "flex flex-1 flex-col items-center justify-center gap-1 py-2 px-1 text-[10px] font-medium transition-colors min-h-[56px]",
              active ? "text-gray-900" : "text-gray-400"
            )}
          >
            <Icon className="h-5 w-5" style={active ? { color: primaryColor } : undefined} />
            <span className="truncate max-w-full px-0.5" style={active ? { color: primaryColor } : undefined}>
              {item.label}
            </span>
          </Link>
        );
      })}
    </nav>
  );
}
