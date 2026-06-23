export function getApiBaseUrl(): string {
  const url = process.env["services__acme-api__http__0"] ?? process.env.API_URL;

  if (!url) {
    throw new Error("API base URL not configured");
  }

  return url.replace(/\/$/, "");
}
