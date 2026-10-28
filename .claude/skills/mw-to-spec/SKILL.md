---
name: mw-to-spec
description: Turn the current conversation into a feature spec and write it into a Linear project's description. No interview, just synthesis of what was already discussed. Use after an interview about a feature, from /spec-project, or when asked to write up a spec.
---

<!-- Adapted from mattpocock/skills `skills/engineering/to-spec` (MIT). See `.claude/skills/THIRD_PARTY_NOTICES.md`. -->

# To spec

Take the conversation and your understanding of the codebase and write a spec. Do NOT interview the user; synthesise
what you already know. If a decision is still open, list it under **Open questions** rather than deciding it.

## Process

1. **Know the area.** If you haven't yet, read the feature's section of `docs/agents/FEATURES.md` and the rules in
   `docs/agents/ARCHITECTURE.md` that the feature will touch. Use the app's names (meal, ingredient, meal plan,
   grocery list, meal import), not new ones (S-01).

2. **Sketch the test seams.** A seam is the public boundary a test drives: a handler through MediatR, a page or
   component in bUnit, a state container, an E2E journey, a pgTAP file. Prefer existing seams, and the highest one that
   catches the behaviour; the fewer the better. Map each to its suite with the table "Which tests for which change" in
   `docs/agents/TESTING.md`. Check with the user that these seams match what they expect.

3. **Write the spec** with the template below. Keep it to one or two screens. Don't put file paths or code in it; they
   go stale. Exception: a schema, payload or type shape that states a decision more exactly than prose can.

4. **Publish it** as the project's description: `save_project` with the project id and `description`. Also set a
   one-line `summary`. If the project already had a description, show the user what you'll replace before writing.

<spec-template>

## Problem

The problem the user faces, from the user's perspective.

## Solution

The solution, from the user's perspective.

## User stories

A numbered list, as long as the feature needs and no longer. Each one:
`As a <actor>, I want <feature>, so that <benefit>.` Cover the edge cases the interview raised (empty states,
errors, signed-out, phone vs desktop).

## Implementation decisions

The decisions made, for example:
- the features (vertical slices) built or changed, and new handlers, pages or state containers by role
- schema changes: tables, columns, RLS (DB-01)
- contracts between parts: payloads, Function endpoints, config keys
- architectural choices and the ARCHITECTURE.md rules they lean on

## Testing decisions

- The agreed seams, each with its suite (`shared-unit`, `shared-integration`, bUnit, `e2e`, `db`, …) and verify level.
- Prior art: similar tests that already exist.
- A good test checks behaviour through the public interface, not the implementation.

## Out of scope

What this feature deliberately doesn't do.

## Open questions

Anything still undecided, with who decides it. Empty is fine.

</spec-template>
