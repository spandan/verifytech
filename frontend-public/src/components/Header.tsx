import Link from "next/link";

import { HeaderNav } from "@/components/HeaderNav";

export function Header() {
  return (
    <header className="site-header">
      <div className="site-header__inner">
        <Link href="/" className="site-logo">
          <span className="site-logo__mark">CX</span>
          <span className="site-logo__text">
            <span className="site-logo__name">Certronx</span>
            <span className="site-logo__tagline">Certify. Verify. Rehome.</span>
          </span>
        </Link>
        <HeaderNav />
      </div>
    </header>
  );
}

export function Footer() {
  return (
    <footer className="site-footer">
      <div className="site-footer__inner">
        <div className="site-footer__grid">
          <div className="site-footer__brand">
            <p className="site-footer__name">Certronx</p>
            <p className="site-footer__tagline">
              The trust platform for used electronics. Certify. Verify. Rehome.
            </p>
          </div>
          <div>
            <p className="mb-3 text-sm font-medium">Product</p>
            <div className="site-footer__links">
              <Link href="/download">Certify Device</Link>
              <Link href="/verify">Verify Device</Link>
              <Link href="/sample-report">Sample Report</Link>
              <Link href="/my-laptops">My Devices</Link>
            </div>
          </div>
          <div>
            <p className="mb-3 text-sm font-medium">Company</p>
            <div className="site-footer__links">
              <Link href="/login">Sign In</Link>
              <Link href="/">About Certronx</Link>
            </div>
          </div>
        </div>
        <div className="site-footer__bottom">
          © {new Date().getFullYear()} Certronx. Trusted devices. Verified history.
        </div>
      </div>
    </footer>
  );
}
