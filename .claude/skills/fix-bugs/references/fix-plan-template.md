# Fix plan template

The `/fix-bugs` coordinator writes one fix plan per bug that needs one and appends it to the bug's description under
`## Fix plan`. The bug's session runs on Sonnet at medium effort and **executes** it: it writes the red test, checks
that it fails for the stated reason, applies the fix and doesn't diagnose again. So the plan carries the diagnosis.

**The bar:** the session makes no diagnostic or design decision. The root cause points at real code, the red test is
named and says what it asserts, and the fix says exactly what changes. Give the code shape when the change is subtle;
leave out what is mechanical.

Delete sections that don't apply.

---

```markdown
## Fix plan

Written <date> against `dev` @ <short sha>. Approved by <user> in the coordinator. Follow it and don't re-diagnose.
If the red test doesn't fail as described, or the fix doesn't fit the code, stop and ask (implement-issue, stop rule 8).

### Root cause
What goes wrong and why, pointing at the code: `<path>:<line>` <what it does>, so <the symptom in the issue>.
How sure: confirmed by reading <the code path> | likely, and the red test confirms it.

### Red test
- `<TestClass>.<Test_name>` in `<path>` (<suite>): <setup>, <action>, asserts <the expected behaviour>. Today it fails
  because <reason>.
- Or: flip the pinning assertion in `<path>` (`<test name>`), listed in TESTING.md "Bugs currently pinned by tests".

### Fix
- `<path>`: <the exact change>. <Code shape if subtle.>
- Other call sites with the same flaw: <paths>, or "none (checked <how>)".

### Cleared by the user
The flagged parts (implement-issue B2) the user approved before the start, quoting the decision: the migration and
what it does to existing rows, the new signature, the visible behaviour change. Omit if nothing is flagged.

### Docs
- `docs/agents/TESTING.md` "Bugs currently pinned by tests": <the row to remove>. `docs/agents/FEATURES.md`: <lines>.

### Verify
`scripts/verify.ps1 fast` (exit 0)<, plus `local --only <step>` or `local --e2e <Class>` when the red test isn't a
unit or bUnit test>. CI runs the rest.
```
