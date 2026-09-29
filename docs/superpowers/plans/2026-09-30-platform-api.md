# Synapse Blocks Platform API Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the ASP.NET Core/PostgreSQL platform API that securely stores teacher-owned levels, ordinary cases, QR links, and student attempt progress while reusing the existing C# game evaluator.

**Architecture:** Extract the level models and block evaluator into a shared C# library. Add an ASP.NET Core API with EF Core/Npgsql, Identity cookie authentication, owner-scoped teacher endpoints, anonymous QR-scoped student endpoints, immutable level versions, and server-authoritative evaluation. Docker Compose provides the API and persistent PostgreSQL service.

**Tech Stack:** .NET 10, ASP.NET Core, ASP.NET Core Identity, EF Core 10, Npgsql, PostgreSQL, Docker Compose, xUnit.

**Spec:** `docs/superpowers/specs/2026-09-30-teacher-qr-platform-design.md`

## Global Constraints

- The repository is a .NET 10 Blazor WebAssembly application.
- Use ASP.NET Core Identity for teacher accounts and secure HTTP-only cookies for the teacher application.
- Use antiforgery protection for cookie-authenticated state-changing requests.
- Every teacher-owned entity has an owner identity. Every read, update, delete, and relationship lookup is scoped to the authenticated owner on the server; client-supplied owner IDs are never trusted.
- Each case may have one active share link represented by a cryptographically random, unguessable token. Store a hash of the token where practical; never use a sequential database ID as the public credential.
- Case items point to immutable level versions. Editing a level creates a new version.
- Do not send hidden tests or expected outputs to student clients. The API owns authoritative evaluation; public tests may still be shown to the student.
- PostgreSQL data uses a named persistent volume. Runtime secrets and database settings come from environment configuration and are not committed.
- The public token is treated as a bearer credential and must not expose teacher data or management endpoints.

## Review Focus

- A malformed or oversized level/program payload must be rejected with a bounded, actionable validation response; pin in Task 6.
- A teacher guessing another teacher's level, case, QR, or attempt ID must receive no data and cause no mutation; pin in Task 3 and verify across Tasks 4, 5, and 7.
- An unknown, revoked, or rotated QR token must not start attempts or disclose whether the link used to exist; pin in Task 5.
- Hidden test cases and expected outputs must remain server-only, including in error responses and logs; pin in Task 6.
- Duplicate submissions and same-name attempts must preserve attempt history; pin in Task 6. Equal best-progress ties compute best progress solely by completed-level count; pin in Task 7.

---

### Task 1: Extract the shared game domain and evaluator

**Files:**
- Create: `src/Synapse.Blocks.Core/Synapse.Blocks.Core.csproj`
- Move: `Models/GameModels.cs` to `src/Synapse.Blocks.Core/Models/GameModels.cs`
- Move: `Services/BlockProgramRunner.cs` to `src/Synapse.Blocks.Core/Services/BlockProgramRunner.cs`
- Modify: `Synapse.Blocks.csproj`
- Modify: `Synapse.Blocks.slnx`
- Create: `tests/Synapse.Blocks.Core.Tests/Synapse.Blocks.Core.Tests.csproj`
- Create: `tests/Synapse.Blocks.Core.Tests/BlockProgramRunnerTests.cs`

**Interfaces:**
- Consumes: existing `LevelDefinition`, `BlockProgram`, `ProgramExecution`, `TestRunResult` and `BlockProgramRunner.Validate(BlockProgram)`, `Run(BlockProgram, string)`, `RunAll(BlockProgram, LevelDefinition)`.
- Produces: `Synapse.Blocks.Core` with the same namespaces and public model/evaluator signatures, referenced by both the existing Blazor app and the API.

- [ ] **Step 1: Write failing compatibility tests** for `RunAll` using representative existing fixtures for arithmetic, condition, loop, and variable programs. Assert `Passed`, `Actual`, `Error`, and visited-node output matches current runner behavior.
- [ ] **Step 2: Run the focused test** with `dotnet test tests/Synapse.Blocks.Core.Tests/Synapse.Blocks.Core.Tests.csproj`; confirm the tests fail because the new project/types do not exist.
- [ ] **Step 3: Move models and runner into `Synapse.Blocks.Core`**, preserve namespaces, reference the project from the Blazor app, and add the project to the solution.
- [ ] **Step 4: Run focused tests and build the existing app** with `dotnet test tests/Synapse.Blocks.Core.Tests/Synapse.Blocks.Core.Tests.csproj` and `dotnet build Synapse.Blocks.csproj`; expect both to pass without behavior changes.
- [ ] **Step 5: Commit** the shared-domain extraction.

### Task 2: Create API, persistence, and local runtime foundation

**Files:**
- Create: `src/Synapse.Blocks.Api/Synapse.Blocks.Api.csproj`
- Create: `src/Synapse.Blocks.Api/Program.cs`
- Create: `src/Synapse.Blocks.Api/Dockerfile`
- Create: `src/Synapse.Blocks.Api/appsettings.json`
- Create: `src/Synapse.Blocks.Api/Data/AppDbContext.cs`
- Create: `src/Synapse.Blocks.Api/Data/Entities/` for persistence-only entities
- Create: `src/Synapse.Blocks.Api/Health/` for readiness checks
- Create: `tests/Synapse.Blocks.Api.Tests/Synapse.Blocks.Api.Tests.csproj`
- Create: `tests/Synapse.Blocks.Api.Tests/Infrastructure/ApiWebApplicationFactory.cs`
- Create: `tests/Synapse.Blocks.Api.Tests/Infrastructure/PostgreSqlFixture.cs`
- Create: `docker-compose.yml`
- Create: `.dockerignore`
- Modify: `Synapse.Blocks.slnx`
- Modify: `.gitignore`

**Interfaces:**
- Consumes: `Synapse.Blocks.Core` from Task 1.
- Produces: API host configured from `ConnectionStrings:Default`, `AppDbContext`, PostgreSQL health/readiness, and an integration-test host using `Testcontainers.PostgreSql` for a real PostgreSQL instance.

- [ ] **Step 1: Write a failing API smoke test** that starts `ApiWebApplicationFactory`, requests `GET /health/ready`, and expects HTTP 200 only when the database is reachable.
- [ ] **Step 2: Run the smoke test** with `dotnet test tests/Synapse.Blocks.Api.Tests/Synapse.Blocks.Api.Tests.csproj`; confirm it fails before the API project exists.
- [ ] **Step 3: Add the API host, EF Core/Npgsql context, health check, and Testcontainers fixture.** Keep API persistence entities separate from game DTOs; store each immutable level version's serialized `LevelDefinition` as PostgreSQL `jsonb`.
- [ ] **Step 4: Add Docker Compose** with API and PostgreSQL services, a named database volume, health dependency, and environment-only credentials. Do not put development secrets in tracked files.
- [ ] **Step 5: Run the smoke test and container checks.** Run the focused `dotnet test`; run `docker compose config` and `docker compose up --build -d`, then verify `GET /health/ready` returns 200 and PostgreSQL data remains after restarting the database container.
- [ ] **Step 6: Commit** the API and database foundation.

### Task 3: Add teacher authentication, invitations, and owner scoping

**Files:**
- Create: `src/Synapse.Blocks.Api/Auth/ApplicationUser.cs`
- Create: `src/Synapse.Blocks.Api/Auth/Invitation.cs`
- Create: `src/Synapse.Blocks.Api/Auth/InvitationService.cs`
- Create: `src/Synapse.Blocks.Api/Auth/BootstrapPlatformAdmin.cs`
- Create: `src/Synapse.Blocks.Api/Endpoints/AuthEndpoints.cs`
- Create: `src/Synapse.Blocks.Api/Endpoints/PlatformAdminEndpoints.cs`
- Modify: `src/Synapse.Blocks.Api/Data/AppDbContext.cs`
- Modify: `src/Synapse.Blocks.Api/Program.cs`
- Create: `tests/Synapse.Blocks.Api.Tests/Auth/InvitationFlowTests.cs`
- Create: `tests/Synapse.Blocks.Api.Tests/Auth/CookieAndCsrfTests.cs`

**Interfaces:**
- Produces: `POST /api/auth/sign-in`, `POST /api/auth/sign-out`, `GET /api/auth/me`, `GET /api/auth/csrf`; platform-admin-only `POST /api/platform/invitations` and `POST /api/platform/invitations/accept`.
- `GET /api/auth/me` returns `{ userId, email, roles }`; `GET /api/auth/csrf` sets the antiforgery cookie and returns `{ requestToken }` for the `RequestVerificationToken` mutation header.
- `InvitationService.CreateAsync(string email, DateTimeOffset expiresAt, string invitedByUserId)` returns a single-use invitation URL token; persist only a token hash. `AcceptAsync(string token, string password)` creates the teacher account once and consumes the invitation.
- Invitation URLs expire after 7 days. Bootstrap the first platform administrator from `BootstrapAdmin__Email` and `BootstrapAdmin__Password` environment settings; reject empty production values.

- [ ] **Step 1: Write failing invitation tests** for valid accept, expired token, reused token, email mismatch, and hashed-token storage; write cookie/CSRF tests for anonymous state-changing requests.
- [ ] **Step 2: Run the focused tests** with `dotnet test tests/Synapse.Blocks.Api.Tests/Synapse.Blocks.Api.Tests.csproj --filter "FullyQualifiedName~InvitationFlowTests|FullyQualifiedName~CookieAndCsrfTests"`; confirm the new endpoints are missing.
- [ ] **Step 3: Implement Identity cookie auth and invitations** with one-time expiring tokens, a platform-admin role, environment bootstrap, HTTP-only secure cookies, same-site policy, and antiforgery validation on unsafe cookie-authenticated requests.
- [ ] **Step 4: Add ownership primitives**: owner ID foreign keys for teacher-owned entities, a required current-user accessor for authenticated endpoints, and a query pattern that filters by owner before loading or mutating a resource.
- [ ] **Step 5: Run focused auth tests** and verify one teacher cannot distinguish an unknown account/resource from another teacher's private data.
- [ ] **Step 6: Commit** authentication and account provisioning.

### Task 4: Implement teacher level catalog and immutable versions

**Files:**
- Create: `src/Synapse.Blocks.Api/Data/Entities/TeacherLevel.cs`
- Create: `src/Synapse.Blocks.Api/Data/Entities/LevelVersion.cs`
- Create: `src/Synapse.Blocks.Api/Levels/LevelDefinitionValidator.cs`
- Create: `src/Synapse.Blocks.Api/Levels/LevelService.cs`
- Create: `src/Synapse.Blocks.Api/Endpoints/TeacherLevelEndpoints.cs`
- Create: `src/Synapse.Blocks.Api/Contracts/Levels/` request and response DTOs
- Modify: `src/Synapse.Blocks.Api/Data/AppDbContext.cs`
- Create: `src/Synapse.Blocks.Api/Data/Migrations/`
- Create: `tests/Synapse.Blocks.Api.Tests/Levels/TeacherLevelEndpointsTests.cs`
- Create: `tests/Synapse.Blocks.Api.Tests/Levels/LevelVersionTests.cs`

**Interfaces:**
- `GET /api/teacher/levels`, `POST /api/teacher/levels`, `GET /api/teacher/levels/{levelId}`, `PUT /api/teacher/levels/{levelId}`.
- `TeacherLevelDto { id, title, currentVersionId, definition }`; create/update body `{ definition: LevelDefinition }`. `GET /api/teacher/levels` returns the current user's definitions only.
- `LevelService.CreateAsync(string ownerId, LevelDefinition definition) -> TeacherLevelDto`; `UpdateAsync(string ownerId, Guid levelId, Guid expectedVersionId, LevelDefinition definition) -> TeacherLevelDto`; `GetVersionsAsync(string ownerId, Guid levelId) -> IReadOnlyList<LevelVersionDto>`.
- Update request body is `{ expectedVersionId, definition }`; return HTTP 409 without mutation if the current version differs.
- Updating creates the next immutable version; all definitions pass `LevelDefinitionValidator` before publication.

- [ ] **Step 1: Write failing level tests** for valid creation, invalid definition rejection, version increments, stale-version HTTP 409 without mutation, and cross-owner GET/PUT returning not-found without mutation.
- [ ] **Step 2: Run the focused level tests** and confirm the routes/handlers do not exist.
- [ ] **Step 3: Implement entities, validator, service, routes, and the first EF migration.** Preserve all fields in `LevelDefinition`, `LevelIntroStep`, and `LevelTestCase` through the serialized version snapshot.
- [ ] **Step 4: Seed existing `wwwroot/levels.json` entries as reusable starter definitions** that are copied into a teacher's catalog on first access; starter entries are templates, not shared editable ownerless rows.
- [ ] **Step 5: Run level and migration tests**; assert a foreign owner cannot read or change the definition, including by supplying a version ID directly.
- [ ] **Step 6: Commit** level authoring APIs and version storage.

### Task 5: Implement ordinary cases and QR share-link lifecycle

**Files:**
- Create: `src/Synapse.Blocks.Api/Data/Entities/TeacherCase.cs`
- Create: `src/Synapse.Blocks.Api/Data/Entities/CaseLevel.cs`
- Create: `src/Synapse.Blocks.Api/Data/Entities/ShareLink.cs`
- Create: `src/Synapse.Blocks.Api/Cases/CaseService.cs`
- Create: `src/Synapse.Blocks.Api/Cases/ShareLinkService.cs`
- Create: `src/Synapse.Blocks.Api/Endpoints/TeacherCaseEndpoints.cs`
- Create: `src/Synapse.Blocks.Api/Endpoints/StudentCaseEndpoints.cs`
- Create: `src/Synapse.Blocks.Api/Contracts/Cases/` request and response DTOs
- Modify: `src/Synapse.Blocks.Api/Data/AppDbContext.cs`
- Create: `tests/Synapse.Blocks.Api.Tests/Cases/CaseOwnershipTests.cs`
- Create: `tests/Synapse.Blocks.Api.Tests/Cases/CaseArchiveTests.cs`
- Create: `tests/Synapse.Blocks.Api.Tests/Cases/ShareLinkLifecycleTests.cs`

**Interfaces:**
- Teacher routes: `GET|POST /api/teacher/cases`, `GET|PUT|DELETE /api/teacher/cases/{caseId}`, `POST /api/teacher/cases/{caseId}/publish`, `POST /api/teacher/cases/{caseId}/share-link`, `DELETE /api/teacher/cases/{caseId}/share-link`.
- Public route: `GET /api/student/cases/{token}` returns only published student-facing case metadata, ordered pinned level versions, and public tests.
- Student case response is `{ id, caseType, title, levels: [{ levelVersionId, order, definition }] }`; `caseType` is `"orderedLevels"` and `definition.Tests` contains only tests with `Hidden == false`.
- Teacher case response is `{ id, caseType, title, description, isPublished, archived, levels: [{ levelId, levelVersionId, order, title }], shareLinkActive }`; create/update body is `{ caseType: "orderedLevels", title, description, orderedLevelVersionIds: string[] }`.
- `CaseService.CreateAsync(string ownerId, CreateCaseRequest request) -> TeacherCaseDto`; `PublishAsync(string ownerId, Guid caseId) -> TeacherCaseDto`.
- `ShareLinkService.CreateOrRotateAsync(string ownerId, Guid caseId) -> ShareLinkCreatedDto` returns the raw token once; persist SHA-256 token hash and compare hashes in constant time.
- Case items contain a stable order and explicit level-version ID; reject empty cases and foreign-owner or duplicate level references.
- Store `caseType: "orderedLevels"` as a discriminator in persistence and teacher/student DTOs; accept only this type in the first release.
- A case contains 1–100 levels. A level definition request is limited to 5 MiB; an evaluation request is limited to 256 KiB, 500 nodes, and 1,000 connections. Reject over-limit requests before execution with HTTP 413 or a field validation response.

- [ ] **Step 1: Write failing case tests** for ordered level selection, empty case rejection, the 1–100 level bound, pinned version IDs, ownership checks on every operation, archiving with preserved results, QR token unguessability/one-time display, and revocation/rotation.
- [ ] **Step 2: Run focused case tests** and confirm the route set is absent.
- [ ] **Step 3: Implement case services and routes** with owner-filtered queries and explicit publication state.
- [ ] **Step 4: Implement token generation and resolution** using at least 256 random bits and stored hashes; revoked, unknown, and unpublished cases all return the same public unavailable response.
- [ ] **Step 5: Run case and QR tests**; confirm historical case results are not deleted when the share link is deactivated.
- [ ] **Step 6: Commit** case composition and QR access.

### Task 6: Implement student participants, attempts, and authoritative evaluation

**Files:**
- Create: `src/Synapse.Blocks.Api/Data/Entities/Participant.cs`
- Create: `src/Synapse.Blocks.Api/Data/Entities/Attempt.cs`
- Create: `src/Synapse.Blocks.Api/Data/Entities/AttemptLevelResult.cs`
- Create: `src/Synapse.Blocks.Api/Students/StudentNameNormalizer.cs`
- Create: `src/Synapse.Blocks.Api/Students/AttemptService.cs`
- Create: `src/Synapse.Blocks.Api/Students/ProgramEvaluationService.cs`
- Create: `src/Synapse.Blocks.Api/Endpoints/StudentAttemptEndpoints.cs`
- Create: `src/Synapse.Blocks.Api/Contracts/Students/` request and response DTOs
- Modify: `src/Synapse.Blocks.Api/Data/AppDbContext.cs`
- Create: `tests/Synapse.Blocks.Api.Tests/Students/AttemptLifecycleTests.cs`
- Create: `tests/Synapse.Blocks.Api.Tests/Students/HiddenTestPrivacyTests.cs`
- Create: `tests/Synapse.Blocks.Api.Tests/Students/ProgressMonotonicityTests.cs`

**Interfaces:**
- `POST /api/student/cases/{token}/participants` accepts `{ displayName }`; server sets a random HTTP-only, same-site continuation cookie and returns an opaque participant ID.
- `displayName` must contain 1–80 characters after trimming. The continuation cookie contains 256 random bits, is HTTP-only and same-site, and only its hash is persisted.
- `POST /api/student/cases/{token}/attempts` creates a distinct attempt for the participant; `GET /api/student/cases/{token}/attempts/current` resumes an unfinished attempt for that participant; `POST /api/student/cases/{token}/attempts/{attemptId}/levels/{levelVersionId}/evaluate` accepts a `BlockProgram`.
- `ProgramEvaluationService.EvaluateAsync(Guid attemptId, Guid levelVersionId, BlockProgram program) -> LevelEvaluationDto` runs `BlockProgramRunner.Validate` and all tests from the pinned level version. Response shape is `{ passed, publicTestResults: [{ testId, name, passed, input, expected, actual, error }] }`; it contains public test feedback and an authoritative `passed` flag, never hidden expected outputs.
- Name normalization trims and collapses whitespace and compares case-insensitively; original display spelling remains reportable.

- [ ] **Step 1: Write failing student tests** for required name, same-device continuation, explicit new attempt, QR/case/version membership checks, 5 MiB level and 256 KiB evaluation request limits, 500-node and 1,000-connection caps, public test feedback, hidden test secrecy in serialized responses and captured logs, and no-complete on any failed hidden or public case.
- [ ] **Step 2: Run focused student tests** and confirm participant/attempt endpoints are missing.
- [ ] **Step 3: Implement participant and attempt persistence.** Hash the continuation cookie identifier before storage; scope participant identity to the case and normalized display name.
- [ ] **Step 4: Implement authoritative evaluation** with size/node/connection limits before execution, the existing 10,000-step runner cap, server-only expected outputs, and idempotent result upsert keyed by `(AttemptId, LevelVersionId)`.
- [ ] **Step 5: Enforce monotonic progress**: a completed level remains completed, and an attempt advances only through the case's next ordered level.
- [ ] **Step 6: Run focused student/privacy tests**; inspect serialized API output and captured logs for hidden expected values and test malicious cross-case submissions.
- [ ] **Step 7: Commit** student attempt and evaluation endpoints.

### Task 7: Implement owner-scoped reports, complete migrations, and API verification

**Files:**
- Create: `src/Synapse.Blocks.Api/Reports/CaseReportService.cs`
- Create: `src/Synapse.Blocks.Api/Endpoints/TeacherReportEndpoints.cs`
- Create: `src/Synapse.Blocks.Api/Contracts/Reports/CaseReportDto.cs`
- Create: `src/Synapse.Blocks.Api/Operations/CaseArchiveService.cs`
- Modify: `src/Synapse.Blocks.Api/Data/Migrations/`
- Create: `tests/Synapse.Blocks.Api.Tests/Reports/BestProgressTests.cs`
- Create: `tests/Synapse.Blocks.Api.Tests/Reports/ReportOwnershipTests.cs`
- Modify: `README.md`

**Interfaces:**
- `GET /api/teacher/cases/{caseId}/report` returns `{ caseId, participants: [{ displayName, bestProgress, attempts: [{ attemptId, status, startedAt, completedAt, completedLevelCount }] }] }`.
- `CaseReportService.GetAsync(string ownerId, Guid caseId) -> CaseReportDto` calculates best progress as `Max(completed level count)`; tied attempts remain listed, with no time/run-count tie-break.
- `DELETE /api/teacher/cases/{caseId}` archives a case, deactivates its QR link, and preserves attempts/reports; it never cascades deletion into student history.
- Readiness route: `GET /health/ready` fails when PostgreSQL is unavailable or unapplied required migrations prevent serving requests.

- [ ] **Step 1: Write failing report tests** for maximum progress across repeated attempts, equal best ties, normalized duplicate names, empty reports, and cross-owner report isolation.
- [ ] **Step 2: Run focused report tests** and confirm reporting routes are not implemented.
- [ ] **Step 3: Implement report aggregation and final migration configuration.** Ensure all query paths filter by case owner before aggregating attempts.
- [ ] **Step 3a: Implement case archiving** as a reversible/visible archived state in teacher data, with share-link deactivation and retained attempt records.
- [ ] **Step 4: Run the API test suite** with `dotnet test tests/Synapse.Blocks.Api.Tests/Synapse.Blocks.Api.Tests.csproj` and core tests with `dotnet test tests/Synapse.Blocks.Core.Tests/Synapse.Blocks.Core.Tests.csproj`.
- [ ] **Step 5: Verify clean deployment** with `docker compose down -v`, `docker compose up --build -d`, migration application, `/health/ready`, invitation sign-in, API authoring, QR resolution, evaluation, and reports. Do not use production data.
- [ ] **Step 6: Update README** with local environment keys, bootstrap admin setup, Docker Compose commands, invitation flow, and API test commands; state that secrets are supplied out of band.
- [ ] **Step 7: Commit** reporting and deployment documentation.

Before production enablement, document the retention and deletion policy with the platform owner. The first release performs no automatic attempt purging; archiving cases disables new access while preserving history.
