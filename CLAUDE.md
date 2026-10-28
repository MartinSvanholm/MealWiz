# MealWiz

## Overview
A meal planning app. Users keep meals with ingredients, plan one meal per day week by week, merge the week's
ingredients into a grocery list, and import meals from recipe URLs. It runs on the web (Blazor WebAssembly) and as a
MAUI app (mobile and desktop) that share one UI project.

## Read before you work

| Before you… | Read |
|---|---|
| look for a feature's files | [docs/agents/FEATURES.md](docs/agents/FEATURES.md): every feature's folder, pages, handlers, state, tables and tests |
| write or review code | [docs/agents/ARCHITECTURE.md](docs/agents/ARCHITECTURE.md): the rules. A broken MUST rule blocks a merge; reviewers cite rule IDs |
| write tests or pick which to run | [docs/agents/TESTING.md](docs/agents/TESTING.md): every suite, which to run for which change, how to write tests |
| run verify or triage a failure | the `verify` skill (`.claude/skills/verify/SKILL.md`) |

## How work is done
Work is tracked in Linear (team MealWiz, issues `MEA-xx`, labels Bug, Feature, Improvement). Project skills in
`.claude/skills/` run the workflow:

| Skill | Use it to |
|---|---|
| `/spec-project` | interview the user about a feature and write the spec into the Linear project description |
| `/plan-project` | research, split a specced project into issues with blocking relations, then coordinate one session per issue (max 4 at once) |
| `/implement-issue` | implement one issue (feature mode, plan-gated, the user merges) or a group of bugs (bug mode, merges itself on green) |
| `/fix-bugs` | group the open Bug issues by the files they touch and start one bug-mode session per group |

They build on adapted copies of Matt Pocock's skills (`mw-grilling`, `mw-to-spec`, `mw-to-tickets`, `mw-tdd`,
`mw-code-review`, `mw-diagnosing-bugs`, `mw-research`); see `.claude/skills/THIRD_PARTY_NOTICES.md`.

Rules for every session:
- **Work only on the issues you were given.** Don't start, fix or edit other issues.
- **File bugs you find, don't fix them.** Create a Linear issue in team MealWiz with the **Bug** label, status
  Backlog and **no project**, even when you found it while working on a project. Say what you saw and where.
- PRs target `dev`. Feature PRs are merged by the user; only bug-mode sessions merge their own PR, and only to `dev`.
  Nothing goes to `main` (see below).

## Tech stack
- **Runtime:** C#. The apps target .NET 9. `MealWiz.Functions`, the test projects and `scripts/verify.cs` need the
  .NET 10 SDK.
- **UI:** Blazor WebAssembly, MudBlazor 8, CodeBeam.MudBlazor.Extensions.
- **Data:** PostgreSQL through Supabase, using the C# SDK directly from the client. There's no API layer, so RLS
  does all authorization. Realtime is used for meal imports.
- **Patterns:** vertical slices, CQRS with MediatR 13, FluentResults 4 for errors, and scoped state containers.
- **Auth:** Supabase Auth with a custom `AuthenticationStateProvider`.
- **Server:** one Azure Functions app (`ScrapeRecipe`, isolated worker, net10.0) for meal import.
- **Hosting:** Azure Static Web Apps and Azure Functions, deployed by GitHub Actions.

## Projects
| Path | Role |
|---|---|
| `MealWiz/MealWiz/MealWiz.Shared/` | All feature code: pages, components, handlers, state, models |
| `MealWiz/MauiWASM/` | Web head. DI is in `ServiceCollectionExtensions.cs` |
| `MealWiz/MealWiz/MealWiz/` | MAUI head. Needs mobile workloads, so verify and CI don't build it |
| `MealWiz/src/MealWiz.Functions/` | Azure Function `ScrapeRecipe`. Deliberately doesn't reference Shared |
| `MealWiz/tests/MealWiz.Shared.Tests/` | Unit, bUnit and integration tests for Shared |
| `MealWiz/tests/MealWiz.Functions.Tests/` | Unit and integration tests for Functions |
| `MealWiz/tests/MauiWASM.Tests/` | Web-head unit tests and Playwright E2E |
| `MealWiz/tests/Contracts/` | JSON contracts shared by the Functions and Shared tests |
| `supabase/` | Migrations, `seed.sql`, pgTAP tests (`supabase/tests/database/`), local config |

Each app project has exactly one test project. Never add another (TEST-01).

## Build & run
```bash
# Build the web app (MealWiz.sln also builds the MAUI head, which needs the Android/iOS workloads)
dotnet build MealWiz/MauiWASM/MauiWASM.csproj

# Run the web app on http://localhost:5053 against local Supabase (`supabase start` first)
dotnet run --project MealWiz/MauiWASM/MauiWASM.csproj --launch-profile http
```
In the Claude desktop app, `.claude/launch.json` has this as the `web` preview. Log in as the seeded
`test@test.com` / `tester`. That user exists **only in the local stack** (`supabase/seed.sql`); never try it against
Staging or Production.

## Verifying changes
One command builds and tests everything relevant. **Run it before you say a change is done**, and report its verdict
line (e.g. `verify smoke: PASSED in 3m10s`). The exit code is the verdict: 0 passed, 1 failed, 2 couldn't run. Never
claim success on a non-zero exit.

```bash
scripts/verify.ps1 smoke     # Windows (PowerShell)
scripts/verify.sh smoke      # Linux, macOS, Git Bash
```

| Level | Runs | Use it for |
|---|---|---|
| `fast` | docs check + build + unit and bUnit tests. No Docker. ~1 min | iterating; docs-only or test-only changes |
| `smoke` | `fast` + local Supabase (started if needed) + integration tests + Smoke E2E | **before finishing** any app, handler, page or migration change |
| `full` | `smoke` + every E2E journey + pgTAP RLS tests | migrations and RLS, meal import/Realtime, meal plans, before a PR |

`smoke` and `full` take minutes, so run them in the background, one at a time. TESTING.md says which tests to run for
which change; the verify skill explains how to read failures. CI (`.github/workflows/CI.yml`) runs `verify full` on
every PR.

## Branches and deployment
- Feature branches and PRs target **`dev`**. A push to `main` deploys to production.
- `.github/workflows/deploy.yml` runs on every push to `main`. It runs `supabase db push` against production, then
  deploys the web app to Static Web Apps and the Function app, each only if its code changed.
- Never run `supabase db push` by hand, and never edit a migration that has reached `main` (DB-02). There is no
  staging deploy.

## Database
- Migrations: `supabase/migrations/<YYYYMMDDHHMMSS>_<description>.sql`. Apply them locally with `supabase db reset`,
  which wipes local data and re-runs `supabase/seed.sql`.
- Every table has a `created_by` owner column, RLS, and pgTAP tests (DB-01).
- Local ports: API 54321, DB 54322, Studio 54323.

## Configuration
- **Web:** `MealWiz/MauiWASM/wwwroot/appsettings.{Development,Staging,Production}.json`, with a `Supabase` section
  (`url`, anon `key`) and a `MealImportFunction` section (`url`, `key`).
  - Development points at local Supabase on `127.0.0.1:54321`.
  - Production's Function key stays empty in git; `deploy.yml` injects it.
- **MAUI:** always loads the embedded `MealWiz/MealWiz/MealWiz/Resources/appsettings.Development.json`.
- **Functions:** a gitignored `local.settings.json`, copied from
  `MealWiz/src/MealWiz.Functions/local.settings.json.example`. Its README covers keys, CORS and deployment.
- Only the anon key may reach a client (SEC-01).

## Keeping these docs current
A PR that adds, moves or renames a feature, handler, page, table, test suite or a documented path updates
`docs/agents/` and this file in the same PR (DOC-01). The `docs` step of verify runs at every level. It fails when a
feature folder, handler, table or verify step is missing from the docs, or when a path in backticks doesn't exist.
