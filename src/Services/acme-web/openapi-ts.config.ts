import { defineConfig } from "@hey-api/openapi-ts";

// Generates a typed fetch client from the API's OpenAPI document.
// The spec is a build artifact produced by `dotnet build` (see ADR-0010); the output
// under app/lib/api/generated/ is git-ignored and must never be hand-edited.
export default defineConfig({
  input: "../Acme.Api/openapi/v1.json",
  output: "app/lib/api/generated",
  // `zod` emits request/response/component schemas from the same spec (#17); app-facing types are
  // `z.infer`-ed from these and DTOs are validated at the SSR boundary. The sdk/typescript plugins are
  // listed explicitly because naming a custom plugin set otherwise drops the auto-included `sdk`.
  plugins: ["@hey-api/typescript", "@hey-api/sdk", "@hey-api/client-fetch", "zod"],
});
