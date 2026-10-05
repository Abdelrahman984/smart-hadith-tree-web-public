import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // Self-contained server output for the Docker image (frontend/Dockerfile).
  output: "standalone",
  // `next dev` blocks dev resources requested from other origins. Playwright opens the app at 127.0.0.1 (see
  // playwright.config.ts); without this the page never hydrates and the graph tests see an empty canvas. Dev only.
  allowedDevOrigins: ["127.0.0.1"],
};

export default nextConfig;
