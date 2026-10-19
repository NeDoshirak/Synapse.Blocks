# Superadmin, teacher accounts, and mandatory authenticator 2FA

**Status:** Approved for implementation
**Date:** 2026-10-19

## Goal

Give the platform a clear superadmin workspace for provisioning and managing teacher accounts and managing every teacher's case sets, while keeping teacher data isolated. Require Google Authenticator-compatible TOTP for every teacher and the superadmin. Refresh the admin experience under the ФВС/Код visual identity.

## User decisions

- Use the existing, separate `PlatformAdmin` identity as the superadmin account, label it «Суперадмин» in the product, and keep teacher accounts in the `Teacher` role.
- Superadmin can invite, search, inspect, block/reactivate, reset passwords, and reset 2FA for teachers. Accounts and their data are never physically deleted.
- Superadmin can manage every teacher's case sets, including view, edit, publish, and archive. A set keeps its teacher owner; creating a set as superadmin is done for a selected teacher and uses that teacher's levels. Superadmin does not manage level definitions.
- Teacher invites are single-use links. At first use, the teacher sets a password, first name, last name, and description, then enrolls in TOTP. Existing accounts enroll the next time they sign in.
- TOTP setup uses a QR code. A valid authenticator code confirms enrollment. The user receives one-time recovery codes and enters an authenticator code (or one unused recovery code) at every subsequent sign-in.
- A superadmin reset of a teacher's password or 2FA invalidates existing sessions. Blocking an account closes active sessions; reactivation allows a new sign-in.
- Visual direction: a warm, light "workshop" UI with a near-white canvas, graphite navigation, and `#F36F29` accent. Superadmin lands on teacher management; cases are a separate section. Teacher home stays focused on that teacher's own sets. Use the current frontend stack and shared components.

## Assumptions for review

- Teacher description is optional; first and last names are required for newly invited teachers.
- Invitation and password-reset links are copied by the superadmin. No email delivery is added because the platform has no configured email service.
- Existing teachers are not forced to fill in new profile fields. Their current display name remains a fallback in the directory until profile information is available.
- Pending invitations can be revoked and replaced; archived sets can be restored.

## Current platform context

- ASP.NET Core Identity already has `PlatformAdmin` and `Teacher` roles. The platform admin is bootstrapped from deployment configuration and is distinct from invited teachers.
- The teacher application is one Refine/React app at `/admin`; platform admin endpoints already create invitation links. The invitee currently sets only a password, and sign-in currently checks only the password.
- Teacher APIs scope levels, cases, and reports to the authenticated owner. Platform-wide case management therefore needs separate, explicitly authorized endpoints; it must not widen teacher endpoints.
- Case sets reference levels belonging to their teacher. This ownership relationship is preserved for superadmin edits.
- Existing cookie authentication, antiforgery, and Identity token providers remain in use.

## Roles and access boundaries

### Superadmin

- Has a dedicated role and account provisioned by the deployment bootstrap; teacher invitations can never grant this role.
- Can open the teacher directory, invite a teacher, inspect account state, block/reactivate the account, create password-reset links, and force 2FA re-enrollment.
- Can list all case sets with owner identity visible; filter/search by owner, title, and state; edit set metadata and assignment composition; publish, archive, and restore archived sets.
- Can create a set on behalf of a selected teacher. Every included level version must belong to that teacher. The set's owner cannot be changed through an edit.
- Cannot edit teachers' level definitions, impersonate users, or delete teacher accounts, levels, cases, attempts, or reports.

### Teacher

- Sees and manages only their own levels, sets, and reports, as today.
- Cannot call platform management APIs, see another teacher's identity data, or change their role or account status.
- A blocked teacher cannot sign in or use an existing authenticated session.

### Server authorization

- Keep `/api/teacher/*` owner-scoped and unchanged in authorization intent.
- Add platform-only APIs under a distinct `/api/platform/*` boundary for teacher management and global case management. Require the `PlatformAdmin` role on every route and enforce ownership/level consistency in services, not from client-supplied IDs alone.
- Keep antiforgery validation on cookie-authenticated state-changing requests. A hidden menu item or route guard is not an authorization control.

## Teacher account lifecycle

1. Superadmin enters the teacher's email and creates an invitation. The application shows a copyable, single-use, expiring URL; it does not send email because the current platform has no configured email delivery.
2. The teacher opens the link and submits password, first name, last name, and description. First name and last name are required; description may be blank. The invitation is consumed only after successful account creation.
3. The new account receives only the `Teacher` role and remains in a restricted setup state until TOTP is enrolled and confirmed.
4. The superadmin directory shows pending invitations and active/blocked accounts, supports search by email/name, and shows the teacher description in account details. A pending invite can be revoked and replaced with a fresh link.
5. Password reset creates a distinct, single-use, expiring reset link which the superadmin copies to the teacher. Completing it invalidates other sessions; it does not turn off an already enrolled authenticator.
6. Blocking an account sets an explicit inactive state, invalidates its security stamp/sessions, and prevents all authenticated teacher API access. Reactivation preserves content and reports and permits sign-in again.
7. Resetting 2FA disables the current authenticator and invalidates recovery codes and sessions. At next sign-in, the teacher must enroll a new authenticator before entering the teacher workspace.
8. No account deletion is provided. Historical cases and reports remain attached to the teacher account.

## Mandatory TOTP and recovery

- Use ASP.NET Core Identity's authenticator-token and recovery-code mechanisms, compatible with Google Authenticator and other standard TOTP apps. This is not Google OAuth and does not require a Google account.
- After a correct password, an unenrolled account receives a restricted enrollment session that can only access setup/confirmation endpoints. It receives no teacher or platform API access until it confirms a valid code and enables 2FA.
- A successfully enrolled account must supply a fresh six-digit authenticator code on every new sign-in. One unused recovery code may substitute when the authenticator is unavailable; recovery codes are single-use.
- Show the generated recovery codes once after enrollment or explicit regeneration. Do not log QR secrets, TOTP codes, recovery codes, invitation tokens, or reset tokens. Regeneration invalidates the previous set.
- Apply to the bootstrapped superadmin and all existing teachers on their next sign-in. No pre-enrolled user or role receives a password-only bypass.
- Failed password/TOTP attempts use Identity lockout and rate limiting. A blocked account is rejected before it can complete sign-in or call protected APIs.
- Account status and MFA state are checked server-side. Session invalidation after block/reset must take effect on the next protected request, not only after a long cookie validation interval.
- If the superadmin loses the authenticator, their recovery codes are the supported self-recovery path. There is no hidden password-only or teacher-accessible bypass.

## Case-set management for superadmin

- Provide a global case list with title, owner, publication/archive state, and updated time. Search/filter results are server-paginated if the data volume requires it.
- Superadmin can inspect and edit a case using the existing case editor, with the owner clearly visible. Add owner selection when creating a case; do not permit owner reassignment after creation.
- The case service resolves eligible levels using the selected case owner's catalog. It rejects any submitted level version whose owner differs from the case owner.
- Preserve existing publishing, QR/share-link, archive, attempt, and report semantics. Editing a case does not rewrite a running or historical attempt snapshot.
- Archived cases can be restored. Physical case deletion and superadmin editing of levels or reports are out of scope.

## Admin interface direction

- Refresh the existing admin app with a warm near-white workspace, graphite sidebar, orange primary actions (`#F36F29`), and crisp cards, tables, and status labels. Keep the visual language playful enough for a coding-learning product while suitable for account/security operations.
- Superadmin navigation prioritizes «Преподаватели», with «Наборы» as a separate section. The landing page summarizes invited, active, and blocked teacher counts and presents the teacher directory/invite action.
- Teacher navigation remains focused on their own levels and sets. Shared typography, surfaces, buttons, and status colors keep both roles in one product; role-specific page content and server authorization remain separate.
- Show clear pending, active, blocked, and 2FA setup states. Destructive-looking security actions require an explicit confirmation and explain the resulting sign-in/reset flow.
- Keep the layout usable on narrow screens and keyboard-accessible. Do not add a new UI framework or a mockup-only implementation.

## Data and migration

- Extend the user profile with first name, last name, optional teacher description, and an explicit active/blocked state. Existing accounts remain active and retain their current Identity role.
- Keep profile fields nullable for existing accounts; do not force existing teachers to invent profile data during mandatory TOTP setup. Their directory label falls back to the existing display name/email until profile fields are supplied.
- Keep the bootstrap superadmin distinct and sourced from deployment configuration. Never commit or expose its credentials in frontend code or invitation responses.
- Use Identity's persisted TOTP and recovery-code state; no external Google service or separate TOTP secret table is required. Add schema changes for profile/status and any platform audit records through additive migrations.
- Record security-sensitive platform actions (invitation/revocation, block/reactivation, password reset creation, 2FA reset, and global case changes) with actor, target, action, and timestamp. Never include credentials or bearer tokens in audit records.
- Migrations preserve existing teachers, ownership, cases, levels, attempts, role assignments, and any already-enabled 2FA. Accounts without 2FA are routed through enrollment on next sign-in.

## API and session behavior

- Extend invitation acceptance to store teacher profile fields and establish only a limited onboarding state, then require TOTP enrollment before normal sign-in completes.
- Extend the auth flow to distinguish password failure, blocked user, 2FA challenge, and required 2FA enrollment without disclosing account existence to unauthenticated clients.
- Add authenticated endpoints for authenticator setup URI/QR data, code confirmation, TOTP challenge, recovery-code sign-in, and recovery-code regeneration. Protect setup endpoints with the limited enrollment state and challenge endpoints with Identity's pending 2FA state.
- Add superadmin-only endpoints for teacher list/detail, invitations, active state, password-reset link creation, and 2FA reset. Return a reset/invite URL only at creation time.
- Add superadmin-only global case list/detail/create/update/publish/archive/restore endpoints. Never make `/api/teacher/*` accept a caller-specified owner.
- Keep response messages generic for invalid, expired, consumed, or unknown invitation/reset tokens. Apply normal CSRF protection to cookie-authenticated state changes.

## Out of scope

- Google OAuth, SMS/email one-time passwords, mandatory email delivery, and school identity-provider integration.
- Public teacher registration, physical account/data deletion, teacher impersonation, or role editing from the UI.
- Superadmin editing of levels, attempts, or reports; changing ownership of a teacher's case set.
- Replacing Refine/React, redesigning the student game, or changing student case behavior.

## Acceptance criteria

1. The bootstrapped superadmin signs in as a distinct «Суперадмин» account; invited accounts can only become teachers.
2. A superadmin can create a teacher invite, copy its expiring link, and the teacher can set password/name/surname/description and enroll TOTP before reaching the workspace.
3. An existing teacher and the superadmin are required to enroll at next sign-in; every subsequent sign-in requires TOTP or one unused recovery code.
4. Recovery codes are shown once, single-use, replaceable, and invalidated by a 2FA reset. No password-only path reaches protected APIs.
5. The superadmin can search and inspect teacher accounts, block/reactivate them, generate password-reset links, and reset their 2FA without deleting account-owned data.
6. Blocking or resetting credentials invalidates existing sessions; a blocked account cannot access protected API routes.
7. Teacher endpoints continue to return only the authenticated teacher's data, including when a teacher changes request IDs or payload owner values.
8. The superadmin can list and manage case sets across teachers, but cannot reassign a set or attach another teacher's levels. Existing QR, archive, and attempt snapshot behavior remains intact.
9. The refreshed UI presents the approved visual direction and role-specific landing/navigation without relying on frontend checks for access control.
10. A migration preserves existing users, roles, content, attempts, and ownership while adding profile/status fields and leaving existing accounts ready for required enrollment.
11. Security actions are auditable without recording passwords, QR secrets, authenticator/recovery codes, invitation tokens, or reset tokens.

## Review focus

- No authenticated teacher or platform API access before TOTP enrollment is confirmed.
- Every platform-management endpoint is role-protected on the server; every teacher data endpoint remains owner-scoped.
- Blocking, password reset, and 2FA reset invalidate active sessions promptly.
- Superadmin case edits cannot cross the owner/level boundary or mutate existing attempt snapshots.
- Existing accounts survive migration and reach mandatory 2FA without a password-only fallback.
- Invite/reset token handling and one-time recovery-code behavior do not leak secrets through responses, logs, or audit records.
