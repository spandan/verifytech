import Link from "next/link";
import { notFound } from "next/navigation";

import { CertificateCard } from "@/components/CertificateCard";
import { InspectionReport } from "@/components/InspectionReport";
import { SaveLaptopPanel } from "@/components/SaveLaptopPanel";
import { ShareCertificateSection } from "@/components/ShareCertificateSection";
import { ShareCertificateClient } from "@/components/ShareCertificateClient";
import { api } from "@/lib/api";
import { buildCertificationSummary } from "@/lib/certification-summary";

interface Props {
  params: Promise<{ certificate_code: string }>;
}

export default async function CertificatePage({ params }: Props) {
  const { certificate_code } = await params;
  const code = decodeURIComponent(certificate_code).toUpperCase();

  let cert;
  try {
    cert = await api.getCertificate(code);
  } catch {
    notFound();
  }

  const summary = buildCertificationSummary(cert);

  return (
    <div className="page-container page-container--cert py-10 md:py-14">
      <ShareCertificateClient event="CertificateGenerated" certificateId={cert.certificate_code} />

      <CertificateCard cert={cert} />

      {cert.inspection_report && (
        <InspectionReport
          report={cert.inspection_report}
          certContext={{
            battery_health_percent: cert.battery_health_percent,
            storage_health_percent: cert.storage_health_percent,
          }}
        />
      )}

      <ShareCertificateSection summary={summary} />

      <SaveLaptopPanel verificationCode={cert.certificate_code} deviceName={cert.device_name} />

      <div className="mt-8 flex flex-wrap items-center justify-center gap-3">
        <Link href={summary.verificationUrl} className="btn btn-brand">
          Open verification portal
        </Link>
        <Link href={`/verify?code=${cert.certificate_code}`} className="btn btn-secondary">
          Run live device check
        </Link>
      </div>
    </div>
  );
}
