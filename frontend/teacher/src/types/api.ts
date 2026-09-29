export interface CurrentUser { userId: string; id?: string; email: string; roles: string[] }
export interface InvitationCreated { token: string; invitationUrl: string; expiresAt: string }
export interface TeacherLevel { id: string; title: string; currentVersionId: string; version: number; definition: LevelDefinition }
export interface LevelDefinition { id: string; title: string; description: string; objective: string; allowedBlocks: string[]; requiredBlocks: string[]; tests: TestCase[]; introSteps: IntroStep[]; [key: string]: unknown }
export interface TestCase { id: string; name: string; input: string; expectedOutput: string; hidden: boolean }
export interface IntroStep { id: string; title: string; body: string; mediaUrl: string; [key: string]: unknown }
export interface TeacherCase { id: string; caseType: "orderedLevels"; title: string; description: string; isPublished: boolean; archived: boolean; levels: { levelId: string; levelVersionId: string; order: number; title: string }[]; shareLinkActive: boolean }
export interface CaseReport { caseId: string; participants: { displayName: string; bestProgress: number; attempts: { attemptId: string; status: string; startedAt: string; completedAt?: string; completedLevelCount: number }[] }[] }
