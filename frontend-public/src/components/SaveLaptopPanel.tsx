"use client";

import { FormEvent, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";

import { api } from "@/lib/api";
import { useAuth } from "@/lib/auth-context";

interface Props {
  verificationCode: string;
  deviceName: string;
}

export function SaveLaptopPanel({ verificationCode, deviceName }: Props) {
  const { userId } = useAuth();
  const router = useRouter();
  const [email, setEmail] = useState("");
  const [claimStatus, setClaimStatus] = useState<"idle" | "saving" | "saved" | "error">("idle");
  const [emailStatus, setEmailStatus] = useState<"idle" | "sending" | "sent" | "error">("idle");
  const [message, setMessage] = useState("");

  async function saveToAccount() {
    if (!userId) {
      router.push(`/login?next=${encodeURIComponent(`/save?code=${verificationCode}`)}`);
      return;
    }
    setClaimStatus("saving");
    try {
      await api.claimReport(verificationCode);
      setClaimStatus("saved");
      setMessage("Saved to your account.");
    } catch (err) {
      setClaimStatus("error");
      setMessage(err instanceof Error ? err.message : "Could not save");
    }
  }

  async function emailReport(e: FormEvent) {
    e.preventDefault();
    setEmailStatus("sending");
    try {
      await api.emailReport(verificationCode, email.trim());
      setEmailStatus("sent");
    } catch (err) {
      setEmailStatus("error");
      setMessage(err instanceof Error ? err.message : "Could not send email");
    }
  }

  return (
    <div className="card card-body mt-8 space-y-6">
      <div>
        <h2 className="font-semibold">Keep this report</h2>
        <p className="mt-1 text-sm text-secondary">
          Save <span className="font-medium text-[var(--color-text-primary)]">{deviceName}</span> to
          your account or email yourself the verification link.
        </p>
      </div>

      <div className="flex flex-wrap gap-3">
        <button
          type="button"
          className="btn btn-trust"
          disabled={claimStatus === "saving" || claimStatus === "saved"}
          onClick={() => void saveToAccount()}
        >
          {claimStatus === "saved" ? "Saved to account" : "Save this laptop to your account"}
        </button>
        {claimStatus === "saved" && (
          <Link href="/my-laptops" className="btn btn-secondary">
            My Laptops
          </Link>
        )}
      </div>

      <form onSubmit={emailReport} className="flex flex-col gap-3 sm:flex-row sm:items-end">
        <label className="flex-1 text-sm font-medium">
          Email me this report
          <input
            type="email"
            required
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            placeholder="you@example.com"
            className="mt-2 w-full rounded-lg border border-[var(--color-border)] bg-white px-3 py-2"
          />
        </label>
        <button type="submit" className="btn btn-secondary" disabled={emailStatus === "sending"}>
          {emailStatus === "sent" ? "Sent" : "Send"}
        </button>
      </form>

      {message && (
        <p className={`text-sm ${claimStatus === "error" || emailStatus === "error" ? "text-[var(--color-error)]" : "text-secondary"}`}>
          {message}
        </p>
      )}

      <button type="button" className="text-left text-sm text-muted opacity-60" disabled title="Coming soon">
        List this laptop for sale — coming soon
      </button>
    </div>
  );
}
