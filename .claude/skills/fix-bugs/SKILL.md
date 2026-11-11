---
name: fix-bugs
description: Fix MealWiz's open Linear bugs in parallel. Finds Bug-labelled issues in the backlog, diagnoses and writes a fix plan for every bug that needs one, groups bugs that touch the same files, shows the groups, then starts one Sonnet session per group (max 4) that executes the plans test-first with the fast checks, opens a PR to dev and merges it on green CI without asking. Use when the user says "fix bugs", "/fix-bugs", "work through the bug backlog", or names several bugs to fix.
---

# Fix bugs

This session is the **coordinator**: it picks bugs, diagnoses and plans the ones that need it, groups them, starts one
session per group, and reports. It fixes nothing itself. Each group session runs `implement-issue` in bug mode.

**The diagnosis happens here.** Every bug that isn't obvious gets a `## Fix plan` in its issue, approved by the user
here. Group sessions run on Sonnet at medium effort and execute the plans: they don't diagnose again or ask about
what a plan settles.

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

One **Explore** subagent (`model: "sonnet"`) gets the list (id, title, description) and this brief:
"For each issue, list the source files a fix will most likely edit (not tests), using the issue text,
`docs/agents/FEATURES.md` and the code. Also flag, per issue, whether the fix likely needs: a migration or RLS change;
a changed handler request/response or state container public API; the Function contract or SSRF guard; a new package
or DI change; a user-visible behaviour change; a change to what existing data does (rows written before the fix that
would behave differently afterwards, e.g. a flag that was never set). For each flag, name the concrete change in a
few words (the policy's new check, the new signature). Note any product decision the issue leaves open. Say whether
the bug needs a fix plan: **no** only when the issue or the code makes the cause and the one small fix obvious (one
file, no flags, high confidence); otherwise **yes**, with the reason. Answer as a table: id | files | flags | open
decision | confidence | needs plan. Under 600 words."

If the agent runs in the background, its report arrives as a later message. That's fine: step 4 makes sure the start
itself happens on the user's own turn.

Pinned bugs: the test files in "Bugs currently pinned by tests" (`docs/agents/TESTING.md`) count as files too, since
the fix flips those assertions.

## 3. Diagnose and plan

For every bug marked **needs plan** (and any you doubt), write a fix plan from
[references/fix-plan-template.md](references/fix-plan-template.md). Meet its bar: the session makes no diagnostic
or design decision.

1. **Diagnose in parallel.** Start one **Plan** subagent per bug, all in one message (bugs the triage expects to share
   files may share one agent), each with the issue (id, url, text, comments), the triage row, the template, and:
   "Find the root cause by reading the code path end to end (FEATURES.md first; the real files, not summaries), the
   way `mw-diagnosing-bugs` ranks hypotheses. Write the fix plan: root cause with file and line, the red test that
   fails today and why, the exact fix, other call sites with the same flaw. Return the plan, the files the fix edits,
   and a separate list of questions only the user can decide, each with a recommended answer. Don't write to Linear,
   edit files, or run anything that changes state."
2. **Review the plans** yourself; a plan is a claim. Read the code at each root cause and check that the red test
   would really fail for the stated reason and pass after the fix. A plan whose cause is only "likely" says so, and
   says what the session does if the red test passes today (stop rule 8).
3. Use the plans' file lists, not the triage's, for grouping below.

Bugs marked **no** get no plan; their session diagnoses them with `mw-diagnosing-bugs` as before.

## 4. Group

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

Show the user a table (group, bugs with titles, root cause in a few words or "no plan", files, flags, starts now /
waits for) and the excluded bugs with the reason.

Flagged bugs, bugs whose issue leaves a product decision open, and the plans' questions would each stop a session
later. Settle them now, in one AskUserQuestion round, and put the answers in the bug's Fix plan ("Cleared by the
user") or, for an unplanned bug, in that group's brief ("cleared by the user: …"), so the session doesn't have to
stop. Make each option say exactly what the user approves: the policy's new `WITH CHECK`, the new method signature,
the migration and what it does to existing rows. "RLS change OK?" isn't enough to decide on.

Then **publish** each plan: append it to its bug's description (`save_issue` with `patch`, `append`).

Then **end your turn** asking for the start (with `wrap_up`'s question and buttons: start / adjust / cancel), rather
than asking it with AskUserQuestion. The yes also approves the fix plans. The app starts sessions only on the user's
own turn, and a turn that a background report began isn't one, so a yes given inside such a turn turns every start
into a card. Their reply is the request to start sessions.

## 5. Start the group sessions

**Cloud mode** (`CLAUDE_CODE_REMOTE` is `true`): start no sessions. Work the groups yourself, one at a time and in
the order above, each in bug mode with the [implement-issue](../implement-issue/SKILL.md) skill's
[Cloud mode](../implement-issue/SKILL.md#cloud-mode): before each group, check every open PR from this session and
merge the green ones into `dev`; then branch the group from the latest `origin/dev` and open its own PR. A group that
shares a file with an unmerged group waits for that merge. Skip the rest of this step and step 6.

**At most 4 run at once** locally. For each group that can start:

1. `mark_chapter` once ("Bug fixes: wave 1").
2. A bold title line and one sentence, then `start_session` with `initiation: "user_asked"`, `context: "fresh"`,
   `use_worktree: true`, `source_branch: "dev"`, `model`: the current Sonnet id (e.g. `claude-sonnet-5-5`),
   `effort: "medium"`, no `permission_mode` (they inherit; bug sessions don't plan-gate), and the brief from
   [references/bug-brief.md](references/bug-brief.md).
3. Move the group's issues to **In Progress**.

If the app refuses a start (on a turn a session's message started), propose it instead with
`initiation: "own_initiative"` and the same brief, and tell the user in one line.

Tell the user once, with the first wave: groups that wait for a merge start later, on a turn a session's report
began, so they arrive as cards to click. Or the user can say "start the next ones" whenever they're back, and you start
every group that is unblocked by then directly.

`.claude/settings.json` pre-approves `scripts/merge-pr.sh` (which only merges green bug PRs into `dev` and green issue
PRs into a project branch), `gh pr view` and
`gh pr checks`. It deliberately doesn't pre-approve `gh pr merge`, which could merge a release PR into `main`.

## 6. Every time you're woken

1. Read the session's report. Check it: `gh pr view <n> --json state,mergedAt,baseRefName,statusCheckRollup`. A report
   is a claim.
   - `MERGED`: confirm the PR merged into `dev`, the issues are Done (set them if the integration hasn't).
   - `BLOCKED`: the session has already asked the user in its own window, so don't ask again here: two places to
     answer means two answers that can disagree. Tell the user in one line which session waits for them, with a link.
     The session sends `UNBLOCKED` once they've answered there. Only a question about the run itself (grouping,
     another group's files) is yours to ask.
   - `PLAN`: a fix plan doesn't fit (the red test passes today, fails for another reason, or the fix doesn't fit the
     code). The plan is yours: check the finding against the code, revise the plan in the issue (a
     `Revised <date>: …` line), and `SendMessage` the session what changed. If it needs a user decision, ask the user
     here and tell the session it waits on you.
   - `STUCK`: tell the user which check fails and what was tried; ask whether to continue, hand it to them, or close.
   - **Bugs it filed:** note each one (id and one line). Add them to the "Known issues" line of every brief you start
     from now on, so later sessions comment on them instead of filing them again.
2. Start waiting groups whose blockers merged, up to 4 running.
3. Report in two or three lines: merged, running, waiting on the user.

## 7. Finish

When every group is merged, stuck or cancelled: summarise (bug → PR → result), list the bugs filed along the way,
and offer cleanup as in `plan-project` step 6 (archive merged sessions on the user's yes, then remove their
worktrees, then `git branch -d` merged branches).
