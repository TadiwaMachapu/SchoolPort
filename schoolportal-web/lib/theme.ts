export interface SchoolTheme {
  primaryColor: string;
  logoUrl?: string;
  faviconUrl?: string;
  fontFamily: string;
  customDomain?: string;
  welcomeMessage?: string;
  supportEmail?: string;
}

export interface SchoolFeatures {
  quizzes: boolean;
  attendance: boolean;
  parentPortal: boolean;
  messaging: boolean;
  courses: boolean;
  analytics: boolean;
  aiGrading: boolean;
  plagiarismDetection: boolean;
  sso: boolean;
  customReports: boolean;
  whiteLabel: boolean;
  pluginApi: boolean;
}

export function applyTheme(theme: SchoolTheme) {
  if (typeof document === "undefined") return;
  const root = document.documentElement;
  root.style.setProperty("--color-primary", theme.primaryColor);
  root.style.setProperty("--font-family", theme.fontFamily);
  if (theme.faviconUrl) {
    const link = document.querySelector<HTMLLinkElement>('link[rel="icon"]')
      ?? Object.assign(document.createElement("link"), { rel: "icon" });
    link.href = theme.faviconUrl;
    document.head.appendChild(link);
  }
}

export function hexToRgb(hex: string): string {
  const result = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})$/i.exec(hex);
  if (!result) return "30 64 175";
  return `${parseInt(result[1], 16)} ${parseInt(result[2], 16)} ${parseInt(result[3], 16)}`;
}
