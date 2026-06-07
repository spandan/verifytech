"use client";

import { FormEvent, Suspense, useCallback, useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { api } from "@/lib/api";
import { useAuth } from "@/lib/auth-context";

function normalizeCode(raw: string): string {
  return raw.trim().toUpperCase().replace(/[^A-Z0-9]/g, "").slice(0, 6);
}

function isAlreadyUsedMessage(message: string): boolean {
  const lower = message.toLowerCase();
  return lower.includes("already been used") || lower.includes("already connected");
}

function PairForm() {
  const router = useRouter();
  const params = useSearchParams();
  const codeFromUrl = normalizeCode(params.get("code") || "");
  const { userId } = useAuth();
  const [pairingCode, setPairingCode] = useState(codeFromUrl);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const claimInFlight = useRef(false);

  const loginNext = codeFromUrl
    ? `/pair?code=${encodeURIComponent(codeFromUrl)}`
    : "/pair";

  useEffect(() => {
    if (codeFromUrl) setPairingCode(codeFromUrl);
  }, [codeFromUrl]);

  const recoverClaimStatus = useCallback(async (code: string): Promise<boolean> => {
    try {
      const status = await api.getAgentPairingClaimStatus(code);
      if (status.connected) {
        setSuccess(status.message);
        setError("");
        return true;
      }
    } catch {
      // Ignore recovery failures — caller shows the original error.
    }
    return false;
  }, []);

  const claimCode = useCallback(
    async (code: string) => {
      const normalized = normalizeCode(code);
      if (normalized.length !== 6) {
        setError("Enter the 6-character pairing code shown in the Certronx Agent.");
        return;
      }

      if (!userId) {
        router.push(`/login?next=${encodeURIComponent(loginNext)}`);
        return;
      }

      if (claimInFlight.current) return;
      claimInFlight.current = true;
      setBusy(true);
      setError("");
      setSuccess("");

      try {
        const result = await api.claimAgentPairing(normalized);
        setSuccess(result.message || "Paired with your account.");
        setError("");
      } catch (err) {
        const message = err instanceof Error ? err.message : "Could not connect to the agent.";
        if (isAlreadyUsedMessage(message)) {
          const recovered = await recoverClaimStatus(normalized);
          if (!recovered) {
            setSuccess(
              "This code was already used. If you clicked Connect before, return to Certronx Agent on this PC — pairing may have succeeded.",
            );
            setError("");
          }
        } else if (message.toLowerCase().includes("could not reach the certronx api")) {
          const recovered = await recoverClaimStatus(normalized);
          if (!recovered) {
            setError(message);
          }
        } else {
          setError(message);
        }
      } finally {
        claimInFlight.current = false;
        setBusy(false);
      }
    },
    [loginNext, recoverClaimStatus, router, userId],
  );

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    await claimCode(pairingCode);
  }

  const paired = Boolean(success);

  return (
    <div className="page-container page-container--narrow py-12">
      <div className="mx-auto max-w-lg">
        <p className="text-sm font-medium text-[var(--color-brand)]">Agent pairing</p>
        <h1 className="page-title mt-2">Connect Certronx Agent</h1>
        <p className="page-subtitle mt-3">
          {codeFromUrl
            ? "Your pairing code from Certronx Agent is filled in below. Click Connect once and return to the agent."
            : "Enter the 6-character code shown in Certronx Agent on this PC."}
        </p>

        {!userId && (
          <div className="alert alert-info mt-6 text-sm">
            <Link
              href={`/login?next=${encodeURIComponent(loginNext)}`}
              className="text-[var(--color-brand)] hover:underline"
            >
              Sign in
            </Link>{" "}
            to connect this device to your Certronx account.
          </div>
        )}

        {paired ? (
          <div className="card card-body mt-8 space-y-4">
            <div className="alert alert-success text-sm space-y-2">
              <p className="font-semibold">Paired with your account</p>
              <p>
                Return to <strong>Certronx Agent</strong> on this PC. If you were linking before submit, your report is
                ready — click <strong>Submit to Certronx</strong>.
              </p>
            </div>
            <p className="text-sm text-secondary">
              You can close this browser tab. Do not close the agent until certification finishes.
            </p>
          </div>
        ) : (
          <form onSubmit={handleSubmit} className="card card-body mt-8 space-y-5">
            <div>
              <label htmlFor="pairing-code" className="block text-sm font-medium">
                Pairing code
              </label>
              <input
                id="pairing-code"
                type="text"
                inputMode="text"
                autoComplete="off"
                spellCheck={false}
                maxLength={6}
                value={pairingCode}
                onChange={(e) => setPairingCode(normalizeCode(e.target.value))}
                className="mt-2 w-full rounded-lg border border-[var(--color-border)] bg-[var(--color-surface)] px-4 py-3 font-mono text-2xl tracking-[0.3em] uppercase"
                placeholder="A7K9Q2"
                disabled={busy}
                readOnly={Boolean(codeFromUrl) && codeFromUrl.length === 6}
              />
            </div>

            {error && <div className="alert alert-error text-sm">{error}</div>}
            {success && <div className="alert alert-success text-sm">{success}</div>}

            <button type="submit" className="btn btn-primary w-full" disabled={busy || !userId}>
              {busy ? "Connecting…" : "Connect agent"}
            </button>
          </form>
        )}

        <p className="mt-6 text-sm text-secondary">
          Need to install the agent?{" "}
          <Link href="/download" className="text-[var(--color-brand)] hover:underline">
            Download from Certronx
          </Link>
          .
        </p>
      </div>
    </div>
  );
}

export default function PairPage() {
  return (
    <Suspense fallback={<div className="page-container py-12 text-secondary">Loading…</div>}>
      <PairForm />
    </Suspense>
  );
}
