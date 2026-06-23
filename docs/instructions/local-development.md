# Local development

## First-time setup

1. Install prerequisites (see [README](./README.md#prerequisites)).
2. Trust HTTPS dev certificates:

   ```powershell
   dotnet dev-certs https --trust
   ```

3. Restore local .NET tools (CSharpier):

   ```powershell
   dotnet tool restore
   ```

4. Install frontend dependencies (if not already):

   ```powershell
   cd src/Services/acme-web
   pnpm install
   cd ../..
   ```

5. Start **Docker Desktop** and wait until the engine is healthy.

## Formatting & dependencies

- **Format C#** with CSharpier before pushing (CI can run the check variant):

  ```powershell
  dotnet csharpier format .   # or: dotnet csharpier check .
  ```

  EF migrations are excluded via `.csharpierignore`.
- **Format/lint the frontend** with Biome before pushing ([ADR-0012](../adr/0012-biome-frontend-lint-format.md)):

  ```powershell
  cd src/Services/acme-web
  pnpm run format   # apply fixes (CI: pnpm run lint)
  ```

- **NuGet lock files** (`packages.lock.json`) are committed per project. After changing a dependency, run `dotnet restore` and commit the updated lock files.
- The **OpenAPI document** (`src/Services/Acme.Api/openapi/v1.json`) and the generated **C# client** (`src/Misc/Acme.ApiClient`) are build artifacts — produced by `dotnet build`, git-ignored, never hand-edited (see [ADR-0010](../adr/0010-openapi-contract-generated-clients.md)).

## Start the full stack

From the repo root:

```powershell
aspire run
```

Or:

```powershell
dotnet run --project src/Aspire/Acme.AppHost
```

Or use the debugger: **Run and Debug → Acme AppHost → F5** (see [Debugging](./debugging.md)).

### What starts

| Resource | Aspire name | External? |
|----------|-------------|-----------|
| Aspire dashboard | (AppHost) | Yes |
| PostgreSQL | `postgres` | No (Docker) |
| API | `acme-api` | **No** |
| Web (SSR) | `acme-web` | Yes |

1. Open the **Aspire dashboard** URL from the console (or it opens automatically when debugging).
2. Click the **acme-web** endpoint to open the landing page.
3. Confirm the landing page shows the API is reachable when the full stack is running.

## Stop the stack

- **Terminal:** `Ctrl+C` in the AppHost terminal.
- **Debugger:** Stop debugging (Shift+F5).
- **Force stop** (if orphaned):

  ```powershell
  Get-Process Acme.AppHost -ErrorAction SilentlyContinue | Stop-Process -Force
  ```

## Run frontend standalone (no Aspire)

Useful for UI-only work. The landing page will show "API not reachable" unless you point at a running API:

1. Start the API separately (or use a stub).
2. Create `src/Services/acme-web/.env`:

   ```
   API_URL=http://localhost:<api-port>
   ```

3. Run:

   ```powershell
   cd src/Services/acme-web
   pnpm run dev
   ```

## Run API standalone (no Aspire)

```powershell
dotnet run --project src/Services/Acme.Api
```

When running under Aspire, data persists to PostgreSQL via EF Core. The API auto-applies each module's pending migrations on startup whenever Postgres is in use (Development and Production). Connection strings are injected by Aspire (`ConnectionStrings:acme`).

To run the API against Postgres without Aspire, set a connection string and disable in-memory mode:

```powershell
$env:ConnectionStrings__acme="Host=localhost;Port=5432;Database=acme;Username=postgres;Password=postgres"
$env:Persistence__UseInMemory="false"
dotnet run --project src/Services/Acme.Api
```

API integration tests set `Persistence:UseInMemory=true` so they do not require a database.

### EF migrations

Migrations are **per module**, against that module's context (see [backend-development.md](./backend-development.md#per-module-persistence)):

```powershell
dotnet ef migrations add <Name> `
  --project src/Modules/Acme.Modules.<M>.Infrastructure `
  --startup-project src/Modules/Acme.Modules.<M>.Infrastructure `
  --context <M>DbContext --output-dir Persistence/Migrations
```

Design-time uses each context's `DbContextFactory` with a local placeholder connection string.

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| Docker unhealthy / WSL error | Enable **SVM Mode** (AMD-V) in UEFI BIOS, reboot, then start Docker Desktop. Windows WSL/VM Platform must stay enabled. See [Docker troubleshooting](#docker-desktop--wsl) below |
| Certificate warnings | `dotnet dev-certs https --trust` |
| Port in use | Stop previous AppHost; check for orphaned `dcp.exe` |
| pnpm not found | `corepack enable` (or `corepack prepare pnpm@11.8.0 --activate`) |
| Web build fails | `cd src/Services/acme-web && pnpm install` |

### Docker Desktop / WSL

Docker Desktop on Windows uses WSL2. If the engine never starts and logs show `HCS_E_HYPERV_NOT_INSTALLED` or `Virtualization Enabled In Firmware: No`:

1. **Reboot into UEFI/BIOS** (ASUS: hold **Del** or **F2** during boot).
2. Enable CPU virtualization:
   - **Advanced → CPU Configuration → SVM Mode → Enabled** (AMD Ryzen)
   - On Intel boards: **Intel Virtualization Technology (VT-x) → Enabled**
3. Save and reboot.
4. Confirm Windows sees it:

   ```powershell
   systeminfo | findstr /C:"Virtualization Enabled In Firmware"
   ```

   Expected: `Virtualization Enabled In Firmware: Yes`

5. Reset WSL and Docker distros if a previous failed install left them broken:

   ```powershell
   wsl --shutdown
   wsl --unregister docker-desktop
   wsl --unregister docker-desktop-data
   ```

   (Ignore errors if those distros do not exist.)

6. Start **Docker Desktop** and wait until `docker info` succeeds.

Windows optional features **Virtual Machine Platform** and **Windows Subsystem for Linux** should remain enabled (`wsl --install --no-distribution`).

## Related ADRs

- [ADR-0001: Aspire orchestration](../adr/0001-aspire-orchestration.md)
- [ADR-0003: Internal API via SSR](../adr/0003-internal-api-via-ssr.md)
- [ADR-0005: PostgreSQL via Aspire](../adr/0005-postgresql-via-aspire.md)
- [ADR-0009: Persistence patterns](../adr/0009-session-persistence-patterns.md)
