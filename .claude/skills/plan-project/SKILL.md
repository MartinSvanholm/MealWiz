---
name: plan-project
description: Step 2 of a MealWiz feature. Research a specced Linear project, split it into tracer-bullet issues with blocking relations, write a full implementation plan into every issue, then coordinate - start one Sonnet implementation session per unblocked issue that executes its plan (max 4 locally, no limit in cloud mode), and start the next ones as PRs merge. Use when the user says "/plan-project", "plan project X", "break X into issues", or "start implementing project X".
---

# Plan and coordinate a feature project

Step 2 of 3 (spec → **plan** → implement). This session is the project's **main session**, the **coordinator**: it
plans, starts one session per issue, and keeps the task graph moving. It doesn't implement issues itself.

**All the design happens here.** You write a full implementation plan into every issue and the user approves the
plans here. Issue sessions then run on Sonnet at medium effort and only execute their plan: no plan mode, no
exploring for alternatives, no design questions. A plan that leaves a decision open costs a stopped session or a
guess, so plan deeply.

Three levels: the **project** (the feature, a collection of user stories), its **issues** (one user story each, one
session and one PR), and their **sub-issues** (`[DB]`, `[BE]`, `[FE]` layer tasks). You start sessions for issues only;
each issue's session works its sub-issues itself or hands them to subagents.

Standing rules:
- Work only on this project's issues. A bug you or a session find goes in a new Linear issue with the **Bug** label,
  team MealWiz, **no project**, status Backlog; it isn't fixed here.
- Feature PRs target `dev` and are merged by the user. Nothing goes to `main`.
- Never poll. You act when you're woken: by the user, or when a session you started ends a turn.

**Local or cloud mode.** If `CLAUDE_CODE_REMOTE` is `true` (or you have `create_session` and no `start_session`), you
run in a Claude Code cloud container: use **cloud mode** wherever a step has one ([Cloud mode](#cloud-mode)). Otherwise
it's local mode: the desktop app on the user's machine, worktrees and one shared Supabase.

## 1. Read

- `get_project` (the spec is its description), `list_documents project:`, `list_issues project:`. If the description
  isn't a spec yet, stop and suggest `/spec-project`.
- `docs/agents/FEATURES.md` for the affected features, `docs/agents/ARCHITECTURE.md`, `docs/agents/TESTING.md`.
- If the project already has issues and sessions (you're resuming), skip to step 6. If it has issues but some lack
  an `## Implementation plan`, do step 4 for those first.

## 2. Research and explore, in parallel

- **Research:** run `mw-research` in the background for every question outside the repo (library behaviour, APIs,
  formats). It saves a **Research** document on the project.
- **Codebase:** one or two **Explore** subagents for the code the feature touches: files, similar features to copy,
  shared files several slices will edit.

Wait for both before slicing; a Research finding can change the slices.

## 3. Slice

Run `mw-to-tickets` on the project, including its quiz. It publishes the issues and their layer sub-issues to Linear
with `blockedBy` edges.

If parallel issues share a seam, write the **Contract** document first (`save_document`, `project:`, title
"Contract", from [references/contract-template.md](references/contract-template.md)), get the user's OK, then link it
in a comment on each issue that uses it. The contract is binding: a session that needs it changed stops and asks you.

## 4. Plan every issue

Write a full implementation plan for each issue, from
[references/implementation-plan-template.md](references/implementation-plan-template.md). Meet its bar: the session
makes no design decision.

1. **Draft in parallel.** Start one **Plan** subagent per issue, all in one message, each with:
   - the issue and its sub-issues (ids, urls, text), the spec, and the Research and Contract documents;
   - the sibling issues in one line each, with the files they own, and what this issue's blockers will have added to
     `dev` by the time it starts (from their issue text and the Contract);
   - the template, and: "Read the code your steps touch (FEATURES.md first; the real files, not summaries) and the
     prior art to copy. Return the plan, plus a separate list of the questions only the user can decide, each with a
     recommended answer. Don't write to Linear or edit files."
2. **Review them together** yourself; a draft is a claim. Check each against the code where it names files, members
   or behaviour, and across issues: the same names for the same things, shared-file edits that don't collide,
   Contract items used exactly as written, blockers that really provide what a later plan relies on, and the
   template's bar (every name, shape, UI string and test case given).
3. **Settle the questions** with `mw-grilling`: collect the drafts' questions and anything your review raised, ask
   them in rounds, and fold the answers into the plans' Decisions. A decision that changes the spec goes into the
   project description too; one that changes a shared seam goes into the Contract.
4. **Publish.** Append each plan to its issue's description (`save_issue` with `patch`, `append`), and the
   one-line `Plan: steps …` pointer to each sub-issue. Tell the user the plans are on the issues and summarise each in
   one or two lines (the steps, the files, anything notable).

## 5. Record the plan

Post a project status update (`save_status_update`, `type: "project"`, health onTrack) with:
- the waves: which issues start now, and what each merge unblocks;
- links to the Research and Contract documents;
- the verify level per issue.

Then **end your turn** asking whether the plans are approved and the first wave can start (with `wrap_up`'s question
and buttons; offer "Approve and start" and "Change a plan"). Their yes approves the plans: sessions don't ask again.
Ask this way rather than
asking with AskUserQuestion. The app starts sessions only on the user's own turn, and a turn that a background report
(research, Explore) began isn't one, so a yes given inside it turns every start into a card. Their reply is the
request to start sessions. Tell them then that later waves start on a turn a session's report began, so they arrive as
cards, unless they say "start the next ones" when they're back.

## 6. Coordinate

### Start sessions

The **frontier** is every Todo top-level issue in the project (not a sub-issue) whose blockers are all Done.
**At most 4 sessions run at once** in local mode
(they share one local Supabase and its sign-in rate limit); the rest wait for a slot. Cloud mode has no limit: each
cloud session has its own container and Supabase. Start the whole frontier, and follow [Cloud mode](#cloud-mode)
instead of the steps below.

For each issue you start:
1. **Refresh its plan** if any of its blockers merged after the plan was written: read their merged diffs
   (`gh pr diff <n>`) against what the plan relies on (names, shapes, files). If something differs, revise the plan
   in the issue (`save_issue` with `patch`) and add a `Revised <date> after <ID> merged: …` line. A session must never
   start on a plan that no longer matches `dev`.
2. `mark_chapter` once per wave ("Wave 2: MEA-52, MEA-53").
3. A bold title line and one sentence, then `start_session` with:
   - `initiation: "user_asked"`, `context: "fresh"`, `use_worktree: true`, `source_branch: "dev"`;
   - `model`: the current Sonnet id (e.g. `claude-sonnet-5-5`), `effort: "medium"`. The plan carries the design, so
     the session only needs to execute it;
   - no `permission_mode`: the session inherits yours. Never `plan`; the plan was approved here;
   - `title: "<ID>: <short title>"`, `name: "<id>-<slug>"` (e.g. `mea-52-fav-toggle`), `branch_name` the same;
   - `prompt` and `background` from [references/session-brief.md](references/session-brief.md).
4. Move the issue to **In Progress** (`save_issue state: "In Progress"`), and the project to **In Progress** on the
   first start.

If the app refuses the start (this happens on a turn that a session's message started, rather than the user), call
`start_session` again with `initiation: "own_initiative"` and the same brief: that shows the user a card to start it
with one click. Tell them in one line.

### Every time you're woken

Recompute the state; never assume it from memory:
1. `list_issues project:` with status and relations; `gh pr list --base dev --state all --search "<ID>"` for each
   In Progress or In Review issue.
2. For each session report: check it against the branch, the PR and CI yourself. A report is a claim.
   - **`MERGED <url>`**, or a PR you find merged: confirm with `gh pr view <n> --json state,mergedAt,baseRefName`,
     set the issue and its sub-issues to **Done** if the GitHub integration hasn't.
   - **Blocked / question**: the session has already asked the user in its own window, so don't ask again here (two
     places to answer means two answers that can disagree). Tell the user in one line which session waits for them.
     Only a contract change is yours: update the Contract document, add a line to its change log, and message every
     session that uses that item.
   - **The plan doesn't fit** (the session stopped on stop rule 8): the plan is yours. Check the session's finding
     against the code, fix the plan in the issue (a `Revised` line), and `SendMessage` the session what changed. If
     the fix needs a user decision, ask the user here, since the plan's decisions are made here; tell the session it
     waits on you.
   - **Bugs it filed:** add each (id and one line) to the "Known issues" line of every brief you start from now on.
   - **Stopped mid-task** (usage limit, error): it committed WIP; tell the user and offer to resume it
     (`SendMessage` "continue") or start a fresh session on the same branch.
3. Start the new frontier, up to 4 running.
4. Tell the user in two or three lines: what merged, what's running, what's waiting on them (reviews, questions).

The user may also say "next", "status" or "MEA-52 is merged"; treat that the same way.

Approvals don't relay: a session's own permission prompts (pushing, `set_monitor`) are answered by the user in that
session. Never ask a session to do something your own permissions would block.

### Cloud mode

How each step above changes when you run in a cloud container.

**Start a session** with `create_session` (claude-code-remote), one per frontier issue, using the cloud brief in
[references/session-brief.md](references/session-brief.md):

```
create_session(
  title: "<ID>: <short title>",
  source_url: "https://github.com/MartinSvanholm/MealWiz",
  source_revision: "dev",
  outcome_branch: "<id>-<slug>",          // e.g. mea-52-fav-toggle: the session's own branch
  tags: ["mealwiz", "<project slug>"],
  prompt: <cloud prompt>,
  append_system_prompt: <background>)
```

No plan mode: the plan in the issue is already approved. Refresh it first ("Refresh its plan" under Start sessions) as in local mode. If
`create_session` takes a model or effort option, pass Sonnet and medium as in local mode; otherwise the session runs
on the environment's default.

Leave `environment_id` out: the session inherits this one's environment, whose setup script
(`bash scripts/cloud-setup.sh`) installs the .NET 10 SDK and the Supabase CLI and starts Docker. If this session had
to run that script by hand, tell the user once to make it the environment's setup script (environment settings, Setup
script).

**Check the record right away.** `get_session <new id>`: `session_context.sources[0].git_repository.revision` must be
`dev` (or `refs/heads/dev`) and `session_context.outcomes[0].git_repository.git_info.branches` must be exactly
`["<id>-<slug>"]`. If the branch shows `dev` or is missing, `archive_session` it and create it again once; if it's
still wrong, stop and tell the user. Never let a session work on `dev`.

**Being woken.** A cloud session that ends its turn cleanly doesn't wake you; only a failed turn does (a
`<child-session-event>`). So the brief has each session `send_message` you (this session's id, from `get_session`
with no id) when it's blocked, opens its PR, merges or is stuck. Act on those messages as on any report. When the user
asks for status, `get_session` each running session (`status_bucket`) and `list_events` with `kinds: ["assistant",
"result"]` for its last words.

**GitHub.** There's no `gh` CLI: check PRs with the GitHub MCP tools (`list_pull_requests` with `base: "dev"`,
`pull_request_read`). If a session reports a push refused with 403 or "GitHub access", the user must reconnect GitHub
(`read_documentation` topic `github.access` gives the link); tell them in one line, then `send_message` the session
"continue" once they have.

**Finish.** There are no worktrees to remove. `archive_session` each merged session on the user's yes; merged
branches are deleted when their PR merges.

## 7. Finish

When every issue is Done:
1. Project status **Completed** and a final status update (what shipped, PRs, follow-up bugs filed).
2. Offer cleanup, and do it on the user's yes:
   - `list_sessions` (linked) → `archive_session` for each merged session. On Windows a worktree can't be deleted while
     its session is open, so archive first.
   - Then remove the worktrees (`clean_up_worktrees`, or `git worktree remove` + `git worktree prune`), checking each
     has no uncommitted work first (`git -C <path> status --short`); never discard work silently.
   - Delete merged local branches with `git branch -d` (never `-D` without asking).
3. Remind the user that releasing (`dev` → `main`) is their call and deploys to production.
