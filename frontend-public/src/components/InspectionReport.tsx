"use client";

import type { InspectionReport as InspectionReportPayload } from "@/lib/api";

/* ── Types ─────────────────────────────────────────────────────────── */

export interface PlainWarning {
  title: string;
  explanation: string;
}

export interface ReportSummary {
  certification_grade?: string;
  grade_subtitle?: string;
  device_name?: string;
  specs_line?: string;
  display_resolution?: string;
  battery?: string;
  storage?: string;
  performance?: string;
  memory?: string;
  thermals?: string;
  screen?: string;
  functional?: Record<string, string>;
  security?: {
    headline?: string;
    secure_boot?: string;
    encryption?: string;
    tpm?: string;
  };
  resale_readiness?: string;
  warnings?: PlainWarning[];
}

export interface AdvancedField {
  label: string;
  value: string;
}

export interface ReportAdvanced {
  battery?: { fields?: AdvancedField[]; capacity_history?: unknown[]; certification_notes?: string[] };
  storage?: Array<{
    headline?: string;
    fields?: AdvancedField[];
    collection?: Record<string, unknown>;
    disclosure?: string;
  }>;
  security?: { fields?: AdvancedField[]; [key: string]: unknown };
  performance?: { benchmark?: AdvancedField[]; thermals?: AdvancedField[]; memory?: AdvancedField[] };
  functional?: Record<string, unknown>;
  collection_metadata?: Record<string, unknown>;
  evidence?: Array<{ artifact_type?: string; label?: string; signed_url?: string }>;
  narrative_details?: Record<string, string | undefined>;
}

export interface NormalizedReport {
  version?: string;
  summary: ReportSummary;
  advanced: ReportAdvanced;
}

/* ── Legacy → two-layer ─────────────────────────────────────────────── */

function normalizeReport(raw: InspectionReportPayload): NormalizedReport {
  if (raw.summary && typeof raw.summary === "object") {
    return {
      version: raw.version,
      summary: raw.summary as ReportSummary,
      advanced: (raw.advanced as ReportAdvanced) ?? {},
    };
  }

  const overview = (raw.device_overview ?? {}) as Record<string, unknown>;
  const health = (raw.health_summary ?? {}) as Record<string, unknown>;
  const fn = (raw.functional_tests ?? {}) as Record<string, string>;

  return {
    version: raw.version,
    summary: {
      certification_grade: raw.certification_grade,
      grade_subtitle: "Inspection report",
      device_name: String(overview.model ?? "This device"),
      specs_line: [
        overview.cpu,
        overview.ram_gb != null ? `${overview.ram_gb} GB RAM` : null,
        overview.storage_gb != null ? `${overview.storage_gb} GB storage` : null,
      ]
        .filter(Boolean)
        .join(" · "),
      display_resolution: overview.display as string | undefined,
      battery: simplifyLabel(String(health.battery ?? "Not available")),
      storage: simplifyLabel(String(health.storage ?? "Not available")),
      performance: simplifyLabel(String(health.performance ?? "Not available")),
      memory: simplifyLabel(String(health.memory ?? "")),
      thermals: simplifyLabel(String(health.thermals ?? "")),
      functional: {
        camera: simplifyLabel(fn.camera),
        microphone: simplifyLabel(fn.microphone),
        speaker: simplifyLabel(fn.speaker),
        keyboard: simplifyLabel(fn.keyboard),
        touchpad: simplifyLabel(fn.touchpad),
        usb_port: simplifyLabel(fn.usb),
      },
      security: {
        headline: "Security features",
        secure_boot: simplifyLabel(String((raw.security as Record<string, unknown>)?.secure_boot ?? "Not verified")),
        encryption: simplifyLabel(String((raw.security as Record<string, unknown>)?.encryption ?? "Not verified")),
        tpm: simplifyLabel(String((raw.security as Record<string, unknown>)?.tpm ?? "Not verified")),
      },
      resale_readiness: raw.refurbisher_notes ?? raw.expected_service_life ?? "Review inspection details",
      warnings: (raw.warnings ?? []).map((w) => ({
        title: "Important",
        explanation: simplifyLabel(String(w)),
      })),
    },
    advanced: {
      evidence: raw.evidence,
      narrative_details: raw.sections,
    },
  };
}

function simplifyLabel(s: string | undefined): string {
  if (!s) return "Not tested";
  return s
    .replace(/Verified with live preview/i, "Verified")
    .replace(/Verified with local recording and playback/i, "Verified")
    .replace(/Verified with stereo audio test/i, "Verified")
    .replace(/Verified with insertion\/removal test/i, "Verified")
    .replace(/Detected but Not Tested/i, "Not tested")
    .replace(/\(Medium confidence\)/gi, "")
    .replace(/\(Low confidence\)/gi, "")
    .replace(/— Storage health was assessed using Windows Storage APIs[^.]*\./gi, "")
    .trim();
}

const FUNCTIONAL_LABELS: Record<string, string> = {
  camera: "Camera",
  microphone: "Microphone",
  speaker: "Speakers",
  keyboard: "Keyboard",
  touchpad: "Touchpad",
  usb_port: "USB port",
  screen: "Screen",
};

const SECURITY_LABELS: Record<string, string> = {
  secure_boot: "Secure Boot",
  encryption: "Disk encryption",
  tpm: "TPM chip",
};

/* ── Main component ─────────────────────────────────────────────────── */

export function InspectionReport({ report }: { report: InspectionReportPayload }) {
  const { summary, advanced } = normalizeReport(report);
  const grade = summary.certification_grade ?? "—";
  const warnings = summary.warnings ?? [];

  return (
    <article className="card mt-8">
      <div className="card-body space-y-6">
        {/* Layer 1 — buyer summary */}
        <header className="border-b border-border pb-6">
          <p className="text-sm font-medium text-secondary uppercase tracking-wide">
            Device inspection report
          </p>
          <div className="mt-3 flex flex-wrap items-end gap-4">
            <div>
              <p className="text-4xl font-bold tracking-tight text-trust">{grade}</p>
              <p className="text-sm text-secondary mt-1">Overall certification grade</p>
            </div>
            <div className="flex-1 min-w-[200px]">
              <h2 className="text-xl font-semibold">{summary.device_name}</h2>
              <p className="text-secondary text-sm mt-1">{summary.grade_subtitle}</p>
              {summary.specs_line && (
                <p className="text-sm mt-2 text-primary">{summary.specs_line}</p>
              )}
            </div>
          </div>
        </header>

        {warnings.length > 0 && (
          <section className="rounded-xl border border-amber-200 bg-amber-50/80 dark:bg-amber-950/20 dark:border-amber-900/50 p-4 space-y-3">
            <h3 className="text-sm font-semibold text-amber-900 dark:text-amber-100">
              Important notices
            </h3>
            {warnings.map((w, i) => (
              <div key={i}>
                <p className="text-sm font-medium text-amber-950 dark:text-amber-50">{w.title}</p>
                <p className="text-sm text-amber-800 dark:text-amber-200/90 mt-0.5">
                  {w.explanation}
                </p>
              </div>
            ))}
          </section>
        )}

        <section>
          <h3 className="text-sm font-semibold mb-3 text-secondary uppercase tracking-wide">
            Condition at a glance
          </h3>
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
            <SummaryCard label="Battery" value={summary.battery} />
            <SummaryCard label="Storage" value={summary.storage} />
            <SummaryCard label="Performance" value={summary.performance} />
            <SummaryCard label="Screen" value={summary.screen} />
            <SummaryCard label="Memory" value={summary.memory} />
            <SummaryCard label="Cooling" value={summary.thermals} />
          </div>
        </section>

        <section>
          <h3 className="text-sm font-semibold mb-3 text-secondary uppercase tracking-wide">
            Functional checks
          </h3>
          <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
            {summary.functional &&
              Object.entries(summary.functional).map(([key, value]) => (
                <SummaryCard
                  key={key}
                  label={FUNCTIONAL_LABELS[key] ?? key}
                  value={value}
                  compact
                />
              ))}
          </div>
        </section>

        <section className="rounded-xl bg-surface-muted p-4">
          <h3 className="text-sm font-semibold mb-2">Security</h3>
          <p className="text-sm font-medium mb-3">{summary.security?.headline}</p>
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
            <SummaryCard label="Secure Boot" value={summary.security?.secure_boot} compact />
            <SummaryCard label="Encryption" value={summary.security?.encryption} compact />
            <SummaryCard label="TPM" value={summary.security?.tpm} compact />
          </div>
        </section>

        {summary.resale_readiness && (
          <section className="rounded-xl border border-trust/30 bg-trust/5 p-4">
            <h3 className="text-sm font-semibold text-trust mb-1">Resale readiness</h3>
            <p className="text-sm">{summary.resale_readiness}</p>
          </section>
        )}

        {/* Layer 2 — collapsed technical details */}
        <section className="pt-4 border-t border-border space-y-2">
          <p className="text-xs text-muted mb-2">
            Technical proof and raw diagnostics are available below — collapsed by default.
          </p>

          <Collapsible title="Show technical details" defaultOpen={false}>
            <div className="space-y-2 pt-2">
              <Collapsible title="Advanced battery data" nested>
                <FieldList fields={advanced.battery?.fields} />
                {advanced.battery?.certification_notes && advanced.battery.certification_notes.length > 0 && (
                  <NoteList notes={advanced.battery.certification_notes} />
                )}
              </Collapsible>

              <Collapsible title="Advanced storage data" nested>
                {advanced.storage?.length ? (
                  advanced.storage.map((drive, i) => (
                    <div key={i} className="mb-4 last:mb-0">
                      <p className="text-sm font-medium mb-2">{drive.headline}</p>
                      <FieldList fields={drive.fields} />
                      {drive.disclosure && (
                        <p className="text-xs text-muted mt-2">{drive.disclosure}</p>
                      )}
                      {drive.collection && (
                        <CollectionMeta meta={drive.collection} />
                      )}
                    </div>
                  ))
                ) : (
                  <p className="text-sm text-muted">No detailed storage data on file.</p>
                )}
              </Collapsible>

              <Collapsible title="Security details" nested>
                <FieldList fields={advanced.security?.fields} />
                <TriStateBlock title="TPM detection" data={advanced.security?.tpm as Record<string, unknown>} />
                <TriStateBlock title="Secure Boot" data={advanced.security?.secure_boot as Record<string, unknown>} />
                <TriStateBlock title="Encryption" data={advanced.security?.device_encryption as Record<string, unknown>} />
              </Collapsible>

              <Collapsible title="Benchmark & performance details" nested>
                <p className="text-xs font-medium text-secondary mb-1">Benchmark scores</p>
                <FieldList fields={advanced.performance?.benchmark} />
                <p className="text-xs font-medium text-secondary mb-1 mt-3">Thermals</p>
                <FieldList fields={advanced.performance?.thermals} />
                <p className="text-xs font-medium text-secondary mb-1 mt-3">Memory</p>
                <FieldList fields={advanced.performance?.memory} />
              </Collapsible>

              <Collapsible title="Functional test evidence" nested>
                <FunctionalEvidence data={advanced.functional} />
              </Collapsible>

              <Collapsible title="Collection metadata" nested>
                <JsonBlock data={advanced.collection_metadata} />
                {advanced.narrative_details && (
                  <div className="mt-3 space-y-2">
                    {Object.entries(advanced.narrative_details).map(([k, v]) =>
                      v ? (
                        <div key={k}>
                          <p className="text-xs font-medium text-secondary capitalize">
                            {k.replace(/_/g, " ")}
                          </p>
                          <p className="text-xs text-muted mt-0.5">{v}</p>
                        </div>
                      ) : null
                    )}
                  </div>
                )}
              </Collapsible>

              <Collapsible title="Raw evidence files" nested>
                {advanced.evidence && advanced.evidence.length > 0 ? (
                  <ul className="space-y-2">
                    {advanced.evidence.map((e) => (
                      <li key={e.artifact_type ?? e.label}>
                        <p className="text-sm font-medium">{e.label ?? "Evidence"}</p>
                        {e.signed_url ? (
                          <a
                            href={e.signed_url}
                            target="_blank"
                            rel="noopener noreferrer"
                            className="text-sm text-trust hover:underline"
                          >
                            Open evidence file
                          </a>
                        ) : (
                          <p className="text-xs text-muted">Link not available</p>
                        )}
                      </li>
                    ))}
                  </ul>
                ) : (
                  <p className="text-sm text-muted">No evidence files attached to this certificate.</p>
                )}
              </Collapsible>
            </div>
          </Collapsible>
        </section>
      </div>
    </article>
  );
}

/* ── Primitives ─────────────────────────────────────────────────────── */

function SummaryCard({
  label,
  value,
  compact,
}: {
  label: string;
  value?: string | null;
  compact?: boolean;
}) {
  const display = value?.trim() || "Not available";
  const tone = statusTone(display);

  return (
    <div
      className={`rounded-lg bg-surface-muted ${compact ? "p-2.5" : "p-3"} border border-transparent`}
    >
      <p className="text-xs text-secondary">{label}</p>
      <p className={`${compact ? "text-sm" : "text-sm"} font-medium mt-0.5 ${tone}`}>{display}</p>
    </div>
  );
}

function statusTone(value: string): string {
  const v = value.toLowerCase();
  if (v.includes("verified") || v.includes("healthy") || v.includes("enabled") || v.includes("excellent") || v.includes("good"))
    return "text-emerald-700 dark:text-emerald-400";
  if (v.includes("failed") || v.includes("poor") || v.includes("disabled") || v.includes("replacement"))
    return "text-red-700 dark:text-red-400";
  if (v.includes("not tested") || v.includes("not available") || v.includes("unclear"))
    return "text-muted";
  return "";
}

function Collapsible({
  title,
  children,
  defaultOpen = false,
  nested,
}: {
  title: string;
  children: React.ReactNode;
  defaultOpen?: boolean;
  nested?: boolean;
}) {
  return (
    <details
      className={`group rounded-lg border border-border ${nested ? "ml-0" : ""} open:bg-surface-muted/50`}
      {...(defaultOpen ? { open: true } : {})}
    >
      <summary className="cursor-pointer list-none px-4 py-3 text-sm font-medium hover:bg-surface-muted/80 rounded-lg [&::-webkit-details-marker]:hidden flex items-center justify-between gap-2">
        <span>{title}</span>
        <span className="text-muted text-xs group-open:rotate-180 transition-transform">▼</span>
      </summary>
      <div className="px-4 pb-4 text-sm">{children}</div>
    </details>
  );
}

function FieldList({ fields }: { fields?: AdvancedField[] }) {
  if (!fields?.length) {
    return <p className="text-sm text-muted">No additional data recorded.</p>;
  }
  return (
    <dl className="grid grid-cols-1 sm:grid-cols-2 gap-x-4 gap-y-2">
      {fields.map((f) => (
        <div key={f.label}>
          <dt className="text-xs text-secondary">{f.label}</dt>
          <dd className="text-sm font-medium">{f.value}</dd>
        </div>
      ))}
    </dl>
  );
}

function NoteList({ notes }: { notes: string[] }) {
  return (
    <ul className="mt-3 list-disc pl-4 text-xs text-muted space-y-1">
      {notes.map((n, i) => (
        <li key={i}>{n}</li>
      ))}
    </ul>
  );
}

function CollectionMeta({ meta }: { meta: Record<string, unknown> }) {
  return (
    <dl className="mt-2 grid grid-cols-2 gap-1 text-xs">
      {Object.entries(meta).map(([k, v]) => (
        <div key={k}>
          <dt className="text-secondary inline">{humanize(k)}: </dt>
          <dd className="inline text-muted">{String(v)}</dd>
        </div>
      ))}
    </dl>
  );
}

function TriStateBlock({ title, data }: { title: string; data?: Record<string, unknown> }) {
  if (!data) return null;
  return (
    <div className="mt-3 text-xs">
      <p className="font-medium text-secondary mb-1">{title}</p>
      <p>Status: {String(data.status ?? "—")}</p>
      {data.data_source != null && <p>Source: {String(data.data_source)}</p>}
      {data.collection_method != null && <p>Method: {String(data.collection_method)}</p>}
    </div>
  );
}

function FunctionalEvidence({ data }: { data?: Record<string, unknown> }) {
  if (!data || Object.keys(data).length === 0) {
    return <p className="text-sm text-muted">No functional test records.</p>;
  }
  const tests = [
    ["camera_test", "Camera test"],
    ["microphone_test", "Microphone test"],
    ["speaker_test", "Speaker test"],
    ["usb_test", "USB test"],
    ["display_output_test", "External display"],
    ["audio_jack_test", "Headset jack"],
  ] as const;
  return (
    <div className="space-y-4">
      {tests.map(([key, title]) => {
        const t = data[key];
        if (!t || typeof t !== "object") return null;
        const rec = t as Record<string, unknown>;
        return (
          <div key={key}>
            <p className="text-sm font-medium mb-1">{title}</p>
            <dl className="grid grid-cols-2 gap-1 text-xs">
              {rec.result != null && (
                <>
                  <dt className="text-secondary">Result</dt>
                  <dd>{String(rec.result)}</dd>
                </>
              )}
              {rec.tested != null && (
                <>
                  <dt className="text-secondary">Tested</dt>
                  <dd>{rec.tested ? "Yes" : "No"}</dd>
                </>
              )}
              {rec.reason != null && (
                <>
                  <dt className="text-secondary">Note</dt>
                  <dd>{String(rec.reason)}</dd>
                </>
              )}
            </dl>
          </div>
        );
      })}
    </div>
  );
}

function JsonBlock({ data }: { data?: unknown }) {
  if (!data || (typeof data === "object" && Object.keys(data as object).length === 0)) {
    return <p className="text-sm text-muted">No data.</p>;
  }
  return (
    <pre className="text-xs bg-surface-muted rounded-lg p-3 overflow-x-auto max-h-64 text-muted">
      {JSON.stringify(data, null, 2)}
    </pre>
  );
}

function humanize(key: string) {
  return key.replace(/_/g, " ").replace(/\b\w/g, (c) => c.toUpperCase());
}
