---
name: plan-project
description: Step 2 of a MealWiz feature. Research and plan a specced Linear project, split it into tracer-bullet issues with blocking relations, then coordinate - start one implementation session per unblocked issue (max 4), and start the next ones as PRs merge. Use when the user says "/plan-project", "plan project X", "break X into issues", or "start implementing project X".
---

# Plan and coordinate a feature project

Step 2 of 3 (spec → **plan** → implement). This session becomes the **coordinator**: it plans, starts one session per
issue, and keeps the task graph moving. It doesn't implement issues itself.

Standing rules:
- Work only on this project's issues. A bug you or a session find goes in a new Linear issue with the **Bug** label,
  team MealWiz, **no project**, status Backlog; it isn't fixed here.
- Feature PRs target `dev` and are merged by the user. Nothing goes to `main`.
- Never poll. You act when you're woken: by the user, or when a session you started ends a turn.

## 1. Read

- `get_project` (the spec is its description), `list_documents project:`, `list_issues project:`. If the description
  isn't a spec yet, stop and suggest `/spec-project`.
- `docs/agents/FEATURES.md` for the affected features, `docs/agents/ARCHITECTURE.md`, `docs/agents/TESTING.md`.
- If the project already has issues and sessions (you're resuming), skip to step 5.

## 2. Research and explore, in parallel

- **Research:** run `mw-research` in the background for every question outside the repo (library behaviour, APIs,
  formats). It saves a **Research** document on the project.
- **Codebase:** one or two **Explore** subagents for the code the feature touches: files, similar features to copy,
  shared files several slices will edit.

Wait for both before slicing; a Research finding can change the slices.

## 3. Slice

Run `mw-to-tickets` on the project, including its quiz. It publishes the issues to Linear with `blockedBy` edges.

If parallel issues share a seam, write the **Contract** document first (`save_document`, `project:`, title
"Contract", from [references/contract-template.md](references/contract-template.md)), get the user's OK, then link it
in a comment on each issue that uses it. The contract is binding: a session that needs it changed stops and asks you.

## 4. Record the plan

Post a project status update (`save_status_update`, `type: "project"`, health onTrack) with:
- the waves: which issues start now, and what each merge unblocks;
- links to the Research and Contract documents;
- the verify level per issue.

Then ask the user (AskUserQuestion) whether to start the first wave now. Their yes is the request to start sessions.

## 5. Coordinate

### Start sessions

The **frontier** is every Todo issue in the project whose blockers are all Done. **At most 4 sessions run at once**
(they share one local Supabase and its sign-in rate limit); the rest wait for a slot.

For each issue you start:
1. `mark_chapter` once per wave ("Wave 2: MEA-52, MEA-53").
2. A bold title line and one sentence, then `start_session` with:
   - `initiation: "user_asked"`, `context: "fresh"`, `use_worktree: true`, `source_branch: "dev"`,
     `permission_mode: "plan"` (the user approves each session's plan before code is written);
   - `title: "<ID>: <short title>"`, `name: "<id>-<slug>"` (e.g. `mea-52-fav-toggle`), `branch_name` the same;
   - `prompt` and `background` from [references/session-brief.md](references/session-brief.md).
3. Move the issue to **In Progress** (`save_issue state: "In Progress"`), and the project to **In Progress** on the
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
     set the issue to **Done** if the GitHub integration hasn't.
   - **Blocked / question**: if it's a decision, ask the user (AskUserQuestion) and send the answer with
     `SendMessage`, quoting the user's words. A contract change: update the Contract document, add a line to its
     change log, and message every session that uses that item.
   - **Stopped mid-task** (usage limit, error): it committed WIP; tell the user and offer to resume it
     (`SendMessage` "continue") or start a fresh session on the same branch.
3. Start the new frontier, up to 4 running.
4. Tell the user in two or three lines: what merged, what's running, what's waiting on them (plan approvals, reviews).

The user may also say "next", "status" or "MEA-52 is merged"; treat that the same way.

Approvals don't relay: a session's own permission prompts (pushing, `set_monitor`) are answered by the user in that
session. Never ask a session to do something your own permissions would block.

## 6. Finish

When every issue is Done:
1. Project status **Completed** and a final status update (what shipped, PRs, follow-up bugs filed).
2. Offer cleanup, and do it on the user's yes:
   - `list_sessions` (linked) → `archive_session` for each merged session. On Windows a worktree can't be deleted while
     its session is open, so archive first.
   - Then remove the worktrees (`clean_up_worktrees`, or `git worktree remove` + `git worktree prune`), checking each
     has no uncommitted work first (`git -C <path> status --short`); never discard work silently.
   - Delete merged local branches with `git branch -d` (never `-D` without asking).
3. Remind the user that releasing (`dev` → `main`) is their call and deploys to production.
