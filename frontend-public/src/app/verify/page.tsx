"use client";

import { useRouter, useSearchParams } from "next/navigation";
import { Suspense, useState } from "react";
import { api } from "@/lib/api";

function VerifyForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const [code, setCode] = useState(searchParams.get("code") || "");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [found, setFound] = useState<{ device_name?: string; status?: string } | null>(null);

  async function handleLookup(e: React.FormEvent) {
    e.preventDefault();
    setError("");
    setFound(null);
    if (!code.trim()) return;
    setLoading(true);
    try {
      const result = await api.verifyLookup(code.trim().toUpperCase());
      if (!result.exists) {
        setError("No certificate found with this code.");
        return;
      }
      setFound(result);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Lookup failed");
    } finally {
      setLoading(false);
    }
  }

  function handleContinue() {
    sessionStorage.setItem("verify_code", code.trim().toUpperCase());
    router.push("/download?mode=verify");
  }

  return (
    <div className="page-container page-container--narrow">
      <header className="mb-10 text-center">
        <h1 className="page-title">Verify a device</h1>
        <p className="page-subtitle">
          Enter the seller&apos;s certificate code, then scan the device you&apos;re buying.
        </p>
      </header>

      <form onSubmit={handleLookup} className="card card-body space-y-5">
        <div>
          <label className="label" htmlFor="cert-code">
            Certificate code
          </label>
          <input
            id="cert-code"
            type="text"
            placeholder="XXXX-XXXX-XXXX"
            value={code}
            onChange={(e) => setCode(e.target.value.toUpperCase())}
            className="input input--mono"
          />
        </div>

        {error && <p className="alert alert-error">{error}</p>}

        {found && (
          <div className="alert alert-success">
            <p className="font-medium">Certificate found</p>
            <p className="mt-1">{found.device_name}</p>
            <p className="mt-1 text-sm capitalize opacity-90">Status: {found.status}</p>
          </div>
        )}

        <button type="submit" disabled={loading} className="btn btn-secondary btn-block">
          {loading ? "Checking…" : "Look up certificate"}
        </button>

        {found && (
          <button type="button" onClick={handleContinue} className="btn btn-trust btn-block">
            Download verifier & scan device
          </button>
        )}
      </form>
    </div>
  );
}

export default function VerifyPage() {
  return (
    <Suspense fallback={<div className="page-container text-center text-muted">Loading…</div>}>
      <VerifyForm />
    </Suspense>
  );
}
