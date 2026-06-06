import type { CertificationSummary } from "@/lib/certification-summary";

function batteryLine(summary: CertificationSummary): string {
  if (summary.batteryHealthPercent == null) return "";
  return `Battery Health: ${Math.round(summary.batteryHealthPercent)}%`;
}

export function genericListingTemplate(summary: CertificationSummary): string {
  return [
    "💻 Certronx Certified Laptop",
    "",
    `Device: ${summary.manufacturer} ${summary.model}`,
    "",
    `CPU: ${summary.cpu}`,
    `RAM: ${summary.ramGb}GB`,
    `Storage: ${summary.storageGb}GB ${summary.storageType}`,
    "",
    `Condition: ${summary.condition}`,
    `Certronx Score: ${summary.overallScore}/100`,
    "",
    batteryLine(summary),
    "",
    "Verify Device:",
    summary.verificationUrl,
    "",
    "Certificate ID:",
    summary.certificateId,
  ]
    .filter(Boolean)
    .join("\n");
}

export function facebookMarketplaceTemplate(summary: CertificationSummary): string {
  return [
    "💻 Certronx Certified Device",
    "",
    `${summary.manufacturer} ${summary.model}`,
    "",
    `CPU: ${summary.cpu}`,
    `RAM: ${summary.ramGb}GB`,
    `Storage: ${summary.storageGb}GB ${summary.storageType}`,
    "",
    `Condition: ${summary.condition}`,
    batteryLine(summary),
    "",
    "Independent verification available:",
    "",
    summary.verificationUrl,
    "",
    "Certificate ID:",
    summary.certificateId,
    "",
    "#UsedLaptop #CertifiedLaptop #Certronx",
  ]
    .filter(Boolean)
    .join("\n");
}

export function craigslistTemplate(summary: CertificationSummary): string {
  return [
    `${summary.manufacturer} ${summary.model}`,
    "",
    `CPU: ${summary.cpu}`,
    `RAM: ${summary.ramGb}GB`,
    `Storage: ${summary.storageGb}GB ${summary.storageType}`,
    "",
    `Condition: ${summary.condition}`,
    "",
    "Verify certification:",
    summary.verificationUrl,
    "",
    "Certificate ID:",
    summary.certificateId,
  ].join("\n");
}

export function olxTemplate(summary: CertificationSummary): string {
  return [
    "Certronx Certified Laptop",
    "",
    `${summary.manufacturer} ${summary.model}`,
    "",
    `CPU: ${summary.cpu}`,
    `RAM: ${summary.ramGb}GB`,
    `Storage: ${summary.storageGb}GB ${summary.storageType}`,
    "",
    `Condition: ${summary.condition}`,
    batteryLine(summary),
    "",
    "Verify:",
    summary.verificationUrl,
  ]
    .filter(Boolean)
    .join("\n");
}

export function whatsappMessage(summary: CertificationSummary): string {
  return [
    "Check out this Certronx Certified Device",
    "",
    `${summary.manufacturer} ${summary.model}`,
    "",
    `Condition: ${summary.condition}`,
    "",
    "Verify Here:",
    summary.verificationUrl,
    "",
    "Certificate:",
    summary.certificateId,
  ].join("\n");
}

export function whatsappShareUrl(summary: CertificationSummary): string {
  return `https://wa.me/?text=${encodeURIComponent(whatsappMessage(summary))}`;
}
