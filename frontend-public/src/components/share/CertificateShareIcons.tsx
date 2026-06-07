"use client";

import { useState, type ReactNode } from "react";

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

import {
  IconCopyListing,
  IconCraigslist,
  IconDownload,
  IconFacebook,
  IconLink,
  IconOlx,
  IconWhatsApp,
} from "./share-icons";

type ShareAction = {
  id: string;
  label: string;
  onClick: () => void | Promise<void>;
  icon: ReactNode;
  tone?: "facebook" | "whatsapp" | "default";
  disabled?: boolean;
};

export function CertificateShareIcons({
  summary,
  variant = "default",
}: {
  summary: CertificationSummary;
  variant?: "default" | "compact";
}) {
  const { showToast } = useToast();
  const [downloading, setDownloading] = useState(false);

  async function copyText(text: string, toast: string, eventName: Parameters<typeof trackEvent>[0]) {
    await navigator.clipboard.writeText(text);
    trackEvent(eventName, { certificateId: summary.certificateId });
    showToast(toast);
  }

  const actions: ShareAction[] = [
    {
      id: "listing",
      label: "Copy listing",
      icon: <IconCopyListing className="share-icon-btn__svg" />,
      onClick: () =>
        copyText(genericListingTemplate(summary), "Listing copied to clipboard.", "ListingCopied"),
    },
    {
      id: "facebook",
      label: "Facebook Marketplace",
      icon: <IconFacebook className="share-icon-btn__svg" />,
      tone: "facebook",
      onClick: () =>
        copyText(
          facebookMarketplaceTemplate(summary),
          "Facebook listing copied to clipboard.",
          "FacebookTemplateCopied",
        ),
    },
    {
      id: "craigslist",
      label: "Craigslist",
      icon: <IconCraigslist className="share-icon-btn__svg" />,
      onClick: () =>
        copyText(
          craigslistTemplate(summary),
          "Craigslist listing copied to clipboard.",
          "CraigslistTemplateCopied",
        ),
    },
    {
      id: "olx",
      label: "OLX",
      icon: <IconOlx className="share-icon-btn__svg" />,
      onClick: () => copyText(olxTemplate(summary), "OLX listing copied to clipboard.", "OlxTemplateCopied"),
    },
    {
      id: "whatsapp",
      label: "WhatsApp",
      icon: <IconWhatsApp className="share-icon-btn__svg" />,
      tone: "whatsapp",
      onClick: () => {
        trackEvent("WhatsappShared", { certificateId: summary.certificateId });
        window.open(whatsappShareUrl(summary), "_blank", "noopener,noreferrer");
      },
    },
    {
      id: "link",
      label: "Copy verification link",
      icon: <IconLink className="share-icon-btn__svg" />,
      onClick: async () => {
        await navigator.clipboard.writeText(summary.verificationUrl);
        showToast("Verification link copied.");
      },
    },
    {
      id: "pdf",
      label: "Download PDF certificate",
      icon: <IconDownload className="share-icon-btn__svg" />,
      disabled: downloading,
      onClick: async () => {
        setDownloading(true);
        try {
          await downloadPdfCertificate(summary);
          trackEvent("PdfDownloaded", { certificateId: summary.certificateId });
          showToast("PDF certificate downloaded.");
        } finally {
          setDownloading(false);
        }
      },
    },
  ];

  const rootClass =
    variant === "compact" ? "share-icon-toolbar share-icon-toolbar--compact" : "share-icon-toolbar";

  return (
    <div className={rootClass} role="toolbar" aria-label="Share certificate">
      {actions.map((action) => (
        <ShareIconButton key={action.id} action={action} showLabel={variant === "default"} />
      ))}
    </div>
  );
}

function ShareIconButton({ action, showLabel }: { action: ShareAction; showLabel: boolean }) {
  const toneClass =
    action.tone === "facebook"
      ? "share-icon-btn--facebook"
      : action.tone === "whatsapp"
        ? "share-icon-btn--whatsapp"
        : "";

  return (
    <button
      type="button"
      className={`share-icon-btn ${toneClass}`}
      onClick={() => void action.onClick()}
      disabled={action.disabled}
      aria-label={action.label}
      title={action.label}
    >
      {action.icon}
      {showLabel && <span className="share-icon-btn__label">{action.label}</span>}
    </button>
  );
}
