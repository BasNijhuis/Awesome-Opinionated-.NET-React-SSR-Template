import { index, type RouteConfig, route } from "@react-router/dev/routes";

export default [
  index("routes/home.tsx"),
  route("set-locale", "routes/set-locale.tsx"),
  route("greetings", "routes/greetings.tsx"),
  route("widgets", "routes/widgets.tsx"),
] satisfies RouteConfig;
