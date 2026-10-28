# E2ETests

Playwright tests that drive the **published** web app in headless Chromium against the local Supabase stack. This is the only level that catches broken JS interop, routing, session persistence in localStorage and Realtime.

## Run

```bash
supabase start
dotnet test --project MealWiz/tests/MauiWASM.Tests
```

That one command runs the whole project: the unit tests plus the E2E journeys. The first run takes about two minutes longer, because it publishes the app in Release (trimmed, like the deploy) and downloads Chromium. Later runs publish incrementally.

`scripts/verify.ps1 smoke` (or `verify.sh`) runs the Smoke subset together with the rest of the repo's tests and prints a short summary; `verify full` runs every journey. See "Verifying changes" in the root `CLAUDE.md`.

To pick which tests run (xunit.v3 filters on Microsoft.Testing.Platform):

| What | Arguments |
|------|-----------|
| Only the E2E tests | `--filter-trait "Category=E2E"` |
| Smoke subset (boot, every nav page, auth, meal CRUD, grocery list) | `--filter-trait "Category=Smoke"` |
| Everything except E2E (fast; never publishes) | `--filter-not-trait "Category=E2E"` |
| One journey | `--filter-class "*MealImportJourneyTests"` |

If Supabase isn't running, the E2E tests **skip**. With `REQUIRE_SUPABASE=true` (set in CI) they fail instead.

Environment variables:

| Variable | Effect |
|----------|--------|
| `SUPABASE_URL`, `SUPABASE_SERVICE_ROLE_KEY`, `SUPABASE_ANON_KEY` | Credentials for the stack. If unset, they're read from `supabase status -o env`. |
| `E2E_APP_DIR` | An existing publish output (the folder containing `wwwroot`). The publish step is skipped. |
| `E2E_HEADED=true` | Show the browser. |

## How it works (`TestInfrastructure/E2E`)

- **`E2EEnvironment`** is built once per run, on first use. It publishes MauiWASM, starts `E2EAppHost`, installs Chromium if it's missing and launches one browser.
- **`E2EAppHost`** is a Kestrel static-file host on a random loopback port with an `index.html` fallback. It sends `Blazor-Environment: Development` and serves `appsettings.Development.json` from memory, pointing the app at the local stack and at a same-origin `/functions` stub for the import Function.
- **`E2ESupabase`** provides GoTrue admin calls for throwaway users, plus service-role PostgREST calls to seed meals and to resolve meal imports.
- **`E2ETestBase`** gives every test its own user and its own browser context, so tests are isolated and classes run in parallel. When a test fails it saves `trace.zip`, `screenshot.png` and `console.log` to `bin/Debug/net10.0/TestResults/e2e/<Class.Method>/`, included in the `test-results` artifact when CI fails. Theory cases add their arguments, e.g. `SmokeTests.Page_Renders(MealsPage,_meals,meal-list-item)`. Open a trace with:

  ```bash
  pwsh MealWiz/tests/MauiWASM.Tests/bin/Debug/net10.0/playwright.ps1 show-trace <path>/trace.zip
  ```

- `console.log` also records the Supabase Realtime frames, minus heartbeats, so a missed update can be told apart from one the app ignored.

The import journey doesn't run the Functions app. The browser's call to `/functions/api/ScrapeRecipe` is intercepted and acknowledged, and the test writes the row's result with the service role, so the app can only learn the result over Realtime. Scraping is covered by MealWiz.Functions.Tests.

## Writing tests without flakes

- Wait only with Playwright's auto-waiting locators and `Expect(...)`. No `Task.Delay`, `WaitForTimeoutAsync` or load-state waits.
- Navigate with `GoToAsync(path)`, which waits for `app-shell` (Blazor has booted). Then wait for something on the page itself.
- Select elements by `data-testid` (`Page.GetByTestId`), never by MudBlazor's generated classes. Add a test id to the `.razor` file if one is missing.
- Look inside `Drawer` / `ConfirmDrawer`, not `Page`. A closed MudDrawer stays in the DOM off-screen.
- Fill MudTextFields with `FillAsync(locator, value)`, which blurs the field so the bound value updates.
- Never compute dates in a test. Read them from the page (e.g. `week-day` and its `data-date`) so timezone and midnight don't matter.
- Seed data through `Supabase` for the user under test (`User`), not through another journey's UI.

## Known bugs pinned by these tests

The tests assert today's behaviour and name the issue at the spot. Flip the assertion when the bug is fixed.

| Issue | Where |
|-------|-------|
| [MEA-38](https://linear.app/mealwiz/issue/MEA-38): `show_on_grocery_list` is never read | `GroceryListJourneyTests`: both plan ingredients are offered |
| [MEA-42](https://linear.app/mealwiz/issue/MEA-42): an item added by hand can't be checked off until a reload | `GroceryListJourneyTests`: the first tick gets a 403 |
