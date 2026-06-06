"use client";

import { useEffect, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";

import { getSupabaseClient } from "@/lib/supabase";

export default function AuthCallbackInner() {
  const router = useRouter();
  const params = useSearchParams();
  const [error, setError] = useState("");

  useEffect(() => {
    let cancelled = false;

    async function finishSignIn() {
      const supabase = getSupabaseClient();
      if (!supabase) {
        setError("Authentication is not configured.");
        return;
      }

      const next = params.get("next") || "/my-laptops";
      const code = params.get("code");

      try {
        if (code) {
          const { error: exchangeError } = await supabase.auth.exchangeCodeForSession(code);
          if (exchangeError) {
            if (!cancelled) setError(exchangeError.message);
            return;
          }
        } else {
          const { data, error: sessionError } = await supabase.auth.getSession();
          if (sessionError) {
            if (!cancelled) setError(sessionError.message);
            return;
          }
          if (!data.session) {
            return;
          }
        }

        if (!cancelled) router.replace(next);
      } catch (err) {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : "Sign-in failed");
        }
      }
    }

    void finishSignIn();

    const supabase = getSupabaseClient();
    if (!supabase) return;

    const next = params.get("next") || "/my-laptops";
    const { data: subscription } = supabase.auth.onAuthStateChange((_event, session) => {
      if (session && !cancelled) {
        router.replace(next);
      }
    });

    return () => {
      cancelled = true;
      subscription.subscription.unsubscribe();
    };
  }, [params, router]);

  return (
    <div className="page-container page-container--narrow py-20 text-center">
      <p className="text-secondary">{error || "Signing you in…"}</p>
    </div>
  );
}
