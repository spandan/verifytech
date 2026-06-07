"use client";

import { SellCertifiedDeviceSection } from "@/components/SellCertifiedDeviceSection";
import type { CertificationSummary } from "@/lib/certification-summary";

/** Seller-focused resale trust document (replaces legacy share certificate UI). */
export function ShareCertificateSection({ summary }: { summary: CertificationSummary }) {
  return (
    <div className="mt-8">
      <SellCertifiedDeviceSection summary={summary} />
    </div>
  );
}
