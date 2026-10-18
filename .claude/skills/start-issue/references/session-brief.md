# Sub-session brief template

Each sub-issue session starts fresh (`context: "fresh"`), so the `prompt` and `background` must carry everything it
needs. Fill this in per sub-issue. Keep the shared parts identical across siblings, so they all follow the same
rules.

## `prompt`

```
Start Linear issue <ID> "<title>" (<url>). It is part of <PARENT-ID>, being built in parallel with <sibling IDs>.

Read first:
- The issue itself (get_issue <ID>) and the contract document "<PARENT-ID> work plan and contract" attached to
  <PARENT-ID> (<doc url>). The contract is binding. Build exactly to the contract items your issue uses: <list,
  e.g. DB-1, JSON-1, HTTP-1>.
- .claude/docs/architectural_patterns.md and .claude/skills/start-issue/references/repo-notes.md.

Setup:
- Move <ID> to In Progress.
- `git fetch origin`. Your worktree starts from dev; make sure it's at origin/dev's tip.
- Use Linear's gitBranchName for <ID> as your branch.

You own: <paths/areas>. Do not edit: <paths owned by siblings>. For a shared file, change only <the specific
part>.
If a sibling's work doesn't exist yet, code against the contract and fake the other side in tests. Don't wait for
it, and don't implement it yourself.

If you need anything in the contract changed (a field, a name, a status code, an interface member), stop and
message the coordinator session (<coordinator session id>) with the proposed change and why. Don't change it
locally.

Workflow:
1. Explore, then plan in plan mode and get the user's approval.
2. Ask implementation questions with AskUserQuestion before building. The user wants to be asked.
3. Build, then verify with targeted builds/tests (see repo-notes.md; run anything slow in the background).
4. Commit, push or open a PR (to dev) only when the user asks. Don't leave fixes uncommitted in extra worktrees.
   If you find out-of-scope work, propose it, and say in your report where it lives.

Report back (your final message is relayed to the coordinator), leading with:
- branch name and commit (or "uncommitted"), plus the PR link if any;
- what you verified and how (commands, test counts, manual checks), and what you could not verify;
- any contract assumptions you made, and open questions for the user.
```

## `background` (at least 80 characters)

```
MealWiz is a .NET 9 Blazor WASM + MAUI hobby meal-planning app on Supabase (MediatR CQRS, FluentResults,
MudBlazor), with a standalone .NET 10 isolated Azure Function App. <PARENT-ID> is <one-line feature summary>. Its
sub-issues are worked in parallel sessions against a shared contract (Linear document on <PARENT-ID>). This
session's piece: <one line>.
User preferences: ask before building; never commit secrets; commit/push/PR only when asked; PRs go to dev.
Environment: Windows; PowerShell 5.1 (no &&, mangles quotes passed to native exes, so use git commit -F <file>).
supabase/snippets/ is the user's own folder; never commit it.
```

## Call shape

```
start_session(
  initiation: "user_asked",          // the user invoked the skill to start the work
  title: "<ID>: <short title>",
  name: "<id-lowercase>-<slug>",     // e.g. mea-15-import-ui
  context: "fresh",
  use_worktree: true,
  source_branch: "dev",              // "main" for hotfix work
  branch_name: "<id>-<slug>",
  prompt: <above>,
  background: <above>)
```

Start all sub-issues that can start now in the **same turn**, after `mark_chapter`. Write the bold title line and
one sentence for each, right before its call.
