import { createInstance, type i18n as I18nInstance } from "i18next";
import { initReactI18next } from "react-i18next";

import enCommon from "../locales/en/common.json";
import enErrors from "../locales/en/errors.json";
import enGreetings from "../locales/en/greetings.json";
import enHome from "../locales/en/home.json";
import enWidgets from "../locales/en/widgets.json";
import nlCommon from "../locales/nl/common.json";
import nlErrors from "../locales/nl/errors.json";
import nlGreetings from "../locales/nl/greetings.json";
import nlHome from "../locales/nl/home.json";
import nlWidgets from "../locales/nl/widgets.json";

export const SUPPORTED_LOCALES = ["en", "nl"] as const;
export type Locale = (typeof SUPPORTED_LOCALES)[number];
export const DEFAULT_LOCALE: Locale = "en";

/** Script-readable cookie (not httpOnly) so the language toggle can be debugged client-side. */
export const LOCALE_COOKIE = "locale";

export const NAMESPACES = ["common", "home", "greetings", "widgets", "errors"] as const;
export const DEFAULT_NS = "common";

// Resources are bundled at build time (no http backend), so server and client init the same
// dictionaries synchronously — first paint and hydration use identical strings, no flash.
export const resources = {
  en: {
    common: enCommon,
    home: enHome,
    greetings: enGreetings,
    widgets: enWidgets,
    errors: enErrors,
  },
  nl: {
    common: nlCommon,
    home: nlHome,
    greetings: nlGreetings,
    widgets: nlWidgets,
    errors: nlErrors,
  },
} as const;

export function isLocale(value: unknown): value is Locale {
  return typeof value === "string" && (SUPPORTED_LOCALES as readonly string[]).includes(value);
}

function readCookie(cookieHeader: string | null, name: string): string | undefined {
  if (!cookieHeader) {
    return undefined;
  }
  for (const part of cookieHeader.split(";")) {
    const [key, ...rest] = part.trim().split("=");
    if (key === name) {
      return decodeURIComponent(rest.join("="));
    }
  }
  return undefined;
}

function pickFromAcceptLanguage(header: string | null): Locale | undefined {
  if (!header) {
    return undefined;
  }
  // Ordered list like "nl-NL,nl;q=0.9,en;q=0.8" — first supported primary subtag wins.
  for (const entry of header.split(",")) {
    const tag = entry.split(";")[0]?.trim().split("-")[0]?.toLowerCase();
    if (isLocale(tag)) {
      return tag;
    }
  }
  return undefined;
}

/** Resolve the active locale: cookie `locale` → `Accept-Language` → default (`en`). */
export function resolveLocale(request: Request): Locale {
  const cookieHeader = request.headers.get("cookie");
  const fromCookie = readCookie(cookieHeader, LOCALE_COOKIE);
  if (isLocale(fromCookie)) {
    return fromCookie;
  }
  return pickFromAcceptLanguage(request.headers.get("accept-language")) ?? DEFAULT_LOCALE;
}

/**
 * Build a fresh i18next instance for the given locale. Synchronous (`initImmediate: false`)
 * because all resources are bundled — safe to create per request on the server and once on the
 * client. English is the fallback so unmapped keys never blank the UI.
 */
export function createI18nInstance(locale: Locale): I18nInstance {
  const instance = createInstance();
  instance.use(initReactI18next).init({
    lng: locale,
    fallbackLng: DEFAULT_LOCALE,
    supportedLngs: SUPPORTED_LOCALES,
    ns: NAMESPACES,
    defaultNS: DEFAULT_NS,
    resources,
    interpolation: { escapeValue: false },
    react: { useSuspense: false },
    initAsync: false,
  });
  return instance;
}
