# MealWiz repo notes

Practical facts for working an issue here. They're the things that cost time when nobody knew them.

## Branches and pipelines
- `dev`: integration branch. Feature PRs target it.
- `main`: production. `dev` → `main` PRs release; hotfix PRs also target `main`, then `main` → `dev` brings them
  back.
- `.github/workflows/CI.yml` runs on **every PR** (any base). One job:
  1. Starts Supabase, which applies all migrations and the seed.
  2. Builds the Blazor app and runs `MealWiz.Shared.Tests`.
  3. Runs `MealWiz.Functions.Tests` (which builds the Function project).
- `.github/workflows/deploy.yml` runs on **push to `main`**:
  1. `supabase db push` first.
  2. Then deploys the Blazor app (Static Web Apps) if `MauiWASM`/`MealWiz.Shared` changed.
  3. Deploys the Function App `MealWizFunctions` if `MealWiz/src/MealWiz.Functions` changed.
  4. A manual run (`workflow_dispatch`) deploys both.
- Merge PRs with a merge commit (`gh pr merge <n> --merge`), as the history does. No branch protection exists, so
  check CI is green yourself before merging.

## Build and test (targeted, Release)
The full `MealWiz.sln` build compiles every MAUI platform and takes very long. Avoid it unless MAUI files changed,
and then run it in the background.

```bash
dotnet build MealWiz/MauiWASM/MauiWASM.csproj -c Release
dotnet test --project MealWiz/tests/MealWiz.Shared.Tests -c Release
dotnet test --project MealWiz/tests/MealWiz.Functions.Tests -c Release
```

- Use `-c Release`: the user often runs the Function in Visual Studio, which locks `bin\Debug`. Never kill their
  process.
- The tests use xUnit v3 on Microsoft.Testing.Platform (repo-root `global.json`), so the flag is `--project`.
- Function integration tests need local Supabase. Without it they're skipped, unless `REQUIRE_SUPABASE=true` is set,
  as it is in CI.
- After changing package versions in WASM, run `dotnet clean MealWiz/MauiWASM/MauiWASM.csproj -c Release` once.
  Incremental builds can keep serving an old DLL.

## Local Supabase
- `supabase start` at the repo root (CLI 2.111.0, pinned in CI too). Seed user `test@test.com`; the password is in
  `supabase/seed.sql`.
- Migrations live in **`supabase/migrations/` at the repo root**. CLAUDE.md's `MealWiz/supabase` is outdated.
  File name: `YYYYMMDDHHMMSS_description.sql`.
- Apply new migrations without wiping data: `supabase migration up --local`. `supabase db reset` wipes local data,
  so ask first.
- Quick SQL as a specific user, in a transaction you roll back:
  `docker exec -i supabase_db_MealWiz psql -U postgres` and `set local role authenticated;` plus
  `set local request.jwt.claims = '{"sub":"<uuid>","role":"authenticated"}';`

## Run locally (end-to-end)
- **Function:** `func.exe` is at `C:\Program Files\Microsoft\Azure Functions Core Tools\func.exe`.
  - Settings: the gitignored `MealWiz/src/MealWiz.Functions/local.settings.json` (local service-role key, and
    `Host.CORS` for `http://localhost:5053,https://localhost:7033`).
  - A fresh worktree doesn't have it. Copy it from the main checkout (it stays gitignored) and never print it.
- **Web app:** `dotnet run --project MealWiz/MauiWASM/MauiWASM.csproj --launch-profile http` serves port 5053 in
  Development, which calls the Function on 7071.
- **With the Browser pane:** use a temporary `.claude/launch.json` and delete it afterwards; it isn't gitignored.
  - Run `func.exe start --script-root <Functions dir> --port 7071` directly. Wrapping it in `cmd /c` mangles the
    quoted path.
  - Set `"autoPort": false`, otherwise the pane moves the server off 7071/5053.

## Production checklist (before a release that adds infrastructure)
Local Supabase still issues **legacy** keys, but the production project disabled them on 2026-05-06. So local
tests can't catch a wrong key.
- **Supabase keys:** production uses only the new keys.
  - Client: `sb_publishable_…`.
  - Server-side, e.g. the Function App setting `Supabase__ServiceRoleKey`: `sb_secret_…`.
  - The C# client sends the key as both `apikey` and `Bearer`, and the gateway accepts that.
- **Function App settings:** `Supabase__Url`, `Supabase__ServiceRoleKey`. Use `__` as the separator.
- **Function App CORS:** must include the Static Web App origin.
- **GitHub secrets the workflows need:**
  - `MEAL_IMPORT_FUNCTION_KEY`;
  - `SUPABASE_ACCESS_TOKEN`, `PRODUCTION_DB_PASSWORD`, `PRODUCTION_PROJECT_ID`;
  - `AZURE_STATIC_WEB_APPS_API_TOKEN_*`;
  - `AZUREAPPSERVICE_*` (passwordless Azure login, trusted for `refs/heads/main` only; don't add `environment:`).
- New production values are set by the user; never commit them. Azure CLI isn't installed here, so give the user
  the portal steps or the `az` command.

## Debugging production
- **Supabase MCP** (`execute_sql`, `query_logs`) is connected to the production project `mofjrqkaxacqtkftbuzo`.
  Read-only queries are fine; writes and deletes need the user's explicit OK, and they may prefer to run them
  themselves.
- **API logs:** `source = 'edge_logs'`, filtered on `log_attributes['request.path']`. Server-side calls (the
  Function) have an empty user agent. `log_attributes['unauthorized_hint.error_code']` explains 401s.
- **Function:** `POST https://mealwizfunctions-avgsexb3a5f6d7g4.northeurope-01.azurewebsites.net/api/ScrapeRecipe`
  with no key should return 401, which shows it's deployed and protected. A CORS preflight from the site origin
  should return 204 with that origin.
- **Site:** https://ashy-hill-0f87a3a10.1.azurestaticapps.net. Its `appsettings.Production.json` is public, so you
  can check the injected settings are present, without printing the key.

## Windows shell pitfalls
- **PowerShell 5.1:**
  - No `&&`; use `;` or `if ($?) { … }`.
  - It mangles embedded double quotes passed to native exes. Write commit messages and PR bodies to a scratch file
    and use `git commit -F` / `gh pr create --body-file`.
  - `Set-Content -Encoding utf8` writes a BOM; prefer the Write tool.
  - Native stderr shows up as errors (`git switch` "Switched to…" isn't a failure).
- **Git Bash:** `git show origin/dev:path/to/file` gets its argument rewritten into a Windows path. Prefix with
  `MSYS_NO_PATHCONV=1`.
- **jq:** complex `--jq` expressions break under PowerShell quoting; run those `gh` commands from Bash.
- **No Python or actionlint locally.** Workflow YAML is only checked when GitHub runs it, and `deploy.yml` only runs
  on `main`.

## Conventions
- **Vertical slices** under `MealWiz/MealWiz/MealWiz.Shared/Features/<Feature>/`, following
  `.claude/docs/architectural_patterns.md`:
  - a static CQRS class with a nested record + Handler;
  - `Result.Try` around Supabase calls;
  - Model/ModelDb pairs;
  - state containers.
- **Naming:** use "Meal" naming, consistent with the existing features. UI text too.
- **DI:** new services go in `Helpers/DependencyInjectionHelper.cs`, `MauiWASM/Program.cs` and
  `MealWiz/MealWiz/MauiProgram.cs`, which registers by hand.
- **Tests:** `MealWiz/tests/<Project>.Tests`, laid out as `Features/<Feature>/UnitTests|IntegrationTests`
  (Shared) or `Functions/<Function>/…` and `Shared/…` (Functions).
