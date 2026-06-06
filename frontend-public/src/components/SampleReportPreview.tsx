import Link from "next/link";
import { QRCodeSVG } from "qrcode.react";

import { Button } from "@/components/Button";
import { ConditionBadge, ScoreDisplay } from "@/components/ConditionBadge";
import { env } from "@/lib/env";

interface Props {
  showCta?: boolean;
  compact?: boolean;
}

export function SampleReportPreview({ showCta = true, compact = false }: Props) {
  const certId = "CTX-93A8-K2L4";
  const verifyUrl = `${env.siteUrl.replace(/\/$/, "")}/verify/${certId}`;

  return (
    <div className={compact ? "sample-report-card max-w-md mx-auto" : "sample-report-card"}>
      <div className="sample-report-card__header">
        <p className="text-xs font-semibold uppercase tracking-widest opacity-90">Certronx Certification</p>
        <h3 className="mt-2 text-2xl font-semibold tracking-tight">Dell Latitude 7420</h3>
        <p className="mt-1 text-sm opacity-90">Trusted device report · Active certification</p>
      </div>
      <div className="sample-report-card__body space-y-6">
        <div className="flex flex-wrap items-center gap-3">
          <ConditionBadge score={92} label="Very Good" />
          <ScoreDisplay score={92} />
        </div>
        <div className="grid gap-3 sm:grid-cols-2">
          <div className="sample-metric">
            <p className="sample-metric__label">Certification Status</p>
            <p className="sample-metric__value text-[var(--color-success-text)]">Certified</p>
          </div>
          <div className="sample-metric">
            <p className="sample-metric__label">Battery Health</p>
            <p className="sample-metric__value">92%</p>
          </div>
          <div className="sample-metric">
            <p className="sample-metric__label">Storage</p>
            <p className="sample-metric__value">512 GB SSD</p>
          </div>
          <div className="sample-metric">
            <p className="sample-metric__label">Certificate ID</p>
            <p className="sample-metric__value font-mono text-sm">{certId}</p>
          </div>
        </div>
        <div className="flex flex-col items-start justify-between gap-4 border-t border-[var(--color-border)] pt-6 sm:flex-row sm:items-center">
          <div>
            <p className="text-xs uppercase tracking-wide text-muted">Verification portal</p>
            <Link href={verifyUrl} className="mt-1 text-sm text-[var(--color-brand)] hover:underline">
              Scan or open verification link
            </Link>
          </div>
          <div className="qr-frame">
            <QRCodeSVG value={verifyUrl} size={72} level="M" />
          </div>
        </div>
        {showCta && (
          <Button href="/sample-report" variant="secondary" className="btn-block">
            View Sample Report
          </Button>
        )}
      </div>
    </div>
  );
}
