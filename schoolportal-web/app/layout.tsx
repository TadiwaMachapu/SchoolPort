import type { Metadata } from "next";
import { Plus_Jakarta_Sans } from "next/font/google";
import "./globals.css";
import { QueryProvider } from "@/shared/providers/QueryProvider";
import { ToastContainer } from "@/components/toast-container";

// Typography is CRAFT, not brand — loaded globally here, not from SchoolTheme.
// Exposed as --font-jakarta, which globals.css wires into --font-sans.
const jakarta = Plus_Jakarta_Sans({
  subsets: ["latin"],
  weight: ["400", "500", "600", "700"],
  variable: "--font-jakarta",
  display: "swap",
});

export const metadata: Metadata = {
  title: "School Portal",
  description: "Your school management platform",
  manifest: "/manifest.json",
  appleWebApp: { capable: true, statusBarStyle: "default", title: "School Portal" },
  formatDetection: { telephone: false },
  icons: { icon: "/icon-192.svg", apple: "/icon-192.svg" },
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" className={`h-full ${jakarta.variable}`} suppressHydrationWarning>
      <body className="h-full bg-surface-page antialiased">
        <QueryProvider>
          {children}
          <ToastContainer />
        </QueryProvider>
      </body>
    </html>
  );
}
