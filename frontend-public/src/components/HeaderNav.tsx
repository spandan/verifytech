"use client";

import Link from "next/link";

import { useAuth } from "@/lib/auth-context";

export function HeaderNav() {
  const { userId, loading, signOut } = useAuth();

  return (
    <nav className="site-nav flex items-center gap-6">
      <Link href="/start">Scan this laptop</Link>
      <Link href="/verify">Verify</Link>
      {!loading && userId ? (
        <>
          <Link href="/my-laptops">My Laptops</Link>
          <button type="button" className="text-sm text-secondary hover:text-[var(--color-text-primary)]" onClick={() => void signOut()}>
            Sign out
          </button>
        </>
      ) : (
        <Link href="/login">Sign in to save reports</Link>
      )}
    </nav>
  );
}
