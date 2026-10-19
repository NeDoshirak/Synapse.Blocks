# Grouped Assignments and Per-Set Names Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let teachers order assignments in named sections and customize assignment names per set, then show that structure in the student case menu and attempt.

**Architecture:** Store ordered sections on each teacher set and store section membership plus the optional display-name override on each set-to-version association. Snapshot the flattened versions and their section/display names when an attempt starts; pass this metadata through the API to a student-only custom menu. Keep canonical level definitions and the standalone game unchanged.

**Tech Stack:** ASP.NET Core, EF Core, PostgreSQL, React, TypeScript, Ant Design, Blazor WebAssembly, Razor component CSS.

**Spec:** `docs/superpowers/specs/2026-10-19-case-groups-and-custom-level-names-design.md`

## Global Constraints

- Work in the current branch and do not stage or commit unrelated existing changes.
- Do not add or run tests, builds, or other verification commands; the user will inspect the result.
- Each set must contain 1–100 unique level versions; a level version belongs to only one section in that set.
- Section names are at most 120 characters; custom assignment names are at most 200 characters. Trim both; blank values become `null`.
- Student ordering is section order, then assignment order. Use the flattened, one-based position for student menu and operation labels without changing `LevelDefinition.Order`.
- Keep the canonical level title and definition unchanged; store custom names on the set-to-assignment association.
- An attempt uses the versions and presentation metadata captured at start. Older attempts with no presentation metadata use canonical titles and no section headings.
- Only case mode uses the custom menu. Keep normal game and sandbox level selection unchanged.
- Apply custom titles in the selected menu control, mission heading, and completion dialog. Empty sections can be saved but do not appear in the student's menu.
- The existing teacher analytics report contains progress totals only; preserve its calculations and use set-specific names if assignment names are added to it later.

## Review Focus

- **Duplicate or unavailable level versions:** The case update path must reject duplicates across sections and versions not owned by the teacher before changing saved data. Handle in Task 1.
- **Legacy attempts with no presentation JSON:** Resume with canonical titles and no headings while keeping their pinned version order. Handle in Task 2.
- **Editing a set during an active attempt:** The resumed attempt must use its captured section/title metadata and versions. Handle in Task 2.
- **Locked and completed student rows:** The custom menu must preserve completed markers and prevent selecting a later incomplete assignment. Handle in Task 4.
- **Regular game and sandbox:** The new picker and aliases must not change either mode. Handle in Task 4.

---

### Task 1: Persist ordered sections and expose the nested case API

**Files:**
- Create: `backend/Synapse.Blocks.Api/Data/Entities/CaseSection.cs`
- Modify: `backend/Synapse.Blocks.Api/Data/Entities/CaseLevel.cs`
- Modify: `backend/Synapse.Blocks.Api/Data/Entities/TeacherCase.cs`
- Modify: `backend/Synapse.Blocks.Api/Data/Entities/Attempt.cs`
- Modify: `backend/Synapse.Blocks.Api/Data/AppDbContext.cs`
- Create: `backend/Synapse.Blocks.Api/Data/Migrations/20261019000000_CaseSectionsAndAttemptPresentation.cs` and its generated designer
- Modify: `backend/Synapse.Blocks.Api/Data/Migrations/AppDbContextModelSnapshot.cs`
- Modify: `backend/Synapse.Blocks.Api/Contracts/Cases/CaseDtos.cs`
- Modify: `backend/Synapse.Blocks.Api/Contracts/Students/StudentDtos.cs`
- Modify: `backend/Synapse.Blocks.Api/Cases/CaseService.cs`
- Modify: `backend/Synapse.Blocks.Api/Cases/ShareLinkService.cs`

**Interfaces:**
- Produce `CaseSection` with `Id`, `CaseId`, nullable `Title` (maximum 120 characters), `Order`, and its ordered `Levels`.
- Extend `CaseLevel` with required `SectionId`/`Section` and nullable `DisplayTitle`; `Order` becomes the position within its section.
- Use request records `CaseSectionInput(string? Title, IReadOnlyList<CaseLevelInput> Levels)` and `CaseLevelInput(Guid LevelVersionId, string? DisplayTitle)`. `CreateCaseRequest` and `UpdateCaseRequest` carry `IReadOnlyList<CaseSectionInput> Sections` instead of `OrderedLevelVersionIds`.
- Return `CaseSectionDto(Guid Id, string? Title, int Order, IReadOnlyList<CaseLevelDto> Levels)` and `CaseLevelDto(Guid LevelId, Guid LevelVersionId, int Order, string Title, string? DisplayTitle)` inside `TeacherCaseDto.Sections`; `TeacherCaseDto` retains its existing set fields, replaces flat `Levels` with `Sections`, and adds `LevelCount` for existing list/report counts.
- Use `StudentCaseLevelDto(Guid LevelVersionId, int Order, int? SectionOrder, string? SectionTitle, string DisplayTitle, LevelDefinition Definition)`, where student `Order` is one-based across the flattened set, `SectionOrder` preserves group boundaries when headings repeat, and `Definition.Title` remains canonical.

- [x] **Step 1: Add the section entity and relationships.** Add `TeacherCase.Sections`; add `CaseLevel.SectionId`, navigation, and `DisplayTitle`; configure section order and per-section assignment order in `AppDbContext`.
- [x] **Step 2: Add the attempt presentation column to the model.** Add required JSONB `Attempt.LevelPresentationJson` with an empty-array default so existing rows can be read before they have a snapshot.
- [x] **Step 3: Add one EF migration.** Create the sections table, backfill one unnamed section per existing case, assign existing case levels while retaining their order, make `SectionId` required, update the order index, and add `LevelPresentationJson` defaulted to `[]`.
- [x] **Step 4: Replace flat case contracts with nested section contracts.** Preserve canonical level titles in teacher DTOs; expose `LevelCount` so the teacher list and report do not depend on the former flat DTO list.
- [x] **Step 5: Update `CaseService` create, read, update, and publish paths.** Include nested sections/versions in list and owned-case queries; flatten section arrays in deterministic order; validate 1–100 unique teacher-owned versions and the approved text limits; normalize blank section/display names to `null`; replace old level associations/sections with the complete validated structure in one save; and check publishability using the total assignment count.
- [x] **Step 6: Map student case DTOs from the current saved structure.** Set each row's global one-based order from section/assignment order and its effective display name from `DisplayTitle ?? LevelVersion.Title`; keep the deserialized definition unchanged.

### Task 2: Snapshot presentation metadata in student attempts

**Files:**
- Modify: `backend/Synapse.Blocks.Api/Students/AttemptService.cs`
- Modify: `backend/Synapse.Blocks.Api/Contracts/Students/StudentDtos.cs`
- Modify: `frontend/student/Models/StudentCaseDtos.cs`
- Modify: `frontend/student/Models/StudentAttemptDtos.cs`
- Modify: `frontend/student/Serialization/AppJsonSerializerContext.cs` as needed for the updated DTO shape

**Interfaces:**
- Serialize `Attempt.LevelPresentationJson` as an ordered list of `{ levelVersionId, sectionTitle, displayTitle }` entries aligned with `LevelVersionIdsJson`.
- `AttemptDto.Levels` and the student `StudentCaseLevelDto` carry `LevelVersionId`, global `Order`, nullable `SectionTitle`, effective `DisplayTitle`, and canonical `Definition`.
- Existing attempts with `LevelPresentationJson == "[]"` produce null section titles and canonical display titles.

- [x] **Step 1: Capture the saved section layout in `StartAttemptAsync`.** Load each case level with its section and pinned version; persist ordered version IDs and the matching effective presentation entries on the new attempt.
- [x] **Step 2: Build resumed attempt levels from the saved snapshot.** In `ToDtoAsync`, pair snapshot entries to the pinned version IDs by version ID and order, while retaining the existing hidden-test filtering.
- [x] **Step 3: Add legacy fallback.** When an attempt snapshot is missing or empty, return canonical titles and null section titles without reading the case's current layout.
- [x] **Step 4: Update Blazor DTO records and source-generated JSON registrations.** Keep field names/types aligned with the API contract.

### Task 3: Add section and assignment editing to the teacher cabinet

**Files:**
- Create: `frontend/teacher/src/components/cases/CaseSectionsEditor.tsx`
- Modify: `frontend/teacher/src/resources/caseOrder.ts`
- Modify: `frontend/teacher/src/pages/cases/edit.tsx`
- Modify: `frontend/teacher/src/pages/cases/list.tsx`
- Modify: `frontend/teacher/src/types/api.ts`

**Interfaces:**
- Define `CaseSectionDraft { clientId: string; title: string; levels: CaseLevelDraft[] }` and `CaseLevelDraft { levelVersionId: string; displayTitle: string }` for local form state.
- `CaseSectionsEditor` receives available `TeacherLevel[]`, `sections`, `disabled`, and `onChange`; it emits section and level order as represented in the arrays.
- Save sends `{ caseType: "orderedLevels", title, description, sections: [{ title, levels: [{ levelVersionId, displayTitle }] }] }`; blank names are sent as `null`.
- `TeacherCase` uses nested `sections` and `levelCount` from the API.

- [x] **Step 1: Add nested teacher API types.** Define section, assignment, request, and response types in `types/api.ts` matching Task 1.
- [x] **Step 2: Add pure ordering/membership helpers.** Extend `caseOrder.ts` to add/remove sections, add/remove unique versions, move sections, move assignments, and transfer an assignment to another section while preserving per-assignment `displayTitle`.
- [x] **Step 3: Build `CaseSectionsEditor`.** Render ordered section cards with a title input (`maxLength=120`) and up/down/delete-empty controls; within each section provide an add-level selector and assignment rows with canonical title, custom-name input (`maxLength=200`), within-section arrows, destination-section selector, and remove action.
- [x] **Step 4: Replace the flat editor in `edit.tsx`.** Initialize a new case with one unnamed section; hydrate existing nested sections; preserve current save/publish/archive rules and disable content edits for archived cases.
- [x] **Step 5: Serialize the nested request and hydrate from the saved response.** Normalize blank names to `null`; keep local section IDs out of the API payload.
- [x] **Step 6: Update case counts.** Use `levelCount` in `list.tsx` and the report card in `edit.tsx` without changing report data or calculations.

### Task 4: Show a grouped custom assignment menu in case mode

**Files:**
- Create: `frontend/student/Components/CaseLevelPicker.razor`
- Create: `frontend/student/Components/CaseLevelPicker.razor.css`
- Create: `frontend/student/Models/CaseLevelMenuItem.cs`
- Modify: `frontend/student/Pages/Home.razor`

**Interfaces:**
- `CaseLevelMenuItem` contains `LevelId`, `LevelVersionId`, one-based global `Order`, nullable `SectionOrder`, nullable `SectionTitle`, effective `DisplayTitle`, `IsCompleted`, and `IsDisabled`.
- `CaseLevelPicker` receives `IReadOnlyList<CaseLevelMenuItem> Items`, `Guid SelectedLevelId`, and `EventCallback<Guid> OnSelect`.
- The existing native `<select>` remains for non-case game and sandbox modes.

- [x] **Step 1: Build the case picker button and listbox.** Show selected status, global sequence, and effective title; group rows under nonblank section headings; render all assignments and disable locked rows.
- [x] **Step 2: Add menu interaction and accessibility.** Open with focus on the selected option (or first enabled option); keep listbox focus with `aria-activedescendant`; let ArrowUp/ArrowDown skip disabled options, Home/End move to the first/last enabled option, and Enter select. Close after selection, on Escape, and on outside click; return focus to the button after Escape or keyboard selection, while an outside click keeps focus on the clicked target. Expose button/listbox/option roles and selected/disabled state.
- [x] **Step 3: Style the menu to the existing student theme.** Use the dark palette, `#F36F29` focus accent, scrollable list, visible selected/completed states, and responsive width without changing global game styling.
- [x] **Step 4: Feed attempt metadata from `Home.razor`.** Build menu items from the attempt DTO, retain level-version identity for completion state, and keep the existing sequential selection rule.
- [x] **Step 5: Apply per-set display titles in case mode.** Use the effective title in the selected picker button, mission heading, and completion dialog; use the flattened case order for operation labels. Keep canonical titles and their `Order` untouched.
- [x] **Step 6: Preserve non-case behavior.** Render the current native selector and existing order/title behavior in regular game and sandbox modes.

### Task 5: Review the scoped change against the approved design

**Files:**
- Review: files changed in Tasks 1–4
- Review: `docs/superpowers/specs/2026-10-19-case-groups-and-custom-level-names-design.md`

**Interfaces:**
- No new runtime interface. This is a source review against the approved acceptance criteria.

- [x] **Step 1: Review the changed-file list.** Keep unrelated existing worktree changes unstaged and out of this feature.
- [x] **Step 2: Compare behavior with the spec.** Confirm migration ordering, nested API payload, attempt snapshot fallback, teacher editor actions, student gating/accessibility, and unchanged regular/sandbox modes; preserve distinct section boundaries when titles repeat.
- [x] **Step 3: Report verification limits.** Tests/builds were not run at the user's request; leave application inspection to the user.
