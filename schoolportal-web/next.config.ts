import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  reactCompiler: true,
  // Explicitly opt into Turbopack (default in Next.js 16)
  turbopack: {},
};

// next-pwa uses webpack — only apply it during production builds.
// In development, Turbopack runs and doesn't need the service worker.
function applyPWA(config: NextConfig): NextConfig {
  if (process.env.NODE_ENV !== "production") return config;
  // eslint-disable-next-line @typescript-eslint/no-require-imports
  const withPWA = require("next-pwa")({
    dest: "public",
    register: true,
    skipWaiting: true,
    runtimeCaching: [
      {
        urlPattern: /^https:\/\/.*\.supabase\.co\/storage\/.*/,
        handler: "CacheFirst",
        options: {
          cacheName: "supabase-storage",
          expiration: { maxAgeSeconds: 60 * 60 * 24 * 7 },
        },
      },
      {
        urlPattern: /\/api\/.*/,
        handler: "NetworkFirst",
        options: { cacheName: "api-cache", networkTimeoutSeconds: 10 },
      },
    ],
  });
  return withPWA(config);
}

export default applyPWA(nextConfig);
