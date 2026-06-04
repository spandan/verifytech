import Link from "next/link";
import { getDashboard } from "@/lib/api";
import { env } from "@/lib/env";

const DEMO_USER_ID = "demo-user-00000000-0000-0000-0000-000000000001";

const LEVEL_LABELS: Record<string, string> = {
  identity_verified: "Identity Verified",
  condition_certified: "Condition Certified",
  enhanced_certified: "Enhanced Certified",
};

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

export default async function DashboardPage() {
  let data;
  try {
    data = await getDashboard(DEMO_USER_ID);
  } catch {
    data = { user_id: DEMO_USER_ID, certificates: [], verification_count: 0 };
  }

  const activeCount = data.certificates.filter((c) => c.status === "active").length;
  const expiringSoon = data.certificates.filter((c) => {
    const days = (new Date(c.expires_at).getTime() - Date.now()) / (1000 * 60 * 60 * 24);
    return c.status === "active" && days <= 30 && days > 0;
  }).length;

  return (
    <div className="page-container">
      <header className="mb-10">
        <h1 className="page-title">Your devices</h1>
        <p className="page-subtitle">Certificates you&apos;ve created and their verification history.</p>
      </header>

      <div className="mb-10 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <StatCard label="Certified devices" value={data.certificates.length} />
        <StatCard label="Active certificates" value={activeCount} />
        <StatCard label="Verification attempts" value={data.verification_count} />
        <StatCard label="Expiring soon" value={expiringSoon} hint="Within 30 days" />
      </div>

      {data.certificates.length === 0 ? (
        <div className="empty-state">
          <p className="text-secondary">No certified devices yet.</p>
          <Link
            href={`${env.publicSiteUrl}/start`}
            className="btn btn-trust mt-6"
          >
            Certify a device
          </Link>
        </div>
      ) : (
        <div className="space-y-4">
          {data.certificates.map((cert) => (
            <div
              key={cert.id}
              className="card card-body flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between"
            >
              <div>
                <h2 className="font-semibold">{cert.device_name}</h2>
                <p className="mt-1 font-mono text-xs text-muted">{cert.certificate_code}</p>
                <div className="mt-3 flex flex-wrap gap-2">
                  <span className="badge badge-trust">
                    {LEVEL_LABELS[cert.certificate_level] || cert.certificate_level}
                  </span>
                  <span className={`badge ${statusBadge(cert.status)}`}>{cert.status}</span>
                  {cert.condition_grade && (
                    <span className="badge badge-info">Grade {cert.condition_grade}</span>
                  )}
                </div>
              </div>
              <Link
                href={`${env.publicSiteUrl}/c/${cert.certificate_code}`}
                className="btn btn-secondary shrink-0"
              >
                View certificate
              </Link>
            </div>
          ))}
        </div>
      )}

      <section className="card card-body mt-16">
        <h2 className="font-semibold">Refurbisher tools</h2>
        <p className="mt-1 text-sm text-secondary">
          Team management, inventory, and bulk certification — coming soon.
        </p>
        <ul className="mt-4 grid gap-2 text-sm text-secondary sm:grid-cols-2">
          {[
            "Device inventory",
            "Bulk certification",
            "Team users & locations",
            "Verification history",
            "Pricing analytics",
          ].map((f) => (
            <li key={f} className="flex items-center gap-2">
              <span className="text-[var(--color-trust)]">·</span> {f}
            </li>
          ))}
        </ul>
      </section>
    </div>
  );
}

function StatCard({
  label,
  value,
  hint,
}: {
  label: string;
  value: number;
  hint?: string;
}) {
  return (
    <div className="stat-card">
      <p className="stat-card__label">{label}</p>
      <p className="stat-card__value">{value}</p>
      {hint && <p className="mt-1 text-xs text-muted">{hint}</p>}
    </div>
  );
}
