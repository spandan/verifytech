import type { CertificatePublic } from "@/lib/api";
import { buildCertificationSummary } from "@/lib/certification-summary";

import { VerificationQrCode } from "@/components/VerificationQrCode";

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
  const summary = buildCertificationSummary(cert);
  const testsPassed = cert.core_tests_passed.length;
  const testsTotal = cert.core_tests_total;
  const grade = cert.condition_grade ?? cert.inspection_report?.summary?.certification_grade;

  return (
    <article className="card card--certificate">
      <div className="card-cert-header">
        <span>Certronx Certified</span>
        <span className={`badge ${statusBadgeClass(cert.status)} !bg-white/95`}>
          {cert.status}
        </span>
      </div>

      <div className="card-body">
        <div className="cert-hero">
          <div className="cert-hero__main">
            <header>
              <h1 className="cert-hero__title">{cert.device_name}</h1>
              <p className="cert-hero__meta capitalize">
                {cert.device_type} · {cert.platform}
              </p>
            </header>

            <div className="cert-hero__badges">
              {grade && (
                <span className="cert-hero__grade" aria-label={`Condition grade ${grade}`}>
                  Grade {grade}
                </span>
              )}
              <span className="badge badge-trust">
                {LEVEL_LABELS[cert.certificate_level] || cert.certificate_level}
              </span>
            </div>

            <div className="cert-hero__metrics">
              <Metric label="Certificate ID" value={cert.certificate_code} mono />
              <Metric
                label="Certified"
                value={new Date(cert.certification_date).toLocaleDateString()}
              />
              <Metric label="Valid until" value={new Date(cert.expires_at).toLocaleDateString()} />
              <Metric
                label="Checks passed"
                value={
                  testsTotal > 0
                    ? `${testsPassed} of ${testsTotal} verified`
                    : "See report below"
                }
              />
            </div>

            <p className="cert-hero__hint">
              Scan the QR code or use the verification link to confirm this certificate is genuine.
            </p>
          </div>

          <aside className="cert-hero__qr">
            <VerificationQrCode url={summary.verificationUrl} />
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
