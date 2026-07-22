import { cookies } from "next/headers";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

async function fetchStats(token: string) {
  const headers = { Authorization: `Bearer ${token}` };
  const base = process.env.NEXT_PUBLIC_API_URL;
  const [users, classes, announcements] = await Promise.allSettled([
    fetch(`${base}/api/users?pageSize=1`, { headers, cache: "no-store" }).then((r) => r.json()),
    fetch(`${base}/api/classes?pageSize=1`, { headers, cache: "no-store" }).then((r) => r.json()),
    fetch(`${base}/api/announcements?pageSize=5`, { headers, cache: "no-store" }).then((r) => r.json()),
  ]);
  return {
    totalUsers: users.status === "fulfilled" ? users.value.total : 0,
    totalClasses: classes.status === "fulfilled" ? classes.value.total : 0,
    announcements: announcements.status === "fulfilled" ? announcements.value.items : [],
  };
}

export default async function DashboardPage() {
  const cookieStore = await cookies();
  const token = cookieStore.get("sp_token")?.value ?? "";
  const stats = await fetchStats(token);

  const statCards = [
    { label: "Total Users", value: stats.totalUsers, icon: "👥" },
    { label: "Classes", value: stats.totalClasses, icon: "🏫" },
    { label: "Announcements", value: stats.announcements.length, icon: "📢" },
  ];

  return (
    <div className="p-8">
      <div className="mb-8">
        <h1 className="text-3xl font-bold text-text-primary">Dashboard</h1>
        <p className="text-text-secondary mt-1">Welcome back to School Portal</p>
      </div>

      <div className="grid grid-cols-1 gap-6 sm:grid-cols-3 mb-8">
        {statCards.map((stat) => (
          <Card key={stat.label}>
            <CardContent className="flex items-center gap-4 p-6">
              <div className="text-4xl">{stat.icon}</div>
              <div>
                <p className="text-sm text-text-secondary">{stat.label}</p>
                <p className="text-3xl font-bold text-text-primary">{stat.value}</p>
              </div>
            </CardContent>
          </Card>
        ))}
      </div>

      {stats.announcements.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle>Recent Announcements</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            {stats.announcements.map((a: { announcementId: string; title: string; content: string; createdByName: string; createdAt: string }) => (
              <div key={a.announcementId} className="border-l-4 border-primary pl-4">
                <p className="font-medium text-text-primary">{a.title}</p>
                <p className="text-sm text-text-secondary mt-1 line-clamp-2">{a.content}</p>
                <p className="text-xs text-text-muted mt-1">
                  {a.createdByName} · {new Date(a.createdAt).toLocaleDateString()}
                </p>
              </div>
            ))}
          </CardContent>
        </Card>
      )}
    </div>
  );
}
