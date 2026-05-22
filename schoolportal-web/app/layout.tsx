import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "School Portal",
  description: "Your school management platform",
  manifest: "/manifest.json",
  appleWebApp: { capable: true, statusBarStyle: "default", title: "School Portal" },
  formatDetection: { telephone: false },
  icons: { icon: "/icon-192.png", apple: "/icon-192.png" },
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" className="h-full">
      <body className="h-full bg-gray-50 antialiased">{children}</body>
    </html>
  );
}
