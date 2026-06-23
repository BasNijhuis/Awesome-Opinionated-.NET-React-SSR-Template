import { reactRouter } from "@react-router/dev/vite";
import tailwindcss from "@tailwindcss/vite";
import { defineConfig, loadEnv } from "vite";

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), "");
  const apiUrl = env.API_URL ?? env["services__acme-api__http__0"] ?? "http://localhost:5000";

  return {
    plugins: [tailwindcss(), reactRouter()],
    resolve: {
      tsconfigPaths: true,
    },
    server: {
      proxy: {
        "/hubs": {
          target: apiUrl.replace(/\/$/, ""),
          ws: true,
          changeOrigin: true,
        },
      },
    },
  };
});
