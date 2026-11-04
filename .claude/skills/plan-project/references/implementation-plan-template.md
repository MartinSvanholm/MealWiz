# Implementation plan template

The coordinator writes one plan per issue and appends it to the issue's description under `## Implementation plan`.
The issue's session runs on Sonnet at medium effort and **executes** the plan; it doesn't plan, explore for
alternatives or ask about anything the plan settles. So the plan is the design.

**The bar:** the session makes no design decision. Every name, signature, column, policy, data-testid, UI string and
test case is written down. Leave out method bodies unless the logic is subtle (a merge rule, a date window, a
generator); then give pseudo-code or the exact rule. Paths and code shapes are fine here, unlike the rest of the issue:
the plan is used within days and is revised when its blockers merge.

Sub-issues get no plan of their own. Each sub-issue's description gets one line under its acceptance criteria:
`Plan: steps <n>–<m> of the parent's Implementation plan (<parent ID>).`

Delete sections that don't apply.

---

```markdown
## Implementation plan

Written <date> against `dev` @ <short sha>. Approved by <user> in the coordinator. Follow it step by step and don't
re-plan. If a step doesn't fit the code, stop and ask (implement-issue, stop rule 8).
<Revised <date> after <ID> merged: what changed.>

### Decisions
Everything the session might otherwise ask, one line each: names (S-01), UI text, empty and error states, edge-case
behaviour, which existing pattern to follow. Include decisions from the spec that this issue implements.

### Files
| File | New / Edit | What changes |
|---|---|---|
| `supabase/migrations/<YYYYMMDDHHMMSS>_<name>.sql` | New | table `x`, RLS, … |
| `Features/<F>/<Op>/<Op>.cs` | New | handler (see Shapes) |
| `docs/agents/FEATURES.md` | Edit | <section>: <rows> |

Shared files (owned edits only): `<file>`: <the exact edit>, because <sibling> edits the rest.

### Shapes
The exact contracts the steps build, so nothing is designed on the fly:
- **Migration:** columns with types, nullability, defaults, FKs with `on delete`, unique constraints and indexes; each
  RLS policy (command, `using`, `with check`); grants.
- **Handlers:** per handler (ARCH-04), its `public record Command(...)` or `Query(...)` with the
  `IRequest<Result…>` it implements, what it reads and writes in Supabase, each failure it returns and its message.
- **Models:** domain and Db properties, the mapping (ARCH-07).
- **State container:** each new or changed public member, one line of behaviour each, and when it notifies.
- **Components and pages:** route, parameters, the elements with their `data-testid`s (UI-01) and texts, which
  existing component to copy, the theme tokens to use (UI-02).

### Steps
In order. Each step is one red → green TDD slice (`mw-tdd`) and one commit.

1. **<slice>** (`[DB]` <sub-issue ID>)
   - Red: `<TestClass>.<Test_name>` in `<path>`: <what it asserts>. <More tests, one line each.>
   - Green: <what to write, pointing at Shapes>.
   - Prior art: `<existing file to copy>`.
2. **<slice>** (`[BE]` <ID>)
   - …

E2E (last step, if any): the journey class and each scenario as numbered user actions and assertions, with the
data-testids it uses and the seeded data it relies on.

### Sub-issues
- Order and mode for each: `[DB]` MEA-x inline → `[BE]` MEA-y inline → `[FE]` MEA-z in a subagent (owns `<paths>`,
  codes against <seam>).

### Docs
- `docs/agents/FEATURES.md`, section <name>: <the lines to add or change>.
- `docs/agents/TESTING.md` / `CLAUDE.md`: <if anything>.

### Verify
`scripts/verify.ps1 local --e2e <Class>` (exit 0).

### Watch out
Gotchas found while planning, e.g. "swapping a meal updates the `meal_plans-meals` row in place, so its id stays the
same".
```
