# Testing guide

This guide covers every test suite: what it covers, which `verify` level runs it, which tests to run for a given
change, and how to write a new test. To run verify and read or triage its results, use the verify skill
(`.claude/skills/verify/SKILL.md`). The rules this guide implements are TEST-01, TEST-02 and UI-01 in
[ARCHITECTURE.md](ARCHITECTURE.md).

## Suites at a glance

`scripts/verify.ps1 <level>` (or `scripts/verify.sh <level>`) runs these steps. Each level includes the one before it.

| Step | What it runs | Covers | Level | Needs |
|---|---|---|---|---|
| `docs` | the doc check in `scripts/verify.cs` | Feature folders, handlers and tables listed in FEATURES.md; verify steps listed here; paths in backticks in CLAUDE.md and `docs/agents/` exist | fast | nothing |
| `build` | `dotnet build` of the three test projects | Shared, MauiWASM, Functions, tests. Not the MAUI head | fast | .NET 9 + 10 SDKs |
| `shared-unit` | MealWiz.Shared.Tests, not `Category=Integration` | Models, state containers, helpers, services, handler logic without Supabase, bUnit components | fast | — |
| `functions-unit` | MealWiz.Functions.Tests, not `Category=Integration` | Parsers, mapper, ingredient parser, SSRF guard, contracts, function HTTP layer | fast | — |
| `web-unit` | MauiWASM.Tests, not `Category=E2E` | Appsettings, session provider, routes, DI registration | fast | — |
| `shared-integration` | MealWiz.Shared.Tests, `Category=Integration` | Every handler against local Supabase, through RLS | smoke | Docker + Supabase CLI |
| `functions-integration` | MealWiz.Functions.Tests, `Category=Integration` | `ScrapeRecipeHandler` end to end, `RecipeFetcher`, SSRF client factory | smoke | Docker (some classes) |
| `e2e-smoke` | MauiWASM.Tests, `Category=Smoke` | Boot, login, every nav page, meal CRUD, grocery list (9 cases) | smoke only | Docker + Chromium |
| `e2e` | MauiWASM.Tests, `Category=E2E` | All journeys, including meal import and meal plan | full | Docker + Chromium |
| `db` | `supabase test db` | pgTAP: RLS enabled, exact policy list, per-table access | full | Docker |

- **Exit code:** the verdict. 0 passed, 1 failed, 2 couldn't run.
- **Logs:** under `artifacts/verify/<level>/`.
- **Supabase:** at smoke and full, verify starts local Supabase if it isn't running and sets `REQUIRE_SUPABASE=true`,
  so a test that can't reach the stack fails instead of skipping. Without that variable (plain `dotnet test`, or
  `fast`), Supabase-backed tests skip with a reason.
- **CI:** `.github/workflows/CI.yml` runs `verify full`, one step per test project via `--only`, with coverage. It
  ends with `--check-complete`, which fails if a step never ran.

## Which tests for which change

Run `scripts/verify.ps1 smoke` before calling any app change done, and `full` where the table says so. While
iterating, run just the project or class you're touching:
`dotnet test --project MealWiz/tests/MealWiz.Shared.Tests --filter-class "*MealHandlerTests"`.

| You changed | Add or update | Run at least |
|---|---|---|
| A handler in `Features/` | Integration test in `Features/<F>/IntegrationTests/`. If it has pure logic, also a unit test in `Features/<F>/UnitTests/Handlers/` | smoke |
| A page or component | bUnit test in `Features/<F>/ComponentTests/` (or `Components/ComponentTests/`, `Layout/ComponentTests/`); `data-testid`s on new elements | smoke |
| A state container | Unit test in `Features/<F>/UnitTests/State/`, with `IMediator` mocked | fast, then smoke |
| A model or its mapping | Unit test in `Features/<F>/UnitTests/Models/` | fast, then smoke |
| A helper or service | Unit test in `Helpers/UnitTests/` or `Services/<Svc>/UnitTests/` | fast, then smoke |
| DI or a host (`MealWiz/MauiWASM/`) | MauiWASM.Tests unit tests; `ServiceRegistrationTests` covers registrations | smoke |
| A migration or RLS policy | pgTAP in `supabase/tests/database/`, plus `00_rls_enabled.test.sql` for new tables or policies; integration tests for affected handlers | **full** |
| Meal import, Realtime | Shared unit and integration tests for MealImport, plus `MealImportJourneyTests` | **full** |
| A meal plan flow | Shared tests plus `MealPlanJourneyTests` | **full** |
| Functions parsing or mapping | Unit tests in `MealWiz/tests/MealWiz.Functions.Tests/Functions/ScrapeRecipe/UnitTests/` | fast, then smoke |
| Functions fetching or SSRF | `MealWiz/tests/MealWiz.Functions.Tests/Shared/UnitTests/Ssrf/` and `Shared/IntegrationTests/` | smoke |
| The `meal_imports` shape or statuses | Both sides' models plus `MealWiz/tests/Contracts/*.json` (FN-01) | smoke |
| A user-visible flow end to end | An E2E journey or a step in an existing one; `Smoke` only if it fits the smoke list below | full |
| Docs only (`docs/agents/`, `CLAUDE.md`) | — | `scripts/verify.ps1 fast --only docs` |

## Traits

Traits are set on the class, never on a method, and decide which suite runs a test. The folder name doesn't.

| Trait | Meaning |
|---|---|
| none | Unit or bUnit test. Runs in the `*-unit` suite and must not need Supabase, network or a browser |
| `[Trait("Category", "Integration")]` | Needs local Supabase or a local web server. Shared and Functions only |
| `[Trait("Category", "E2E")]` | Playwright, MauiWASM.Tests only |
| `[Trait("Category", "E2E")]` + `[Trait("Category", "Smoke")]` | Also part of `e2e-smoke` |

`MealWiz/tests/MauiWASM.Tests/IntegrationTests/` has no trait on purpose. `ServiceRegistrationTests` needs no
Supabase, so it runs in `web-unit`.

**Smoke criteria:** a journey gets `Smoke` only if it proves the app is basically usable: it boots without browser
errors, login works, every nav page renders, meal CRUD works, and the grocery list works. Keep the smoke set to a few
minutes. Everything else is `E2E` only.

## The suites in detail

### MealWiz.Shared.Tests: unit, bUnit, integration

`MealWiz/tests/MealWiz.Shared.Tests/` is xUnit v3 on Microsoft.Testing.Platform (net10.0), with bUnit and Moq.
`MealWiz.Shared` grants it `InternalsVisibleTo`.

**Layout.** It mirrors MealWiz.Shared:

```
Features/<Feature>/UnitTests/{Models,State,Handlers}/
Features/<Feature>/ComponentTests/
Features/<Feature>/IntegrationTests/<Feature>HandlerTests.cs
Components/ComponentTests/   Layout/ComponentTests/   Helpers/UnitTests/   Services/<Svc>/UnitTests/
TestInfrastructure/
```

**Infrastructure** (`MealWiz/tests/MealWiz.Shared.Tests/TestInfrastructure/`):

- `ComponentTestBase.cs` is the base class for every bUnit test.
  - It mocks `IMediator` (`Mediator`, `SetupSend<TReq,TResp>(response)`, `Sent<T>()`, `SentRequests`),
    `ILocalStorageService`, `IMealImportStateContainer` and `IThemeService`.
  - It uses the real Meals, GroceryList, MealPlan and Drawer containers, running on the mocked mediator.
  - Auth starts anonymous (`Auth`).
  - Helpers: `TestId("x")` and `InputOf("x")` selectors, `RenderWithPopovers`, `RenderDrawers()` + `Confirm()`,
    `SnackbarMessages`, `CurrentPath`.
- `LocalSupabaseFixture.cs` is shared per class through `IClassFixture`.
  - It creates a throwaway user (`shared-tests-<guid>@mealwiz.test`) and signs it in with the **anon key**, so every
    query goes through RLS.
  - It exposes `Client`, `UserId`, `GetOtherUserAsync()` (a second user, for cross-user checks),
    `CreateSignedInUserAsync()`, `CreateSignedOutClientAsync()`, `IsAvailable` and `SkipReason`.
  - The users are deleted at the end, and their rows cascade away with them.
- `TestInfrastructure/Builders/` holds `MealBuilder`, `IngredientBuilder`, `GroceryItemBuilder` and `MealPlanBuilder`. Each has fluent
  `WithX()` methods; `Build()` makes an unsaved model, and `InsertAsync(client)` inserts it as that client's user.
  `ClientExtensions` adds `CurrentUserId()` and `UniqueSuffix()`.
- `TestData.cs` provides in-memory models for unit and bUnit tests.
- `ResultAssert.cs`: `Ok(result)` returns the value; `Failed(result[, message])`. A failure message includes the errors.
- `TimeAssert.cs`: `CloseTo(expected, actual)`, with one minute of tolerance.
- `AuthTestHelper.cs`: `RoutedPage(typeof(Page))` renders a page with the real `[Authorize]` wiring.

**Integration test shape:**

```csharp
[Trait("Category", "Integration")]
public class MealHandlerTests(LocalSupabaseFixture supabase) : IClassFixture<LocalSupabaseFixture>
{
    [Fact]
    public async Task SaveMeal_NewMeal_InsertsItOwnedByTheUser()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var meal = new MealBuilder().WithName("Lasagne").Build();
        var saved = ResultAssert.Ok(await new SaveMeal.Handler(supabase.Client).Handle(new(meal), CancellationToken));
        // assert on the returned value and on the row read back with supabase.Client
    }
}
```

Tests in a class share its user, so each test only looks at rows it created. A full run signs in about 15 times.
GoTrue allows 300 sign-ins per 5 minutes locally (`supabase/config.toml`), but back-to-back runs together with E2E
can still hit a `429`. Run one verify at a time.

### MealWiz.Functions.Tests

`MealWiz/tests/MealWiz.Functions.Tests/` is xUnit v3, references `MealWiz/src/MealWiz.Functions/`, and has a
framework reference to ASP.NET Core.

**Layout:**

```
Functions/ScrapeRecipe/{UnitTests,IntegrationTests}/
Shared/{UnitTests,IntegrationTests}/<Area>/     (Fetching, Ingredients, Models, Ssrf, SupabaseAccess)
UnitTests/                                      (ServiceCollectionExtensions)
TestInfrastructure/
```

**Infrastructure:**

- `TestInfrastructure/LocalSupabaseFixture.cs` is a separate copy from the Shared one. It uses the **service role**,
  like the Function, and inserts pending `meal_imports` rows owned by the seeded user (`InsertPendingRowAsync`). Rows
  are deleted at the end.
- `TestInfrastructure/RecipeTestServer.cs` is a Kestrel server on a random loopback port that serves recipe pages.
  Tests never scrape live sites.
- `TestInfrastructure/LoopbackAllowingIpAddressGuard.cs` wraps the real `IpAddressGuard` so tests can reach 127.0.0.1
  while everything else is still checked.

**Integration tests:** `RecipeFetcherTests` and `SsrfGuardedHttpClientFactoryTests` are tagged Integration because
they open sockets, but they don't need Supabase. `ScrapeRecipeHandlerTests` needs Supabase.

**Contract tests:** `Shared/UnitTests/Models/ContractTests.cs` (here) and
`MealWiz/tests/MealWiz.Shared.Tests/Features/MealImport/UnitTests/Models/MealImportModelTests.cs` (Shared side)
compare each side's types with `MealWiz/tests/Contracts/*.json`. Both projects link these files.

Test methods are named `Scenario_Expected`, e.g. `RedirectToCloudMetadataEndpoint_WritesFailedStatus`.

### MauiWASM.Tests: unit and Playwright E2E

`MealWiz/tests/MauiWASM.Tests/` uses xUnit v3, bUnit and Microsoft.Playwright, with Chromium only.

- **Unit tests:**
  - `UnitTests/Configuration/AppSettingsTests.cs`: the appsettings files.
  - `UnitTests/Provider/CustomSupabaseSessionProviderTests.cs`: the session provider.
  - `UnitTests/Routing/RouteTests.cs`: the routes.
  - `IntegrationTests/DependencyInjection/ServiceRegistrationTests.cs`: builds the real web container, and resolves
    every handler and every component's injected services.
- **E2E** (`E2ETests/`): drives the **published** app (Release, trimmed) in headless Chromium against local Supabase.
  It is the only level that catches broken JS interop, routing, session persistence and Realtime. The journeys are
  `SmokeTests`, `AuthJourneyTests`, `MealJourneyTests`, `GroceryListJourneyTests`, `MealPlanJourneyTests` and
  `MealImportJourneyTests`.
- **E2E infrastructure** (`TestInfrastructure/E2E/`):
  - `E2EEnvironment` publishes the app once per run, hosts it with `E2EAppHost` (which serves a Development
    appsettings pointing at local Supabase, plus a `/functions` stub), and launches the browser.
  - `E2ETestBase` gives each test its own user and browser context. Helpers: `GoToAsync`, `LoginAsync`, `FillAsync`,
    `Drawer`, `ConfirmDrawer`, `Row`, `Expect`.
  - `E2ESupabase` seeds data and resolves imports with the service role.
  - When a test fails, `trace.zip`, `screenshot.png` and `console.log` are saved under
    `MealWiz/tests/MauiWASM.Tests/bin/Debug/net10.0/TestResults/e2e/<Class.Method>/`.
- **Rules for E2E tests** are in `MealWiz/tests/MauiWASM.Tests/E2ETests/README.md`. Read it before writing one:
  - no sleeps or load-state waits
  - `data-testid` selectors only
  - look inside `Drawer`, not `Page`
  - never compute dates in a test
  - seed data through `Supabase`, not another journey's UI
- **The import journey** doesn't run the Functions app. It intercepts the call to the Function and writes the result
  row itself.

### pgTAP: `supabase/tests/database/`

There's one file per table (`meals`, `ingredients`, `meal_plans`, `meal_plans_meals`, `grocery_items`,
`meal_imports`), plus `00_rls_enabled.test.sql`, which pins the exact table list, RLS-on, and each table's policy
names. Each file follows the same pattern:

```sql
begin;
create extension if not exists pgtap with schema extensions;
select plan(14);
-- fixture rows as postgres (hard-coded uuids in auth.users, ids from 900001)
set local role authenticated;
select set_config('request.jwt.claims', '{"sub":"<uuid>","role":"authenticated"}', true);
select results_eq(...);  select lives_ok(...);  select throws_ok(..., '42501', ...);
select * from finish();
rollback;
```

Postgres applies a table's SELECT policy to UPDATE and DELETE statements that have a WHERE clause. So loosening only
an UPDATE or DELETE policy may not change effective access, and `policies_are` is what catches the change.

## Writing a new test

1. **Pick the project and folder.** Use the table under TEST-01 in [ARCHITECTURE.md](ARCHITECTURE.md), then mirror
   the source path. Never create a new test project.
2. **Name it.**
   - The class is `<Subject>Tests`.
   - Unit and integration methods are `Method_Scenario_Expected`.
   - bUnit methods are snake_case sentences, e.g. `Saves_the_meal_then_its_pending_ingredients_and_returns_to_meals`.
   - E2E methods are `Actor_Behaviour_AndMore`.
3. **Tag it** with the right trait (see [Traits](#traits)). Start every Supabase-backed test with
   `Assert.SkipUnless(supabase.IsAvailable, …)`.
4. **Use the infrastructure.** Use the builders, `TestData`, `ResultAssert` and `ComponentTestBase` rather than
   writing new setup. Select by `data-testid`; if an element has none, add one to the `.razor` file.
5. **Isolate it.** Integration and E2E tests create their own users and rows; never depend on another test's data or
   on the seeded `test@test.com`. Tests run in parallel across classes.
6. **Pin known bugs, don't hide them.** If the code has a bug you aren't fixing, assert today's behaviour and say so
   at the spot:

   ```csharp
   // Pins current behaviour, see MEA-36: the update matches no rows, and `value.Model.Id` runs outside
   // Result.Try, so the handler throws instead of returning a failed result.
   await Assert.ThrowsAsync<NullReferenceException>(() => UpdateAsync(missing));
   ```

   File the bug in Linear first, so the comment can name it. When someone fixes it, they flip the assertion. Never
   use `Skip` to get a failing test out of the way.
7. **New table or policy:** add `supabase/tests/database/<table>.test.sql` and update `tables_are` / `policies_are`
   in `00_rls_enabled.test.sql`, which fails until you do.

### Bugs currently pinned by tests

| Issue | Pinned in |
|---|---|
| [MEA-35](https://linear.app/mealwiz/issue/MEA-35), [MEA-37](https://linear.app/mealwiz/issue/MEA-37) | `MealWiz/tests/MealWiz.Shared.Tests/Features/MealPlans/IntegrationTests/MealPlanHandlerTests.cs` |
| [MEA-39](https://linear.app/mealwiz/issue/MEA-39), [MEA-40](https://linear.app/mealwiz/issue/MEA-40) | `MealWiz/tests/MealWiz.Shared.Tests/Features/Meals/IntegrationTests/MealHandlerTests.cs` |
| [MEA-43](https://linear.app/mealwiz/issue/MEA-43) | `MealWiz/tests/MauiWASM.Tests/E2ETests/MealImportJourneyTests.cs` |

## Adding a test suite or verify step

New suites are rare. A new test project breaks TEST-01, so a new suite usually means a new trait filter on an
existing project.

1. Add the suite to the `suites` list in `scripts/verify.cs` (id, display name, project, filter, coverage flag).
2. Add its id to an `--only` list in `.github/workflows/CI.yml`. Until you do, the `--check-complete` step fails.
3. Add it to the [suites table](#suites-at-a-glance) above. Until you do, the `docs` step fails.
