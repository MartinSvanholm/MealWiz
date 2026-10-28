# Cross-project contracts

`MealWiz.Functions` writes `meal_imports` rows and the Blazor client (`MealWiz.Shared`) reads them. The two
projects deliberately share no code, so the payload and status types are duplicated on each side:

| File | Functions side | Shared side |
|------|----------------|-------------|
| `parsed_meal.json` | `MealImportPayload` serializes to exactly this | `ParsedMeal` deserializes it with every field set |
| `meal_import_status.json` | `RecipeImportStatus` names and DB values | `MealImportStatus` names and DB values |

Both test projects link these files as content. Change a type on one side and that side's test fails;
update the file and the other side's test fails until it matches too.
