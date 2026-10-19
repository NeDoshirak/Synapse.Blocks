# Markdown Pages in Learning Sets Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add versioned Markdown pages to teacher-owned libraries, combine them with coding levels in learning sets, and render/progress through them in student attempts.

**Architecture:** Persist Markdown pages as immutable versions with owner-scoped image assets and one canonical sanitized renderer. Store mixed, ordered set content items in the existing sections, then snapshot every item and progression mode into each new attempt. Keep legacy level data and attempts compatible while updating teacher, superadmin, and student interfaces.

**Tech Stack:** ASP.NET Core 10, EF Core, PostgreSQL, Markdig, HTML sanitization, React 19, Refine, Ant Design, TypeScript, Blazor WebAssembly, JavaScript interop, Mermaid, KaTeX.

**Spec:** `docs/superpowers/specs/2026-10-20-markdown-pages-in-learning-sets-design.md`

## Global Constraints

- Work in the current `master` branch; preserve existing unrelated local changes, do not stage them, and do not commit.
- Do not add or run tests; the user will inspect the feature manually. Build the backend and frontends for compile-time verification only.
- Support CommonMark and GFM, math, Mermaid, and only a safe sanitized subset of raw HTML; never execute page-authored scripts or event handlers.
- Limit Markdown source to 100 KiB and uploaded PNG/JPEG/WebP images to 10 MiB each; keep uploaded bytes in PostgreSQL and keep assets while a page version references them.
- Keep pages and images owner-scoped. Cross-owner superadmin routes require the platform-admin role, active MFA, antiforgery, and audit events for writes.
- A published case contains 1–100 total mixed content items. A level version is unique per case; a Markdown page version may appear more than once as separate steps.
- Existing cases migrate to `Sequential`; an attempt captures step order, section/title presentation, content versions/source, and progression mode at start.
- A Markdown step completes only via the student **Continue** button. In sequential mode only the next incomplete step unlocks; `AllOpen` enables selection of all steps.
- Keep the built-in demo page superadmin-only, immutable, locally illustrated, and insertable into the superadmin's own test sets.
- Preserve current UI work in `CaseService.cs`, `Home.razor`, `CaseLevelPicker.razor`, its CSS, shared layouts, and student app assets. When touching those files, retain the already requested clear-field button at the far right of the student header.
- Preserve existing QR, participant continuation, archive/restore, level evaluation, report aggregation, standalone game, and sandbox behavior.

## Review Focus

- **Invalid or cross-owner IDs and oversized uploads:** reject a foreign page/asset, a forged content type, SVG, or content beyond the specified limits before persisting a page or set change. Implement guards in Tasks 1–2.
- **Untrusted Markdown and URLs:** rendered output must remove scripts, event handlers, unsafe schemes, and disallowed HTML in both preview and student views. Use one renderer in Task 1 and render only its sanitized output in Tasks 4 and 6.
- **Existing cases and attempts:** migration must retain each section, level order, custom title, legacy attempt order, and progress. Convert existing case records in Task 2; keep legacy attempt fallback in Task 3.
- **Sequential completion and retries:** a page Continue request must be idempotent, reject a locked/non-member step, and enable only the next item in sequential mode. Implement serialized progression in Task 3 and reflect it in Task 6.
- **Edits during a published set or active attempt:** selecting a new page version must affect only future attempts started after the set update; older attempts retain their captured body and presentation. Implement immutable references in Tasks 1–3.

---

### Task 1: Build the Markdown page library, renderer, and media API

**Files:**
- Create: `backend/Synapse.Blocks.Api/Data/Entities/MarkdownPage.cs`
- Create: `backend/Synapse.Blocks.Api/Data/Entities/MarkdownPageVersion.cs`
- Create: `backend/Synapse.Blocks.Api/Data/Entities/MarkdownAsset.cs`
- Create: `backend/Synapse.Blocks.Api/Contracts/MarkdownPages/MarkdownPageDtos.cs`
- Create: `backend/Synapse.Blocks.Api/MarkdownPages/MarkdownPageService.cs`
- Create: `backend/Synapse.Blocks.Api/MarkdownPages/MarkdownAssetService.cs`
- Create: `backend/Synapse.Blocks.Api/MarkdownPages/MarkdownRenderer.cs`
- Create: `backend/Synapse.Blocks.Api/Endpoints/TeacherMarkdownPageEndpoints.cs`
- Create: `backend/Synapse.Blocks.Api/Endpoints/PlatformMarkdownPageEndpoints.cs`
- Create: `backend/Synapse.Blocks.Api/Data/Migrations/20261020100000_MarkdownPages.cs` and generated designer
- Modify: `backend/Synapse.Blocks.Api/Data/AppDbContext.cs`
- Modify: `backend/Synapse.Blocks.Api/Data/Migrations/AppDbContextModelSnapshot.cs`
- Modify: `backend/Synapse.Blocks.Api/Program.cs`
- Modify: `backend/Synapse.Blocks.Api/Synapse.Blocks.Api.csproj`
- Create: `backend/Synapse.Blocks.Api/MarkdownPages/MarkdownDemoSeeder.cs`
- Create: `backend/Synapse.Blocks.Api/MarkdownPages/DemoAssets/*`
- Modify: `backend/Synapse.Blocks.Api/Auth/BootstrapPlatformAdmin.cs`

**Interfaces:**
- `MarkdownPageDto(Guid Id, string Title, Guid CurrentVersionId, int Version, bool IsBuiltIn, bool IsArchived)`.
- `MarkdownPageVersionDto(Guid Id, Guid PageId, int Version, string Title, string Markdown, DateTimeOffset CreatedAt)`.
- `CreateMarkdownPageRequest(string Title, string Markdown)` and `UpdateMarkdownPageRequest(Guid ExpectedVersionId, string Title, string Markdown)`; the update creates an immutable version and checks `ExpectedVersionId` for concurrency.
- `MarkdownPageService.ListAsync(ownerId)`, `GetAsync(ownerId, pageId)`, `CreateAsync(ownerId, request)`, `UpdateAsync(ownerId, pageId, request)`, `ListVersionsAsync(ownerId, pageId)`, `ArchiveAsync(ownerId, pageId)`, and `RestoreAsync(ownerId, pageId)` own all page/owner checks. Archive is rejected for built-in or set-referenced pages and never deletes versions/assets.
- `MarkdownRenderer.Render(string markdown, Func<Guid, string> assetUrl)` returns sanitized HTML from Markdig CommonMark/GFM extensions, a safe HTML allowlist, math blocks, Mermaid blocks, and validated asset references.
- `MarkdownAssetService.UploadAsync(ownerId, stream, declaredContentType)` detects PNG/JPEG/WebP from bytes, applies the 10 MiB limit, persists bytes as PostgreSQL `bytea`, and returns an opaque asset ID. `GetForPreviewAsync(ownerId, assetId)` enforces owner access.
- Map teacher endpoints under `/api/teacher/markdown-pages` and `/api/teacher/markdown-assets` with `ActiveMfa` and antiforgery. Add draft preview, upload, `POST /{pageId}/archive`, and `POST /{pageId}/restore` endpoints. Map a superadmin-only, owner-scoped read endpoint `/api/platform/teachers/{teacherId}/markdown-pages` with platform authorization.
- `MarkdownDemoSeeder.EnsureSeededAsync(platformAdminId)` inserts one built-in, immutable page and its local image assets idempotently after `BootstrapPlatformAdmin` has ensured the account exists.

- [ ] Add page, immutable version, and asset entities, indexes, owner foreign keys, `IsBuiltIn`/stable built-in key, and migration. Store Markdown source as bounded text and image bytes as `bytea`.
- [ ] Add owner-scoped page service and page CRUD/version contracts. Normalize titles, enforce expected-current-version updates, and reject updates/archive/restore of the built-in page. Permit archival only when no set references the page; allow restoring an archived page.
- [ ] Implement one canonical Markdig renderer plus sanitizer allowlist. Emit inert math/Mermaid placeholders, validate links/schemes, and rewrite inserted asset IDs through the provided URL factory.
- [ ] Add upload, preview, and platform page-list endpoints. Reject files over 10 MiB, disallowed detected formats, unowned assets, unowned pages, inactive users, and writes without active MFA/antiforgery.
- [ ] Add an idempotent startup seeder invoked after the bootstrap superadmin account exists. Include local demo artwork and Markdown demonstrating headings, lists, table, checklist, fenced code, formula, and Mermaid.
- [ ] Build `backend/Synapse.Blocks.Api/Synapse.Blocks.Api.csproj`; resolve compile/migration-model issues without running test suites.

### Task 2: Store mixed set items and per-set progression

**Files:**
- Create: `backend/Synapse.Blocks.Api/Data/Entities/CaseContentItem.cs`
- Delete: `backend/Synapse.Blocks.Api/Data/Entities/CaseLevel.cs` after migrating its rows to `CaseContentItem`
- Modify: `backend/Synapse.Blocks.Api/Data/Entities/CaseSection.cs`
- Modify: `backend/Synapse.Blocks.Api/Data/Entities/TeacherCase.cs`
- Modify: `backend/Synapse.Blocks.Api/Contracts/Cases/CaseDtos.cs`
- Modify: `backend/Synapse.Blocks.Api/Contracts/Cases/PlatformCaseDtos.cs`
- Modify: `backend/Synapse.Blocks.Api/Cases/CaseService.cs`
- Modify: `backend/Synapse.Blocks.Api/Cases/PlatformCaseService.cs`
- Modify: `backend/Synapse.Blocks.Api/Data/AppDbContext.cs`
- Create: `backend/Synapse.Blocks.Api/Data/Migrations/20261020110000_MixedCaseContent.cs` and generated designer
- Modify: `backend/Synapse.Blocks.Api/Data/Migrations/AppDbContextModelSnapshot.cs`
- Modify: `backend/Synapse.Blocks.Api/Endpoints/TeacherCaseEndpoints.cs`
- Modify: `backend/Synapse.Blocks.Api/Endpoints/PlatformCaseEndpoints.cs`

**Interfaces:**
- Use enum `CaseProgressionMode { Sequential, AllOpen }` and `TeacherCase.ProgressionMode` default `Sequential`.
- Use `CaseContentItemInput(string Kind, Guid? LevelVersionId, Guid? MarkdownPageVersionId, string? DisplayTitle)`, `CaseSectionInput(string? Title, IReadOnlyList<CaseContentItemInput> Items)`, and `CaseContentItemDto(Guid Id, string Kind, Guid? LevelVersionId, Guid? MarkdownPageVersionId, int Order, string Title, string? DisplayTitle, bool IsBuiltIn)`.
- `CaseSectionDto` returns ordered `Items`; `TeacherCaseDto` returns `ProgressionMode`, sections, and total `ItemCount`.
- `CaseService.CreateAsync` and `UpdateAsync` validate all item references against the set owner's level/page libraries before mutating saved rows. `PublishAsync` requires at least one item and caps total item count at 100.
- The migration creates `case_content_items`, copies each existing `case_levels` row to the same section/order/title, adds a database check that `Kind` matches exactly one non-null version foreign key, preserves level-version uniqueness within a set, and drops the replaced association table only after copying.

- [ ] Add `CaseContentItem` and `ProgressionMode`; configure section/order indexes, reference check constraints, restrict-delete foreign keys, and uniqueness for a level version per case.
- [ ] Migrate existing `CaseLevel` rows into their same sections with current ordering and display titles; set all existing cases to `Sequential`; retain section and item identity where possible.
- [ ] Replace level-only case inputs and DTOs with mixed section items. Include title/effective display title and page built-in status in teacher DTOs.
- [ ] Update teacher case create/read/update/publish paths. Validate 1–100 unique level versions, page versions (including same-owner or built-in superadmin-owned page only), item kinds, titles, and all associations before replacing the case content in one transaction.
- [ ] Update superadmin case list/edit data access so page choices follow the selected case owner; preserve original case owner and record platform write audits.
- [ ] Build the API project and inspect generated EF migration/model for the data-copy order and FK constraints; do not run tests.

### Task 3: Snapshot mixed attempts, enforce completion, and count progress

**Files:**
- Create: `backend/Synapse.Blocks.Api/Data/Entities/AttemptStep.cs`
- Modify: `backend/Synapse.Blocks.Api/Data/Entities/Attempt.cs`
- Modify: `backend/Synapse.Blocks.Api/Data/Entities/AttemptLevelResult.cs` only if necessary to map legacy/new completion
- Modify: `backend/Synapse.Blocks.Api/Data/AppDbContext.cs`
- Create: `backend/Synapse.Blocks.Api/Data/Migrations/20261020120000_AttemptContentSteps.cs` and generated designer
- Modify: `backend/Synapse.Blocks.Api/Data/Migrations/AppDbContextModelSnapshot.cs`
- Modify: `backend/Synapse.Blocks.Api/Contracts/Students/StudentDtos.cs`
- Modify: `backend/Synapse.Blocks.Api/Contracts/Cases/CaseDtos.cs`
- Modify: `backend/Synapse.Blocks.Api/Students/AttemptService.cs`
- Modify: `backend/Synapse.Blocks.Api/Students/ProgramEvaluationService.cs`
- Modify: `backend/Synapse.Blocks.Api/Endpoints/StudentAttemptEndpoints.cs`
- Modify: `backend/Synapse.Blocks.Api/Cases/ShareLinkService.cs`
- Modify: `backend/Synapse.Blocks.Api/Reports/CaseReportService.cs`

**Interfaces:**
- `AttemptStep` is an attempt-owned immutable row with step ID, one-based order, kind, pinned source-version IDs, section/title presentation, Markdown source for page steps, `Completed`, and `CompletedAt`.
- `StudentCaseStepDto(Guid StepId, string Kind, int Order, int? SectionOrder, string? SectionTitle, string Title, Guid? LevelVersionId, LevelDefinition? Definition, string? MarkdownHtml, bool IsCompleted)` is the unified student item.
- Extend `AttemptDto` with `ProgressionMode`, `CompletedStepCount`, and ordered `Steps`; preserve legacy level ID/result fields for compatibility with existing level evaluation and attempts.
- `AttemptService.StartAttemptAsync` snapshots the saved set's items and mode; `ToDtoAsync` reads the attempt snapshot, falling back to old level JSON for historical attempts.
- Add `POST /api/student/cases/{token}/attempts/{attemptId}/steps/{stepId}/complete`; it authenticates the share token and continuation cookie, locks the attempt, idempotently completes only an in-attempt unlocked Markdown step, and returns refreshed progress.
- `ProgramEvaluationService` maps level submissions to the matching `AttemptStep`, enforces order only for `Sequential`, marks completion when tests pass, and keeps legacy `AttemptLevelResult` behavior for old attempts.

- [ ] Add attempt step snapshots/completion fields and database migration. Leave historical attempt/result rows untouched.
- [ ] Update attempt start/resume mapping to capture mixed item order, display titles, section titles, progression mode, page Markdown source, and pinned level/page versions.
- [ ] Add student share-case mapping that renders sanitized page HTML and case-scoped asset URLs only for active published sets.
- [ ] Implement the locked, idempotent Markdown completion endpoint under the existing participant continuation-token flow and serializable/advisory-lock transaction.
- [ ] Update level evaluation to locate pinned level step identity, enforce `Sequential` lock order, allow `AllOpen` submissions for any item, and update attempt-step completion on pass.
- [ ] Update case report aggregation to count new completed attempt steps and fall back to old completed level results for legacy attempts; rename the exposed field to `CompletedStepCount`. Update `CaseReport.tsx` to display the generalized count.
- [ ] Build the API project and inspect attempt responses for new and legacy shapes; do not run tests.

### Task 4: Add the teacher Markdown library and editor

**Files:**
- Create: `frontend/teacher/src/pages/markdown-pages/list.tsx`
- Create: `frontend/teacher/src/pages/markdown-pages/edit.tsx`
- Create: `frontend/teacher/src/components/markdown/MarkdownSourceEditor.tsx`
- Create: `frontend/teacher/src/components/markdown/MarkdownPreview.tsx`
- Create: `frontend/teacher/src/resources/markdownPages.ts`
- Modify: `frontend/teacher/src/types/api.ts`
- Modify: `frontend/teacher/src/App.tsx`
- Modify: `frontend/teacher/package.json` and lock file
- Modify: `frontend/teacher/src/styles/*` only if shared markdown preview styles belong in the existing theme

**Interfaces:**
- `MarkdownPage` and `MarkdownPageVersion` TypeScript interfaces match Task 1 DTOs.
- Resource methods `listMarkdownPages`, `getMarkdownPage`, `createMarkdownPage`, `updateMarkdownPage`, `listMarkdownPageVersions`, `uploadMarkdownAsset`, and `previewMarkdown` call the matching teacher endpoints and preserve antiforgery handling.
- `MarkdownSourceEditor` receives `{ value, onChange, onUploadImage, disabled }`; `MarkdownPreview` receives sanitized HTML and applies local math/Mermaid enhancements without re-parsing author HTML.

- [ ] Add page list/create/edit routes and teacher navigation entry. Display version, built-in/archived status, and non-destructive archive/restore/reference behavior.
- [ ] Build split source/live-preview editor with formatting insertion for CommonMark/GFM, images, code, math, and Mermaid; enforce the 100 KiB source limit client-side and rely on API validation server-side.
- [ ] Add image upload/paste insertion that uses the authenticated API and inserts stable asset references rather than base64 or external storage links.
- [ ] Render only server-sanitized preview HTML; add bundled local Mermaid/KaTeX assets to the teacher build without CDN dependencies.
- [ ] Build `frontend/teacher` with its existing production build command; do not run its test suite.

### Task 5: Add mixed items and progression controls to set editors

**Files:**
- Modify: `frontend/teacher/src/components/cases/CaseSectionsEditor.tsx`
- Modify: `frontend/teacher/src/resources/caseOrder.ts`
- Modify: `frontend/teacher/src/pages/cases/edit.tsx`
- Modify: `frontend/teacher/src/pages/cases/list.tsx`
- Modify: `frontend/teacher/src/pages/platform/cases/edit.tsx`
- Modify: `frontend/teacher/src/components/reports/CaseReport.tsx`
- Modify: `frontend/teacher/src/types/api.ts`
- Modify: `frontend/teacher/src/resources/*` for platform page loading as needed

**Interfaces:**
- `CaseContentItemDraft` discriminates `{ kind: "level"; levelVersionId; displayTitle }` from `{ kind: "markdown"; pageVersionId; displayTitle }`; `CaseSectionDraft` carries ordered `items` instead of level-only arrays.
- Ordering helpers add/remove/move either item kind within or between sections while preserving stable client IDs and custom titles.
- `CaseSectionsEditor` receives owner-scoped `levels`, `markdownPages`, `sections`, `progressionMode`, `disabled`, and an `onChange` callback. The same component serves teacher and platform case forms.

- [ ] Extend API types and ordering helpers for mixed item identities and page versions.
- [ ] Add an item picker per section for coding assignments and Markdown page versions; preserve current assignment naming/reordering controls and add page display-title overrides.
- [ ] Add section move/reorder behavior for both item kinds, and permit a page version to be inserted as multiple distinct steps.
- [ ] Add per-set radio/segmented control for sequential opening versus all steps open; serialize the selected mode in create/update requests.
- [ ] In platform case editing, reload page and level choices when owner changes and prevent cross-owner selection. Preserve archived-case read-only state.
- [ ] Build `frontend/teacher` and resolve type errors; do not run tests.

### Task 6: Render mixed content and progression in the student app

**Files:**
- Create: `frontend/student/Components/MarkdownPageContent.razor`
- Create: `frontend/student/Components/MarkdownPageContent.razor.css`
- Modify: `frontend/student/Components/CaseLevelPicker.razor`
- Modify: `frontend/student/Components/CaseLevelPicker.razor.css`
- Modify: `frontend/student/Pages/Home.razor`
- Modify: `frontend/student/Pages/CaseEntry.razor`
- Modify: `frontend/student/Models/StudentCaseDtos.cs`
- Modify: `frontend/student/Models/StudentAttemptDtos.cs`
- Modify: `frontend/student/Serialization/AppJsonSerializerContext.cs`
- Modify: `frontend/student/wwwroot/js/*` for math/Mermaid enhancement and Continue requests
- Modify: `frontend/student/wwwroot/index.html`
- Modify: `frontend/student/Synapse.Blocks.csproj`
- Modify: `frontend/student/Dockerfile`
- Create: `frontend/student/package.json` and lock file for local math/diagram assets
- Create: `frontend/student/scripts/copy-markdown-assets.mjs`
- Modify: `frontend/student/wwwroot/css/app.css`

**Interfaces:**
- Blazor `StudentCaseStepDto` mirrors the Task 3 API DTO; keep existing `StudentCaseLevelDto` compatibility as needed for the standalone game and old cases.
- `MarkdownPageContent` receives the sanitized HTML, step ID, completion state, and callbacks to complete/continue; it emits no raw source HTML into the DOM.
- Generalized picker receives ordered steps, section headings, completion IDs, and `ProgressionMode`. It keeps completed entries selectable, disables only locked later items in sequential mode, and enables every item in all-open mode.

- [ ] Update generated JSON registrations and student API models for mixed attempt steps and completion responses.
- [ ] Generalize the case menu while preserving the existing menu visual treatment, status markers, current selection, keyboard behavior, and far-right clear-field button placement.
- [ ] Render Markdown steps with safe sanitized HTML, locally bundled formula/Mermaid enhancement, and image URLs authorized by the active share link.
- [ ] Add explicit **Continue** button behavior; send completion to the API, retain completion after refresh/resume, and select the next item only after a successful response.
- [ ] Update case entry/progress labels and completion state to count content steps; leave standalone game and sandbox mode paths unchanged.
- [ ] Update student build asset handling for local KaTeX/Mermaid files, then build the student project without running tests or rebuilding/running containers.

### Task 7: Integrate, inspect the diff, and hand off

**Files:**
- Modify only files in Tasks 1–6.

- [ ] Read the approved spec and this plan against the final diff; confirm each acceptance criterion has an implementation path and no unrelated changes were staged or reverted.
- [ ] Build API, teacher frontend, and student frontend once after integration; do not run tests or alter containers unless the user separately asks.
- [ ] Check `git status` and report changed files, migration behavior, build results, and the fact that behavioral testing was not run.
