# Feature session brief

Each issue's session starts fresh, so `prompt` and `background` carry everything it needs. The rules themselves live
in the `implement-issue` skill; the brief points at it and adds what's specific to this issue. Keep the shared parts
identical across siblings.

## `prompt`

```
Run the `implement-issue` skill for Linear issue <ID> "<title>" (<url>), in feature mode.

It's part of the project "<project>" (<project url>). Running in parallel with you: <sibling IDs, or "none">.
- Spec: the project description. Research: the project's "Research" document. <Contract: the project's "Contract"
  document; this issue uses <items, e.g. DB-1, API-1>. It's binding.>
- You own: <areas/paths>. Don't edit: <siblings' areas>. In shared files, only: <the specific edit>.
- <Anything the coordinator learned that the issue doesn't say: prior art to copy, a gotcha. Omit if nothing.>

Verify level: <smoke|full>.

You started in plan mode: explore, ask, and present your plan for the user's approval before writing code.
When you're blocked or have a question, follow the stop rules in the skill: ask the user here, and message the
session that started you in one line so it knows you're waiting.
```

## `background` (at least 80 characters)

```
MealWiz: a .NET 9 Blazor WebAssembly + MAUI meal planner on Supabase (MediatR CQRS, FluentResults, MudBlazor), with
a .NET 10 Azure Function for meal import. Feature "<project>" is built as one Linear issue per session, in parallel
worktrees from dev; a coordinator session started you and starts the next issues as PRs merge. This session's piece:
<one line>. Rules: CLAUDE.md and docs/agents/. PRs go to dev and the user merges them. Work only on <ID>; bugs you
find go in Linear with the Bug label and no project. Windows: PowerShell 5.1, so write commit messages and PR bodies
to a file (git commit -F, gh pr create --body-file).
```

## Call shape

```
start_session(
  initiation: "user_asked",
  title: "<ID>: <short title>",
  name: "<id>-<slug>",            // e.g. mea-52-fav-toggle
  context: "fresh",
  use_worktree: true,
  source_branch: "dev",
  branch_name: "<id>-<slug>",
  permission_mode: "plan",
  prompt: <above>,
  background: <above>)
```
