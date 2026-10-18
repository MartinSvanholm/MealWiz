# MealWiz.Functions

Standalone Azure Functions app (isolated worker, .NET 10) implementing `ScrapeRecipe` — the scrape/parse/write-back
step of the "import a meal from a URL" flow ([MEA-13](https://linear.app/mealwiz/issue/MEA-13)). Deliberately does
**not** reference `MealWiz.Shared`.

## Local development

1. Copy `local.settings.json.example` to `local.settings.json` and fill in `Supabase:ServiceRoleKey` (`supabase status`
   after starting the local stack prints it as `service_role key`). `local.settings.json` is gitignored — never commit it.
   Keep its `Host.CORS` entry: it lets the Blazor WASM dev server (`http://localhost:5053` / `https://localhost:7033`,
   from `MauiWASM/Properties/launchSettings.json`) call `http://localhost:7071` from the browser. Restart the host after
   changing it.
2. Start the local Supabase stack from the repo root:
   ```
   supabase start
   ```
3. Start the Function host from this directory:
   ```
   func start
   ```
4. Insert a `meal_imports` row (via the app, or directly against local Supabase) and POST to `ScrapeRecipe` with its
   `id` as `jobId`:
   ```
   POST http://localhost:7071/api/ScrapeRecipe
   { "jobId": "<meal_imports.id>", "url": "https://example.com/some-recipe" }
   ```
   The function never reports success/failure through the HTTP response — check the `meal_imports` row's
   `status`/`parsed_meal`/`error_message` columns (or subscribe via Realtime, same as the client does).

Core Tools doesn't enforce function keys locally, so no `x-functions-key` header is needed here. That's why
`MealImportFunction:key` is empty in the clients' `appsettings.Development.json`.

## Auth and CORS ([MEA-16](https://linear.app/mealwiz/issue/MEA-16))

### Function key: not a true secret

`ScrapeRecipe` uses `AuthorizationLevel.Function`, and the Blazor client sends the key as `x-functions-key`
(`MealImportFunction:key` in its appsettings). **The key is not a secret.** The WASM app downloads
`appsettings.Production.json` as a plain file, so anyone can read the key from the deployed site. It only stops
casual or drive-by calls. Assume anyone can call the function and trigger a scrape for a known `jobId`. Proper per-user
auth is the [hardening path](#future-hardening-supabase-jwt) below.

The key is still kept out of git, so it can be rotated without a commit:

- `MauiWASM/wwwroot/appsettings.Production.json` commits the Function `url` with an empty `key`.
- The Static Web Apps workflow (`.github/workflows/azure-static-web-apps-*.yml`) writes the `MEAL_IMPORT_FUNCTION_KEY`
  GitHub secret into that file before deploying, including on rollback. The deploy fails if the secret isn't set.

The client uses a dedicated host-level key named `blazor-client` on `MealWizFunctions` (resource group `MealWiz`), so it
can be rotated without touching the `default` key. It's host-level rather than scoped to `ScrapeRecipe` because the
function only exists after the push to `main` that also deploys the Static Web App, and that deploy already needs the
secret. A host-level function key is accepted by every `AuthorizationLevel.Function` function on the app.

To create or rotate it, generate a new value, then copy it into the secret. `keys set` redacts its output, so read
the value back with `keys list`:

```
az functionapp keys set -g MealWiz -n MealWizFunctions --key-type functionKeys --key-name blazor-client
az functionapp keys list -g MealWiz -n MealWizFunctions --query "functionKeys.\"blazor-client\"" -o tsv
```

Store that value as the `MEAL_IMPORT_FUNCTION_KEY` repository secret, then re-run the Static Web Apps workflow
(it has `workflow_dispatch`).

The Staging appsettings files and the MAUI Production/Staging files leave `url`/`key` empty because nothing loads them
yet. On Static Web Apps, the WASM app always runs as `Production` (no `Blazor-Environment` header is set), and
`MauiProgram.cs` always loads `appsettings.Development.json`.

### CORS

The browser calls the function cross-origin, with `Content-Type: application/json` and `x-functions-key`, so every
call starts with an `OPTIONS` preflight.

- **Locally**: `Host.CORS` in `local.settings.json` (see above). It only applies to Core Tools.
- **In Azure**: configure CORS on the Function App itself (Portal → API → CORS, or the CLI). Platform CORS answers the
  preflight before the request reaches the functions host, so `ScrapeRecipe` only needs to accept `post`, and the
  preflight doesn't need a key.

```
az functionapp cors add -g <resource-group> -n <function-app> --allowed-origins https://<static-web-app-host> http://localhost:5053 https://localhost:7033
az functionapp cors show -g <resource-group> -n <function-app>
```

`<static-web-app-host>` is the Static Web App's hostname (default `*.azurestaticapps.net`, or a custom domain), with no
trailing slash. The localhost origins only let a local dev build call the deployed function, so they can be left out.
The MAUI app is unaffected by CORS: its `HttpClient` is native, not the browser's.

### Future hardening: Supabase JWT

Not implemented. This would replace the function key with real per-user auth:

1. The client sends the signed-in user's Supabase access token as `Authorization: Bearer <token>` with the
   `ScrapeRecipe` call.
2. The function validates it against Supabase's JWKS, `https://<project-ref>.supabase.co/auth/v1/.well-known/jwks.json`.
   It checks the signature, `exp`, `iss` (`https://<project-ref>.supabase.co/auth/v1`) and `aud` (`authenticated`). This
   requires the project to use asymmetric JWT signing keys. Projects on the legacy HS256 secret would validate with the
   shared JWT secret instead.
3. After loading the `meal_imports` row, it rejects the call unless the token's `sub` equals the row's `created_by`.
4. The trigger can then drop to `AuthorizationLevel.Anonymous`, and the key, the GitHub secret and the workflow step
   can go away.

## Tests

Tests live in `MealWiz/tests/MealWiz.Functions.Tests`, mirroring this project's layout with a folder per test type:
`Functions/<FunctionName>/UnitTests`, `Functions/<FunctionName>/IntegrationTests`, `Shared/UnitTests`,
`Shared/IntegrationTests`. They use xUnit v3 on Microsoft.Testing.Platform (opted in via the repo-root `global.json`).

Run everything from the repo root:
```
dotnet test --project MealWiz/tests/MealWiz.Functions.Tests
```

Unit tests only:
```
dotnet test --project MealWiz/tests/MealWiz.Functions.Tests -- --filter-not-trait "Category=Integration"
```

Integration tests scrape a local web server started by the tests (never a live website). The ones that write to
`meal_imports` also need local Supabase (`supabase start`); without it they're skipped with the reason. Credentials
come from `SUPABASE_URL` / `SUPABASE_SERVICE_ROLE_KEY` if set, otherwise from `supabase status -o env`. Rows they
create are deleted afterwards.
