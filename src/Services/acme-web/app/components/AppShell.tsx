import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { NavLink } from "react-router";
import { Footer } from "./Footer";
import { LanguageToggle } from "./LanguageToggle";
import { NotificationsSubscriber } from "./NotificationsSubscriber";

type AppShellProps = {
  children: ReactNode;
  subtitle?: string;
  title?: string;
  eyebrow?: string;
};

const NAV_LINKS = [
  { to: "/", labelKey: "nav.home", end: true },
  { to: "/greetings", labelKey: "nav.greetings", end: false },
  { to: "/widgets", labelKey: "nav.widgets", end: false },
] as const;

/** Generic application chrome: header, primary nav, language toggle, live notifications and footer. */
export function AppShell({ children, subtitle, title, eyebrow }: AppShellProps) {
  const { t } = useTranslation("common");

  return (
    <div className="min-h-screen bg-surface text-surface-fg">
      <div className="mx-auto min-h-screen max-w-3xl space-y-8 px-4 py-8 sm:px-8">
        <nav className="flex items-center justify-between gap-4">
          <ul className="flex items-center gap-1 text-sm">
            {NAV_LINKS.map((link) => (
              <li key={link.to}>
                <NavLink
                  to={link.to}
                  end={link.end}
                  className={({ isActive }) =>
                    `rounded-lg px-3 py-1.5 transition-colors ${
                      isActive
                        ? "bg-brand/15 font-semibold text-brand"
                        : "text-surface-muted hover:text-surface-fg"
                    }`
                  }
                >
                  {t(link.labelKey)}
                </NavLink>
              </li>
            ))}
          </ul>
          <LanguageToggle />
        </nav>

        {(eyebrow || title || subtitle) && (
          <header className="space-y-2 text-center">
            {eyebrow && (
              <p className="text-brand text-xs uppercase tracking-[0.35em] sm:text-sm">{eyebrow}</p>
            )}
            {title && (
              <h1 className="font-display text-4xl text-surface-fg sm:text-5xl">{title}</h1>
            )}
            {subtitle && <p className="mx-auto max-w-xl text-surface-muted">{subtitle}</p>}
          </header>
        )}

        {children}

        <Footer />
      </div>

      <NotificationsSubscriber />
    </div>
  );
}
