---
name: mw-diagnosing-bugs
description: A disciplined loop for MealWiz bugs - build a red-capable feedback loop (usually a failing test), reproduce, minimise, rank hypotheses, instrument, fix with a regression test, clean up. Use from /implement-issue in bug mode, or when the user says diagnose/debug, or reports something broken, throwing, failing or slow.
---

<!-- Adapted from mattpocock/skills `skills/engineering/diagnosing-bugs` (MIT). See `.claude/skills/THIRD_PARTY_NOTICES.md`. -->

# Diagnosing bugs

A discipline for bugs. Skip a phase only when you can say why (e.g. the Linear issue already names the line and a
test pins it).

Start from the issue: many MealWiz bugs were filed with a root cause, and some are **pinned by tests** that assert
today's wrong behaviour (`docs/agents/TESTING.md`, "Bugs currently pinned by tests"). A pinned test is your loop:
flip its assertion to the correct behaviour and it goes red.

## Redact

Never show secrets in commands, output or artifacts: write `<REDACTED>`. Use the local stack's keys from
`supabase status`, never production values. Production is debugged read-only, and only if the user asks.

## Phase 1: Build a feedback loop

**This is the skill.** With a tight pass/fail signal that goes red on _this_ bug, you will find the cause. Without
one, staring at code won't save you. Spend disproportionate effort here.

Ways to build one, roughly in this order:

1. **A failing test** at the seam that reaches the bug (see the seams table in `mw-tdd`):
   - handler or RLS behaviour → integration test in `MealWiz/tests/MealWiz.Shared.Tests/Features/<F>/IntegrationTests/`
     against local Supabase (`supabase start` first);
   - pure logic → unit test; a component → bUnit test;
   - a policy → pgTAP in `supabase/tests/database/`, run with `supabase test db`;
   - Function parsing or SSRF → `MealWiz/tests/MealWiz.Functions.Tests/`.
2. **A Playwright journey or step** in `MealWiz/tests/MauiWASM.Tests/E2ETests/`, when only the real browser shows it
   (Realtime, navigation, rendering). Its failure artifacts (screenshot, console log) are described in the verify
   skill.
3. **SQL as a specific user**, in a transaction you roll back: `docker exec -i supabase_db_MealWiz psql -U postgres`,
   then `set local role authenticated;` and `set local request.jwt.claims = '{"sub":"<uuid>","role":"authenticated"}';`.
4. **A differential loop**: the same input through `origin/main` and your branch, or through two configs.
5. **The app in the Browser pane** (the `web` preview in `.claude/launch.json`, user `test@test.com`) when a human
   has to look. Last resort: it isn't a repeatable loop, so turn what you see into a test.

Then **tighten** it: run one class (`--filter-class`), assert on the exact symptom (not "didn't throw"), and make it
deterministic (own users and rows, `FakeTimeProvider`, no shared data).

**Non-deterministic bugs:** raise the reproduction rate (loop it, add load, narrow timing) until it's debuggable.

**When you can't build a loop:** stop and say so. List what you tried. Ask for what would unblock it (a captured
artifact, steps, access). Don't hypothesise without a loop.

**Done when** you can name one command you've already run that is red-capable (it asserts the user's symptom),
deterministic, fast (seconds) and runs unattended. No command, no Phase 2.

## Phase 2: Reproduce and minimise

Run the loop and watch it go red with the failure the **issue** describes, not a nearby one. Then cut inputs, rows
and steps one at a time, rerunning each time, until every remaining element is load-bearing.

## Phase 3: Hypothesise

Write **3–5 ranked, falsifiable hypotheses** before testing any: "If X is the cause, changing Y makes the bug go
away." A hypothesis without a prediction is a vibe; sharpen or drop it. In an interactive session, show the list to
the user; in an unattended bug session, carry on with your ranking.

## Phase 4: Instrument

One probe per prediction, one variable at a time. Debugger or a targeted log at the boundary that separates
hypotheses; never "log everything". Tag every debug line with a unique prefix (`[DEBUG-a4f2]`) so cleanup is one
grep.

## Phase 5: Fix and regression test

Write the regression test **before the fix**, at a seam that exercises the real bug pattern. If the only seam is too
shallow to reproduce it, that's a finding: say so in the PR and the Linear issue.

1. Turn the minimised repro into a failing test (or flip the pinning test's assertion, keeping its comment accurate).
2. Watch it fail for the right reason.
3. Fix.
4. Watch it pass, then rerun the original loop.
5. Update "Bugs currently pinned by tests" in `docs/agents/TESTING.md` and the pinning comment if the bug was pinned.

## Phase 6: Clean up

- [ ] The original repro no longer reproduces
- [ ] The regression test passes (or the missing seam is documented)
- [ ] No `[DEBUG-` lines left (`git grep "\[DEBUG-"`)
- [ ] Throwaway scripts deleted
- [ ] The hypothesis that turned out right is stated in the commit message and PR body
