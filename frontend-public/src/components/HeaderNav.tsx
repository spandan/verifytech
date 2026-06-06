"use client";

import Link from "next/link";

import { useAuth } from "@/lib/auth-context";

function AuthControls({ className }: { className?: string }) {
  const { userId, loading, signOut } = useAuth();

  if (loading) return null;

  if (userId) {
    return (
      <div className={className}>
        <Link href="/my-laptops">My Devices</Link>
        <button type="button" onClick={() => void signOut()}>
          Sign out
        </button>
      </div>
    );
  }

  return (
    <div className={className}>
      <Link href="/login">Sign In</Link>
    </div>
  );
}

export function HeaderNav() {
  return (
    <>
      <nav className="site-nav hidden items-center gap-5 lg:flex">
        <Link href="/download">Certify Device</Link>
        <Link href="/verify">Verify Device</Link>
        <Link href="/sample-report">Sample Report</Link>
        <AuthControls className="flex items-center gap-5" />
      </nav>
      <div className="site-header__actions">
        <AuthControls className="site-nav hidden items-center gap-3 sm:flex lg:hidden" />
        <Link href="/download" className="btn btn-brand">
          Get Started
        </Link>
      </div>
    </>
  );
}
