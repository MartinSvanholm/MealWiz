# Feature map

Where each feature lives and how it works, so you can open the right files without searching.

- `Features/…` paths are relative to `MealWiz/MealWiz/MealWiz.Shared/`. Test paths are relative to
  `MealWiz/tests/MealWiz.Shared.Tests/` unless they name another project.
- Every handler is a static class holding a `Command` or `Query` record plus a nested `Handler`
  ([ARCHITECTURE.md](ARCHITECTURE.md), ARCH-04). The folder, the file and the class all share the operation's name.
- Every table has a `created_by uuid` owner column and RLS policies scoped to `auth.uid() = created_by`. Exceptions
  are noted per table under [Database](#database).
- Keep this file current: a PR that adds, moves or renames a feature, handler, page, table or test file updates it
  (DOC-01). `scripts/verify.ps1 fast` fails when a feature folder, handler or table is missing here, or when a path
  in backticks doesn't exist.

## Meals

Users create, edit and delete their own meals. Each meal has a name, a recipe text and a list of ingredients. Every
user also sees two system meals, Leftovers and Eat Out: rows with `created_by` null and `meal_type` set to something
other than `regular`, seeded by `supabase/seed.sql`. They can be planned but not edited or deleted.

On the Edit Meal page, ingredient changes are held in `IMealsStateContainer` as pending ingredients. Clicking save
first saves the meal, then each pending ingredient (see Ingredients).

- **Folder:** `Features/Meals/`
- **Pages:**
  - `Features/Meals/MealsPage.razor` at `/meals`: the meal list plus the add menu.
  - `Features/Meals/EditMeal/EditMealPage.razor` at `/meals/edit`: create or edit the meal in `MealToEdit`, including its ingredients.
- **Components:**
  - `Features/Meals/MealList.razor`
  - `Features/Meals/MealDisplayComponent.razor`
  - `Features/Meals/AddMealMenu.razor`: "new meal" or "import from URL", and shows the import status.
  - `Features/Meals/SaveMeal/SaveMealButton.razor`
  - `Features/Meals/DeleteMeal/DeleteMealButton.razor`: deletes after a confirm drawer.
- **Handlers:**

  | Handler | Request → result | Does |
  |---|---|---|
  | GetAllMeals | `Query()` → `Result<List<Meal>>` | Every meal visible to the user (own + system), regular meals last, then by name |
  | SaveMeal | `Command(Meal)` → `Result<Meal>` | Insert when `Id == 0` (sets `created_by`), otherwise update |
  | DeleteMeal | `Command(int MealId)` → `Result` | Delete by id; ingredients cascade |

- **State:** `Features/Meals/State/MealsStateContainer.cs` (`IMealsStateContainer`). It holds `Meals`, `MealToEdit`,
  and the pending ingredients; methods include `LoadMeals` and `ReloadIngredientsForMealToEdit`.
- **Models:**
  - `Features/Meals/Models/Meal.cs` ↔ `Features/Meals/Models/MealDb.cs`, table `meals`. `MealDb` references `IngredientDb`.
  - `Features/Meals/Models/MealType.cs`: `regular`, `leftover`, `eat_out`, mapped with `ToDbValue`/`FromDbValue`.
- **Migrations:**
  - `supabase/migrations/20260505135956_remote_schema.sql`
  - `supabase/migrations/20260506120000_add_leftovers_meal.sql`: adds `meal_type` and makes `created_by` nullable.
  - `supabase/migrations/20260506130000_add_eat_out_meal.sql`
- **Tests:**
  - unit: `Features/Meals/UnitTests/` (Models, State)
  - bUnit: `Features/Meals/ComponentTests/`
  - integration: `Features/Meals/IntegrationTests/MealHandlerTests.cs`
  - E2E: `MealWiz/tests/MauiWASM.Tests/E2ETests/MealJourneyTests.cs`
  - pgTAP: `supabase/tests/database/meals.test.sql`

## Ingredients

A meal's ingredients: name, amount, and a "show on grocery list" flag (a switch in the drawer, on by default;
ingredients with it off are left out when the plan's ingredients go on the grocery list). They're edited only from the
Edit Meal page, in a drawer. A new meal's ingredients are kept as pending in `IMealsStateContainer` until the meal is
saved and has an id.

- **Folder:** `Features/Ingredients/`
- **Pages:** none. The Edit Meal page hosts this feature.
- **Components:**
  - `Features/Ingredients/IngredientCard.razor`
  - `Features/Ingredients/IngredientDrawer.razor`: drawer content.
  - `Features/Ingredients/CreateIngredient/CreateIngredientCard.razor`: UI only.
  - `Features/Ingredients/SaveIngredient/SaveIngredientButton.razor`
  - `Features/Ingredients/DeleteIngredient/DeleteIngredientButton.razor`
- **Handlers:**

  | Handler | Request → result | Does |
  |---|---|---|
  | GetIngredientsByMealId | `Query(int MealId)` → `Result<List<Ingredient>>` | Ingredients where `fk_meal = MealId` |
  | SaveIngredient | `Command(Ingredient, int MealId)` → `Result` | Insert when `Id == 0` (sets `created_by`, `fk_meal`), otherwise update |
  | DeleteIngredient | `Command(Ingredient)` → `Result` | Delete one ingredient |

- **State:** none of its own. It uses `IMealsStateContainer` (pending ingredients, `ReloadIngredientsForMealToEdit`).
- **Models:** `Features/Ingredients/Models/Ingredient.cs` ↔ `Features/Ingredients/Models/IngredientDb.cs`, table
  `ingredients` (`fk_meal` → `meals`, on delete cascade).
- **Migrations:** `supabase/migrations/20261028130000_backfill_show_on_grocery_list.sql`: turns the flag on for every
  ingredient saved before the switch existed, and defaults the column to true.
- **Tests:**
  - unit: `Features/Ingredients/UnitTests/Models/`
  - bUnit: `Features/Ingredients/ComponentTests/`
  - integration: `Features/Ingredients/IntegrationTests/IngredientHandlerTests.cs`
  - pgTAP: `supabase/tests/database/ingredients.test.sql`

## MealPlans

A weekly plan with one meal per day. The home page shows the selected week, and you can move between weeks; the
selected week is kept in local storage under `mealplan_selected_week`. Picking a meal for a day creates that week's
`meal_plans` row if none exists yet (`CreateMealPlan`), then inserts a `meal_plans-meals` row for the date. Dates are
stored as `yyyy-MM-dd` strings.

- **Folder:** `Features/MealPlans/`
- **Pages:** `Features/MealPlans/MealPlanPage.razor` at `/`: the week overview plus the selected day's meal.
- **Components:**
  - `Features/MealPlans/MealPlanWeekOverview.razor`, with `.razor.css`
  - `Features/MealPlans/SelectMealForDate/MealSelectionList.razor`: UI only.
  - `Features/MealPlans/RemoveMealFromMealPlan/RemoveMealButton.razor`
- **Handlers:**

  | Handler | Request → result | Does |
  |---|---|---|
  | GetMealPlanByDate | `Query(DateTime Date)` → `Result<MealPlan?>` | The plan whose `start_date..end_date` contains the date, with its meals; `null` if none |
  | CreateMealPlan | `Command(MealPlan)` → `Result<MealPlan>` | Insert a week's plan (sets `created_by`) |
  | AddMealToMealPlan | `Command(Meal)` → `Result<Meal>` | Insert a `meal_plans-meals` row from `Meal.MealPlan` + `Meal.MealDate` |
  | RemoveMealFromMealPlan | `Command(int MealId, int MealPlanId, DateTime MealDate)` → `Result` | Delete the meal's row on that date in that plan |

- **State:** `Features/MealPlans/State/MealPlanStateContainer.cs` (`IMealPlanStateContainer`, uses `ILocalStorageService`):
  `MealPlan`, `SelectedDate`, `SelectedWeekStart`, `LoadMealPlan`, `NavigateToWeek`, `AddMealToPlan`.
- **Models:**
  - `Features/MealPlans/Models/MealPlan.cs` ↔ `Features/MealPlans/Models/MealPlanDb.cs`, table `meal_plans`.
    `MealPlan.MealOnDate` is a `Dictionary<DateTime, Meal>`.
  - `Features/MealPlans/Models/MealPlanMealDb.cs`, table `meal_plans-meals` (`fk_meal_plan`, `fk_meal`, `meal_date`).
    It is the join table and has no domain model.
- **Tests:**
  - unit: `Features/MealPlans/UnitTests/` (Models, State)
  - bUnit: `Features/MealPlans/ComponentTests/`
  - integration: `Features/MealPlans/IntegrationTests/MealPlanHandlerTests.cs`
  - E2E: `MealWiz/tests/MauiWASM.Tests/E2ETests/MealPlanJourneyTests.cs`
  - pgTAP: `supabase/tests/database/meal_plans.test.sql`, `supabase/tests/database/meal_plans_meals.test.sql`

## GroceryList

A per-user shopping list. You can add, edit and delete items, and tick items off (`is_picked`). "Add ingredients from
plan" opens a drawer that lists the current week's plan ingredients. It matches them to existing items by name,
ignoring case, and lets you set the final amount; matches update the existing item and the rest are inserted in bulk.

- **Folder:** `Features/GroceryList/`
- **Pages:** `Features/GroceryList/GroceryListPage.razor` at `/grocerylist`.
- **Components:**
  - `Features/GroceryList/AddGroceryItem/AddGroceryItemButton.razor`: opens `GroceryItemDrawer`.
  - `Features/GroceryList/Components/GroceryItemDrawer.razor`
  - `Features/GroceryList/Components/SaveGroceryItemButton.razor`
  - `Features/GroceryList/UpdateGroceryItem/UpdatedGroceryItemButton.razor`
  - `Features/GroceryList/DeleteGroceryItem/DeleteGroceryItemButton.razor`: deletes after a confirm drawer.
  - `Features/GroceryList/AddMealPlanIngredientsToGroceryList/AddIngredientsToGroceryListButton.razor`
  - `Features/GroceryList/AddMealPlanIngredientsToGroceryList/AddPlanIngredientsToGroceryListDrawer.razor`: the merge
    drawer. It reads `IMealPlanStateContainer` and skips ingredients with `ShowOnGroceryList` off.
- **Handlers:**

  | Handler | Request → result | Does |
  |---|---|---|
  | GetAllGroceryItems | `Query()` → `Result<List<GroceryItem>>` | All of the user's items |
  | AddGroceryItem | `Command(GroceryItem)` → `Result<GroceryItem>` | Insert one item (sets `created_by`), returns the inserted row |
  | AddMealPlanIngredientsToGroceryList | `Command(List<GroceryItem>)` → `Result<List<int>>` | Bulk insert; `created_by` comes from each ingredient |
  | UpdateGroceryItem | `Command(GroceryItem)` → `Result<int>` | Update one item, including ticking it off; fails if no row matches |
  | DeleteGroceryItem | `Command(GroceryItem)` → `Result<int>` | Delete one item; fails if no row matches |

- **State:** `Features/GroceryList/State/GroceryListStateContainer.cs` (`IGroceryListStateContainer`): `GroceryList`,
  `LoadGroceryItems`, `AddGroceryItemToGroceryList`, `UpdateGroceryItem`, `ToggleIsPicked`, `DeleteGroceryItem`.
- **Models:**
  - `Features/GroceryList/Models/GroceryItem.cs` ↔ `Features/GroceryList/Models/GroceryItemDb.cs`, table
    `grocery_items`. `GroceryItem` has a constructor from `Ingredient` and maps with `MapToDb()`.
  - `Features/GroceryList/Models/GroceryList.cs`: an in-memory aggregate with no table.
- **Tests:**
  - unit: `Features/GroceryList/UnitTests/` (Models, State)
  - bUnit: `Features/GroceryList/ComponentTests/`
  - integration: `Features/GroceryList/IntegrationTests/GroceryListHandlerTests.cs`
  - E2E: `MealWiz/tests/MauiWASM.Tests/E2ETests/GroceryListJourneyTests.cs`
  - pgTAP: `supabase/tests/database/grocery_items.test.sql`

## MealImport

Imports a meal from a recipe URL. The client inserts a `pending` row in `meal_imports` and subscribes to that row
over Supabase Realtime. It then POSTs `{ jobId, url }` to the Azure Function (see
[Functions: ScrapeRecipe](#functions-scraperecipe)), which scrapes the page and writes `succeeded` + `parsed_meal` or
`failed` + `error_message` back to the row. The row is the source of truth; the Function's HTTP status only says
whether the row was written.

On success the Import page pre-fills the Edit Meal form from `ParsedMeal.ToMeal()`. After 30 s the page offers
"Continue in background", and leaving the page does the same. A backgrounded job saves the meal itself, or shows a
toast if it fails. The client fails the row itself (`MarkMealImportFailed`) if the Function is unreachable or the row
stays pending past 3 minutes. One job runs at a time.

- **Folder:** `Features/MealImport/`
- **Pages:** `Features/MealImport/ImportMeal/ImportMealPage.razor` at `/meals/import`. `AddMealMenu` on the Meals
  page is the entry point and shows the latest import's status.
- **Handlers:**

  | Handler | Request → result | Does |
  |---|---|---|
  | CreateMealImport | `Command(Guid Id, string SourceUrl)` → `Result<MealImport>` | Insert the pending row (guarded user id) |
  | GetMealImport | `Query(Guid Id)` → `Result<MealImport>` | Read one row (used to catch up after subscribing) |
  | GetLatestMealImport | `Query` → `Result<MealImport?>` | The user's newest row, for the entry-point status |
  | MarkMealImportFailed | `Command(Guid Id, string ErrorMessage)` → `Result<MealImport?>` | Move own row `pending` → `failed`; the only client update RLS allows |
  | TriggerScrapeRecipe | `Command(Guid JobId, string Url)` → `Result` | POST to `<MealImportFunction:url>/api/ScrapeRecipe` with `x-functions-key`. Uses `HttpClient`, not Supabase |

- **State:**
  - `Features/MealImport/State/MealImportStateContainer.cs` (`IMealImportStateContainer`): runs the job, its timers
    and background mode. It depends on `IMealsStateContainer` to save imported meals, and injects `ISnackbar` itself.
  - `Features/MealImport/State/MealImportRealtimeSubscriber.cs` (`IMealImportRealtimeSubscriber`): the Realtime
    channel for one row. It is the only Shared service outside a handler that uses the Supabase client.
  - `Features/MealImport/State/MealImportJobHelper.cs`: pure logic: URL normalising, the entry-point state, and stale-row checks.
- **Models:**
  - `Features/MealImport/Models/MealImport.cs` ↔ `Features/MealImport/Models/MealImportDb.cs`, table `meal_imports`.
  - `Features/MealImport/Models/ParsedMeal.cs`: the `parsed_meal` jsonb contract.
  - `Features/MealImport/Models/MealImportStatus.cs`: `pending`, `succeeded`, `failed`.
  - `Features/MealImport/Models/MealImportJobStatus.cs` and `Features/MealImport/Models/MealImportEntryState.cs`: UI state.
  - `Features/MealImport/Models/MealImportFunctionOptions.cs`: Function URL and key from appsettings.
- **Migrations:**
  - `supabase/migrations/20260806120000_add_meal_imports_table.sql`: the table, plus adding it to the
    `supabase_realtime` publication.
  - `supabase/migrations/20261012120000_allow_client_to_fail_meal_imports.sql`
- **Tests:**
  - unit: `Features/MealImport/UnitTests/` (Handlers, Models, State)
  - bUnit: `Features/MealImport/ComponentTests/ImportMealPageTests.cs`
  - integration: `Features/MealImport/IntegrationTests/MealImportHandlerTests.cs`
  - E2E: `MealWiz/tests/MauiWASM.Tests/E2ETests/MealImportJourneyTests.cs`
  - pgTAP: `supabase/tests/database/meal_imports.test.sql`
  - contracts: `MealWiz/tests/Contracts/`
- **Known bug:** the Realtime channel joins with the anon key, [MEA-43](https://linear.app/mealwiz/issue/MEA-43).

## Authentication

Email and password sign-in through Supabase Auth. After login or logout the handler asks the
`AuthenticationStateProvider` to re-read the session. Unauthenticated users hitting an `[Authorize]` page are sent to
`/login` by `Layout/RedirectToLogin.razor`. The session is persisted per host (see [Hosts](#hosts)).

- **Folder:** `Features/Authentication/`
- **Pages:** `Features/Authentication/Login/LoginPage.razor` at `/login`, the only page without `[Authorize]`.
- **Components:** `Features/Authentication/Logout/LogoutButton.razor`
- **Handlers:**

  | Handler | Request → result | Does |
  |---|---|---|
  | Login | `Command(string Email, string Password)` → `Result<Session?>` | `SignInWithPassword`, then refresh auth state |
  | Logout | `Command()` → `Result` | `SignOut`, then refresh auth state |

- **Related:** `Services/Authentication/CustomAuthStateProvider.cs` builds claims from the Supabase access token.
- **Tests:**
  - bUnit: `Features/Authentication/ComponentTests/`
  - integration: `Features/Authentication/IntegrationTests/AuthenticationHandlerTests.cs`
  - unit: `Services/Authentication/UnitTests/CustomAuthStateProviderTests.cs`
  - E2E: `MealWiz/tests/MauiWASM.Tests/E2ETests/AuthJourneyTests.cs`

## Account

The settings page: a dark-mode toggle (`IThemeService`, stored in local storage under `mealwiz.theme`) and logout.
No handlers, no tables.

- **Folder:** `Features/Account/`
- **Pages:** `Features/Account/AccountPage.razor` at `/account`.
- **Related:** `Theme/ThemeService.cs`, `Theme/IThemeService.cs`, `Theme/MealWizTheme.cs`
- **Tests:** bUnit: `Features/Account/ComponentTests/AccountPageTests.cs`

## Functions: ScrapeRecipe

The server half of MealImport, in a separate Azure Functions app (isolated worker, net10.0) that deliberately
doesn't reference MealWiz.Shared. `POST /api/ScrapeRecipe` (`AuthorizationLevel.Function`) takes `{ jobId, url }`. It
loads the `meal_imports` row with the service-role key and acts only if the row is still `pending`. It always fetches
the row's stored `source_url`, never the URL in the request body. It parses the recipe (JSON-LD, then Microdata, then
OpenGraph), maps it to the payload, and writes the result back.

Responses: 200 means the row is resolved; 502 means the row couldn't be read or written and is still pending, so the
client fails it; 400 means a bad body. `MealWiz/src/MealWiz.Functions/README.md` covers local setup, the function
key, CORS and deployment.

- **Folder:** `MealWiz/src/MealWiz.Functions/`
- **Function:**
  - `MealWiz/src/MealWiz.Functions/Functions/ScrapeRecipe/ScrapeRecipeFunction.cs`: the HTTP trigger.
  - `MealWiz/src/MealWiz.Functions/Functions/ScrapeRecipe/ScrapeRecipeHandler.cs`: `IScrapeRecipeHandler.HandleAsync` → `Result`.
  - `MealWiz/src/MealWiz.Functions/Functions/ScrapeRecipe/RecipeMapper.cs`: `ParsedRecipe` → `MealImportPayload`.
  - `MealWiz/src/MealWiz.Functions/Functions/ScrapeRecipe/Parsing/`: `RecipeParser`, plus the JSON-LD, Microdata and OpenGraph parsers.
- **Shared code (inside Functions):**
  - `MealWiz/src/MealWiz.Functions/Shared/Fetching/RecipeFetcher.cs`: follows up to 5 redirects by hand and caps the body at 5 MB.
  - `MealWiz/src/MealWiz.Functions/Shared/Ssrf/IpAddressGuard.cs` and
    `MealWiz/src/MealWiz.Functions/Shared/Ssrf/SsrfGuardedHttpClientFactory.cs`: the SSRF guard. It blocks private,
    loopback, link-local, metadata and similar ranges, pins the connection to the address it checked, and never
    auto-redirects (FN-02).
  - `MealWiz/src/MealWiz.Functions/Shared/Ingredients/IngredientLineParser.cs`: splits "200 g flour" into amount and name.
  - `MealWiz/src/MealWiz.Functions/Shared/SupabaseAccess/ServiceRoleSupabaseClientFactory.cs`: the service-role
    client. It bypasses RLS, and exists only here.
  - `MealWiz/src/MealWiz.Functions/Shared/Models/`: `MealImportDb`, `MealImportPayload` and `RecipeImportStatus`.
    These deliberately duplicate the Shared types (FN-01); `MealWiz/tests/Contracts/` keeps them in sync.
- **DI:** `MealWiz/src/MealWiz.Functions/ServiceCollectionExtensions.cs`, called from `MealWiz/src/MealWiz.Functions/Program.cs`.
- **Tests:** `MealWiz/tests/MealWiz.Functions.Tests/`
  - `Functions/ScrapeRecipe/UnitTests/` and `Functions/ScrapeRecipe/IntegrationTests/`
  - `Shared/UnitTests/` and `Shared/IntegrationTests/`

## Cross-cutting code (MealWiz.Shared)

- **`Features/StateContainerBase.cs`:** base class for state containers. It provides `OnStateChanged`,
  `NotifyStateChanged()` and `CurrentSnackbar`.
- **`Services/DrawerStateContainer/`:** the global slide-in drawer.
  - `IDrawerStateContainer.cs` and `DrawerStateContainer.cs`
  - `DrawerStateparameters.cs`, which holds the `DrawerStateParameters` class.
  - `Components/Drawers/DrawerBase.razor` and `Components/Drawers/ConfirmDrawer.razor` render it.
  - See [ARCHITECTURE.md](ARCHITECTURE.md#drawers).
- **`Services/Authentication/CustomAuthStateProvider.cs`:** the auth state, from the Supabase session.
- **`Theme/`:** `IThemeService` (dark mode) and `MealWizTheme`.
- **`Helpers/`:**
  - `ResultHelper.cs`: `Handle(snackbar)`, plus `SetDefaultCatchHandler()`/`MapException`, which turn Supabase
    exceptions into errors.
  - `DependencyInjectionHelper.cs`: `RegisterScopedServices()`, the one list of Shared services.
  - `ErrorMessages.cs`: `GenericError`, `NotSignedIn`.
  - `SupabaseHelper.cs`, `DateTimeExtensions.cs` (`StartOfWeek`), `DictionaryExtensions.cs`.
- **`Models/Supabase/`:** the shapes of Supabase error responses, used by `ResultHelper`.
- **`Components/`:** generic UI.
  - `Components/NavMenu.razor`, the bottom navigation, and `Components/CustomNavLink.razor`
  - `Components/Tooltip.razor`
  - `Components/Card/BaseCard.razor`
  - `Components/Form/Button.razor`, `Components/Form/TextField.razor`, `Components/Form/CustomForm.razor`
- **`Layout/`:** `Layout/MainLayout.razor` (theme, snackbar provider, drawers, nav menu) and `Layout/RedirectToLogin.razor`.
- **`Routes.razor`:** the router used by the MAUI head. The web head has its own `MealWiz/MauiWASM/App.razor`.
- **`_Imports.razor`:** global `@using`s. A new feature namespace goes here.
- **Tests:**
  - `Components/ComponentTests/`, `Layout/ComponentTests/`
  - `Helpers/UnitTests/`
  - `Services/DrawerStateContainer/UnitTests/`

## Hosts

- **MauiWASM (web, deployed to Azure Static Web Apps).**
  - `MealWiz/MauiWASM/Program.cs` calls `AddMealWizWeb`.
  - `MealWiz/MauiWASM/ServiceCollectionExtensions.cs` registers everything: local storage, MudBlazor,
    `RegisterScopedServices()`, the MediatR assembly scan, auth, `TimeProvider`, `HttpClient`,
    `MealImportFunctionOptions`, and the scoped Supabase `Client` (anon key, Realtime auto-connect).
  - `MealWiz/MauiWASM/Provider/CustomSupabaseSessionProvider.cs` persists the session in local storage under `SUPABASE_SESSION`.
  - Config: `MealWiz/MauiWASM/wwwroot/appsettings.Development.json` (local stack), `appsettings.Staging.json` and `appsettings.Production.json`.
  - Tests: `MealWiz/tests/MauiWASM.Tests/`.
- **MealWiz (MAUI head: Android, iOS, Mac Catalyst, Windows).**
  - `MealWiz/MealWiz/MealWiz/MauiProgram.cs` registers local storage, MudBlazor, `RegisterScopedServices()`, the
    MediatR assembly scan, auth, `TimeProvider`, `HttpClient`, `MealImportFunctionOptions` and the Supabase `Client`
    like the web head, with its own session provider, and always loads the embedded
    `MealWiz/MealWiz/MealWiz/Resources/appsettings.Development.json`.
  - `MealWiz/MealWiz/MealWiz/Providers/CustomSupabaseSessionProvider.cs` uses `SecureStorage`.
  - It needs mobile workloads, so verify and CI don't build it, and it has no test project.

## Database

All schema lives in `supabase/migrations/`, applied to production by `.github/workflows/deploy.yml` on push to
`main`. Local data comes from `supabase/seed.sql`: the system meals, the local-only user `test@test.com`, and sample
meals. RLS is tested in `supabase/tests/database/`; `supabase/tests/database/00_rls_enabled.test.sql` pins the exact
table and policy list.

| Table | Feature | Created in | Notes |
|---|---|---|---|
| `meals` | Meals | `remote_schema` | `meal_type` added in `add_leftovers_meal`. SELECT also shows system meals (`meal_type <> 'regular'`); INSERT, UPDATE and DELETE only for your own `regular` meals |
| `ingredients` | Ingredients | `remote_schema` | `fk_meal` → `meals` on delete cascade. INSERT and UPDATE also require `fk_meal` to be your own meal |
| `meal_plans` | MealPlans | `remote_schema` | `start_date`, `end_date` |
| `meal_plans-meals` | MealPlans | `remote_schema` | `fk_meal_plan`, `fk_meal`, `meal_date`. The hyphen means it always needs quoting. INSERT and UPDATE also require your own plan, and your own meal or a system meal |
| `grocery_items` | GroceryList | `remote_schema` | `is_picked`, `amount` |
| `meal_imports` | MealImport | `add_meal_imports_table` | uuid id; in the Realtime publication. The client may only update its own row `pending` → `failed`, and only `status`, `error_message`, `updated_at` and `updated_by` (column grants); the Function writes the result with the service role |
