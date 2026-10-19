export interface CurrentUser {
  userId: string;
  id?: string;
  email: string;
  firstName: string | null;
  lastName: string | null;
  displayName: string;
  teacherDescription: string | null;
  isActive: boolean;
  twoFactorEnabled: boolean;
  roles: string[];
}
export interface AuthStep { nextStep: "authenticated" | "twoFactor" | "authenticatorSetup" }
export interface AuthenticatorSetup { sharedKey: string; authenticatorUri: string }
export interface RecoveryCodes { recoveryCodes: string[] }
export interface InvitationCreated { invitationUrl: string; expiresAt: string }
export interface PendingInvitation { id: string; email: string; expiresAt: string }
export interface TeacherAccount {
  id: string;
  email: string;
  displayName: string;
  firstName: string | null;
  lastName: string | null;
  teacherDescription: string | null;
  isActive: boolean;
  twoFactorEnabled: boolean;
}
export interface PasswordResetLink { resetUrl: string; expiresAt: string }
export interface TeacherLevel { id: string; title: string; currentVersionId: string; version: number; definition: LevelDefinition }
export interface LevelDefinition { id: string; title: string; description: string; objective: string; allowedBlocks: string[]; requiredBlocks: string[]; tests: TestCase[]; introSteps: IntroStep[]; [key: string]: unknown }
export interface TestCase { id: string; name: string; input: string; expectedOutput: string; hidden: boolean }
export interface IntroStep { id: string; title: string; body: string; mediaUrl: string; [key: string]: unknown }
export interface TeacherCaseLevel { levelId: string; levelVersionId: string; order: number; title: string; displayTitle: string | null }
export interface TeacherCaseSection { id: string; title: string | null; order: number; levels: TeacherCaseLevel[] }
export interface TeacherCase { id: string; caseType: "orderedLevels"; title: string; description: string; isPublished: boolean; archived: boolean; sections: TeacherCaseSection[]; levelCount: number; shareLinkActive: boolean }
export interface PlatformCase extends TeacherCase {
  ownerId: string;
  ownerName: string;
  ownerEmail: string;
  createdAt: string;
  updatedAt: string;
}
export interface CaseLevelDraft { levelVersionId: string; displayTitle: string }
export interface CaseSectionDraft { clientId: string; title: string; levels: CaseLevelDraft[] }
export interface CaseReport { caseId: string; participants: { displayName: string; bestProgress: number; attempts: { attemptId: string; status: string; startedAt: string; completedAt?: string; completedLevelCount: number }[] }[] }
