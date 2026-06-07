import type { CertificatePublic, InspectionReport } from "@/lib/api";
import { env } from "@/lib/env";

export interface CertificationSummary {
  certificateId: string;
  verificationUrl: string;
  manufacturer: string;
  model: string;
  deviceType: string;
  platform: string;
  certificateLevel: string;
  status: string;
  expiresAt: string;
  coreTestsPassed: number;
  coreTestsTotal: number;
  cpu: string;
  ramGb: number;
  storageGb: number;
  storageType: string;
  batteryHealthPercent?: number;
  overallScore: number;
  condition: string;
  verificationConfidence: string;
  certificationDate: string;
}

const LETTER_GRADE_SCORES: Record<string, number> = {
  "A+": 98,
  A: 95,
  "A-": 92,
  "B+": 90,
  B: 85,
  "B-": 82,
  "C+": 78,
  C: 70,
  "C-": 65,
  D: 55,
  F: 40,
};

export function scoreToCondition(score: number): string {
  if (score >= 95) return "Excellent";
  if (score >= 85) return "Very Good";
  if (score >= 70) return "Good";
  if (score >= 50) return "Fair";
  return "Needs Attention";
}

export function verificationConfidenceLabel(score: number): string {
  if (score >= 90) return "High";
  if (score >= 75) return "Moderate";
  return "Limited";
}

function formatDeviceType(deviceType: string, platform: string): string {
  const normalized = deviceType.trim().replace(/_/g, " ");
  const label = normalized
    ? normalized.charAt(0).toUpperCase() + normalized.slice(1)
    : "Device";
  const platformLabel = platform.trim().replace(/_/g, " ");
  if (!platformLabel || platformLabel.toLowerCase() === "unknown") return label;
  const platformTitle = platformLabel.charAt(0).toUpperCase() + platformLabel.slice(1);
  return `${platformTitle} ${label}`;
}

export function buildVerificationUrl(certificateId: string, siteUrl = env.siteUrl): string {
  const base = siteUrl.replace(/\/$/, "");
  return `${base}/verify/${encodeURIComponent(certificateId)}`;
}

function parseNumericScore(value: unknown): number | null {
  if (value == null) return null;
  const text = String(value).trim();
  const slashMatch = text.match(/(\d{1,3})\s*\/\s*100/);
  if (slashMatch) {
    const score = Number(slashMatch[1]);
    return score > 0 ? clampScore(score) : null;
  }
  const numMatch = text.match(/\d{1,3}/);
  if (!numMatch) return null;
  const score = Number(numMatch[0]);
  return score > 0 ? clampScore(score) : null;
}

function clampScore(score: number): number {
  if (Number.isNaN(score)) return 70;
  return Math.max(0, Math.min(100, Math.round(score)));
}

function letterGradeToScore(grade: string | undefined): number | null {
  if (!grade) return null;
  const normalized = grade.trim().toUpperCase();
  if (LETTER_GRADE_SCORES[normalized] != null) return LETTER_GRADE_SCORES[normalized];
  return null;
}

function findAdvancedField(
  fields: Array<{ label?: string; value?: string }> | undefined,
  key: string,
): string | undefined {
  if (!fields?.length) return undefined;
  const target = key.replace(/_/g, " ").toLowerCase();
  const match = fields.find(
    (field) =>
      field.label?.toLowerCase().includes(target) ||
      field.label?.toLowerCase() === "overall score",
  );
  return match?.value;
}

function parseSpecsLine(specsLine: string): { cpu: string; ramGb: number; storageGb: number; storageType: string } {
  const parts = specsLine.split("·").map((part) => part.trim()).filter(Boolean);
  const cpu = parts[0] || "Unknown CPU";
  let ramGb = 0;
  let storageGb = 0;
  let storageType = "Storage";

  for (const part of parts.slice(1)) {
    const ramMatch = part.match(/(\d+)\s*GB\s*RAM/i);
    if (ramMatch) {
      ramGb = Number(ramMatch[1]);
      continue;
    }
    const storageMatch = part.match(/(\d+)\s*GB/i);
    if (storageMatch) {
      storageGb = Number(storageMatch[1]);
      if (/nvme/i.test(part)) storageType = "NVMe SSD";
      else if (/ssd/i.test(part)) storageType = "SSD";
      else if (/hdd|hard drive/i.test(part)) storageType = "HDD";
      else storageType = "Storage";
    }
  }

  return { cpu, ramGb, storageGb, storageType };
}

function extractSpecs(cert: CertificatePublic, report?: InspectionReport | null) {
  const overview = report?.device_overview as Record<string, unknown> | undefined;
  if (overview) {
    return {
      cpu: String(overview.cpu ?? overview.cpu_model ?? cert.device_name),
      ramGb: Number(overview.ram_gb ?? 0) || 0,
      storageGb: Number(overview.storage_gb ?? 0) || 0,
      storageType: String(overview.storage_type ?? inferStorageType(report)),
    };
  }

  const summaryLine = report?.summary?.specs_line;
  if (summaryLine) {
    return parseSpecsLine(summaryLine);
  }

  return {
    cpu: cert.device_name,
    ramGb: 0,
    storageGb: 0,
    storageType: inferStorageType(report),
  };
}

function inferStorageType(report?: InspectionReport | null): string {
  const storageText = report?.summary?.storage ?? report?.health_summary?.storage;
  const text = String(storageText ?? "").toLowerCase();
  if (text.includes("nvme")) return "NVMe SSD";
  if (text.includes("ssd")) return "SSD";
  if (text.includes("hdd") || text.includes("hard drive")) return "HDD";
  return "Storage";
}

export function extractOverallScore(cert: CertificatePublic): number {
  const report = cert.inspection_report;
  if (report) {
    const grade = report.summary?.certification_grade ?? report.certification_grade;
    const letterScore = letterGradeToScore(typeof grade === "string" ? grade : undefined);
    if (letterScore != null) return letterScore;

    const advanced = report.advanced as
      | { performance?: { benchmark?: Array<{ label?: string; value?: string }> } }
      | undefined;

    const benchmarkScore = findAdvancedField(advanced?.performance?.benchmark, "overall_score");
    const parsedBenchmark = parseNumericScore(benchmarkScore);
    if (parsedBenchmark != null) return parsedBenchmark;
  }

  const healthScores: number[] = [];
  if (cert.battery_health_percent != null && cert.battery_health_percent > 0) {
    healthScores.push(cert.battery_health_percent);
  }
  if (cert.storage_health_percent != null && cert.storage_health_percent > 0) {
    healthScores.push(cert.storage_health_percent);
  }
  if (healthScores.length) {
    return clampScore(healthScores.reduce((sum, value) => sum + value, 0) / healthScores.length);
  }

  return 75;
}

export function extractOverallScoreFromReport(
  report: InspectionReport | null | undefined,
  cert?: {
    battery_health_percent?: number | null;
    storage_health_percent?: number | null;
  },
): number {
  if (!report && cert) {
    return extractOverallScore({
      certificate_code: "",
      device_name: "",
      manufacturer: "",
      model: "",
      device_type: "",
      platform: "",
      certificate_level: "",
      status: "active",
      certification_date: new Date().toISOString(),
      expires_at: new Date().toISOString(),
      core_tests_passed: [],
      core_tests_total: 0,
      verification_url: "",
      qr_code_payload: "",
      public_url: "",
      battery_health_percent: cert.battery_health_percent ?? undefined,
      storage_health_percent: cert.storage_health_percent ?? undefined,
    });
  }

  return extractOverallScore({
    certificate_code: "",
    device_name: "",
    manufacturer: "",
    model: "",
    device_type: "",
    platform: "",
    certificate_level: "",
    status: "active",
    certification_date: new Date().toISOString(),
    expires_at: new Date().toISOString(),
    core_tests_passed: [],
    core_tests_total: 0,
    verification_url: "",
    qr_code_payload: "",
    public_url: "",
    battery_health_percent: cert?.battery_health_percent ?? undefined,
    storage_health_percent: cert?.storage_health_percent ?? undefined,
    inspection_report: report ?? undefined,
  });
}

export function buildCertificationSummary(
  cert: CertificatePublic,
  siteUrl = env.siteUrl,
): CertificationSummary {
  const report = cert.inspection_report;
  const specs = extractSpecs(cert, report);
  const overallScore = extractOverallScore(cert);

  return {
    certificateId: cert.certificate_code,
    verificationUrl: buildVerificationUrl(cert.certificate_code, siteUrl),
    manufacturer: cert.manufacturer || "Unknown",
    model: cert.model || cert.device_name,
    deviceType: formatDeviceType(cert.device_type, cert.platform),
    platform: cert.platform || "Unknown",
    certificateLevel: cert.certificate_level || "Standard",
    status: cert.status || "active",
    expiresAt: cert.expires_at,
    coreTestsPassed: cert.core_tests_passed?.length ?? 0,
    coreTestsTotal: cert.core_tests_total ?? 0,
    cpu: specs.cpu,
    ramGb: specs.ramGb,
    storageGb: specs.storageGb,
    storageType: specs.storageType,
    batteryHealthPercent: cert.battery_health_percent ?? undefined,
    overallScore,
    condition: scoreToCondition(overallScore),
    verificationConfidence: verificationConfidenceLabel(overallScore),
    certificationDate: cert.certification_date,
  };
}
