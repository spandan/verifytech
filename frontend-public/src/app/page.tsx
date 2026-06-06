import { Button } from "@/components/Button";

const STEPS = [
  {
    step: "1",
    title: "Scan this laptop",
    desc: "Download the scanner and run a quick diagnostic — no account required.",
  },
  {
    step: "2",
    title: "Get your report",
    desc: "Receive a verification code and shareable report link when the scan completes.",
  },
  {
    step: "3",
    title: "Save to your account",
    desc: "Sign in with email to keep laptops and reports in one place — optional.",
  },
];

export default function HomePage() {
  return (
    <>
      <section className="page-container pb-8 pt-16 md:pt-20">
        <div className="mx-auto max-w-2xl text-center">
          <h1 className="text-3xl font-semibold tracking-tight md:text-4xl">
            Scan, verify, and save your laptop reports
          </h1>
          <p className="page-subtitle mx-auto mt-4 max-w-lg">
            Run a quick hardware scan with no account required. Sign in anytime to save scanned
            laptops to your account and access reports without carrying verification codes.
          </p>

          <div className="mt-10 flex flex-wrap items-center justify-center gap-3">
            <Button href="/start" variant="trust">
              Scan this laptop
            </Button>
            <Button href="/login" variant="secondary">
              Sign in to save reports
            </Button>
          </div>

          <p className="mt-4 text-sm text-secondary">
            Save scanned laptops to your account and access reports anytime.
          </p>
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
