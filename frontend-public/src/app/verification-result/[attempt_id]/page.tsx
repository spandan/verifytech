import Link from "next/link";
import { notFound } from "next/navigation";
import { VerificationDetails } from "@/components/VerificationDetails";
import { api } from "@/lib/api";

interface Props {
  params: Promise<{ attempt_id: string }>;
}

const RESULT_CONFIG: Record<
  string,
  { title: string; variant: "success" | "warning" | "error" | "neutral"; icon: string; description: string }
> = {
  CERTIFIED_MATCH: {
    title: "Certified Match",
    variant: "success",
    icon: "✓",
    description: "This device matches the certified report. You can proceed with confidence.",
  },
  CERTIFIED_WITH_CHANGES: {
    title: "Certified with Changes",
    variant: "warning",
    icon: "!",
    description:
      "The device identity matches, but some condition details have changed since certification.",
  },
  DEVICE_MISMATCH: {
    title: "Device Mismatch",
    variant: "error",
    icon: "✕",
    description: "This device does not match the certified device. Do not rely on this certificate.",
  },
  CERTIFICATE_NOT_FOUND: {
    title: "Certificate Not Found",
    variant: "neutral",
    icon: "?",
    description: "No certificate was found with the provided code.",
  },
  CERTIFICATE_EXPIRED: {
    title: "Certificate Expired",
    variant: "warning",
    icon: "⏱",
    description: "This certificate has expired. Ask the seller for a new certification.",
  },
  CERTIFICATE_REVOKED: {
    title: "Certificate Revoked",
    variant: "error",
    icon: "✕",
    description: "This certificate has been revoked and is no longer valid.",
  },
};

export default async function VerificationResultPage({ params }: Props) {
  const { attempt_id } = await params;

  let result;
  try {
    result = await api.getVerificationAttempt(attempt_id);
  } catch {
    notFound();
  }

  const config = RESULT_CONFIG[result.result] || RESULT_CONFIG.CERTIFICATE_NOT_FOUND;
  const hasTechnical =
    result.identity_match_score > 0 ||
    result.value_match_score > 0 ||
    result.changes.length > 0;

  return (
    <div className="page-container page-container--narrow">
      <div className={`result-banner result-banner--${config.variant}`}>
        <div className="result-banner__icon">{config.icon}</div>
        <h1 className="result-banner__title">{config.title}</h1>
        <p className="result-banner__desc">{config.description}</p>
        {result.device_name && (
          <p className="mt-4 text-sm text-secondary">{result.device_name}</p>
        )}
        {result.certificate_code && (
          <p className="mt-1 font-mono text-xs text-muted">{result.certificate_code}</p>
        )}
      </div>

      {result.value_estimate_invalidated && (
        <p className="alert alert-warning mt-6">
          The previous value estimate may no longer apply due to hardware changes.
        </p>
      )}

      {hasTechnical && (
        <div className="mt-6">
          <VerificationDetails
            identityScore={result.identity_match_score}
            valueScore={result.value_match_score}
            changes={result.changes}
          />
        </div>
      )}

      <div className="mt-8 flex flex-wrap justify-center gap-3">
        {result.certificate_code && (
          <Link href={`/c/${result.certificate_code}`} className="btn btn-secondary">
            View certificate
          </Link>
        )}
        <Link href="/verify" className="btn btn-trust">
          Verify another
        </Link>
      </div>
    </div>
  );
}
