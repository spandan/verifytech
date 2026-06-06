import { createClient, type SupabaseClient } from "@supabase/supabase-js";

import { env, supabaseConfigured } from "@/lib/env";

let client: SupabaseClient | null = null;

export function getSupabaseClient(): SupabaseClient | null {
  if (!supabaseConfigured) {
    return null;
  }
  if (!client) {
    client = createClient(env.supabaseUrl, env.supabaseAnonKey, {
      auth: {
        flowType: "pkce",
        detectSessionInUrl: true,
      },
    });
  }
  return client;
}

export async function getAccessToken(): Promise<string | null> {
  const supabase = getSupabaseClient();
  if (!supabase) return null;
  const { data } = await supabase.auth.getSession();
  return data.session?.access_token ?? null;
}
