-- Migration 001: Add features & theme columns to schools
-- Run this in the Supabase SQL Editor

ALTER TABLE schools
ADD COLUMN IF NOT EXISTS features jsonb NOT NULL DEFAULT '{
  "quizzes": true,
  "attendance": true,
  "parentPortal": true,
  "messaging": true,
  "courses": true,
  "analytics": true,
  "aiGrading": false,
  "plagiarismDetection": false,
  "sso": false,
  "customReports": false,
  "whiteLabel": false,
  "pluginApi": false
}'::jsonb;

ALTER TABLE schools
ADD COLUMN IF NOT EXISTS theme jsonb NOT NULL DEFAULT '{
  "primaryColor": "#1E40AF",
  "logoUrl": null,
  "faviconUrl": null,
  "fontFamily": "Inter",
  "customDomain": null,
  "welcomeMessage": null,
  "supportEmail": null
}'::jsonb;

-- Backfill existing schools with defaults (already covered by DEFAULT, but be explicit)
UPDATE schools
SET
  features = COALESCE(features, '{
    "quizzes": true,
    "attendance": true,
    "parentPortal": true,
    "messaging": true,
    "courses": true,
    "analytics": true,
    "aiGrading": false,
    "plagiarismDetection": false,
    "sso": false,
    "customReports": false,
    "whiteLabel": false,
    "pluginApi": false
  }'::jsonb),
  theme = COALESCE(theme, '{
    "primaryColor": "#1E40AF",
    "logoUrl": null,
    "faviconUrl": null,
    "fontFamily": "Inter",
    "customDomain": null,
    "welcomeMessage": null,
    "supportEmail": null
  }'::jsonb)
WHERE features IS NULL OR theme IS NULL;

-- Update the Demo High School theme to match branding
UPDATE schools
SET theme = jsonb_set(theme, '{primaryColor}', '"#1E40AF"')
WHERE domain = 'demo.schoolportal.com';
