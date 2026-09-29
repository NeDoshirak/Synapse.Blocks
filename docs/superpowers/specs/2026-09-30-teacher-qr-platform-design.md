# Synapse Blocks teacher and QR platform design

## Purpose

Turn Synapse Blocks from a static Blazor game into a teacher-managed platform. Teachers need private accounts, an editor for their own levels and test cases, a way to assemble levels into ordinary ordered cases, QR links for students, and reports on student attempts. Students scan a QR code, enter a display name, and solve the assigned levels. A teacher should be able to see each attempt and each student's best progress through the case.

## Current state

- The repository is a .NET 10 Blazor WebAssembly application.
- The game loads level definitions from `wwwroot/levels.json`.
- Level and test-case editing is a browser-side JSON workflow; it does not publish shared server data.
- Student progress is stored in browser local storage.
- The block-program execution engine and student game UI already exist in C# and Razor.

## Goals

1. Provide a Refine-based teacher application, an ASP.NET Core API, and PostgreSQL persistence.
2. Let each teacher manage only their own levels, cases, QR links, and student results.
3. Let platform administrators provision teacher accounts through invitations; do not expose public teacher self-registration in the first release.
4. Let teachers create and edit levels and their test cases, then assemble levels in a chosen order into an ordinary case.
5. Issue a QR code and shareable URL for each case. Students can enter a name and make multiple attempts without student accounts.
6. Persist student progress and attempt history on the server. Report best progress as the greatest number of levels completed in any attempt for the same normalized display name and case.
7. Preserve the existing Blazor student experience and block-program engine where practical, connecting it to the new API rather than rewriting the game UI.
8. Keep content extensible so future case types can be introduced without changing the ordinary ordered-level case contract.

## Out of scope for the first release

- Student accounts or school-wide shared teacher workspaces.
- Public teacher registration.
- Timed examinations or other case types beyond an ordinary ordered set of levels.
- Collaborative editing, class rosters, or external school-system integration.
- Replacing the existing block editor/game interface with a new React student application.

## Architecture

### Applications and runtime

- **Teacher application:** `frontend/teacher`, a React/Refine app for sign-in, level authoring, case assembly, QR management, and results. It runs in its own container.
- **Student application:** `frontend/student`, the current Blazor WebAssembly game, adapted to load published case content and submit progress through the API. It runs in its own container.
- **Backend:** `backend/Synapse.Blocks.Api`, an ASP.NET Core Web API that owns authentication, authorization, content validation, QR resolution, attempt lifecycle, persistence, and reporting. It runs in its own container.
- **Shared game domain:** `shared/Synapse.Blocks.Core` contains the block-program model and execution/evaluation rules used by both Blazor and the API, avoiding divergent evaluator implementations.
- **Database:** PostgreSQL stores identities, teacher-owned content, immutable content versions, QR links, participants, and attempts.
- **Local deployment:** Docker Compose runs separate `teacher-frontend`, `student-frontend`, `api`, `postgres`, and `gateway` containers. PostgreSQL data uses a named persistent volume. Runtime secrets and database settings come from environment configuration and are not committed.
- **Routing:** an Nginx gateway routes `/admin/*` to the teacher container, `/api/*` to the API container, and student routes including `/case/*` and `/game` to the Blazor container. This keeps all apps under one origin for cookie authentication.

### Authentication and ownership

- Use ASP.NET Core Identity for teacher accounts and secure HTTP-only cookies for the teacher application.
- Platform administrators provision accounts through an invitation flow. Invitation tokens are single-use and expire. The exact platform-administrator bootstrap mechanism is an implementation detail to settle in the implementation plan.
- Use antiforgery protection for cookie-authenticated state-changing requests.
- Every teacher-owned entity has an owner identity. Every read, update, delete, and relationship lookup is scoped to the authenticated owner on the server; client-supplied owner IDs are never trusted.
- Student endpoints do not require teacher login. They require a valid, active QR token and only expose the case content needed to play and submit that case.

### QR access

- Each case may have one active share link represented by a cryptographically random, unguessable token. Store a hash of the token where practical; never use a sequential database ID as the public credential.
- QR resolution returns only the active case's student-facing content.
- Teachers can deactivate or rotate a link. Deactivation prevents new attempts and further content access; existing results remain visible to the owner.
- The public token is treated as a bearer credential and must not expose teacher data or management endpoints.

## Domain model

- **Teacher:** Identity account owning content and reports.
- **Level:** teacher-owned editable level metadata and one or more immutable content versions. A version contains the game definition, allowed and required blocks, intro content, and test cases.
- **Case:** teacher-owned ordinary case with a title, description, publication state, ordered references to specific level versions, and an optional share link.
- **Case item:** one ordered reference from a case to a level version.
- **Share link:** case reference, token hash, active state, creation and revocation timestamps.
- **Participant:** case-scoped student display name plus a random browser continuation identifier. A student does not create an account. Name matching is case/whitespace normalized for best-progress grouping; preserve the entered display spelling for reports.
- **Attempt:** one student's run through one case, with started/completed timestamps, status, and per-level progress records. Multiple attempts are allowed for the same name and case.
- **Attempt level result:** level-version reference, completion state, and relevant submission/progress data for that level.

Case items point to immutable level versions. Editing a level creates a new version. Existing published cases and attempts keep their pinned version until a teacher explicitly updates the case to the new version. This prevents edits from silently changing active or historical work.

## Main flows

### Teacher creates and publishes a case

1. Teacher signs in through the Refine application.
2. Teacher creates or edits a level and its test cases in their private catalog.
3. Teacher creates a case, selects level versions, and orders them.
4. Teacher publishes the case and creates or activates a share link.
5. The panel displays the QR code, URL, and active/closed state.

### Student completes a case

1. Student scans the QR code or opens the case URL.
2. API validates the share token and returns student-facing case content.
3. Student enters a display name and starts a new attempt. A random browser identifier can find an unfinished attempt for continuation on the same device; starting a new attempt remains available.
4. Blazor loads one level at a time and uses the shared C# domain library for responsive local feedback. It submits the block program to the API for authoritative evaluation.
5. The API runs all public and hidden checks using the shared evaluator, returns public-test feedback and a pass/fail result without disclosing hidden expected outputs, and records completion only when all checks pass. A client cannot mark an arbitrary level complete without satisfying the level's checks.
6. Student sees completion for the case; server history remains available to the teacher.

### Teacher reviews results

- The case report lists participant display names, all attempts, attempt status, and completed levels.
- Best progress for a normalized participant name is the maximum number of completed case levels across that participant's attempts. Attempts tied at the maximum remain in history; do not invent a secondary score based on time or number of program runs.
- Reports are scoped to the owning teacher and case.

## API boundaries

The API should expose separate authenticated teacher routes and anonymous QR-scoped student routes. Exact route names and DTOs belong in the implementation plan. The API must enforce ownership and validate all level, case, and attempt relationships, regardless of frontend behavior.

Teacher operations include account/session management, level CRUD and version history, test-case editing, case CRUD and ordered level selection, publication/share-link creation or revocation, and attempt/report queries.

Student operations include case resolution by token, participant/attempt creation or continuation, public level content retrieval for the attempt, and block-program evaluation/progress submission. Student responses expose neither teacher identity credentials nor unrelated cases, catalog content, or hidden tests/expected outputs. Hidden evaluation runs on the API using the shared game domain library.

## Data and validation rules

- Validate level definitions and test cases on the server before publishing a version.
- A case must contain at least one valid level version; ordering is explicit and stable.
- An attempt is bound to the case version list captured when it starts. Editing a case does not mutate an in-progress attempt.
- Progress is monotonic within an attempt: completed levels cannot become incomplete due to a retry or refresh.
- Enforce ownership in service/data access boundaries, not only in UI filters.
- Do not send hidden tests or expected outputs to student clients. The API owns authoritative evaluation; public tests may still be shown to the student. The shared game domain library keeps local feedback and server evaluation consistent.
- Define data retention and deletion behavior before production use; the first release stores names and learning results but does not require student accounts.

## Error handling and operational behavior

- Invalid, revoked, or unknown QR links show a student-friendly unavailable page without revealing whether a token was formerly valid.
- A network interruption must not mark a level complete. The student can retry submission; submission handling should be idempotent for the same attempt and level result.
- Teacher authorization failures return standard unauthenticated/forbidden responses without leaking whether another teacher owns an entity.
- Database migrations run as part of a controlled startup/deployment step; PostgreSQL data survives container restarts through its persistent volume.
- Log operational errors without logging passwords, raw invitation tokens, raw QR tokens, or unnecessary student data.

## Delivery phases

### Phase 1: hosted platform foundation

- Establish separate `frontend/`, `backend/`, and `shared/` project folders and Docker build contexts for ASP.NET Core API, current Blazor student app, and Refine teacher app.
- Add PostgreSQL persistence and Docker Compose configuration.
- Implement Identity, invitation provisioning, owner-scoped data access, and migrations.
- Move level catalog access behind the API and preserve existing game behavior.
- Add level versions, case, share-link, participant, and attempt domain models needed for the full flow.

### Phase 2: authoring and QR learning loop

- Deliver teacher level/test-case editor and ordinary case builder.
- Deliver case publication, QR/share URL generation, revocation, and rotation.
- Connect student name entry, repeated attempts, server-side progress, and continuation.
- Deliver teacher reports with complete attempt history and best progress by completed-level count.
- Add integration and UI verification for the end-to-end scenario.

The phases are implementation sequencing, not separate product definitions: completion means the whole create-to-report scenario works.

## Acceptance criteria

1. A teacher can sign in only after account provisioning and cannot access another teacher's levels, cases, links, or reports by changing request IDs.
2. A teacher can create/edit a level and its tests, assemble an ordered case, and publish a QR link.
3. A student scanning an active QR can enter a name and start an attempt without a student account.
4. Students can make multiple attempts; progress is persisted and can be continued on the same device.
5. Only verified level completion is recorded. Hidden expected outputs are not exposed to the browser.
6. The teacher can see all attempts and the maximum number of completed levels for each normalized name and case.
7. Editing a level or case does not silently alter a running or historical attempt.
8. Revoking a QR link blocks new access while preserving historical results.
9. Docker Compose starts separate teacher frontend, student frontend, API, PostgreSQL, and gateway containers with persistent database storage and environment-based configuration.
10. Existing block-program functionality remains available in the student experience.

## Verification approach

During implementation, add focused automated checks for ownership isolation, version pinning, QR token lifecycle, attempt idempotency, monotonic progress, and best-progress calculation. Verify migrations and container startup, then walk through the full teacher-to-student-to-report flow in a browser. Compare representative existing levels in the new hosted flow against the current game behavior. No verification is claimed by this design document; evidence will be reported after implementation.
