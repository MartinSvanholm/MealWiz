---
name: fix-bugs
description: Fix MealWiz's open Linear bugs in parallel. Finds Bug-labelled issues in the backlog, groups those that touch the same files, shows the groups, then starts one session per group (max 4) that fixes its bugs test-first, opens a PR to dev and merges it on green CI without asking. Use when the user says "fix bugs", "/fix-bugs", "work through the bug backlog", or names several bugs to fix.
---

# Fix bugs

This session is the **coordinator**: it picks and groups bugs, starts one session per group, and reports. It fixes
nothing itself. Each group session runs `implement-issue` in bug mode.

Standing rules:
- PRs target `dev` only. There is no hotfix path to `main`.
- Only the bugs in the approved groups. New bugs found along the way are filed (Bug label, no project), not fixed.
- Never poll; you act when woken.

## 1. Find the bugs

1. `list_issues` with `team: "MealWiz"`, `label: "Bug"`, all pages. If the user named issues, use just those.
2. Keep issues that are in **Backlog** or **Todo**, with **no project**. Drop the rest (In Progress, In Review, Done,
   Canceled, Duplicate, or in a project) and say how many and why.
3. Drop bugs someone is already on: a branch or PR that names the issue id.
   - `git fetch origin --prune`, then `git branch -a` and `gh pr list --state open --json number,title,headRefName`.
   - Match the id case-insensitively, including in combined names like `mea-35-37-…` (that covers MEA-35 and MEA-37).
   - These don't count: a branch whose PR is merged or closed, and a branch with no commits ahead of `origin/dev`
     and no PR (`git rev-list --count origin/dev..<branch>` is 0; worktree names get reused). Ask the user about a
     branch that has commits but no PR.

## 2. Predict the files

One **Explore** subagent (`model: "sonnet"`), in the foreground, gets the list (id, title, description) and this brief:
"For each issue, list the source files a fix will most likely edit (not tests), using the issue text,
`docs/agents/FEATURES.md` and the code. Also flag, per issue, whether the fix likely needs: a migration or RLS change;
a changed handler request/response or state container public API; the Function contract or SSRF guard; a new package
or DI change; a user-visible behaviour change. Answer as a table: id | files | flags | confidence. Under 400 words."

Pinned bugs: the test files in "Bugs currently pinned by tests" (`docs/agents/TESTING.md`) count as files too, since
the fix flips those assertions.

## 3. Group

- Bugs that share a **source** file or a **pinning test** go in the same group (follow chains: A shares with B, B with
  C → one group). Other shared test files (`MealHandlerTests.cs`, a journey) don't link bugs: new test methods in the
  same file merge cleanly. Counting them chains most of the backlog into one cluster.
- Migrations are new files, so they don't link bugs, but pgTAP files do, and so does `00_rls_enabled.test.sql` when
  policies change.
- **At most 3 bugs per group.** A bigger cluster splits by feature folder, and the groups that share a file run one
  after the other (the second is blocked by the first).
- A bug with flags is still grouped, but marked: its session must stop and ask before that part (implement-issue,
  B2).
- Order groups: blockers first, then by priority, then by size (small first).

Show the user a table (group, bugs with titles, files, flags, starts now / waits for) and the excluded bugs with the
reason. **Wait for their OK** (AskUserQuestion: start all / adjust / cancel). Their yes is the request to start
sessions.

Flagged bugs and bugs whose issue leaves a product decision open would each stop their session later. Offer to
settle them now, one AskUserQuestion round, and put the answers in that group's brief ("cleared by the user: …"),
so the session doesn't have to stop.

## 4. Start the group sessions

**At most 4 run at once.** For each group that can start:

1. `mark_chapter` once ("Bug fixes: wave 1").
2. A bold title line and one sentence, then `start_session` with `initiation: "user_asked"`, `context: "fresh"`,
   `use_worktree: true`, `source_branch: "dev"`, no `permission_mode` (they inherit; bug sessions don't plan-gate),
   and the brief from [references/bug-brief.md](references/bug-brief.md).
3. Move the group's issues to **In Progress**.

If the app refuses a start (on a turn a session's message started), propose it instead with
`initiation: "own_initiative"` and the same brief, and tell the user in one line.

Tell the user once: each session will ask the app to approve `set_monitor`, and possibly `gh pr merge` the first time,
unless they've allowed it. `.claude/settings.json` allows `gh pr merge`, `gh pr view` and `gh pr checks`.

## 5. Every time you're woken

1. Read the session's report. Check it: `gh pr view <n> --json state,mergedAt,baseRefName,statusCheckRollup`. A report
   is a claim.
   - `MERGED`: confirm the PR merged into `dev`, the issues are Done (set them if the integration hasn't).
   - `BLOCKED`: ask the user, forward the answer with `SendMessage` quoting their words.
   - `STUCK`: tell the user which check fails and what was tried; ask whether to continue, hand it to them, or close.
2. Start waiting groups whose blockers merged, up to 4 running.
3. Report in two or three lines: merged, running, waiting on the user.

## 6. Finish

When every group is merged, stuck or cancelled: summarise (bug → PR → result), list the bugs filed along the way,
and offer cleanup as in `plan-project` step 6 (archive merged sessions on the user's yes, then remove their
worktrees, then `git branch -d` merged branches).
