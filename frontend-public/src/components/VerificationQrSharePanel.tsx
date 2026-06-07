"use client";

import { CertificateShareIcons } from "@/components/share/CertificateShareIcons";
import { CopyableVerificationUrl } from "@/components/CopyableVerificationUrl";
import { VerificationQrCode } from "@/components/VerificationQrCode";
import type { CertificationSummary } from "@/lib/certification-summary";

export function VerificationQrSharePanel({ summary }: { summary: CertificationSummary }) {
  return (
    <section className="card card-body mt-6 verification-share-panel">
      <div className="verification-share-panel__layout">
        <div className="verification-share-panel__copy">
          <p className="text-sm font-medium text-secondary">Verification URL</p>
          <CopyableVerificationUrl url={summary.verificationUrl} />
          <p className="text-sm text-muted">
            Share this link with buyers so they can confirm the certification before purchase.
          </p>
        </div>

        <div className="verification-share-panel__qr">
          <VerificationQrCode url={summary.verificationUrl} size={120} />
          <p className="verification-share-panel__share-label">Share via</p>
          <CertificateShareIcons summary={summary} variant="compact" />
        </div>
      </div>
    </section>
  );
}
