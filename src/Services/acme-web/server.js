import path from "node:path";
import url from "node:url";
import { createRequestHandler } from "@react-router/express";
import compression from "compression";
import express from "express";
import { createProxyMiddleware } from "http-proxy-middleware";
import morgan from "morgan";

// Production server for the Acme web app.
//
// It is the `react-router-serve` setup (compression + static assets + the React Router request
// handler) plus one addition: it proxies `/hubs/*` to the internal API so SignalR works at the web
// origin (ADR-0003). The dev server gets this from `vite.config.ts`'s `server.proxy`; that proxy
// does not exist in a built app, so production needs its own. Used by Aspire for end-to-end tests
// (#12) and any production-style run.

const port = Number(process.env.PORT) || 3000;

// Same resolution order as `vite.config.ts` / `app/lib/config.server.ts`: an explicit API_URL wins,
// otherwise Aspire's injected service-discovery variable, otherwise the local default.
const apiUrl = (
  process.env.API_URL ??
  process.env["services__acme-api__http__0"] ??
  "http://localhost:5000"
).replace(/\/$/, "");

const build = await import("./build/server/index.js");

function expressPathOf(publicPath) {
  let pathname;
  try {
    pathname = new URL(publicPath).pathname;
  } catch {
    pathname = publicPath;
  }
  return pathname.startsWith("/") ? pathname : `/${pathname}`;
}

const app = express();
app.disable("x-powered-by");

// Proxy SignalR (and any other `/hubs` traffic) to the API, upgrading websockets. Registered before
// compression/static so hub requests are never touched by the asset pipeline. A `pathFilter` is used
// instead of mounting at `app.use("/hubs", …)` on purpose: Express strips the mount prefix from the
// URL, which would forward `/notifications/negotiate` instead of `/hubs/notifications/negotiate` (a
// 404). The filter keeps the full path so the API's `/hubs/notifications` hub is reached.
const hubsProxy = createProxyMiddleware({
  pathFilter: (pathname) => pathname.startsWith("/hubs"),
  target: apiUrl,
  changeOrigin: true,
  ws: true,
});
app.use(hubsProxy);

app.use(compression());

const publicPath = expressPathOf(build.publicPath);
const assetsDir = path.resolve(
  path.dirname(url.fileURLToPath(import.meta.url)),
  build.assetsBuildDirectory,
);
app.use(
  path.posix.join(publicPath, "assets"),
  express.static(path.join(assetsDir, "assets"), { immutable: true, maxAge: "1y" }),
);
app.use(publicPath, express.static(assetsDir));
app.use(express.static("public", { maxAge: "1h" }));

app.use(morgan("tiny"));

app.all("/{*splat}", createRequestHandler({ build, mode: process.env.NODE_ENV }));

const server = app.listen(port, () => {
  console.log(`[server] http://localhost:${port} — proxying /hubs to ${apiUrl}`);
});

// http-proxy-middleware needs the raw upgrade event to proxy websockets.
server.on("upgrade", hubsProxy.upgrade);

for (const signal of ["SIGTERM", "SIGINT"]) {
  process.once(signal, () => server.close(console.error));
}
