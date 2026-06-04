import Link from "next/link";

export function Header() {
  return (
    <header className="site-header">
      <div className="site-header__inner">
        <Link href="/" className="site-logo">
          <span className="site-logo__mark">DP</span>
          <span>DevicePassport</span>
        </Link>
        <nav className="site-nav flex items-center gap-6">
          <Link href="/start">Certify device</Link>
          <Link href="/verify">Verify</Link>
        </nav>
      </div>
    </header>
  );
}

export function Footer() {
  return (
    <footer className="site-footer">
      <div className="site-footer__inner">
        DevicePassport — Trusted certification for used electronics
      </div>
    </footer>
  );
}
