# Session briefs

Each session starts fresh, so `prompt` and `background` carry everything it needs. The rules themselves live in the
skills; a brief points at the skill and adds what's specific.

## Issue session

### `prompt`

```
Run the `implement-issue` skill for Linear issue <ID> "<title>" (<url>), in feature mode.

It's part of the project "<project>" (<project url>). Project branch: `project/<slug>`. <Your branch is based on it
and your PR targets it. | This is the project's only issue: work on the project branch itself and open no PR.>
Running in parallel with you: <sibling IDs and the areas they own, or "none">.
- Spec: the project description. Research: the project's "Research" document, if there is one.
- <Anything the coordinator learned that the issue doesn't say: a gotcha, a decision made since. Omit if nothing.>

<Known issues filed during this project: <ID> <one line>, … Don't file these again; if you hit one, add a comment to it.>

Your plan is the `## Implementation plan` in the issue description; the user approved the approach in the
coordinator. Execute it story by story: don't re-plan or explore for alternatives. If a step doesn't fit the code,
stop (stop rule 8 in the skill) and message the session that started you; it owns the plan. For any other block or
question, follow the stop rules: ask the user here, and message the session that started you in one line so it knows
you're waiting.

Verify with `fast` (plus the `--only` steps the plan names). Don't run `local`, `smoke` or `full` in whole: CI runs
`verify full` on your PR, and a ship session verifies and reviews the whole project before it reaches dev. When CI is
green on your PR, merge it into the project branch yourself (`scripts/merge-pr.sh <n>`) and report `MERGED <url>`.
```

### `background` (at least 80 characters)

```
MealWiz: a .NET 9 Blazor WebAssembly + MAUI meal planner on Supabase (MediatR CQRS, FluentResults, MudBlazor), with
a .NET 10 Azure Function for meal import. Feature "<project>" is built as a few large Linear issues, each one session
and one PR into the project branch project/<slug>; a coordinator session planned this issue, started you and plans
the next issue when yours merges. A separate ship session later verifies and reviews the whole project and opens the
one PR to dev, which the user merges. This session's piece: <one line>. Rules: CLAUDE.md and docs/agents/. Work only
on <ID>; bugs you find go in Linear with the Bug label and no project. Windows: PowerShell 5.1, so write commit
messages and PR bodies to a file (git commit -F, gh pr create --body-file).
```

### Call shape (local mode)

```
start_session(
  initiation: "user_asked",
  title: "<ID>: <short title>",
  name: "<id>-<slug>",              // e.g. mea-52-favourites
  context: "fresh",
  use_worktree: true,
  source_branch: "project/<slug>",
  branch_name: "<id>-<slug>",       // a single-issue project: the session checks out project/<slug> itself
  model: "<current Sonnet id>",     // e.g. claude-sonnet-5-5
  effort: "medium",
                                    // no permission_mode: it inherits the coordinator's; never "plan"
  prompt: <above>,
  background: <above>)
```

## Ship session

One per project, when every issue is merged into the project branch. Fresh context and a stronger model on purpose:
it is the project's only independent review.

### `prompt`

```
Run the `ship-project` skill for the Linear project "<project>" (<project url>), branch `project/<slug>`.

Issues merged into the project branch: <ID "title" (PR url), …>.
<Known issues filed during this project: <ID> <one line>, …>
<Anything the user asked to have checked. Omit if nothing.>

You didn't write this code. Verify it in full, review the whole diff against the spec and the architecture rules,
take the screenshots, and open the one PR to dev for the user to review and merge.
```

### `background` (at least 80 characters)

```
MealWiz: a .NET 9 Blazor WebAssembly + MAUI meal planner on Supabase (MediatR CQRS, FluentResults, MudBlazor), with
a .NET 10 Azure Function for meal import. Feature "<project>" was built by separate issue sessions that only ran the
fast checks and merged into project/<slug>. You are the ship session: the full verify, the fresh-context review, the
screenshots and the PR from project/<slug> to dev. The user merges that PR; you never do. Rules: CLAUDE.md and
docs/agents/. Windows: PowerShell 5.1, so write commit messages and PR bodies to a file.
```

### Call shape

Local mode:

```
start_session(
  initiation: "user_asked",
  title: "Ship: <project>",
  name: "ship-<slug>",
  context: "fresh",
  use_worktree: true,
  source_branch: "project/<slug>",
  branch_name: "ship-<slug>",       // a scratch name; the session checks out project/<slug> and works there
  model: "<current Opus id>",       // e.g. claude-opus-5-5
  effort: "medium",
  prompt: <above>,
  background: <above>)
```

Cloud mode: `create_session` with `model: "<current Opus id>"`, `effort: "medium"`, `source_url` this repository,
`source_revision: "project/<slug>"`, `title: "Ship: <project>"` and the prompt above with the background appended.
