# Grouped assignments and per-set names

**Status:** Awaiting review of this design document  
**Date:** 2026-10-19

## Goal

Let a teacher organize a learning set into named sections, reorder its assignments, and give each assignment a set-specific display name. Replace the student's native assignment `<select>` with an accessible custom menu that shows all assignments and section headings.

## User decisions

- Sections are first-class, ordered groups within a set. An assignment belongs to one section and has its own order in that section.
- A blank custom assignment name uses the assignment's canonical title. A custom name applies throughout the student experience for that set.
- The student menu shows all assignments and section headings. It retains completion status and the current sequential lock: later assignments remain visible but unavailable until earlier assignments are completed.
- Teachers can add and rename sections, reorder sections and assignments, move assignments between sections, set a per-set custom name, and remove assignments. A section can be removed when empty. Empty sections may be saved during setup but are omitted from the student's menu.
- Existing sets migrate to one unnamed section with their current assignment order and no custom names.
- An attempt captures the assignment versions, order, section names, and display names at start. Editing a set does not change an active or historical attempt.
- The standalone game and sandbox stay unchanged. The teacher analytics report currently shows progress totals and does not list assignment titles; if it later lists them, it uses the per-set display name.

## Scope

### Teacher workspace

- Replace flat ordering with ordered section cards.
- A new set starts with one unnamed section. The teacher can add, rename, and reorder sections.
- Each assignment row shows its canonical title and provides an optional custom-name field, within-section move controls, a section selector, and a remove action.
- A blank or whitespace-only custom name is normalized to `null` and displays the canonical title.
- Preserve existing save, publish, archive, QR, and report flows. Archived sets remain read-only.

### Student experience

- Replace the native select in the case game with a custom button and scrollable listbox, styled to the existing dark interface and orange accent (`#F36F29`).
- Show section headings and every assignment in section/order sequence. Each assignment row shows its sequence number, effective display title, and the existing completion marker.
- Keep locked assignments visible and disabled according to the existing case progression rule. Do not change normal game or sandbox level selection.
- Use the effective display title in the selected control, mission heading, and completion dialog. Keep the canonical definition title intact for assignment identity and game data.
- Support pointer selection, closing on outside click or Escape, and keyboard navigation with Arrow keys, Home, End, and Enter. Expose listbox/option semantics and selected/disabled state to assistive technology.

## Data and API design

- Add a `CaseSection` entity with a set foreign key, optional name, and order.
- Extend the existing set-to-assignment association (`CaseLevel`) with a required section foreign key and optional `DisplayTitle`. Its order is the assignment's position within the section. Because `DisplayTitle` belongs to the association, the same canonical assignment can have different names and groups in different sets.
- Derive the overall student sequence by section order and then assignment order. Use that flattened, one-based sequence for menu and operation labels; do not rewrite the canonical `LevelDefinition.Order`.
- Change teacher case create/update payloads and DTOs to carry nested sections and assignments. Validate that the set contains 1–100 assignments, each level version appears only once across all sections, each referenced version belongs to the teacher, section names are at most 120 characters, and custom names are at most 200 characters. Trim both names; blank values become `null`. A set with no assignments cannot be published.
- Validate the full update before replacing saved sections and assignments; persist the new structure as one unit so a rejected update cannot leave a partial set.
- Student case/attempt DTOs include the section title and effective display title separately from the canonical `LevelDefinition`.

## Attempt snapshots and migration

- Keep `LevelVersionIdsJson` as the ordered version list used by evaluation.
- Add a JSON snapshot on `Attempt` containing one presentation entry per pinned version: version ID, section title (nullable), and effective display title. Write the ordered version IDs and presentation snapshot when starting the attempt.
- Resume loads the saved presentation snapshot, so changes to a set affect only attempts started afterward.
- For existing attempts with no presentation snapshot, show the canonical version title with no section heading. Do not infer historical names from the set's current configuration.
- The database migration creates one unnamed section per existing set, assigns its existing assignments to that section, preserves their current order, and leaves custom names empty. The attempt snapshot field defaults to an empty value for old rows.

## Compatibility and behavior boundaries

- Level definitions and immutable version IDs are not changed; custom names and grouping are per set.
- Set updates continue to affect new attempts, while in-progress attempts keep their pinned assignment versions and presentation.
- Existing attempts, completion data, QR links, archive behavior, and report calculations remain intact.
- The regular game catalog and `/sandbox` do not consume set-specific grouping or names.

## Acceptance criteria

1. The teacher can create and edit sections, reorder sections and assignments, move an assignment to another section, rename it for that set, clear that name, and remove an empty section.
2. The same assignment can appear with different section membership and display names in two different sets.
3. Saving and reopening a set preserves its section and assignment order, custom names, and memberships.
4. The student's custom menu lists all assignments under the correct headings, marks completed assignments, and preserves the current lock on future assignments.
5. A custom assignment name appears in the student menu, mission heading, and completion dialog; a blank override falls back to the canonical title.
6. A new attempt reflects the saved set structure at start. Editing the set does not change the names, groups, or assignment versions shown by an existing attempt after refresh.
7. A migrated set keeps its old assignment order in a single unnamed section, with canonical names.
8. Teacher report totals and standalone game/sandbox behavior remain unchanged.

## Review focus

- Section ordering and assignment ordering must produce one deterministic sequence for navigation and operation numbers.
- Unavailable or invalid level versions must not result in a partially updated set.
- A presentation snapshot must align with the same pinned version IDs that evaluation uses.
- A custom display title must never replace the canonical title in the stored level definition.
