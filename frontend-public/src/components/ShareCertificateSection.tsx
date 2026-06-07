"use client";

import { CertificateShareIcons } from "@/components/share/CertificateShareIcons";
import type { CertificationSummary } from "@/lib/certification-summary";

export function ShareCertificateSection({ summary }: { summary: CertificationSummary }) {
  return (
    <section className="card card-body mt-8 share-certificate">
      <div className="share-certificate__header">
        <h2 className="text-lg font-semibold">Share Certificate</h2>
        <p className="mt-1 text-sm text-secondary">
          Copy a marketplace listing, share verification, or download a PDF certificate for buyers.
        </p>
      </div>

      <CertificateShareIcons summary={summary} />
    </section>
  );
}
