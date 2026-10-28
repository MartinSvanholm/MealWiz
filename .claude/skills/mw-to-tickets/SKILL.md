---
name: mw-to-tickets
description: Break a spec, plan or conversation into tracer-bullet Linear issues under a project, each a vertical slice one session can finish in one PR, linked with native "blocks" relations. Use from /plan-project, or when asked to split a feature into issues or tickets.
---

<!-- Adapted from mattpocock/skills `skills/engineering/to-tickets` (MIT), with the task-graph idea from
`skills/engineering/implement-spec`. See `.claude/skills/THIRD_PARTY_NOTICES.md`. -->

# To tickets

Break the work into **issues**: tracer-bullet vertical slices, each declaring the issues that **block** it. The
issues form a **task graph**, not a list: at any time the **frontier** is every issue whose blockers are all Done,
and the frontier is what runs in parallel.

## 1. Gather context

Work from the conversation. If you were given a Linear project, read its description (the spec), its documents
(`list_documents project:` → "Research", "Contract") and any issues it already has. Don't duplicate existing issues.

## 2. Explore the code

If you haven't yet, read the affected sections of `docs/agents/FEATURES.md` and the rules in
`docs/agents/ARCHITECTURE.md` the work touches. Use the app's names (S-01).

Look for **prefactoring**: "make the change easy, then make the easy change". A prefactor is its own issue, first.

## 3. Draft the slices

<vertical-slice-rules>

- Each slice cuts a narrow but COMPLETE path through every layer it needs: migration + RLS + pgTAP, handler,
  state, page, tests. Vertical, NOT one layer at a time.
- A finished slice is demoable or verifiable on its own, and leaves `dev` green.
- Each slice fits one fresh session and one PR to `dev`. If it doesn't, split it, or make it a parent with
  sub-issues (the only time to use sub-issues).
- Prefactoring comes first.
- Each slice ships its tests (TEST-02) and its doc updates (DOC-01).

</vertical-slice-rules>

Give each issue its **blocking edges**: the issues that must be merged before it can start. An issue with no blockers
can start immediately. Only add an edge when one issue truly needs the other's code; issues that merely touch the
same feature can run in parallel if they edit different files.

**Shared seams.** When two issues that run in parallel meet at a seam (a table, a payload, a handler request/response,
a state container's public API, a config key), the seam must be agreed before either starts. Write it into a
**Contract** document on the project (the calling skill has the template). An issue that uses a contract item names
it; it codes against the contract and fakes the other side until that side exists. No shared seam, no contract.

**Shared files.** List files more than one parallel issue must edit (`_Imports.razor`, DI in
`ServiceCollectionExtensions.cs`, `docs/agents/FEATURES.md`, …) and say which issue owns which edit, so two
sessions never rewrite the same lines. If that gets messy, add an edge instead.

**Wide refactors** are the exception to vertical slicing: one mechanical change whose blast radius fans across the
codebase (rename a column, retype a shared model). Sequence it as **expand–contract**: first expand (add the new
form beside the old, so nothing breaks), then migrate call sites in batches (one issue per feature folder, each
blocked by the expand), then contract (delete the old form, blocked by every batch). Every step leaves `dev` green.
A migration that has reached `main` is never edited (DB-02); expand and contract are new migrations.

## 4. Quiz the user

Present the breakdown as a numbered list. For each issue:

- **Title**: short and descriptive
- **Blocked by**: which issues, if any
- **What it delivers**: the end-to-end behaviour it makes work
- **Verify level**: `smoke` or `full`, from "Which tests for which change" in `docs/agents/TESTING.md`

Then show the frontier waves: what starts first, what starts after each merge. Ask with AskUserQuestion:

- Does the granularity feel right (too coarse / too fine)?
- Are the blocking edges right: does each only depend on issues that really gate it?
- Should any be merged or split?

Iterate until the user approves.

## 5. Publish to Linear

Create the issues in dependency order (blockers first) so edges can name real identifiers:

- `save_issue` with `team: "MealWiz"`, `project: <project>`, `state: "Todo"`, `labels: ["Feature"]` (or
  `["Improvement"]` for a change to an existing behaviour), and `blockedBy: [<ids>]` for its edges. Linear's native
  relation is the source of truth, so don't repeat the edges in the description.
- Sub-issues, where you decided on them: `parentId: <parent>`.
- Never close or edit issues outside the project.

<issue-template>

## What to build

The end-to-end behaviour this issue makes work, from the user's perspective, not a layer-by-layer list.

## Acceptance criteria

- [ ] Criterion 1
- [ ] Criterion 2
- [ ] Tests: the seams and suites (e.g. "integration test for the new handler; bUnit for the page")
- [ ] `scripts/verify.ps1 <level>` passes

## Contract items used

The items from the project's Contract document, e.g. `DB-1`, `API-1`. Omit if there's no contract.

## Notes

Shared-file ownership, prior art to copy, anything the spec leaves to this issue. Omit if empty.

</issue-template>

Avoid file paths and code in issues; they go stale. Exception: a schema or type shape that states a decision more
exactly than prose, or a shared file whose ownership you're assigning.
