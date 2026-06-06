import Link from "next/link";

import { Button } from "@/components/Button";
import { SampleReportPreview } from "@/components/SampleReportPreview";

const BUYER_CHECKS = [
  "Device Identity",
  "Battery Health",
  "Storage Health",
  "Hardware Configuration",
  "Certification Date",
];

export default function SampleReportPage() {
  return (
    <div className="page-container">
      <header className="mx-auto mb-12 max-w-2xl text-center">
        <p className="text-sm font-medium text-[var(--color-brand)]">Sample certification</p>
        <h1 className="page-title mt-3">What a Certronx Report Looks Like</h1>
        <p className="page-subtitle mx-auto">
          Buyers receive a clear, professional certification report — not a technical diagnostic dump.
          Identity, condition, and verification status at a glance.
        </p>
      </header>

      <SampleReportPreview showCta={false} />

      <div className="mx-auto mt-12 max-w-2xl">
        <h2 className="text-lg font-semibold">Included in every report</h2>
        <ul className="check-list mt-4">
          {BUYER_CHECKS.map((item) => (
            <li key={item}>
              <span className="check-list__icon">✓</span>
              {item}
            </li>
          ))}
        </ul>
        <div className="mt-10 flex flex-wrap justify-center gap-3">
          <Button href="/download" variant="brand">
            Certify Your Device
          </Button>
          <Link href="/verify" className="btn btn-secondary">
            Verify a Device
          </Link>
        </div>
      </div>
    </div>
  );
}
