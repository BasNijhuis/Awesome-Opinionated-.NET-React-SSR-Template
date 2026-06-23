import { redirect } from "react-router";
import { DEFAULT_LOCALE, isLocale, LOCALE_COOKIE } from "../lib/i18n";
import type { Route } from "./+types/set-locale";

const ONE_YEAR_SECONDS = 60 * 60 * 24 * 365;

/**
 * Resource route (no component) that persists the chosen locale in a cookie and redirects back.
 * Works without client JS — the LanguageToggle posts a plain form here. The cookie is read by
 * `resolveLocale` on the next request, so the whole document re-renders in the new language.
 */
export async function action({ request }: Route.ActionArgs) {
  const formData = await request.formData();
  const requested = formData.get("locale");
  const redirectToRaw = formData.get("redirectTo");

  // Only ever redirect to a same-site path to avoid an open-redirect.
  const redirectTo =
    typeof redirectToRaw === "string" && redirectToRaw.startsWith("/") ? redirectToRaw : "/";

  const locale = isLocale(requested) ? requested : DEFAULT_LOCALE;

  return redirect(redirectTo, {
    headers: {
      "Set-Cookie": `${LOCALE_COOKIE}=${locale}; Path=/; Max-Age=${ONE_YEAR_SECONDS}; SameSite=Lax`,
    },
  });
}

// Hitting this route directly (GET) is meaningless — bounce home.
export function loader() {
  return redirect("/");
}
