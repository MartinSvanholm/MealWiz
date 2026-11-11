---
name: plan-project
description: Step 2 of a MealWiz feature. Research a specced Linear project, split it into tracer-bullet issues with blocking relations, write a full implementation plan into every issue, then coordinate - start one Sonnet implementation session per unblocked issue that executes its plan (max 4 locally), and start the next ones as PRs merge. In a cloud container this session implements the issues itself, one at a time. Use when the user says "/plan-project", "plan project X", "break X into issues", or "start implementing project X".
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
run in a Claude Code cloud container: steps 1 to 5 are the same, then [Cloud mode](#cloud-mode) replaces step 6. This
session implements the issues itself, one at a time; it starts no other sessions. Otherwise it's local mode: the
desktop app on the user's machine, worktrees and one shared Supabase.

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
(they share one local Supabase and its sign-in rate limit); the rest wait for a slot. In cloud mode follow
[Cloud mode](#cloud-mode) instead of this step.

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

In a cloud container this session doesn't start sessions: it works the issues itself, **one at a time**, with the
[implement-issue](../implement-issue/SKILL.md) skill's [Cloud mode](../implement-issue/SKILL.md#cloud-mode). The
plans are approved in step 5, so there's no plan mode.

1. Ask once, before the first issue, whether you may merge this session's own PRs into `dev` when CI is green and
   they're mergeable (the default is yes in cloud mode, since nobody else watches them; feature PRs are otherwise the
   user's to merge). Record the answer in the project status update.
2. Run implement-issue's [loop](../implement-issue/SKILL.md#the-loop) until every issue is Done: check every open PR
   first (conflicts, red CI, reviews; merge what's green if allowed), keep the open ones up to date with `dev`, then
   start the next issue whose blockers are merged. Order: the frontier in the status update's wave order.
3. Before starting an issue, **refresh its plan** against `dev` as under "Start sessions" (blockers merged since the
   plan was written may have renamed or reshaped what it relies on, or added a rule such as UI-03 the plan predates).
   A small mismatch you fix yourself and note in the PR's Decisions; a design change is stop rule 8: ask the user.
4. Move each issue to **In Progress** when you start it, **In Review** with its PR, **Done** when it merges; the
   project to **In Progress** with the first issue.
5. If this session had to run `scripts/cloud-setup.sh` by hand, tell the user once to make it the environment's setup
   script (environment settings, Setup script).

The user can redirect at any time ("stop at the PR", "merge only on my say", "skip MEA-52"); that wins over these
steps for the rest of the session. There are no sessions to archive and no worktrees to remove in step 7.

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
