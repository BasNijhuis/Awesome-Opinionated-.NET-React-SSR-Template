import { z } from "zod";
import * as api from "./api/generated";
import {
  zCreateGreetingResult,
  zCreateWidgetResult,
  zGreetingDto,
  zWidgetDto,
} from "./api/generated/zod.gen";
import { getApiBaseUrl } from "./config.server";
import { ApiError } from "./errors";

// App-facing contract types are generated as `z.infer` of the Zod schemas (contract.gen.ts, #17) and
// re-exported from this SSR boundary, so loaders/actions/components import them here. App code never
// imports the static `types.gen.ts`; the SDK call functions (values) come from the generated index.
export type * from "./api/generated/contract.gen";

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

/** Common per-call options: inject the SSR base URL lazily (Aspire service discovery). */
function base() {
  return { baseUrl: getApiBaseUrl() };
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
