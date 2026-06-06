"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Collapsible } from "@/components/Collapsible";
import { api, type AgentInfo, type ScanPairingSession } from "@/lib/api";
import { trackEvent } from "@/lib/analytics";
import { useAuth } from "@/lib/auth-context";
import { env } from "@/lib/env";
import { getRecommendedPlatform } from "@/components/OSDetector";

const FLOW_STEPS = [
  { step: "1", title: "Start Windows Scan", desc: "Open Certronx Agent from your browser." },
  { step: "2", title: "Run Scan", desc: "Inspect device condition in minutes." },
  { step: "3", title: "Generate Report", desc: "Receive your trusted certification report." },
  { step: "4", title: "Save Report", desc: "Certificate appears in your account." },
];

export default function DownloadPage() {
  const router = useRouter();
  const { userId } = useAuth();
  const [agent, setAgent] = useState<AgentInfo | null>(null);
  const [platform, setPlatform] = useState("windows");
  const [error, setError] = useState("");
  const [pairing, setPairing] = useState<ScanPairingSession | null>(null);
  const [pairingBusy, setPairingBusy] = useState(false);
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    const p = getRecommendedPlatform();
    setPlatform(p);
    api
      .getAgent(p === "macos" || p === "android" ? p : "windows")
      .then(setAgent)
      .catch(() => {
        api.getAgent("windows").then(setAgent).catch((e: Error) => setError(e.message));
      });
  }, []);

  const startWindowsScan = useCallback(async () => {
    if (!userId) {
      router.push("/login?next=/download");
      return;
    }

    setPairingBusy(true);
    setError("");
    setCopied(false);

    try {
      const session = await api.createPairingSession();
      setPairing(session);
      trackEvent("PairingSessionCreated", { expires_at: session.expires_at });

      trackEvent("DeepLinkLaunchAttempted");
      window.location.href = session.deep_link;

      setTimeout(() => {
        if (document.hasFocus()) {
          // Browser still focused — deep link may not have opened the agent.
        }
      }, 1500);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Could not start Windows scan.");
    } finally {
      setPairingBusy(false);
    }
  }, [router, userId]);

  const copyPairingCode = useCallback(async () => {
    if (!pairing?.pairing_code) return;
    try {
      await navigator.clipboard.writeText(pairing.pairing_code);
      setCopied(true);
      trackEvent("PairingCodeCopied");
      setTimeout(() => setCopied(false), 2500);
    } catch {
      setError("Could not copy pairing code.");
    }
  }, [pairing?.pairing_code]);

  const isSupported = platform === "windows";

  return (
    <>
      <section className="section-subtle border-b border-[var(--color-border)]">
        <div className="page-container py-12">
          <div className="mx-auto max-w-2xl text-center">
            <p className="text-sm font-medium text-[var(--color-brand)]">Certify a device</p>
            <h1 className="page-title mt-2">Certify with Certronx Scanner</h1>
            <p className="page-subtitle mx-auto">
              Signed-in users can launch the Windows agent instantly. No typing required when deep linking works.
            </p>
          </div>
          <div className="flow-steps mx-auto mt-10 max-w-4xl">
            {FLOW_STEPS.map((item) => (
              <div key={item.step} className="step-card text-center">
                <div className="step-card__number mx-auto">{item.step}</div>
                <h3 className="step-card__title">{item.title}</h3>
                <p className="step-card__desc">{item.desc}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      <div className="page-container page-container--narrow">
        {!userId && (
          <div className="alert alert-info mb-6 text-sm">
            <Link href="/login?next=/download" className="text-[var(--color-brand)] hover:underline">
              Sign in
            </Link>{" "}
            to use seamless Windows agent pairing. You can still download the scanner without an account.
          </div>
        )}

        {!isSupported && (
          <div className="alert alert-warning mb-6">
            {platform === "macos" || platform === "android" ? (
              <>
                {platform === "macos" ? "macOS" : "Android"} scanner coming soon. Use Windows to certify today.
              </>
            ) : (
              <>Windows is required for certification at this time.</>
            )}
          </div>
        )}

        {error && (
          <div className="alert alert-error mb-6">
            {error}. Make sure the API is running at {env.apiUrl}.
          </div>
        )}

        {userId && isSupported && (
          <div className="card card-body mb-6 space-y-4">
            <div>
              <h2 className="font-semibold">Start Windows Scan</h2>
              <p className="mt-1 text-sm text-secondary">
                Opens Certronx Agent on this PC with a short-lived pairing code. The code expires in 2 minutes.
              </p>
            </div>
            <button
              type="button"
              className="btn btn-brand btn-block"
              disabled={pairingBusy}
              onClick={() => void startWindowsScan()}
            >
              {pairingBusy ? "Creating pairing session…" : "Start Windows Scan"}
            </button>

            {pairing && (
              <div className="rounded-xl border border-[var(--color-border)] bg-[var(--color-bg-subtle)] p-4 text-sm space-y-3">
                <p className="font-medium text-[var(--color-brand)]">
                  Waiting for Certronx Agent to open…
                </p>
                <p className="text-secondary">
                  If nothing happens, use the options below. Your pairing code expires at{" "}
                  {new Date(pairing.expires_at).toLocaleTimeString()}.
                </p>
                <p className="font-mono text-xs break-all">{pairing.pairing_code}</p>
                <div className="flex flex-wrap gap-2">
                  <a href={agent?.full_download_url ?? "#"} className="btn btn-secondary btn-sm">
                    Download Windows Agent
                  </a>
                  <button type="button" className="btn btn-secondary btn-sm" onClick={() => void copyPairingCode()}>
                    {copied ? "Copied!" : "Copy Pairing Code"}
                  </button>
                </div>
                <p className="text-secondary">
                  In the agent, choose <strong>Enter Pairing Code</strong> and paste the code if the browser did not
                  open the app automatically.
                </p>
              </div>
            )}
          </div>
        )}

        {agent && (
          <div className="card card-body space-y-6">
            <div className="flex items-start gap-4">
              <div className="feature-card__icon">CX</div>
              <div>
                <h2 className="font-semibold">Certronx Scanner — {agent.platform}</h2>
                <p className="text-sm text-secondary">Version {agent.version}</p>
              </div>
            </div>

            <CollapsibleChecksum checksum={agent.checksum} />

            <a href={agent.full_download_url} className="btn btn-secondary btn-block">
              Download Certronx Scanner
            </a>

            <ul className="check-list">
              <li>
                <span className="check-list__icon">✓</span>
                Seamless pairing when signed in
              </li>
              <li>
                <span className="check-list__icon">✓</span>
                Shareable report for buyers
              </li>
              <li>
                <span className="check-list__icon">✓</span>
                Public verification by code or QR
              </li>
            </ul>
          </div>
        )}

        <p className="mt-8 text-center text-sm text-muted">
          Already have a code?{" "}
          <Link href="/save" className="text-[var(--color-brand)] hover:underline">
            Save to your account
          </Link>
          {" · "}
          <Link href="/verify" className="text-[var(--color-brand)] hover:underline">
            Verify a device
          </Link>
        </p>
      </div>
    </>
  );
}

function CollapsibleChecksum({ checksum }: { checksum: string }) {
  return (
    <Collapsible title="File checksum">
      <p className="font-mono text-xs break-all">{checksum}</p>
    </Collapsible>
  );
}
