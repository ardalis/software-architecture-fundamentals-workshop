---
name: adr-writing
description: Write Architecture Decision Records (ADRs) for a project's documentation. Covers file naming, frontmatter, required sections, status lifecycle, index maintenance, and detecting/fixing duplicate ADR numbers from parallel branches. Load before creating, revising, or superseding any ADR, or when asked to check for duplicate ADR numbers or fix the ADR list.
invocable: false
---

# Writing Architecture Decision Records

Files (read only when needed):

- [template.md](template.md) — blank ADR to copy
- [examples/full-example-adr.md](examples/full-example-adr.md) — complete example; read when
  writing a full ADR to match style and Options Considered format
- [examples/superseded-example-adr.md](examples/superseded-example-adr.md) — Superseded status line
- [examples/example-index.md](examples/example-index.md) — sample index
- [references/duplicate-numbers.md](references/duplicate-numbers.md) — detecting and fixing
  duplicate ADR numbers

## Locate ADRs

Glob for `**/adr-*.md` or `adr`/`adrs` directories (e.g. `docs/adr/`, `devdocs/adrs/`). There's
usually an index (`_index.md`, `index.md`, `README.md`). **Existing repo conventions win** — check
for a template there or read 2–3 recent ADRs and match their frontmatter, sections, and numbering.
No ADRs yet → create `devdocs/adrs/` with an index and start at 001.

## Create an ADR

1. **Number**: highest number in the **index titles** + 1 (not filenames — they can drift). Run the
   duplicate check in [references/duplicate-numbers.md](references/duplicate-numbers.md), and check
   open PRs (`gh pr list --search adr`) so you don't reuse an in-flight number.
2. **File**: `<adr-dir>/adr-NNN-short-slug.md`, `NNN` zero-padded and matching the title. Copy
   [template.md](template.md).
3. **Frontmatter**: `title: ADR NNN - <Decision in Title Case>` (name the decision, not the
   problem), `weight: N` (unpadded) — unless the repo differs. Body metadata is only
   `- **Date:** YYYY-MM-DD` and `- **Status:** Proposed | Active | Superseded` (Superseded links to
   its replacement). Don't invent other fields.
4. **Index**: add an entry in numeric order using the existing link form, e.g.
   `- [ADR 042 - Outbox Pattern ...](/architecture/adrs/adr-042-outbox-event-publishing/)`.

## Sections (`##`, in order)

Context (required) · Problem Statement (1–3 sentence question) · Decision (required) · Options
Considered (required if real alternatives) · Rationale · Consequences (required; `### Positive` /
`### Negative`) · Open Issues · Future Considerations · Related Links. Trivially small ADRs may
omit optional sections.

**Options Considered**: each `### Option N: <name>` with a short description, then either
`**Rejected because:**` with concrete, codebase-verified reasons (no strawmen), or `(chosen)` in
the heading.

## Style

- Ground claims in the actual code: real classes, files, packages; audit before asserting.
- Link the driving issue/PR and any plan or design doc.
- Short code snippets when the decision is about code shape.
- Honest negatives, each with mitigation if one exists.
- Wrap ~100 chars (or match repo). Present tense for the decision, past tense for investigation.

## Supersede

Write the new ADR normally; reference the old one in its Context and why it no longer holds. Set
the old ADR's status to a Superseded link (see
[examples/superseded-example-adr.md](examples/superseded-example-adr.md)). Never delete or
renumber ADRs — except to resolve a duplicate number.
