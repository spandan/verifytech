"use client";

import { FormEvent, Suspense, useState } from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";

import { api } from "@/lib/api";
import { useAuth } from "@/lib/auth-context";

function SaveForm() {
  const params = useSearchParams();
  const router = useRouter();
  const initialCode = params.get("code") || "";
  const { userId, loading } = useAuth();
  const [code, setCode] = useState(initialCode);
  const [status, setStatus] = useState<"idle" | "saving" | "done" | "error">("idle");
  const [message, setMessage] = useState("");

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    if (!userId) {
      router.push(`/login?next=${encodeURIComponent(`/save?code=${code}`)}`);
      return;
    }
    setStatus("saving");
    try {
      const result = await api.claimReport(code);
      setStatus("done");
      setMessage(result.message);
      setTimeout(() => router.push("/my-laptops"), 1200);
    } catch (err) {
      setStatus("error");
      setMessage(err instanceof Error ? err.message : "Could not save laptop");
    }
  }

  if (loading) {
    return <p className="text-secondary">Loading…</p>;
  }

  return (
    <div className="card card-body space-y-6">
      <div>
        <h1 className="page-title">Save this report</h1>
        <p className="page-subtitle mt-2">
          Enter your Certronx certification code. If you&apos;re not signed in, we&apos;ll send a
          magic link first.
        </p>
      </div>

      {!userId && (
        <p className="rounded-lg bg-[var(--color-bg-secondary)] p-4 text-sm text-secondary">
          You&apos;ll be asked to sign in with email before saving.
        </p>
      )}

      <form onSubmit={onSubmit} className="space-y-4">
        <label className="block text-sm font-medium">
          Verification code
          <input
            value={code}
            onChange={(e) => setCode(e.target.value.toUpperCase())}
            required
            placeholder="XXXX-XXXX-XXXX"
            className="mt-2 w-full rounded-lg border border-[var(--color-border)] bg-white px-3 py-2 font-mono"
          />
        </label>
        <button type="submit" className="btn btn-brand btn-block" disabled={status === "saving"}>
          {userId ? "Save this report" : "Sign in and save"}
        </button>
      </form>

      {message && (
        <p className={`text-sm ${status === "error" ? "text-[var(--color-error)]" : "text-[var(--color-success-text)]"}`}>
          {message}
        </p>
      )}

      <p className="text-center text-sm text-muted">
        <Link href="/my-laptops" className="text-[var(--color-brand)] hover:underline">
          View My Devices
        </Link>
      </p>
    </div>
  );
}

export default function SavePage() {
  return (
    <div className="page-container page-container--narrow py-16">
      <Suspense fallback={<p className="text-secondary">Loading…</p>}>
        <SaveForm />
      </Suspense>
    </div>
  );
}
