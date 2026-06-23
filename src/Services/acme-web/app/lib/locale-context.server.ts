import { AsyncLocalStorage } from "node:async_hooks";
import { DEFAULT_LOCALE, type Locale } from "./i18n";

// Request-scoped active locale. A root middleware resolves it once (cookie → Accept-Language) and
// runs the rest of the request inside this store, so every server-side API call can attach the
// caller's language as `Accept-Language` without each loader/action threading it through (the
// improvement over per-call forwarding). Server-only (`node:async_hooks`), hence the `.server` suffix.
const store = new AsyncLocalStorage<Locale>();

/** Run <paramref name="fn"/> with <paramref name="locale"/> as the ambient request locale. */
export function runWithLocale<T>(locale: Locale, fn: () => T): T {
  return store.run(locale, fn);
}

/** The current request's locale, or the default outside a request scope. */
export function currentLocale(): Locale {
  return store.getStore() ?? DEFAULT_LOCALE;
}
