"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Collapsible } from "@/components/Collapsible";
import { CopyableCode } from "@/components/CopyableCode";
import { api, type AgentInfo, type CertificationSession } from "@/lib/api";
import { trackEvent } from "@/lib/analytics";
import { useAuth } from "@/lib/auth-context";
import { env } from "@/lib/env";
import { getRecommendedPlatform } from "@/components/OSDetector";

type DeviceChoice = "laptop" | "desktop" | null;

const FLOW_STEPS = [
  { step: "1", title: "Choose device type", desc: "Certronx Phase 1 certifies Windows laptops." },
  { step: "2", title: "Launch agent", desc: "Opens Certronx Agent with a signed session token." },
  { step: "3", title: "Eligibility check", desc: "Know within seconds if this device can be certified." },
  { step: "4", title: "Certificate", desc: "Scan, upload, and save to your account." },
];

export default function DownloadPage() {
  const router = useRouter();
  const { userId } = useAuth();
  const [agent, setAgent] = useState<AgentInfo | null>(null);
  const [platform, setPlatform] = useState("windows");
  const [error, setError] = useState("");
  const [deviceChoice, setDeviceChoice] = useState<DeviceChoice>(null);
  const [session, setSession] = useState<CertificationSession | null>(null);
  const [launchBusy, setLaunchBusy] = useState(false);

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

  const startLaptopCertification = useCallback(async () => {
    if (!userId) {
      router.push("/login?next=/download");
      return;
    }

    setLaunchBusy(true);
    setError("");
    setSession(null);

    try {
      const created = await api.createCertificationSession("laptop");
      setSession(created);
      trackEvent("CertificationSessionCreated", {
        expected_device_type: created.expected_device_type,
        expires_at: created.expires_at,
      });
      trackEvent("DeepLinkLaunchAttempted");
      window.location.href = created.deep_link;
    } catch (e) {
      setError(e instanceof Error ? e.message : "Could not start certification.");
    } finally {
      setLaunchBusy(false);
    }
  }, [router, userId]);

  const isSupported = platform === "windows";

  return (
    <>
      <section className="section-subtle border-b border-[var(--color-border)]">
        <div className="page-container py-12">
          <div className="mx-auto max-w-2xl text-center">
            <p className="text-sm font-medium text-[var(--color-brand)]">Certify a device</p>
            <h1 className="page-title mt-2">Certify with Certronx</h1>
            <p className="page-subtitle mx-auto">
              Phase 1 supports Windows 10 and 11 laptops. Eligibility is checked in seconds before any full scan.
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
            to certify a laptop and save the certificate to your account.
          </div>
        )}

        {!isSupported && (
          <div className="alert alert-warning mb-6">
            Windows is required for certification at this time.
          </div>
        )}

        {error && (
          <div className="alert alert-error mb-6">
            {error}. Make sure the API is running at {env.apiUrl}.
          </div>
        )}

        {userId && isSupported && (
          <div className="card card-body mb-6 space-y-5">
            <div>
              <h2 className="font-semibold">What type of device are you certifying?</h2>
              <p className="mt-1 text-sm text-secondary">
                Certronx checks eligibility before running diagnostics so you are not kept waiting on unsupported hardware.
              </p>
            </div>

            <div className="grid gap-3 sm:grid-cols-2">
              <button
                type="button"
                className={`rounded-xl border p-4 text-left transition ${
                  deviceChoice === "laptop"
                    ? "border-[var(--color-brand)] bg-[var(--color-bg-subtle)]"
                    : "border-[var(--color-border)]"
                }`}
                onClick={() => setDeviceChoice("laptop")}
              >
                <p className="font-medium">Laptop</p>
                <p className="mt-1 text-sm text-secondary">Windows 10 or 11 with a detected battery</p>
              </button>
              <button
                type="button"
                className={`rounded-xl border p-4 text-left transition ${
                  deviceChoice === "desktop"
                    ? "border-[var(--color-border)] bg-[var(--color-bg-subtle)]"
                    : "border-[var(--color-border)]"
                }`}
                onClick={() => setDeviceChoice("desktop")}
              >
                <p className="font-medium">Desktop</p>
                <p className="mt-1 text-sm text-secondary">Coming in a future release</p>
              </button>
            </div>

            {deviceChoice === "desktop" && (
              <div className="rounded-xl border border-[var(--color-border)] bg-[var(--color-bg-subtle)] p-4 text-sm">
                <p className="font-medium">Certronx desktop certification is not yet available.</p>
                <p className="mt-2 text-secondary">
                  Desktop, workstation, and custom PC support is planned for a future release.
                </p>
              </div>
            )}

            {deviceChoice === "laptop" && (
              <>
                <button
                  type="button"
                  className="btn btn-brand btn-block"
                  disabled={launchBusy}
                  onClick={() => void startLaptopCertification()}
                >
                  {launchBusy ? "Creating certification session…" : "Certify Device"}
                </button>

                {session && (
                  <div className="rounded-xl border border-[var(--color-border)] bg-[var(--color-bg-subtle)] p-4 text-sm space-y-2">
                    <p className="font-medium text-[var(--color-brand)]">
                      Waiting for Certronx Agent to open…
                    </p>
                    <p className="text-secondary">
                      Your signed session expires at {new Date(session.expires_at).toLocaleTimeString()}.
                      No pairing code is required.
                    </p>
                    {agent && (
                      <a href={agent.full_download_url} className="btn btn-secondary btn-sm inline-flex">
                        Download Windows Agent
                      </a>
                    )}
                  </div>
                )}

                <p className="text-sm text-secondary">
                  Agent already open without a token?{" "}
                  <Link href="/pair" className="text-[var(--color-brand)] hover:underline">
                    Enter the pairing code from the agent
                  </Link>
                  .
                </p>
              </>
            )}
          </div>
        )}

        {agent && deviceChoice !== "desktop" && (
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
          </div>
        )}

        <p className="mt-8 text-center text-sm text-muted">
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
      <CopyableCode value={checksum} monoClassName="font-mono text-xs break-all" copyLabel="Copy checksum" />
    </Collapsible>
  );
}
