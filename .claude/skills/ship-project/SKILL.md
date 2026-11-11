---
name: ship-project
description: Step 4 of a MealWiz feature, the last gate before dev. In a fresh session on Opus at medium effort, bring the project branch up to date with dev, run verify full, review the whole project diff against the spec and the architecture rules, fix what it finds, take screenshots of the changed UI and attach them to Linear, and open the one PR from project/<slug> to dev with the architecture, the critical changes and the screenshots. The user merges it. Use when every issue of a project is merged into its project branch, from /plan-project, or when the user says "/ship-project X", "ship project X" or "review and verify project X".
---

# Ship a project

Step 4 of 4 (spec → plan → implement → **ship**). The issue sessions only ran the fast checks and didn't review
their work. This session is the project's one full verification and its one independent review, so it runs in a
**fresh session** on **Opus** at **medium** effort: you didn't write this code, and you read it as a reviewer.

If this session wrote any of the project's code, or isn't on Opus, say so and ask the user whether to go on or to
start a new session.

Standing rules:
- You work on the project branch, `project/<slug>`. Fixes you make are commits on it.
- The PR you open, from the project branch to `dev`, is **the user's to merge**, in cloud mode too. Never merge it,
  and never touch `main`.
- Only this project. A bug outside its diff goes in Linear (label **Bug**, team MealWiz, no project, Backlog), after
  checking it isn't there already.
- Never poll. Run verify in the background and wait for its notice.

## 1. Read

1. `get_project`: the spec, and the branch on its first line. `list_issues project:`: every issue must be **Done**
   (merged into the project branch, or pushed to it for a one-issue project). If one isn't, stop and say which.
2. Read each issue's stories and its PR's "Decisions and assumptions" (`gh pr list --base project/<slug> --state
   merged`): they tell you where to look hardest.
3. `docs/agents/ARCHITECTURE.md`, `docs/agents/TESTING.md`, and the project's sections of `docs/agents/FEATURES.md`.

## 2. Bring the branch up to date

`git fetch origin`, check out `project/<slug>` at `origin/project/<slug>`, and merge `origin/dev` into it. Resolve
conflicts (keep both sides' resource entries, E2E Facts and FEATURES.md lines) and commit. Don't rebase: the issue
commits are the story-by-story history the user reads.

## 3. Verify in full

`scripts/verify.ps1 full` (bash: `scripts/verify.sh full`), in the background. It is the only full run the project
gets before `dev`, so don't swap it for a lower level. Don't edit files or switch branches until it exits.

- **Red:** triage with the `verify` skill. A failure the project caused is yours to fix: a regression test first
  where one is missing, then the fix, one commit per fix (`Ship <project>: fix …`), then rerun the failed steps
  (`--only <steps>`), and the full level once more at the end. After **3** attempts on the same failure, stop and
  report `STUCK`.
- A failure the project didn't cause (it fails on `origin/dev` too): file it as a bug, name it in the PR, go on.

Start the review while verify runs; just don't change files until it has exited.

## 4. Review the whole project

Run `mw-code-review` in **ship mode** on `origin/dev...HEAD`: you review it yourself, both axes, with the whole
diff and the spec in front of you. Beyond the two axes, look for what no single issue session could see:

- **Across issues:** the same thing named or done two ways, duplicated helpers, a seam where one issue's code
  doesn't match what the next one assumes, dead code a later issue made redundant.
- **Every user story in the spec**, one by one: where it's implemented and which test proves it. A story with no
  test is a finding.
- **Data and security:** every new table has `created_by`, RLS and pgTAP (DB-01); no key but the anon key reaches a
  client (SEC-01); no edited migration that reached `main` (DB-02).
- **Both languages** for every new UI text (UI-03), and phone as well as desktop layouts.
- **Docs:** FEATURES.md, TESTING.md and CLAUDE.md describe what the project added (DOC-01).

Act on the findings:
- 🔴, a Spec gap, or a missing test: fix it on the project branch, test-first, and rerun the affected verify steps.
- A fix that needs a product or architecture decision, a migration the spec doesn't mention, or more than about 150
  changed lines: **ask the user** (AskUserQuestion) before doing it.
- 🟡 you leave: list them in the PR with the reason.

If you changed code after the full verify, run `scripts/verify.ps1 full` again before the PR. The verdict in the PR
must be for the head you open it from.

## 5. Screenshots

The user reviews the UI from the PR, so show it.

1. Run the web app against the local stack verify left running:
   `dotnet run --project MealWiz/MauiWASM/MauiWASM.csproj --launch-profile http` (http://localhost:5053), and log in
   as the seeded `test@test.com` / `tester` (local stack only).
2. With a browser tool (the `web` preview in the desktop app, or Playwright's Chromium in a cloud container), capture
   **every page, dialog and state the project added or visibly changed**: at phone width (390×844) and desktop width
   (1280×800), with realistic data, and the empty and error states the spec names. Name the files
   `<nn>-<page>-<state>-<phone|desktop>.png`. Look at each one: a screenshot of a spinner or an error bar is a
   finding, not a deliverable.
3. Attach them to Linear, one file at a time: `prepare_file_upload` (filename, `image/png`, exact size), PUT the
   bytes to the returned URL with every returned header, then keep the markdown it returns. The signed URL lasts 60
   seconds, so finish one file before preparing the next.
4. Save a **Screenshots** document on the project (`save_document`, `project:`, title "Screenshots"): one heading per
   user story or page, the images under it, phone beside desktop, with a one-line caption each.

Linear's image URLs are signed and expire, so they can't be embedded in the PR: the PR links the document. If the
upload is blocked (in a cloud container the upload host may not be on the network allowlist), don't commit the
images to the repo: tell the user which host was refused and list the files you captured.

A project with no UI change has no screenshots; say so in the PR.

## 6. Open the PR to dev

Push the project branch, then open the PR: `gh pr create --base dev --head project/<slug>
--title "<Project name>" --body-file <file>` (cloud: `create_pull_request`, then `subscribe_pr_activity`). Write the
body from [references/ship-pr-template.md](references/ship-pr-template.md): what shipped, the architecture, the
critical changes, the screenshots, the verify verdict and the review.

Write it for a reader who hasn't followed the project. Explain the design in prose, the way you'd walk a colleague
through it, and point at files; don't paste the diff back.

Then post a project status update (`save_status_update`, `type: "project"`) with the PR link, and end your turn with
`PR <url>` first, the verify verdict line, and what the user should look at first.

## 7. After the PR

- **Red CI:** it passed locally at this head, so read the failing step before changing anything. Known flakes (GoTrue
  `429`, "JWT issued at future") get one rerun. Anything else: reproduce, fix, push; 3 attempts, then `STUCK`.
- **Review comment from the user:** if it's right, fix it, rerun the affected verify steps, push, and reply saying
  what changed. If you disagree, reply with your reasoning and ask.
- **`dev` moved and the PR conflicts:** merge `origin/dev` in, resolve, `verify fast`, push.
- **Merged** (by the user): confirm it (`gh pr view <n> --json state,mergedAt,baseRefName`), set the project to
  **Completed**, post a final status update (what shipped, the PR, follow-up bugs filed), and end your turn with
  `MERGED <url>`. Remind the user that releasing (`dev` → `main`) is their call and deploys to production.
