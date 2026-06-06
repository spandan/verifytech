"use client";

import Link from "next/link";

import { useAuth } from "@/lib/auth-context";

export function FooterAuthLink() {
  const { userId, loading } = useAuth();

  if (loading) return null;

  if (userId) {
    return <Link href="/my-laptops">My Devices</Link>;
  }

  return <Link href="/login">Sign In</Link>;
}
