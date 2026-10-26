# MealWiz

## Overview
Meal planning and grocery management app. Users create meals with ingredients, build date-ranged meal plans, and auto-generate grocery lists from those plans. Targets web (Blazor WASM) and mobile/desktop (MAUI).

## Tech Stack
- **Runtime:** .NET 9.0, C#
- **UI:** Blazor WebAssembly, MudBlazor v8
- **Database:** PostgreSQL via Supabase (direct C# SDK — no separate REST/GraphQL layer)
- **Patterns:** CQRS via MediatR v13, FluentResults v4 for error handling
- **Auth:** Supabase Auth + JWT + custom `AuthenticationStateProvider`
- **State:** Scoped DI service containers + Blazored.LocalStorage
- **Deployment:** Azure Static Web Apps + GitHub Actions CI/CD

## Projects
| Project | Role |
|---------|------|
| `MealWiz.Shared` | All feature code: pages, components, CQRS handlers, state, models |
| `MauiWASM` | Blazor WASM web entry point — DI registration, Supabase client setup |
| `MealWiz` (MAUI) | Mobile/desktop targets sharing the Shared project |

## Key Directories
```
MealWiz/MealWiz/
  MealWiz.Shared/
    Features/          # Vertical-slice features: Meals, Ingredients, MealPlans, GroceryList, Authentication
    Services/          # Cross-cutting services (auth state, drawer state)
    Helpers/           # Extension methods, DI registration helper
    Components/        # Generic reusable UI components
    Layout/            # App shell (MainLayout, nav, redirect)

  MealWiz/             # MAUI project (mobile/desktop)
    Providers/         # Session persistence (CustomSupabaseSessionProvider)

MealWiz/MauiWASM/      # Blazor WASM web project
  Program.cs           # WASM host — service registration + Supabase client setup
  Provider/            # Session persistence (CustomSupabaseSessionProvider)

MealWiz/supabase/
  migrations/          # SQL migration files managed by Supabase CLI
```

## Build & Run
```bash
# Build the web app (the whole MealWiz.sln also builds the MAUI head, which needs the Android/iOS workloads)
dotnet build MealWiz/MauiWASM/MauiWASM.csproj

# Run the web app on http://localhost:5053 against local Supabase (`supabase start` first)
dotnet run --project MealWiz/MauiWASM/MauiWASM.csproj --launch-profile http
```
In the Claude desktop app, `.claude/launch.json` has this as the `web` preview, so you can open it in the browser pane.
Log in as the seeded `test@test.com` / `tester`. That user exists **only in the local stack** (`supabase/seed.sql`);
never try it against Staging or Production.

## Verifying changes
One command builds and tests everything relevant. **Run it before you say a change is done**, and report its verdict
line (e.g. `verify smoke: PASSED in 3m10s`). The exit code is the verdict: 0 passed, 1 failed, 2 couldn't run. Never
claim success on a non-zero exit.

```bash
scripts/verify.ps1 smoke     # Windows (PowerShell)
scripts/verify.sh smoke      # Linux, macOS, Git Bash
```

| Level | What it runs | Use it for |
|-------|--------------|------------|
| `fast` | build + unit and bUnit tests. No Docker. ~1 min | Quick checks while iterating on any C# or Razor change |
| `smoke` | `fast` + starts local Supabase if needed + integration tests + the Smoke E2E tests (boot, login, every nav page, meal CRUD, grocery list) | **Before finishing** any change to app code, handlers, pages or migrations |
| `full` | `smoke` + every E2E journey + pgTAP RLS tests (`supabase test db`) | Migrations and RLS policies, meal import/Realtime, meal plan flows, and before a PR |

Docs-only or test-only changes in one project can make do with `fast`. It prints a short summary (counts per suite,
each failing test with its first assertion message, E2E screenshots/traces); full logs are in `artifacts/verify/<level>/`.
smoke and full take a few minutes, so run them in the background. The `verify` skill
(`.claude/skills/verify/SKILL.md`) explains how to read and triage failures.

CI (`.github/workflows/CI.yml`) runs the same `verify full`, one step per test project (`--only`), with coverage in the
job summary and a `test-results` artifact on failure. A new suite goes into `scripts/verify.cs`; CI fails until an
`--only` list in CI.yml runs it.

## Database Migrations
```bash
# Requires Supabase CLI
supabase link --project-ref <PROJECT_REF>
supabase db push
```
Migration files: `supabase/migrations/YYYYMMDDHHMMSS_description.sql` (at the repo root)

## Configuration
Supabase credentials in `MealWiz/MauiWASM/wwwroot/appsettings.*.json`:
```json
{ "Supabase": { "url": "...", "key": "..." } }
```
Three profiles: `Development` (local Supabase on `127.0.0.1:54321`), `Staging`, `Production`.

## Additional Documentation
- [Architectural Patterns](.claude/docs/architectural_patterns.md) — CQRS handler structure, domain/DB model mapping, state containers, drawer system, DI registration
