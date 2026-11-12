# Bug group session brief

## `prompt`

```
Run the `implement-issue` skill in bug mode for these Linear bugs, in one branch and one PR to dev:
- <ID> "<title>" (<url>) — fix plan in the issue<flags, e.g. " — may need an RLS change: ask before that part">
- <ID> "<title>" (<url>) — no plan: diagnose it yourself

A bug marked "fix plan in the issue" has an approved `## Fix plan` in its description: execute it (red test, check it
fails for the stated reason, fix) and don't re-diagnose. If the red test doesn't fail as the plan says, or the fix
doesn't fit the code, stop (stop rule 8 in the skill) and message the session that started you; it owns the plan.

<Cleared by the user before start: <decision, quoting the user>. This counts as the user's answer for that flag.>

Likely files (a prediction, not a limit): <files>.
Other groups running in parallel own: <their files>. Don't edit those; if your fix needs to, stop and ask.
<Pinned by tests: <test file>. Flip the pinning assertion as TESTING.md says.>

<Known issues filed during this run: <ID> <one line>, … Don't file these again; if you hit one, add a comment to it.>

Verify with `fast`, plus only the step that holds a bug's red test when it isn't a unit or bUnit test
(`local --only <step>`, or `local --e2e <class>` for a bug pinned by an E2E test). CI runs `verify full` on the PR.

Merge rule, written by the user for this flow: when CI is green on your PR to dev, merge it yourself without asking
(`scripts/merge-pr.sh <n>`, never `gh pr merge` directly), unless the skill's "ask before fixing" list applied and the user hasn't
cleared it.
```

## `background` (at least 80 characters)

```
MealWiz: a .NET 9 Blazor WebAssembly + MAUI meal planner on Supabase (MediatR CQRS, FluentResults, MudBlazor), with
a .NET 10 Azure Function for meal import. A /fix-bugs coordinator diagnosed the open Bug issues, wrote approved fix
plans into the ones that needed them, grouped them by the files they touch and started one session per group in
parallel worktrees from dev. Rules: CLAUDE.md and docs/agents/. PRs go to dev
only, never main. Fix only your group's bugs; file anything else in Linear with the Bug label and no project.
Windows: PowerShell 5.1, so write commit messages and PR bodies to a file (git commit -F, gh pr create --body-file).
```

## Call shape (local mode)

```
start_session(
  initiation: "user_asked",
  title: "Fix <IDs>: <short theme>",      // e.g. "Fix MEA-35, MEA-37: meal plan handlers"
  name: "bugs-<ids>",                     // e.g. bugs-mea-35-37
  context: "fresh",
  use_worktree: true,
  source_branch: "dev",
  branch_name: "<ids>-<slug>",            // e.g. mea-35-37-mealplan-fixes
  model: "<current Sonnet id>",           // e.g. claude-sonnet-5-5
  effort: "medium",
  prompt: <above>,
  background: <above>)
```
