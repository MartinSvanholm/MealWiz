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
- Sub-issues: <[DB] ID, [BE] ID, [FE] ID with their blockers, or "none (one layer)">. Work them inline or in
  subagents as the plan's Sub-issues section says; they all land in your one PR.
- You own: <areas/paths>. Don't edit: <siblings' areas>. In shared files, only: <the specific edit>.
- <Anything the coordinator learned that the issue doesn't say: prior art to copy, a gotcha. Omit if nothing.>

<Known issues filed during this project: <ID> <one line>, … Don't file these again; if you hit one, add a comment to it.>

Verify level: <local | local --e2e <class>>. Don't run smoke or full locally; CI runs every E2E journey.

Your plan is the `## Implementation plan` in the issue description. The user approved it in the coordinator, so
execute it step by step: don't re-plan, explore for alternatives, or ask about what it settles. If a step doesn't fit
the code, stop (stop rule 8 in the skill) and message the session that started you; it owns the plan. For any other
block or question, follow the stop rules: ask the user here, and message the session that started you in one line
so it knows you're waiting.
```

## `background` (at least 80 characters)

```
MealWiz: a .NET 9 Blazor WebAssembly + MAUI meal planner on Supabase (MediatR CQRS, FluentResults, MudBlazor), with
a .NET 10 Azure Function for meal import. Feature "<project>" is built as one Linear issue (user story) per session,
in parallel worktrees from dev, each split into [DB]/[BE]/[FE] sub-issues; a coordinator session planned every issue
in full, started you and starts the next issues as PRs merge. Your approved plan is in your issue. This session's piece:
<one line>. Rules: CLAUDE.md and docs/agents/. PRs go to dev and the user merges them. Work only on <ID>; bugs you
find go in Linear with the Bug label and no project. Windows: PowerShell 5.1, so write commit messages and PR bodies
to a file (git commit -F, gh pr create --body-file).
```

## Cloud mode

In cloud mode ([plan-project](../SKILL.md#cloud-mode)) use the same `prompt` with these changes, and pass the
`background` as `append_system_prompt`:

- Add this paragraph before the plan paragraph at the end:

  ```
  You run in your own Claude Code cloud container, in cloud mode (see the skill). Your branch is <id>-<slug>, made
  from dev: check `git branch --show-current` first, and if it says dev, run `git checkout -b <id>-<slug>`. Never
  commit to dev. If `dotnet` or `supabase` is missing, run `bash scripts/cloud-setup.sh` first. You have your own
  Supabase stack, so verify with `scripts/verify.sh`.
  ```
- And this one after it:

  ```
  The coordinator is session <coordinator session id>. It isn't woken when your turn ends, so `send_message` it a
  one-line report every time you're blocked or unblocked, open your PR, merge, or get stuck, as the skill says.
  ```
- In `background`, replace the last sentence ("Windows: PowerShell 5.1, …") with: "Linux cloud container (bash),
  with its own Docker and Supabase; GitHub through the GitHub MCP tools, no gh CLI."

## Call shape (local mode)

```
start_session(
  initiation: "user_asked",
  title: "<ID>: <short title>",
  name: "<id>-<slug>",            // e.g. mea-52-fav-toggle
  context: "fresh",
  use_worktree: true,
  source_branch: "dev",
  branch_name: "<id>-<slug>",
  model: "<current Sonnet id>",   // e.g. claude-sonnet-5-5
  effort: "medium",
                                  // no permission_mode: it inherits the coordinator's; never "plan"
  prompt: <above>,
  background: <above>)
```
