# Bug group session brief

## `prompt`

```
Run the `implement-issue` skill in bug mode for these Linear bugs, in one branch and one PR to dev:
- <ID> "<title>" (<url>)<flags, e.g. " — may need an RLS change: ask before that part">
- <ID> "<title>" (<url>)

<Cleared by the user before start: <decision, quoting the user>. This counts as the user's answer for that flag.>

Likely files (a prediction, not a limit): <files>.
Other groups running in parallel own: <their files>. Don't edit those; if your fix needs to, stop and ask.
<Pinned by tests: <test file>. Flip the pinning assertion as TESTING.md says.>

Verify level: <smoke|full>.

Merge rule, written by the user for this flow: when CI is green on your PR to dev, merge it yourself without asking
(`scripts/merge-to-dev.sh <n>`, never `gh pr merge` directly), unless the skill's "ask before fixing" list applied and the user hasn't
cleared it.
```

## `background` (at least 80 characters)

```
MealWiz: a .NET 9 Blazor WebAssembly + MAUI meal planner on Supabase (MediatR CQRS, FluentResults, MudBlazor), with
a .NET 10 Azure Function for meal import. A /fix-bugs coordinator grouped the open Bug issues by the files they touch
and started one session per group in parallel worktrees from dev. Rules: CLAUDE.md and docs/agents/. PRs go to dev
only, never main. Fix only your group's bugs; file anything else in Linear with the Bug label and no project.
Windows: PowerShell 5.1, so write commit messages and PR bodies to a file (git commit -F, gh pr create --body-file).
```

## Call shape

```
start_session(
  initiation: "user_asked",
  title: "Fix <IDs>: <short theme>",      // e.g. "Fix MEA-35, MEA-37: meal plan handlers"
  name: "bugs-<ids>",                     // e.g. bugs-mea-35-37
  context: "fresh",
  use_worktree: true,
  source_branch: "dev",
  branch_name: "<ids>-<slug>",            // e.g. mea-35-37-mealplan-fixes
  prompt: <above>,
  background: <above>)
```
