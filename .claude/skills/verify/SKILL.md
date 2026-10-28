---
name: verify
description: Run MealWiz's one-command verification (build, unit/bUnit, integration, Playwright E2E, pgTAP) and triage what fails. Use before saying any code change is done, and whenever asked to verify, test, or check that the app still works.
---

# Verify a change

`scripts/verify.ps1 <level>` (Windows) or `scripts/verify.sh <level>` (bash) builds the app and runs the test suites
for that level. It prints only a short summary; the exit code is the verdict. What each suite covers, which tests a
change needs, and how to write them is in `docs/agents/TESTING.md`.

## 1. Pick the level

| Level | Runs | When |
|-------|------|------|
| `fast` | docs check + build + unit and bUnit tests, no Docker | while iterating |
| `smoke` | `fast` + local Supabase (started if needed) + integration tests + Smoke E2E | **before saying a change is done** |
| `full` | `smoke` + all E2E journeys + `supabase test db` (pgTAP RLS tests) | migrations, RLS, meal import/Realtime, meal plans, before a PR |

## 2. Run it

From the repo root:

```bash
scripts/verify.ps1 smoke     # PowerShell
scripts/verify.sh smoke      # bash
```

`smoke` and `full` take minutes (the E2E fixture publishes the app in Release; the first run also pulls Docker images
and Chromium), so run them in the background and wait for the completion notice rather than polling. Run one verify at
a time: the suites share the local Supabase stack and its sign-in rate limit.

## 3. Read the result

```
verify smoke: FAILED in 3m02s
  build                   ok
  supabase                running
  Shared unit + bUnit     402 passed  (25s)
  ...
  E2E smoke               8 passed, 1 failed  (1m40s)

FAILED SmokeTests.Page_Renders(page: "MealsPage", path: "/meals", marker: "meal-list-item")
    Assert.Fail(): /meals (MealsPage) didn't render: Blazor's "An unhandled error has occurred" bar is showing.
    Browser errors (2), first:
    Unhandled exception rendering component: ...
    artifacts: MealWiz/tests/MauiWASM.Tests/bin/Debug/net10.0/TestResults/e2e/SmokeTests.Page_Renders(...) (...)

Full logs: artifacts/verify/smoke/
```

- Exit code **0** means passed, **1** means a build or test failed, and **2** means it couldn't run (unknown level, no Supabase CLI, Docker not running). Report the verdict line. Never call a change done on a non-zero exit.
- `artifacts/verify/<level>/` has `build.log`, `<suite>.log`, `<suite>/<suite>.trx` (every test with its full message and stack), `supabase.log` and `db.log`.
- A failed CI run has the same folder (plus the E2E traces) in its `test-results` artifact: `gh run download <run-id> -n test-results`. CI runs `verify full` one step at a time (`--only shared-unit,shared-integration`, ...); you can rerun just those steps locally the same way (after `--only build`, or a normal run).

## 4. Triage by what failed

- **docs**: each line names a doc and what it's missing: a feature folder, handler or table not in
  `docs/agents/FEATURES.md`, a verify step not in `docs/agents/TESTING.md`, or a path in backticks that doesn't exist.
  Update the doc to match the code (DOC-01). Don't delete the entry to make the check pass. `docs.log` has the full list.
- **build**: the summary lists `file(line,col): error CSxxxx`. Fix it there. `build.log` has the rest.
- **unit / bUnit**: the test name tells you the file: `MealWiz/tests/<Project>/**/<Class>.cs`. Read the assertion message first, then the test, then the code under test. To rerun one class: `dotnet test --project MealWiz/tests/MealWiz.Shared.Tests --filter-class "*MealsPageTests"`.
- **integration** (Shared/Functions `Category=Integration`): Shared runs its handlers against local Supabase as a
  throwaway user, through RLS. Functions uses the service role and a local recipe web server.
  - "REQUIRE_SUPABASE is set, but…" means the stack isn't reachable. Check `supabase status`.
  - A GoTrue `429` means the sign-in rate limit was hit by back-to-back runs. Wait a few minutes.
  - A migration change needs `supabase db reset` to apply locally. It wipes local data and re-runs `seed.sql`.
- **E2E** (`MauiWASM.Tests/E2ETests`): the message usually names the page and the first browser error. Then:
  1. Open `screenshot.png` from the artifacts folder with the Read tool to see what the page showed.
  2. Read `console.log` in the same folder: browser console, page errors, failed requests and Realtime frames, with timestamps.
  3. If you still need more, `pwsh MealWiz/tests/MauiWASM.Tests/bin/Debug/net10.0/playwright.ps1 show-trace <folder>/trace.zip` opens the step-by-step trace (needs a display, so it's for humans).
  4. Rerun a single journey: `dotnet test --project MealWiz/tests/MauiWASM.Tests --filter-class "*MealJourneyTests"`.
- **pgTAP**: the summary shows the `not ok` lines. The files are under `supabase/tests/database/`.
- **SKIPPED** at smoke/full shouldn't happen (REQUIRE_SUPABASE turns skips into failures). If you see it, the reason is shown.

Some E2E tests pin known bugs on purpose (MEA-38, MEA-42, MEA-43; see `MealWiz/tests/MauiWASM.Tests/E2ETests/README.md`).
If your change fixes one of those, flip the assertion as the comment there says, rather than deleting it.

## 5. Look at it yourself (optional)

When a test can't tell you whether the UI looks right, open the app:

1. `supabase start` (smoke/full already started it).
2. In the Claude desktop app, start the `web` preview from `.claude/launch.json`. It runs `dotnet run` for MauiWASM on http://localhost:5053 in Development, pointing at local Supabase.
3. Log in as `test@test.com` / `tester`. That user is seeded by `supabase/seed.sql` and exists only in the local stack.
4. The `supabase-studio` entry opens Studio on http://127.0.0.1:54323 to inspect the data.
