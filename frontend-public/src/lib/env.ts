/** Public env vars (embedded at build time). See .env.example */

const DEFAULT_API_URL = "http://localhost:8000";
const DEFAULT_SITE_URL = "http://localhost:3000";

export const env = {
  apiUrl: process.env.NEXT_PUBLIC_API_URL?.replace(/\/$/, "") || DEFAULT_API_URL,
  siteUrl: process.env.NEXT_PUBLIC_SITE_URL?.replace(/\/$/, "") || DEFAULT_SITE_URL,
};
