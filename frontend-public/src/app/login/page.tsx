"use client";

import { FormEvent, Suspense, useEffect, useState } from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";

import { getSupabaseClient } from "@/lib/supabase";
import { env, supabaseConfigured } from "@/lib/env";

const RESEND_COOLDOWN_SECONDS = 60;

function authErrorMessage(message: string, status?: number): string {
  const lower = message.toLowerCase();

  if (lower.includes("rate limit") || lower.includes("over_email_send_rate_limit")) {
    return (
      "Too many sign-in emails were sent recently. Supabase limits magic links to about 2 per hour " +
      "on the built-in email service. Wait up to an hour, use a link already in your inbox, or connect " +
      "custom SMTP in Supabase (Project Settings → Authentication → SMTP)."
    );
  }

  if (lower.includes("not authorized")) {
    return (
      "This email address is not authorized on Supabase's built-in email service. Default Supabase " +
      "email only works for addresses on your Supabase organization team. Add custom SMTP " +
      "(Resend recommended) in Project Settings → Authentication → SMTP to send to any user."
    );
  }

  if (
    lower.includes("error sending magic link") ||
    lower.includes("error sending confirmation") ||
    (status === 500 && lower.includes("error sending"))
  ) {
    return (
      "Supabase could not send the magic link email. This usually means email delivery is not configured " +
      "for production. In Supabase: Project Settings → Authentication → SMTP → enable custom SMTP " +
      "(e.g. Resend: host smtp.resend.com, port 465, user resend, password = your Resend API key, " +
      "sender = a verified domain address). Also check Authentication → Logs for the exact SMTP error."
    );
  }

  return message;
}

function LoginForm() {
  const params = useSearchParams();
  const next = params.get("next") || "/my-laptops";
  const [email, setEmail] = useState("");
  const [status, setStatus] = useState<"idle" | "sending" | "sent" | "error">("idle");
  const [message, setMessage] = useState("");
  const [cooldown, setCooldown] = useState(0);

  useEffect(() => {
    if (cooldown <= 0) return;
    const timer = window.setInterval(() => {
      setCooldown((seconds) => (seconds <= 1 ? 0 : seconds - 1));
    }, 1000);
    return () => window.clearInterval(timer);
  }, [cooldown]);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    if (cooldown > 0) return;

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
      setMessage(authErrorMessage(error.message, error.status));
      return;
    }
    setStatus("sent");
    setCooldown(RESEND_COOLDOWN_SECONDS);
    setMessage("Check your email for a magic link to sign in. Links expire after a short time.");
  }

  return (
    <div className="page-container page-container--narrow py-16">
      <div className="card card-body space-y-6">
        <div>
          <h1 className="page-title">Sign in with email</h1>
          <p className="page-subtitle mt-2">
            No password needed. We&apos;ll send a secure magic link to access your Certronx reports and
            devices.
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
          <button
            type="submit"
            className="btn btn-brand btn-block"
            disabled={status === "sending" || cooldown > 0}
          >
            {status === "sending"
              ? "Sending…"
              : cooldown > 0
                ? `Resend available in ${cooldown}s`
                : status === "sent"
                  ? "Send another link"
                  : "Send magic link"}
          </button>
        </form>

        {message && (
          <p className={`text-sm ${status === "error" ? "text-[var(--color-error)]" : "text-secondary"}`}>
            {message}
          </p>
        )}

        <p className="text-center text-sm text-muted">
          Just scanning?{" "}
          <Link href="/download" className="text-[var(--color-brand)] hover:underline">
            Certify a device without signing in
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
