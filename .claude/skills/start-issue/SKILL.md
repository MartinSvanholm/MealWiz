---
name: start-issue
description: Start work on a MealWiz Linear issue (MEA-xx) the way this repo works. It reads the issue, picks the right flow and runs it to merge, release and cleanup. Issues with sub-issues get a work plan and a contract between the sub-issues, stored as a Linear document, and the sub-issues are worked in parallel sessions so they don't block each other. Bugs in features already released to production are fixed on main as a hotfix and then merged back into dev. Use this whenever the user says "start MEA-..", "begin/pick up/work on issue ..", "next issue", "fix bug MEA-..", "hotfix", or pastes a linear.app/mealwiz issue link to work on, even if they don't mention the skill.
---

# Start a MealWiz issue

This skill encodes how the user wants Linear issues worked in this repo. It was written after building the
recipe-import feature (MEA-11 → MEA-12..17), so the rules below come from what worked there and what didn't.

**Standing preferences, which apply in every flow and every sub-session:**
- **Ask before deciding.** Use plan mode for anything non-trivial and AskUserQuestion for real implementation
  choices. The user wants to be asked, not to find out later. Give a recommendation as the first option.
- **Commit, push, open a PR or merge only when the user asks.** PRs normally target `dev`; hotfixes target `main`.
- **Never put secrets in committed files.** Never commit `supabase/snippets/` (the user's own folder).
- **Use FluentResults `Result` rather than custom exceptions** (see `.claude/docs/architectural_patterns.md`).
- **Verify claims before repeating them.** Check git, CI and logs yourself; a sub-session's report is a claim.
- **Keep commands fast.** Build only the affected projects, and run anything over ~2 minutes in the background. See
  `references/repo-notes.md` for commands and Windows pitfalls.

## 1. Read the issue and choose the flow

1. `get_issue` the issue, with relations. Also read its parent, if it has one, and its sub-issues
   (`list_issues parentId=<id>`).
2. `git fetch origin`. Then decide:

| Situation | Flow |
|---|---|
| The issue has sub-issues (a feature / epic) | **B. Feature with sub-issues** |
| A bug or change in a feature that is **already on `main`** (released) | **C. Hotfix** |
| Anything else: a single issue, or a bug in work that is still only on `dev` | **A. Single issue** |

A feature counts as released when its code is on `origin/main`. Check with `git log origin/main` or
`git merge-base --is-ancestor <merge-commit> origin/main`, not with the Linear status. If you're unsure which flow
fits, ask the user.

3. Move the issue to **In Progress** in Linear.

## A. Single issue

1. Create the branch Linear suggests (`gitBranchName`) from `origin/dev`:
   `git switch -c <gitBranchName> origin/dev`. If you're in a worktree session, branch from the up-to-date `dev`.
2. Explore the relevant code. Read `.claude/docs/architectural_patterns.md` and the parent issue's contract
   document, if one exists.
3. Plan in plan mode, ask the open questions, and get the plan approved.
4. Build and verify:
   - targeted builds and tests (see `references/repo-notes.md`);
   - local Supabase for anything touching the DB;
   - for UI, run the app and look.
5. When the user asks, commit and open a PR to `dev`, describing what was verified and how. After opening it, bind
   the PR (ccd_pr `get_status`/`bind_pr`) and offer Auto-fix.
6. After it merges, move the issue to **Done** if the GitHub integration hasn't, then **clean up** (section D).

## B. Feature with sub-issues

The goal is for every sub-issue to start at once, instead of waiting on each other. That works when the seams
between them are agreed before anyone builds. This session becomes the **coordinator**. It plans and integrates
but doesn't implement the sub-issues itself.

### B1. Work plan and contract

1. Read every sub-issue, its relations and the parent's spec. Explore the code each one will touch.
2. Identify the **seams**: everything one sub-issue produces and another consumes. In this repo that is typically:
   - DB tables/columns, status values, RLS and Realtime;
   - JSON payload shapes, with exact property names and casing;
   - HTTP endpoints, with request/response bodies and status codes;
   - config sections and keys, per environment, plus secret names;
   - the public C# interfaces of state containers and handlers that another slice calls;
   - **naming**: the feature's name in code, folders and UI text. Match the rest of the app; for example, the import
     feature is "Meal", not "Recipe". A late rename forces every other sub-issue to follow, as happened with the
     `MealImportFunction` config section.
3. Ask the user about every seam that has a real choice (AskUserQuestion), and about the plan itself.
4. Write the plan and contract as a **Linear document attached to the parent issue** (`save_document` with
   `issue: <parent>`), using `references/contract-template.md`. Link it in a comment on each sub-issue. It is the
   source of truth; sub-sessions read it instead of relying on notes in a temp folder.
5. Get the user's approval of the plan document.

### B2. Start the sub-issues in parallel

1. `git fetch origin dev:dev` (or `git pull` on dev) so worktrees start from the current `dev`.
2. Move each sub-issue you start to **In Progress**.
3. Start **one session per sub-issue that can start now**, all in the same turn. Use `start_session` with
   `initiation: "user_asked"`, since invoking this skill is the request, plus:
   - `context: "fresh"` and `use_worktree: true`;
   - `source_branch: "dev"` and a short `branch_name`.

   A sub-issue whose seams are all in the contract can start even if the code behind them doesn't exist yet: it
   codes against the contract and fakes the other side in tests.
4. Write each brief from `references/session-brief.md`. It must name the contract document, the files and areas the
   session **owns**, the ones it must **not touch** (other sessions' areas), and the report-back format.
5. Mark a chapter before starting them, then tell the user which sessions are running.

### B3. Coordinate while they run

- **Contract changes go through the coordinator.** A sub-session that needs one stops and messages the
  coordinator. The coordinator asks the user if it's a real decision, updates the Linear document, and messages
  every affected session.
- **Follow-up work keeps an owner.** A sub-session shouldn't leave fixes uncommitted in a side worktree nobody
  owns. Either the fix goes on its own branch with a PR when the user asks, or it's reported back so the
  coordinator can place it.
- **Check each report** against the branch, the diff and CI before telling the user it's done.
- **Answer questions and forward approvals.** When a sub-session asks something only the user can answer, ask the
  user and pass the answer on, quoting the user's words.

### B4. Integrate, then release

1. **Integration test before merging.** Once the sub-issue branches are committed, test them together. Create a
   worktree from `dev`, merge the sub-issue branches into it locally (never push it), then run both apps locally
   and click through the feature (`references/repo-notes.md` → "Run locally").
2. **Merge in dependency order** when the user asks, each PR to `dev` with green CI. After each merge, check that
   the next PR is still mergeable.
3. **Release** when the user asks: open a PR `dev` → `main`. CI runs on it. The description lists what ships, the
   migrations that will run, and the post-deploy checks. Merging it runs `deploy.yml`.
4. **Before the first release of anything new, run through the production checklist** in
   `references/repo-notes.md`. Local tests don't catch production config. The first import failed because the
   Function App still had a disabled legacy Supabase key.
5. After the merge, watch the Deploy run (`gh run watch <id>` in the background), then verify production yourself.
   Have the user do the logged-in check.
6. Move the parent and sub-issues to **Done**, then clean up (section D).

## C. Hotfix: bug in a released feature

Production runs `main`. A bug there is fixed on `main` directly, so the fix doesn't have to wait for, or drag
along, unreleased work on `dev`.

1. Confirm the affected code is on `origin/main` (step 1). If the bug is only on `dev`, use flow A instead.
2. Investigate from evidence first. Production data and logs usually locate the fault faster than reading code;
   see "Debugging production" in `references/repo-notes.md`.
3. Branch from main: `git switch -c <gitBranchName> origin/main`. In a worktree session, use
   `source_branch: "main"`, after `git fetch origin main:main`.
4. Plan, ask, fix and verify as in flow A. Keep the change minimal; refactors go to `dev` separately.
5. When the user asks, open the PR with **base `main`**. Merging it deploys: watch the Deploy run and verify the fix
   in production.
6. **Merge `main` back into `dev`:** open a PR `main` → `dev` (`gh pr create --base dev --head main`) and merge it
   when CI is green and the user agrees.
   - On conflicts, create a branch from `dev`, `git merge origin/main`, resolve, push it, and PR that branch into
     `dev`.
   - Never rebase or force-push `dev` or `main`.
7. Move the issue to **Done**, then clean up (section D).

## D. Clean up when done

When an issue's PRs are merged (and released, for a feature), delete the worktrees, so finished work doesn't
leave copies of the repo behind:
1. List the worktrees (`git worktree list`). For each one used by this work, including side sessions those
   sessions started:
   - check it has no uncommitted changes (`git -C <worktree> status --short`);
   - check its branch is merged: `git merge-base --is-ancestor <branch> origin/dev`, or `origin/main` for hotfixes.

   If a worktree holds uncommitted changes, show the user what's there. Removing it is fine only once you've
   checked the changes already exist on the target branch (renamed paths count) and the user agrees.
   Never discard work silently.
2. Ask the user once, listing the sessions. Then archive them with `archive_session`. Archiving a session also
   archives the side sessions it started.
3. **Archiving does not reliably delete the worktree folders.** In practice they stayed registered, some on a
   detached HEAD. So afterwards:
   - remove each with `git worktree remove .claude/worktrees/<name>`, or `--force` for the agreed uncommitted ones;
   - then `git worktree prune` and check `git worktree list`.
4. **Windows blockers:**
   - "Invalid argument" usually means a path over 260 characters or a locked file. The folder may already be
     unregistered from git but still on disk.
   - Delete what's left with a **literal** path (a variable path is blocked as unsafe):
     `cmd /c 'rmdir /s /q "\\?\C:\Users\marti\Documents\GitHub\MealWiz\.claude\worktrees\<name>"'`.
   - Files still in use, such as `.vs\…\*.vsidx` or `bin\Debug`, mean Visual Studio or a running app has that
     worktree open. Don't kill those processes. Ask the user to close the solution, then delete the folder.
5. Remove the rest:
   - temporary `.claude/launch.json` files;
   - stray empty folders under `.claude/worktrees/`;
   - local branches that are fully merged (`git branch -d <branch>`; never `-D` without asking).
6. Switch the main checkout back to an up-to-date `dev`
   (`git switch dev; git merge --ff-only origin/dev`) if it's sitting on a merged branch.
