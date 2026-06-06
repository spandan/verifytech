export type AnalyticsEventName =
  | "CertificateGenerated"
  | "PdfDownloaded"
  | "ListingCopied"
  | "FacebookTemplateCopied"
  | "CraigslistTemplateCopied"
  | "OlxTemplateCopied"
  | "WhatsappShared"
  | "VerificationViewed"
  | "QrVerificationViewed";

export interface AnalyticsEvent {
  name: AnalyticsEventName;
  timestamp: string;
  properties?: Record<string, string | number | boolean | null | undefined>;
}

const STORAGE_KEY = "certronx_analytics_events";

export function trackEvent(
  name: AnalyticsEventName,
  properties?: Record<string, string | number | boolean | null | undefined>,
): void {
  if (typeof window === "undefined") return;

  const event: AnalyticsEvent = {
    name,
    timestamp: new Date().toISOString(),
    properties,
  };

  try {
    const existing = window.localStorage.getItem(STORAGE_KEY);
    const events: AnalyticsEvent[] = existing ? JSON.parse(existing) : [];
    events.push(event);
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(events.slice(-500)));
  } catch {
    // Ignore storage failures in private browsing.
  }

  if (process.env.NODE_ENV !== "production") {
    console.info("[Certronx analytics]", event);
  }
}
