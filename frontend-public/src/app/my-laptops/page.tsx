"use client";

import { FormEvent, useEffect, useState } from "react";
import Link from "next/link";

import { api, type MyLaptop } from "@/lib/api";
import { useAuth } from "@/lib/auth-context";
import { env } from "@/lib/env";

function statusBadge(status: string): string {
  switch (status) {
    case "active":
      return "badge-success";
    case "expired":
      return "badge-warning";
    case "revoked":
      return "badge-error";
    default:
      return "badge-neutral";
  }
}

export default function MyLaptopsPage() {
  const { userId, loading: authLoading } = useAuth();
  const [laptops, setLaptops] = useState<MyLaptop[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [editingId, setEditingId] = useState<string | null>(null);
  const [nicknameDraft, setNicknameDraft] = useState("");

  useEffect(() => {
    if (authLoading) return;
    if (!userId) {
      setLoading(false);
      return;
    }
    api
      .getMyLaptops()
      .then((data) => setLaptops(data.laptops))
      .catch((e: Error) => setError(e.message))
      .finally(() => setLoading(false));
  }, [userId, authLoading]);

  async function saveNickname(deviceId: string) {
    try {
      const updated = await api.renameDevice(deviceId, nicknameDraft);
      setLaptops((prev) => prev.map((l) => (l.device_id === deviceId ? updated : l)));
      setEditingId(null);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Could not rename device");
    }
  }

  if (authLoading || loading) {
    return <div className="page-container py-16 text-center text-secondary">Loading your laptops…</div>;
  }

  if (!userId) {
    return (
      <div className="page-container page-container--narrow py-16 text-center">
        <h1 className="page-title">My Devices</h1>
        <p className="page-subtitle mt-2">Sign in to access your Certronx certification reports.</p>
        <Link href="/login?next=/my-laptops" className="btn btn-brand mt-8 inline-flex">
          Sign in with email
        </Link>
      </div>
    );
  }

  return (
    <div className="page-container py-10">
      <header className="mb-8 flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="page-title">My Devices</h1>
          <p className="page-subtitle mt-1">Your certified devices and verification reports.</p>
        </div>
          <Link href="/download" className="btn btn-brand">
            Certify a device
          </Link>
      </header>

      {error && <p className="mb-4 text-sm text-[var(--color-error)]">{error}</p>}

      {laptops.length === 0 ? (
        <div className="empty-state">
          <p className="text-secondary">No saved laptops yet.</p>
          <p className="mt-2 text-sm text-muted">
            Scan a laptop, then save it to your account with your verification code.
          </p>
          <Link href="/download" className="btn btn-brand mt-6 inline-flex">
            Certify a device
          </Link>
        </div>
      ) : (
        <div className="space-y-4">
          {laptops.map((laptop) => (
            <div key={laptop.device_id} className="card card-body space-y-4">
              <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                <div>
                  {editingId === laptop.device_id ? (
                    <form
                      className="flex flex-wrap gap-2"
                      onSubmit={(e: FormEvent) => {
                        e.preventDefault();
                        void saveNickname(laptop.device_id);
                      }}
                    >
                      <input
                        value={nicknameDraft}
                        onChange={(e) => setNicknameDraft(e.target.value)}
                        className="rounded-lg border border-[var(--color-border)] px-3 py-1.5 text-sm"
                        placeholder="Nickname"
                      />
                      <button type="submit" className="btn btn-secondary text-sm">
                        Save
                      </button>
                      <button type="button" className="btn btn-secondary text-sm" onClick={() => setEditingId(null)}>
                        Cancel
                      </button>
                    </form>
                  ) : (
                    <>
                      <h2 className="font-semibold">{laptop.device_name}</h2>
                      <p className="text-sm text-secondary">
                        {[laptop.manufacturer, laptop.model].filter(Boolean).join(" ")}
                        {laptop.serial_last4 ? ` · Serial ····${laptop.serial_last4}` : ""}
                      </p>
                    </>
                  )}
                  <p className="mt-2 font-mono text-xs text-muted">{laptop.verification_code}</p>
                  <div className="mt-2 flex flex-wrap gap-2">
                    <span className={`badge ${statusBadge(laptop.verification_status)}`}>
                      {laptop.verification_status}
                    </span>
                    {laptop.last_scan_at && (
                      <span className="badge badge-neutral">
                        Last scan {new Date(laptop.last_scan_at).toLocaleDateString()}
                      </span>
                    )}
                  </div>
                </div>
                <div className="flex flex-wrap gap-2">
                  <Link href={`/c/${laptop.verification_code}`} className="btn btn-brand text-sm">
                    View Report
                  </Link>
                  <button
                    type="button"
                    className="btn btn-secondary text-sm"
                    onClick={() => {
                      const url = laptop.public_report_url.startsWith("http")
                        ? laptop.public_report_url
                        : `${env.siteUrl}${laptop.public_report_url}`;
                      void navigator.clipboard.writeText(url);
                    }}
                  >
                    Copy Verification Link
                  </button>
                  <Link href="/download" className="btn btn-secondary text-sm">
                    Download Scanner
                  </Link>
                  {editingId !== laptop.device_id && (
                    <button
                      type="button"
                      className="btn btn-secondary text-sm"
                      onClick={() => {
                        setEditingId(laptop.device_id);
                        setNicknameDraft(laptop.nickname || laptop.device_name);
                      }}
                    >
                      Rename
                    </button>
                  )}
                </div>
              </div>
              <button type="button" className="btn btn-secondary text-sm opacity-60" disabled title="Coming soon">
                List this laptop for sale
              </button>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
