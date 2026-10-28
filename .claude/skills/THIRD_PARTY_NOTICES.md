# Third-party notices

The `mw-*` skills in this folder are edited copies of skills from
[mattpocock/skills](https://github.com/mattpocock/skills), taken at commit `59db609eb1e18aaf59b63284d0e52255aff0c63c`.
They were adapted to MealWiz: Linear instead of GitHub issues, `docs/agents/` as the standards, `scripts/verify.*` as
the test command, and PRs to `dev`. They are model-invocable, so the MealWiz flow skills can call them.

| Copy | Upstream |
|---|---|
| `mw-grilling/` | `skills/productivity/grilling/` |
| `mw-to-spec/` | `skills/engineering/to-spec/` |
| `mw-to-tickets/` | `skills/engineering/to-tickets/` (with ideas from `skills/engineering/implement-spec/`) |
| `mw-tdd/` (with `tests.md`, `mocking.md`) | `skills/engineering/tdd/` |
| `mw-code-review/` | `skills/engineering/code-review/` |
| `mw-diagnosing-bugs/` | `skills/engineering/diagnosing-bugs/` |
| `mw-research/` | `skills/engineering/research/` |

When you change one, keep its attribution line. To pull an upstream change, diff the upstream file at a newer commit
against the commit above and apply what still fits.

## License

```
MIT License

Copyright (c) 2026 Matt Pocock

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```
