---
name: mw-to-tickets
description: Break a spec, plan or conversation into a few large Linear issues under a project, each a group of user stories one session can finish in one PR, linked with native "blocks" relations. No sub-issues. Use from /plan-project, or when asked to split a feature into issues or tickets.
---

<!-- Adapted from mattpocock/skills `skills/engineering/to-tickets` (MIT), with the task-graph idea from
`skills/engineering/implement-spec`. See `.claude/skills/THIRD_PARTY_NOTICES.md`. -->

# To tickets

Break the work into **issues**: large vertical slices, each declaring the issues that **block** it. The issues form
a **task graph**: at any time the **frontier** is every issue whose blockers are all Done.

The hierarchy has two levels:
- **Project**: the feature. Its description is the spec. It has one branch, `project/<slug>`, that every issue's PR
  merges into, and one PR to `dev` at the end (`/ship-project`).
- **Issue**: a group of user stories that belong together and leave the app working. One session, one branch, one PR
  into the project branch.

There are **no sub-issues**. The layers of an issue (database, handlers, UI) are steps in its implementation plan.

## 1. Gather context

Work from the conversation. If you were given a Linear project, read its description (the spec), its documents
(`list_documents project:` → "Research") and any issues it already has. Don't duplicate existing issues.

## 2. Explore the code

If you haven't yet, read the affected sections of `docs/agents/FEATURES.md` and the rules in
`docs/agents/ARCHITECTURE.md` the work touches. Use the app's names (S-01).

## 3. Draft the issues

Every session costs a start-up (reading the docs, the spec and the code), so **few, large issues** beat many small
ones.

<slicing-rules>

- **Aim for one to three issues per project.** One issue is right for any feature a single session can finish. Add
  an issue only when the first would be too large (more than about five user stories, or more than one new feature
  folder), or when part of the work can truly run in parallel.
- Each issue is a COMPLETE path through every layer it needs: migration + RLS + pgTAP, handlers, state, pages,
  tests. Vertical, NOT one layer at a time. Never split an issue by layer.
- A finished issue is demoable on its own and leaves the project branch green.
- Each issue lists the user stories it covers, by their number in the spec. Its session commits story by story, so
  the PR can be read one story at a time.
- Each issue ships its tests (TEST-02) and its doc updates (DOC-01).
- Prefactoring ("make the change easy, then make the easy change") is the first story of the first issue, not an
  issue of its own, unless it is large.

</slicing-rules>

Give each issue its **blocking edges**: the issues that must be merged into the project branch before it can start.
Prefer a chain. Let two issues run in parallel only when they edit different files and share no seam (a table, a
handler's request/response, a state container's public members). If they share one, add an edge instead: the second
issue is planned against the first one's merged code, so nothing has to be agreed up front.

**Wide refactors** are the exception to vertical slicing: one mechanical change whose blast radius fans across the
codebase (rename a column, retype a shared model). Sequence it as **expand–contract**: expand (add the new form
beside the old), migrate the call sites, contract (delete the old form). That is usually one issue with three groups
of commits; split it only when the call sites don't fit one session. A migration that has reached `main` is never
edited (DB-02); expand and contract are new migrations.

## 4. Check with the user

Present the breakdown as a numbered list. For each issue:

- **Title**: short and descriptive
- **Stories**: the spec's user stories it covers
- **Blocked by**: which issues, if any
- **What it delivers**: the end-to-end behaviour it makes work

Then the order: what starts first, and what starts after each merge.

Called from `/plan-project`, don't ask here: return the draft. The caller asks about it in its one round of
questions, together with the first plan's questions, and tells you when to publish. Called on its own, ask with
AskUserQuestion whether the issues should be merged or split and whether the edges are right, and iterate until the
user approves.

## 5. Publish to Linear

Create the issues in dependency order (blockers first) so edges can name real identifiers:

- `save_issue` with `team: "MealWiz"`, `project: <project>`, `state: "Todo"`, `labels: ["Feature"]` (or
  `["Improvement"]` for a change to an existing behaviour), and `blockedBy: [<ids>]` for its edges. Linear's native
  relation is the source of truth, so don't repeat the edges in the description.
- Never close or edit issues outside the project.

<issue-template>

## What to build

The end-to-end behaviour this issue makes work, from the user's perspective, not a layer-by-layer list.

## User stories

The spec's stories this issue covers, by number, each with its acceptance criteria:

- [ ] **Story 3**: <the story in one line>
  - <criterion>
  - <criterion>

## Done when

- [ ] Every story above works end to end
- [ ] Tests: the seams and suites (e.g. "integration tests for the new handlers; bUnit for the page; a journey step")
- [ ] `scripts/verify.ps1 fast` passes, and CI is green on the PR into the project branch

## Notes

Prior art to copy, anything the spec leaves to this issue. Omit if empty.

</issue-template>

Avoid file paths and code in issues; they go stale. Exceptions: a schema or type shape that states a decision more
exactly than prose, and the `## Implementation plan` section that `/plan-project` appends to an issue just before it
starts.
