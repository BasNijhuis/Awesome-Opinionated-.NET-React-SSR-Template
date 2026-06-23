import { StrictMode, startTransition } from "react";
import { hydrateRoot } from "react-dom/client";
import { I18nextProvider } from "react-i18next";
import { HydratedRouter } from "react-router/dom";
import { createI18nInstance, DEFAULT_LOCALE, isLocale, type Locale } from "./lib/i18n";

// The server rendered the resolved locale into <html lang>; reuse it so the client i18next
// instance matches the server-rendered markup exactly (no hydration mismatch, no flash).
const lang = document.documentElement.lang;
const locale: Locale = isLocale(lang) ? lang : DEFAULT_LOCALE;
const i18n = createI18nInstance(locale);

startTransition(() => {
  hydrateRoot(
    document,
    <StrictMode>
      <I18nextProvider i18n={i18n}>
        <HydratedRouter />
      </I18nextProvider>
    </StrictMode>,
  );
});
