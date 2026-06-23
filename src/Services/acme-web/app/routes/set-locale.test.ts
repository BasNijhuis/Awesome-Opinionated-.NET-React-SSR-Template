import { action } from "~/routes/set-locale";

function postForm(fields: Record<string, string>): Request {
  const body = new URLSearchParams(fields);
  return new Request("http://localhost/set-locale", {
    method: "POST",
    body,
    headers: { "content-type": "application/x-www-form-urlencoded" },
  });
}

// The action returns a Response (redirect) — react-router's redirect() produces a 302 with Location.
async function runAction(fields: Record<string, string>): Promise<Response> {
  // biome-ignore lint/suspicious/noExplicitAny: route args are not needed by this action.
  return (await action({ request: postForm(fields) } as any)) as unknown as Response;
}

describe("set-locale action", () => {
  it("sets the locale cookie and redirects to the given path", async () => {
    const response = await runAction({ locale: "nl", redirectTo: "/greetings" });

    expect(response.status).toBe(302);
    expect(response.headers.get("Location")).toBe("/greetings");
    expect(response.headers.get("Set-Cookie")).toContain("locale=nl");
    expect(response.headers.get("Set-Cookie")).toContain("Path=/");
    expect(response.headers.get("Set-Cookie")).toContain("SameSite=Lax");
  });

  it("falls back to the default locale for an unsupported value", async () => {
    const response = await runAction({ locale: "fr", redirectTo: "/" });
    expect(response.headers.get("Set-Cookie")).toContain("locale=en");
  });

  it("refuses to redirect off-site (open-redirect guard)", async () => {
    const response = await runAction({ locale: "en", redirectTo: "https://evil.example.com" });
    expect(response.headers.get("Location")).toBe("/");
  });

  it("defaults redirect to home when redirectTo is missing", async () => {
    const response = await runAction({ locale: "en" });
    expect(response.headers.get("Location")).toBe("/");
  });
});
