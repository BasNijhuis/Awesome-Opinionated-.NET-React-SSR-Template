import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { AppShell } from "../components/AppShell";
import type { Route } from "./+types/home";

export function meta(_: Route.MetaArgs) {
  return [
    { title: "Acme" },
    {
      name: "description",
      content:
        "An opinionated full-stack template: React Router SSR, .NET Aspire, and a typed API client.",
    },
  ];
}

export default function Home() {
  const { t } = useTranslation(["home", "common"]);

  const cards = [
    {
      to: "/greetings",
      title: t("home:cards.greetings.title"),
      body: t("home:cards.greetings.body"),
      cta: t("home:cards.greetings.cta"),
    },
    {
      to: "/widgets",
      title: t("home:cards.widgets.title"),
      body: t("home:cards.widgets.body"),
      cta: t("home:cards.widgets.cta"),
    },
  ];

  return (
    <AppShell
      eyebrow={t("home:hero.eyebrow")}
      title={t("common:appName")}
      subtitle={t("home:hero.subtitle")}
    >
      <p className="panel p-6 text-surface-muted">{t("home:blurb")}</p>

      <div className="grid gap-6 md:grid-cols-2">
        {cards.map((card) => (
          <section key={card.to} className="panel flex flex-col gap-3 p-6">
            <h2 className="font-display text-surface-fg text-xl">{card.title}</h2>
            <p className="flex-1 text-surface-muted text-sm">{card.body}</p>
            <Link to={card.to} className="btn-primary self-start">
              {card.cta}
            </Link>
          </section>
        ))}
      </div>
    </AppShell>
  );
}
