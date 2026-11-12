---
name: plan-project
description: Step 2 of a MealWiz feature. Split a specced Linear project into one to three large issues, plan only the issue that starts next, then coordinate - one Sonnet session per issue, each merging its PR into the project branch on green CI, the next issue planned just in time when its blockers merge, and an Opus ship session at the end that verifies and reviews the whole project and opens the one PR to dev. In a cloud container this session implements the issues itself, one at a time. Use when the user says "/plan-project", "plan project X", "break X into issues", or "start implementing project X".
---

# Plan and coordinate a feature project

Step 2 of 4 (spec → **plan** → implement → ship). This session is the project's **main session**, the
**coordinator**: it slices the project, plans each issue just before it starts, starts one session per issue, and
starts the ship session at the end. It doesn't implement issues itself (except in cloud mode).

**Plan late and light.** Only the issue that starts next gets a plan, written against the project branch as it is
then, so no plan goes stale. A plan settles the product and architecture decisions and the public shapes; the
session works out the rest (see the template's bar). The user is asked once, in one round, before the first issue
starts.

**How the work lands.** The project has one branch, `project/<slug>`. Each issue is one session, one branch and one
PR **into the project branch**, which its session merges itself when CI is green. Nothing reaches `dev` until
`/ship-project` has run `verify full`, reviewed the whole diff in a fresh Opus session and opened the one PR from the
project branch to `dev`. The user reviews and merges that PR.

Standing rules:
- Work only on this project's issues. A bug you or a session find goes in a new Linear issue with the **Bug** label,
  team MealWiz, **no project**, status Backlog; it isn't fixed here.
- Only the user merges into `dev` (the ship PR). Nothing goes to `main`.
- Never poll. You act when you're woken: by the user, or when a session you started ends a turn.

**Local or cloud mode.** If `CLAUDE_CODE_REMOTE` is `true` (or you have `create_session` and no `start_session`), you
run in a Claude Code cloud container: steps 1 to 4 are the same, then [Cloud mode](#cloud-mode) replaces step 5.
Otherwise it's local mode: the desktop app on the user's machine, worktrees and one shared Supabase.

## 1. Read

- `get_project` (the spec is its description), `list_documents project:`, `list_issues project:`. If the description
  isn't a spec yet, stop and suggest `/spec-project`.
- `docs/agents/FEATURES.md` for the affected features, `docs/agents/ARCHITECTURE.md`, `docs/agents/TESTING.md`.
- If the project already has issues (you're resuming), skip to step 5.

## 2. Explore, and research only if needed

- **Codebase:** one **Explore** subagent for the code the feature touches: files, similar features to copy. Skip it
  if `/spec-project` ran in this session and you already know the area.
- **Research:** run `mw-research` in the background only when the spec leaves a question about something outside the
  repo (a library's behaviour, an API, a format). It saves a **Research** document on the project. No such question,
  no research.

## 3. Slice and plan the first issue

1. **Draft the issues** with `mw-to-tickets` (steps 1 to 4; it returns the draft without asking). Expect one to
   three issues.
2. **Draft the plan for the frontier only**: the issue or issues that can start now, from
   [references/implementation-plan-template.md](references/implementation-plan-template.md). One issue: write it
   yourself. More than one: a **Plan** subagent per issue, in one message, each with the draft issue, the spec, the
   Research document, the siblings in one line each, the template, and "Read the code your stories touch
   (FEATURES.md first). Return the plan and a separate list of the questions only the user can decide, each with a
   recommended answer. Don't write to Linear or edit files." A draft is a claim: check what it says about the code.
3. **One round of questions** with `mw-grilling`: the slicing (are the issues the right size, are the edges right)
   together with the plan's open questions. A decision that changes the spec goes into the project description too.
4. **Publish.** Create the issues (`mw-to-tickets` step 5) and append the plan to each frontier issue
   (`save_issue` with `patch`, `append`). Later issues get no plan yet.
5. **Create the project branch** from the latest `dev` if it doesn't exist:
   `git fetch origin dev && git push origin origin/dev:refs/heads/project/<slug>` (the slug is the project's name in
   kebab-case). Write the branch name into the project description's first line: `Branch: project/<slug>`.

## 4. Record and ask for the start

Post a project status update (`save_status_update`, `type: "project"`, health onTrack) with the issues in order,
what each merge unblocks, and the project branch.

Then **end your turn** asking whether the approach is approved and the first issue can start (with `wrap_up`'s
question and buttons; offer "Approve and start" and "Change the plan"), rather than asking with AskUserQuestion. The
app starts sessions only on the user's own turn, and a turn that a background report began isn't one, so a yes given
inside it turns every start into a card. Their yes approves the slicing and the first plan, and covers the later
plans as long as they stay inside the spec. Tell them that later issues and the ship session start on a turn a
session's report began, so they arrive as cards, unless they say "start the next ones" when they're back.

## 5. Coordinate

### Start an issue

The **frontier** is every Todo issue in the project whose blockers are all Done (merged into the project branch).
**At most 4 sessions run at once** in local mode; with one to three issues that is rarely a limit.

For each issue you start:
1. **Plan it now** if it has no `## Implementation plan`: `git fetch origin`, read the project branch as it is
   (the blockers' merged code is the seam, so nothing needs agreeing up front), write the plan from the template and
   append it to the issue. A question only the user can decide: ask it, in one AskUserQuestion round, before
   starting. No such question: start without asking; the approach was approved in step 4.
2. `mark_chapter` ("MEA-53: grocery list sharing").
3. A bold title line and one sentence, then `start_session` with the issue brief and call shape from
   [references/session-brief.md](references/session-brief.md): Sonnet, medium effort, `source_branch` the project
   branch.
4. Move the issue to **In Progress** (`save_issue state: "In Progress"`), and the project to **In Progress** on the
   first start.

A project with **one issue** has no issue PR: the brief tells the session to work on the project branch itself, and
the ship PR is the only PR.

If the app refuses the start (this happens on a turn that a session's message started, rather than the user), call
`start_session` again with `initiation: "own_initiative"` and the same brief: that shows the user a card to start it
with one click. Tell them in one line.

### Every time you're woken

Recompute the state; never assume it from memory:
1. `list_issues project:` with status and relations; `gh pr list --base project/<slug> --state all`.
2. For each session report: check it against the branch, the PR and CI yourself. A report is a claim.
   - **`MERGED <url>`**, or a PR you find merged into the project branch: confirm with
     `gh pr view <n> --json state,mergedAt,baseRefName`, and set the issue to **Done** if it isn't.
   - **Blocked / question**: the session has already asked the user in its own window, so don't ask again here (two
     places to answer means two answers that can disagree). Tell the user in one line which session waits for them.
   - **The plan doesn't fit** (stop rule 8): the plan is yours. Check the session's finding against the code, fix the
     plan in the issue (a `Revised <date>: …` line), and `SendMessage` the session what changed. If the fix needs a
     user decision, ask the user here and tell the session it waits on you.
   - **`STUCK <url>`**: CI is red after three fix attempts. Tell the user which check fails and what was tried.
   - **Bugs it filed:** add each (id and one line) to the "Known issues" line of every brief you start from now on.
   - **Stopped mid-task** (usage limit, error): it committed WIP; tell the user and offer to resume it
     (`SendMessage` "continue") or start a fresh session on the same branch.
3. Start the new frontier. When **every issue is Done**, start the ship session instead (below).
4. Tell the user in two or three lines: what merged, what's running, what's waiting on them.

The user may also say "next", "status" or "MEA-52 is merged"; treat that the same way.

Approvals don't relay: a session's own permission prompts are answered by the user in that session. Never ask a
session to do something your own permissions would block.

### Start the ship session

When every issue is merged into the project branch, start **one** ship session with the ship brief and call shape
from [references/session-brief.md](references/session-brief.md): fresh context, the current **Opus** model, **medium**
effort. It runs the [ship-project](../ship-project/SKILL.md) skill: `verify full`, the review, the screenshots and
the PR to `dev`. Tell the user it has started and that its PR is the one they review.

Its reports: `PR <url>` (the ship PR is open: tell the user it waits for their review), `BLOCKED` (it asked the user
in its own window), `MERGED <url>` (go to step 6).

### Cloud mode

In a cloud container this session works the issues itself, **one at a time**, with the
[implement-issue](../implement-issue/SKILL.md) skill's [Cloud mode](../implement-issue/SKILL.md#cloud-mode).

1. Run implement-issue's [loop](../implement-issue/SKILL.md#the-loop) until every issue is Done: check every open PR
   first (conflicts, red CI), merge the green issue PRs into the project branch (they need no approval: only the ship
   PR goes to `dev`), then start the next issue whose blockers are merged.
2. Before starting an issue, **plan it** as under "Start an issue", against the project branch as it is now.
3. Move each issue to **In Progress** when you start it, **In Review** with its PR, **Done** when it merges; the
   project to **In Progress** with the first issue.
4. When every issue is Done, start the ship session with `create_session` (call shape in the session brief). The
   review must not run in this session: you wrote the code. If the start is refused, tell the user to open a new
   session on Opus at medium effort and run `/ship-project <project>`.
5. If this session had to run `scripts/cloud-setup.sh` by hand, tell the user once to make it the environment's setup
   script (environment settings, Setup script).

The user can redirect at any time ("stop at the PR", "skip MEA-52"); that wins over these steps for the rest of the
session.

## 6. Finish

The ship session sets the project to **Completed** when its PR merges. When you learn that it merged:
1. Offer cleanup, and do it on the user's yes (local mode):
   - `list_sessions` (linked) → `archive_session` for each finished session. On Windows a worktree can't be deleted
     while its session is open, so archive first.
   - Then remove the worktrees (`clean_up_worktrees`, or `git worktree remove` + `git worktree prune`), checking each
     has no uncommitted work first (`git -C <path> status --short`); never discard work silently.
   - Delete merged local branches with `git branch -d` (never `-D` without asking), and the remote project branch
     once the ship PR is merged.
2. Remind the user that releasing (`dev` → `main`) is their call and deploys to production.
