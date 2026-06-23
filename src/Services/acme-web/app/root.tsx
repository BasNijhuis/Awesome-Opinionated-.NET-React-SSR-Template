import { useEffect } from "react";
import { useTranslation } from "react-i18next";
import {
  isRouteErrorResponse,
  Links,
  Meta,
  Outlet,
  Scripts,
  ScrollRestoration,
  useRouteLoaderData,
} from "react-router";

import type { Route } from "./+types/root";
import { getErrorMessage } from "./lib/errors";
import { DEFAULT_LOCALE, resolveLocale } from "./lib/i18n";
import "./app.css";

// Resolve the active locale once on the server (cookie → Accept-Language → default) and expose it
// so <html lang> matches what entry.server/entry.client seed their i18next instances with.
export function loader({ request }: Route.LoaderArgs) {
  return { locale: resolveLocale(request) };
}

// Fonts are self-hosted via @fontsource (imported in app.css), so no external <link> tags are
// needed. Kept as an empty list for any future document-level links.
export const links: Route.LinksFunction = () => [];

// Fallback document title — used by any route that doesn't set its own (a leaf route's `meta`
// title overrides this).
export function meta(_: Route.MetaArgs) {
  return [{ title: "Acme" }];
}

export function Layout({ children }: { children: React.ReactNode }) {
  // useRouteLoaderData (not useLoaderData) so the ErrorBoundary can also read it when the root
  // loader succeeded but a child threw; falls back to the default locale if unavailable.
  const data = useRouteLoaderData<typeof loader>("root");
  return (
    <html lang={data?.locale ?? DEFAULT_LOCALE}>
      <head>
        <meta charSet="utf-8" />
        <meta name="viewport" content="width=device-width, initial-scale=1" />
        <Meta />
        <Links />
      </head>
      <body>
        {children}
        <ScrollRestoration />
        <Scripts />
      </body>
    </html>
  );
}

export default function App() {
  const data = useRouteLoaderData<typeof loader>("root");
  const { i18n } = useTranslation();

  // After the language toggle, the root loader re-runs (client-side) and returns the new locale, but
  // the client i18next instance was created once in entry.client from the initial <html lang> — sync
  // it here so a switch re-renders in the new language without a full refresh.
  useEffect(() => {
    if (data?.locale && i18n.language !== data.locale) {
      void i18n.changeLanguage(data.locale);
    }
  }, [data?.locale, i18n]);

  return <Outlet />;
}

export function ErrorBoundary({ error }: Route.ErrorBoundaryProps) {
  let message = "Oops!";
  let details = "An unexpected error occurred.";
  let stack: string | undefined;

  if (isRouteErrorResponse(error)) {
    message = error.status === 404 ? "404" : "Error";
    details =
      error.status === 404 ? "The requested page could not be found." : error.statusText || details;
  } else if (error && error instanceof Error) {
    details = getErrorMessage(error);
    if (import.meta.env.DEV) {
      stack = error.stack;
    }
  }

  return (
    <main className="flex min-h-screen items-center justify-center bg-surface p-6">
      <div className="panel w-full max-w-lg p-8 text-center">
        <h1 className="font-display text-4xl text-surface-fg">{message}</h1>
        <p className="mt-3 text-surface-muted">{details}</p>
        {stack && (
          <pre className="mt-6 w-full overflow-x-auto rounded-xl bg-surface-raised p-4 text-left text-sm text-surface-muted">
            <code>{stack}</code>
          </pre>
        )}
      </div>
    </main>
  );
}
