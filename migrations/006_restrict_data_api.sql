-- 006_restrict_data_api.sql
-- Sprint 1.5.0 Step 11 — close the Supabase Data API (PostgREST, /rest/v1) for the
-- application schema. Applied to the live project (School Management) on 2026-06-19 via
-- the Supabase migrations `restrict_data_api` + `restrict_data_api_revoke_public_execute`.
--
-- WHY: the project had every table RLS-enabled-but-policyless (default-deny for the API
-- roles) yet five aggregate views/matviews — vw_subject_term_averages, vw_matric_aps_summary,
-- vw_school_performance_summary (matviews; RLS on base tables does NOT protect these) and
-- vw_attendance_summary, vw_gradebook_simple (SECURITY DEFINER views; run as the owner) —
-- granted full SELECT to `anon`/`authenticated`. They were therefore readable over /rest/v1/
-- with the public anon key, leaking cross-school learner APS/performance data and bypassing
-- the entire application permission model (a POPIA-relevant exposure for minors' data).
--
-- SAFE BECAUSE: nothing in the codebase uses the Data API — the Next.js frontend has no
-- supabase-js client and the .NET backend uses Npgsql/EF for data and the Storage API
-- (/storage/v1, service-role key) for files. The app connects as the `postgres` role
-- (rolbypassrls = true), so none of the revokes below affect application access.
--
-- VERIFIED: GET /rest/v1/vw_matric_aps_summary with the anon key returned HTTP 200 + rows
-- before, and HTTP 401 "permission denied for materialized view" after. Supabase security
-- advisors: the 2 SECURITY DEFINER view ERRORs, 3 matview-in-API WARNs, the public-bucket
-- listing WARN and the 2 rls_auto_enable DEFINER-function WARNs all cleared.
--
-- NOTE: this DDL targets Supabase-platform roles (anon/authenticated) that do not exist in
-- the EF model or the test database, so it lives here as a Supabase-side migration rather
-- than an EF migration.
--
-- ROLLBACK: re-GRANT the privileges; ALTER VIEW ... SET (security_invoker = false);
--           GRANT EXECUTE ON FUNCTION public.rls_auto_enable() TO public;
--           CREATE POLICY "Public can read submissions" ON storage.objects
--             FOR SELECT TO public USING (bucket_id = 'submissions');

-- 1) Strip every privilege the PostgREST API roles hold in public.
revoke all on all tables    in schema public from anon, authenticated;
revoke all on all sequences in schema public from anon, authenticated;
revoke all on all functions in schema public from anon, authenticated;

-- 2) Remove schema usage entirely -> PostgREST returns 404 for public objects.
revoke usage on schema public from anon, authenticated;

-- 3) Stop future objects (EF migrations run as postgres) from re-granting to the API roles.
alter default privileges for role postgres in schema public revoke all on tables    from anon, authenticated;
alter default privileges for role postgres in schema public revoke all on sequences from anon, authenticated;
alter default privileges for role postgres in schema public revoke all on functions from anon, authenticated;

-- 4) Defense-in-depth: the two SECURITY DEFINER views honor the caller, not the owner.
alter view public.vw_attendance_summary set (security_invoker = true);
alter view public.vw_gradebook_simple   set (security_invoker = true);

-- 5) Storage: drop the broad listing policy on the public `submissions` bucket. App uploads
--    (service-role) and public object-URL downloads still work; only anon LISTING is removed.
drop policy if exists "Public can read submissions" on storage.objects;

-- 6) The SECURITY DEFINER helper rls_auto_enable() still had EXECUTE granted to PUBLIC
--    (which anon/authenticated inherit); revoke it so it is not callable over /rest/v1/rpc.
revoke execute on function public.rls_auto_enable() from public, anon, authenticated;
