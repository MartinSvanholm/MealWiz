---
name: mw-code-review
description: Review a branch's diff against dev on two separate axes, Standards (docs/agents/ARCHITECTURE.md rules, TESTING.md, a code-smell baseline) and Spec (the Linear issue and project spec), and report both side by side. Light by default to save tokens. Use before opening a PR, from /implement-issue, or when asked to review a branch, PR or work in progress.
---

<!-- Adapted from mattpocock/skills `skills/engineering/code-review` (MIT). See `.claude/skills/THIRD_PARTY_NOTICES.md`. -->

# Code review

Two-axis review of the diff between `HEAD` and a fixed point:

- **Standards**: does the code follow this repo's rules?
- **Spec**: does it do what the Linear issue (and the project spec) asked for?

The axes are reported separately, never merged or reranked: code can follow every rule and build the wrong thing, or
build the right thing and break the rules.

## 1. Pin the fixed point

Default `origin/dev` (PRs target `dev`), unless the user names another. `git fetch origin dev` first. Capture
`git diff origin/dev...HEAD` (three dots: against the merge-base), `git diff --stat origin/dev...HEAD`, and
`git log origin/dev..HEAD --oneline`. Confirm the ref resolves and the diff isn't empty before going further.

## 2. Find the spec

1. Issue ids (`MEA-xx`) in the branch name or commit messages → `get_issue`. If the issue is in a project, also read
   the project description (`get_project`) and the Contract document, if any.
2. A path or issue the user passed.
3. If nothing is found, ask. If there's no spec, skip the Spec axis and say so.

## 3. Standards sources

- `docs/agents/ARCHITECTURE.md`: every MUST and SHOULD rule, with its **Check** line. Severity as that file says:
  🔴 broken MUST (fix before merge), 🟡 broken SHOULD (nit), 🟣 pre-existing in untouched code (mention, don't block).
  Report a rule as broken only when you can point at the line.
- `docs/agents/TESTING.md`: the change ships the tests "Which tests for which change" asks for (TEST-02), with the
  right trait and naming.
- `docs/agents/FEATURES.md`: updated if a feature, handler, page or table was added or moved (DOC-01).
- The **smell baseline** below. The repo's rules override it, and each smell is a judgement call, never a hard
  violation. Skip anything the build already enforces.

Smell baseline (Fowler, _Refactoring_ ch. 3), each *what it is* → *fix*:

- **Mysterious Name**: a name that doesn't say what it does or holds → rename it.
- **Duplicated Code**: the same logic shape in more than one hunk or file → extract it.
- **Feature Envy**: a method that uses another object's data more than its own → move it there.
- **Data Clumps**: the same few fields or parameters travelling together → make them one type.
- **Primitive Obsession**: a string or int standing in for a domain concept → give it a type.
- **Repeated Switches**: the same switch on the same type in several places → one map or polymorphism.
- **Shotgun Surgery**: one logical change scattered across many files → gather what changes together.
- **Divergent Change**: one file edited for unrelated reasons → split it.
- **Speculative Generality**: abstraction or parameters the spec doesn't need → delete it.
- **Message Chains**: long `a.B().C().D()` navigation → hide the walk behind one method.
- **Middle Man**: a class that mostly delegates → call the real target.
- **Refused Bequest**: an implementer that ignores most of what it inherits → use composition.

## 4. Review: light by default

The user asked for light token use.

- **Under ~50 changed lines (excluding tests):** review inline yourself, both axes.
- **Otherwise:** one subagent (Agent tool, `model: "sonnet"`), in the foreground. Give it the diff commands and commit
  list, the issue text (and contract items it uses), the paths of the standards files, the smell baseline pasted in,
  and this brief: "Read only the diff, the standards files and the issue; open other files only to confirm a finding.
  Report two sections. **Standards**: each finding as `🔴/🟡/🟣 RULE-ID file:line: what and why`, or a labelled
  smell. **Spec**: (a) asked for but missing or partial, (b) built but not asked for, (c) implemented but looks
  wrong; quote the issue line for each. Under 300 words."
- **On request** ("thorough review", a large feature PR): two subagents in parallel, one per axis, as upstream does.

## 5. Report and act

Present `## Standards` and `## Spec` as returned, then one line: findings per axis and the worst in each.

When called from /implement-issue: fix every 🔴 and every Spec gap (a) and (c) before opening the PR. For (b), remove
it or, if it's needed, say why in the PR. List the 🟡 you chose not to fix, with a reason, in the PR body.
