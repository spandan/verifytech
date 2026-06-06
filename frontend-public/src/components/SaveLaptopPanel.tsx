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
      setMessage("Saved to your Certronx account.");
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
        <h2 className="font-semibold">Save this report to your account</h2>
        <p className="mt-1 text-sm text-secondary">
          Keep <span className="font-medium text-[var(--color-text-primary)]">{deviceName}</span> in
          your Certronx account for easy access and sharing.
        </p>
      </div>

      <ul className="check-list">
        <li>
          <span className="check-list__icon">✓</span>
          Access reports anytime
        </li>
        <li>
          <span className="check-list__icon">✓</span>
          Manage multiple devices
        </li>
        <li>
          <span className="check-list__icon">✓</span>
          Share reports easily
        </li>
      </ul>

      <div className="flex flex-wrap gap-3">
        <button
          type="button"
          className="btn btn-brand"
          disabled={claimStatus === "saving" || claimStatus === "saved"}
          onClick={() => void saveToAccount()}
        >
          {claimStatus === "saved" ? "Saved to account" : "Save this report"}
        </button>
        {claimStatus === "saved" && (
          <Link href="/my-laptops" className="btn btn-secondary">
            My Devices
          </Link>
        )}
      </div>

      <form onSubmit={emailReport} className="flex flex-col gap-3 border-t border-[var(--color-border)] pt-6 sm:flex-row sm:items-end">
        <label className="flex-1 text-sm font-medium">
          Email me this report
          <input
            type="email"
            required
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            placeholder="you@example.com"
            className="input mt-2"
          />
        </label>
        <button type="submit" className="btn btn-secondary" disabled={emailStatus === "sending"}>
          {emailStatus === "sent" ? "Sent" : "Send"}
        </button>
      </form>

      {message && (
        <p
          className={`text-sm ${claimStatus === "error" || emailStatus === "error" ? "text-[var(--color-error)]" : "text-secondary"}`}
        >
          {message}
        </p>
      )}

      <SellerNotesForm verificationCode={verificationCode} />

      <button type="button" className="text-left text-sm text-muted opacity-60" disabled title="Coming soon">
        List this device for sale — coming soon
      </button>
    </div>
  );
}

function SellerNotesForm({ verificationCode }: { verificationCode: string }) {
  const storageKey = `certronx_seller_notes_${verificationCode}`;
  const [open, setOpen] = useState(false);
  const [saved, setSaved] = useState(false);
  const [form, setForm] = useState({
    charger_included: "",
    box_included: "",
    physical_condition: "",
    notes: "",
  });

  function handleSave(e: FormEvent) {
    e.preventDefault();
    sessionStorage.setItem(storageKey, JSON.stringify(form));
    setSaved(true);
  }

  if (!open) {
    return (
      <button
        type="button"
        className="btn btn-ghost text-sm"
        onClick={() => setOpen(true)}
      >
        Add optional seller details (charger, condition, notes)
      </button>
    );
  }

  return (
    <form onSubmit={handleSave} className="space-y-4 rounded-xl border border-[var(--color-border)] bg-[var(--color-bg-subtle)] p-5">
      <p className="text-sm font-medium">Optional seller details</p>
      <p className="text-sm text-secondary">Help buyers understand what&apos;s included — all fields optional.</p>
      <label className="block text-sm">
        Original charger included?
        <select
          className="select mt-1"
          value={form.charger_included}
          onChange={(e) => setForm({ ...form, charger_included: e.target.value })}
        >
          <option value="">Select…</option>
          <option value="yes">Yes</option>
          <option value="no">No</option>
          <option value="not_sure">Not sure</option>
        </select>
      </label>
      <label className="block text-sm">
        Original box included?
        <select
          className="select mt-1"
          value={form.box_included}
          onChange={(e) => setForm({ ...form, box_included: e.target.value })}
        >
          <option value="">Select…</option>
          <option value="yes">Yes</option>
          <option value="no">No</option>
        </select>
      </label>
      <label className="block text-sm">
        Physical condition
        <select
          className="select mt-1"
          value={form.physical_condition}
          onChange={(e) => setForm({ ...form, physical_condition: e.target.value })}
        >
          <option value="">Select…</option>
          <option value="excellent">Excellent</option>
          <option value="good">Good — light wear</option>
          <option value="fair">Fair — visible wear</option>
        </select>
      </label>
      <label className="block text-sm">
        Additional notes
        <textarea
          className="input mt-1 min-h-20 resize-y"
          value={form.notes}
          onChange={(e) => setForm({ ...form, notes: e.target.value })}
          placeholder="Anything else buyers should know"
        />
      </label>
      <button type="submit" className="btn btn-secondary text-sm">
        {saved ? "Saved locally" : "Save details"}
      </button>
    </form>
  );
}
