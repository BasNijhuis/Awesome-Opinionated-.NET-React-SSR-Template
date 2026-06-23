import type { ErrorTranslator } from "./errors";
import { createI18nInstance, resolveLocale } from "./i18n";

/**
 * Build an {@link ErrorTranslator} for the request's locale. Returns a localized `errors` message
 * for a known code, or `undefined` so `getErrorMessage` falls back to the API's English detail.
 * SSR-only — actions run on the server where the request (and its locale cookie) is available.
 */
export function getErrorTranslator(request: Request): ErrorTranslator {
  const i18n = createI18nInstance(resolveLocale(request));
  return (code) => {
    const key = `errors:${code}`;
    return i18n.exists(key) ? i18n.t(key) : undefined;
  };
}

/** Localized "Invalid request." for client-side form validation failures in actions. */
export function invalidRequestMessage(request: Request): string {
  return createI18nInstance(resolveLocale(request)).t("errors:invalidRequest");
}
