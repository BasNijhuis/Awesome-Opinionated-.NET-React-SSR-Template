import { ApiError, getErrorMessage } from "~/lib/errors";

describe("getErrorMessage", () => {
  it("returns the detail of an ApiError when no translator is given", () => {
    expect(getErrorMessage(new ApiError(404, "Greeting not found"))).toBe("Greeting not found");
  });

  it("prefers a translated message when the ApiError carries a known errorCode", () => {
    const translate = (code: string) => (code === "not_found" ? "Niet gevonden" : undefined);
    const error = new ApiError(404, "Greeting not found", "not_found");
    expect(getErrorMessage(error, translate)).toBe("Niet gevonden");
  });

  it("falls back to the English detail when the code has no translation", () => {
    const translate = () => undefined;
    const error = new ApiError(409, "That name is already taken.", "name_taken");
    expect(getErrorMessage(error, translate)).toBe("That name is already taken.");
  });

  it("falls back to the detail when the ApiError has no errorCode", () => {
    const translate = () => "should not be used";
    expect(getErrorMessage(new ApiError(409, "Conflict"), translate)).toBe("Conflict");
  });

  it("extracts detail from an Error whose message is JSON", () => {
    expect(getErrorMessage(new Error(JSON.stringify({ detail: "Validation failed" })))).toBe(
      "Validation failed",
    );
  });

  it("returns the raw message of a plain (non-JSON) Error", () => {
    expect(getErrorMessage(new Error("boom"))).toBe("boom");
  });

  it("returns the raw message when JSON has no detail field", () => {
    const message = JSON.stringify({ title: "Conflict" });
    expect(getErrorMessage(new Error(message))).toBe(message);
  });

  it("falls back to a generic message for non-Error values", () => {
    expect(getErrorMessage(null)).toBe("Something went wrong.");
    expect(getErrorMessage("nope")).toBe("Something went wrong.");
    expect(getErrorMessage(42)).toBe("Something went wrong.");
  });
});
