---
name: spec-project
description: Step 1 of a MealWiz feature. Interview the user about a new feature in rounds, then write the spec into the Linear project's description. Use when the user wants to spec, scope or discuss a new feature or Linear project, says "/spec-project", "let's spec X", or "new project for X".
---

# Spec a feature project

The first of three steps for a feature (spec → plan → implement). This session only talks and writes the spec. It
writes no code and creates no issues; `/plan-project` does that, in a new session.

## 1. Find or create the project

- The user names a Linear project (name, slug or link): `get_project`, and `list_issues project:` to see what's in
  it. Read any existing description and documents.
- No project yet: propose a name and a one-line summary, and create it on the user's yes (`save_project` with
  `name`, `addTeams: ["MealWiz"]`, `summary`, `state: "Backlog"`).

## 2. Learn the area

Before asking anything, read `docs/agents/FEATURES.md` for the features this touches, and the rules in
`docs/agents/ARCHITECTURE.md` that will constrain it (DB-01 for a new table, FN-01 for the Function contract, …).
Send an **Explore** subagent for anything wider: current behaviour, similar features to copy. Facts are yours to
find; never ask the user what the code can tell you.

## 3. Interview

Run `mw-grilling` until the frontier is empty. Cover at least:

- the problem and who has it; what "done" looks like for the user
- the user stories and edge cases: empty states, errors, signed-out, phone vs desktop (MAUI and web share the UI)
- data: new tables or columns, ownership (`created_by`), who can see what (RLS)
- UI: which pages change, new pages or drawers, navigation
- anything outside the app: the Function, external sites, new config or secrets (SEC-01)
- the test seams (step 2 of `mw-to-spec`)
- what's out of scope

Don't decide anything the user hasn't. An answer you can't get now becomes an Open question in the spec.

## 4. Write the spec

Run `mw-to-spec`: it writes the description with `save_project`. Then:

- set the project status to **Planned** (`save_project`, `state: "Planned"`);
- tell the user the spec is in place, link the project, and say the next step is `/plan-project <project>` in a
  **new session**, which researches, slices the work into issues and starts the implementation sessions.
