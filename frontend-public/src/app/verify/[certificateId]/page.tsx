import Link from "next/link";
import { notFound } from "next/navigation";

import { BuyerDeviceSummary } from "@/components/BuyerDeviceSummary";
import { ShareCertificateSection } from "@/components/ShareCertificateSection";
import { ShareCertificateClient } from "@/components/ShareCertificateClient";
import { CopyableVerificationUrl } from "@/components/CopyableVerificationUrl";
import { api } from "@/lib/api";
import { buildCertificationSummary } from "@/lib/certification-summary";

interface Props {
  params: Promise<{ certificateId: string }>;
}

export default async function VerifyCertificatePage({ params }: Props) {
  const { certificateId } = await params;
  const code = decodeURIComponent(certificateId).toUpperCase();

  let cert;
  try {
    cert = await api.getCertificate(code);
  } catch {
    notFound();
  }

  const summary = buildCertificationSummary(cert);

  return (
    <div className="page-container page-container--narrow py-10 md:py-14">
      <ShareCertificateClient event="VerificationViewed" certificateId={cert.certificate_code} />

      <div className="verified-banner">
        <span className="verified-banner__icon">✓</span>
        <div>
          <p className="verified-banner__title">Verified by Certronx</p>
          <p className="verified-banner__subtitle">
            This certificate is registered in the Certronx trust platform.
          </p>
        </div>
      </div>

      <BuyerDeviceSummary summary={summary} />

      <section className="card card-body mt-6">
        <div className="flex flex-col items-center gap-4 sm:flex-row sm:items-start sm:justify-between">
          <div className="space-y-2 text-center sm:text-left">
            <p className="text-sm font-medium text-secondary">Verification URL</p>
            <CopyableVerificationUrl url={summary.verificationUrl} />
            <p className="text-sm text-muted">
              Share this link with buyers so they can confirm the certification before purchase.
            </p>
          </div>
          <VerificationQrCode url={summary.verificationUrl} size={120} />
        </div>
      </section>

      <ShareCertificateSection summary={summary} />

      <div className="mt-8 flex flex-wrap justify-center gap-3">
        <Link href={`/c/${cert.certificate_code}`} className="btn btn-brand">
          View full report
        </Link>
        <Link href={`/verify?code=${cert.certificate_code}`} className="btn btn-secondary">
          Run live device check
        </Link>
      </div>
    </div>
  );
}
