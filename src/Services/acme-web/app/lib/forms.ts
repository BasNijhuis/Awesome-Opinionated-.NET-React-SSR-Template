import { z } from "zod";

// Frontend-owned input schemas (#17). Form submits and loader/search params are parsed through these
// at the SSR trust boundary instead of `String(formData.get(...)) as T` casts; their TS types are
// inferred with `z.infer`. Contract schemas (DTOs) are generated — only these inputs are hand-written.

// ---- value objects ----

/** A greeting message: required, trimmed, kept to a sensible length. */
const messageSchema = z.string().trim().min(1, "Enter a message.").max(280);

/** A widget name. */
const widgetNameSchema = z.string().trim().min(1, "Enter a name.").max(120);

/** A non-negative whole quantity (coerced from the form string). */
const quantitySchema = z.coerce
  .number()
  .int("Enter a whole number.")
  .min(0, "Quantity cannot be negative.");

// ---- greetings ----

export const createGreetingFormSchema = z.object({
  message: messageSchema,
});

// ---- widgets ----

export const createWidgetFormSchema = z.object({
  name: widgetNameSchema,
  quantity: quantitySchema,
});

// ---- helpers ----

/** Parse a FormData submit through a schema. Object.fromEntries is fine — our forms have unique keys. */
export function parseForm<Schema extends z.ZodType>(schema: Schema, formData: FormData) {
  return schema.safeParse(Object.fromEntries(formData));
}

/** Flatten a Zod error to per-field message arrays for `useActionData`-driven form display. */
export function fieldErrorsOf(error: z.ZodError): Record<string, string[] | undefined> {
  return z.flattenError(error).fieldErrors;
}

// ---- inferred types (no hand-written interfaces duplicate these) ----

export type CreateGreetingForm = z.infer<typeof createGreetingFormSchema>;
export type CreateWidgetForm = z.infer<typeof createWidgetFormSchema>;
export type FieldErrors = ReturnType<typeof fieldErrorsOf>;
