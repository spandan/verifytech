export type CheckTone = "good" | "bad" | "warn" | "muted" | "neutral";

export interface CheckItem {
  label: string;
  headline: string;
  detail?: string | null;
  tone?: CheckTone | string;
}

function toneFromHeadline(headline: string): CheckTone {
  const h = headline.toLowerCase();
  if (h.includes("verified") || h.includes("enabled") || h.includes("healthy") || h.includes("excellent")) {
    return "good";
  }
  if (h.includes("failed") || h.includes("poor") || h.includes("disabled") || h.includes("replacement")) {
    return "bad";
  }
  if (h.includes("not tested") || h.includes("not verified") || h.includes("not checked") || h.includes("unclear")) {
    return "muted";
  }
  if (h.includes("fair") || h.includes("caution") || h.includes("wear")) {
    return "warn";
  }
  if (h.includes("good") || h.includes("normal") || h.includes("checked")) {
    return "good";
  }
  return "neutral";
}

export function parseCheckItem(label: string, value?: string | null): CheckItem {
  const raw = (value ?? "").trim();
  if (!raw || /not available|not tested|not measured|not checked/i.test(raw)) {
    return { label, headline: "Not checked", detail: null, tone: "muted" };
  }

  const parts = raw.split(/\s*[—–-]\s*/, 2);
  const headline = parts[0]?.trim() || raw;
  const detail = parts[1]?.trim() || null;
  return {
    label,
    headline,
    detail,
    tone: toneFromHeadline(headline),
  };
}

export function normalizeCheckItem(item: Partial<CheckItem> & { label: string }): CheckItem {
  if (item.headline) {
    return {
      label: item.label,
      headline: item.headline,
      detail: item.detail ?? null,
      tone: (item.tone as CheckTone) || toneFromHeadline(item.headline),
    };
  }
  return parseCheckItem(item.label, item.headline);
}
