# Synapse Blocks Teacher and Student Experience Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver the Refine teacher workspace and connect the existing Blazor game to QR cases, server-verified level completion, repeated attempts, and teacher reports.

**Architecture:** Build React/Refine in `frontend/teacher` and retain the Blazor game in `frontend/student`; the API and shared game library are in `backend/` and `shared/`. Teacher UI, student UI, API, PostgreSQL, and an Nginx gateway run in separate containers. The gateway routes all paths under one origin. Both clients call the platform API from `2026-09-30-platform-api.md`; Blazor uses local evaluation for responsive public-test feedback and the API for authoritative completion.

**Tech Stack:** React, TypeScript, Refine, Ant Design, Vite, .NET 10 Blazor WebAssembly, ASP.NET Core API from the platform plan, Nginx, Vitest.

**Spec:** `docs/superpowers/specs/2026-09-30-teacher-qr-platform-design.md`

**Prerequisite Plan:** `docs/superpowers/plans/2026-09-30-platform-api.md` must be completed first; its routes and DTOs are the interface contract for this plan.

## Global Constraints

- **Teacher application:** React with Refine, used for sign-in, level authoring, case assembly, QR management, and results.
- **Student application:** the current Blazor WebAssembly game, adapted to load published case content and submit progress through the API.
- Serve teacher app, student app, and API under one origin via the gateway (for example `/admin`, `/game`, and `/api`) to simplify cookie-based authentication and avoid exposing credentials to JavaScript storage.
- Students have no accounts, enter a display name, and can make multiple attempts.
- The student client never receives hidden tests or expected outputs; the API evaluation response is authoritative for level completion.
- Teacher editing creates new level versions. Published cases and in-progress attempts remain pinned to their captured versions.
- Best progress is the greatest number of case levels completed across a normalized display name's attempts; do not add a time or run-count tie-break.
- A teacher can deactivate a QR link while historical results remain visible.
- The first release supports ordinary ordered-level cases; future case types are outside this plan.

## Review Focus

- An unauthenticated user or a user with an expired cookie must never see teacher pages or private responses; pin in Task 1.
- Edits with invalid definitions, stale versions, or failed network requests must leave the prior saved version intact and show a recoverable error; pin in Task 2.
- QR entry with a revoked token, blank/oversized name, or network interruption must not create a false attempt or completion; pin in Tasks 3 and 4.
- Client-side success on visible tests must not be enough to complete a level if the server rejects hidden tests; pin in Task 5.
- Repeated attempts, a refresh during an unfinished attempt, duplicate submissions, and a closed QR must not erase or misattribute history; pin in Tasks 3–5.

---

### Task 1: Create the Refine teacher application and sign-in flow

**Files:**
- Create: `frontend/teacher/package.json`
- Create: `frontend/teacher/package-lock.json`
- Create: `frontend/teacher/index.html`
- Create: `frontend/teacher/vite.config.ts`
- Create: `frontend/teacher/tsconfig.json`
- Create: `frontend/teacher/src/main.tsx`
- Create: `frontend/teacher/src/App.tsx`
- Create: `frontend/teacher/src/providers/authProvider.ts`
- Create: `frontend/teacher/src/providers/dataProvider.ts`
- Create: `frontend/teacher/src/providers/apiClient.ts`
- Create: `frontend/teacher/src/pages/login.tsx`
- Create: `frontend/teacher/src/pages/invitations/accept.tsx`
- Create: `frontend/teacher/src/pages/platform/invitations.tsx`
- Create: `frontend/teacher/src/types/api.ts`
- Create: `frontend/teacher/src/test/setup.ts`
- Create: `frontend/teacher/vitest.config.ts`
- Modify: `.gitignore`

**Interfaces:**
- Consumes: API routes `POST /api/auth/sign-in`, `POST /api/auth/sign-out`, `GET /api/auth/me`, `POST /api/platform/invitations`, and `POST /api/platform/invitations/accept` from the platform API plan.
- `POST /api/platform/invitations` responds once with `{ email, expiresAt, invitationUrl }`; the platform-admin screen displays a copyable link, not the hashed token.
- Produces: Refine `authProvider` (`login`, `logout`, `check`, `getIdentity`, `onError`) using same-origin `fetch` with `credentials: "include"`; `dataProvider` calls `/api/teacher/*` with cookie credentials and the antiforgery token.
- `frontend/teacher/src/types/api.ts` defines matching TypeScript DTOs. Do not store auth tokens in localStorage/sessionStorage.

- [ ] **Step 1: Write failing provider tests** for login success/failure, expired session check, logout, identity lookup, credentials included, and antiforgery header on mutations.
- [ ] **Step 2: Run `cd frontend/teacher && npm test -- --run`** and confirm failures before providers exist.
- [ ] **Step 3: Scaffold Refine + Vite + TypeScript** with React Router and Ant Design; configure the app base path as `/admin/` and same-origin API root `/api`.
- [ ] **Step 4: Implement cookie-based auth and invitation pages.** Expose a platform-admin invitation screen only when `GET /api/auth/me` reports the platform-admin role; accept one-time invitation URLs without persisting invitation tokens after submission.
- [ ] **Step 5: Run `cd frontend/teacher && npm test -- --run && npm run build`**; expect all provider tests and a production build to pass.
- [ ] **Step 6: Commit** the teacher app shell and authentication.

### Task 2: Build teacher level catalog and test-case editor

**Files:**
- Create: `frontend/teacher/src/resources/levels.ts`
- Create: `frontend/teacher/src/pages/levels/list.tsx`
- Create: `frontend/teacher/src/pages/levels/create.tsx`
- Create: `frontend/teacher/src/pages/levels/edit.tsx`
- Create: `frontend/teacher/src/components/levels/LevelDefinitionForm.tsx`
- Create: `frontend/teacher/src/components/levels/TestCaseEditor.tsx`
- Create: `frontend/teacher/src/components/levels/IntroStepEditor.tsx`
- Create: `frontend/teacher/src/components/levels/BlockRulesEditor.tsx`
- Create: `frontend/teacher/src/components/levels/StarterTemplatePicker.tsx`
- Create: `frontend/teacher/src/pages/levels/level-editor.test.tsx`
- Modify: `frontend/teacher/src/App.tsx`
- Modify: `frontend/teacher/src/types/api.ts`

**Interfaces:**
- Consumes: `GET|POST /api/teacher/levels`, `GET|PUT /api/teacher/levels/{levelId}`; API response `TeacherLevelDto { id, title, currentVersionId, definition }`; create body `{ definition: LevelDefinition }`; update body `{ expectedVersionId, definition: LevelDefinition }`.
- Produces: Refine `levels` resource and a form that edits all fields in shared `LevelDefinition`, including allowed/required blocks, loop/operation constraints, visible/hidden tests, and intro steps.
- Saving creates a new immutable version; the editor shows the returned `currentVersionId` and does not claim an unsaved draft is published.

- [ ] **Step 1: Write failing component tests** for loading a template, editing level metadata, adding/editing/removing tests, preserving hidden flags, editing intro steps, handling stale-version HTTP 409 without overwriting remote changes, and showing validation/network errors without discarding current form values.
- [ ] **Step 2: Run the focused level UI tests** and confirm the resource/forms are missing.
- [ ] **Step 3: Implement the Refine levels resource and editor components.** Keep test input/expected output controls together and visibly mark hidden tests; use the starter endpoint/data contract from the API plan.
- [ ] **Step 4: Run `cd frontend/teacher && npm test -- --run && npm run build`**; verify successful create/edit and error recovery in tests.
- [ ] **Step 5: Commit** level catalog and test-case editing.

### Task 3: Build ordinary case assembly, QR controls, and reports

**Files:**
- Create: `frontend/teacher/src/resources/cases.ts`
- Create: `frontend/teacher/src/pages/cases/list.tsx`
- Create: `frontend/teacher/src/pages/cases/create.tsx`
- Create: `frontend/teacher/src/pages/cases/edit.tsx`
- Create: `frontend/teacher/src/pages/cases/show.tsx`
- Create: `frontend/teacher/src/components/cases/OrderedLevelPicker.tsx`
- Create: `frontend/teacher/src/components/cases/ShareLinkPanel.tsx`
- Create: `frontend/teacher/src/components/reports/CaseReport.tsx`
- Create: `frontend/teacher/src/pages/cases/case-management.test.tsx`
- Create: `frontend/teacher/src/pages/cases/case-report.test.tsx`
- Modify: `frontend/teacher/src/App.tsx`
- Modify: `frontend/teacher/src/types/api.ts`

**Interfaces:**
- Consumes: case CRUD/publish/share-link routes, archive-as-delete behavior, and `GET /api/teacher/cases/{caseId}/report` from the platform API plan.
- `TeacherCaseDto { id, caseType: "orderedLevels", title, description, isPublished, archived, levels: [{ levelId, levelVersionId, order, title }], shareLinkActive }`.
- Create/update body `{ caseType: "orderedLevels", title, description, orderedLevelVersionIds: string[] }`.
- Report DTO includes participants, attempts, completed-level count, and `bestProgress`; raw token is returned only by share-link create/rotate and is never requested again from the server. Build the QR URL from `window.location.origin + "/case/" + token`.
- Produces: ordered ordinary-case editor, publish state, QR image/link display, close/rotate action, complete attempt history, and best progress per participant.

- [ ] **Step 1: Write failing UI tests** for ordering, duplicate prevention, publishing disabled on an empty case, QR display only on one-time token creation, archive/close/rotate with preserved report history, and best-progress value display.
- [ ] **Step 2: Run the focused case/report tests** and confirm pages/components are missing.
- [ ] **Step 3: Implement case resources and pages** using only the teacher-owned endpoints; draw the QR client-side from the one-time share URL and show a warning if the raw URL is no longer available after navigation.
- [ ] **Step 4: Implement report rendering** with all attempts and best progress based solely on completed-level count; tied attempts remain visible without a fabricated secondary ranking.
- [ ] **Step 5: Run `cd frontend/teacher && npm test -- --run && npm run build`**; verify ownership errors and revoked links are presented as unavailable, not as another teacher's data.
- [ ] **Step 6: Commit** case management and reports.

### Task 4: Add QR entry, student name, attempt start, and resume state

**Files:**
- Create: `frontend/student/Pages/CaseEntry.razor`
- Create: `frontend/student/Services/StudentCaseStore.cs`
- Create: `frontend/student/Models/StudentCaseDtos.cs`
- Create: `frontend/student/Models/StudentAttemptDtos.cs`
- Modify: `frontend/student/Program.cs`
- Modify: `frontend/student/App.razor`
- Modify: `frontend/student/Layout/MainLayout.razor`
- Create: `tests/Synapse.Blocks.Student.Tests/Synapse.Blocks.Student.Tests.csproj`
- Create: `tests/Synapse.Blocks.Student.Tests/StudentCaseStoreTests.cs`

**Interfaces:**
- Consumes: `GET /api/student/cases/{token}`, `POST /api/student/cases/{token}/participants`, `POST /api/student/cases/{token}/attempts`, and `GET /api/student/cases/{token}/attempts/current` from the platform API plan.
- `StudentCaseStore.LoadAsync(string token) -> Task<StudentCaseDto>`; `StartAsync(string token, string displayName) -> Task<StudentAttemptDto>`; `ResumeAsync(string token) -> Task<StudentAttemptDto?>`.
- Public case DTO contains only ordered pinned versions and public tests. `StudentAttemptDto` contains attempt ID, ordered version IDs, and already-completed version IDs.
- QR URL route is `/case/{token}`; after name entry of 1–80 trimmed characters and attempt creation, navigate to `/game?case={token}&attempt={attemptId}`.

- [ ] **Step 1: Write failing store tests** for active/unavailable case, blank or oversized name rejection, cookie credential inclusion, attempt creation, and resume of an unfinished attempt.
- [ ] **Step 2: Run `dotnet test tests/Synapse.Blocks.Student.Tests/Synapse.Blocks.Student.Tests.csproj`;** confirm the DTO/store/page do not exist.
- [ ] **Step 3: Implement student DTOs, HTTP store, and `/case/{token}` entry page.** Normalize input for validation but preserve entered display spelling; do not include a student password/account flow.
- [ ] **Step 4: Add case-aware shell behavior** so the personal campaign reset button and city navigation do not clear or escape a QR attempt.
- [ ] **Step 5: Run focused student tests and `dotnet build frontend/student/Synapse.Blocks.csproj`;** verify invalid links show a friendly unavailable screen and do not start an attempt.
- [ ] **Step 6: Commit** QR entry and attempt creation/resume.

### Task 5: Integrate server evaluation and case progression into the existing game

**Files:**
- Modify: `frontend/student/Pages/Home.razor`
- Modify: `frontend/student/Services/StudentCaseStore.cs`
- Modify: `frontend/student/Services/ProgressStore.cs`
- Modify: `frontend/student/Services/SolutionStore.cs`
- Modify: `frontend/student/Serialization/AppJsonSerializerContext.cs`
- Create: `tests/Synapse.Blocks.Student.Tests/CaseProgressionTests.cs`
- Create: `tests/Synapse.Blocks.Student.Tests/HiddenTestClientContractTests.cs`

**Interfaces:**
- Consumes: `StudentCaseStore` and `/api/student/cases/{token}/attempts/{attemptId}/levels/{levelVersionId}/evaluate` with `BlockProgram`; response `LevelEvaluationDto { passed, publicTestResults }`.
- `StudentCaseStore.EvaluateAsync(string token, Guid attemptId, Guid levelVersionId, BlockProgram program) -> Task<LevelEvaluationDto>`.
- Produces: case mode within the current `/game` block editor: only the next incomplete case level is selectable; each run sends the program to the API; the API `passed` value alone marks the level complete and advances the case.

- [ ] **Step 1: Write failing progression tests** for public-test pass + hidden-test fail, successful server evaluation, duplicate evaluation response, resume after refresh, no skipping case order, and no completion when API is unavailable.
- [ ] **Step 2: Run the focused tests** and verify the existing local-only `RunTests` completion path cannot satisfy the new contract.
- [ ] **Step 3: Implement case mode in `frontend/student/Pages/Home.razor`.** Retain local `BlockProgramRunner` feedback only for public tests; use `LevelEvaluationDto.passed` as the only completion authority. Hide hidden expected values from the in-memory case DTO and never serialize them into client storage.
- [ ] **Step 4: Persist unfinished block programs by `(attemptId, levelVersionId)`** and completed levels from API responses. Keep campaign local-storage behavior unchanged outside case mode.
- [ ] **Step 5: Run `dotnet test tests/Synapse.Blocks.Student.Tests/Synapse.Blocks.Student.Tests.csproj` and `dotnet build frontend/student/Synapse.Blocks.csproj`;** confirm all case progression/privacy tests pass and existing campaign builds.
- [ ] **Step 6: Commit** server-verified game progression.

### Task 6: Serve all apps from one origin and verify the end-to-end learning loop

**Files:**
- Create: `frontend/student/Dockerfile` and `frontend/teacher/Dockerfile`
- Create: `deploy/gateway/Dockerfile` and `deploy/gateway/nginx.conf`
- Modify: `backend/Synapse.Blocks.Api/Program.cs`
- Modify: `docker-compose.yml`
- Modify: `.dockerignore`
- Modify: `README.md`
- Remove or replace: `frontend/student/Pages/Admin.razor`
- Remove or replace: `frontend/student/Pages/LevelEditor.razor`
- Remove or replace: `frontend/student/Pages/CityEditor.razor`
- Modify: `frontend/student/Layout/NavMenu.razor`
- Modify: `frontend/student/Layout/AdminLayout.razor`
- Create: `tests/Synapse.Blocks.Api.Tests/EndToEnd/TeacherToStudentCaseFlowTests.cs`

**Interfaces:**
- Consumes: all routes and client flows from Tasks 1–5 and the API plan.
- Produces: `https://<host>/admin/*` serves Refine, `/case/{token}` and `/game` serve Blazor, `/api/*` serves API; unknown API paths remain API 404s and are never swallowed by SPA fallback.
- Vite build uses base `/admin/`; Blazor keeps its root base path. Teacher and student assets remain in their own images; the gateway routes to them and the API as separate services.

- [ ] **Step 1: Write the end-to-end API test** for provisioned teacher sign-in → create level/tests → create/publish case → resolve QR → start attempt → evaluate passing program → fetch report with best progress; include a foreign teacher access denial.
- [ ] **Step 2: Run the focused end-to-end test** against the test database and verify failures are isolated to missing web hosting/client wiring.
- [ ] **Step 3: Build separate Docker images** for the ASP.NET Core API, Blazor student app, and Refine teacher app; add the Nginx gateway container to route `/api`, `/admin`, and student paths under one origin. Keep `frontend/student`, `frontend/teacher`, `backend/Synapse.Blocks.Api`, and `shared/Synapse.Blocks.Core` as distinct source/build contexts.
- [ ] **Step 4: Replace the legacy JSON-download `/admin` and `/editor` pages** with the Refine application, and remove the unauthenticated `/admin/city` editor route. Update navigation/layout so only the Refine app links to teacher operations; keep student game and guide routes. Ensure no unauthenticated path reaches teacher APIs.
- [ ] **Step 5: Run the end-to-end test**, API/core/Blazor tests, `cd frontend/teacher && npm test -- --run && npm run build`, and `docker compose config`.
- [ ] **Step 6: Walk through the flow in a browser:** invite a teacher, sign in, create two levels with public and hidden tests, create and print a case QR, complete an attempt, refresh and resume another attempt, check best progress, revoke the link, and verify the report remains.
- [ ] **Step 7: Update README** with the unified Docker Compose workflow, Refine URL, teacher provisioning, QR student flow, and developer commands.
- [ ] **Step 8: Commit** unified hosting and the end-to-end learning loop.
