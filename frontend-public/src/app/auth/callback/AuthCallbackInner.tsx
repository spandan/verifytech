"use client";

import { useEffect, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";

import { getSupabaseClient } from "@/lib/supabase";

export default function AuthCallbackInner() {
  const router = useRouter();
  const params = useSearchParams();
  const [error, setError] = useState("");

  useEffect(() => {
    const supabase = getSupabaseClient();
    if (!supabase) {
      setError("Authentication is not configured.");
      return;
    }

    const next = params.get("next") || "/my-laptops";

    supabase.auth.getSession().then(({ data: { session } }) => {
      if (session) {
        router.replace(next);
      }
    });

    const { data: subscription } = supabase.auth.onAuthStateChange((_event, session) => {
      if (session) {
        router.replace(next);
      }
    });

    return () => subscription.subscription.unsubscribe();
  }, [params, router]);

  return (
    <div className="page-container page-container--narrow py-20 text-center">
      <p className="text-secondary">{error || "Signing you in…"}</p>
    </div>
  );
}
