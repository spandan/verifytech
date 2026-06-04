import Link from "next/link";
import { env } from "@/lib/env";

export function DashboardHeader() {
  return (
    <header className="site-header">
      <div className="site-header__inner">
        <Link href="/" className="site-logo">
          <span className="site-logo__mark">DP</span>
          <span>DevicePassport</span>
        </Link>
        <nav className="site-nav flex items-center gap-6">
          <Link href="/">Dashboard</Link>
          <a
            href={env.publicSiteUrl}
            className="text-muted"
          >
            Public site →
          </a>
        </nav>
      </div>
    </header>
  );
}
