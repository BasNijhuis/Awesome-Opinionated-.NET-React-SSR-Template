# Debugging

## Cursor / VS Code (recommended)

Configuration lives in `.vscode/launch.json`.

### Debug AppHost (full stack)

1. **Run and Debug** panel (Ctrl+Shift+D).
2. Select **Acme AppHost**.
3. Press **F5**.

This:

- Uses the C# Dev Kit `dotnet` debugger with `Acme.AppHost.csproj`
- Reads env vars and URLs from `src/Aspire/Acme.AppHost/Properties/launchSettings.json` (first profile: `https`)
- Opens the Aspire dashboard when the login URL appears in logs

To switch launch profiles (e.g. `http` vs `https`), use **C#: Launch Startup Project** and pick the profile from the status bar `{ }` control, or run `.NET: Generate Assets for Build and Debug` to regenerate configs from the IDE.

Set breakpoints in:

- `src/Aspire/Acme.AppHost/AppHost.cs` — orchestration wiring
- `src/Services/Acme.Api/Program.cs` — API startup and endpoints
- (Future) Application handlers and domain aggregates

### Debug API only

Select **Acme Api** and F5. Useful when testing `/api/ping` without Docker or the frontend.

> Child services started by Aspire (web, Postgres) are **not** automatically attached to this debug session. For API logic, prefer debugging the API project directly or use Aspire dashboard logs.

## Command line (no debugger)

```powershell
dotnet run --project src/Aspire/Acme.AppHost
```

## Aspire dashboard

Use the dashboard for:

- Resource health (green/red)
- Structured logs per resource
- Traces and metrics (via ServiceDefaults)

Dashboard URL pattern: `https://localhost:17xxx/login?t=...` (token in console output).

## HTTPS certificates

If you see untrusted certificate warnings:

```powershell
dotnet dev-certs https --trust
```

Optional (if Aspire CLI installed):

```powershell
dotnet tool install -g aspire.cli --version 13.4.5
aspire certs trust
```

## Frontend debugging

- **Browser DevTools** for client-side React/hydration.
- **Server-side:** add `console.log` in loaders/actions (visible in the `acme-web` resource logs in Aspire dashboard).
- Do not put breakpoints in `*.server.ts` from the browser—those run on the Node SSR process. Use Node inspector if needed:

  ```powershell
  cd src/Services/acme-web
  node --inspect-brk node_modules/@react-router/serve/dist/cli.js ./build/server/index.js
  ```

## Related

- [.vscode/launch.json](../../.vscode/launch.json)
- [.vscode/tasks.json](../../.vscode/tasks.json)
- [Local development](./local-development.md)
