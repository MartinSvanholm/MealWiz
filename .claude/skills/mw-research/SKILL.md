---
name: mw-research
description: Research a question against primary sources (official docs, source code, specs) in a background agent and save the cited findings as a Linear document on the project. Use when a feature needs docs or API facts gathered (MudBlazor, Supabase, Blazor, Azure Functions, a recipe site format), from /plan-project, or when asked to research something.
---

<!-- Adapted from mattpocock/skills `skills/engineering/research` (MIT). See `.claude/skills/THIRD_PARTY_NOTICES.md`. -->

# Research

Run the research in a **background agent** (Agent tool, `run_in_background: true`), so you keep working while it
reads. Give it the questions, the project id, and this job:

1. Investigate each question against **primary sources**: official docs, the library's source code, specs, the
   first-party API. Not a blog post about them. Follow every claim back to the source that owns it.
2. Check what the repo already pins: package versions in the `.csproj` files (MudBlazor 8, MediatR 13, FluentResults 4,
   the Supabase C# SDK), the Supabase CLI version in `.github/workflows/CI.yml`. Research the version in use, not the
   latest.
3. Write the findings as Markdown: one section per question, the answer first, then the evidence, each claim with its
   source link. End with "Gotchas" (things that would surprise an implementer) and "Not found" (questions left open).
4. Save it on the project as a Linear document titled **Research** (`save_document` with `project:`). If one exists,
   append a dated section with `patch` (`append`) rather than overwriting it.
5. Report back in under 150 words: the document link and the three findings that most affect the plan.

Codebase facts (where code lives, how a handler works) are not research: get them from `docs/agents/` and an
Explore subagent. Research is for things outside the repo.

Without a project (a one-off question), put the findings in your reply instead, still with sources.
