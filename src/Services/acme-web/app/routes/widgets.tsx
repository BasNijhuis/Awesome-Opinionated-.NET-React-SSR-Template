import { useTranslation } from "react-i18next";
import { Form, redirect, useActionData } from "react-router";
import { AppShell } from "../components/AppShell";
import { adjustWidgetQuantity, createWidget, listWidgets } from "../lib/api.server";
import { createWidgetFormSchema, type FieldErrors, fieldErrorsOf, parseForm } from "../lib/forms";
import type { Route } from "./+types/widgets";

export function meta(_: Route.MetaArgs) {
  return [{ title: "Widgets — Acme" }];
}

export async function loader(_: Route.LoaderArgs) {
  const widgets = await listWidgets();
  return { widgets };
}

type WidgetsActionData = { fieldErrors: FieldErrors } | null;

export async function action({ request }: Route.ActionArgs): Promise<WidgetsActionData> {
  const formData = await request.formData();

  // The "adjust quantity" buttons post intent=adjust with the widget id and a signed delta. This
  // triggers the cross-module domain event (Widgets → Greetings) — a new greeting appears on /greetings.
  if (formData.get("intent") === "adjust") {
    await adjustWidgetQuantity(String(formData.get("id")), Number(formData.get("delta")));
    throw redirect("/widgets");
  }

  const parsed = parseForm(createWidgetFormSchema, formData);
  if (!parsed.success) {
    return { fieldErrors: fieldErrorsOf(parsed.error) };
  }

  await createWidget(parsed.data.name, parsed.data.quantity);
  // Redirect back to self so the loader re-runs and the new widget is shown (PRG pattern).
  throw redirect("/widgets");
}

function FieldError({ messages }: { messages?: string[] }) {
  if (!messages?.length) {
    return null;
  }
  return <p className="text-red-600 text-xs">{messages[0]}</p>;
}

export default function Widgets({ loaderData }: Route.ComponentProps) {
  const { widgets } = loaderData;
  const { t, i18n } = useTranslation("widgets");
  const actionData = useActionData() as WidgetsActionData;
  const dateFormatter = new Intl.DateTimeFormat(i18n.language, { dateStyle: "medium" });

  return (
    <AppShell title={t("title")} subtitle={t("subtitle")}>
      <section className="panel space-y-4 p-6">
        <Form method="post" className="space-y-3" data-testid="widget-form">
          <label className="block space-y-1 text-sm">
            <span>{t("form.nameLabel")}</span>
            <input
              name="name"
              required
              className="input"
              placeholder={t("form.namePlaceholder")}
              data-testid="widget-name"
            />
            <FieldError messages={actionData?.fieldErrors?.name} />
          </label>
          <label className="block space-y-1 text-sm">
            <span>{t("form.quantityLabel")}</span>
            <input
              name="quantity"
              type="number"
              min={0}
              defaultValue={1}
              required
              className="input"
              data-testid="widget-quantity"
            />
            <FieldError messages={actionData?.fieldErrors?.quantity} />
          </label>
          <button type="submit" className="btn-primary" data-testid="widget-submit">
            {t("form.submit")}
          </button>
        </Form>
      </section>

      {widgets.length === 0 ? (
        <p className="text-surface-muted" data-testid="widget-empty">
          {t("list.empty")}
        </p>
      ) : (
        <ul className="space-y-2" data-testid="widget-list">
          {widgets.map((widget) => (
            <li key={widget.id} className="panel p-4">
              <div className="flex items-start justify-between gap-4">
                <div>
                  <p className="text-surface-fg">{widget.name}</p>
                  <p className="mt-1 text-surface-muted text-sm">
                    {t("list.quantity", { count: widget.quantity })}
                  </p>
                  <p className="mt-1 text-surface-muted text-xs">
                    {t("list.createdAt", {
                      date: dateFormatter.format(new Date(widget.createdAt)),
                    })}
                  </p>
                </div>
                {/* Adjusting the quantity raises a cross-module domain event; a matching greeting
                    then appears on the Greetings page (same transaction). */}
                <Form method="post" className="flex shrink-0 items-center gap-1">
                  <input type="hidden" name="intent" value="adjust" />
                  <input type="hidden" name="id" value={widget.id} />
                  <button
                    type="submit"
                    name="delta"
                    value="-1"
                    className="btn-secondary"
                    disabled={widget.quantity === 0}
                    aria-label={t("list.decrement")}
                    data-testid="widget-decrement"
                  >
                    −1
                  </button>
                  <button
                    type="submit"
                    name="delta"
                    value="1"
                    className="btn-secondary"
                    aria-label={t("list.increment")}
                    data-testid="widget-increment"
                  >
                    +1
                  </button>
                </Form>
              </div>
            </li>
          ))}
        </ul>
      )}
    </AppShell>
  );
}
