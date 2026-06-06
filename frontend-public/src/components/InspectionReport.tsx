"use client";

import type { InspectionReport as InspectionReportPayload } from "@/lib/api";
import { extractOverallScoreFromReport, scoreToCondition } from "@/lib/certification-summary";
import { parseCheckItem, type CheckItem, type CheckTone } from "@/lib/check-result";

import { ConditionBadge, ScoreDisplay } from "@/components/ConditionBadge";

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
  network?: string;
  windows?: string;
  ports?: string;
  functional?: Record<string, string>;
  security?: {
    headline?: string;
    secure_boot?: string;
    encryption?: string;
    tpm?: string;
  };
  resale_readiness?: string;
  warnings?: PlainWarning[] | string[];
  check_items?: CheckItem[];
  functional_checks?: CheckItem[];
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
  network?: AdvancedField[];
  windows?: AdvancedField[];
  ports?: {
    inventory?: AdvancedField[];
    features?: Record<string, unknown>;
    certification_status?: Record<string, string>;
    notes?: string[];
  };
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

function normalizeWarnings(raw: ReportSummary["warnings"]): PlainWarning[] {
  if (!raw?.length) return [];
  return raw.map((w) => {
    if (typeof w === "string") {
      return { title: "Important", explanation: simplifyLabel(w) };
    }
    return {
      title: w.title || "Important",
      explanation: simplifyLabel(w.explanation),
    };
  });
}

function normalizeReport(raw: InspectionReportPayload): NormalizedReport {
  if (raw.summary && typeof raw.summary === "object") {
    const summary = raw.summary as ReportSummary;
    return {
      version: raw.version,
      summary: {
        ...summary,
        warnings: normalizeWarnings(summary.warnings),
      },
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
      warnings: normalizeWarnings(
        (raw.warnings ?? []).map((w) => ({ title: "Important", explanation: String(w) })),
      ),
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

/* ── Main component ─────────────────────────────────────────────────── */

export function InspectionReport({
  report,
  certContext,
}: {
  report: InspectionReportPayload;
  certContext?: {
    battery_health_percent?: number | null;
    storage_health_percent?: number | null;
  };
}) {
  const { summary, advanced } = normalizeReport(report);
  const warnings = (summary.warnings ?? []) as PlainWarning[];
  const overallScore = extractOverallScoreFromReport(report, certContext);
  const condition = scoreToCondition(overallScore);
  const grade = summary.certification_grade;

  return (
    <article className="cert-report">
      <div className="cert-report__body">
        {/* Hero — grade and device summary */}
        <header className="cert-report__hero">
          <p className="cert-report__eyebrow">Device inspection report</p>
          <div className="cert-report__hero-grid">
            <div className="cert-report__grade-block">
              {grade && (
                <p className="cert-report__grade-letter" aria-label={`Grade ${grade}`}>
                  {grade}
                </p>
              )}
              <div className="cert-report__score-row">
                <ConditionBadge score={overallScore} label={condition} />
                <ScoreDisplay score={overallScore} />
              </div>
            </div>
            <div className="cert-report__device-block">
              <h2 className="cert-report__device-name">{summary.device_name}</h2>
              <p className="cert-report__subtitle">{summary.grade_subtitle}</p>
              {summary.specs_line && (
                <p className="cert-report__specs">{summary.specs_line}</p>
              )}
              {summary.display_resolution && (
                <p className="cert-report__resolution">Display: {summary.display_resolution}</p>
              )}
            </div>
          </div>
        </header>

        {warnings.length > 0 && (
          <section className="cert-report__warnings">
            <h3 className="cert-report__section-title cert-report__section-title--warn">
              Important notices
            </h3>
            <ul className="cert-report__warning-list">
              {warnings.map((w, i) => (
                <li key={i} className="cert-report__warning-item">
                  <p className="cert-report__warning-title">{w.title}</p>
                  <p className="cert-report__warning-text">{w.explanation}</p>
                </li>
              ))}
            </ul>
          </section>
        )}

        {/* Buyer-facing condition summary — always visible */}
        <section className="cert-report__section">
          <h3 className="cert-report__section-title">What we checked</h3>
          <div className="cert-report__check-list">
            {buildCheckItems(summary, "hardware").map((item) => (
              <CheckResult key={item.label} item={item} />
            ))}
          </div>
        </section>

        <section className="cert-report__section">
          <h3 className="cert-report__section-title">Interactive checks</h3>
          <div className="cert-report__check-list cert-report__check-list--compact">
            {buildCheckItems(summary, "functional").map((item) => (
              <CheckResult key={item.label} item={item} compact />
            ))}
          </div>
        </section>

        <section className="cert-report__section cert-report__security">
          <h3 className="cert-report__section-title">Security</h3>
          <p className="cert-report__security-headline">{summary.security?.headline}</p>
          <div className="cert-report__check-list cert-report__check-list--compact">
            <CheckResult item={parseCheckItem("Secure Boot", summary.security?.secure_boot)} compact />
            <CheckResult item={parseCheckItem("Disk encryption", summary.security?.encryption)} compact />
            <CheckResult item={parseCheckItem("TPM chip", summary.security?.tpm)} compact />
          </div>
        </section>

        {(summary.network || summary.ports || summary.windows) && (
          <section className="cert-report__section">
            <h3 className="cert-report__section-title">Connectivity &amp; setup</h3>
            <div className="cert-report__check-list cert-report__check-list--compact">
              {summary.network && <CheckResult item={parseCheckItem("Wireless", summary.network)} compact />}
              {summary.ports && <CheckResult item={parseCheckItem("Ports", summary.ports)} compact />}
              {summary.windows && <CheckResult item={parseCheckItem("Windows", summary.windows)} compact />}
            </div>
          </section>
        )}

        {summary.resale_readiness && (
          <section className="cert-report__readiness">
            <h3 className="cert-report__section-title">Resale readiness</h3>
            <p>{summary.resale_readiness}</p>
          </section>
        )}

        {/* Technical details — collapsed by default */}
        <section className="cert-report__technical">
          <Collapsible title="Technical details" defaultOpen={false}>
            <div className="cert-report__technical-inner">
              <Collapsible title="Battery diagnostics" nested>
                <FieldList fields={advanced.battery?.fields} />
                {advanced.battery?.certification_notes && advanced.battery.certification_notes.length > 0 && (
                  <NoteList notes={advanced.battery.certification_notes} />
                )}
              </Collapsible>

              <Collapsible title="Storage diagnostics" nested>
                {advanced.storage?.length ? (
                  advanced.storage.map((drive, i) => (
                    <div key={i} className="cert-report__drive-block">
                      <p className="cert-report__drive-headline">{drive.headline}</p>
                      <FieldList fields={drive.fields} />
                      {drive.disclosure && (
                        <p className="cert-report__disclosure">{drive.disclosure}</p>
                      )}
                      {drive.collection && <CollectionMeta meta={drive.collection} />}
                    </div>
                  ))
                ) : (
                  <p className="cert-report__empty">No detailed storage data on file.</p>
                )}
              </Collapsible>

              <Collapsible title="Security diagnostics" nested>
                <FieldList fields={advanced.security?.fields} />
                <TriStateBlock title="TPM detection" data={advanced.security?.tpm as Record<string, unknown>} />
                <TriStateBlock title="Secure Boot" data={advanced.security?.secure_boot as Record<string, unknown>} />
                <TriStateBlock title="Encryption" data={advanced.security?.device_encryption as Record<string, unknown>} />
              </Collapsible>

              <Collapsible title="Performance &amp; thermals" nested>
                <p className="cert-report__field-group-label">Benchmark scores</p>
                <FieldList fields={advanced.performance?.benchmark} />
                <p className="cert-report__field-group-label">Thermals</p>
                <FieldList fields={advanced.performance?.thermals} />
                <p className="cert-report__field-group-label">Memory</p>
                <FieldList fields={advanced.performance?.memory} />
              </Collapsible>

              <Collapsible title="Network &amp; ports" nested>
                <p className="cert-report__field-group-label">Network</p>
                <FieldList fields={advanced.network} />
                <p className="cert-report__field-group-label">Windows</p>
                <FieldList fields={advanced.windows} />
                {advanced.ports?.inventory && (
                  <>
                    <p className="cert-report__field-group-label">Port inventory</p>
                    <FieldList fields={advanced.ports.inventory} />
                  </>
                )}
                {advanced.ports?.certification_status &&
                  Object.keys(advanced.ports.certification_status).length > 0 && (
                    <>
                      <p className="cert-report__field-group-label">Port verification</p>
                      <dl className="cert-report__field-list">
                        {Object.entries(advanced.ports.certification_status).map(([k, v]) => (
                          <div key={k}>
                            <dt>{humanize(k)}</dt>
                            <dd>{v}</dd>
                          </div>
                        ))}
                      </dl>
                    </>
                  )}
              </Collapsible>

              <Collapsible title="Functional test records" nested>
                <FunctionalEvidence data={advanced.functional} />
              </Collapsible>

              <Collapsible title="Scan metadata" nested>
                <JsonBlock data={advanced.collection_metadata} />
                {advanced.narrative_details && (
                  <div className="cert-report__narrative">
                    {Object.entries(advanced.narrative_details).map(([k, v]) =>
                      v ? (
                        <div key={k}>
                          <p className="cert-report__field-group-label">{humanize(k)}</p>
                          <p className="cert-report__narrative-text">{v}</p>
                        </div>
                      ) : null
                    )}
                  </div>
                )}
              </Collapsible>

              <Collapsible title="Evidence files" nested>
                {advanced.evidence && advanced.evidence.length > 0 ? (
                  <ul className="cert-report__evidence-list">
                    {advanced.evidence.map((e) => (
                      <li key={e.artifact_type ?? e.label}>
                        <p className="cert-report__evidence-label">{e.label ?? "Evidence"}</p>
                        {e.signed_url ? (
                          <a
                            href={e.signed_url}
                            target="_blank"
                            rel="noopener noreferrer"
                            className="cert-report__evidence-link"
                          >
                            Open evidence file
                          </a>
                        ) : (
                          <p className="cert-report__empty">Link not available</p>
                        )}
                      </li>
                    ))}
                  </ul>
                ) : (
                  <p className="cert-report__empty">No evidence files attached to this certificate.</p>
                )}
              </Collapsible>
            </div>
          </Collapsible>
        </section>
      </div>
    </article>
  );
}

/* ── Check result rows ──────────────────────────────────────────────── */

function buildCheckItems(summary: ReportSummary, group: "hardware" | "functional"): CheckItem[] {
  if (group === "hardware") {
    if (summary.check_items?.length) return summary.check_items;
    return [
      parseCheckItem("Battery", summary.battery),
      parseCheckItem("Storage", summary.storage),
      parseCheckItem("Performance", summary.performance),
      parseCheckItem("Screen", summary.screen),
      parseCheckItem("Memory", summary.memory),
      parseCheckItem("Cooling", summary.thermals),
    ];
  }

  if (summary.functional_checks?.length) return summary.functional_checks;
  if (!summary.functional) return [];
  return Object.entries(summary.functional).map(([key, value]) =>
    parseCheckItem(FUNCTIONAL_LABELS[key] ?? key, value),
  );
}

function CheckResult({ item, compact }: { item: CheckItem; compact?: boolean }) {
  const tone = (item.tone as CheckTone) || "neutral";
  return (
    <div className={`check-result ${compact ? "check-result--compact" : ""}`}>
      <p className="check-result__label">{item.label}</p>
      <div className="check-result__body">
        <p className={`check-result__headline check-result__headline--${tone}`}>{item.headline}</p>
        {item.detail && <p className="check-result__detail">{item.detail}</p>}
      </div>
    </div>
  );
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
      className={`cert-collapsible ${nested ? "cert-collapsible--nested" : ""}`}
      {...(defaultOpen ? { open: true } : {})}
    >
      <summary className="cert-collapsible__summary">
        <span>{title}</span>
        <span className="cert-collapsible__chevron" aria-hidden="true">
          ▼
        </span>
      </summary>
      <div className="cert-collapsible__content">{children}</div>
    </details>
  );
}

function FieldList({ fields }: { fields?: AdvancedField[] }) {
  if (!fields?.length) {
    return <p className="cert-report__empty">No additional data recorded.</p>;
  }
  return (
    <dl className="cert-report__field-list">
      {fields.map((f) => (
        <div key={f.label}>
          <dt>{f.label}</dt>
          <dd>{f.value}</dd>
        </div>
      ))}
    </dl>
  );
}

function NoteList({ notes }: { notes: string[] }) {
  return (
    <ul className="cert-report__notes">
      {notes.map((n, i) => (
        <li key={i}>{n}</li>
      ))}
    </ul>
  );
}

function CollectionMeta({ meta }: { meta: Record<string, unknown> }) {
  return (
    <dl className="cert-report__collection-meta">
      {Object.entries(meta).map(([k, v]) => (
        <div key={k}>
          <dt>{humanize(k)}:</dt>
          <dd>{String(v)}</dd>
        </div>
      ))}
    </dl>
  );
}

function TriStateBlock({ title, data }: { title: string; data?: Record<string, unknown> }) {
  if (!data) return null;
  return (
    <div className="cert-report__tristate">
      <p className="cert-report__field-group-label">{title}</p>
      <p>Status: {String(data.status ?? "—")}</p>
      {data.data_source != null && <p>Source: {String(data.data_source)}</p>}
      {data.collection_method != null && <p>Method: {String(data.collection_method)}</p>}
    </div>
  );
}

function FunctionalEvidence({ data }: { data?: Record<string, unknown> }) {
  if (!data || Object.keys(data).length === 0) {
    return <p className="cert-report__empty">No functional test records.</p>;
  }
  const tests = [
    ["camera_test", "Camera test"],
    ["microphone_test", "Microphone test"],
    ["speaker_test", "Speaker test"],
    ["usb_test", "USB test"],
    ["audio_jack_test", "Headset jack"],
  ] as const;
  return (
    <div className="cert-report__functional-evidence">
      {tests.map(([key, title]) => {
        const t = data[key];
        if (!t || typeof t !== "object") return null;
        const rec = t as Record<string, unknown>;
        return (
          <div key={key}>
            <p className="cert-report__field-group-label">{title}</p>
            <dl className="cert-report__field-list cert-report__field-list--compact">
              {rec.result != null && (
                <>
                  <dt>Result</dt>
                  <dd>{String(rec.result)}</dd>
                </>
              )}
              {rec.tested != null && (
                <>
                  <dt>Tested</dt>
                  <dd>{rec.tested ? "Yes" : "No"}</dd>
                </>
              )}
              {rec.reason != null && (
                <>
                  <dt>Note</dt>
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
    return <p className="cert-report__empty">No data.</p>;
  }
  return (
    <pre className="cert-report__json">{JSON.stringify(data, null, 2)}</pre>
  );
}

function humanize(key: string) {
  return key.replace(/_/g, " ").replace(/\b\w/g, (c) => c.toUpperCase());
}
