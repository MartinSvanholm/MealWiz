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
# Build entire solution
dotnet build MealWiz/MealWiz.sln

# Run web (Blazor WASM — open via browser after publish or use dotnet serve)
dotnet build MealWiz/MauiWASM/MauiWASM.csproj
```

## Database Migrations
```bash
# Requires Supabase CLI
supabase link --project-ref <PROJECT_REF>
supabase db push
```
Migration files: `MealWiz/supabase/migrations/YYYYMMDDHHMMSS_description.sql`

## Configuration
Supabase credentials in `MealWiz/MauiWASM/wwwroot/appsettings.*.json`:
```json
{ "Supabase": { "url": "...", "key": "..." } }
```
Three profiles: `Development` (local Supabase on `127.0.0.1:54321`), `Staging`, `Production`.

## Additional Documentation
- [Architectural Patterns](.claude/docs/architectural_patterns.md) — CQRS handler structure, domain/DB model mapping, state containers, drawer system, DI registration
