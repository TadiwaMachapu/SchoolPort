import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { Sidebar } from "@/components/sidebar";
import type { SchoolFeatures, SchoolTheme } from "@/lib/theme";

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

  const [me, school] = await Promise.all([getMe(token), getSchool(token)]);

  // API unreachable or token invalid — show a helpful error instead of looping
  if (!me) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-gray-900 p-8">
        <div className="text-center text-white max-w-md">
          <div className="text-5xl mb-4">🔌</div>
          <h1 className="text-2xl font-bold mb-2">API server not reachable</h1>
          <p className="text-gray-400 mb-6">
            Make sure the backend is running:
          </p>
          <code className="block bg-gray-800 rounded-lg p-4 text-left text-sm text-green-400 mb-6">
            cd C:\Projects\SchoolPort\SchoolPortal.Server{"\n"}
            dotnet run
          </code>
          <p className="text-gray-500 text-sm">
            Then refresh this page. The API should be at{" "}
            <span className="text-gray-300">{API}</span>
          </p>
        </div>
      </div>
    );
  }

  const theme: SchoolTheme = school?.theme ?? { primaryColor: "#1E40AF", fontFamily: "Inter" };
  const features: SchoolFeatures = school?.features ?? {};

  return (
    <div
      className="flex h-screen overflow-hidden"
      style={{ "--color-primary": theme.primaryColor } as React.CSSProperties}
    >
      <Sidebar user={me.user} school={{ ...me.school, theme, features }} />
      <main className="flex-1 overflow-y-auto bg-gray-50">{children}</main>
    </div>
  );
}
