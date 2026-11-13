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
- **Texts:** `Features/Meals/MealsResource.cs` with `MealsResource.resx` / `.da.resx` (UI-03). The system meals are shown
  by type, never by their stored English name: `Features/Meals/Models/MealDisplayName.cs`, `meal.DisplayName(L)`
  (Leftovers/Rester, Eat Out/Spis ude; a regular meal keeps its name). Used by the pick list, Plan week and the day card.
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
  - `Features/Meals/MealDisplayComponent.razor`: the selected day's card on the Meal plan page (details under
    MealPlans); its Add all to list opens GroceryList's merge drawer.
  - `Features/Meals/AddMealMenu.razor`: the orange "New" button (`add-meal-menu`, with `add-meal-menu-dot` and
    `data-state="in-progress|failed"` while an import runs or has failed) and its menu: "Create manually"
    (`add-meal-create-manually`) and the import item (`add-meal-import`), whose text follows the import status.
  - `Features/Meals/SaveMeal/SaveMealButton.razor`: the dark Save button in the Edit meal header.
  - `Features/Meals/DeleteMeal/DeleteMealButton.razor`: "Delete meal" on the Edit meal page; deletes after a confirm
    drawer, then returns to `/meals`. If grocery items still to buy are linked to the meal's ingredients, it opens
    `ReviewGroceryItemsDrawer` ("Remove from grocery list?", one group under the meal's name) and deletes the meal only
    after Remove; closing the review deletes nothing. Basket items are unlinked and links on items other meals need are
    dropped first (`LoadItemsLinkedToMeal`, `SettleLinkedItems`).
- **Handlers:**

  | Handler | Request → result | Does |
  |---|---|---|
  | GetAllMeals | `Query()` → `Result<List<Meal>>` | Every meal visible to the user (own + system), regular meals last, then by name |
  | SaveMeal | `Command(Meal)` → `Result<Meal>` | Insert when `Id == 0` (sets `created_by`), otherwise update |
  | DeleteMeal | `Command(int MealId)` → `Result` | Delete by id; its ingredients and planned days cascade; its grocery item sources lose both links (set null) |

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
saved and has an id. Deleting a saved ingredient that has to-buy grocery items asks whether to remove them too; basket
items are unlinked silently.

- **Folder:** `Features/Ingredients/`
- **Texts:** `Features/Ingredients/IngredientsResource.cs` with `IngredientsResource.resx` / `.da.resx`.
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
  - `Features/Ingredients/DeleteIngredient/DeleteIngredientButton.razor`: the trash on a row (`delete-ingredient-button`)
    and in the drawer header (`drawer-delete-ingredient`). A pending ingredient goes at once; a saved one after the red
    confirm, then the question below when it has grocery items still to buy.
  - `Features/Ingredients/DeleteIngredient/RemoveIngredientFromGroceryListDrawer.razor`, with `.razor.css`: "On your
    grocery list" / "Also remove X from the grocery list?" (`remove-ingredient-items-question`, hint
    `remove-ingredient-items-hint`). Both answers delete the ingredient first: Yes (`remove-ingredient-items-yes`) then
    deletes the items, No (`remove-ingredient-items-no`) unlinks them. Closing it deletes nothing.
- **Handlers:**

  | Handler | Request → result | Does |
  |---|---|---|
  | GetIngredientsByMealId | `Query(int MealId)` → `Result<List<Ingredient>>` | Ingredients where `fk_meal = MealId` |
  | SaveIngredient | `Command(Ingredient, int MealId)` → `Result` | Insert when `Id == 0` (sets `created_by`, `fk_meal`), otherwise update |
  | DeleteIngredient | `Command(Ingredient)` → `Result` | Delete one ingredient |

- **State:** none of its own. It uses `IMealsStateContainer` (pending ingredients, `SaveIngredientForMealToEdit`,
  `ReloadIngredientsForMealToEdit`) and `IGroceryListStateContainer` (`LoadItemsLinkedToIngredient`, `SettleLinkedItems`).
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
Removing a day's meal always asks first in a drawer. When the day has grocery items only it needs (still to buy, not manual, linked to the meal now on the day, no other live source), a switch, on by default, deletes them too. Shared and manual items lose that day's link and its amount (switch on, or no switch shown); with the switch off the FK nulls the link and the amount stays.

- **Folder:** `Features/MealPlans/`
- **Texts:** `Features/MealPlans/MealPlansResource.cs` with `MealPlansResource.resx` / `.da.resx`, also read by the
  day card (`Features/Meals/MealDisplayComponent.razor`). The relative week title is
  `Features/MealPlans/WeekTitles.cs`, `weekStart.ToRelativeWeekTitle(today, L)` ("Denne uge", "Om 3 uger").
- **Pages:** `Features/MealPlans/MealPlanPage.razor` at `/` (with `Features/MealPlans/MealPlanPage.razor.css`):
  `ScreenHeader` with the label "Week NN · Month" and a title relative to this week ("This week", "Next week",
  "In 3 weeks"; `meal-plan-week-title`). On another week the title is a button back to this week
  (`meal-plan-current-week`). Header actions: `week-prev`, `week-next`, `plan-week-button` (opens `/plan-week?week=` on the viewed week) and
  the avatar with the user's initial (`account-button`, opens `/account`). Below it the day strip, then the selected
  day's meal (`Features/Meals/MealDisplayComponent.razor`, with `RemoveMealButton` in its `HeroAction` corner) or the
  pick list for an empty day or while swapping the day's meal. After Plan week's Save it asks once whether to add the
  saved week's ingredients to the grocery list (`AddWeekIngredientsPromptDrawer`, opened from `OnAfterRender` when
  `TakeWeekIngredientsOffer()` returns the week).
- **Pages (continued):** `Features/MealPlans/PlanWeek/PlanWeekPage.razor` at `/plan-week?week=yyyy-MM-dd` (missing or invalid = this week; a past week opens on this week), `[Authorize]`, with `.razor.css`. `SubPageHeader` (`plan-week-back` returns to `/`, `plan-week-title`, `plan-week-prev` disabled on this week, `plan-week-next`), then settings (`plan-week-weeks-back-decrease`/`-value`/`-increase`, 0-8 weeks, default 3; `plan-week-leftovers-switch`; both kept in local storage as `planweek_settings`), `plan-week-generate` (replaced by `plan-week-shuffle-all`, "Shuffle all", once a preview exists), `plan-week-fallback-note`, seven `plan-week-day` rows (`data-date`, `data-locked`; `plan-week-day-meal`, `plan-week-day-lock`; once a preview exists also `plan-week-day-pick` (opens `Features/MealPlans/PlanWeek/PlanWeekPickMealDrawer.razor`, the pick list titled "Pick a meal for <Day>"; an empty day shows `plan-week-day-empty`), `plan-week-day-shuffle` (disabled on a locked day) and `plan-week-day-clear` (disabled with a saved meal; unlocks the day)) and `plan-week-save`. Picking by hand locks the day. `plan-week-back` leaves at once, or asks "Discard this plan?" (Cancel / Discard) when the preview has unsaved changes; the nav bar leaves without asking. With no regular meals only `plan-week-empty-pool` and `plan-week-go-to-meals` show. Save navigates to `/` on the saved week and offers its ingredients. Its new texts (`PlanWeek_*`) live in `Features/MealPlans/MealPlansResource.cs` with `MealPlansResource.resx` and `MealPlansResource.da.resx`.
- **Components:**
  - `Features/MealPlans/PlanWeek/AddWeekIngredientsPromptDrawer.razor`, with `.razor.css`: the prompt after Save
    (`week-ingredients-prompt`): header "Week saved", an orange cart circle (`.mw-confirm-icon-accent`, the orange
    variant of the confirm icon circle), the question (`week-ingredients-prompt-title`) and description
    (`week-ingredients-prompt-description`). "Add ingredients" (`week-ingredients-prompt-accept`) swaps in the merge
    drawer titled "Week NN ingredients" for every planned date of that week; "Not now"
    (`week-ingredients-prompt-decline`, the header X and a scrim tap) only closes. `Open(drawer, localizer, weekStart)`.
  - `Features/MealPlans/MealPlanWeekOverview.razor`, with `.razor.css`: the seven 76px day tiles (`week-day`, with
    `data-date` and the classes `selected-day`, `has-meal` or `empty-day`, and `current-day`). A planned day shows the
    day dot (`week-day-meal-indicator`), an empty one a dashed tile with "+" (`week-day-empty`). Swiping the strip
    moves a week. The E2E tests rely on `selected-day` and `has-meal`.
  - `Features/MealPlans/SelectMealForDate/MealSelectionList.razor`: a thin wrapper over `IMealPlanStateContainer` around `Features/MealPlans/SelectMealForDate/MealPickList.razor` (with `.razor.css`), the parameter-driven list (`Heading`, `Hint`, `ExcludedMealId`, `ShowEdit`, `ShowCancel`/`OnCancel`, `Disabled`, `OnPick`) also used by the Plan week pick drawer. UI only. "Pick a meal for
    <Day>" (`meal-selection-heading`, hint `meal-selection-hint`), search (`meal-selection-search`), shuffle
    (`shuffle-meals-button`, reloads and reorders the list) and one compact list: rows `meal-list-item`, each with
    `MealTile` (Eat Out and Leftovers first, neutral; the user's meals alternate orange and green), the pick button
    `meal-selection-pick`, the name `meal-selection-name` and, on regular meals, `meal-selection-edit`. Empty:
    `meal-list-empty`. In swap mode (`IsSwappingMeal`) the heading reads "Swap the meal for <Day>", the day's meal is
    left out and Cancel (`meal-selection-cancel`) returns to the card. A pick on a day with a planned-day id loads the grocery items and sources first (picks disabled meanwhile); items only the old meal needs (`PlannedMealGroceryItems.OnlyNeededBy`) open `SwapMealDrawer`, otherwise the pick swaps straight away (`SwapMealOnSelectedDate`). A failed load shows the error and swaps nothing.
  - `Features/MealPlans/RemoveMealFromMealPlan/RemoveMealButton.razor`, with `.razor.css`: the dark round button in the
    corner of the selected day's card (`remove-meal-button`). It loads the grocery items and sources through MediatR, picks the ids with `PlannedMealGroceryItems.OnlyNeededBy` and the shared links with `SourceIdsOnSharedItems`, opens `RemoveMealDrawer` and is disabled while loading.
  - `Features/MealPlans/RemoveMealFromMealPlan/RemoveMealDrawer.razor`, with `.razor.css`: the drawer content (title, `remove-meal-description`, the `GroceryItemsSwitch` row (prefix `remove-meal`: `remove-meal-grocery-row|label|switch`) only when there are items, Cancel `remove-meal-cancel` and the red Remove `remove-meal-confirm`). It removes the planned day first, then sends `DeleteGroceryItems`; then drops this day's links on shared items (`DeleteGroceryItemSources`) unless the switch is off; a failed remove deletes nothing, a failed delete says the items stayed, a failed drop says the amounts stayed.
  - `Features/MealPlans/SwapMealInMealPlan/SwapMealDrawer.razor`, with `.razor.css`: asks before a swap when the old meal has to-buy items only it needs (title "Swap <old> for <new>?", `swap-meal-description`, the `GroceryItemsSwitch` row with prefix `swap-meal`, Cancel `swap-meal-cancel` and the orange Swap `swap-meal-confirm`). Cancel only closes (the old meal and swap mode stay). Swap swaps first, then, with the switch on, sends `DeleteGroceryItems`; then drops the old day's links on shared items (`DeleteGroceryItemSources`); a failed swap deletes nothing and keeps the drawer open, a failed delete says the items stayed, a failed drop says the amounts stayed. A straight swap (no own items) drops those links too. `Open(drawer, localizer, mealsLocalizer, current, new, date, ids, sourceIds)`.
  - `Features/MealPlans/Components/GroceryItemsSwitch.razor`, with `.razor.css`: the shared "Also remove N items from the grocery list" row of the remove and swap drawers (`<prefix>-grocery-row|label|switch`; texts `GroceryItems_*`, including `GroceryItems_AmountsKept`).
  - `Features/MealPlans/MealPlansResource.cs` (+ `MealPlansResource.resx`, `MealPlansResource.da.resx`): the feature's texts (UI-03).
  - The selected day's card lives in `Features/Meals/MealDisplayComponent.razor` (with `.razor.css`; listed under
    Meals): the orange card (`meal-display-name`, `meal-display-day`, `meal-display-summary`), the ingredients
    (`meal-display-ingredient`) and the recipe (`meal-display-recipe`), or the "No recipe yet" card
    (`meal-display-no-recipe`) whose Add (`meal-display-add-recipe`) opens Edit meal. System meals show only the card.
    Swap meal (`swap-meal-button`) calls `StartSwap()`, and the page shows the pick list in swap mode instead of the
    card. Add all to list (`add-all-to-list`) opens GroceryList's merge drawer for the selected day only
    (`AddPlanIngredientsToGroceryListDrawer.Open`, title "Add <meal> to list"); it's disabled when no ingredient is shown
    on the grocery list.
  - `Features/MealPlans/PlanWeek/WeekGenerator.cs`: pure, seeded `Random`. `PickMeal` picks one regular meal for a single day (never the current one). Picks random regular meals for unlocked days; skips meals used within N weeks before or after the week or already in it; falls back to the meals used longest ago (`UsedFallback`); repeats only after every meal is used; with leftovers on, an unlocked day right after a cooked (regular) meal gets Leftovers.
- **Handlers:**

  | Handler | Request → result | Does |
  |---|---|---|
  | GetMealPlanByDate | `Query(DateTime Date)` → `Result<MealPlan?>` | The plan whose `start_date..end_date` contains the date, with its meals; `null` if none |
  | CreateMealPlan | `Command(MealPlan)` → `Result<MealPlan>` | Insert a week's plan (sets `created_by`) |
  | AddMealToMealPlan | `Command(Meal)` → `Result<int>` | Insert a `meal_plans-meals` row from `Meal.MealPlan` + `Meal.MealDate`; returns the planned day's id |
  | RemoveMealFromMealPlan | `Command(int MealId, int MealPlanId, DateTime MealDate)` → `Result` | Delete the meal's row on that date in that plan |
  | SwapMealInMealPlan | `Command(int MealPlanId, DateTime MealDate, int CurrentMealId, int NewMealId)` → `Result` | Update that day's row to the new meal in one write; fails ("Meal not found in plan") when no row matches, including another user's |
  | GetMealPlannedDates | `Query(DateTime From, DateTime To)` → `Result<List<PlannedMealDate>>` | Each planned meal and date in the range (own rows) |
  | SaveGeneratedWeek | `Command(DateTime WeekStart, IReadOnlyList<PlannedDay> Days)` → `Result` | Creates the plan if missing, updates days that have a row (in place), bulk-inserts the rest |

- **State:** `Features/MealPlans/State/MealPlanStateContainer.cs` (`IMealPlanStateContainer`, uses `ILocalStorageService`):
  `MealPlan`, `SelectedDate`, `SelectedWeekStart`, `LoadMealPlan`, `NavigateToWeek`, `AddMealToPlan`, the week-ingredients offer (`OfferWeekIngredients(weekStart)`, called by Plan week's Save, and the one-shot `TakeWeekIngredientsOffer()`, which returns the week only once it is loaded and shown and has an ingredient for the list), and swap mode:
  `IsSwappingMeal`, `StartSwap`, `CancelSwap`, `SwapMealOnSelectedDate` (returns `Task<bool>`: true when saved; swap mode ends when the selected day changes).
  `Features/MealPlans/State/PlanWeekStateContainer.cs` (`IPlanWeekStateContainer`, uses `ILocalStorageService`, `TimeProvider`, `IMealPlanStateContainer`): `Open`, `NavigateToWeek`, `WeekStart`, `Settings`, `SetWeeksBack`, `SetLeftoversAfterEveryMeal`, `Days`, `ToggleLock`, `Generate`, `ShuffleAll`, `ShuffleDay`, `PickMeal`, `ClearDay`, `HasSavedMeal`, `HasPreview`, `Save`, `IsDirty`, `CanGenerate`, `CanSave`, `UsedFallback`, `HasNoRegularMeals`.
- **Models:**
  - `Features/MealPlans/Models/MealPlan.cs` ↔ `Features/MealPlans/Models/MealPlanDb.cs`, table `meal_plans`.
    `MealPlan.MealOnDate` is a `Dictionary<DateTime, Meal>`. `MealPlan.PlannedMealIdOnDate` is a `Dictionary<DateTime, int>` of `meal_plans-meals` ids (a swap keeps the id); `AddMealToPlan` records the id and `RemoveMealButton` drops the date's id.
  - `Features/MealPlans/Models/MealPlanMealDb.cs`, table `meal_plans-meals` (`fk_meal_plan`, `fk_meal`, `meal_date`).
    It is the join table and has no domain model.
  - `Features/MealPlans/Models/PlannedMealDate.cs` ↔ `PlannedMealDateDb.cs` (slim read of `meal_plans-meals`), `PlanWeekSettings.cs`, `PlanWeekDay.cs`.
- **Tests:**
  - unit: `Features/MealPlans/UnitTests/` (Models, State, PlanWeek)
  - bUnit: `Features/MealPlans/ComponentTests/`
  - integration: `Features/MealPlans/IntegrationTests/MealPlanHandlerTests.cs`, `PlanWeekHandlerTests.cs`, `RemoveMealWithGroceryItemsTests.cs`
  - E2E: `MealWiz/tests/MauiWASM.Tests/E2ETests/MealPlanJourneyTests.cs`
  - pgTAP: `supabase/tests/database/meal_plans.test.sql`, `supabase/tests/database/meal_plans_meals.test.sql`, `supabase/tests/database/meal_plans_duplicates_cleanup.test.sql`

## GroceryList

A per-user shopping list. You can add, edit and delete items, and tick items off (`is_picked`). "From meal plan"
opens a merge drawer (reusable for one planned day or a whole week) that lists the planned ingredients not linked
from their planned day yet; with everything linked it says "Everything is already on your list". A to-buy item with the
same name (ignoring case) is merged into: the total is pre-filled by `AmountHelper` when the amounts have one unit, else
empty to type. Every item added or merged gets a `grocery_item_sources` link (planned day, ingredient, amount); items
added from the plan have `is_manual` false, older and hand-typed items true and are never "from the plan".
Dropping a planned meal's link from an item another meal or the user still needs lowers the item's amount by that
link's amount (Garlic 3 cloves − Pasta's 2 cloves = 1 clove).
A banner at the top of the list ("N items are from meals no longer in your plan", `stale-items-banner`) appears when
plan-added items come from meals that were swapped out or removed from the plan with the switch off (every source
stale; basket and manual items never count). Review opens a drawer of those items grouped by their old meal, all
ticked; Remove deletes the ticked items and unlinks the rest, which become manual items, so the banner goes away.

A linked ingredient whose amount changed after it was added (the source's amount differs from the ingredient's current
amount, trimmed and ignoring case; the item is still to buy; the source isn't stale) is offered again in the same
drawer. The row shows "Was … · Now … · In list …" and suggests item − old + new (`AmountHelper.Adjust`) when all share
a unit and the result is above zero, else an empty required field. Confirming updates the item's amount and the
source's remembered amount, so it isn't offered again.

The page is a dense list (about ten to-buy rows on a 390×844 phone): a `ScreenHeader` labelled "N items · Week W"
(W is the ISO week of `IMealPlanStateContainer.SelectedWeekStart`, the week the drawer merges), a green progress bar
"X of N items", the add field, "To buy" as one list of 44px rows and "In the basket" as wrapping pills. Tapping an
item's name opens the Edit item drawer; its red header action (`DrawerStateParameters.HeaderAction`) deletes after the
confirm drawer. Tapping a pill puts the item back on the list. The basket's "Clear" (`clear-basket-button`) deletes
every picked item after the confirm drawer ("Clear 2 items?"). Swiping a to-buy row left (touch or mouse) past 80px
also deletes it after the same confirm drawer; basket pills don't swipe.

- **Folder:** `Features/GroceryList/`
- **Texts:** `Features/GroceryList/GroceryListResource.cs` with `GroceryListResource.resx` / `.da.resx`.
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
    basket"; after a confirm drawer it calls `ClearBasket` on the state container, which sends `DeleteGroceryItems`.
  - `Features/GroceryList/AddMealPlanIngredientsToGroceryList/AddIngredientsToGroceryListButton.razor`: the "From
    meal plan" header button; loads the meal plan if needed, then calls `AddPlanIngredientsToGroceryListDrawer.Open`.
  - `Features/GroceryList/AddMealPlanIngredientsToGroceryList/AddPlanIngredientsToGroceryListDrawer.razor`: the merge
    drawer, `Open(drawer, title, dates)`. It loads items and sources itself, skips ingredients with `ShowOnGroceryList`
    off, makes one row per existing to-buy item (or per new name), combining new ingredients and linked ingredients
    with a changed amount (subline parts "Plan X + Y", "Was … · Now …" with `plan-ingredient-now`, "In list Z";
    orange field for merged rows) and shows `plan-ingredients-loading`, `plan-ingredients-empty` or
    `plan-ingredients-all-on-list` (nothing new and nothing changed); the button reads "Add N to grocery list", or
    "Update N on grocery list" when every row is change-only (snackbar "Grocery list updated"); row names carry
    `plan-ingredient-name`. Opened by From meal plan (the week) and by Add all to list on the Meal plan's day card
    (that day).
  - `Features/GroceryList/Components/ReviewGroceryItemsDrawer.razor`, with `.razor.css`: the review drawer, opened with
    `ReviewGroceryItemsDrawer.Open(drawer, title, groups, afterRemove)` (deleting a meal or ingredient reuses it). Its
    button reads "Remove N items" or "Keep all" and calls `DeleteAndUnlinkGroceryItems`.
- **Handlers:**

  | Handler | Request → result | Does |
  |---|---|---|
  | GetAllGroceryItems | `Query()` → `Result<List<GroceryItem>>` | All of the user's items |
  | AddGroceryItem | `Command(GroceryItem)` → `Result<GroceryItem>` | Insert one item (sets `created_by`), returns the inserted row |
  | GetGroceryItemSources | `Query()` → `Result<List<GroceryItemSource>>` | Every source of the user's items, with the planned day's meal and date and the ingredient's meal and name |
  | AddPlanIngredientsToGroceryList | `Command(List<PlanIngredientToAdd> Items)` → `Result<List<GroceryItem>>` | Skips already-linked pairs, updates merged items' amount, inserts new items (is_manual false, one per name), adds a source per ingredient |
  | UpdateLinkedGroceryAmounts | `Command(List<LinkedAmountUpdate> Updates)` → `Result` | Checks each item is the user's and still to buy and each source belongs to its item; sets each item's amount once, then each source's remembered amount |
  | UpdateGroceryItem | `Command(GroceryItem)` → `Result<int>` | Update one item, including ticking it off; fails if no row matches |
  | DeleteGroceryItem | `Command(GroceryItem)` → `Result<int>` | Delete one item; fails if no row matches |
  | DeleteGroceryItems | `Command(List<int> GroceryItemIds)` → `Result` | Delete the given items in one request; sources cascade; ids RLS hides are skipped |
  | UnlinkGroceryItems | `Command(List<int> GroceryItemIds)` → `Result` | Delete the items' sources and set `is_manual` = true; ids RLS hides are skipped |
  | DeleteGroceryItemSources | `Command(List<int> SourceIds)` → `Result<List<GroceryItem>>` | Deletes the given sources, then lowers each to-buy item's amount by the dropped sources' amounts (`AmountHelper.Subtract`: same unit, result > 0, else unchanged; basket items keep theirs); returns the items whose amount changed (`Result<List<GroceryItem>>`); ids RLS hides are skipped |

- **State:** `Features/GroceryList/State/GroceryListStateContainer.cs` (`IGroceryListStateContainer`): `GroceryList`,
  `LoadGroceryItems`, `AddGroceryItemToGroceryList`, `UpdateGroceryItem`, `ToggleIsPicked`, `DeleteGroceryItem`,
  `ClearBasket`, `Sources`, `LoadSources`, `StaleItems`, `DeleteAndUnlinkGroceryItems`, and for a meal or ingredient
  being deleted `LoadItemsLinkedToMeal`, `LoadItemsLinkedToIngredient` and `SettleLinkedItems` (delete, unlink with the
  basket, drop links and apply the lowered amounts; failures returned unreported).
- **Models:**
  - `Features/GroceryList/Models/GroceryItem.cs` ↔ `Features/GroceryList/Models/GroceryItemDb.cs`, table
    `grocery_items`. `GroceryItem` has a constructor from `Ingredient` and maps with `MapToDb()`.
  - `GroceryItem.IsManual` (`is_manual`).
  - `Features/GroceryList/Models/GroceryItemSource.cs` ↔ `GroceryItemSourceDb.cs` (writes) and `GroceryItemSourceDetailsDb.cs` (reads, with the planned day and ingredient), table `grocery_item_sources`; `PlanIngredientToAdd.cs`; `LinkedAmountUpdate.cs`; `Features/GroceryList/GroceryListResource.cs` (+ `.resx`, `.da.resx`): the merge drawer's and `UpdateLinkedGroceryAmounts`' texts; `AmountHelper.cs` (`SuggestTotal`, `Adjust`, and `Subtract`, which alone tolerates a plural s: "3 cloves" − "2 cloves" = "1 clove"). `GroceryItemSource` also carries `IngredientAmount` and `HasChangedAmount`.
  - `Features/GroceryList/Models/GroceryList.cs`: an in-memory aggregate with no table.
  - `Features/GroceryList/Models/LinkedGroceryItems.cs`: a deleted meal's or ingredient's items sorted into `ToBuy`,
    `InBasket` and `SourceIdsToDrop` (manual items and items another live link needs only lose its links).
  - `Features/GroceryList/Models/StaleGroceryItems.cs` (the stale rule and the grouping by old meal) and `Features/GroceryList/Models/GroceryItemGroup.cs` (a null `Heading` is "Other items", resolved in the drawer).
  - `Features/GroceryList/GroceryListResource.cs` with `GroceryListResource.resx` and `.da.resx`: the banner and review drawer texts (UI-03).
  - `Features/GroceryList/Models/PlannedMealGroceryItems.cs`: static `OnlyNeededBy(plannedMealId, items, sources)`, the items only one planned day needs (MEA-81), and `SourceIdsOnSharedItems`, that day's live links on shared and manual to-buy items (MEA-99).
- **Tests:**
  - unit: `Features/GroceryList/UnitTests/` (Models, State)
  - bUnit: `Features/GroceryList/ComponentTests/`
  - integration: `Features/GroceryList/IntegrationTests/GroceryListHandlerTests.cs`, `LinkedGroceryAmountHandlerTests.cs`
  - E2E: `MealWiz/tests/MauiWASM.Tests/E2ETests/GroceryListJourneyTests.cs`
  - pgTAP: `supabase/tests/database/grocery_items.test.sql`, `supabase/tests/database/grocery_item_sources.test.sql`
  - migration: `supabase/migrations/20261104120000_add_grocery_item_sources.sql`

## MealImport

Imports a meal from a recipe URL. The client inserts a `pending` row in `meal_imports` and subscribes to that row
over Supabase Realtime. It then POSTs `{ jobId, url }` to the Azure Function (see
[Functions: ScrapeRecipe](#functions-scraperecipe)), which scrapes the page and writes `succeeded` + `parsed_meal` or
`failed` + `error_code` + `error_message` back to the row. The row is the source of truth; the Function's HTTP status only says
whether the row was written.

On success the Import page pre-fills the Edit Meal form from `ParsedMeal.ToMeal()`. After 30 s the page offers
"Continue in background", and leaving the page does the same. A backgrounded job saves the meal itself, or shows a
toast if it fails. The client fails the row itself (`MarkMealImportFailed`) if the Function is unreachable or the row
stays pending past 3 minutes. One job runs at a time.

A job row is a **link job** (`source_url`) or a **picture job** (`picture_paths`: one to three JPEGs in the private
`meal-import-pictures` bucket, `language` `en`/`da`, optional `hint`), never both. A picture job is resolved by
[Functions: AnalyzeMealPicture](#functions-analyzemealpicture) and its `parsed_meal` also has `IsGuessed` (true for a
meal guessed from a dish; absent reads as false). The client starts one from the Import page (`/meals/import?source=picture`, reached by "Create from a picture" in the
New menu: `add-meal-import-picture`): the user takes or chooses up to 3 pictures (`import-picture-camera-input`,
`import-picture-gallery-input`), sees thumbnails (`import-picture-thumb-{i}`, `import-picture-remove-{i}`), a privacy note
(`import-picture-privacy`) and an inline error (`import-picture-error`), and presses `import-picture-submit`. Each picture
is shrunk in the browser first (`MealWiz/MealWiz/MealWiz.Shared/wwwroot/js/mealPictures.js` through `Features/MealImport/State/MealPictureShrinker.cs`,
`IMealPictureShrinker`: orientation applied, long edge 2,500 px, JPEG 0.8; a file the browser can't decode is not added).
`IMealImportStateContainer.StartPictureImport` then uploads the pictures, inserts the row (with the app language, from
`MealImportJobHelper.LanguageFor`), and calls the Function; the rest (progress card, background mode, one job at a
time) is shared with link jobs. Constants live in `Features/MealImport/Models/MealImportPictures.cs` (bucket id,
`MaxPictures`, `MaxHintLength`, `PathFor(userId, jobId, index)`). The Edit meal form knows its job (`IMealImportStateContainer.FormImport`, set by the
Import page's `OpenInForm()`): `EditMealPage` shows `edit-meal-guessed-note` when the meal was guessed from a dish, and for
a picture job `edit-meal-retry` ("Not right? Try again with a hint") opens `edit-meal-hint-input` and
`edit-meal-hint-submit` (disabled for a blank hint). `RetryWithHint` starts a new job on the same picture paths with the
hint (no upload); while it runs (`IsRetrying`, `edit-meal-retrying`) `save-meal-button` is disabled. A successful retry
replaces the form and `FormImport`; a failed one leaves the form and shows `RetryErrorMessage`
(`edit-meal-retry-error`); leaving the form during a retry backgrounds it. `ReleaseFormImport()` deletes the form job's
pictures (best effort, silent, through `DeleteMealImportPictures`): after a full save (`SaveMealButton`) and when the form
is left; a backgrounded picture job deletes its pictures when it finishes. What's left (a closed tab, a failed job) goes in
the daily clean-up ([Functions: CleanUpMealPictures](#functions-cleanupmealpictures)).

A failed row's `error_code` says why: `invalid_url`, `no_recipe_found` and `fetch_failed` (or `unknown`) from the
Function; `unreachable` or `timed_out` from the client. A picture job adds `no_meal_found`, `daily_limit_reached`,
`picture_unreadable` and `ai_unavailable` (and the Function may write `timed_out`). The UI shows the localized message for the code
(`ImportError_<Code>`, null or unknown = `ImportError_Unknown`) and never `error_message`, which keeps the English
detail for diagnosis.

- **Folder:** `Features/MealImport/`
- **Texts:** `Features/MealImport/MealImportResource.cs` with `MealImportResource.resx` / `.da.resx`.
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
  | MarkMealImportFailed | `Command(Guid Id, MealImportErrorCode ErrorCode, string ErrorMessage)` → `Result<MealImport?>` | Move own row `pending` → `failed`; the only client update RLS allows |
  | TriggerScrapeRecipe | `Command(Guid JobId, string Url)` → `Result` | POST to `<MealImportFunction:url>/api/ScrapeRecipe` with `x-functions-key`. Uses `HttpClient`, not Supabase |
  | UploadMealImportPictures | `Command(Guid JobId, IReadOnlyList<byte[]> Pictures)` → `Result<IReadOnlyList<string>>` | Upload 1–3 JPEGs one at a time to `{userId}/{jobId}/{index}.jpg` in the `meal-import-pictures` bucket; removes the earlier ones if a later one fails |
  | DeleteMealImportPictures | `Command(IReadOnlyList<string> Paths)` → `Result` | Remove objects through the Storage API (empty list = success) |
  | CreatePictureMealImport | `Command(Guid Id, IReadOnlyList<string> PicturePaths, string Language, string? Hint)` → `Result<MealImport>` | Insert the pending picture job (no `source_url`); a blank hint becomes null, a hint over 200 characters fails |
  | TriggerAnalyzeMealPicture | `Command(Guid JobId)` → `Result` | POST `{ jobId }` to `<MealImportFunction:url>/api/AnalyzeMealPicture` with `x-functions-key` |

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
  - `Features/MealImport/Models/MealImportErrorCode.cs`: the `error_code` values (contract
    `MealWiz/tests/Contracts/meal_import_error_code.json`). `Features/MealImport/Models/MealImportErrorMessages.cs`:
    `L.ToMessage(code)`, the text the state container exposes as `JobErrorMessage` and `LatestErrorMessage`.
  - `Features/MealImport/Models/MealImportJobStatus.cs` and `Features/MealImport/Models/MealImportEntryState.cs`: UI state.
  - `Features/MealImport/Models/MealImportFunctionOptions.cs`: Function URL and key from appsettings.
- **Migrations:**
  - `supabase/migrations/20260806120000_add_meal_imports_table.sql`: the table, plus adding it to the
    `supabase_realtime` publication.
  - `supabase/migrations/20261012120000_allow_client_to_fail_meal_imports.sql`
  - `supabase/migrations/20261106120000_add_meal_import_error_codes.sql`: `error_code` with its check constraint, in
    the client's update grant.
  - `supabase/migrations/20261112120000_add_meal_import_pictures.sql`: the `meal-import-pictures` bucket with its
    owner-only policies, the picture-job columns and checks, and the four new error codes.
  - `supabase/migrations/20261113120000_set_meal_import_created_at_on_client_insert.sql`: a trigger that sets
    `created_at` to the server's `now()` on a client's insert (the Function's freshness check and daily limit rely on
    it); the service role keeps its own value. The client never sends `created_at`.
- **Tests:**
  - unit: `Features/MealImport/UnitTests/` (Handlers, Models, State)
  - bUnit: `Features/MealImport/ComponentTests/ImportMealPageTests.cs`
  - integration: `Features/MealImport/IntegrationTests/MealImportHandlerTests.cs`
  - E2E: `MealWiz/tests/MauiWASM.Tests/E2ETests/MealImportJourneyTests.cs`
  - pgTAP: `supabase/tests/database/meal_imports.test.sql`, `supabase/tests/database/meal_import_pictures.test.sql` (the bucket policies)
  - contracts: `MealWiz/tests/Contracts/`

## Authentication

password reset or logout the handler asks the `AuthenticationStateProvider` to re-read the session (logout goes through
`SessionKeeper` instead, below). Unauthenticated users hitting an `[Authorize]` page are sent to `/login` by
`Layout/RedirectToLogin.razor`. The session is persisted per host (see [Hosts](#hosts)).
per host (see [Hosts](#hosts)).

- **Folder:** `Features/Authentication/`
- **Pages:** the only pages without `[Authorize]`. None has the bottom nav: `Layout/MainLayout.razor` shows it to
  signed-in users only.
  - `Features/Authentication/Login/LoginPage.razor` at `/login`: email and password fields (`login-email`,
    `login-password`) with labels above them, the full-width "Log in" button (`login-submit`) and "No account yet?
    Create one" (`login-create-account`), a link to `/signup`. "Forgot password?" (`login-forgot-password`) links to
    `/forgot-password`.
  - `Features/Authentication/RequestPasswordReset/ForgotPasswordPage.razor` at `/forgot-password`: email (`forgot-email`)
    and "Send reset link" (`forgot-submit`), which asks Supabase to email a reset link that lands on the app's
    `/reset-password` (`NavigationManager` base URI). Then a check-your-email note (`forgot-check-email`), worded so
    it doesn't say whether the account exists (Supabase answers the same either way). `forgot-login` goes back.
  - `Features/Authentication/ResetPassword/ResetPasswordPage.razor` at `/reset-password`, where the email's link
    lands with the recovery session in the URL fragment (implicit flow, so any browser works). It keeps the URL in
    memory and replaces the address bar's with one without the tokens. New password and repeat (`reset-password`,
    `reset-password-repeat`), "Set password" (`reset-submit`) sets it, signs the user in and goes to the meal plan.
    An expired or used link, or none, shows `reset-expired` and "Send a new link" (`reset-request-again`).
  - **Web only.** The redirect has to be on Supabase's allow list: locally `additional_redirect_urls` in
    `supabase/config.toml` (the dev run on `localhost:5053` and the test app host on `127.0.0.1`); in production the
    dashboard's Redirect URLs. The MAUI app's base URI (`https://0.0.0.0/`) isn't reachable from an email, so a reset
    asked for in the MAUI app lands on the Site URL instead.
  - `Features/Authentication/SignUp/SignUpPage.razor` at `/signup`: email, password and repeat password
    (`signup-email`, `signup-password`, `signup-password-repeat`), "Create account" (`signup-submit`) and "Already
    have an account? Log in" (`signup-login`). Passwords that don't match are caught on the page. With a session
    back it goes to the meal plan; without one (the Supabase project requires email confirmation) it shows a
    check-your-email note (`signup-check-email`). Supabase enforces the password minimum (6 locally).
- **Email links:** `Features/Authentication/EmailLinks/EmailLinkBuilder.cs` (scoped) builds the absolute URL an email link opens: the web head uses its own base URL, MAUI the `WebApp:url` setting (`EmailLinkOptions`, registered per head); `OpensInBrowser` is true when one is configured. Forgot password uses it, and in MAUI its check-your-email note adds "The link opens in your browser." (`forgot-opens-in-browser`). `Features/Authentication/GotrueErrorCode.cs` reads `error_code` from a GoTrue error body (`weak_password`, `same_password`, `email_exists`, `validation_failed`). `Features/Authentication/SupabaseAuthSettings.cs` (url + anon key, both heads) is for the two direct GoTrue calls of the Account handlers.
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
  | Logout | `Command()` → `Result` | `SignOut`, then `SessionKeeper.SignOutLocallyAsync()`; a failed sign-out changes nothing. The button then reloads the app fully on `/login` |
  | RequestPasswordReset | `Command(string Email, string RedirectTo)` → `Result` | `ResetPasswordForEmail` with the redirect (implicit flow) |
  | ResetPassword | `Command(Uri ResetLink, string NewPassword)` → `Result<Session?>` | reads the link's fragment (`Features/Authentication/ResetPassword/ResetLink.cs`; GoTrue's error, e.g. an expired link, fails it), `SetSession` with the recovery tokens, `Update` the password (signs out again if it's refused), then refresh auth state |

- **Related:** `Services/Authentication/CustomAuthStateProvider.cs` builds claims from the Supabase access token and
  notifies on `SessionKeeper.Changed` (which also relays the SDK's auth events); reading the state never notifies.
  Signed in means a session with a refresh token that the keeper hasn't ended.
  `Services/Authentication/SessionKeeper.cs` (singleton) replaces the SDK's refresh (`AutoRefreshToken` is off, and the
  SDK's `RetrieveSessionAsync` deletes expired sessions): it loads the stored session, refreshes it when the access
  token expires within 60 s, then at 80 % of its lifetime. Offline, timeout, 5xx and 429 keep the session and retry
  after 15 s, doubling to 5 min (`RefreshFailure.cs` classifies); only a 400/401/403 destroys the session and raises
  `SessionRejected`. `RefreshNowAsync()` refreshes at once with the same failure rules (after an email change). `SignOutLocallyAsync()` never calls the server. `Services/Authentication/SessionGate.razor`
  wraps `Routes.razor`: a spinner (`session-gate-loading`) until `StartAsync()` is done (10 s cap), and a forced
  reload on `/login` when the session is rejected.
- **Texts:** `Features/Authentication/AuthenticationResource.cs` (the signed-out pages, AuthShell and Log out). A bad
  reset link always shows `Reset_LinkInvalid`; GoTrue's English reason is never shown.
- **Emails:** SignUp stores the app's language as `user_metadata.lang`. The confirmation and password-reset emails,
  `supabase/templates/confirmation.html` and `supabase/templates/recovery.html` (subjects in `supabase/config.toml`),
  are Danish for `da` and English otherwise. Locally they land in Mailpit (port 54324), which every verify stack runs. The hosted project's
  templates and subjects are pasted from these files by hand (Supabase dashboard, Authentication, Emails).
- **Tests:**
  - bUnit: `Features/Authentication/ComponentTests/`
  - integration: `Features/Authentication/IntegrationTests/AuthenticationHandlerTests.cs`
  - unit: `Services/Authentication/UnitTests/` (`CustomAuthStateProviderTests.cs`, `SessionKeeperTests.cs`, `RefreshFailureTests.cs`)
  - bUnit: `Services/Authentication/ComponentTests/SessionGateTests.cs`
  - E2E: `MealWiz/tests/MauiWASM.Tests/E2ETests/AuthJourneyTests.cs`

## Account

The settings page: who is signed in, a dark-mode toggle (`IThemeService`, stored in local storage under
`mealwiz.theme`), the language dropdown, change password, change email, logout and delete account. No tables.
Texts: `Features/Account/AccountResource.cs`.

The language dropdown (`language-select`, a native select so phones show their own picker) lists Device default, then
`SupportedLanguages.All` by native name, with the stored choice preselected. A change goes through
`ILanguagePreference` (`Localization/ILanguagePreference.cs`): it saves the code (Device default removes it) and reloads
the page in the new language. Web: `Localization/LocalStorageLanguagePreference.cs`, registered in
`MealWiz/MauiWASM/ServiceCollectionExtensions.cs`. MAUI: `MealWiz/MealWiz/MealWiz/Providers/PreferencesLanguagePreference.cs`,
which also applies the culture in-process, since a reload there doesn't re-run `MauiProgram`.

- **Folder:** `Features/Account/`
- **Pages:** `Features/Account/AccountPage.razor` at `/account`: a SubPageHeader ("Profile" / "Account", back
  `account-back` to `/`), a profile card with a green avatar (`account-avatar`, the email's initial) and "Signed in as"
  the email (`account-email`, the access token's `email` claim from the cascading `AuthenticationState`), a settings
  group with the Dark mode row (`dark-mode-switch`), the two forms below and the `LogoutButton` (a full reload on
  `/login`). Styles in `Features/Account/AccountPage.razor.css`.
  `Features/Account/EmailChangePage.razor` at `/email-change` (public, no `[Authorize]`): where the email-change links
  land. From the URL fragment it shows `email-change-confirm-other` (`message=`: click the other address's link),
  `email-change-done` (`type=email_change` with a token) or `email-change-invalid`, each with a link to the Account page
  (`email-change-account-link`); the fragment is removed from the address bar and the link's own session is not
  adopted. On done it calls `SessionKeeper.RefreshNowAsync()` when this browser is signed in, so the Account page shows
  the new address.
- **Components:** `Features/Account/ChangePassword/ChangePasswordForm.razor` (`change-password-new`,
  `change-password-repeat`, `change-password-submit`; a mismatch is caught on the page) and
  `Features/Account/ChangeEmail/ChangeEmailForm.razor` (`change-email-new`, `change-email-submit`, and the pending note
  `account-email-pending` loaded on init) and `Features/Account/DeleteAccount/DeleteAccountPanel.razor` (a red outlined
  `delete-account-open` below Log out opens an inline panel: `delete-account-password`, `delete-account-confirm`,
  `delete-account-cancel`; a failure shows in `delete-account-error`; success is `NavigateTo("/login", forceLoad: true)`).
- **Handlers:**

  | Handler | Request → result | Does |
  |---|---|---|
  | ChangePassword | `Command(string NewPassword)` → `Result` | `Auth.Update` on the current session (GoTrue keeps it and revokes the others); `weak_password` and `same_password` have their own messages |
  | ChangeEmail | `Command(string NewEmail)` → `Result` | `PUT /auth/v1/user` with `HttpClient` (the SDK sends no redirect) and a `redirect_to` header from `EmailLinkBuilder.Build("email-change")`; GoTrue emails both addresses; `email_exists` and `validation_failed` have their own messages |
  | GetPendingEmailChange | `Query()` → `Result<string?>` | `GET /auth/v1/user`, reads `new_email` (the SDK's `User` drops it); null when none |
  | DeleteAccount | `Command(string Password)` → `Result` | `POST {MealImportFunction:Url}/api/DeleteAccount` with the session's bearer token, the password and `x-functions-key`; 204 → `SessionKeeper.SignOutLocallyAsync()` (never `SignOut`, the user is gone), 403 / 401 / anything else → their own messages and no sign-out |

- **Texts:** `Features/Account/AccountResource.resx` / `.da.resx` (marker `AccountResource.cs`).
- **Related:** `Theme/ThemeService.cs`, `Theme/IThemeService.cs`, `Theme/MealWizTheme.cs`
- **Tests:**
  - bUnit: `Features/Account/ComponentTests/` (`AccountPageTests`, `ChangePasswordFormTests`, `ChangeEmailFormTests`, `EmailChangePageTests`, `DeleteAccountPanelTests`)
  - integration: `Features/Account/IntegrationTests/AccountHandlerTests.cs` (GoTrue and Mailpit)
  - unit: `Features/Account/UnitTests/DeleteAccountTests.cs` (stubbed Function)
  - E2E: `AuthJourneyTests` (change password; change email; delete account with the Function stubbed by a route)

## Functions: DeleteAccount

The other function in the same app. `POST /api/DeleteAccount` (`AuthorizationLevel.Function`) takes the caller's access
token as `Authorization: Bearer` and `{ password }`. With the service-role key it asks GoTrue who the token belongs to,
checks the password with a password grant, and only then hard-deletes the user (admin API); the `created_by` foreign
keys cascade to every row the user owns. Responses: 204 deleted, 400 bad body or blank password, 401 missing or invalid
token, 403 wrong password (nothing deleted), 502 any other GoTrue failure (logged, not returned). It reuses
`Supabase:Url` and `Supabase:ServiceRoleKey`; there is no new setting.

- **Function:** `MealWiz/src/MealWiz.Functions/Functions/DeleteAccount/DeleteAccountFunction.cs` (the HTTP trigger, outcome → status) and
  `MealWiz/src/MealWiz.Functions/Functions/DeleteAccount/DeleteAccountHandler.cs` (`IDeleteAccountHandler` → `DeleteAccountOutcome`, a named `HttpClient`).
- **Tests:** `MealWiz/tests/MealWiz.Functions.Tests/Functions/DeleteAccount/UnitTests/` and `MealWiz/tests/MealWiz.Functions.Tests/Functions/DeleteAccount/IntegrationTests/`;
  pgTAP `supabase/tests/database/delete_account_cascade.test.sql`.

## Functions: ScrapeRecipe

The server half of MealImport, in a separate Azure Functions app (isolated worker, net10.0) that deliberately
doesn't reference MealWiz.Shared. `POST /api/ScrapeRecipe` (`AuthorizationLevel.Function`) takes `{ jobId, url }`. It
loads the `meal_imports` row with the service-role key and acts only if the row is still `pending`. It always fetches
the row's stored `source_url`, never the URL in the request body. It parses the recipe (JSON-LD, then Microdata, then
OpenGraph), maps it to the payload, and writes the result back. A failure also writes `error_code`: `invalid_url`
(the stored URL isn't absolute http/https), `fetch_failed` (any RecipeFetcher error: redirects, status, size cap,
DNS, non-public host, network, timeout), `no_recipe_found` (no parser found a recipe) or `unknown` (anything else).

Responses: 200 means the row is resolved; 502 means the row couldn't be read or written and is still pending, so the
client fails it; 400 means a bad body. `MealWiz/src/MealWiz.Functions/README.md` covers local setup, the function
key, CORS and deployment. A picture job (no `source_url`) is left pending with a warning: AnalyzeMealPicture resolves it.

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
  - `MealWiz/src/MealWiz.Functions/Shared/Models/`: `MealImportDb`, `MealImportPayload`, `RecipeImportStatus` and
    `RecipeImportErrorCode`.
    These deliberately duplicate the Shared types (FN-01); `MealWiz/tests/Contracts/` keeps them in sync.
- **DI:** `MealWiz/src/MealWiz.Functions/ServiceCollectionExtensions.cs`, called from `MealWiz/src/MealWiz.Functions/Program.cs`.
- **Tests:** `MealWiz/tests/MealWiz.Functions.Tests/`
  - `Functions/ScrapeRecipe/UnitTests/` and `Functions/ScrapeRecipe/IntegrationTests/`
  - `Shared/UnitTests/` and `Shared/IntegrationTests/`

## Functions: AnalyzeMealPicture

The picture half of MealImport, in the same Functions app. `POST /api/AnalyzeMealPicture` (`AuthorizationLevel.Function`)
takes `{ jobId }` (other fields are ignored). It resolves a pending picture job with the service-role key, in this
order: load the row (502 if it can't be read); leave a non-pending row or a link job alone (200); **claim** the row
(conditional update of `updated_at` where it's pending and still null, so a duplicate call does nothing); the row must
be at most 5 minutes old and at most 1 minute ahead (else `timed_out`); its 1–3 paths must be lowercase and under
`{created_by}/` with no `.`, `..` or empty segment (else `picture_unreadable`); the owner's picture jobs created today
(UTC day, any status, this one included) must not exceed the daily limit (else `daily_limit_reached`); download the
pictures one at a time (any failure → `picture_unreadable`); analyse; write back with the conditional
`status = pending` update, so the first resolution wins. The reply, 200/400/502, means the same as ScrapeRecipe's.

The AI seam is `IMealPictureAnalyzer`. `AnthropicMealPictureAnalyzer` sends all the pictures (labelled "Image 1:", …)
and one instruction ("Write the meal in English|Danish.", plus the hint) in one request to Claude through the official
`Anthropic` NuGet package, with low effort and schema-constrained output (`MealPictureReading`), and maps the answer:
`written_recipe` → `IsGuessed` false, `dish` → true, `no_meal` or a blank name → `no_meal_found`. A stop reason other
than `end_turn`, a 429/5xx/other API error or a missing API key → `ai_unavailable` (the API isn't called without a key);
a 413 or an image 400 → `picture_unreadable`; a timeout or the 80 s budget → `timed_out`. Every error carries its code
in a `PictureImportError`. Nothing logs picture bytes, the key or the hint.

- **Folder:** `MealWiz/src/MealWiz.Functions/Functions/AnalyzeMealPicture/`
  - `AnalyzeMealPictureFunction.cs`: the HTTP trigger.
  - `AnalyzeMealPictureHandler.cs`: `IAnalyzeMealPictureHandler.HandleAsync` → `Result`.
  - `PictureJobRules.cs`: the pure rules: path check, freshness window, the daily-limit setting.
  - `IMealPictureAnalyzer.cs`: the seam, `MealPictureAnalysisRequest` and `PictureImportError`.
  - `AnthropicMealPictureAnalyzer.cs`, `MealPicturePrompt.cs`, `MealPictureReading.cs`, `MealPictureMapper.cs`: the production analyzer.
- **Settings:** `Anthropic:ApiKey` (required; `Anthropic__ApiKey` in Azure), `Anthropic:Model` (default `claude-haiku-5-5`),
  `MealPictureImport:DailyLimit` (default 20; 0 turns analysis off). See `MealWiz/src/MealWiz.Functions/README.md`.
- **Shared code:** `MealWiz/src/MealWiz.Functions/Shared/Models/MealImportPictures.cs` (bucket id, limits), duplicated
  on the client (FN-01).
- **Tests:** `MealWiz/tests/MealWiz.Functions.Tests/Functions/AnalyzeMealPicture/UnitTests/` (analyzer through
  `TestInfrastructure/FakeAnthropicHandler.cs`, mapper, prompt, rules, trigger) and `MealWiz/tests/MealWiz.Functions.Tests/Functions/AnalyzeMealPicture/IntegrationTests/` (the handler
  with a fake analyzer against local Supabase and Storage). No test calls the real AI service.

## Functions: CleanUpMealPictures

A timer Function (`0 17 3 * * *`, 03:17 UTC daily, `AzureWebJobsStorage`) that deletes every picture in the
`meal-import-pictures` bucket older than 24 hours, so none outlives about two days (the app deletes a job's pictures on
save or discard; this catches the rest, including a deleted account's). Storage can't list by age, so
`CleanUpMealPicturesHandler.HandleAsync(cutoff)` asks the service-role-only database function
`public.expired_meal_import_pictures(cutoff)` for the names and removes them through the Storage API in batches of 100,
at most 5,000 per run (never SQL). A failed batch is logged and ends the run; the next day tries again.

- **Folder:** `MealWiz/src/MealWiz.Functions/Functions/CleanUpMealPictures/`
  (`CleanUpMealPicturesFunction.cs`: the timer, passes now minus 24 h; `CleanUpMealPicturesHandler.cs`).
- **Migration:** `supabase/migrations/20261112130000_add_expired_meal_import_pictures.sql`.
- **Tests:** `MealWiz/tests/MealWiz.Functions.Tests/Functions/CleanUpMealPictures/UnitTests/` and
  `MealWiz/tests/MealWiz.Functions.Tests/Functions/CleanUpMealPictures/IntegrationTests/` (local Storage; collection
  `PictureJobs`); pgTAP `supabase/tests/database/expired_meal_import_pictures.test.sql`.

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
      Two buttons go in `<div class="mw-sheet-footer-pair">`; `.mw-sheet-footer-cancel` is grey and `.mw-sheet-footer-danger` is red.
    - ConfirmDrawer shows a red trash circle, the title (`confirm-drawer-title`), the description
      (`confirm-drawer-description`), Cancel (`confirm-drawer-cancel`, only closes) and a red confirm button
      (`confirm-drawer-confirm`, runs `onConfirm`; labelled Delete, or `confirmLabel` when given).
  - See [ARCHITECTURE.md](ARCHITECTURE.md#drawers).
- **`Services/Authentication/`:** `CustomAuthStateProvider.cs` (the auth state, from the Supabase session), `SessionKeeper.cs` (load, refresh and end the session), `SessionGate.razor` (the startup gate).
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
  - `SupabaseHelper.cs`, `DateTimeExtensions.cs` (`StartOfWeek`, `EndOfWeek`), `DictionaryExtensions.cs`.
- **`Models/Supabase/`:** the shapes of Supabase error responses, used by `ResultHelper`.
- **`Components/`:** generic UI.
  - `Components/NavMenu.razor`, the three tabs (Plan, also active on `/plan-week`; Groceries; Meals; `nav-meal-plan`, `nav-grocery-list`,
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
| `grocery_items` | GroceryList | `remote_schema` | `is_picked`, `amount`, `is_manual` (default true; false for plan-added items; since `add_grocery_item_sources`) |
| `grocery_item_sources` | GroceryList | `add_grocery_item_sources` | `fk_grocery_item` → `grocery_items` on delete cascade; `fk_planned_meal` → `meal_plans-meals` and `fk_ingredient` → `ingredients`, both on delete set null; unique (`fk_planned_meal`, `fk_ingredient`), NULLs distinct. INSERT and UPDATE also require your own item, and your own planned day and ingredient when set |
| `meal_plans` | MealPlans | `remote_schema` | `start_date`, `end_date`. One plan per user and week: unique (`created_by`, `start_date`), since `one_plan_per_week_and_meal_per_day`, which merged existing duplicates into the lowest id |
| `meal_plans-meals` | MealPlans | `remote_schema` | `fk_meal_plan`, `fk_meal` (→ `meals` on delete cascade, since `remove_deleted_meals_from_plans`), `meal_date`. The hyphen means it always needs quoting. INSERT and UPDATE also require your own plan, and your own meal or a system meal. One planned meal per plan and date: unique (`fk_meal_plan`, `meal_date`), since `one_plan_per_week_and_meal_per_day`, which kept the newest of existing duplicates. `fk_meal_plan` has no ON DELETE action, so a plan that still has planned meals can't be deleted |
| `meal_imports` | MealImport | `add_meal_imports_table` | uuid id; in the Realtime publication. The client may only update its own row `pending` → `failed`, and only `status`, `error_code`, `error_message`, `updated_at` and `updated_by` (column grants); `error_code` is checked against the ten codes in `meal_import_error_code.json`; the Function writes the result with the service role; since `add_meal_import_pictures` it is a link job or a picture job (`source_url` nullable; `picture_paths` 1–3, `language`, `hint`, checked by `meal_imports_kind_check`), and the client can set the new columns on insert only |

### Functions

| Function | Feature | Created in | Notes |
|---|---|---|---|
| `public.expired_meal_import_pictures(cutoff)` | MealImport | `add_expired_meal_import_pictures` | Names the pictures of `meal-import-pictures` created before the cutoff. `security definer`; only `service_role` may execute it. The clean-up Function removes them through the Storage API |

### Storage buckets

| Bucket | Feature | Created in | Notes |
|---|---|---|---|
| `meal-import-pictures` | MealImport | `add_meal_import_pictures` | Private; 5 MB; JPEG only. Paths are `{userId}/{jobId}/{index}.jpg`. Policies "Meal import pictures: owner inserts / reads / deletes" allow `authenticated` only inside their own `{userId}/` folder; there is no UPDATE policy. The service role (the Function) reads everything. Objects aren't removed with SQL; the app deletes through the Storage API |
