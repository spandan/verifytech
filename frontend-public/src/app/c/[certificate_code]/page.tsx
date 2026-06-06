import Link from "next/link";
import { notFound } from "next/navigation";
import { CertificateCard } from "@/components/CertificateCard";
import { InspectionReport } from "@/components/InspectionReport";
import { SaveLaptopPanel } from "@/components/SaveLaptopPanel";
import { ShareButton } from "@/components/ShareButton";
import { api } from "@/lib/api";
import { env } from "@/lib/env";

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

  const shareUrl =
    cert.public_url || `${env.siteUrl}/c/${cert.certificate_code}`;

  return (
    <div className="page-container page-container--cert py-10 md:py-14">
      <CertificateCard cert={cert} />

      {cert.inspection_report && (
        <InspectionReport report={cert.inspection_report} />
      )}

      <SaveLaptopPanel verificationCode={cert.certificate_code} deviceName={cert.device_name} />

      <div className="mt-8 flex flex-wrap items-center justify-center gap-3">
        <Link
          href={`/verify?code=${cert.certificate_code}`}
          className="btn btn-brand"
        >
          Verify this certification
        </Link>
        <ShareButton url={shareUrl} />
      </div>
    </div>
  );
}
