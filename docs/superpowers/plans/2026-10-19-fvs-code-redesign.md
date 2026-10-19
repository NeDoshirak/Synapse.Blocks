# ФВС/Код Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rebrand student and teacher UI as «ФВС/Код», make `#F36F29` the student UI's primary brand accent, and remove the student landing page while retaining the dark look and game behavior.

**Architecture:** Keep the change in existing Blazor student UI and React/Refine teacher UI. Update the student CSS palette and visible page titles/brand labels; update only visible teacher brand labels and document title. Preserve technical identifiers and the embedded developer/admin styling.

**Tech Stack:** Blazor / ASP.NET Core, CSS, React, Refine, TypeScript, Vite.

**Spec:** `docs/superpowers/specs/2026-10-19-fvs-code-redesign-design.md`

## Global Constraints

- Student surfaces retain a dark look with neutral charcoal backgrounds, including the 3D city floor and grid, no green tint in base surfaces, and use exact accent `#F36F29` for brand and primary actions.
- Replace user-visible platform naming with «ФВС/Код» in student and teacher interfaces.
- Change the teacher interface's visible name only; preserve its theme, layout, routes, and functionality.
- Do not visually change the developer/admin area (`AdminLayout`, `.admin-*`).
- Preserve routes, game mechanics, levels, lesson text, storage keys, API contracts, namespaces, and JavaScript integration identifiers.
- Keep semantic colors for game outcomes, block types, and connection states where they convey meaning.
- Do not change server infrastructure or dependencies.
- Follow-up decision: remove the landing page and its 3D city, settings/data, and dedicated CSS/JS; `/` redirects to `/game`; remove the «Главная» navigation item.

## Review Focus

- Admin CSS shares the student stylesheet: confirm `.admin-*` selectors and AdminLayout's blue theme remain unchanged (inspect diff and compare affected selector rules).
- Orange can be confused with error or block semantics: inspect game status, block, port, and wire colors on the game screen and keep their existing roles.
- QR/case mode has a distinct header path: inspect `/case/...` and `game?case=...` branding while preserving its existing navigation behavior.
- Loading, unavailable-level, and not-found states can retain stale branding: inspect all of them for visible old names.
- Branding text may exist in both browser metadata and UI components: inspect tab titles, loading screen, login page, and Refine shell in both apps.

---

### Task 1: Apply the student brand palette and rename

**Files:**
- Modify: `frontend/student/wwwroot/css/app.css`
- Modify: `frontend/student/wwwroot/city-scene.json`
- Modify: `frontend/student/wwwroot/js/city3d.js`
- Modify: `frontend/student/Services/CitySceneStore.cs`
- Modify: `shared/Synapse.Blocks.Core/Models/GameModels.cs`
- Modify: `frontend/student/Layout/MainLayout.razor`
- Modify: `frontend/student/wwwroot/index.html`
- Modify: `frontend/student/Pages/Landing.razor`
- Modify: `frontend/student/Pages/Home.razor`
- Modify: `frontend/student/Pages/CaseEntry.razor`
- Modify: `frontend/student/Pages/Guide.razor`
- Modify: `frontend/student/Pages/NotFound.razor`

**Interfaces:**
- Consumes: existing CSS variables and existing Blazor routes/layouts.
- Produces: student-visible branding «ФВС/Код» and orange brand accent `#F36F29`, with semantic game colors and admin-only rules preserved.

- [x] **Step 1: Add and apply the student brand token.** Keep `--green` as the existing semantic/game color; add `--brand: #F36F29` and use it for student branding, active navigation, focus, and primary actions. Keep `.admin-shell` and `/sandbox` appearance unchanged, including the shared startup screen. Recolor student landing and case-entry accents and neutralize the base surfaces, game canvas, overlays, and 3D city floor/grid; preserve success states and route colors.
- [x] **Step 2: Update common student branding and page titles.** Set the visible header and boot-screen wordmark to «ФВС/Код»; set student document title, page titles, and branded case-entry eyebrow copy to the same name. Leave `AdminLayout.razor` and all `.admin-*` rules untouched.
- [ ] **Step 3: Leave final student visual inspection to the user.** The user will review landing, game, case/QR entry, guide, and responsive states.
- [ ] **Step 4: No post-change build or tests.** The user asked to inspect the result without tests; do not run tests, builds, or lint after the final background change.
- [x] **Step 5: Review the student changes.** Inspect changed selectors and data flow to ensure student-only styling, unchanged routes/behavior, and neutral defaults with migration of the old default city floor/grid.

### Task 2: Rename the teacher interface without restyling it

**Files:**
- Modify: `frontend/teacher/index.html`
- Modify: `frontend/teacher/src/App.tsx`
- Modify: `frontend/teacher/src/pages/login.tsx`

**Interfaces:**
- Consumes: existing Refine app shell, login page, and Vite document metadata.
- Produces: visible teacher-facing name «ФВС/Код» with existing theme and layout intact.

- [x] **Step 1: Replace visible teacher brand text.** Update Vite document title, Refine title, shell brand, and login title from `Synapse Blocks` to «ФВС/Код».
- [x] **Step 2: Confirm no teacher theme changes.** Inspect the diff to ensure CSS/theme, layout, routes, form content, and functionality are untouched.
- [x] **Step 3: Teacher lint/build were run earlier in the task.** Do not rerun them after the user's instruction to skip further checks; no teacher files changed afterward.

### Task 3: Cross-surface visual and branding review

**Files:**
- Review: all files changed in Tasks 1–2.

**Interfaces:**
- Consumes: completed student and teacher changes.
- Produces: verified consistency against the spec and no accidental technical renames.

- [x] **Step 1: Search remaining visible old names.** Remaining `Синапс` in student code is the excluded `/sandbox` title/admin shell; remaining `synapse` strings are technical identifiers. Teacher visible labels use «ФВС/Код».
- [ ] **Step 2: Final visual review belongs to the user.** The user asked to inspect the redesigned screens personally; no app/browser checks will be run after the background update.
- [x] **Step 3: Review teacher changes.** Login and shell copy use the new name; teacher theme remains unchanged.
- [x] **Step 4: Confirm final scope by source review.** The final edits stay within student styling/scene defaults, student/teacher visible brand strings, and the spec/plan.

### Follow-up: Remove the landing page

- [x] Delete `Landing.razor`, the 3D city scene, its data/settings service, and landing-only CSS/JS.
- [x] Redirect `/` to `/game`; retain `/game`, case/QR, guide, and `/sandbox` routes.
- [x] Remove «Главная» navigation and update links/copy that pointed back to the city.
- [x] Skip tests, builds, lint, and browser checks at the user's request; user will review the result.
