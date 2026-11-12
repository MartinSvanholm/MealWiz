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
Work is tracked in Linear (team MealWiz, issues `MEA-xx`, labels Bug, Feature, Improvement), in two levels. There are
no sub-issues: an issue's layers (database, handlers, UI) are steps in its plan.

| Level | Is | Worked by |
|---|---|---|
| Project | a feature; its description is the spec. One branch, `project/<slug>`, and one PR to `dev` | the main (coordinator) session, `/plan-project`, then a ship session, `/ship-project` |
| Issue | a group of user stories that leaves the app working (one to three issues per project): one branch, one PR **into the project branch**; the coordinator writes its plan into the description just before it starts | its own session (Sonnet, medium effort), started by the coordinator; in a cloud container, the main session itself, one issue at a time |

A feature goes through four steps, each a project skill in `.claude/skills/`:

| Step | Skill | What it does |
|---|---|---|
| 1. Spec | `/spec-project` | interviews the user about a feature and writes the spec into the Linear project description. A feature that fits one issue can skip step 2 |
| 2. Plan | `/plan-project` | splits the project into one to three large issues, plans only the issue that starts next (later ones just before they start), asks the user once, then coordinates one Sonnet session per issue and starts the ship session at the end |
| 3. Implement | `/implement-issue` | builds one issue story by story, test-first, with `verify fast` only, and merges its PR into the project branch itself on green CI. No review of its own |
| 4. Ship | `/ship-project` | a fresh session on Opus at medium effort: runs `verify full`, reviews the whole project diff, fixes what it finds, attaches UI screenshots to Linear, and opens the one PR to `dev` with the architecture and the critical changes. The user merges it |

Bugs have their own flow: `/fix-bugs` diagnoses the open Bug issues, writes a fix plan into each one that needs it
for the user to approve, groups them by the files they touch, and starts one Sonnet session per group that runs
`/implement-issue` in bug mode (fast checks, a PR to `dev`, merged by the session on green CI). In a cloud container
it fixes the groups itself, one at a time.

They build on adapted copies of Matt Pocock's skills (`mw-grilling`, `mw-to-spec`, `mw-to-tickets`, `mw-tdd`,
`mw-code-review`, `mw-diagnosing-bugs`, `mw-research`); see `.claude/skills/THIRD_PARTY_NOTICES.md`.

Rules for every session:
- **Work only on the issues you were given.** Don't start, fix or edit other issues.
- **File bugs you find, don't fix them.** Search Linear first (team MealWiz, label Bug, the error text or file name);
  if it's there, add a comment with what you saw. Otherwise create a Linear issue in team MealWiz with the **Bug**
  label, status Backlog and **no project**, even when you found it while working on a project. Say what you saw and
  where.
- Issue PRs target the project branch and are merged by their session on green CI. The ship PR (project branch to
  `dev`) is merged by the user. Bug PRs target `dev` and are merged by their session on green CI, through
  `scripts/merge-pr.sh`. Nothing goes to `main` (see below).

## Tech stack
- **Runtime:** C#. The apps target .NET 9. `MealWiz.Functions`, the test projects and `scripts/verify.cs` need the
  .NET 10 SDK.
- **UI:** Blazor WebAssembly, MudBlazor 8, CodeBeam.MudBlazor.Extensions.
- **Data:** PostgreSQL through Supabase, using the C# SDK directly from the client. There's no API layer, so RLS
  does all authorization. Realtime is used for meal imports.
- **Patterns:** vertical slices, CQRS with MediatR 13, FluentResults 4 for errors, and scoped state containers.
- **Languages:** English and Danish, from `.resx` resources through `IStringLocalizer`; the device language
  picks one at startup (ARCHITECTURE.md, UI-03 and "Localization").
- **Auth:** Supabase Auth with a custom `AuthenticationStateProvider`.
- **Server:** one Azure Functions app (`ScrapeRecipe` for meal import, `DeleteAccount`; isolated worker, net10.0).
- **Hosting:** Azure Static Web Apps and Azure Functions, deployed by GitHub Actions.

## Projects
| Path | Role |
|---|---|
| `MealWiz/MealWiz/MealWiz.Shared/` | All feature code: pages, components, handlers, state, models |
| `MealWiz/MauiWASM/` | Web head. DI is in `ServiceCollectionExtensions.cs` |
| `MealWiz/MealWiz/MealWiz/` | MAUI head. Needs mobile workloads, so verify and CI don't build it |
| `MealWiz/src/MealWiz.Functions/` | Azure Functions `ScrapeRecipe` and `DeleteAccount`. Deliberately doesn't reference Shared |
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
One command builds and tests everything relevant. **Run it, at the level your role calls for, before you say a
change is done**, and report its verdict
line (e.g. `verify local: PASSED in 2m10s`). The exit code is the verdict: 0 passed, 1 failed, 2 couldn't run. Never
claim success on a non-zero exit.

```bash
scripts/verify.ps1 local     # Windows (PowerShell)
scripts/verify.sh local      # Linux, macOS, Git Bash
```

| Level | Runs | Use it for |
|---|---|---|
| `fast` | docs check + build + unit and bUnit tests. No Docker, no lock. ~1 min | **issue and bug sessions, before their PR**; docs-only or test-only changes. Add `local --only <steps>` for just the Supabase-backed steps a plan names |
| `local` | `fast` + local Supabase + integration tests + pgTAP RLS tests. No browser tests | a change made outside the project flow, with no ship session behind it. Add `--e2e <class>` for the journey it touches (TESTING.md) |
| `smoke` | `fast` + local Supabase + integration tests + Smoke E2E | when you want the browser smoke set locally |
| `full` | `smoke` + every E2E journey + pgTAP RLS tests | **the ship session, once per project**, before the PR to `dev`; CI on every PR |

Issue sessions run only `fast` so they never hold the shared Supabase stack for long. The full run and the review
happen once per project, in the ship session.

In a Claude Code cloud container (Ubuntu, `CLAUDE_CODE_REMOTE=true`), `scripts/cloud-setup.sh` installs the .NET 10
SDK and the Supabase CLI and starts Docker. Make `bash scripts/cloud-setup.sh` the environment's setup script so each
session starts ready. A cloud session has its own Docker and Supabase stack. There, the workflow skills run in one
session: it works the issues one at a time, each on its own branch from the latest project branch with its own PR,
and before each new issue it checks its open PRs (conflicts, CI) and merges the green ones into the project branch
(implement-issue, "Cloud mode"). The ship session is still a separate, fresh session.

Browser tests are slow and suffer most when several sessions run at once, so the full E2E suite runs in CI
(`.github/workflows/CI.yml` runs `verify full` on every PR), and a PR merges only on green CI. Run verify in the
background. All worktrees share one local Supabase stack: verify takes a machine-wide lock for it (a second run waits)
and resets the database to your worktree's migrations and seed first. So don't run `supabase db reset` or
`supabase migration up` by hand while sessions run in parallel; run verify instead. TESTING.md says which tests to run
for which change; the verify skill explains how to read failures.

## Branches and deployment
- A feature's issue branches merge into its project branch, `project/<slug>`, and that branch reaches **`dev`** through
  one ship PR. Bug PRs target **`dev`**. A push to `main` deploys to production.
- `.github/workflows/deploy.yml` runs on every push to `main`. It runs `supabase db push` against production, then
  deploys the web app to Static Web Apps and the Function app, each only if its code changed.
- Never run `supabase db push` by hand, and never edit a migration that has reached `main` (DB-02). There is no
  staging deploy.

## Database
- Migrations: `supabase/migrations/<YYYYMMDDHHMMSS>_<description>.sql`. Apply them locally with `supabase db reset`,
  which wipes local data and re-runs `supabase/seed.sql`.
- Every table has a `created_by` owner column, RLS, and pgTAP tests (DB-01).
- Local ports: API 54321, DB 54322, Studio 54323 (verify starts the stack without Studio unless you pass `--studio`).

## Configuration
- **Web:** `MealWiz/MauiWASM/wwwroot/appsettings.{Development,Staging,Production}.json`, with a `Supabase` section
  (`url`, anon `key`) and a `MealImportFunction` section (`url`, `key`).
  - Development points at local Supabase on `127.0.0.1:54321`.
  - Production's Function key stays empty in git; `deploy.yml` injects it.
- **MAUI:** always loads the embedded `MealWiz/MealWiz/MealWiz/Resources/appsettings.Development.json`.
  Its `WebApp:url` (the web app, `http://localhost:5053/` in Development) is where email links open; the web head leaves it out and links to itself.
- **Functions:** a gitignored `local.settings.json`, copied from
  `MealWiz/src/MealWiz.Functions/local.settings.json.example`. Its README covers keys, CORS and deployment.
- Only the anon key may reach a client (SEC-01).

## Keeping these docs current
A PR that adds, moves or renames a feature, handler, page, table, test suite or a documented path updates
`docs/agents/` and this file in the same PR (DOC-01). The `docs` step of verify runs at every level. It fails when a
feature folder, handler, table or verify step is missing from the docs, or when a path in backticks doesn't exist.
