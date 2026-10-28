# Duplicate ADR Numbers

Parallel branches can each pick the same "next" number; after both merge, two ADRs share it.

## Detect

All three views must agree:

1. **Frontmatter titles** (source of truth): grep `^title: ADR` across the ADR directory; look for
   a repeated `NNN`. Duplicate `weight:` values (if used) are the same bug.
2. **Index**: `ADR NNN` entries form a gap-free ascending sequence, no repeats, every link target
   exists.
3. **Filenames**: `adr-NNN-*.md` prefixes match frontmatter. A long-standing mismatch is not a new
   collision — leave the published filename alone unless asked (renaming changes its URL), but
   note it.

## Resolve

The ADR merged to the default branch **first** keeps the number (check `git log --follow` or merge
dates; if ambiguous, keep it on the ADR other documents already reference). Renumber the other to
highest existing + 1 (don't fill gaps). In one commit:

1. Rename `adr-NNN-slug.md` → `adr-MMM-slug.md`; update `title:` and `weight:`.
2. Fix the index entry's title and URL; move it into numeric order.
3. Grep the **whole repo** for `adr-NNN-slug` and `ADR NNN` / `ADR-NNN`; update hits that refer to
   the renumbered ADR (other ADRs' Superseded links, docs, plans, READMEs, agent files, code
   comments). Confirm each prose hit isn't about the same-numbered sibling.
4. Re-run detection and grep for the old slug. Merged PR/issue text can't be fixed — that's fine.
