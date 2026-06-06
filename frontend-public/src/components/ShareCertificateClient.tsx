"use client";

import { useEffect } from "react";

import { trackEvent, type AnalyticsEventName } from "@/lib/analytics";

export function ShareCertificateClient({
  event,
  certificateId,
}: {
  event: AnalyticsEventName;
  certificateId: string;
}) {
  useEffect(() => {
    trackEvent(event, { certificateId });
  }, [event, certificateId]);

  return null;
}
