# Implementation plan template

The coordinator writes a plan for an issue **just before that issue starts**, against the project branch as it is
then, and appends it to the issue's description under `## Implementation plan`. The issue's session runs on Sonnet at
medium effort and executes it.

**The bar:** the session makes no product or architecture decision. The plan settles what the user and the next
issue can see: behaviour, data shapes, public members, routes, and which existing code to copy. It leaves the rest
to the session: private names, test names and test data, method bodies, the exact wording of UI text the spec
doesn't fix. Keep the plan to about one screen per three stories. A line the session could work out by reading the
prior art doesn't belong here.

Delete sections that don't apply.

---

```markdown
## Implementation plan

Written <date> against `project/<slug>` @ <short sha>. Follow it story by story and don't re-plan. If a step doesn't
fit the code, stop and ask (implement-issue, stop rule 8).

### Decisions
What the session would otherwise have to ask, one line each: names the user or other code sees (S-01), behaviour in
empty and error states, edge cases, which existing pattern to follow. Include the spec's decisions this issue
implements.

### Shapes
Only the public shapes, so the next issue and the tests can rely on them:
- **Migration:** tables and columns with types, nullability, defaults and FKs; each RLS policy in one line (who may
  do what).
- **Handlers:** per handler (ARCH-04), its request and result type, and the failures it returns.
- **State container:** each new or changed public member, one line of behaviour each.
- **Pages and components:** route, the component to copy, and the `data-testid`s the E2E journey uses (UI-01).

### Stories
In order. Each story is one or more red → green slices (`mw-tdd`) and at least one commit, named `<ID> story <n>: …`.
Inside a story, work database → handlers → UI.

1. **Story <n>: <one line>**
   - Build: <what to add or change, by layer, pointing at Shapes>.
   - Tests: <what must be proven, by suite: "pgTAP: a non-owner can't read the row", "integration: the handler
     returns … when …", "bUnit: the page shows the empty state">.
   - Prior art: `<existing file to copy>`.
2. **Story <n>: …**

E2E (last, if the spec names a journey): the journey class and each scenario as numbered user actions and
assertions.

### Docs
The sections of `docs/agents/FEATURES.md` (and TESTING.md, CLAUDE.md) that need a line (DOC-01).

### Verify
`scripts/verify.ps1 fast` (exit 0). <Plus, only if the issue has a migration, an RLS change or new integration
tests: `scripts/verify.ps1 local --only <steps>` for those steps.> CI runs the rest on the PR; the ship session runs
`verify full` on the whole project.

### Watch out
Gotchas found while planning, e.g. "swapping a meal updates the `meal_plans-meals` row in place, so its id stays the
same".
```
