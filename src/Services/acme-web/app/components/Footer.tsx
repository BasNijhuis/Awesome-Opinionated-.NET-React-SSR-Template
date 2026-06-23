import { useTranslation } from "react-i18next";

export function Footer() {
  const { t } = useTranslation("common");

  return (
    <footer className="border-surface-border border-t pt-4 text-center text-surface-muted text-xs">
      <p>{t("footer.tagline")}</p>
    </footer>
  );
}
