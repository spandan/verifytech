"use client";

import { useState } from "react";

import { useToast } from "@/components/ToastProvider";
import { trackEvent } from "@/lib/analytics";
import type { CertificationSummary } from "@/lib/certification-summary";
import { downloadPdfCertificate } from "@/lib/pdf-certificate";
import {
  craigslistTemplate,
  facebookMarketplaceTemplate,
  genericListingTemplate,
  olxTemplate,
  whatsappShareUrl,
} from "@/lib/share-templates";

export function ShareCertificateSection({ summary }: { summary: CertificationSummary }) {
  const { showToast } = useToast();
  const [downloading, setDownloading] = useState(false);

  async function copyText(text: string, toast: string, eventName: Parameters<typeof trackEvent>[0]) {
    await navigator.clipboard.writeText(text);
    trackEvent(eventName, { certificateId: summary.certificateId });
    showToast(toast);
  }

  async function handleCopyListing() {
    await copyText(genericListingTemplate(summary), "Listing copied to clipboard.", "ListingCopied");
  }

  async function handleCopyFacebook() {
    await copyText(
      facebookMarketplaceTemplate(summary),
      "Facebook listing copied to clipboard.",
      "FacebookTemplateCopied",
    );
  }

  async function handleCopyCraigslist() {
    await copyText(
      craigslistTemplate(summary),
      "Craigslist listing copied to clipboard.",
      "CraigslistTemplateCopied",
    );
  }

  async function handleCopyOlx() {
    await copyText(olxTemplate(summary), "OLX listing copied to clipboard.", "OlxTemplateCopied");
  }

  async function handleCopyVerificationLink() {
    await navigator.clipboard.writeText(summary.verificationUrl);
    showToast("Verification link copied.");
  }

  function handleWhatsAppShare() {
    trackEvent("WhatsappShared", { certificateId: summary.certificateId });
    window.open(whatsappShareUrl(summary), "_blank", "noopener,noreferrer");
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
    <section className="card card-body mt-8 share-certificate">
      <div className="share-certificate__header">
        <h2 className="text-lg font-semibold">Share Certificate</h2>
        <p className="mt-1 text-sm text-secondary">
          Copy a marketplace listing, share verification, or download a PDF certificate for buyers.
        </p>
      </div>

      <div className="share-certificate__grid">
        <ShareButton onClick={handleCopyListing}>Copy Listing</ShareButton>
        <ShareButton onClick={handleCopyFacebook}>Facebook Marketplace</ShareButton>
        <ShareButton onClick={handleCopyCraigslist}>Craigslist</ShareButton>
        <ShareButton onClick={handleCopyOlx}>OLX</ShareButton>
        <ShareButton onClick={handleWhatsAppShare}>WhatsApp</ShareButton>
        <ShareButton onClick={handleCopyVerificationLink}>Copy Verification Link</ShareButton>
        <ShareButton onClick={handleDownloadPdf} disabled={downloading}>
          {downloading ? "Generating PDF…" : "Download PDF Certificate"}
        </ShareButton>
      </div>
    </section>
  );
}

function ShareButton({
  children,
  onClick,
  disabled,
}: {
  children: React.ReactNode;
  onClick: () => void;
  disabled?: boolean;
}) {
  return (
    <button type="button" className="btn btn-secondary text-sm" onClick={onClick} disabled={disabled}>
      {children}
    </button>
  );
}
