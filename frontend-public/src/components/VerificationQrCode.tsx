"use client";

import { QRCodeSVG } from "qrcode.react";
import { useEffect } from "react";

import { trackEvent } from "@/lib/analytics";

export function VerificationQrCode({
  url,
  size = 132,
  label = "Scan to Verify",
}: {
  url: string;
  size?: number;
  label?: string;
}) {
  useEffect(() => {
    trackEvent("QrVerificationViewed", { url });
  }, [url]);

  return (
    <div className="verification-qr">
      <div className="qr-frame">
        <QRCodeSVG value={url} size={size} />
      </div>
      <p className="verification-qr__label">{label}</p>
    </div>
  );
}
