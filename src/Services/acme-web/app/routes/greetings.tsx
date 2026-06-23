import { useTranslation } from "react-i18next";
import { Form, redirect, useActionData } from "react-router";
import { AppShell } from "../components/AppShell";
import { createGreeting, listGreetings } from "../lib/api.server";
import { createGreetingFormSchema, type FieldErrors, fieldErrorsOf, parseForm } from "../lib/forms";
import type { Route } from "./+types/greetings";

export function meta(_: Route.MetaArgs) {
  return [{ title: "Greetings — Acme" }];
}

export async function loader(_: Route.LoaderArgs) {
  const greetings = await listGreetings();
  return { greetings };
}

type GreetingsActionData = { fieldErrors: FieldErrors } | null;

export async function action({ request }: Route.ActionArgs): Promise<GreetingsActionData> {
  const formData = await request.formData();
  const parsed = parseForm(createGreetingFormSchema, formData);
  if (!parsed.success) {
    return { fieldErrors: fieldErrorsOf(parsed.error) };
  }

  await createGreeting(parsed.data.message);
  // Redirect back to self so the loader re-runs and the new greeting is shown (PRG pattern).
  throw redirect("/greetings");
}

function FieldError({ messages }: { messages?: string[] }) {
  if (!messages?.length) {
    return null;
  }
  return <p className="text-red-600 text-xs">{messages[0]}</p>;
}

export default function Greetings({ loaderData }: Route.ComponentProps) {
  const { greetings } = loaderData;
  const { t, i18n } = useTranslation("greetings");
  const actionData = useActionData() as GreetingsActionData;
  const dateFormatter = new Intl.DateTimeFormat(i18n.language, { dateStyle: "medium" });

  return (
    <AppShell title={t("title")} subtitle={t("subtitle")}>
      <section className="panel space-y-4 p-6">
        <Form method="post" className="space-y-3" data-testid="greeting-form">
          <label className="block space-y-1 text-sm">
            <span>{t("form.messageLabel")}</span>
            <input
              name="message"
              required
              className="input"
              placeholder={t("form.messagePlaceholder")}
              data-testid="greeting-message"
            />
            <FieldError messages={actionData?.fieldErrors?.message} />
          </label>
          <button type="submit" className="btn-primary" data-testid="greeting-submit">
            {t("form.submit")}
          </button>
        </Form>
      </section>

      {greetings.length === 0 ? (
        <p className="text-surface-muted" data-testid="greeting-empty">
          {t("list.empty")}
        </p>
      ) : (
        <ul className="space-y-2" data-testid="greeting-list">
          {greetings.map((greeting) => (
            <li key={greeting.id} className="panel p-4">
              <p className="text-surface-fg">{greeting.message}</p>
              <p className="mt-1 text-surface-muted text-xs">
                {t("list.createdAt", { date: dateFormatter.format(new Date(greeting.createdAt)) })}
              </p>
            </li>
          ))}
        </ul>
      )}
    </AppShell>
  );
}
