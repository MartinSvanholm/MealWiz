---
name: mw-tdd
description: Test-driven development for MealWiz (xUnit v3, bUnit, Playwright, pgTAP). Red → green, one slice at a time, at agreed seams. Use when building a feature or fixing a bug test-first, from /implement-issue, or when the user mentions TDD, red-green, or writing tests first.
---

<!-- Adapted from mattpocock/skills `skills/engineering/tdd` (MIT). See `.claude/skills/THIRD_PARTY_NOTICES.md`. -->

# Test-driven development

TDD is the red → green loop. This skill is what makes the loop produce tests worth keeping: what a good test is,
where tests go, the anti-patterns, and the rules of the loop. Consult every section before and during the loop, not
after.

**Where, how to name, which trait, which infrastructure:** `docs/agents/TESTING.md` ("Which tests for which change",
"Traits", "Writing a new test"). The rules behind it are TEST-01, TEST-02 and UI-01 in `docs/agents/ARCHITECTURE.md`.
Never create a test project (TEST-01).

## What a good test is

Tests verify behaviour through public interfaces, not implementation details. The code can change entirely; the
tests shouldn't. A good test reads like a specification: `SaveMeal_NewMeal_InsertsItOwnedByTheUser` tells you
exactly what capability exists, and it survives refactors because it doesn't care about internal structure.

See [tests.md](tests.md) for examples and [mocking.md](mocking.md) for what to mock.

## Seams: where tests go

A **seam** is the public boundary you test at. In MealWiz:

| Seam | Test | Suite |
|---|---|---|
| A handler, sent through MediatR as the signed-in user | Integration test against local Supabase, through RLS | `shared-integration` |
| Pure logic in a handler, model, helper or mapping | Unit test | `shared-unit` / `functions-unit` |
| A state container, with `IMediator` mocked | Unit test | `shared-unit` |
| A page or component | bUnit test (`ComponentTestBase`), selecting by `data-testid` | `shared-unit` |
| A user flow across pages | Playwright journey | `e2e` (and `e2e-smoke` if it meets the smoke criteria) |
| A table's RLS | pgTAP file in `supabase/tests/database/` | `db` |
| The Function, parsing or SSRF | Functions unit / integration tests | `functions-*` |

**Test only at agreed seams.** If the issue or the project spec lists its test seams, those are agreed. Otherwise,
write down the seams you'll test, each with a one-line note on what it catches and what it misses, and confirm them
with the user before writing a test. Agreeing seams up front puts the effort on critical paths and complex logic, not
on every edge case.

## Anti-patterns

- **Implementation-coupled**: mocks your own collaborators, tests private methods, or asserts on call counts. The
  tell: the test breaks when you refactor but behaviour hasn't changed.
- **Tautological**: the expected value is computed the way the code computes it, so the test passes by construction.
  Expected values come from an independent source: a literal, a worked example, the spec.
- **Horizontal slicing**: all tests first, then all code. Bulk tests verify _imagined_ behaviour. Work in **vertical
  slices** instead: one test → one implementation → repeat, each test a tracer bullet that responds to what the last
  cycle taught you.
- **Shared state**: depending on another test's rows, or on the seeded `test@test.com`. Tests run in parallel across
  classes; integration and E2E tests create their own users and rows.
- **Hiding a bug**: `Skip` on a failing test. Pin today's behaviour with a comment naming the Linear issue instead
  (TESTING.md, "Pin known bugs").

## Rules of the loop

- **Red before green.** Write the failing test, run it, and see it fail for the reason you expect. Then write only
  enough code to pass it. Don't anticipate future tests or add speculative features.
- **One slice at a time.** One seam, one test, one minimal implementation per cycle.
- **Run narrowly while looping**, e.g.
  `dotnet test --project MealWiz/tests/MealWiz.Shared.Tests --filter-class "*MealHandlerTests"`. Integration tests
  need `supabase start` first; without it they skip, which is not green.
- **Refactoring is not part of the loop.** Tidy up once the slice is green, then rerun.
- **Finish with verify.** `scripts/verify.ps1 smoke` (or `full`, per TESTING.md) in the background. The exit code is
  the verdict.
