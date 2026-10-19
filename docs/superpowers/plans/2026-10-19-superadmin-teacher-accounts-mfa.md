# Superadmin, Teacher Accounts, and Mandatory 2FA Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver a separate superadmin workspace for teacher accounts and all teacher case sets, require authenticator-based 2FA for every teacher and superadmin, and refresh the admin visual system.

**Architecture:** Keep the existing ASP.NET Core Identity roles, PostgreSQL database, cookie authentication, and Refine/React application. Add server-enforced active-account and MFA policies, explicit `/api/platform/*` services for teacher and case administration, and role-specific pages in the existing frontend. Preserve teacher owner scoping and route superadmin case operations through owner-aware services.

**Tech Stack:** .NET 10, ASP.NET Core Identity, EF Core/Npgsql, React 19, Refine, Ant Design 5, TypeScript, existing cookie/antiforgery setup.

**Spec:** `docs/superpowers/specs/2026-10-19-superadmin-teacher-accounts-mfa-design.md`

## Global Constraints

- Keep the `PlatformAdmin` and `Teacher` roles distinct; teacher invitations assign only `Teacher`.
- TOTP must be compatible with Google Authenticator and other standard authenticator apps; this is not Google OAuth.
- Require TOTP setup before a new account reaches its workspace, require TOTP or an unused recovery code at every later sign-in, and do not provide a password-only API path.
- Preserve teacher ownership for every level, case, attempt, and report; never trust a client-supplied owner ID without server validation.
- Keep the existing cookie authentication and antiforgery protections for state-changing requests.
- Do not physically delete teacher accounts or their data.
- Superadmin case management must preserve the owner and use only that owner's levels; superadmin does not edit level definitions.
- Use a warm near-white canvas, graphite navigation, and `#F36F29` accent; retain the existing frontend stack.
- Invitation and password-reset links are copied by the superadmin; do not add email delivery.
- Do not log passwords, TOTP secrets/codes, recovery codes, invitation tokens, or password-reset tokens.

## Review Focus

- **Password-only access after login, invitation acceptance, or MFA reset:** confirm the unconfigured state can reach only MFA setup and not teacher/platform APIs.
- **Blocked or reset account with an existing cookie:** confirm the next protected request rejects the session.
- **Cross-owner case edits and level IDs:** confirm the service rejects a level version from a different owner and never changes the case owner.
- **Invitation, password-reset, and recovery-code reuse:** confirm consumed/revoked/expired credentials cannot be replayed.
- **Migration of existing accounts and learning data:** confirm profile defaults, roles, existing MFA state, case ownership, QR links, attempts, and reports remain intact.

## File Map

- `backend/Synapse.Blocks.Api/Auth/ApplicationUser.cs` — teacher profile and active-account state.
- `backend/Synapse.Blocks.Api/Auth/Invitation.cs` — invitation revocation state.
- `backend/Synapse.Blocks.Api/Auth/PlatformAuditEvent.cs` — security/action audit record.
- `backend/Synapse.Blocks.Api/Auth/InvitationService.cs` — invitation creation, revocation, and first-use profile capture.
- `backend/Synapse.Blocks.Api/Auth/TeacherAccountService.cs` — directory, block/reactivate, password-reset link, and MFA-reset operations.
- `backend/Synapse.Blocks.Api/Auth/AuthenticatorService.cs` — TOTP enrollment, confirmation, challenge, and recovery codes.
- `backend/Synapse.Blocks.Api/Auth/ActiveMfaRequirement.cs` — server policy for active users with enrolled MFA.
- `backend/Synapse.Blocks.Api/Auth/PlatformAuditEventService.cs` — secret-free audit writes.
- `backend/Synapse.Blocks.Api/Endpoints/AuthEndpoints.cs` — password, MFA, recovery, and identity state endpoints.
- `backend/Synapse.Blocks.Api/Endpoints/PlatformAdminEndpoints.cs` — invitation and teacher-management endpoints.
- `backend/Synapse.Blocks.Api/Endpoints/PlatformCaseEndpoints.cs` — superadmin-only case-set routes.
- `backend/Synapse.Blocks.Api/Cases/PlatformCaseService.cs` — owner-visible global case queries and owner-safe delegation to `CaseService`.
- `backend/Synapse.Blocks.Api/Contracts/Auth/PlatformAuthDtos.cs` — auth, enrollment, and account DTOs.
- `backend/Synapse.Blocks.Api/Contracts/Cases/PlatformCaseDtos.cs` — case DTOs that include owner identity.
- `backend/Synapse.Blocks.Api/Data/AppDbContext.cs` — account and audit mapping.
- `backend/Synapse.Blocks.Api/Program.cs` — Identity, cookie validation, policy registration, service registration, and endpoint maps.
- `backend/Synapse.Blocks.Api/Endpoints/TeacherLevelEndpoints.cs`, `TeacherCaseEndpoints.cs`, and `TeacherReportEndpoints.cs` — apply the active-MFA policy to all teacher-only management routes.
- `backend/Synapse.Blocks.Api/Data/Migrations/` — generated additive migration and model snapshot.
- `frontend/teacher/src/App.tsx` — role-aware routes, resources, and role-specific landing pages.
- `frontend/teacher/src/style.css` — shared warm/graphite/orange visual tokens and responsive layout.
- `frontend/teacher/src/providers/authProvider.ts` and `apiClient.ts` — staged sign-in and cookie/CSRF behavior.
- `frontend/teacher/src/types/api.ts` — identity, teacher account, invitation, and platform case contracts.
- `frontend/teacher/src/pages/login.tsx`, `invitations/accept.tsx`, and `platform/invitations.tsx` — sign-in, onboarding, and invitation flows.
- `frontend/teacher/src/pages/authenticator-setup.tsx`, `two-factor-challenge.tsx`, and `recovery-codes.tsx` — mandatory 2FA screens.
- `frontend/teacher/src/pages/security.tsx` — signed-in recovery-code regeneration for both roles.
- `frontend/teacher/src/pages/password-reset.tsx` — completion of a superadmin-issued password-reset link.
- `frontend/teacher/src/pages/platform/teachers.tsx` — superadmin teacher directory and security actions.
- `frontend/teacher/src/pages/platform/cases/list.tsx` and `edit.tsx` — global case management.
- `frontend/teacher/src/pages/platform/dashboard.tsx` — superadmin overview.

---

### Task 1: Persist Teacher Profile, Account State, and Platform Audit

**Files:**
- Modify: `backend/Synapse.Blocks.Api/Auth/ApplicationUser.cs`
- Modify: `backend/Synapse.Blocks.Api/Auth/Invitation.cs`
- Create: `backend/Synapse.Blocks.Api/Auth/PlatformAuditEvent.cs`
- Modify: `backend/Synapse.Blocks.Api/Data/AppDbContext.cs`
- Create: generated migration, migration designer, and update `AppDbContextModelSnapshot.cs`

**Interfaces:**
- Produces: nullable `FirstName`, `LastName`, and `TeacherDescription`; `IsActive` defaulting to `true`; `Invitation.RevokedAt`; `PlatformAuditEvent` with actor ID, action, target type/ID, and timestamp.
- Existing `DisplayName`, roles, owner IDs, and MFA fields remain unchanged.

- [x] Add the profile/status fields and audit entity; configure nullable profile columns, active-by-default behavior, bounded lengths, invitation revocation, and audit indexes in `AppDbContext`.
- [x] Generate an additive PostgreSQL migration that leaves all existing accounts active, leaves their current `TwoFactorEnabled` state intact, and does not rewrite content or attempt data.
- [x] Build `backend/Synapse.Blocks.Api/Synapse.Blocks.Api.csproj` and inspect the migration/model snapshot diff for unintended destructive operations.

### Task 2: Enforce Active Account and Mandatory Authenticator 2FA

**Files:**
- Create: `backend/Synapse.Blocks.Api/Auth/ActiveMfaRequirement.cs`
- Create: `backend/Synapse.Blocks.Api/Auth/AuthenticatorService.cs`
- Create: `backend/Synapse.Blocks.Api/Contracts/Auth/PlatformAuthDtos.cs`
- Modify: `backend/Synapse.Blocks.Api/Endpoints/AuthEndpoints.cs`
- Modify: `backend/Synapse.Blocks.Api/Endpoints/TeacherLevelEndpoints.cs`
- Modify: `backend/Synapse.Blocks.Api/Endpoints/TeacherCaseEndpoints.cs`
- Modify: `backend/Synapse.Blocks.Api/Endpoints/TeacherReportEndpoints.cs`
- Modify: `backend/Synapse.Blocks.Api/Endpoints/PlatformAdminEndpoints.cs`
- Modify: `backend/Synapse.Blocks.Api/Program.cs`

**Interfaces:**
- Produces: `POST /api/auth/sign-in` returns `nextStep` as `authenticated`, `twoFactor`, or `authenticatorSetup`.
- Produces: `GET /api/auth/authenticator/setup`, `POST /api/auth/authenticator/confirm`, `POST /api/auth/two-factor/verify`, `POST /api/auth/recovery-codes/sign-in`, and `POST /api/auth/recovery-codes/regenerate`.
- Produces: `POST /api/auth/password-reset/complete` accepts email, reset token, and new password from a superadmin-issued link.
- Produces: `/api/auth/me` includes roles, profile, `isActive`, and `twoFactorEnabled` without exposing the secret key or recovery codes.
- Produces: authorization policy `ActiveMfa`, which requires an active account and confirmed Identity 2FA for protected teacher/platform APIs.

- [x] Add a server authorization requirement that loads the authenticated `ApplicationUser` and rejects inactive or MFA-unconfigured accounts.
- [x] Apply `ActiveMfa` to teacher levels, cases, reports, and platform API groups; leave anonymous student routes and the limited auth/setup endpoints outside this policy.
- [x] Change password sign-in to return the next required step and rely on Identity's pending two-factor sign-in state when the account already has 2FA.
- [x] Add authenticator setup URI/key retrieval for an authenticated account without 2FA; emit a standard six-digit TOTP URI with issuer `ФВС/Код`, confirm a valid code before enabling 2FA, and generate recovery codes once.
- [x] Add TOTP and one-time recovery-code sign-in paths; reject invalid, consumed, or malformed codes without revealing account existence.
- [x] Add recovery-code regeneration that requires an enrolled authenticated account and invalidates the previous code set.
- [x] Add anonymous password-reset completion using Identity's reset token; successful reset rotates the security stamp and preserves the account's MFA enrollment and active/blocked state.
- [x] Apply a named ASP.NET Core rate-limit policy to password, TOTP, and recovery-code sign-in attempts; retain Identity lockout for password failures.
- [x] On every protected request, reject an inactive account promptly; configure security-stamp/session validation so block and credential reset take effect on the next request.
- [x] Build the API project and review each protected endpoint map to confirm it uses `ActiveMfa` while the intended onboarding endpoints remain reachable.

### Task 3: Add Invitation Completion and Superadmin Teacher Operations

**Files:**
- Create: `backend/Synapse.Blocks.Api/Auth/TeacherAccountService.cs`
- Create: `backend/Synapse.Blocks.Api/Auth/PlatformAuditEventService.cs`
- Modify: `backend/Synapse.Blocks.Api/Auth/InvitationService.cs`
- Modify: `backend/Synapse.Blocks.Api/Endpoints/PlatformAdminEndpoints.cs`
- Modify: `backend/Synapse.Blocks.Api/Contracts/Auth/PlatformAuthDtos.cs`
- Modify: `backend/Synapse.Blocks.Api/Program.cs`

**Interfaces:**
- Invitation acceptance takes token, password, first name, last name, and optional teacher description; successful acceptance creates only a `Teacher` account.
- `GET /api/platform/teachers` returns account/profile/status/MFA state, with search by email/name; it returns no credentials or bearer tokens.
- Platform actions: list/revoke pending invitations, block/reactivate a teacher, create a password-reset URL, and reset teacher 2FA.
- Security operations write `PlatformAuditEvent` records with actor, target, action, and timestamp only.

- [x] Extend invitation creation/acceptance with revocation checks, required names, optional description, and the existing single-use/expiry rules; normalize the user's display name from first/last name.
- [x] Add a teacher directory query that returns only teacher-role accounts and pending invitation metadata, with search over email and name.
- [x] Add block/reactivate operations that change `IsActive`, rotate the security stamp, and preserve all account-owned records.
- [x] Add one-time password-reset URL creation and 2FA reset; the latter clears the old authenticator/recovery state, rotates the security stamp, and requires enrollment at next sign-in.
- [x] Add invitation revocation and secret-free audit events for invitation, account-status, password-reset, MFA-reset, and future global-case actions.
- [x] Keep every route under the `PlatformAdmin` role policy and antiforgery protection; confirm teacher invites cannot assign `PlatformAdmin`.
- [x] Build the API project and inspect response DTOs to ensure raw invite/reset tokens appear only in the one-time creation response.

### Task 4: Implement Invitation Onboarding and Authenticator Screens

**Files:**
- Modify: `frontend/teacher/src/providers/authProvider.ts`
- Modify: `frontend/teacher/src/providers/apiClient.ts`
- Modify: `frontend/teacher/src/types/api.ts`
- Modify: `frontend/teacher/src/pages/login.tsx`
- Modify: `frontend/teacher/src/pages/invitations/accept.tsx`
- Create: `frontend/teacher/src/pages/authenticator-setup.tsx`
- Create: `frontend/teacher/src/pages/two-factor-challenge.tsx`
- Create: `frontend/teacher/src/pages/recovery-codes.tsx`
- Create: `frontend/teacher/src/pages/security.tsx`
- Create: `frontend/teacher/src/pages/password-reset.tsx`
- Modify: `frontend/teacher/src/App.tsx`

**Interfaces:**
- `authProvider.login` handles the backend `nextStep` and routes to setup, challenge, or the authenticated role home.
- Invitation acceptance submits email-bound token, password, first name, last name, and optional description; after account creation, the page routes through sign-in into required authenticator setup.
- Password reset completion submits the email, token, and new password from the copyable superadmin-issued link.
- Setup screen consumes authenticator URI/key, displays the QR with the existing Ant Design `QRCode`, confirms a six-digit code, and displays recovery codes once.
- Challenge screen accepts either a TOTP code or a recovery code and never stores either in persistent browser storage.

- [x] Update TypeScript contracts to match the backend auth/account DTOs and keep `apiClient` cookies and antiforgery behavior intact.
- [x] Update login to route each `nextStep`, preserve generic invalid-credential messaging, and show a six-digit challenge only when required.
- [x] Extend invitation acceptance with required password/name fields and optional description; align password validation with the existing 12-character Identity minimum.
- [x] Add authenticator setup, challenge, and one-time recovery-code display pages; clear code values after submission and avoid local storage.
- [x] Add the password-reset completion page and route; enforce the existing password policy and return to sign-in without changing the account's blocked or MFA state.
- [x] Register public onboarding routes and prevent the authenticated workspace layout from rendering before MFA is confirmed.
- [x] Run `npm run build` in `frontend/teacher` and resolve TypeScript/Vite build errors.

### Task 5: Refresh Role-Aware Admin Shell and Teacher Directory

**Files:**
- Modify: `frontend/teacher/src/App.tsx`
- Modify: `frontend/teacher/src/style.css`
- Create: `frontend/teacher/src/pages/platform/dashboard.tsx`
- Create: `frontend/teacher/src/pages/platform/teachers.tsx`
- Modify: `frontend/teacher/src/pages/platform/invitations.tsx`
- Modify: `frontend/teacher/src/types/api.ts`

**Interfaces:**
- Superadmin navigation exposes «Преподаватели» and «Наборы»; teacher navigation exposes only the teacher's levels, own sets, and reports.
- Superadmin dashboard prioritizes invited/active/blocked counts and the teacher directory; teacher dashboard remains set-focused.
- Teacher directory supports search, account details, pending/active/blocked/2FA states, invite creation, block/reactivate, password-reset link copy, and 2FA reset confirmation.

- [x] Add a role-aware layout/navigation and route guard based on `/api/auth/me`; retain server authorization as the source of access decisions.
- [x] Refresh app tokens and responsive styles to the approved near-white/graphite/`#F36F29` direction with clear status labels and accessible focus states.
- [x] Replace the admin-only invitation page as the sole platform screen with a teacher directory and invitation workflow; keep newly generated links visible only at creation time.
- [x] Add explicit confirmations and clear result messages for block, reactivate, reset-password, and reset-2FA actions.
- [x] Add the role-specific dashboards and route the superadmin to teacher management while keeping the teacher dashboard centered on their own sets.
- [x] Run `npm run build` in `frontend/teacher` and inspect the navigation/resource configuration for role leaks in the UI.

### Task 6: Add Owner-Safe Superadmin Case APIs

**Files:**
- Create: `backend/Synapse.Blocks.Api/Cases/PlatformCaseService.cs`
- Create: `backend/Synapse.Blocks.Api/Contracts/Cases/PlatformCaseDtos.cs`
- Create: `backend/Synapse.Blocks.Api/Endpoints/PlatformCaseEndpoints.cs`
- Modify: `backend/Synapse.Blocks.Api/Cases/CaseService.cs`
- Modify: `backend/Synapse.Blocks.Api/Program.cs`

**Interfaces:**
- `GET /api/platform/cases` returns case state and owner email/display name; supports title/owner/state filtering.
- `GET /api/platform/teachers/{teacherId}/levels` returns read-only eligible level versions for case composition.
- Platform create requires a selected `ownerId`; platform update/publish/archive/restore address a case ID and resolve its existing owner on the server.
- Platform share-link create/rotate/revoke resolves the owner from the case ID and preserves current QR token semantics.
- Platform case update DTO never accepts an owner change; all selected versions must belong to the case owner.

- [x] Add platform-only case list/detail queries with owner identity and existing section/assignment details.
- [x] Add a read-only level catalog query for a selected teacher so the superadmin can compose a case without editing level definitions.
- [x] Reuse current case validation, section ordering, publication, archiving, restoring, share-link revocation, and attempt snapshot rules through owner-scoped `CaseService` methods.
- [x] Add platform share-link create/rotate/revoke by resolving the case's existing owner; never return an old share-link token.
- [x] Resolve the existing case owner from the database for update/publish/archive/restore; reject cross-owner level versions and ignore any attempted client owner reassignment.
- [x] Audit global case create/update/publish/archive/restore actions without storing tokens or student data.
- [x] Map the platform case route group with both `PlatformAdmin` role authorization and `ActiveMfa` policy; leave `/api/teacher/cases` owner-scoped.
- [x] Build the API project and review the platform service paths for owner changes and foreign level IDs.

### Task 7: Build the Global Case List and Owner-Aware Editor

**Files:**
- Create: `frontend/teacher/src/pages/platform/cases/list.tsx`
- Create: `frontend/teacher/src/pages/platform/cases/edit.tsx`
- Modify: `frontend/teacher/src/App.tsx`
- Modify: `frontend/teacher/src/types/api.ts`
- Reuse: `frontend/teacher/src/components/cases/CaseSectionsEditor.tsx`

**Interfaces:**
- Superadmin case list shows title, owner, draft/published/archived state, and updated time with owner/title/state filters.
- Create flow requires selecting a teacher; edit flow shows the existing owner as read-only.
- Existing section editor is reused with a platform data source for the selected owner's level catalog.

- [x] Add the all-teacher case table and filters using `/api/platform/cases`.
- [x] Add a create flow that selects a teacher and loads that teacher's eligible levels; do not expose level editing actions.
- [x] Add an edit flow that displays the immutable owner, saves case content through platform APIs, and preserves publish/archive/restore semantics.
- [x] Expose QR/share-link create, rotate, copy, and revoke actions for the selected case while preserving one-time token display behavior.
- [x] Add menu/routes under superadmin «Наборы» and ensure teachers remain on `/api/teacher/cases` routes.
- [x] Run `npm run build` in `frontend/teacher` and inspect that owner context remains visible throughout case editing.

### Task 8: Final Build and User Review Handoff

**Files:**
- Review: all changed backend, migration, and frontend files.
- Update: this plan's checkboxes as each task is completed.

- [x] Run `dotnet build backend/Synapse.Blocks.Api/Synapse.Blocks.Api.csproj`.
- [x] Run `npm run build` in `frontend/teacher`.
- [x] Inspect the additive migration and final code paths against the five Review Focus items above; do not claim behavioral verification from build results.
- [x] Leave the branch unpushed unless the user explicitly asks to push this new task.
- [ ] Hand off the running app and changed files for the user's visual/behavioral review.
