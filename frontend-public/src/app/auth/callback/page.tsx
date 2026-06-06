"use client";

import { Suspense } from "react";

import AuthCallbackInner from "./AuthCallbackInner";

export default function AuthCallbackPage() {
  return (
    <Suspense fallback={<div className="page-container py-20 text-center text-secondary">Loading…</div>}>
      <AuthCallbackInner />
    </Suspense>
  );
}
