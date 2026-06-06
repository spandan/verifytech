import type { CertificatePublic } from "@/lib/api";
import { QRCodeSVG } from "qrcode.react";

const LEVEL_LABELS: Record<string, string> = {
  identity_verified: "Identity Verified",
  condition_certified: "Condition Certified",
  enhanced_certified: "Enhanced Certified",
};

function statusBadgeClass(status: string): string {
  switch (status) {
    case "active":
      return "badge-success";
    case "expired":
      return "badge-warning";
    case "revoked":
      return "badge-error";
    default:
      return "badge-neutral";
  }
}

export function CertificateCard({ cert }: { cert: CertificatePublic }) {
  const testsPassed = cert.core_tests_passed.length;
  const testsTotal = cert.core_tests_total;

  return (
    <article className="card card--certificate">
      <div className="card-cert-header">
        <span>Certronx Certified</span>
        <span className={`badge ${statusBadgeClass(cert.status)} !bg-white/95`}>
          {cert.status}
        </span>
      </div>

      <div className="card-body">
        <div className="flex flex-col gap-8 md:flex-row md:justify-between">
          <div className="flex-1 space-y-6">
            <header>
              <h1 className="text-2xl font-semibold tracking-tight md:text-3xl">
                {cert.device_name}
              </h1>
              <p className="mt-1 text-secondary capitalize text-sm">
                {cert.device_type} · {cert.platform}
              </p>
            </header>

            <div className="flex flex-wrap gap-2">
              <span className="badge badge-trust">
                {LEVEL_LABELS[cert.certificate_level] || cert.certificate_level}
              </span>
              {cert.condition_grade && (
                <span className="badge badge-info">Grade {cert.condition_grade}</span>
              )}
            </div>

            <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
              <Metric label="Certificate ID" value={cert.certificate_code} mono />
              <Metric
                label="Certified"
                value={new Date(cert.certification_date).toLocaleDateString()}
              />
              <Metric label="Valid until" value={new Date(cert.expires_at).toLocaleDateString()} />
              {cert.battery_health_percent != null && (
                <Metric label="Battery health" value={`${cert.battery_health_percent}%`} />
              )}
              {cert.storage_health_percent != null && (
                <Metric
                  label="Storage health"
                  value={`${Math.round(cert.storage_health_percent)}%`}
                />
              )}
              <Metric
                label="Checks passed"
                value={
                  testsTotal > 0
                    ? `${testsPassed} of ${testsTotal} verified`
                    : "See report below"
                }
              />
            </div>

            {testsPassed > 0 && (
              <p className="text-sm text-secondary">
                Interactive checks completed for{" "}
                {cert.core_tests_passed.slice(0, 4).join(", ")}
                {cert.core_tests_passed.length > 4
                  ? `, and ${cert.core_tests_passed.length - 4} more`
                  : ""}
                . Full details are in the inspection report below.
              </p>
            )}

            <p className="text-xs text-muted">
              Identifiers are masked for privacy. Anyone can verify this certificate at the link
              below.
            </p>
          </div>

          <aside className="flex flex-col items-center gap-3 md:min-w-[160px]">
            <div className="qr-frame">
              <QRCodeSVG value={cert.qr_code_payload} size={132} />
            </div>
            <p className="text-center text-xs text-muted max-w-[140px]">
              Scan to verify this device
            </p>
          </aside>
        </div>
      </div>
    </article>
  );
}

function Metric({
  label,
  value,
  mono,
}: {
  label: string;
  value: string;
  mono?: boolean;
}) {
  return (
    <div className="metric-cell">
      <p className="metric-cell__label">{label}</p>
      <p className={`metric-cell__value ${mono ? "metric-cell__value--mono" : ""}`}>{value}</p>
    </div>
  );
}
