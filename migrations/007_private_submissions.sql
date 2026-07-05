-- Migration 007: Make the submissions storage bucket private (Sprint 1.5.0.6, POPIA)
-- Run this in the Supabase SQL Editor
--
-- Student work submissions are personal data belonging to minors. The bucket was
-- created public in migration 002 ("so download links work without additional auth"),
-- which means anyone with the exact GUID-path URL could read a submission file.
-- After this migration:
--   * the bucket is private — unsigned /object/public/... URLs return 400/403
--   * reads require a short-lived signed URL minted server-side by StorageService
--     (service role key; 1 hour expiry) — see StorageService.GetSignedUrlAsync
--   * uploads are unaffected (the backend uploads with the service role key,
--     which bypasses storage RLS policies)

UPDATE storage.buckets SET public = false WHERE id = 'submissions';

-- Drop the public read policy from migration 002. The remaining INSERT/DELETE
-- policies for "authenticated" are inert (nothing uses the Data API / anon key),
-- but they are harmless; only the public SELECT is the POPIA hole.
DROP POLICY IF EXISTS "Public can read submissions" ON storage.objects;
