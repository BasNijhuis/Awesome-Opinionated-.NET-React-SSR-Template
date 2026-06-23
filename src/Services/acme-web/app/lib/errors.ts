export class ApiError extends Error {
  readonly status: number;
  readonly detail: string;
  /** Stable, locale-agnostic code from the API problem body (see ResultHttpExtensions). */
  readonly errorCode?: string;

  constructor(status: number, detail: string, errorCode?: string) {
    super(detail);
    this.status = status;
    this.detail = detail;
    this.errorCode = errorCode;
  }
}

/** Maps an API error code to a localized message, or returns undefined to fall back. */
export type ErrorTranslator = (code: string) => string | undefined;

/**
 * Resolve a user-facing message for an error. When the error is an `ApiError` carrying a stable
 * `errorCode` and a `translate` fn is supplied, the localized message wins; otherwise we fall back
 * to the English `detail` from the API (never the raw English string parsing of before).
 */
export function getErrorMessage(error: unknown, translate?: ErrorTranslator): string {
  if (error instanceof ApiError) {
    if (error.errorCode && translate) {
      const localized = translate(error.errorCode);
      if (localized) {
        return localized;
      }
    }
    return error.detail;
  }

  if (error instanceof Error) {
    try {
      const parsed = JSON.parse(error.message) as { detail?: string };
      if (parsed.detail) {
        return parsed.detail;
      }
    } catch {
      // Not JSON — use the raw message.
    }

    return error.message;
  }

  return "Something went wrong.";
}
