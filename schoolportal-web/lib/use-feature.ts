"use client";
import { useFeatures } from "@/lib/features-context";
import type { SchoolFeatures } from "@/lib/theme";

export function useFeature(name: keyof SchoolFeatures): boolean {
  const features = useFeatures();
  return features[name] === true;
}
