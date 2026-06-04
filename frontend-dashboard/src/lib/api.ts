import { env } from "@/lib/env";

const API_BASE = env.apiUrl;

export interface DashboardData {
  user_id: string;
  certificates: Array<{
    id: string;
    certificate_code: string;
    device_name: string;
    certificate_level: string;
    status: string;
    condition_grade?: string;
    issued_at: string;
    expires_at: string;
    public_url: string;
  }>;
  verification_count: number;
}

export async function getDashboard(userId: string): Promise<DashboardData> {
  const res = await fetch(`${API_BASE}/api/dashboard`, {
    headers: { "X-User-Id": userId },
    cache: "no-store",
  });
  if (!res.ok) throw new Error("Failed to load dashboard");
  return res.json();
}

export async function getAuthProfile(userId: string, email?: string) {
  const res = await fetch(`${API_BASE}/api/auth-profile`, {
    headers: {
      "X-User-Id": userId,
      ...(email ? { "X-User-Email": email } : {}),
    },
    cache: "no-store",
  });
  if (!res.ok) throw new Error("Failed to load profile");
  return res.json();
}
