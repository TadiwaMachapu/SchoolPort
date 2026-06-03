"use client";
import { createContext, useContext } from "react";
import type { SchoolFeatures } from "@/lib/theme";

const FeaturesContext = createContext<Partial<SchoolFeatures>>({});

export function FeaturesProvider({
  features,
  children,
}: {
  features: Partial<SchoolFeatures>;
  children: React.ReactNode;
}) {
  return (
    <FeaturesContext.Provider value={features}>
      {children}
    </FeaturesContext.Provider>
  );
}

export function useFeatures(): Partial<SchoolFeatures> {
  return useContext(FeaturesContext);
}
