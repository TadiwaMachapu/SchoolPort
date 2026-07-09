# Sprint 1.5.0.6 — Private Submissions Bucket (POPIA Fix)

---
sprint: 1.5.0.6
status: complete
completed: 2026-07-05
pr: #7 merged (97450835)
---

## Goal
Fix a POPIA violation: student work files were stored in a public Supabase bucket, accessible by URL to anyone. Pre-pilot blocker.

## What was built
- Supabase `submissions` bucket changed from public to private (migration 007)
- `IStorageService.GetSignedUrlAsync()` and `GetSignedUrlsAsync()` — mint 1-hour signed URLs using service role key
- `StorageService.ExtractObjectPath()` — normalises legacy public URLs and new bucket-relative paths. Signing failures → null in DTO, never a raw path, never a 500
- `Submission.FileUrl` now stores bucket-relative object path (not full public URL)
- 15 new StorageServiceTests

## Verification
- Unsigned `/object/public/submissions/...` returns `400 Bucket not found`
- Verified live via Supabase MCP: `storage.buckets.public = false`
- Two policies remain (both correct): upload policy + delete-own policy. No SELECT policy for anon.

## Important operational note
`Supabase:ServiceRoleKey` must be configured in the deployed environment via user secrets or environment variables. The `appsettings.json` placeholder (`CHANGE_ME`) must never be used in production — uploads and signed URL minting both fail without the real key.

## Test count
Before: 185 | After: 188 (185 + 3 QUERY smoke tests from .NET 10 upgrade + nothing from this sprint — StorageServiceTests were part of this sprint commit)
Correction: 185 → 188 = net10 upgrade (3 QUERY tests). This sprint's 15 StorageServiceTests brought local count to 185 before the net10 upgrade.

## Related
- [[Sprint 1.5.0 — Security Layer]]
- [[RLS — App Layer Only]]
