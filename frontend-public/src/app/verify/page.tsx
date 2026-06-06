"use client";

import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { Suspense, useState } from "react";

import { api } from "@/lib/api";

const VERIFY_ITEMS = [
  "Device Identity",
  "Battery Health",
  "Storage Health",
  "Hardware Configuration",
  "Certification Date",
];

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
        setError("No certification found with this code.");
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

  function handleViewReport() {
    router.push(`/c/${code.trim().toUpperCase()}`);
  }

  return (
    <div className="page-container">
      <div className="mx-auto grid max-w-5xl gap-12 lg:grid-cols-5">
        <div className="lg:col-span-3">
          <header className="mb-8">
            <p className="text-sm font-medium text-[var(--color-brand)]">Buyer verification</p>
            <h1 className="page-title mt-2">Verify a Certified Device</h1>
            <p className="page-subtitle">
              Enter a Certronx certification code or scan a QR code to validate a device report before
              you purchase.
            </p>
          </header>

          <form onSubmit={handleLookup} className="card card-body space-y-5">
            <div>
              <label className="label" htmlFor="cert-code">
                Certification code
              </label>
              <input
                id="cert-code"
                type="text"
                placeholder="XXXX-XXXX-XXXX"
                value={code}
                onChange={(e) => setCode(e.target.value.toUpperCase())}
                className="input input--mono"
              />
              <p className="mt-2 text-sm text-muted">
                Find this code on the seller&apos;s Certronx report or QR label.
              </p>
            </div>

            {error && <p className="alert alert-error">{error}</p>}

            {found && (
              <div className="alert alert-success">
                <p className="font-medium">Certification found</p>
                <p className="mt-1">{found.device_name}</p>
                <p className="mt-1 text-sm capitalize opacity-90">Status: {found.status}</p>
              </div>
            )}

            <button type="submit" disabled={loading} className="btn btn-secondary btn-block">
              {loading ? "Checking…" : "Look up certification"}
            </button>

            {found && (
              <div className="grid gap-3 sm:grid-cols-2">
                <button type="button" onClick={handleViewReport} className="btn btn-brand">
                  View report
                </button>
                <button type="button" onClick={handleContinue} className="btn btn-secondary">
                  Re-scan device
                </button>
              </div>
            )}
          </form>

          <p className="mt-6 text-center text-sm text-muted">
            New to Certronx?{" "}
            <Link href="/sample-report" className="text-[var(--color-brand)] hover:underline">
              View sample report
            </Link>
          </p>
        </div>

        <aside className="lg:col-span-2">
          <div className="card card-body">
            <h2 className="font-semibold">What you&apos;ll verify</h2>
            <ul className="check-list mt-5">
              {VERIFY_ITEMS.map((item) => (
                <li key={item}>
                  <span className="check-list__icon">✓</span>
                  {item}
                </li>
              ))}
            </ul>
            <div className="mt-8 rounded-xl border border-dashed border-[var(--color-border)] bg-[var(--color-bg-subtle)] p-5 text-center">
              <p className="text-sm font-medium">QR verification</p>
              <p className="mt-2 text-sm text-secondary">
                Scan the QR code on a Certronx report to open verification instantly on your phone.
              </p>
            </div>
          </div>
        </aside>
      </div>
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
