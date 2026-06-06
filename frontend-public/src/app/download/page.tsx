"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { Collapsible } from "@/components/Collapsible";
import { api, type AgentInfo } from "@/lib/api";
import { useAuth } from "@/lib/auth-context";
import { env } from "@/lib/env";
import { getRecommendedPlatform } from "@/components/OSDetector";

const FLOW_STEPS = [
  { step: "1", title: "Download Scanner", desc: "Get the Certronx Scanner for Windows." },
  { step: "2", title: "Run Scan", desc: "Inspect device condition in minutes." },
  { step: "3", title: "Generate Report", desc: "Receive your trusted certification report." },
  { step: "4", title: "Save Report", desc: "Share with buyers or save to your account." },
];

export default function DownloadPage() {
  const { userId } = useAuth();
  const [agent, setAgent] = useState<AgentInfo | null>(null);
  const [platform, setPlatform] = useState("windows");
  const [error, setError] = useState("");
  const [linkToken, setLinkToken] = useState("");

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

  useEffect(() => {
    if (!userId) return;
    api
      .createScanLinkToken()
      .then((data) => {
        setLinkToken(data.token);
        sessionStorage.setItem("certronx_account_link_token", data.token);
      })
      .catch(() => {});
  }, [userId]);

  const isSupported = platform === "windows";

  return (
    <>
      <section className="section-subtle border-b border-[var(--color-border)]">
        <div className="page-container py-12">
          <div className="mx-auto max-w-2xl text-center">
            <p className="text-sm font-medium text-[var(--color-brand)]">Certify a device</p>
            <h1 className="page-title mt-2">Download the Certronx Scanner</h1>
            <p className="page-subtitle mx-auto">
              Create a trusted certification report in minutes. No account required to get started.
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
        {userId && (
          <div className="alert alert-info mb-6 text-sm">
            Signed in — save reports to My Devices automatically using the optional link code below, or
            save from your report page after certification.
          </div>
        )}

        {!isSupported && (
          <div className="alert alert-warning mb-6">
            {platform === "macos" || platform === "android" ? (
              <>
                {platform === "macos" ? "macOS" : "Android"} scanner coming soon. Use Windows to certify
                today.
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

            {linkToken && (
              <div className="rounded-xl border border-[var(--color-border)] bg-[var(--color-bg-subtle)] p-4 text-sm">
                <p className="font-medium">Account link code (optional)</p>
                <p className="mt-1 text-secondary">
                  Set{" "}
                  <code className="rounded bg-white px-1 py-0.5 font-mono text-xs">
                    VERIFYTECH_ACCOUNT_LINK_TOKEN
                  </code>{" "}
                  before running the scanner, or save your report after certification.
                </p>
                <p className="mt-2 font-mono text-xs break-all">{linkToken}</p>
              </div>
            )}

            <a href={agent.full_download_url} className="btn btn-brand btn-block">
              Download Certronx Scanner
            </a>

            <ul className="check-list">
              <li>
                <span className="check-list__icon">✓</span>
                No account required to certify
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
