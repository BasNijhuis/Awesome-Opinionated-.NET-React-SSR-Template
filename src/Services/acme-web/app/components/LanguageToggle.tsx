import { useTranslation } from "react-i18next";
import { Form, useLocation } from "react-router";
import { SUPPORTED_LOCALES } from "../lib/i18n";

/**
 * Posts to the `set-locale` resource route with the current path as `redirectTo`, so switching
 * language reloads the same page in the new locale. Works without client JS.
 */
export function LanguageToggle() {
  const { t, i18n } = useTranslation("common");
  const location = useLocation();
  const redirectTo = `${location.pathname}${location.search}`;

  return (
    <Form
      method="post"
      action="/set-locale"
      preventScrollReset
      className="flex items-center justify-center gap-1 text-xs"
      aria-label={t("language.label")}
    >
      <input type="hidden" name="redirectTo" value={redirectTo} />
      {SUPPORTED_LOCALES.map((locale) => {
        const active = i18n.resolvedLanguage === locale;
        return (
          <button
            key={locale}
            type="submit"
            name="locale"
            value={locale}
            aria-current={active ? "true" : undefined}
            className={`rounded px-2 py-1 transition-colors ${
              active ? "font-semibold text-brand" : "text-surface-muted hover:text-surface-fg"
            }`}
          >
            {t(`language.${locale}`)}
          </button>
        );
      })}
    </Form>
  );
}
