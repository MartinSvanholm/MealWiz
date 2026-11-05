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

The Meals page shows only regular meals; Eat Out and Leftovers are offered only when planning a day (the pick-a-meal
list). Each card's day tag comes from this calendar week's plan, which the page reads by sending the MealPlans
feature's `GetMealPlanByDate` query through MediatR (a failed load just leaves the tags out). Search matches meal
names and ingredient names (case-insensitive). The single-select filter chips narrow the grid together with the search:
This week to the meals with a day tag (same plan, so a failed load shows none), No recipe to meals whose recipe is
blank. The header count stays the total. When nothing matches, `meal-list-no-match` names the search, or says no
meals are planned this week, or that every meal has a recipe.

- **Folder:** `Features/Meals/`
- **Pages:**
  - `Features/Meals/MealsPage.razor` at `/meals`: header ("N meals", add menu), search, filter chips
    (`meal-filter-all|this-week|no-recipe`), the meal grid and the "Import from a recipe link" entry
    (`meals-import-link`). Empty state `meal-list-empty`.
  - `Features/Meals/EditMeal/EditMealPage.razor` at `/meals/edit`: create or edit the meal in `MealToEdit`, including
    its ingredients. Orange header with back (`edit-meal-back`), Save and the name field (`meal-name-input`); then the
    recipe, the add-ingredient row, the ingredient rows and, for a saved meal, Delete meal.
- **Components:**
  - `Features/Meals/MealGrid.razor`: the Meals page's two-column grid of cards (`meal-list-item`) with a
    `MealTile` (orange/green by position, day tag), the name (`meal-card-name`) and ingredient count (`meal-card-meta`).
  - `Features/Meals/MealDisplayComponent.razor`: used by the Meal plan page.
  - `Features/Meals/AddMealMenu.razor`: the orange "New" button (`add-meal-menu`, with `add-meal-menu-dot` and
    `data-state="in-progress|failed"` while an import runs or has failed) and its menu: "Create manually"
    (`add-meal-create-manually`) and the import item (`add-meal-import`), whose text follows the import status.
  - `Features/Meals/SaveMeal/SaveMealButton.razor`: the dark Save button in the Edit meal header.
  - `Features/Meals/DeleteMeal/DeleteMealButton.razor`: "Delete meal" on the Edit meal page; deletes after a confirm
    drawer, then returns to `/meals`.
- **Handlers:**

  | Handler | Request → result | Does |
  |---|---|---|
  | GetAllMeals | `Query()` → `Result<List<Meal>>` | Every meal visible to the user (own + system), regular meals last, then by name |
  | SaveMeal | `Command(Meal)` → `Result<Meal>` | Insert when `Id == 0` (sets `created_by`), otherwise update |
  | DeleteMeal | `Command(int MealId)` → `Result` | Delete by id; its ingredients and planned days cascade |

- **State:** `Features/Meals/State/MealsStateContainer.cs` (`IMealsStateContainer`). It holds `Meals`, `MealToEdit`,
  and the pending ingredients; methods include `LoadMeals`, `ReloadIngredientsForMealToEdit` and
  `SaveIngredientForMealToEdit` (pending while the meal is new, otherwise `SaveIngredient` and a reload).
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
Edit Meal page, always saved from the ingredient drawer: the add row opens it ("Add ingredient") and an ingredient row
opens it to edit ("Edit ingredient"). A new meal's ingredients are kept as pending in `IMealsStateContainer` until the meal is
saved and has an id.

- **Folder:** `Features/Ingredients/`
- **Pages:** none. The Edit Meal page hosts this feature.
- **Components:**
  - `Features/Ingredients/IngredientCard.razor`: one ingredient row (`ingredient-card`): name, amount
    chip (`ingredient-amount-chip`, `data-empty`; "+ amount" when empty) and trash. The row and the chip open the drawer ("Edit ingredient") with a delete action in its header
    (`drawer-delete-ingredient`).
  - `Features/Ingredients/IngredientDrawer.razor`: drawer content: name and amount side by side, the "Add to grocery
    list" switch row, and Save ingredient in a `DrawerFooter`. No field is focused when it opens. The fields edit a
    copy; `SaveIngredientButton` applies it to the ingredient on Save (and undoes that if the save fails).
  - `Features/Ingredients/CreateIngredient/AddIngredientRow.razor`: the add row (`add-ingredient-name`,
    `add-ingredient-amount`, `create-ingredient-button`). The button, or Enter in the amount, opens the drawer filled
    with what was typed (empty if nothing was) and clears the row; it saves nothing itself. Enter in the name moves to
    the amount.
  - `Features/Ingredients/SaveIngredient/SaveIngredientButton.razor`: refuses an ingredient without a name.
  - `Features/Ingredients/DeleteIngredient/DeleteIngredientButton.razor`
- **Handlers:**

  | Handler | Request → result | Does |
  |---|---|---|
  | GetIngredientsByMealId | `Query(int MealId)` → `Result<List<Ingredient>>` | Ingredients where `fk_meal = MealId` |
  | SaveIngredient | `Command(Ingredient, int MealId)` → `Result` | Insert when `Id == 0` (sets `created_by`, `fk_meal`), otherwise update |
  | DeleteIngredient | `Command(Ingredient)` → `Result` | Delete one ingredient |

- **State:** none of its own. It uses `IMealsStateContainer` (pending ingredients, `SaveIngredientForMealToEdit`,
  `ReloadIngredientsForMealToEdit`).
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
- **Pages:** `Features/MealPlans/MealPlanPage.razor` at `/` (with `Features/MealPlans/MealPlanPage.razor.css`):
  `ScreenHeader` with the label "Week NN · Month" and a title relative to this week ("This week", "Next week",
  "In 3 weeks"; `meal-plan-week-title`). On another week the title is a button back to this week
  (`meal-plan-current-week`). Header actions: `week-prev`, `week-next`, `plan-week-button` (not wired, MEA-75) and
  the avatar with the user's initial (`account-button`, opens `/account`). Below it the day strip, then the selected
  day's meal (`Features/Meals/MealDisplayComponent.razor`, with `RemoveMealButton` in its `HeroAction` corner) or the
  pick list for an empty day or while swapping the day's meal.
- **Components:**
  - `Features/MealPlans/MealPlanWeekOverview.razor`, with `.razor.css`: the seven 76px day tiles (`week-day`, with
    `data-date` and the classes `selected-day`, `has-meal` or `empty-day`, and `current-day`). A planned day shows the
    day dot (`week-day-meal-indicator`), an empty one a dashed tile with "+" (`week-day-empty`). Swiping the strip
    moves a week. The E2E tests rely on `selected-day` and `has-meal`.
  - `Features/MealPlans/SelectMealForDate/MealSelectionList.razor`, with `.razor.css`: UI only. "Pick a meal for
    <Day>" (`meal-selection-heading`, hint `meal-selection-hint`), search (`meal-selection-search`), shuffle
    (`shuffle-meals-button`, reloads and reorders the list) and one compact list: rows `meal-list-item`, each with
    `MealTile` (Eat Out and Leftovers first, neutral; the user's meals alternate orange and green), the pick button
    `meal-selection-pick`, the name `meal-selection-name` and, on regular meals, `meal-selection-edit`. Empty:
    `meal-list-empty`. In swap mode (`IsSwappingMeal`) the heading reads "Swap the meal for <Day>", the day's meal is
    left out, a pick replaces it (`SwapMealOnSelectedDate`) and Cancel (`meal-selection-cancel`) returns to the card.
  - `Features/MealPlans/RemoveMealFromMealPlan/RemoveMealButton.razor`, with `.razor.css`: the dark round button in the
    corner of the selected day's card (`remove-meal-button`).
  - The selected day's card lives in `Features/Meals/MealDisplayComponent.razor` (with `.razor.css`; listed under
    Meals): the orange card (`meal-display-name`, `meal-display-day`, `meal-display-summary`), the ingredients
    (`meal-display-ingredient`) and the recipe (`meal-display-recipe`), or the "No recipe yet" card
    (`meal-display-no-recipe`) whose Add (`meal-display-add-recipe`) opens Edit meal. System meals show only the card.
    Swap meal (`swap-meal-button`) calls `StartSwap()`, and the page shows the pick list in swap mode instead of the
    card. Shown but not wired: `add-all-to-list` (MEA-74).
- **Handlers:**

  | Handler | Request → result | Does |
  |---|---|---|
  | GetMealPlanByDate | `Query(DateTime Date)` → `Result<MealPlan?>` | The plan whose `start_date..end_date` contains the date, with its meals; `null` if none |
  | CreateMealPlan | `Command(MealPlan)` → `Result<MealPlan>` | Insert a week's plan (sets `created_by`) |
  | AddMealToMealPlan | `Command(Meal)` → `Result<Meal>` | Insert a `meal_plans-meals` row from `Meal.MealPlan` + `Meal.MealDate` |
  | RemoveMealFromMealPlan | `Command(int MealId, int MealPlanId, DateTime MealDate)` → `Result` | Delete the meal's row on that date in that plan |
  | SwapMealInMealPlan | `Command(int MealPlanId, DateTime MealDate, int CurrentMealId, int NewMealId)` → `Result` | Update that day's row to the new meal in one write; fails ("Meal not found in plan") when no row matches, including another user's |

- **State:** `Features/MealPlans/State/MealPlanStateContainer.cs` (`IMealPlanStateContainer`, uses `ILocalStorageService`):
  `MealPlan`, `SelectedDate`, `SelectedWeekStart`, `LoadMealPlan`, `NavigateToWeek`, `AddMealToPlan`, and swap mode:
  `IsSwappingMeal`, `StartSwap`, `CancelSwap`, `SwapMealOnSelectedDate` (swap mode ends when the selected day changes).
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
  - pgTAP: `supabase/tests/database/meal_plans.test.sql`, `supabase/tests/database/meal_plans_meals.test.sql`, `supabase/tests/database/meal_plans_duplicates_cleanup.test.sql`

## GroceryList

A per-user shopping list. You can add, edit and delete items, and tick items off (`is_picked`). "From meal plan"
opens a drawer that lists the current week's plan ingredients. It matches them to existing items by name, ignoring
case, and lets you set the final amount; matches update the existing item and the rest are inserted in bulk.

The page is a dense list (about ten to-buy rows on a 390×844 phone): a `ScreenHeader` labelled "N items · Week W"
(W is the ISO week of `IMealPlanStateContainer.SelectedWeekStart`, the week the drawer merges), a green progress bar
"X of N items", the add field, "To buy" as one list of 44px rows and "In the basket" as wrapping pills. Tapping an
item's name opens the Edit item drawer; its red header action (`DrawerStateParameters.HeaderAction`) deletes after the
confirm drawer. Tapping a pill puts the item back on the list. The basket's "Clear" (`clear-basket-button`) deletes
every picked item after the confirm drawer ("Clear 2 items?"). Swiping a to-buy row left (touch or mouse) past 80px
also deletes it after the same confirm drawer; basket pills don't swipe.

- **Folder:** `Features/GroceryList/`
- **Pages:** `Features/GroceryList/GroceryListPage.razor` at `/grocerylist` (styles in `GroceryListPage.razor.css`).
- **Components:**
  - `Features/GroceryList/AddGroceryItem/AddGroceryItemField.razor`: the "Add an item" field and + button; + or Enter
    opens `GroceryItemDrawer` with the typed name filled in.
  - `Features/GroceryList/Components/GroceryItemDrawer.razor`: name and amount side by side, "Save item" in a
    `DrawerFooter`. Focuses the amount when the name is already filled in. The fields edit a copy of the item;
    `UpdateGroceryItem` swaps the saved copy into the list, so closing without saving changes nothing.
  - `Features/GroceryList/Components/SaveGroceryItemButton.razor`
  - `Features/GroceryList/UpdateGroceryItem/EditGroceryItemButton.razor`: the item's name on its row; opens the edit
    drawer with `DeleteGroceryItemButton` as its header action.
  - `Features/GroceryList/DeleteGroceryItem/DeleteGroceryItemButton.razor`: the red delete in the edit drawer's
    header; deletes after a confirm drawer ("Delete Milk?"). That confirm lives in
    `Features/GroceryList/DeleteGroceryItem/ConfirmDeleteGroceryItem.cs`, shared with the swipe.
  - `Features/GroceryList/DeleteGroceryItem/SwipeToDeleteGroceryItem.razor`, with `.razor.css`: wraps a to-buy row's
    content. Blazor pointer events (no JS) move the row over a red layer; `touch-action: pan-y` keeps vertical
    scrolling native, and letting go past `DeleteDistance` opens `ConfirmDeleteGroceryItem`.
  - `Features/GroceryList/ClearBasket/ClearBasketButton.razor`, with `.razor.css`: the red "Clear" next to "In the
    basket"; after a confirm drawer it calls `ClearBasket` on the state container.
  - `Features/GroceryList/AddMealPlanIngredientsToGroceryList/AddIngredientsToGroceryListButton.razor`: the "From
    meal plan" header button; loads the meal plan if needed.
  - `Features/GroceryList/AddMealPlanIngredientsToGroceryList/AddPlanIngredientsToGroceryListDrawer.razor`: the merge
    drawer. It reads `IMealPlanStateContainer` and skips ingredients with `ShowOnGroceryList` off. Items already on
    the list show "Plan X · In list Y" and an empty, orange-outlined amount; the button reads "Add N to grocery list".
- **Handlers:**

  | Handler | Request → result | Does |
  |---|---|---|
  | GetAllGroceryItems | `Query()` → `Result<List<GroceryItem>>` | All of the user's items |
  | AddGroceryItem | `Command(GroceryItem)` → `Result<GroceryItem>` | Insert one item (sets `created_by`), returns the inserted row |
  | AddMealPlanIngredientsToGroceryList | `Command(List<GroceryItem>)` → `Result<List<int>>` | Bulk insert; `created_by` comes from each ingredient |
  | UpdateGroceryItem | `Command(GroceryItem)` → `Result<int>` | Update one item, including ticking it off; fails if no row matches |
  | DeleteGroceryItem | `Command(GroceryItem)` → `Result<int>` | Delete one item; fails if no row matches |
  | ClearBasket | `Command(List<int> GroceryItemIds)` → `Result` | Delete the given items in one request (the picked ones the user saw); ids RLS hides are skipped |

- **State:** `Features/GroceryList/State/GroceryListStateContainer.cs` (`IGroceryListStateContainer`): `GroceryList`,
  `LoadGroceryItems`, `AddGroceryItemToGroceryList`, `UpdateGroceryItem`, `ToggleIsPicked`, `DeleteGroceryItem`,
  `ClearBasket`.
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
- **Pages:** `Features/MealImport/ImportMeal/ImportMealPage.razor` at `/meals/import`: sub-page header, the failed
  alert (`import-job-error` or `import-latest-failed`), the form card (`import-form`, `import-url-input`,
  `import-submit`) and the progress card (`import-progress`). `AddMealMenu` on the Meals
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

## Authentication

Email and password sign-in and self sign-up through Supabase Auth. After login, sign-up or logout the handler asks
the `AuthenticationStateProvider` to re-read the session. Unauthenticated users hitting an `[Authorize]` page are
sent to `/login` by `Layout/RedirectToLogin.razor`. The session is persisted per host (see [Hosts](#hosts)).

- **Folder:** `Features/Authentication/`
- **Pages:** the only pages without `[Authorize]`. Neither has the bottom nav: `Layout/MainLayout.razor` shows it to
  signed-in users only.
  - `Features/Authentication/Login/LoginPage.razor` at `/login`: email and password fields (`login-email`,
    `login-password`) with labels above them, the full-width "Log in" button (`login-submit`) and "No account yet?
    Create one" (`login-create-account`), a link to `/signup`. "Forgot password?" (`login-forgot-password`) is shown
    but not wired yet (MEA-69).
  - `Features/Authentication/SignUp/SignUpPage.razor` at `/signup`: email, password and repeat password
    (`signup-email`, `signup-password`, `signup-password-repeat`), "Create account" (`signup-submit`) and "Already
    have an account? Log in" (`signup-login`). Passwords that don't match are caught on the page. With a session
    back it goes to the meal plan; without one (the Supabase project requires email confirmation) it shows a
    check-your-email note (`signup-check-email`). Supabase enforces the password minimum (6 locally).
- **Components:** `Features/Authentication/AuthShell.razor`, the signed-out screens' frame (rings, logo tile, title
  and tagline; test ids `<prefix>-page`, `<prefix>-title`, `<prefix>-tagline`). Its
  `Features/Authentication/AuthShell.razor.css` also styles the pages' `mw-auth-*` form classes.
  `Features/Authentication/Logout/LogoutButton.razor`, the full-width red outline "Log out" button
  (`logout-button`) on the Account page.
- **Handlers:**

  | Handler | Request → result | Does |
  |---|---|---|
  | Login | `Command(string Email, string Password)` → `Result<Session?>` | `SignInWithPassword`, then refresh auth state |
  | SignUp | `Command(string Email, string Password)` → `Result<Session?>` | `SignUp`, then `SetSession` with the returned session (gotrue-csharp doesn't keep an auto-confirmed sign-up's session itself; null when email confirmation is on), then refresh auth state |
  | Logout | `Command()` → `Result` | `SignOut`, then refresh auth state |

- **Related:** `Services/Authentication/CustomAuthStateProvider.cs` builds claims from the Supabase access token.
- **Tests:**
  - bUnit: `Features/Authentication/ComponentTests/`
  - integration: `Features/Authentication/IntegrationTests/AuthenticationHandlerTests.cs`
  - unit: `Services/Authentication/UnitTests/CustomAuthStateProviderTests.cs`
  - E2E: `MealWiz/tests/MauiWASM.Tests/E2ETests/AuthJourneyTests.cs`

## Account

The settings page: who is signed in, a dark-mode toggle (`IThemeService`, stored in local storage under
`mealwiz.theme`) and logout. No handlers, no tables.

- **Folder:** `Features/Account/`
- **Pages:** `Features/Account/AccountPage.razor` at `/account`: a SubPageHeader ("Profile" / "Account", back
  `account-back` to `/`), a profile card with a green avatar (`account-avatar`, the email's initial) and "Signed in as"
  the email (`account-email`, the access token's `email` claim from the cascading `AuthenticationState`), a settings
  group with the Dark mode row (`dark-mode-switch`) and the `LogoutButton`. Styles in
  `Features/Account/AccountPage.razor.css`.
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
    - DrawerBase is the bottom sheet: grab handle, a header row with the close (or back) button, the 22px `Title`
      and an optional `DrawerStateParameters.HeaderAction` on the right (`drawer-header-action`), then the content,
      which scrolls. A custom `Header` replaces the whole row.
    - `Components/Drawers/DrawerFooter.razor`: drawer content wraps its primary button in `<DrawerFooter>` to get
      the full-width 52px orange button at the bottom (`drawer-footer`).
    - ConfirmDrawer shows a red trash circle, the title (`confirm-drawer-title`), the description
      (`confirm-drawer-description`), Cancel (`confirm-drawer-cancel`, only closes) and a red Delete
      (`confirm-drawer-confirm`, runs `onConfirm`).
  - See [ARCHITECTURE.md](ARCHITECTURE.md#drawers).
- **`Services/Authentication/CustomAuthStateProvider.cs`:** the auth state, from the Supabase session.
- **`Theme/`:** `IThemeService` (dark mode, stored in local storage) and `MealWizTheme` (MudBlazor palettes,
  typography, radius, and `ModeAttribute()` for the `data-theme` hook).
- **`wwwroot/css/mealwiz.css`:** the global redesign stylesheet, linked by both heads after MudBlazor: the `--mw-*`
  tokens per theme, `@font-face`, global component styles and `.mw-text-*` classes. Tokens and when to use them:
  [ARCHITECTURE.md](ARCHITECTURE.md#theme-tokens-ui-02) (UI-02). A drawer field with its label above it is a
  `.mw-field` wrapping a `<label class="mw-field-label" for="x">` and a `MudTextField` with `InputId="x"` and no
  `Label` (the grocery item and ingredient drawers).
- **`wwwroot/fonts/`:** self-hosted Bricolage Grotesque and DM Sans (variable woff2, latin + latin-ext) with their
  OFL licences.
- **`Helpers/`:**
  - `ResultHelper.cs`: `Handle(snackbar)`, plus `SetDefaultCatchHandler()`/`MapException`, which turn Supabase
    exceptions into errors; a unique violation (23505) becomes "This was just changed on another device. Reload to see it."
  - `DependencyInjectionHelper.cs`: `RegisterScopedServices()`, the one list of Shared services.
  - `ErrorMessages.cs`: `GenericError`, `NotSignedIn`.
  - `SupabaseHelper.cs`, `DateTimeExtensions.cs` (`StartOfWeek`), `DictionaryExtensions.cs`.
- **`Models/Supabase/`:** the shapes of Supabase error responses, used by `ResultHelper`.
- **`Components/`:** generic UI.
  - `Components/NavMenu.razor`, the three tabs (Plan, Groceries, Meals; `nav-meal-plan`, `nav-grocery-list`,
    `nav-meals`), and `Components/CustomNavLink.razor`, one tab: icon above label, the active one an orange pill
    with the `active` class. The floating bar around them (`bottom-nav`) is in `Layout/MainLayout.razor`. It
    floats over the page: pages fill the screen (the `.mw-page` class) and end their scroll area with
    `padding-bottom: var(--mw-nav-clearance)` (`wwwroot/css/mealwiz.css`), so content scrolls behind the bar.
  - `Components/Headers/ScreenHeader.razor`: the header on top of every screen. Parameters: `Label` (small
    uppercase muted line, left out when empty), `Title` (26px), `TitleContent` (markup instead of `Title`, e.g. a
    button) and `Actions` (buttons on the right). Test ids `screen-header`, `screen-header-label`,
    `screen-header-title`, `screen-header-actions`.
  - `Components/Headers/SubPageHeader.razor`: ScreenHeader with a 44px back button on the left (Account, Import
    meal). Same parameters plus `BackHref` (default `/`), `OnBack` (runs instead of navigating) and `BackTestId`
    (default `sub-page-back`).
  - `Components/MealTile.razor`: a meal's coloured initial tile. Parameters: `Name`, `Index` (position in the list:
    even orange, odd green), `Neutral` (chip colour, for Eat Out and Leftovers), `DayTag` (e.g. "MON", left out
    when empty) and `Size` (`MealTile.TileSize.Small` 36px for lists, `Large` fills the width for the grid). Test
    ids `meal-tile` (with `data-color="orange|green|neutral"`), `meal-tile-initial`, `meal-tile-day`.
  - `Components/Tooltip.razor`
  - `Components/Card/BaseCard.razor`
  - `Components/Form/Button.razor`, `Components/Form/TextField.razor`, `Components/Form/CustomForm.razor`
  - `Components/Form/EnterKey.cs`: `EnterKey.Then(...)`, the OnKeyDown handler that makes Enter (also on a phone
    keyboard) move to a form's next field, and submit on its last single-line field. Every multi-field form uses it.
- **`Layout/`:** `Layout/MainLayout.razor` (the `data-theme` wrapper around the theme, popover and snackbar
  providers, drawers and the floating bottom nav, which it shows to signed-in users only) and
  `Layout/RedirectToLogin.razor`.
- **`Routes.razor`:** the router used by the MAUI head. The web head has its own `MealWiz/MauiWASM/App.razor`.
- **`_Imports.razor`:** global `@using`s. A new feature namespace goes here.
- **Tests:**
  - `Components/ComponentTests/`, `Layout/ComponentTests/`
  - `Helpers/UnitTests/`, `Theme/UnitTests/`
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
| `meal_plans` | MealPlans | `remote_schema` | `start_date`, `end_date`. One plan per user and week: unique (`created_by`, `start_date`), since `one_plan_per_week_and_meal_per_day`, which merged existing duplicates into the lowest id |
| `meal_plans-meals` | MealPlans | `remote_schema` | `fk_meal_plan`, `fk_meal` (→ `meals` on delete cascade, since `remove_deleted_meals_from_plans`), `meal_date`. The hyphen means it always needs quoting. INSERT and UPDATE also require your own plan, and your own meal or a system meal. One planned meal per plan and date: unique (`fk_meal_plan`, `meal_date`), since `one_plan_per_week_and_meal_per_day`, which kept the newest of existing duplicates. `fk_meal_plan` has no ON DELETE action, so a plan that still has planned meals can't be deleted |
| `grocery_items` | GroceryList | `remote_schema` | `is_picked`, `amount` |
| `meal_imports` | MealImport | `add_meal_imports_table` | uuid id; in the Realtime publication. The client may only update its own row `pending` → `failed`, and only `status`, `error_message`, `updated_at` and `updated_by` (column grants); the Function writes the result with the service role |
