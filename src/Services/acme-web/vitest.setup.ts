// Registers @testing-library/jest-dom matchers (toBeChecked, toHaveAttribute, …) on Vitest's expect,
// and wires React Testing Library's automatic cleanup (active because `globals: true`).
import "@testing-library/jest-dom/vitest";

import i18next from "i18next";
import { initReactI18next } from "react-i18next";
import { DEFAULT_LOCALE, DEFAULT_NS, NAMESPACES, resources } from "./app/lib/i18n";

// Initialize the global i18next instance so components calling `useTranslation()` resolve strings
// in tests without each test wrapping in <I18nextProvider>. SSR uses per-request instances instead.
if (!i18next.isInitialized) {
  i18next.use(initReactI18next).init({
    lng: DEFAULT_LOCALE,
    fallbackLng: DEFAULT_LOCALE,
    ns: NAMESPACES,
    defaultNS: DEFAULT_NS,
    resources,
    interpolation: { escapeValue: false },
    react: { useSuspense: false },
    initAsync: false,
  });
}
