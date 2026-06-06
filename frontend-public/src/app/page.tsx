import { Button } from "@/components/Button";
import { SampleReportPreview } from "@/components/SampleReportPreview";

const TRUST_BADGES = [
  "Device Identity Verified",
  "Battery Health Checked",
  "Hardware Validation",
  "Public Verification",
  "Tamper Detection",
];

const HOW_IT_WORKS = [
  {
    icon: "⬇",
    title: "Verify Device",
    desc: "Download the Certronx Scanner and inspect device condition.",
  },
  {
    icon: "📋",
    title: "Create Trusted Report",
    desc: "Generate a shareable certification report with hardware and condition details.",
  },
  {
    icon: "🔗",
    title: "Share With Buyers",
    desc: "Buyers verify the report before purchasing.",
  },
];

const TRUST_CARDS = [
  {
    title: "Verified Identity",
    desc: "Confirms device model and hardware configuration.",
  },
  {
    title: "Condition Insights",
    desc: "Highlights battery, storage, and key health indicators.",
  },
  {
    title: "Independent Verification",
    desc: "Buyers can verify reports directly from Certronx.",
  },
  {
    title: "Fraud Reduction",
    desc: "Makes hardware swaps and misrepresentation easier to detect.",
  },
];

const ROADMAP = [
  "Device Certification",
  "Ownership History",
  "Transfer of Ownership",
  "Refurbisher Programs",
  "Marketplace Integrations",
];

export default function HomePage() {
  return (
    <>
      {/* Hero */}
      <section className="hero">
        <div className="page-container page-container--hero">
          <div className="grid items-center gap-12 lg:grid-cols-2">
            <div>
              <p className="text-sm font-medium text-[var(--color-brand)]">Certify. Verify. Rehome.</p>
              <h1 className="hero__headline mt-4">Trusted Devices. Verified History.</h1>
              <p className="hero__subheadline">
                Generate trusted certification reports for laptops, phones, tablets, and electronics.
                Help buyers verify condition before they purchase.
              </p>
              <div className="mt-8 flex flex-wrap gap-3">
                <Button href="/download" variant="brand">
                  Certify a Device
                </Button>
                <Button href="/verify" variant="secondary">
                  Verify a Device
                </Button>
              </div>
              <div className="trust-badge-row">
                {TRUST_BADGES.map((badge) => (
                  <span key={badge} className="trust-badge">
                    <span className="trust-badge__check">✓</span>
                    {badge}
                  </span>
                ))}
              </div>
            </div>
            <SampleReportPreview showCta={false} compact />
          </div>
        </div>
      </section>

      {/* Problem */}
      <section className="page-container">
        <h2 className="section-title">The Used Electronics Market Has a Trust Problem</h2>
        <p className="section-lead">
          Millions of perfectly usable devices go unsold because buyers don&apos;t know what condition
          they&apos;re in.
        </p>
        <ul className="problem-list">
          <li>Is the battery healthy?</li>
          <li>Does everything work?</li>
          <li>Are the specifications accurate?</li>
          <li>Can I trust the seller?</li>
        </ul>
        <p className="mt-8 text-lg text-secondary">
          Certronx helps answer those questions before money changes hands.
        </p>
      </section>

      {/* How it works */}
      <section className="section-subtle">
        <div className="page-container">
          <h2 className="section-title text-center">How Certronx Works</h2>
          <p className="section-lead mx-auto text-center">
            A simple trust workflow for sellers and buyers — no technical expertise required.
          </p>
          <div className="feature-grid feature-grid--3 mt-12">
            {HOW_IT_WORKS.map((item) => (
              <div key={item.title} className="feature-card">
                <div className="feature-card__icon">{item.icon}</div>
                <h3 className="feature-card__title">{item.title}</h3>
                <p className="feature-card__desc">{item.desc}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* Sample report */}
      <section className="page-container">
        <div className="grid items-center gap-12 lg:grid-cols-2">
          <div>
            <h2 className="section-title">What Buyers See</h2>
            <p className="section-lead">
              Every certification includes a polished report with identity, condition, and verification
              details buyers can trust at a glance.
            </p>
            <div className="mt-8">
              <Button href="/sample-report" variant="brand">
                View Sample Report
              </Button>
            </div>
          </div>
          <SampleReportPreview showCta={false} />
        </div>
      </section>

      {/* Trust */}
      <section className="section-subtle">
        <div className="page-container">
          <h2 className="section-title text-center">Why Buyers Trust Certronx</h2>
          <div className="feature-grid feature-grid--4 mt-12">
            {TRUST_CARDS.map((card) => (
              <div key={card.title} className="feature-card">
                <h3 className="feature-card__title">{card.title}</h3>
                <p className="feature-card__desc">{card.desc}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* Future vision */}
      <section className="page-container">
        <h2 className="section-title">More Than Certification</h2>
        <p className="section-lead">
          Certronx is building the trust layer for electronics ownership.
        </p>
        <div className="mt-10 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {ROADMAP.map((item, i) => (
            <div key={item} className="roadmap-card">
              <p className="roadmap-card__label">{i === 0 ? "Available now" : "Roadmap"}</p>
              <p className="roadmap-card__title">{item}</p>
            </div>
          ))}
        </div>
      </section>

      {/* Sustainability */}
      <section className="section-subtle">
        <div className="page-container text-center">
          <h2 className="section-title">Give Tech a New Home</h2>
          <p className="section-lead mx-auto">
            Every year millions of devices remain unused despite being fully functional. Certronx helps
            extend device lifecycles by making electronics easier to trust, buy, and sell.
          </p>
        </div>
      </section>

      {/* Final CTA */}
      <section className="page-container pb-24">
        <div className="cta-band">
          <h2 className="section-title">Ready to Certify a Device?</h2>
          <p className="page-subtitle mt-4">
            Generate a trusted device report in minutes.
          </p>
          <div className="mt-8 flex flex-wrap items-center justify-center gap-3">
            <Button href="/download" variant="brand">
              Certify Device
            </Button>
            <Button href="/verify" variant="secondary">
              Verify Device
            </Button>
          </div>
        </div>
      </section>
    </>
  );
}
