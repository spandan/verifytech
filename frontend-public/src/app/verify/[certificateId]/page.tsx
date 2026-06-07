import Link from "next/link";
import { notFound } from "next/navigation";

import { SellCertifiedDeviceSection } from "@/components/SellCertifiedDeviceSection";
import { ShareCertificateClient } from "@/components/ShareCertificateClient";
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
    <div className="page-container py-10 md:py-14">
      <ShareCertificateClient event="VerificationViewed" certificateId={cert.certificate_code} />

      <SellCertifiedDeviceSection summary={summary} />

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
