# MealWiz.Functions

Standalone Azure Functions app (isolated worker, .NET 10) implementing `ScrapeRecipe` — the scrape/parse/write-back
step of the "import a meal from a URL" flow ([MEA-13](https://linear.app/mealwiz/issue/MEA-13)). Deliberately does
**not** reference `MealWiz.Shared`.

## Local development

1. Copy `local.settings.json.example` to `local.settings.json` and fill in `Supabase:ServiceRoleKey` (`supabase status`
   after starting the local stack prints it as `service_role key`). `local.settings.json` is gitignored — never commit it.
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
