import { env } from "@/lib/env";
import { getAccessToken, getSupabaseClient } from "@/lib/supabase";

const API_BASE = env.apiUrl;

async function request<T>(
  path: string,
  options?: RequestInit,
  auth = false,
  retried = false,
): Promise<T> {
  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    ...(options?.headers as Record<string, string> | undefined),
  };
  if (auth) {
    const token = await getAccessToken();
    if (token) headers.Authorization = `Bearer ${token}`;
  }
  const res = await fetch(`${API_BASE}${path}`, { ...options, headers });
  if (res.status === 401 && auth && !retried) {
    const supabase = getSupabaseClient();
    if (supabase) {
      const { data, error } = await supabase.auth.refreshSession();
      if (!error && data.session) {
        return request(path, options, auth, true);
      }
    }
  }
  if (!res.ok) {
    const err = await res.json().catch(() => ({ detail: res.statusText }));
    const detail = err.detail;
    throw new Error(typeof detail === "string" ? detail : "Request failed");
  }
  return res.json();
}

export interface IntakePayload {
  device_category: string;
  approximate_purchase_year: number;
  zip_code_or_region: string;
  charger_included?: string;
}

export interface AgentInfo {
  platform: string;
  version: string;
  download_url: string;
  checksum: string;
  full_download_url: string;
}

export interface InspectionReportSummary {
  certification_grade?: string;
  grade_subtitle?: string;
  device_name?: string;
  specs_line?: string;
  battery?: string;
  storage?: string;
  performance?: string;
  memory?: string;
  thermals?: string;
  screen?: string;
  functional?: Record<string, string>;
  security?: {
    headline?: string;
    secure_boot?: string;
    encryption?: string;
    tpm?: string;
  };
  resale_readiness?: string;
  warnings?: Array<{ title: string; explanation: string }>;
}

export interface InspectionReport {
  version?: string;
  /** Layer 1 — plain-language buyer summary (preferred). */
  summary?: InspectionReportSummary;
  /** Layer 2 — technical details (collapsed in UI). */
  advanced?: Record<string, unknown>;
  /** @deprecated Legacy flat shape — UI normalizes automatically. */
  device_overview?: Record<string, unknown>;
  health_summary?: Record<string, unknown>;
  functional_tests?: Record<string, string>;
  security?: Record<string, unknown>;
  warnings?: string[];
  certification_grade?: string;
  refurbisher_notes?: string;
  expected_service_life?: string;
  evidence?: Array<{
    artifact_type: string;
    label: string;
    signed_url?: string;
    storage_path?: string;
  }>;
  sections?: Record<string, string>;
}

export interface CertificatePublic {
  certificate_code: string;
  device_name: string;
  manufacturer: string;
  model: string;
  device_type: string;
  platform: string;
  certificate_level: string;
  status: string;
  condition_grade?: string;
  certification_date: string;
  expires_at: string;
  battery_health_percent?: number;
  storage_health_percent?: number;
  core_tests_passed: string[];
  core_tests_total: number;
  verification_url: string;
  qr_code_payload: string;
  public_url: string;
  inspection_report?: InspectionReport | null;
  agent_provenance?: Record<string, unknown> | null;
}

export interface VerificationResult {
  attempt_id: string;
  result: string;
  identity_match_score: number;
  value_match_score: number;
  summary: string;
  changes: Array<{ field: string; certified_value: unknown; live_value: unknown }>;
  value_estimate_invalidated: boolean;
  certificate_code: string;
  device_name?: string;
}

export interface MyLaptop {
  device_id: string;
  nickname?: string | null;
  device_name: string;
  manufacturer?: string | null;
  model?: string | null;
  serial_last4?: string | null;
  last_scan_at?: string | null;
  verification_status: string;
  verification_code: string;
  public_report_url: string;
  report_token?: string | null;
}

export const api = {
  detectPlatform: () => request<{ platform: string }>("/api/agents/detect"),
  getAgent: (platform: string) => request<AgentInfo>(`/api/agents/${platform}`),
  submitIntake: (data: IntakePayload) =>
    request<{ id: string }>("/api/intake", { method: "POST", body: JSON.stringify(data) }),
  getCertificate: (code: string) => request<CertificatePublic>(`/api/certificates/${code}`),
  lookupCertificate: (code: string) =>
    request<{ exists: boolean; device_name?: string; status?: string }>(
      `/api/certificates/lookup/${encodeURIComponent(code)}`
    ),
  verifyLookup: (code: string) =>
    request<{ exists: boolean; device_name?: string; status?: string }>("/api/verify/lookup", {
      method: "POST",
      body: JSON.stringify({ certificate_code: code }),
    }),
  getVerificationAttempt: (id: string) =>
    request<VerificationResult>(`/api/verify/attempts/${id}`),
  getMyLaptops: () => request<{ laptops: MyLaptop[] }>("/api/account/laptops", undefined, true),
  createScanLinkToken: () =>
    request<{ token: string; expires_at: string }>("/api/account/scan-link-token", { method: "POST" }, true),
  renameDevice: (deviceId: string, nickname: string) =>
    request<MyLaptop>(
      `/api/account/devices/${deviceId}`,
      { method: "PATCH", body: JSON.stringify({ nickname }) },
      true,
    ),
  claimReport: (verificationCode: string) =>
    request<{ device_id: string; verification_code: string; public_report_url: string; message: string }>(
      "/api/account/reports/claim",
      { method: "POST", body: JSON.stringify({ verification_code: verificationCode }) },
      true,
    ),
  emailReport: (verificationCode: string, email: string) =>
    request<{ sent: boolean }>(
      "/api/account/reports/email",
      { method: "POST", body: JSON.stringify({ verification_code: verificationCode, email }) },
    ),
};
