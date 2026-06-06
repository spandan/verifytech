"use client";

import Link from "next/link";

import { useAuth } from "@/lib/auth-context";

export function HeaderNav() {
  const { userId, loading, signOut } = useAuth();
  const signedIn = !loading && Boolean(userId);

  return (
    <>
      <nav className="site-nav hidden items-center gap-5 lg:flex">
        <Link href="/download">Certify Device</Link>
        <Link href="/verify">Verify Device</Link>
        <Link href="/sample-report">Sample Report</Link>
        {signedIn ? (
          <>
            <Link href="/my-laptops">My Devices</Link>
            <button type="button" onClick={() => void signOut()}>
              Sign out
            </button>
          </>
        ) : !loading ? (
          <Link href="/login">Sign In</Link>
        ) : null}
      </nav>
      <div className="site-header__actions">
        {signedIn ? (
          <>
            <Link href="/my-laptops" className="btn btn-ghost hidden sm:inline-flex lg:hidden">
              My Devices
            </Link>
            <button
              type="button"
              className="btn btn-ghost hidden sm:inline-flex lg:hidden"
              onClick={() => void signOut()}
            >
              Sign out
            </button>
          </>
        ) : !loading ? (
          <Link href="/login" className="btn btn-ghost hidden sm:inline-flex lg:hidden">
            Sign In
          </Link>
        ) : null}
        <Link href="/download" className="btn btn-brand">
          Get Started
        </Link>
      </div>
    </>
  );
}
