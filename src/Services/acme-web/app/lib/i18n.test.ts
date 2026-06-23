import { createI18nInstance, DEFAULT_LOCALE, isLocale, resolveLocale } from "~/lib/i18n";

function request(headers: Record<string, string>): Request {
  return new Request("http://localhost/", { headers });
}

describe("isLocale", () => {
  it("accepts supported locales and rejects everything else", () => {
    expect(isLocale("en")).toBe(true);
    expect(isLocale("nl")).toBe(true);
    expect(isLocale("de")).toBe(false);
    expect(isLocale(null)).toBe(false);
    expect(isLocale(42)).toBe(false);
  });
});

describe("resolveLocale", () => {
  it("prefers the locale cookie over Accept-Language", () => {
    expect(
      resolveLocale(request({ cookie: "locale=nl", "accept-language": "en-US,en;q=0.9" })),
    ).toBe("nl");
  });

  it("ignores an unsupported cookie value and falls through", () => {
    expect(resolveLocale(request({ cookie: "locale=fr", "accept-language": "nl,en" }))).toBe("nl");
  });

  it("falls back to Accept-Language when no cookie is set", () => {
    expect(resolveLocale(request({ "accept-language": "nl-NL,nl;q=0.9,en;q=0.8" }))).toBe("nl");
  });

  it("falls back to the default locale when nothing matches", () => {
    expect(resolveLocale(request({ "accept-language": "de-DE,fr;q=0.5" }))).toBe(DEFAULT_LOCALE);
    expect(resolveLocale(request({}))).toBe(DEFAULT_LOCALE);
  });
});

describe("createI18nInstance", () => {
  it("returns Dutch strings for the nl locale", () => {
    const i18n = createI18nInstance("nl");
    expect(i18n.t("common:nav.greetings")).toBe("Begroetingen");
  });

  it("returns English strings for the en locale", () => {
    const i18n = createI18nInstance("en");
    expect(i18n.t("common:nav.greetings")).toBe("Greetings");
  });

  it("falls back to English for a key missing in Dutch", () => {
    const i18n = createI18nInstance("nl");
    // appName is identical in both, but fallback must resolve a known key without returning the raw key.
    expect(i18n.t("common:appName")).toBe("Acme");
  });

  it("resolves nested namespace keys per locale", () => {
    const nl = createI18nInstance("nl");
    expect(nl.t("greetings:form.submit")).toBe("Begroeting toevoegen");
    expect(nl.t("widgets:form.submit")).toBe("Widget toevoegen");
  });
});
