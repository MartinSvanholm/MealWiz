---
name: mw-grilling
description: Interview the user in rounds of numbered questions, each with a recommended answer, until every decision in a plan, feature or idea is settled. Use when the user wants to be interviewed or grilled, when a spec or plan has open decisions, and from /spec-project and /plan-project.
---

<!-- Adapted from mattpocock/skills `skills/productivity/grilling` (MIT). See `.claude/skills/THIRD_PARTY_NOTICES.md`. -->

# Grilling

Interview the user relentlessly until you reach a shared understanding. Map this as a **design tree**: every decision
branches into the decisions that hang off it.

Work the tree in **rounds**. The **frontier** is every decision whose prerequisites are already settled: the questions
you can ask _now_ without guessing at answers you haven't heard yet. Ask the whole frontier in one round, then wait for
the answers before the next round.

## Asking a round

Ask with **AskUserQuestion**. It takes up to 4 questions per call, so a bigger frontier goes in several calls in the
same round.

- Number questions across the whole interview (`Q1`, `Q2`, …), so answers can be referred to later.
- Put your recommended answer first, with `(Recommended)` at the end of its label. Its description says what it means
  and why you recommend it.
- Give 2–4 real options. The user can always pick "Other" and write their own.
- Word each question so picking the recommendation is the easy path, but make the trade-off visible.

If AskUserQuestion isn't available, write the round as text:

```
❓ **Q1** - **<question title>**: <question body, might be multiple paragraphs, including multiple choices>

➡️ <your recommended answer>
```

## Between rounds

Each answer reshapes the tree: settled decisions push the frontier outward and unblock the questions that depended on
them. Recompute the frontier and ask the next round. A question whose answer depends on another question still open
in this round belongs to a _later_ round.

Read the answers carefully. A free-text answer may change a question, add a constraint, or ask for something lighter
or different from every option you offered. Follow what it actually says.

## Facts versus decisions

Finding _facts_ is your job, never the user's. When a question needs a fact from the code, Linear, git or the docs,
look it up yourself, or send an **Explore** subagent for anything wider than a few reads. Start with
`docs/agents/FEATURES.md` (where things live), `docs/agents/ARCHITECTURE.md` (the rules) and
`docs/agents/TESTING.md` (the suites). Don't block on a running exploration: only the questions downstream of it
wait; ask the rest of the frontier now. The _decisions_ are the user's: put each to them and wait.

## Done

The interview is done when the frontier is empty: every branch visited, nothing silently assumed. Summarise the
decisions as a numbered list (`Q1 → …`). Don't act on them until the user confirms you've reached a shared
understanding, unless the calling skill says what happens next.
