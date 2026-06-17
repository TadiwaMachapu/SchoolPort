-- Migration 002: Create Supabase Storage bucket for submissions
-- Run this in the Supabase SQL Editor

-- Create the submissions storage bucket
INSERT INTO storage.buckets (id, name, public, file_size_limit, allowed_mime_types)
VALUES (
  'submissions',
  'submissions',
  true,  -- public so download links work without additional auth
  52428800,  -- 50 MB limit
  ARRAY[
    'application/pdf',
    'application/msword',
    'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
    'application/vnd.ms-powerpoint',
    'application/vnd.openxmlformats-officedocument.presentationml.presentation',
    'application/vnd.ms-excel',
    'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    'text/plain',
    'text/markdown',
    'application/zip',
    'image/jpeg',
    'image/png',
    'image/gif',
    'video/mp4',
    'audio/mpeg'
  ]
)
ON CONFLICT (id) DO NOTHING;

-- Storage policy: authenticated users can upload to their own path
CREATE POLICY "Authenticated users can upload submissions"
ON storage.objects FOR INSERT
TO authenticated
WITH CHECK (bucket_id = 'submissions');

-- Storage policy: anyone can read public submissions
CREATE POLICY "Public can read submissions"
ON storage.objects FOR SELECT
TO public
USING (bucket_id = 'submissions');

-- Storage policy: users can delete their own submissions
CREATE POLICY "Users can delete own submissions"
ON storage.objects FOR DELETE
TO authenticated
USING (bucket_id = 'submissions' AND auth.uid()::text = (storage.foldername(name))[3]);
