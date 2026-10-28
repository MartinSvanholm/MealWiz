# Architecture rules

These rules are strict and checkable. Agents follow them when they write code and check them when they review a
change. [FEATURES.md](FEATURES.md) says where code lives; [TESTING.md](TESTING.md) covers tests.

## How to use this file

- **Implementing:** follow every MUST rule. Break a SHOULD rule only for a reason you can state in the PR.
- **Reviewing:** check each changed file against the rules. Cite the rule ID and `file:line` for each finding.
  - A broken MUST rule is 🔴 **Important**: fix it before merging.
  - A broken SHOULD rule is 🟡 **Nit**.
  - Code the PR didn't touch that breaks a rule is 🟣 **Pre-existing**. Mention it; don't block on it.
  - Report a rule as broken only when you can point at the line that breaks it.
- **Rule shape:** each rule has a one-line statement, **Why**, **Violation** (what breaking it looks like) and
  **Check** (how to spot it in a diff).
- **Changing a rule:** do it in its own PR, and say why.

Paths: `Features/…`, `Helpers/…` and `Services/…` are under `MealWiz/MealWiz/MealWiz.Shared/`.

---

## Structure

### ARCH-01 · MUST · Vertical slice layout

Each feature is a folder under `Features/<Feature>/`. An operation (one command or query) gets its own folder
`Features/<Feature>/<Operation>/`. The handler file, the folder and the static class all share the operation's name,
e.g. `Features/Meals/SaveMeal/SaveMeal.cs`. Components that belong only to that operation go in the same folder.
Models go in `Features/<Feature>/Models/`, and state containers in `Features/<Feature>/State/`. A new feature's
namespaces are added to `MealWiz/MealWiz/MealWiz.Shared/_Imports.razor`.

- **Why:** an agent or a person finds every piece of a feature in one folder, and the feature map stays accurate.
- **Violation:** a handler in a new Helpers/MealHelpers.cs. A new Features/Meals/Handlers/ folder holding several
  operations. `SaveMeal.cs` whose class is called `SaveMealHandler`.
- **Check:** new `.cs` files with `IRequestHandler` outside `Features/<Feature>/<Operation>/<Operation>.cs`.

### ARCH-02 · MUST · No cross-feature handler use

A feature never constructs, injects or subclasses another feature's `Handler` class. To use another feature's data,
send that feature's request through MediatR or call its state-container interface. Domain models may be shared
across features; `GroceryItem` builds from `Ingredient`, and `MealPlan` holds `Meal`s.

- **Why:** handlers are resolved only through MediatR. Calling one directly skips the pipeline, couples the two
  features' internals, and breaks when the handler's dependencies change.
- **Violation:** `new SaveMeal.Handler(client).Handle(...)` inside a MealImport class. A component injecting
  `GetAllMeals.Handler`.
- **Check:** `.Handler(` or `.Handler>` that refers to a handler from another feature folder.

### ARCH-03 · MUST · The Supabase `Client` is used only in handlers

Only MediatR handlers inject or use `Supabase.Client`. The exceptions are these, and no others:

- `Services/Authentication/CustomAuthStateProvider.cs`, which reads the session
- `Features/MealImport/State/MealImportRealtimeSubscriber.cs`, which owns the Realtime channel
- the host DI setup that creates the client, in `MealWiz/MauiWASM/ServiceCollectionExtensions.cs` and
  `MealWiz/MealWiz/MealWiz/MauiProgram.cs`

Adding to this list needs a rule change.

- **Why:** all data access goes through handlers that return `Result`s, so errors are handled one way, tests can mock
  `IMediator`, and RLS-dependent queries live in one reviewable place.
- **Violation:** `@inject Client _client` in a `.razor` file, even if unused. A state container calling
  `client.From<MealDb>()`.
- **Check:** `Client` in an `@inject` line, or a constructor parameter in a `.razor` or non-handler `.cs` file.

## Handlers and errors

### ARCH-04 · MUST · Handler shape

A handler is a `public static class <Operation>` that contains:

- `public record Command(...)` or `public record Query(...)`, implementing `IRequest<Result>` or `IRequest<Result<T>>`
- `public class Handler(<dependencies>) : IRequestHandler<Command|Query, Result…>`, with its dependencies taken
  through the primary constructor

Commands change data; queries only read it. A handler returns FluentResults types only.

- **Why:** one shape means one way to read, call, mock (`SetupSend<TReq,TResp>`) and test every operation.
- **Violation:** a handler returning `Task<Meal>` or `Task<bool>`. A query that inserts. A separate
  `SaveMealCommand.cs` and `SaveMealHandler.cs`. Dependencies set through properties.
- **Check:** new handlers whose request isn't `IRequest<Result…>`, or that aren't nested in a static class.

### ARCH-05 · MUST · No exceptions escape a handler

Every call that can throw (Supabase, HTTP, parsing) runs inside `Result.Try(...)`. Handlers contain no `try`/`catch`
and no `throw`. Read the current user's id with a guard that fails, never with a conversion that can throw:

```csharp
var userId = supabaseClient.Auth.CurrentSession?.User?.Id;
if (userId is null) return Result.Fail(ErrorMessages.NotSignedIn);
mealImportDb.CreatedBy = new Guid(userId);
```

- **Why:** callers rely on getting a `Result`. One escaped exception crashes the component, or forces every caller
  to add its own `try`/`catch`. `ResultHelper.SetDefaultCatchHandler()` already turns Supabase exceptions into
  user-safe messages inside `Result.Try`.
- **Violation:** `new Guid(supabaseClient.Auth.CurrentSession?.User.Id)` without a null check, which throws when
  signed out. `await client.From<X>().Insert(x)` outside `Result.Try`. `throw new InvalidOperationException(...)`.
- **Check:** in a handler, any `await` on Supabase or HTTP that isn't wrapped in `Result.Try`, any `throw`, `catch`,
  or `new Guid(` applied to a value that may be null.

### ARCH-06 · MUST · The UI reports failures through `Handle`

Code that sends a request and shows its outcome reports a failure with `result.Handle(snackbar)` and stops:

```csharp
if (result.Handle(CurrentSnackbar).IsFailed) return;
```

Pages and components don't wrap mediator calls in `try`/`catch`, and never discard a failed `Result` silently.
Success snackbars may be added directly. One exception: code that runs in the background, fire-and-forget, may have
a last-resort `catch (Exception)` that turns the exception into a user-visible failure, as `MealImportStateContainer`
does. Without it the exception would be lost.

- **Why:** users always see why something failed, and the UI never updates as if a failed operation had succeeded.
- **Violation:** `try { await _mediator.Send(...) } catch (Exception) { }`. An empty `catch` that rethrows. Removing
  an item from the screen without checking the delete's `Result`.
- **Check:** `catch` in `.razor` files. A `mediator.Send`/`sender.Send` result that is ignored or only partly checked.

### ARCH-07 · MUST · Domain and database models are separate

- Every Postgrest model is `public class XxxDb : BaseModel` with `[Table]`, `[PrimaryKey]` and `[Column]`, and lives
  in `Features/<Feature>/Models/`.
- Mapping lives on the domain class: a constructor `Xxx(XxxDb db)` for reading, and `MapToXxxDb()` for writing.
  `GroceryItem.MapToDb()` predates the naming.
- Enums are stored as strings through `ToDbValue()` and `FromDbValue()` extensions; see `MealType` and
  `MealImportStatus`.
- Pages, components and state containers use only domain types. `*Db` types appear only in handlers, in `Models/`,
  and in `MealImportRealtimeSubscriber`.

- **Why:** the database schema can change without touching UI code, and Postgrest attributes and `BaseModel`
  behaviour stay out of UI state.
- **Violation:** a component binding to `MealDb`. A state container holding `List<GroceryItemDb>`. Mapping code
  written inline in a handler instead of on the model. `[Column]` attributes on a domain class.
- **Check:** `Db\b` type names in `.razor` files or under `Features/<Feature>/State/`. A new `BaseModel` subclass outside `Models/`.

### ARCH-08 · MUST · Services are registered in one place

Every Shared service or state container is registered in
`DependencyInjectionHelper.RegisterScopedServices()` (`Helpers/DependencyInjectionHelper.cs`). Host-specific
services, such as the Supabase client, configuration options and the session provider, go in
`MealWiz/MauiWASM/ServiceCollectionExtensions.cs`. Handlers need no registration because MediatR scans the Shared
assembly.

`MealWiz/tests/MauiWASM.Tests/IntegrationTests/DependencyInjection/ServiceRegistrationTests.cs` builds the web
container and resolves every handler and every component's `[Inject]` properties, so `verify fast` catches a missing
registration. A service that no component injects needs its own case there.

- **Why:** a service registered only in some host fails at runtime, on the first page that needs it. The registration
  test only covers the web host, so the shared list is what keeps the MAUI head in step.
- **Violation:** a new `IFooStateContainer` added only in `MealWiz/MauiWASM/ServiceCollectionExtensions.cs`, or only
  in `MealWiz/MealWiz/MealWiz/MauiProgram.cs`.
- **Check:** a new interface or implementation pair with no line in `DependencyInjectionHelper`.

## Database

### DB-01 · MUST · Every table has RLS, and RLS has tests

Every new table:

1. has `created_by uuid` referencing `auth.users(id)` `on delete cascade`
2. enables row level security in the same migration
3. has policies limited to `auth.uid() = created_by` for each operation the client needs
4. gets `supabase/tests/database/<table>.test.sql`, and is added to `supabase/tests/database/00_rls_enabled.test.sql`
   (`tables_are` and `policies_are`)

A policy wider than owner-only needs a SQL comment in the migration that explains why. Existing examples are system
meals and the `meal_imports` fail-only update. Handlers rely on RLS for ownership and don't filter by user.

- **Why:** the client uses the public anon key, so RLS is the only thing between one user and another's data.
- **Violation:** `create table public.notes (...)` with no `enable row level security`. A policy
  `using (true)` with no comment. A new table missing from `00_rls_enabled.test.sql`, which then fails `verify full`.
- **Check:** every `create table` or `create policy` in a migration diff has matching pgTAP changes in the same PR.

### DB-02 · MUST · Migration conventions

- Each schema change is a new file `supabase/migrations/<YYYYMMDDHHMMSS>_<snake_case_description>.sql`.
- Never edit a migration that has reached `main`; production has already applied it.
- Never run `supabase db push` by hand. `.github/workflows/deploy.yml` pushes migrations to production on every push
  to `main`.
- Data for local development and tests goes in `supabase/seed.sql`. System rows the app needs in production go in a
  migration.
- Apply a migration locally with `supabase db reset`, which wipes local data and re-runs the seed.

- **Why:** an edited migration never runs again on production, so production and the repo silently diverge. A manual
  push skips review.
- **Violation:** changing a column in `20260505135956_remote_schema.sql`. A file named `add_notes.sql`. Test users
  inserted by a migration.
- **Check:** a diff that changes an existing file under `supabase/migrations/`, or adds a file whose name doesn't
  match the pattern.

## Functions

### FN-01 · MUST · Duplicated contracts change together

`MealWiz.Functions` deliberately doesn't reference `MealWiz.Shared`, so the `meal_imports` types exist twice:

- Functions: `MealWiz/src/MealWiz.Functions/Shared/Models/`
- Shared: `Features/MealImport/Models/`

A change to the table, the `parsed_meal` shape or the status values updates both sides, plus
`MealWiz/tests/Contracts/parsed_meal.json` or `MealWiz/tests/Contracts/meal_import_status.json`, in the same PR.

- **Why:** the two apps deploy separately and talk only through the row. The contract files make each side's tests
  fail until the shapes match.
- **Violation:** adding `servings` to `MealImportPayload` without adding it to `ParsedMeal` and `parsed_meal.json`.
  Making Functions reference Shared "to share the model".
- **Check:** a diff touching one side's models or the `meal_imports` table without the other side and the contract
  JSON.

### FN-02 · MUST · Fetch user-supplied URLs only through the SSRF guard

In Functions, every request to a URL that came from a user, or from a page you fetched, goes through `RecipeFetcher`
and `SsrfGuardedHttpClientFactory` (`MealWiz/src/MealWiz.Functions/Shared/Ssrf/`). Never create a bare `HttpClient`
for it, never turn on `AllowAutoRedirect`, and never skip `IpAddressGuard`. Tests that need loopback use
`LoopbackAllowingIpAddressGuard`.

- **Why:** the Function runs inside Azure with the service-role key. An unguarded fetch can reach cloud metadata or
  internal services, including through a redirect.
- **Violation:** `new HttpClient().GetStringAsync(row.SourceUrl)`. `new HttpClientHandler { AllowAutoRedirect = true }`.
- **Check:** `HttpClient` or `HttpClientHandler` created in Functions code outside `Shared/Ssrf/` or `Shared/Fetching/`.

## Security

### SEC-01 · MUST · Only the anon key reaches the client

The client projects (Shared, MauiWASM and the MAUI head) use only the public anon or publishable key. The
service-role or secret key is used only in Functions, through `ServiceRoleSupabaseClientFactory`, and in test
fixtures. Committed appsettings hold no secrets: the Function key in `appsettings.Production.json` stays empty and is
injected by `deploy.yml`, and `local.settings.json` is gitignored.

- **Why:** everything in the WASM app is downloadable by anyone. A service-role key there would bypass all RLS.
- **Violation:** a `ServiceRoleKey` setting in `MealWiz/MauiWASM/wwwroot/appsettings.Production.json`. An `sb_secret_…`
  or service-role JWT anywhere outside tests and Function app settings.
- **Check:** new keys in `wwwroot/appsettings.*.json` or `Resources/appsettings.*.json`; `service_role` or
  `sb_secret` in non-test code.

## Tests

### TEST-01 · MUST · One test project per app project

Tests go in the existing project for the code under test:

| Code under test | Test project |
|---|---|
| `MealWiz.Shared` | `MealWiz/tests/MealWiz.Shared.Tests/` |
| `MealWiz.Functions` | `MealWiz/tests/MealWiz.Functions.Tests/` |
| `MauiWASM`, plus E2E | `MealWiz/tests/MauiWASM.Tests/` |
| database | `supabase/tests/database/` |

Never add another test project. Test folders mirror the source path, with a folder per test type (`UnitTests`,
`ComponentTests`, `IntegrationTests`, `E2ETests`). Tests that need Supabase or a browser carry the right
`[Trait("Category", …)]` (see [TESTING.md](TESTING.md#traits)).

- **Why:** verify and CI run a fixed list of projects and traits. A new project or a missing trait means tests that
  silently never run, or that run in the wrong suite.
- **Violation:** a new `MealWiz.Shared.IntegrationTests.csproj`. An integration test class with no
  `Category=Integration` trait: it lands in the unit suite, which then skips it at `fast`. A Meals test placed under `Features/GroceryList/`.
- **Check:** a new `.csproj` under `MealWiz/tests/`. A new test class that uses `LocalSupabaseFixture` or
  `E2ETestBase` but has no trait.

### TEST-02 · MUST · Tests ship with the change

| Change | Required test |
|---|---|
| New or changed handler | An integration test against local Supabase |
| Component with logic (branches, calls, state changes) | A bUnit test |
| State container, model mapping, pure helper | A unit test |
| New table or policy | pgTAP (DB-01) |
| Bug fix | A regression test that fails without the fix |

A known bug you're not fixing is pinned by a test that asserts today's behaviour and names the Linear issue at the
spot. When the bug is fixed, flip the assertion rather than deleting the test.

- **Why:** verify is the only safety net agents have. Code without tests can't be checked by the next agent.
- **Violation:** a new `ArchiveMeal` handler with no test in `Features/Meals/IntegrationTests/`. A bug fix with no
  test. A `Skip = "flaky"` added to make a suite pass.
- **Check:** new handlers, components or migrations in the diff with no matching test file changes.

## UI

### UI-01 · MUST · Interactive elements have a `data-testid`

Every button, input, menu item, list row or other element that a user or a test acts on or reads has a
kebab-case `data-testid`, such as `save-meal-button` or `meal-list-item`. bUnit and Playwright tests select elements
only by `data-testid` (`TestId("x")`, `Page.GetByTestId("x")`), never by text, CSS class or MudBlazor's generated
markup.

- **Why:** MudBlazor's markup and the UI text change. Test ids keep tests stable and make new UI testable without
  changing the component again.
- **Violation:** `<MudButton OnClick="Save">Save</MudButton>` with no test id. A test using
  `Find(".mud-button-root")` or `GetByText("Save")`.
- **Check:** new interactive Mud components in `.razor` diffs without `data-testid`, and new selectors in tests that
  aren't test ids.

## Docs

### DOC-01 · MUST · Docs change with the code

A PR that adds, moves, renames or removes a feature, handler, page, table, test suite, verify step or a path
mentioned in the docs updates the matching doc ([FEATURES.md](FEATURES.md), [TESTING.md](TESTING.md),
[ARCHITECTURE.md](ARCHITECTURE.md), `CLAUDE.md`) in the same PR. The `docs` step of verify, which runs at every
level, fails on missing feature folders, handlers, tables and verify steps, and on paths in backticks that don't
exist. The rest is up to the author and the reviewer.

- **Why:** agents trust these files instead of searching. A stale entry sends them to the wrong place, and is worse
  than no entry.
- **Violation:** a new Features/Freezer/ folder with no FEATURES.md section. A renamed handler still listed under its old
  name.
- **Check:** `scripts/verify.ps1 fast` passes, and the diff touches the docs whenever it touches the things above.

---

## SHOULD rules

### S-01 · SHOULD · Naming

| Kind | Name |
|---|---|
| DB model | `XxxDb` |
| State container | `IXxxStateContainer` / `XxxStateContainer` |
| Action component | `<Verb><Thing>Button.razor` |
| Drawer content | `XxxDrawer.razor` |
| Page | `XxxPage.razor` |
| Test class | `<Subject>Tests` |
| Unit and integration test method | `Method_Scenario_Expected` |
| bUnit test method | a snake_case sentence |
| E2E test method | `Actor_Behaviour_AndMore` |

Namespaces are file-scoped and match the folder path.

- **Why:** names say what a file is without opening it. Violation: `MealRepository.cs`, `MealsStore`, `Test1`.

### S-02 · SHOULD · State container conventions

- A state container inherits `StateContainerBase`, declares its interface in the same file, is registered as scoped,
  and calls `NotifyStateChanged()` after every state change.
- Components that subscribe to `OnStateChanged` implement `IDisposable` and unsubscribe in `Dispose`.
- Events that can arrive off the UI thread (Realtime, timers) are handled with `() => InvokeAsync(StateHasChanged)`.
- The hosting page sets `CurrentSnackbar` before calling load methods.

- **Why:** a subscription that is never removed leaks the component and re-renders a page that's gone, and off-thread
  `StateHasChanged` throws on MAUI. Violation: `_state.OnStateChanged += StateHasChanged;` with no `Dispose`.

### S-03 · SHOULD · A state container only for shared state

Add a state container, or a method on one, only when more than one component needs the state, or it must outlive a
page. A component that just runs one operation and shows the result sends to `IMediator`/`ISender` directly; see
`SaveMealButton` and `LoginPage`.

- **Why:** a container per operation adds indirection, registrations and tests with no benefit. Violation: a new
  `ILoginStateContainer` that only wraps `Login.Command`.

---

## Patterns

Worked examples of the rules above, taken from the code.

### Handler (ARCH-04, ARCH-05, ARCH-07)

```csharp
// Features/MealImport/CreateMealImport/CreateMealImport.cs
public static class CreateMealImport
{
    public record Command(Guid Id, string SourceUrl) : IRequest<Result<Models.MealImport>>;

    public class Handler(Client supabaseClient) : IRequestHandler<Command, Result<Models.MealImport>>
    {
        public async Task<Result<Models.MealImport>> Handle(Command request, CancellationToken cancellationToken)
        {
            var mealImportDb = new Models.MealImport { Id = request.Id, SourceUrl = request.SourceUrl,
                Status = MealImportStatus.Pending }.MapToMealImportDb();

            var userId = supabaseClient.Auth.CurrentSession?.User?.Id;
            if (userId is null) return Result.Fail(ErrorMessages.NotSignedIn);
            mealImportDb.CreatedAt = DateTime.UtcNow;
            mealImportDb.CreatedBy = new Guid(userId);

            var insertResult = await Result.Try(async Task<ModeledResponse<MealImportDb>> () => await supabaseClient
                .From<MealImportDb>()
                .Insert(mealImportDb, cancellationToken: cancellationToken));

            return insertResult.Map(r => new Models.MealImport(r.Model ?? mealImportDb));
        }
    }
}
```

The static class has the same name as its namespace's last segment, so callers write
`new GetAllMeals.GetAllMeals.Query()` or alias it (`using CreateMealPlanAction = …CreateMealPlan.CreateMealPlan;`, as
in `Features/MealPlans/State/MealPlanStateContainer.cs`).

### Result handling (ARCH-05, ARCH-06)

- `Helpers/ResultHelper.cs` provides two things:
  - `SetDefaultCatchHandler()`, called once in `MealWiz/MauiWASM/Program.cs`. It maps `GotrueException` and
    `PostgrestException` caught by `Result.Try` to an `Error`. In Release builds the message is "No response from
    server"; in Debug it's Supabase's own message.
  - `Handle(snackbar)`, which shows the first error, or the first success message if there is one, and returns the
    same result.
- Attach a success message in a handler with `.WithSuccess("Meal removed")`. `Handle` shows it.
- Build failures with `Result.Fail("…")` or `Result.Fail(ErrorMessages.X)`.

### State container and subscription (S-02)

```csharp
// Features/GroceryList/State/GroceryListStateContainer.cs (shape)
public interface IGroceryListStateContainer
{
    GroceryList GroceryList { get; }
    event Action OnStateChanged;
    Task LoadGroceryItems();
}

public class GroceryListStateContainer(IMediator mediator) : StateContainerBase, IGroceryListStateContainer
{
    public async Task LoadGroceryItems()
    {
        var result = await mediator.Send(new GetAllGroceryItems.GetAllGroceryItems.Query());
        if (result.Handle(CurrentSnackbar).IsFailed) return;
        // update state, then:
        NotifyStateChanged();
    }
}
```

```razor
@implements IDisposable
@inject IGroceryListStateContainer _state
@inject ISnackbar _snackbar

@code {
    protected override async Task OnInitializedAsync()
    {
        _state.OnStateChanged += StateHasChanged;     // MealImport: () => InvokeAsync(StateHasChanged)
        _state.CurrentSnackbar = _snackbar;
        await _state.LoadGroceryItems();
    }

    public void Dispose() => _state.OnStateChanged -= StateHasChanged;
}
```

### Drawers

There is one global drawer, `IDrawerStateContainer` (`Services/DrawerStateContainer/`), rendered by
`Components/Drawers/DrawerBase.razor` in `MainLayout`. Open it with a `DrawerStateParameters` object, using
`ContentParameters` keyed by the content component's parameter names:

```csharp
_drawerStateContainer.OpenDrawer(new DrawerStateParameters
{
    Content = typeof(GroceryItemDrawer),
    Title = "Add item",
    Height = "38%",
    ContentParameters = new() { { nameof(GroceryItemDrawer.GroceryItem), new GroceryItem() } }
});
```

For a confirmation, call `OpenConfirmDrawer(title, description, onConfirm)`. The container keeps `onConfirm` for that
one confirmation only: Confirm awaits it, and closing the confirm drawer (confirmed or dismissed) drops it. Call
`CloseDrawer()` at the end of `onConfirm`. `Features/GroceryList/DeleteGroceryItem/DeleteGroceryItemButton.razor`
is the reference. The overloads `OpenDrawer(string, Type)`, `OpenDrawer(RenderFragment, Type)` and
`SwitchDrawerContent` exist but nothing uses them.

### Auth, ownership and RLS (DB-01, SEC-01)

- The client signs in with Supabase Auth. `CustomAuthStateProvider` turns the session's access token into claims.
- Pages use `@attribute [Authorize]`, and `Layout/RedirectToLogin.razor` sends anonymous users to `/login`.
- Handlers set `created_by` on insert from the current session (ARCH-05 guard) and never filter reads by user; RLS
  returns only the caller's rows. The system meals are the deliberate exception (see FEATURES.md, Database).
- The Function uses the service role, so it has to check row state itself: it only resolves `pending` rows and only
  fetches the row's own `source_url`.

### Implicit `ChildContent`

Markup nested inside a component goes to its `RenderFragment` parameter named `ChildContent` without an explicit
tag. `Components/Tooltip.razor` and `Components/CustomNavLink.razor` spell the parameter `Childcontent`; Blazor
matches it case-insensitively, so nesting works:

```razor
<Tooltip Text="Add item">
    <MudIconButton ... />
</Tooltip>
```
