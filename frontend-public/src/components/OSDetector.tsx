"use client";

import { useEffect, useState } from "react";
import { api } from "@/lib/api";

const PLATFORM_LABELS: Record<string, string> = {
  windows: "Windows",
  macos: "macOS",
  android: "Android",
  linux: "Linux",
  ios: "iOS",
  unknown: "Unknown",
};

export function OSDetector() {
  const [platform, setPlatform] = useState<string | null>(null);

  useEffect(() => {
    api.detectPlatform().then((r) => setPlatform(r.platform)).catch(() => setPlatform("unknown"));
  }, []);

  if (!platform) return null;

  return (
    <span className="trust-pill">
      <span className="trust-pill__dot" />
      {PLATFORM_LABELS[platform] || platform} detected
    </span>
  );
}

export function getRecommendedPlatform(): string {
  if (typeof window === "undefined") return "windows";
  const ua = navigator.userAgent.toLowerCase();
  if (ua.includes("windows")) return "windows";
  if (ua.includes("mac")) return "macos";
  if (ua.includes("android")) return "android";
  return "windows";
}
