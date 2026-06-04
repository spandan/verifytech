from functools import lru_cache

from supabase import Client, create_client

from app.config import settings


@lru_cache
def get_supabase_admin() -> Client:
    """Service-role client for backend persistence (bypasses RLS)."""
    return create_client(settings.supabase_url, settings.supabase_service_role_key)


@lru_cache
def get_supabase_anon() -> Client:
    """Anon client for user-scoped operations when needed."""
    return create_client(settings.supabase_url, settings.supabase_anon_key)
