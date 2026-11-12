# Ship PR template

The body of the one PR from `project/<slug>` to `dev`. The user reviews the whole feature from it, so it explains the
design and names what deserves a close look. Keep every section; write "None" where one is empty. Aim for one to two
screens.

---

```markdown
<What the feature does for the user, 2-4 lines.>

Project: <Linear project link>
Issues: MEA-xx <title> (#<issue PR>), MEA-yy <title> (#<issue PR>)

## Architecture

How it's built, in prose: the feature folder(s) and what each slice does, how data flows from the page through the
state container and the handlers to Supabase (and the Function, if it's involved), and which existing pattern each
part follows. Name the main files. Say why where a choice isn't obvious, with the rule it leans on (ARCH-xx).

<A Mermaid diagram when there's a flow worth drawing: a sequence for a multi-step flow, or the tables and their
relations for a schema change. Leave it out for a simple feature.>

| Area | Added or changed |
|---|---|
| Database | tables, columns, policies, migrations |
| Handlers | commands and queries, by feature |
| State | state containers and their new public members |
| UI | pages, components, routes |
| Function / config | endpoint, payload or config changes, or "none" |

## Critical changes

What a reviewer must not skim, most important first. Each with the file and what could go wrong:

- **Data and migrations:** new or changed tables and what the migration does to existing rows.
- **Security:** each new or changed RLS policy in one line (who may do what); anything near keys or the SSRF guard.
- **Changes to existing behaviour:** anything a user of the current app would notice outside the new feature.
- **Shared code:** edits to handlers, state containers, components or helpers that other features use.
- **Deployment and config:** new settings, secrets, workflow changes, anything the release to `main` needs.

## Screenshots

<Link to the project's "Screenshots" document in Linear.> It shows:
- <page or dialog>: <states captured>, phone and desktop
- …

## Verified

- `verify full: PASSED in …` at <short sha> (this PR's head)
- Checked by hand: <what you clicked through while taking the screenshots>

## Review

Fresh-context review of the whole diff (`mw-code-review`, ship mode).

- **Fixed in this session:** <finding → commit>, or "None"
- **Left as is:** <🟡 finding and the reason>, or "None"
- **User stories:** every story in the spec is implemented and tested | <story n: what's missing and why>

## Decisions and follow-ups

- Decisions the issue sessions and this session made that the spec didn't settle.
- Bugs filed along the way (MEA-xx), and anything deliberately left for later.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
```
