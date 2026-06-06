import type { CertificatePublic } from "@/lib/api";
import { buildCertificationSummary } from "@/lib/certification-summary";

import { ConditionBadge, ScoreDisplay } from "@/components/ConditionBadge";
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

            <div className="flex flex-wrap items-center gap-3">
              <ConditionBadge score={summary.overallScore} label={summary.condition} />
              <ScoreDisplay score={summary.overallScore} />
            </div>

            <div className="flex flex-wrap gap-2">
              <span className="badge badge-trust">
                {LEVEL_LABELS[cert.certificate_level] || cert.certificate_level}
              </span>
            </div>

            <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
              <Metric label="Certificate ID" value={cert.certificate_code} mono />
              <Metric label="CPU" value={summary.cpu} />
              <Metric
                label="RAM"
                value={summary.ramGb > 0 ? `${summary.ramGb} GB` : "See report"}
              />
              <Metric
                label="Storage"
                value={
                  summary.storageGb > 0
                    ? `${summary.storageGb} GB ${summary.storageType}`
                    : summary.storageType
                }
              />
              <Metric
                label="Certified"
                value={new Date(cert.certification_date).toLocaleDateString()}
              />
              <Metric label="Valid until" value={new Date(cert.expires_at).toLocaleDateString()} />
              {summary.batteryHealthPercent != null && (
                <Metric
                  label="Battery health"
                  value={`${Math.round(summary.batteryHealthPercent)}%`}
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

            <p className="text-xs text-muted">
              Buyers can verify this certificate instantly using the QR code or verification link.
            </p>
          </div>

          <aside className="flex flex-col items-center gap-3 md:min-w-[160px]">
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
