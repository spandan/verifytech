"use client";

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";

import { getSupabaseClient } from "@/lib/supabase";

type AuthState = {
  userId: string | null;
  email: string | null;
  loading: boolean;
  signOut: () => Promise<void>;
  refresh: () => Promise<void>;
};

const AuthContext = createContext<AuthState | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [userId, setUserId] = useState<string | null>(null);
  const [email, setEmail] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const refresh = useCallback(async () => {
    const supabase = getSupabaseClient();
    if (!supabase) {
      setUserId(null);
      setEmail(null);
      setLoading(false);
      return;
    }
    const { data } = await supabase.auth.getSession();
    setUserId(data.session?.user.id ?? null);
    setEmail(data.session?.user.email ?? null);
    setLoading(false);
  }, []);

  useEffect(() => {
    void refresh();
    const supabase = getSupabaseClient();
    if (!supabase) return;
    const { data: subscription } = supabase.auth.onAuthStateChange(() => {
      void refresh();
    });
    return () => subscription.subscription.unsubscribe();
  }, [refresh]);

  const signOut = useCallback(async () => {
    const supabase = getSupabaseClient();
    if (supabase) {
      await supabase.auth.signOut();
    }
    setUserId(null);
    setEmail(null);
  }, []);

  const value = useMemo(
    () => ({ userId, email, loading, signOut, refresh }),
    [userId, email, loading, signOut, refresh],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) {
    throw new Error("useAuth must be used within AuthProvider");
  }
  return ctx;
}
