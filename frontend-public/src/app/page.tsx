import { Button } from "@/components/Button";
import { OSDetector } from "@/components/OSDetector";

const STEPS = [
  {
    step: "1",
    title: "Scan device",
    desc: "Download the agent and run a quick diagnostic on the device you want to certify.",
  },
  {
    step: "2",
    title: "Generate certificate",
    desc: "Receive a shareable certificate with identity, condition grade, and test results.",
  },
  {
    step: "3",
    title: "Buyer verifies later",
    desc: "Buyers enter your certificate code and confirm the device matches before they buy.",
  },
];

export default function HomePage() {
  return (
    <>
      <section className="page-container pb-8 pt-16 md:pt-20">
        <div className="mx-auto max-w-2xl text-center">
          <OSDetector />
          <h1 className="mt-6 text-3xl font-semibold tracking-tight md:text-4xl">
            Certify and verify used devices with confidence
          </h1>
          <p className="page-subtitle mx-auto mt-4 max-w-lg">
            A trusted passport for laptops and electronics — share proof of identity and
            condition, and let buyers verify before they purchase.
          </p>

          <div className="hero-trust-badge mx-auto mt-8">
            <span>Hardware identity</span>
            <span className="text-muted">•</span>
            <span>Condition checks</span>
            <span className="text-muted">•</span>
            <span>Buyer verification</span>
          </div>

          <div className="mt-10 flex flex-wrap items-center justify-center gap-3">
            <Button href="/start" variant="trust">
              Start certification
            </Button>
            <Button href="/verify" variant="secondary">
              Verify a certificate
            </Button>
          </div>
        </div>
      </section>

      <section className="page-container pt-4 pb-20">
        <h2 className="mb-8 text-center text-sm font-medium uppercase tracking-wide text-muted">
          How it works
        </h2>
        <div className="grid gap-6 md:grid-cols-3">
          {STEPS.map((item) => (
            <div key={item.step} className="step-card">
              <div className="step-card__number">{item.step}</div>
              <h3 className="step-card__title">{item.title}</h3>
              <p className="step-card__desc">{item.desc}</p>
            </div>
          ))}
        </div>
      </section>
    </>
  );
}
