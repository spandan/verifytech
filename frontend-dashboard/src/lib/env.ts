/** Public env vars (embedded at build time). See .env.example */

const DEFAULT_API_URL = "http://localhost:8000";
const DEFAULT_PUBLIC_SITE_URL = "http://localhost:3000";

export const env = {
  apiUrl: process.env.NEXT_PUBLIC_API_URL?.replace(/\/$/, "") || DEFAULT_API_URL,
  publicSiteUrl:
    process.env.NEXT_PUBLIC_PUBLIC_SITE_URL?.replace(/\/$/, "") || DEFAULT_PUBLIC_SITE_URL,
  supabaseUrl: process.env.NEXT_PUBLIC_SUPABASE_URL?.replace(/\/$/, "") || "",
  supabaseAnonKey: process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY || "",
};

export const supabaseConfigured = Boolean(env.supabaseUrl && env.supabaseAnonKey);
