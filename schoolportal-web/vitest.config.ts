import { defineConfig } from "vitest/config";
import { fileURLToPath } from "node:url";

// Step 8 — unit tests for the pure deriveNav sidebar logic. Mirrors the tsconfig "@/*" alias.
export default defineConfig({
  resolve: {
    alias: {
      "@": fileURLToPath(new URL("./", import.meta.url)),
    },
  },
  test: {
    environment: "node",
    include: ["lib/**/*.test.ts"],
  },
});
