# Markdown pages in learning sets

**Status:** Awaiting review of this design document
**Date:** 2026-10-20

## Goal

Let teachers author rich Markdown learning pages in the teacher cabinet and combine them with coding assignments as ordered steps in a learning set. Give teachers predictable progression controls and keep published content and existing student attempts stable when authors edit a page later.

## User decisions

- A Markdown page is a distinct step that can be placed among coding assignments in a set.
- Each set chooses one progression mode: steps open sequentially, or all steps are open immediately.
- A student completes a Markdown step explicitly with a **Continue** button. There is no automatic advance and no separate acknowledgement checkbox.
- The teacher editor uses Markdown source beside a live preview, with a formatting/insertion toolbar.
- Supported syntax is CommonMark and GitHub Flavored Markdown, plus math and Mermaid diagrams. HTML is sanitized before display; scripts and unsafe markup are not allowed.
- Markdown pages are reusable library entries owned by a teacher and have immutable versions. A page edit creates a new version. A set continues using the version it references until the teacher changes that set; attempts already started retain their original snapshot.
- The built-in demo page is visible only to the superadmin, has local images, can be added to the superadmin's test sets, and cannot be edited or deleted.
- The requested change applies to the teacher and superadmin workspaces. A superadmin's own pages belong to the superadmin's teaching account. When managing another teacher's set, the superadmin can select that owner's pages and does not change ownership.

## Scope

### Markdown page library and editor

- Add a Markdown pages area in the teacher cabinet with list, create, and edit views. Include the same area for the superadmin's own teacher account, plus an owner-scoped page selector while editing another teacher's set.
- The page has a title and Markdown body. Editing saves a new immutable version; existing page versions stay available while any set or attempt refers to them. A page may be archived only after it is no longer referenced by a set; archival is reversible and never deletes versions or assets.
- Provide source and preview panes, a toolbar for common formatting, tables, checklists, links, images, code blocks, formulas, and Mermaid blocks, plus image insertion by upload. Changes to source update preview without saving.
- Limit Markdown source to 100 KiB and each uploaded image to 10 MiB. Uploaded images are application-managed PNG, JPEG, or WebP assets. The API validates owner, detected image format, and size; SVG, executable content, and arbitrary file downloads are not accepted. Keep assets while referenced by a page version. The student view receives image access only through an active published set share link that includes the page.
- Render Markdown consistently for preview and student view. Permit only a safe subset of raw HTML and sanitize rendered output as defense in depth. Permit safe HTTP/HTTPS and application asset URLs; block script/data URLs, event handlers, and other unsafe navigation or embedding.
- Math and Mermaid are rendered as inert content first, then enhanced by the client renderers. Markdown must never inject executable scripts or inline event attributes.

### Learning-set authoring

- Keep ordered named sections. Replace the section's level-only list with a single ordered list of content items: coding assignment version or Markdown page version. Each item belongs to exactly one section and has one order within it.
- A set contains 1–100 total content items. Teachers can add/remove a page, reorder it alongside assignments, and move it between sections. A level version can occur at most once per set, preserving current assignment behavior. A page version may appear more than once as separate ordered items. Existing assignment custom names and section behavior remain available.
- Set creation and editing expose `Sequential` and `AllOpen` modes. Existing sets migrate to `Sequential`, preserving the current student lock behavior.
- Publishing requires at least one content item. Archived sets remain read-only. A set update is validated in full before its saved contents are replaced.
- In the superadmin case editor, available pages are filtered by the selected set owner. The superadmin can also use their own pages in sets they own.

### Student experience and progress

- The student menu shows every coding and Markdown step in section/order sequence with one-based item numbering, completion state, and lock state.
- In `Sequential` mode, later steps remain visible but disabled until the immediately preceding step is complete. In `AllOpen` mode, every step is selectable; completion still contributes to progress.
- A Markdown step displays its pinned title and rendered body. Clicking **Continue** marks that step complete and selects the next step when one exists. If it is the last step, the student stays on the page and sees its completed state.
- The continue action is idempotent and server-authoritative. It cannot complete a page that is not in that attempt or is locked in sequential mode.
- Progress totals and case reports count completed content steps, including Markdown pages. Existing level evaluation and completion rules remain the way coding steps are completed.
- A student attempt snapshots item order, section headings, effective titles, selected immutable versions, progression mode, and Markdown page content at start. Editing a set or page cannot alter an in-progress or historical attempt.
- Existing attempts without Markdown snapshots remain readable and keep their current level order and completion totals.

### Built-in superadmin demo

- Seed one immutable demo page into the superadmin's page library, with authored sample content exercising headings, emphasis, lists, a table, a checklist, code, a formula, a Mermaid diagram, and local images.
- Mark the page/version as built-in so the UI cannot edit or archive it. It remains available only to the superadmin and can be inserted in the superadmin's own test sets.
- Use image assets shipped with the application for the demo so the page works without external network access.

## Data and API design

- Add `MarkdownPage` as an owner-scoped catalog entry and `MarkdownPageVersion` as an immutable snapshot containing title, Markdown source, version number, and creation time. Set items reference a specific `MarkdownPageVersion`.
- Add application-managed `MarkdownAsset` records with owner, safe detected content type, length, bytes, and creation time. Markdown source stores a stable asset reference; API rendering converts it into an authorized preview or case-scoped student URL.
- Generalize a section's assignment association to a `CaseContentItem` (or equivalent) with `CaseId`, `SectionId`, `Order`, `Kind`, nullable level-version reference, nullable Markdown-page-version reference, and optional per-item display-title override. A database constraint enforces exactly one reference matching `Kind`; level-version references remain unique per set. Each occurrence of a page version has its own item identity and order.
- Add `ProgressionMode` to `TeacherCase`, defaulting to `Sequential`. Preserve the current `orderedLevels` case type or provide a backward-compatible API mapping while the set now contains mixed content items.
- Teacher API endpoints manage only the current teacher's page library and validate that each uploaded asset and selected page version belongs to the set owner. Platform endpoints require the superadmin role, active MFA, antiforgery validation, and audit events for cross-owner actions.
- Extend student case/attempt contracts with a discriminated content-step type (`level` or `markdown`), effective title, section presentation, order, lock mode, and the content needed to render that immutable step. Add a student endpoint to mark a Markdown step complete using the case share token and continuation token.
- Add per-attempt step snapshots and completion records keyed by attempt step identity, so repeated references and mixed step order are represented without conflating page IDs with level-version IDs. Existing `AttemptLevelResult` rows remain readable for legacy attempts; new evaluation writes completion to the corresponding attempt step.
- Preserve report API semantics by returning total completed-step count. Existing reports continue to aggregate attempts by participant and do not expose Markdown source.

## Migration and compatibility

- Create page, page-version, asset, and mixed-case-item tables; add progression mode; and add attempt step snapshot/completion storage. After the bootstrap superadmin account is available, an idempotent application seed creates the built-in demo page and its local image assets if they are missing.
- Migrate each existing `CaseLevel` into a `CaseContentItem` in its current section and order, retaining its level-version ID and custom title. Existing sets become `Sequential`.
- Keep old attempt data intact. For historical attempts with no generalized step snapshot, derive their level steps from `LevelVersionIdsJson` and presentation from `LevelPresentationJson`; do not infer historical page data from the set's current state.
- Preserve QR links, participant continuation tokens, archive/restore, ownership, existing level definitions, and standalone game/sandbox behavior.

## Acceptance criteria

1. A teacher can create, edit, and version Markdown pages; reopen them; and see the saved source and rendered preview.
2. The editor supports CommonMark/GFM, tables, task lists, fenced code, math, Mermaid, images, and a safe sanitized HTML subset. A script, event handler, or unsafe URL cannot execute in preview or student view.
3. An uploaded image persists across application/container restarts and renders for an authorized preview and in an active published set; another teacher cannot use the asset by guessing its ID.
4. A teacher can insert page versions among assignments, move them within and between sections, and save/reopen the resulting order.
5. Existing cases migrate with their section and assignment order, custom names, and sequential lock unchanged.
6. Sequential mode locks later items until completion; all-open mode allows selection of any item. The selected mode persists for each set.
7. A Markdown step is completed only after the student presses Continue. In sequential mode this enables the next item; the action remains completed after refresh or resume.
8. Progress and reports count completed Markdown steps and coding assignments consistently. Repeated submissions do not inflate progress.
9. Editing a Markdown page creates a new version without changing sets that use an older one. Updating a set affects only new attempts; active attempts retain their original order, titles, mode, and page content.
10. The superadmin can add the immutable local-image demo page to an owned test set; teachers do not see it and cannot edit or delete it. A page version can appear multiple times in a set as distinct steps.
11. The superadmin can choose owner-scoped pages while editing any teacher's set without changing the set or page owner.
12. Existing level evaluation, standalone game, sandbox, QR entry, attempt continuation, and archive flows remain functional.

## Review focus

- The database must enforce valid item kind/reference pairs and ownership checks must happen before any case update is applied.
- Attempt snapshots must cover the complete step sequence, including page source/version and access mode, and agree with evaluation's pinned level versions.
- Completing a Markdown page must be serialized/idempotent and obey sequential progression even if the request is repeated or sent concurrently.
- Image URLs must be valid in teacher preview and student sessions without making unreferenced/private uploads public.
- The Markdown renderer must produce the same visible syntax in React preview and Blazor student UI and must not interpret raw author HTML as trusted code.
- The demo seed must be stable across repeated startup/migration and must not create duplicate records or overwrite a user's content.
