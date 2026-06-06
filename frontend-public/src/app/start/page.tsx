"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { api } from "@/lib/api";

const CATEGORIES = [
  { value: "personal", label: "Personal use" },
  { value: "business", label: "Business / work" },
  { value: "school", label: "School / education" },
  { value: "refurbished", label: "Refurbished / resale" },
  { value: "unknown", label: "Not sure" },
];

const CHARGER_OPTIONS = [
  { value: "yes", label: "Yes" },
  { value: "no", label: "No" },
  { value: "not_sure", label: "Not sure" },
];

export default function StartPage() {
  const router = useRouter();
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [form, setForm] = useState({
    device_category: "personal",
    approximate_purchase_year: new Date().getFullYear() - 2,
    zip_code_or_region: "",
    charger_included: "",
  });

  const currentYear = new Date().getFullYear();
  const years = Array.from({ length: 25 }, (_, i) => currentYear - i);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError("");
    if (!form.zip_code_or_region.trim()) {
      setError("Please enter your zip code or region.");
      return;
    }
    setLoading(true);
    try {
      const result = await api.submitIntake({
        device_category: form.device_category,
        approximate_purchase_year: form.approximate_purchase_year,
        zip_code_or_region: form.zip_code_or_region.trim(),
        charger_included: form.charger_included || undefined,
      });
      sessionStorage.setItem("intake_id", result.id);
      router.push("/download");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Something went wrong");
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="page-container page-container--narrow">
      <header className="mb-10 text-center">
        <h1 className="page-title">Before you download</h1>
        <p className="page-subtitle">Three quick questions — takes under a minute.</p>
      </header>

      <form onSubmit={handleSubmit} className="card card-body space-y-6">
        <Field label="What will you use this device for?" required>
          <select
            value={form.device_category}
            onChange={(e) => setForm({ ...form, device_category: e.target.value })}
            className="select"
          >
            {CATEGORIES.map((c) => (
              <option key={c.value} value={c.value}>
                {c.label}
              </option>
            ))}
          </select>
        </Field>

        <Field label="Approximate purchase year" required>
          <select
            value={form.approximate_purchase_year}
            onChange={(e) =>
              setForm({ ...form, approximate_purchase_year: parseInt(e.target.value) })
            }
            className="select"
          >
            {years.map((y) => (
              <option key={y} value={y}>
                {y}
              </option>
            ))}
          </select>
        </Field>

        <Field label="Zip code or region" required>
          <input
            type="text"
            placeholder="e.g. 94102 or Bay Area"
            value={form.zip_code_or_region}
            onChange={(e) => setForm({ ...form, zip_code_or_region: e.target.value })}
            className="input"
            maxLength={20}
          />
        </Field>

        <Field label="Original charger included?">
          <div className="flex flex-wrap gap-4">
            {CHARGER_OPTIONS.map((o) => (
              <label key={o.value} className="flex cursor-pointer items-center gap-2 text-sm">
                <input
                  type="radio"
                  name="charger"
                  value={o.value}
                  checked={form.charger_included === o.value}
                  onChange={() => setForm({ ...form, charger_included: o.value })}
                />
                <span className="text-secondary">{o.label}</span>
              </label>
            ))}
          </div>
        </Field>

        {error && <p className="alert alert-error">{error}</p>}

        <button type="submit" disabled={loading} className="btn btn-trust btn-block">
          {loading ? "Saving…" : "Continue to download"}
        </button>

        <p className="text-center text-sm text-muted">
          Want to save reports later?{" "}
          <a href="/login?next=/download" className="text-[var(--color-trust)] hover:underline">
            Sign in to save reports
          </a>
        </p>
      </form>
    </div>
  );
}

function Field({
  label,
  required,
  children,
}: {
  label: string;
  required?: boolean;
  children: React.ReactNode;
}) {
  return (
    <div>
      <label className="label">
        {label}
        {required && <span className="label-required">*</span>}
      </label>
      {children}
    </div>
  );
}
