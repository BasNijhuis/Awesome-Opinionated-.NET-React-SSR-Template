import { isbot } from "isbot";
import { renderToReadableStream } from "react-dom/server";
import { I18nextProvider } from "react-i18next";
import type { EntryContext, RouterContextProvider } from "react-router";
import { ServerRouter } from "react-router";
import { createI18nInstance, resolveLocale } from "./lib/i18n";

export const streamTimeout = 5_000;

export default async function handleRequest(
  request: Request,
  responseStatusCode: number,
  responseHeaders: Headers,
  routerContext: EntryContext,
  _loadContext: RouterContextProvider,
) {
  // https://httpwg.org/specs/rfc9110.html#HEAD
  if (request.method.toUpperCase() === "HEAD") {
    return new Response(null, {
      status: responseStatusCode,
      headers: responseHeaders,
    });
  }

  // Abort the rendering stream after the `streamTimeout` so it has time to
  // flush down the rejected boundaries.
  const controller = new AbortController();
  const timeoutId = setTimeout(() => controller.abort(), streamTimeout + 1000);

  // Per-request i18next instance, seeded from the request's resolved locale. The root loader
  // resolves the same locale and renders it into <html lang>, so the client rehydrates identically.
  const i18n = createI18nInstance(resolveLocale(request));

  let shellRendered = false;
  const stream = await renderToReadableStream(
    <I18nextProvider i18n={i18n}>
      <ServerRouter context={routerContext} url={request.url} />
    </I18nextProvider>,
    {
      signal: controller.signal,
      onError(error: unknown) {
        responseStatusCode = 500;
        // Log streaming rendering errors from inside the shell.  Don't log
        // errors encountered during initial shell rendering since they'll
        // reject and get logged in handleDocumentRequest.
        if (shellRendered) {
          console.error(error);
        }
      },
    },
  );
  // The promise above resolves once the shell is ready (or rejects on a shell error, which
  // propagates to handleDocumentRequest); anything after this is streamed content.
  shellRendered = true;

  // Release the abort timer once all content has flushed (or the render aborts/errors), so the
  // closure isn't retained — covers the streaming case where we've already returned the Response.
  stream.allReady.then(
    () => clearTimeout(timeoutId),
    () => clearTimeout(timeoutId),
  );

  // Ensure requests from bots and SPA Mode renders wait for all content to load before responding
  // https://react.dev/reference/react-dom/server/renderToReadableStream#waiting-for-all-content-to-load-for-crawlers-and-static-generation
  const userAgent = request.headers.get("user-agent");
  if ((userAgent && isbot(userAgent)) || routerContext.isSpaMode) {
    await stream.allReady;
  }

  responseHeaders.set("Content-Type", "text/html");
  return new Response(stream, {
    headers: responseHeaders,
    status: responseStatusCode,
  });
}
