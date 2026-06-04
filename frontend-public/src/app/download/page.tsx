"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { Collapsible } from "@/components/Collapsible";
import { api, type AgentInfo } from "@/lib/api";
import { env } from "@/lib/env";
import { getRecommendedPlatform } from "@/components/OSDetector";

export default function DownloadPage() {
  const [agent, setAgent] = useState<AgentInfo | null>(null);
  const [platform, setPlatform] = useState("windows");
  const [error, setError] = useState("");

  useEffect(() => {
    const p = getRecommendedPlatform();
    setPlatform(p);
    api
      .getAgent(p === "macos" || p === "android" ? p : "windows")
      .then(setAgent)
      .catch(() => {
        api.getAgent("windows").then(setAgent).catch((e) => setError(e.message));
      });
  }, []);

  const isSupported = platform === "windows";

  return (
    <div className="page-container page-container--narrow">
      <header className="mb-10 text-center">
        <h1 className="page-title">Download the agent</h1>
        <p className="page-subtitle">
          Run the diagnostic on the device you want to certify or verify.
        </p>
      </header>

      {!isSupported && (
        <div className="alert alert-warning mb-6">
          {platform === "macos" || platform === "android" ? (
            <>
              {platform === "macos" ? "macOS" : "Android"} support is coming soon. For now, use a
              Windows device to certify.
            </>
          ) : (
            <>Windows is recommended for certification at this time.</>
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
            <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-lg bg-[var(--color-bg-secondary)] text-lg">
              💻
            </div>
            <div>
              <h2 className="font-semibold capitalize">DevicePassport Agent — {agent.platform}</h2>
              <p className="text-sm text-secondary">Version {agent.version}</p>
            </div>
          </div>

          <CollapsibleChecksum checksum={agent.checksum} />

          <a href={agent.full_download_url} className="btn btn-trust btn-block">
            Download for Windows
          </a>

          <div className="rounded-lg bg-[var(--color-bg-secondary)] p-4 text-sm text-secondary">
            <p className="mb-2 font-medium text-[var(--color-text-primary)]">What happens next</p>
            <ol className="list-decimal space-y-1 pl-4">
              <li>Run the agent on your device</li>
              <li>Wait for diagnostics to finish (~5 min)</li>
              <li>Your certificate link will appear when complete</li>
            </ol>
          </div>
        </div>
      )}

      <p className="mt-8 text-center text-sm text-muted">
        Already certified?{" "}
        <Link href="/verify" className="text-[var(--color-trust)] hover:underline">
          Verify a certificate
        </Link>
      </p>
    </div>
  );
}

function CollapsibleChecksum({ checksum }: { checksum: string }) {
  return (
    <Collapsible title="File checksum">
      <p className="font-mono text-xs break-all">{checksum}</p>
    </Collapsible>
  );
}
