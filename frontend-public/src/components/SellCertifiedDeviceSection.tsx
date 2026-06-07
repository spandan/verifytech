"use client";

import { useState } from "react";

import { CopyButton } from "@/components/CopyButton";
import { useToast } from "@/components/ToastProvider";
import { VerificationQrCode } from "@/components/VerificationQrCode";
import { trackEvent } from "@/lib/analytics";
import type { CertificationSummary } from "@/lib/certification-summary";
import { downloadPdfCertificate } from "@/lib/pdf-certificate";
import {
  craigslistTemplate,
  facebookMarketplaceTemplate,
  genericListingTemplate,
  marketplaceListingTitle,
  olxTemplate,
  whatsappShareUrl,
} from "@/lib/share-templates";

export function SellCertifiedDeviceSection({ summary }: { summary: CertificationSummary }) {
  const { showToast } = useToast();
  const [downloading, setDownloading] = useState(false);
  const [moreOpen, setMoreOpen] = useState(false);

  const deviceName = `${summary.manufacturer} ${summary.model}`.trim();
  const certifiedDate = new Date(summary.certificationDate).toLocaleDateString(undefined, {
    year: "numeric",
    month: "long",
    day: "numeric",
  });
  const validThrough = new Date(summary.expiresAt).toLocaleDateString(undefined, {
    year: "numeric",
    month: "long",
    day: "numeric",
  });
  const ramLabel = summary.ramGb > 0 ? `${summary.ramGb} GB` : "See report";
  const storageLabel =
    summary.storageGb > 0
      ? `${summary.storageGb} GB ${summary.storageType}`
      : summary.storageType;
  const batteryLabel =
    summary.batteryHealthPercent != null
      ? `${Math.round(summary.batteryHealthPercent)}%`
      : "Not reported";
  const listingTitle = marketplaceListingTitle(summary);
  const listingPreview = genericListingTemplate(summary);
  const attestationLabel =
    summary.coreTestsTotal > 0
      ? `${summary.coreTestsPassed} of ${summary.coreTestsTotal} core hardware checks passed`
      : "Independent hardware attestation completed";

  async function copyText(text: string, toast: string, eventName?: Parameters<typeof trackEvent>[0]) {
    await navigator.clipboard.writeText(text);
    if (eventName) trackEvent(eventName, { certificateId: summary.certificateId });
    showToast(toast);
  }

  async function handleDownloadPdf() {
    setDownloading(true);
    try {
      await downloadPdfCertificate(summary);
      trackEvent("PdfDownloaded", { certificateId: summary.certificateId });
      showToast("PDF certificate downloaded.");
    } finally {
      setDownloading(false);
    }
  }

  return (
    <section className="sell-certified" aria-labelledby="sell-certified-title">
      <header className="sell-certified__intro">
        <p className="sell-certified__eyebrow">Resale trust document</p>
        <h2 id="sell-certified-title" className="sell-certified__title">
          Sell Your Certified Device
        </h2>
        <p className="sell-certified__lede">
          Certronx gives buyers independent proof of hardware condition — the same kind of confidence
          buyers expect from a vehicle history report or manufacturer warranty lookup.
        </p>
      </header>

      <div className="sell-certified__verified">
        <div className="sell-certified__badge" aria-label="Certification verified">
          <span className="sell-certified__badge-icon" aria-hidden="true">
            ✓
          </span>
          Verified
        </div>
        <div className="sell-certified__verified-copy">
          <p className="sell-certified__verified-title">Certronx certification is active</p>
          <p className="sell-certified__verified-text">
            Record status: <strong className="capitalize">{summary.status.replace(/_/g, " ")}</strong>
            {" · "}
            Valid through {validThrough}
            {" · "}
            {attestationLabel}
          </p>
        </div>
      </div>

      <article className="sell-certified__panel">
        <h3 className="sell-certified__panel-title">Device Summary</h3>
        <div className="sell-certified__device-header">
          <p className="sell-certified__device-name">{deviceName}</p>
          <p className="sell-certified__device-type">{summary.deviceType}</p>
        </div>
        <dl className="sell-certified__spec-grid">
          <SpecItem label="CPU" value={summary.cpu} />
          <SpecItem label="RAM" value={ramLabel} />
          <SpecItem label="Storage" value={storageLabel} />
          <SpecItem label="Battery Health" value={batteryLabel} />
          <SpecItem label="Certification Score" value={`${summary.overallScore}/100`} highlight />
          <SpecItem label="Condition" value={summary.condition} />
          <SpecItem label="Certification Date" value={certifiedDate} />
          <SpecItem label="Verification ID" value={summary.certificateId} mono copyable />
        </dl>
        <div className="sell-certified__trust-note">
          <p>
            <strong>Certificate level:</strong> {summary.certificateLevel}
          </p>
          <p>
            <strong>Verification confidence:</strong> {summary.verificationConfidence} (
            {summary.overallScore}/100)
          </p>
        </div>
      </article>

      <article className="sell-certified__panel sell-certified__panel--verification">
        <div className="sell-certified__verification-copy">
          <h3 className="sell-certified__panel-title">Buyer Verification</h3>
          <p className="sell-certified__panel-desc">
            Buyers can independently verify this certification by scanning the QR code.
          </p>
          <ul className="sell-certified__verification-list">
            <li>Confirms this certification ID is registered with Certronx</li>
            <li>Shows the official device summary and inspection record</li>
            <li>Works without installing an app — scan and open in browser</li>
          </ul>
        </div>
        <div className="sell-certified__verification-qr">
          <VerificationQrCode
            url={summary.verificationUrl}
            size={112}
            label="Scan to verify authenticity"
          />
        </div>
      </article>

      <article className="sell-certified__panel">
        <h3 className="sell-certified__panel-title">Selling Tools</h3>
        <p className="sell-certified__panel-desc">
          Use the listing preview below in marketplace posts. Buyers can confirm every claim using
          your verification link.
        </p>

        <div className="sell-certified__listing-preview">
          <p className="sell-certified__listing-label">Marketplace listing preview</p>
          <div className="sell-certified__listing-card">
            <p className="sell-certified__listing-title">{listingTitle}</p>
            <ul className="sell-certified__listing-specs">
              <li>{summary.cpu}</li>
              <li>
                {ramLabel} RAM · {storageLabel}
              </li>
              <li>
                Condition: {summary.condition} · Score: {summary.overallScore}/100
              </li>
              {summary.batteryHealthPercent != null && (
                <li>Battery health: {batteryLabel}</li>
              )}
              <li className="sell-certified__listing-link">Verify: {summary.verificationUrl}</li>
            </ul>
          </div>
          <button
            type="button"
            className="sell-certified__btn sell-certified__btn--secondary sell-certified__btn--inline"
            onClick={() =>
              void copyText(listingPreview, "Listing copied to clipboard.", "ListingCopied")
            }
          >
            Copy Listing
          </button>
        </div>

        <div className="sell-certified__primary-actions">
          <button
            type="button"
            className="sell-certified__btn sell-certified__btn--primary"
            onClick={() =>
              void copyText(listingPreview, "Marketplace listing copied.", "ListingCopied")
            }
          >
            Copy Marketplace Listing
          </button>
          <button
            type="button"
            className="sell-certified__btn sell-certified__btn--primary"
            onClick={() => void copyText(summary.verificationUrl, "Verification link copied.")}
          >
            Copy Verification Link
          </button>
          <button
            type="button"
            className="sell-certified__btn sell-certified__btn--primary"
            onClick={() => void handleDownloadPdf()}
            disabled={downloading}
          >
            {downloading ? "Generating PDF…" : "Download PDF Certificate"}
          </button>
        </div>

        <div className="sell-certified__more">
          <button
            type="button"
            className="sell-certified__more-toggle"
            aria-expanded={moreOpen}
            onClick={() => setMoreOpen((open) => !open)}
          >
            More Sharing Options
            <span className={`sell-certified__chevron ${moreOpen ? "sell-certified__chevron--open" : ""}`}>
              ▼
            </span>
          </button>
          {moreOpen && (
            <div className="sell-certified__secondary-actions">
              <button
                type="button"
                className="sell-certified__btn sell-certified__btn--ghost"
                onClick={() =>
                  void copyText(
                    facebookMarketplaceTemplate(summary),
                    "Facebook listing copied to clipboard.",
                    "FacebookTemplateCopied",
                  )
                }
              >
                Facebook Marketplace
              </button>
              <button
                type="button"
                className="sell-certified__btn sell-certified__btn--ghost"
                onClick={() =>
                  void copyText(
                    craigslistTemplate(summary),
                    "Craigslist listing copied to clipboard.",
                    "CraigslistTemplateCopied",
                  )
                }
              >
                Craigslist
              </button>
              <button
                type="button"
                className="sell-certified__btn sell-certified__btn--ghost"
                onClick={() => {
                  trackEvent("WhatsappShared", { certificateId: summary.certificateId });
                  window.open(whatsappShareUrl(summary), "_blank", "noopener,noreferrer");
                }}
              >
                WhatsApp
              </button>
              <button
                type="button"
                className="sell-certified__btn sell-certified__btn--ghost"
                onClick={() =>
                  void copyText(olxTemplate(summary), "OLX listing copied to clipboard.", "OlxTemplateCopied")
                }
              >
                OLX
              </button>
            </div>
          )}
        </div>
      </article>
    </section>
  );
}

function SpecItem({
  label,
  value,
  mono,
  copyable,
  highlight,
}: {
  label: string;
  value: string;
  mono?: boolean;
  copyable?: boolean;
  highlight?: boolean;
}) {
  return (
    <>
      <dt className="sell-certified__spec-label">{label}</dt>
      <dd
        className={[
          "sell-certified__spec-value",
          mono ? "sell-certified__spec-value--mono" : "",
          highlight ? "sell-certified__spec-value--highlight" : "",
        ]
          .filter(Boolean)
          .join(" ")}
      >
        {copyable ? (
          <span className="sell-certified__spec-copy">
            <span>{value}</span>
            <CopyButton text={value} label={`Copy ${label.toLowerCase()}`} />
          </span>
        ) : (
          value
        )}
      </dd>
    </>
  );
}
