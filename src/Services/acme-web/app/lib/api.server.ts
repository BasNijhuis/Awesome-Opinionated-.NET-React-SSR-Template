import { z } from "zod";
import * as api from "./api/generated";
import {
  zCreateGreetingResult,
  zCreateWidgetResult,
  zGreetingDto,
  zGreetingSuggestionDto,
  zWidgetDto,
} from "./api/generated/zod.gen";
import { getApiBaseUrl } from "./config.server";
import { ApiError } from "./errors";
import { currentLocale } from "./locale-context.server";

// App-facing contract types are `z.infer` of the Zod schemas (#17) — the post-processed `types.gen.ts`
// makes the schemas the single source of truth — re-exported from this SSR boundary so loaders/actions/
// components import them here. The SDK call functions (values) come from the generated index.
export type * from "./api/generated/types.gen";

export { ApiError, getErrorMessage } from "./errors";

/** Generated SDK calls return `{ data, error, response }` (throwOnError is off). */
type SdkResult = { data?: unknown; error?: unknown; response?: Response };

function toApiError(response: Response | undefined, error: unknown): ApiError {
  const body = error as { detail?: string; title?: string; errorCode?: string } | undefined;
  const status = response?.status ?? 0;
  const detail = body?.detail ?? body?.title ?? `API error ${status}`;
  return new ApiError(status, detail, body?.errorCode);
}

/**
 * Unwrap an SDK result: surface a non-2xx as `ApiError`, then validate the body against its Zod
 * response schema at the SSR boundary (#17) so a shape mismatch surfaces as `ApiError` here rather
 * than crashing deep in a component.
 */
function unwrap<T>(result: SdkResult, schema: z.ZodType<T>): T {
  if (!result.response?.ok) {
    throw toApiError(result.response, result.error);
  }

  const parsed = schema.safeParse(result.data);
  if (!parsed.success) {
    throw new ApiError(result.response.status, "Unexpected API response shape.");
  }

  return parsed.data;
}

// Common per-call options: the SSR base URL (Aspire service discovery) plus the request's locale as
// `Accept-Language`, so the API (ILocaleProvider) serves localized content in the user's language.
// The locale comes from the request-scoped store set by the root middleware — every call forwards it,
// no caller threads it through. Browser → SSR → API only (ADR-0003).
function base() {
  return { baseUrl: getApiBaseUrl(), headers: { "Accept-Language": currentLocale() } };
}

// ---- greetings ----

export async function createGreeting(message: string) {
  return unwrap(await api.createGreeting({ ...base(), body: { message } }), zCreateGreetingResult);
}

export async function getGreeting(id: string) {
  return unwrap(await api.getGreeting({ ...base(), path: { id } }), zGreetingDto);
}

export async function listGreetings() {
  return unwrap(await api.listGreetings({ ...base() }), z.array(zGreetingDto));
}

export async function suggestGreeting() {
  return unwrap(await api.suggestGreeting({ ...base() }), zGreetingSuggestionDto);
}

// ---- widgets ----

export async function createWidget(name: string, quantity: number) {
  return unwrap(
    await api.createWidget({ ...base(), body: { name, quantity } }),
    zCreateWidgetResult,
  );
}

export async function getWidget(id: string) {
  return unwrap(await api.getWidget({ ...base(), path: { id } }), zWidgetDto);
}

export async function adjustWidgetQuantity(id: string, delta: number) {
  return unwrap(
    await api.adjustWidgetQuantity({ ...base(), path: { id }, body: { delta } }),
    zWidgetDto,
  );
}

export async function listWidgets() {
  return unwrap(await api.listWidgets({ ...base() }), z.array(zWidgetDto));
}
