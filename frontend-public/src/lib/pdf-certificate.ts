import QRCode from "qrcode";

import type { CertificationSummary } from "@/lib/certification-summary";

export async function downloadPdfCertificate(summary: CertificationSummary): Promise<void> {
  const [{ jsPDF }] = await Promise.all([import("jspdf")]);
  const qrDataUrl = await QRCode.toDataURL(summary.verificationUrl, {
    margin: 1,
    width: 160,
  });

  const doc = new jsPDF({ unit: "pt", format: "letter" });
  const pageWidth = doc.internal.pageSize.getWidth();
  const margin = 48;
  let y = margin;

  doc.setFillColor(79, 70, 229);
  doc.roundedRect(margin, y, 40, 40, 8, 8, "F");
  doc.setTextColor(255, 255, 255);
  doc.setFontSize(14);
  doc.text("CX", margin + 12, y + 26);

  doc.setTextColor(17, 24, 39);
  doc.setFontSize(22);
  doc.text("Certronx", margin + 52, y + 18);
  doc.setFontSize(11);
  doc.setTextColor(107, 114, 128);
  doc.text("Certify. Verify. Rehome.", margin + 52, y + 34);

  y += 72;
  doc.setDrawColor(229, 231, 235);
  doc.line(margin, y, pageWidth - margin, y);
  y += 28;

  doc.setFontSize(16);
  doc.setTextColor(17, 24, 39);
  doc.text("Device Certification", margin, y);
  y += 24;

  const rows: Array<[string, string]> = [
    ["Manufacturer", summary.manufacturer],
    ["Model", summary.model],
    ["CPU", summary.cpu],
    ["RAM", summary.ramGb > 0 ? `${summary.ramGb} GB` : "See report"],
    ["Storage", summary.storageGb > 0 ? `${summary.storageGb} GB ${summary.storageType}` : "See report"],
    [
      "Battery Health",
      summary.batteryHealthPercent != null ? `${Math.round(summary.batteryHealthPercent)}%` : "Not reported",
    ],
    ["Condition", summary.condition],
    ["Certronx Score", `${summary.overallScore}/100`],
    ["Certification Date", new Date(summary.certificationDate).toLocaleDateString()],
    ["Certificate ID", summary.certificateId],
    ["Verification URL", summary.verificationUrl],
  ];

  doc.setFontSize(11);
  for (const [label, value] of rows) {
    doc.setTextColor(107, 114, 128);
    doc.text(label, margin, y);
    doc.setTextColor(17, 24, 39);
    const lines = doc.splitTextToSize(value, pageWidth - margin * 2 - 140);
    doc.text(lines, margin + 140, y);
    y += Math.max(18, lines.length * 14);
  }

  y += 12;
  doc.addImage(qrDataUrl, "PNG", pageWidth - margin - 120, y - 20, 120, 120);
  doc.setFontSize(10);
  doc.setTextColor(107, 114, 128);
  doc.text("Scan to Verify", pageWidth - margin - 120, y + 112);

  y += 140;
  doc.line(margin, y, pageWidth - margin, y);
  y += 20;
  doc.setFontSize(9);
  doc.text(
    "This certification reflects the device state at the time of inspection.",
    margin,
    y,
  );
  y += 14;
  doc.text("Always verify using the Certronx verification portal.", margin, y);

  doc.save(`Certronx-${summary.certificateId}.pdf`);
}
