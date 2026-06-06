"use client";

import { FormEvent, Suspense, useState } from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";

import { getSupabaseClient } from "@/lib/supabase";
import { env, supabaseConfigured } from "@/lib/env";

function LoginForm() {
  const params = useSearchParams();
  const next = params.get("next") || "/my-laptops";
  const [email, setEmail] = useState("");
  const [status, setStatus] = useState<"idle" | "sending" | "sent" | "error">("idle");
  const [message, setMessage] = useState("");

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    const supabase = getSupabaseClient();
    if (!supabase) {
      setStatus("error");
      setMessage("Sign-in is not configured yet. Add Supabase env vars to enable magic links.");
      return;
    }
    setStatus("sending");
    setMessage("");
    const redirectTo = `${env.siteUrl}/auth/callback?next=${encodeURIComponent(next)}`;
    const { error } = await supabase.auth.signInWithOtp({
      email: email.trim(),
      options: { emailRedirectTo: redirectTo },
    });
    if (error) {
      setStatus("error");
      setMessage(error.message);
      return;
    }
    setStatus("sent");
    setMessage("Check your email for a magic link to sign in.");
  }

  return (
    <div className="page-container page-container--narrow py-16">
      <div className="card card-body space-y-6">
        <div>
          <h1 className="page-title">Sign in with email</h1>
          <p className="page-subtitle mt-2">
            No password needed. We&apos;ll email you a secure link to access your saved laptops.
          </p>
        </div>

        {!supabaseConfigured && (
          <p className="rounded-lg bg-[var(--color-warning-bg)] p-4 text-sm text-[var(--color-warning-text)]">
            Configure <code className="font-mono">NEXT_PUBLIC_SUPABASE_URL</code> and{" "}
            <code className="font-mono">NEXT_PUBLIC_SUPABASE_ANON_KEY</code> to enable sign-in.
          </p>
        )}

        <form onSubmit={onSubmit} className="space-y-4">
          <label className="block text-sm font-medium">
            Email
            <input
              type="email"
              required
              autoComplete="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              className="mt-2 w-full rounded-lg border border-[var(--color-border)] bg-white px-3 py-2"
              placeholder="you@example.com"
            />
          </label>
          <button type="submit" className="btn btn-trust btn-block" disabled={status === "sending"}>
            {status === "sending" ? "Sending…" : "Send magic link"}
          </button>
        </form>

        {message && (
          <p className={`text-sm ${status === "error" ? "text-[var(--color-error)]" : "text-secondary"}`}>
            {message}
          </p>
        )}

        <p className="text-center text-sm text-muted">
          Just scanning?{" "}
          <Link href="/start" className="text-[var(--color-trust)] hover:underline">
            Scan this laptop without signing in
          </Link>
        </p>
      </div>
    </div>
  );
}

export default function LoginPage() {
  return (
    <Suspense fallback={<div className="page-container py-16 text-center text-secondary">Loading…</div>}>
      <LoginForm />
    </Suspense>
  );
}
