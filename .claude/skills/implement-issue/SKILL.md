---
name: implement-issue
description: Implement one MealWiz Linear issue (a group of user stories) by executing the Implementation plan in the issue, story by story and test-first, with only the fast checks, a PR into the project branch that it merges itself on green CI (feature mode), or fix a group of Bug issues with a PR to dev that merges itself on green (bug mode). The full verify, the review and the PR to dev belong to /ship-project. In a cloud container one session works the issues one at a time, a branch and PR each. Use when a session is told to implement or fix MEA-xx, from /plan-project and /fix-bugs briefs, or when the user says "/implement-issue MEA-xx".
---

# Implement an issue

You run in your own worktree, usually started by a coordinator session (`/plan-project` or `/fix-bugs`). Two modes:

- **Feature mode** (default): one issue from a project, a group of user stories. The coordinator wrote its
  `## Implementation plan` into the issue; you execute it. Your PR goes **into the project branch**
  (`project/<slug>`) and you merge it yourself when CI is green. You don't run the long verify levels and you don't
  review: a separate ship session (`/ship-project`) verifies and reviews the whole project with fresh eyes and opens
  the one PR to `dev`, which the user merges.
- **Bug mode** (`--bug`, or the brief says so): one or more Bug issues in one PR to `dev`. You merge it yourself when
  CI is green.

Either mode runs **locally** (the desktop app, a worktree, one Supabase shared with other sessions) or in **cloud
mode**: a Claude Code cloud container, when `CLAUDE_CODE_REMOTE` is `true`. In cloud mode there is no coordinator and
no worktree: one session works the project's issues (or bug groups) **one at a time**, each on its own branch with its
own PR. Cloud mode changes the steps listed under [Cloud mode](#cloud-mode); read it before step 1.

## Standing rules

- **Only the issues you were given.** Anything else you notice: a bug goes in Linear, then keep going. First check it
  isn't there already: the brief's "Known issues", then `list_issues` with `team: "MealWiz"`, `label: "Bug"` and a
  `query` from the error text or the file name. If one matches, add a comment to it with what you saw. Otherwise create
  one (`save_issue`, `team: "MealWiz"`, `labels: ["Bug"]`, no project, `state: "Backlog"`, with what you saw and where).
  An improvement goes in your final report as a suggestion.
- **Feature PRs target the project branch, bug PRs target `dev`.** Never push to, open a PR against, or merge into
  `main`. Never merge a project branch into `dev`: that is the ship PR, and it is the user's.
- **Follow `docs/agents/ARCHITECTURE.md`** (every MUST rule) and `docs/agents/TESTING.md`. Update `docs/agents/` and
  CLAUDE.md in the same PR when you add or move a feature, handler, page or table (DOC-01).
- **Be resumable.** Commit each green slice on your branch, so a usage-limit stop loses nothing. If you were resumed,
  read `git log origin/<base>..HEAD`, `git status` and the issue's comments before doing anything.
- **Never poll** except the one CI watch (step 6 in either mode).

### Stop and ask

Be critical. **Stop and ask** when one of the cases below applies. Ask the user in one place only, AskUserQuestion in
this session: you have the context, and two places to answer means two answers that can disagree. Then send the
session that started you a one-line notice, `BLOCKED MEA-52: waiting for the user (<topic>)`, and add a comment on the
Linear issue. When the user has answered, send `UNBLOCKED MEA-52: <the decision>`. If the coordinator sends you an
answer too, the user's answer here wins; tell the coordinator if they differ. The cases:

1. the issue or spec is unclear, or contradicts the code;
2. a shape another issue already relies on (a table, a handler's request or result, a state container's public
   members) would have to change;
3. you'd break a MUST rule, or change the architecture: a new pattern, project, package, or a cross-feature handler
   call (ARCH-02);
4. the work grows beyond the issue's stories;
5. it needs a migration or RLS change the plan doesn't mention;
6. verify is red and you can't explain why after 2 attempts;
7. it touches secrets, deployment, workflows or `main`;
8. the plan in the issue (an Implementation plan, or a bug's Fix plan) doesn't fit the code: a file, member or
   behaviour it relies on is missing or different, a red test doesn't fail as it says, or a story needs a product or
   architecture decision the plan doesn't settle. Don't redesign or re-diagnose around it. Send the coordinator
   `PLAN MEA-52: <what doesn't fit, with file and line>`, comment on the issue, and wait: the coordinator owns the
   plan, fixes it and tells you what changed. If there's no coordinator (you were started by hand), ask the user here.

**Decide yourself**, and note it in the PR: everything the plan leaves to the implementation (private names, test
names and data, method bodies, the wording of UI text the spec doesn't fix, in English and Danish), refactors inside
the files you touch, SHOULD-rule trade-offs. The plan's Decisions and Shapes stay as written.

While you wait for an answer, don't build on a guess.

## 1. Set up

1. `get_issue <ID>` with relations, for each issue. In feature mode also read the project description (the spec; its
   first line names the project branch) and its "Research" document if there is one.
2. `git fetch origin`. Your **base** is the project branch in feature mode and `dev` in bug mode. If your branch has
   no commits of its own, bring it to the base's tip: `git merge --ff-only origin/<base>`. (Resuming? See the standing
   rules.)
3. Move each issue to **In Progress**.
4. The local Supabase stack is shared with other sessions. Apply migrations and run pgTAP through verify, never with
   `supabase db reset` or `supabase test db` by hand (the verify skill explains the lock).

## F. Feature mode

1. **Read the plan.** Read the issue's `## Implementation plan` end to end, including any `Revised` lines, then the
   prior art it points to. Don't look for alternatives or re-plan. Check that what it relies on exists as described;
   if not, stop rule 8.
   - **No plan in the issue** (the fast path: you were started by hand, usually on a one-issue project): plan it
     yourself first. Explore (FEATURES.md first), ask the open questions with AskUserQuestion in one round as
     `mw-grilling` does, write the plan from
     `.claude/skills/plan-project/references/implementation-plan-template.md` into the issue, and present it with
     ExitPlanMode. Code nothing before the user approves. If the project has no branch yet, create it from `dev`
     (plan-project, step 3, item 5).
2. **Build test-first** with `mw-tdd`, following the plan's Stories in order: inside a story, database → handlers →
   UI. Commit each story when it's green, as `<ID> story <n>: <what works now>` (more than one commit per story is
   fine), so the PR reads story by story. While iterating, run only the tests you're touching
   (`dotnet test --project … --filter-class …`).
3. **Docs.** Make the plan's Docs edits (and any other line in `docs/agents/` or CLAUDE.md that describes what you
   changed).
4. **Verify fast.** `scripts/verify.ps1 fast`: about a minute, no Docker, no lock, so it never blocks another
   session. If the plan's Verify section names more (a migration, an RLS change, new integration tests), also run
   exactly those steps: `scripts/verify.ps1 local --only <steps>`. Don't run `local`, `smoke` or `full` in whole. CI
   runs `verify full` on your PR, and the ship session runs it on the whole project. Triage with the `verify` skill.
   Green means exit code 0.
5. **Check yourself against the plan**, inline, in a few lines: every story built and committed, every test the plan
   asks for present, nothing built that it doesn't ask for, docs updated. This isn't a code review; don't start a
   review subagent. The ship session reviews.
6. **Open the PR** into the project branch (go to [Opening the PR](#opening-the-pr)) and wait for CI with one
   background command: `gh pr checks <n> --watch --fail-fast` (Bash, `run_in_background`). You're notified when it
   exits. Don't check in between.
7. **Green: merge it yourself** with `scripts/merge-pr.sh <n>` (Bash). It refuses anything but an open, non-draft,
   mergeable issue PR into a project branch (or a bug PR into `dev`) with every check green. Never call `gh pr merge`
   directly. If the script refuses, don't work around it: read why, then fix it or stop and ask. Set the issue to
   **Done** and end your turn with `MERGED <PR url>` first. That wakes the coordinator, which plans and starts the
   next issue, or the ship session.
8. **Red:** see [Red CI](#red-ci).

**A project with one issue** has no issue PR. Work on the project branch itself (`git checkout project/<slug>`),
push it after step 5, set the issue to **Done**, and report `READY project/<slug>`: the ship session opens the only
PR. If nobody started you, tell the user the next step is `/ship-project <project>` in a new session on Opus at
medium effort.

**An issue with no project** (a stand-alone improvement): there is no project branch and no ship session. Branch from
`dev`, run `scripts/verify.ps1 local` and `mw-code-review` (light) before the PR, open the PR to `dev`, and leave the
merge to the user.

## B. Bug mode

The user wrote this rule for bug sessions: **merge your bug PR to `dev` without asking once CI is green.** It covers
only PRs you opened, based on `dev`, in this session.

1. **Each bug, one at a time.** Commit each fixed bug separately (`Fix MEA-35: …`).
   - **With a `## Fix plan`** in the issue (approved in the coordinator): execute it. Write its red test (or flip the
     pinning assertion), run it and check it fails for the reason the plan gives, then make the fix it describes.
     Don't diagnose again. A red test that passes today or fails for another reason is stop rule 8.
   - **Without one:** use `mw-diagnosing-bugs`: a red test first (or flip the test that pins it), then the fix.
2. **Ask before fixing** (stop as above) if the fix would:
   - add or edit a migration or an RLS policy (DB-01, DB-02);
   - change a handler's request or response, or a state container's public members;
   - touch the Function contract (FN-01) or the SSRF guard (FN-02);
   - add a package or project, or change DI registrations (ARCH-08);
   - change behaviour a user would notice beyond the bug itself;
   - be more than about 150 changed lines outside tests.

   Then you also don't self-merge that PR unless the user says so in this session. A part the Fix plan's "Cleared by
   the user" section (or the brief's "Cleared by the user before start") already approves, exactly as you'll do it,
   counts as asked and answered, for fixing and for merging.
3. **Docs:** update "Bugs currently pinned by tests" in `docs/agents/TESTING.md` and any FEATURES.md lines you
   changed.
4. **Verify** with `scripts/verify.ps1 fast`, plus only the step that holds each bug's red test when that isn't a
   unit or bUnit test: `scripts/verify.ps1 local --only <step>` for an integration or pgTAP test, or
   `local --e2e <class>` for a bug pinned by an E2E test. CI runs the rest.
5. **Self-review** with `mw-code-review` (light): a bug PR goes straight to `dev` and has no ship session behind it.
   Then **open the PR** (below), with one `Fixes MEA-xx` line per bug.
6. **Wait for CI** with one background command: `gh pr checks <n> --watch --fail-fast` (Bash, `run_in_background`).
   You're notified when it exits. Don't check in between.
7. **Green:** merge with `scripts/merge-pr.sh <n>` (Bash). Never call `gh pr merge` directly. If the script refuses,
   don't work around it: read why, then fix it or stop and ask. Set each issue to **Done** if the integration hasn't.
   End your turn with `MERGED <PR url>` and one line per bug: root cause and fix.
8. **Red:** see [Red CI](#red-ci).

## Opening the PR

1. `git push -u origin HEAD`.
2. Write the body to a file and `gh pr create --base <base> --title "<ID>: <title>" --body-file <file>`, where
   `<base>` is the project branch (feature) or `dev` (bug). Keep an issue PR's body short: the ship PR carries the
   architecture write-up and the screenshots.

   ```
   <What changed and why, 2-5 lines.>

   <Feature: "Part of <project>: MEA-xx" | Bug: one "Fixes MEA-xx" line per bug>

   ## Stories
   - Story <n>: <one line> (commits <sha>..<sha>)        <!-- feature mode only -->

   ## Verified
   - `verify fast: PASSED in …` (and any `--only` steps you ran). The rest is CI's.

   ## Decisions and assumptions
   - What you decided yourself (stop rules), and anything the ship review should look at.

   ## Filed
   - Bugs you filed while working (MEA-xx), or "None".

   🤖 Generated with [Claude Code](https://claude.com/claude-code)
   ```
3. Move each issue to **In Review**.

## Red CI

Read the failure (verify skill; `gh run download <run-id> -n test-results`), reproduce it with
`scripts/verify.ps1 <level> --only <steps>`, fix, push, and watch again.

- A known flake (GoTrue `429`, "JWT issued at future") gets one rerun (`gh run rerun <run-id> --failed`) that
  doesn't count.
- After **3** fix attempts, stop: leave the PR open and unmerged, comment on each issue with what you tried, and
  end your turn with `STUCK <PR url>` and the failing check. The coordinator tells the user.
- **Merge conflict** with the base: bring the branch up to date with `sync_with_base_branch` (ccd_host), resolve,
  run `verify fast`, push.

## Cloud mode

One cloud session works through the issues itself, in order, with its own container, Docker and Supabase stack, a
Linux bash shell, and GitHub through the GitHub MCP tools (no `gh` CLI). There's no coordinator to message: the user
is the only one you report to. Each issue still gets its own branch and its own PR, into the project branch (bugs:
into `dev`).

### The loop

Before **every** issue, including the first and every time you're woken:

1. **PRs first.** List this session's open PRs (`list_pull_requests`, `state: "open"`, with `base` the project branch,
   or `dev` for bugs) and, for each, read it at its current head (`pull_request_read`: `get`, `get_check_runs`). Work
   every open item before anything else: merge conflict, then red CI. A PR whose head moved since its last run waits
   for the new run; a check result for an older head decides nothing.
2. **Merge what's green.** An own PR that is open, not a draft, mergeable (`mergeable_state: "clean"`) and green on
   its **current** head, and that is an issue PR into the project branch or a bug PR into `dev`:
   `merge_pull_request` with `merge_method: "merge"`, set its issue to **Done**, `unsubscribe_pr_activity`. Never a
   PR into `main`, and never the ship PR (a project branch into `dev`).
3. **Keep the rest current.** After a merge, the base has moved: merge it into every remaining open PR branch,
   resolve conflicts (keep both sides' resource entries, E2E Facts and FEATURES.md lines), run `verify fast`, push.
4. **Pick the next issue.** The next Todo issue whose blockers are all **merged** (Done), not just "PR open". If
   every remaining issue waits on an open PR, there's nothing to start: say what you're waiting for and end your
   turn; CI events wake you. When every issue is Done, start the ship session (plan-project, Cloud mode step 4).

Then do the issue as in [Feature mode](#f-feature-mode) or [Bug mode](#b-bug-mode), with these changes:

- **Branch from the latest base, every time:** `git fetch origin <base> && git checkout -b <id>-<slug> origin/<base>`.
  Never start from the branch the session was opened on, or from another issue's branch. Check with
  `git log --oneline origin/<base>..HEAD` before the first push: only this issue's commits.
- **Set up once per session:** if `dotnet --list-sdks` shows no 10.x SDK, or `supabase` or `supabase-go` isn't on
  PATH, run `bash scripts/cloud-setup.sh` (about a minute). It also starts Docker.
- **Verify** with `scripts/verify.sh`, wherever this skill says `scripts/verify.ps1`. **Don't switch branches, merge
  or edit files until it exits**: verify builds and tests the working tree.
- **Push** with `git push -u origin <branch>`. A 403 or "GitHub access" refusal isn't fixable from here: read the
  `github.access` page with `read_documentation`, tell the user what it says, and stop. Retry only network errors.
- **Opening the PR.** Write the same body, then `create_pull_request` (GitHub MCP) with `base` the project branch
  (or `dev` for bugs), then `subscribe_pr_activity` for it. Move the issue to **In Review**. Don't wait on CI with a
  watch command: go back to [the loop](#the-loop), which merges the PR once its checks are green.
- **A later issue that needs an unmerged one** waits for that merge. Don't stack branches.
- **Merge conflict:** `git fetch origin <base> && git merge origin/<base>`, resolve, `verify fast`, push. Never
  rebase or force-push a branch that already has a PR, unless the user asks.
- **Red CI.** Read the failing step with `get_job_logs`. A failure in a test the PR doesn't touch, with a known
  infrastructure cause (GoTrue's `PGRST303 "JWT issued at future"`), gets **one** re-run (`actions_run_trigger`,
  `rerun_failed_jobs`) and **one** comment on the PR naming the test and why it isn't this PR's. A second failure is
  real. Anything else is yours to fix (same 3-attempt limit). A failure in CI itself (a workflow or action that breaks
  every PR) is a separate fix: tell the user, and fix it in its own PR to `dev` only on their OK (stop rule 7).
- **Checking a merge:** `pull_request_read` (`merged`, `base.ref`) instead of `gh pr view`.

### Status

Whenever you end a turn, say in a few lines: what merged, which PRs wait on CI, which issue is in progress and its
last verify verdict, and what's next. The user can't see your background commands.

## Report

Your last message is relayed to the coordinator (in cloud mode, it's for the user); its first 400 characters matter
most. Lead with:
`MERGED <url>` or `READY project/<slug>` or `PR <url> (<ID>)` or `BLOCKED <ID>: …` or `PLAN <ID>: …` or
`STUCK <url>`, then the verify verdict line, then anything the user must do (answer a question).
