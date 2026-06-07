"use client";

import { FormEvent, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { api } from "@/lib/api";
import { useAuth } from "@/lib/auth-context";

export default function PairPage() {
  const router = useRouter();
  const { userId } = useAuth();
  const [pairingCode, setPairingCode] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (!userId) {
      router.push("/login?next=/pair");
      return;
    }

    const code = pairingCode.trim().toUpperCase();
    if (code.length !== 6) {
      setError("Enter the 6-character pairing code shown in the Certronx Agent.");
      return;
    }

    setBusy(true);
    setError("");
    setSuccess("");

    try {
      const result = await api.claimAgentPairing(code);
      setSuccess(result.message);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not connect to the agent.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="page-container page-container--narrow py-12">
      <div className="mx-auto max-w-lg">
        <p className="text-sm font-medium text-[var(--color-brand)]">Agent pairing</p>
        <h1 className="page-title mt-2">Connect Certronx Agent</h1>
        <p className="page-subtitle mt-3">
          Enter the 6-character pairing code displayed on this device&apos;s Certronx Agent window.
          Pairing codes expire after 10 minutes.
        </p>

        {!userId && (
          <div className="alert alert-info mt-6 text-sm">
            <Link href="/login?next=/pair" className="text-[var(--color-brand)] hover:underline">
              Sign in
            </Link>{" "}
            to connect an agent to your Certronx account.
          </div>
        )}

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
              onChange={(e) => setPairingCode(e.target.value.toUpperCase().replace(/[^A-Z0-9]/g, ""))}
              className="mt-2 w-full rounded-lg border border-[var(--color-border)] bg-[var(--color-surface)] px-4 py-3 font-mono text-2xl tracking-[0.3em] uppercase"
              placeholder="A7K9Q2"
              disabled={busy}
            />
          </div>

          {error && <div className="alert alert-error text-sm">{error}</div>}
          {success && <div className="alert alert-success text-sm">{success}</div>}

          <button type="submit" className="btn btn-primary w-full" disabled={busy || !userId}>
            {busy ? "Connecting…" : "Connect agent"}
          </button>
        </form>

        <p className="mt-6 text-sm text-secondary">
          Launched from the website with a token instead?{" "}
          <Link href="/download" className="text-[var(--color-brand)] hover:underline">
            Certify from download page
          </Link>
          .
        </p>
      </div>
    </div>
  );
}
