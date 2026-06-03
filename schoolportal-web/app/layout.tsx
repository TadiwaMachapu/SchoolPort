import type { Metadata } from "next";
import "./globals.css";
import { QueryProvider } from "@/shared/providers/QueryProvider";
import { ToastContainer } from "@/components/toast-container";

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
    <html lang="en" className="h-full" suppressHydrationWarning>
      <body className="h-full bg-gray-50 antialiased">
        <QueryProvider>
          {children}
          <ToastContainer />
        </QueryProvider>
      </body>
    </html>
  );
}
