import { createClient, type SupabaseClient } from "@supabase/supabase-js";

import { env, supabaseConfigured } from "@/lib/env";

let client: SupabaseClient | null = null;

/** Browser Supabase client (anon key only). Used for Auth when enabled. */
export function getSupabaseClient(): SupabaseClient | null {
  if (!supabaseConfigured) {
    return null;
  }
  if (!client) {
    client = createClient(env.supabaseUrl, env.supabaseAnonKey);
  }
  return client;
}
