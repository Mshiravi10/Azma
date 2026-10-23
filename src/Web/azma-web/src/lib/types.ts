// Shared API types — mirror backend DTOs

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
}

// ---------- Auth ----------
export interface LoginResponse {
  accessToken: string;
  accessTokenExpiresAtUtc?: string;
  refreshToken: string;
  refreshTokenExpiresAtUtc?: string;
  user: {
    id: string;
    email: string;
    displayName: string | null;
    avatarUrl?: string | null;
    mustChangePassword?: boolean;
  };
  workspace: {
    id: string;
    name: string;
    slug?: string;
    role?: string;
    accessScope?: AccessScope;
  } | null;
}

export interface MeResponse {
  user: {
    id: string;
    email: string;
    displayName: string | null;
    avatarUrl?: string | null;
    mustChangePassword?: boolean;
  };
  current: { id: string; name: string; slug: string; role?: string; accessScope?: AccessScope } | null;
  workspaces: Array<{ id: string; name: string; slug?: string; role?: string; accessScope?: AccessScope }>;
}

export interface McpToken {
  id: string;
  name: string;
  createdAtUtc: string;
  expiresAtUtc: string;
  lastUsedAtUtc?: string | null;
  revokedAtUtc?: string | null;
  status: 'Active' | 'Expired' | 'Revoked';
}

export interface CreateMcpTokenResponse {
  id: string;
  name: string;
  token: string;
  createdAtUtc: string;
  expiresAtUtc: string;
}

// ---------- Projects ----------
export interface Project {
  id: string;
  workspaceId: string;
  code: string;
  name: string;
  description?: string | null;
  isArchived: boolean;
  createdAtUtc: string;
}

export interface Milestone {
  id: string;
  projectId: string;
  name: string;
  description?: string | null;
  dueDateUtc?: string | null;
  status: string;
  createdAtUtc: string;
}

export interface Suite {
  id: string;
  projectId: string;
  name: string;
  description?: string | null;
  position: number;
  createdAtUtc: string;
}

export interface SectionNode {
  id: string;
  suiteId: string;
  parentId: string | null;
  name: string;
  description?: string | null;
  position: number;
  children: SectionNode[];
}

export interface SuiteTree {
  id: string;
  name: string;
  position: number;
  sections: SectionNode[];
}

// kept for backward compat in older imports
export interface Section {
  id: string;
  suiteId: string;
  parentId: string | null;
  name: string;
  position: number;
  children?: Section[];
}

// ---------- Test Cases ----------
export type TestCaseStatus = 'Draft' | 'Active' | 'Deprecated';
export type TestCasePriority = 'Critical' | 'High' | 'Medium' | 'Low';
export type TestCaseType =
  | 'Functional'
  | 'Integration'
  | 'EndToEnd'
  | 'Performance'
  | 'Security'
  | 'Usability'
  | 'Other';

export interface TestStep {
  order: number;
  action: string;
  expectedResult: string | null;
}

export type AutomationEngine = 'None' | 'ScenarioRecording';
export type AutomationStatus = 'None' | 'Draft' | 'Validated' | 'Published' | 'Broken';
export type RecordedActionType = 'Navigate' | 'Click' | 'Fill' | 'Select' | 'Submit' | 'Wait';
export type RecordedExpectationType =
  | 'VisibleText'
  | 'ElementVisible'
  | 'UrlMatches'
  | 'ApiStatus'
  | 'ApiJsonPathEquals'
  | 'ApiBodyContains';
export type AutomationFailureKind =
  | 'None'
  | 'FrontendFailure'
  | 'ApiFailure'
  | 'AssertionFailure'
  | 'Timeout'
  | 'LocatorFailure'
  | 'ValidationFailure'
  | 'RunnerFailure';

export interface RecordedScenarioAction {
  type: RecordedActionType;
  url?: string | null;
  selector?: string | null;
  text?: string | null;
  value?: string | null;
  delayMs?: number | null;
  label?: string | null;
  selectorAlternates?: string[] | null;
  sensitive?: boolean;
  selectorStrategy?: string | null;
  selectorConfidence?: number | null;
}

export interface RecordedNetworkCapture {
  method: string;
  urlPattern: string;
  expectedStatus?: number | null;
  durationMs?: number | null;
  requestSummary?: string | null;
  responseSummary?: string | null;
}

export interface RecordedExpectation {
  type: RecordedExpectationType;
  selector?: string | null;
  text?: string | null;
  urlPattern?: string | null;
  method?: string | null;
  statusCode?: number | null;
  jsonPath?: string | null;
  expectedValue?: string | null;
  bodyContains?: string | null;
  label?: string | null;
  selectorAlternates?: string[] | null;
  selectorStrategy?: string | null;
  selectorConfidence?: number | null;
}

export interface RecordedStepEvidence {
  url?: string | null;
  pageTitle?: string | null;
  capturedAtUtc?: string | null;
}

export interface RecordedScenarioStep {
  id: string;
  title?: string | null;
  actionText?: string | null;
  expectedResult?: string | null;
  actions: RecordedScenarioAction[];
  assertions: RecordedExpectation[];
  networkCaptures?: RecordedNetworkCapture[] | null;
  evidence?: RecordedStepEvidence | null;
}

export interface RecordedScenarioDefinition {
  name?: string | null;
  startUrl?: string | null;
  actions: RecordedScenarioAction[];
  networkCaptures: RecordedNetworkCapture[];
  expectations: RecordedExpectation[];
  variables?: Record<string, string> | null;
  timeoutSeconds?: number | null;
  steps?: RecordedScenarioStep[] | null;
  schemaVersion?: number;
}

export interface AutomationRecordingSession {
  sessionId: string;
  sessionToken: string;
  expiresAtUtc: string;
}

export interface TestCaseAutomation {
  testCaseId: string;
  automationEngine: AutomationEngine;
  automationStatus: AutomationStatus;
  automationDefinitionVersion: number | null;
  automationRecordingJson: string | null;
  isValid: boolean;
  validationErrors: string[];
}

export interface AutomationStepResult {
  order: number;
  kind: string;
  success: boolean;
  label: string | null;
  errorMessage: string | null;
  durationMs: number;
  pageUrl?: string | null;
  screenshotBase64?: string | null;
}

export interface AutomationNetworkResult {
  method: string;
  url: string;
  statusCode: number;
  matchedExpectation: boolean;
  durationMs: number | null;
  errorMessage: string | null;
  responseBody?: string | null;
}

export interface AutomationRunResponse {
  success: boolean;
  failureKind: AutomationFailureKind;
  errorMessage: string | null;
  durationMs: number;
  steps: AutomationStepResult[];
  network: AutomationNetworkResult[];
}

export interface TestCaseSummary {
  id: string;
  projectId: string;
  suiteId: string | null;
  sectionId: string | null;
  code: string;
  title: string;
  hasAutomation: boolean;
  automationEngine: AutomationEngine;
  automationStatus: AutomationStatus;
  automationDefinitionVersion: number | null;
  automationScenarioPath: string | null;
  type: TestCaseType;
  priority: TestCasePriority;
  status: TestCaseStatus;
  version: number;
  tags: string[];
  createdByUserId: string;
  createdAtUtc: string;
  updatedAtUtc: string | null;
  lastExecutedAtUtc: string | null;
  lastExecutionStatus: ResultStatus | null;
  lastExecutionRunId: string | null;
  lastExecutionRunCaseId: string | null;
  lastExecutedByUserId: string | null;
}

export interface TestCase {
  id: string;
  projectId: string;
  suiteId: string | null;
  sectionId: string | null;
  code: string;
  title: string;
  preconditions: string | null;
  hasAutomation: boolean;
  automationEngine: AutomationEngine;
  automationStatus: AutomationStatus;
  automationDefinitionVersion: number | null;
  automationScenarioPath: string | null;
  automationRecordingJson: string | null;
  type: TestCaseType;
  status: TestCaseStatus;
  priority: TestCasePriority;
  version: number;
  tags: string[];
  steps: TestStep[];
  createdByUserId: string;
  createdAtUtc: string;
  updatedAtUtc: string | null;
  lastExecutedAtUtc: string | null;
  lastExecutionStatus: ResultStatus | null;
  lastExecutionRunId: string | null;
  lastExecutionRunCaseId: string | null;
  lastExecutedByUserId: string | null;
  customFields: Record<string, unknown>;
  automationPrerequisiteTestCaseId: string | null;
}

// ---------- Custom Fields ----------
export type CustomFieldType =
  | 'Text' | 'LongText' | 'Number' | 'Date' | 'Boolean' | 'Select' | 'MultiSelect' | 'Url';

export interface CustomFieldDefinition {
  id: string;
  projectId: string;
  key: string;
  label: string;
  description: string | null;
  type: CustomFieldType;
  options: string[] | null;
  required: boolean;
  position: number;
  isArchived: boolean;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export interface TestCaseVersion {
  testCaseId: string;
  version: number;
  snapshotJson: string;
  authorUserId: string;
  createdAtUtc: string;
}

// ---------- Runs ----------
export type RunStatus = 'Open' | 'Closed';
export type ResultStatus = 'Untested' | 'Passed' | 'Failed' | 'Blocked' | 'Retest' | 'Skipped';
export type TestCaseExecutionState = 'Never' | 'Stale' | 'Fresh' | 'NeedsRun';

export interface Run {
  id: string;
  workspaceId: string;
  projectId: string;
  projectCode: string;
  milestoneId: string | null;
  code: string;
  name: string;
  description: string | null;
  status: RunStatus;
  caseCount: number;
  createdByUserId: string;
  createdAtUtc: string;
  closedAtUtc: string | null;
  environment: string | null;
  dueDateUtc: string | null;
}

export interface RunCase {
  id: string;
  runId?: string;
  testCaseId: string;
  testCaseCode: string;
  testCaseTitle: string;
  testCaseVersion?: number;
  hasAutomation: boolean;
  automationEngine: AutomationEngine;
  automationStatus: AutomationStatus;
  automationDefinitionVersion: number | null;
  automationScenarioPath: string | null;
  assignedToUserId: string | null;
  currentStatus: ResultStatus;
  lastExecutedAtUtc: string | null;
  lastExecutedByUserId: string | null;
}

export interface RunResult {
  id: string;
  runCaseId: string;
  status: ResultStatus;
  comment: string | null;
  elapsedSeconds: number | null;
  automationEngine: AutomationEngine | null;
  automationDetailJson: string | null;
  executedByUserId: string;
  executedAtUtc: string;
}

export interface AutomaticRunResult {
  result: RunResult;
  scenarioName: string;
  automationEngine: AutomationEngine;
  succeeded: boolean;
  errorMessage: string | null;
}

export interface RunComment {
  id: string;
  runId: string;
  authorUserId: string;
  body: string;
  createdAtUtc: string;
}

export interface RunAttachment {
  id: string;
  runId: string;
  runCaseId: string | null;
  runResultId: string | null;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  uploadedByUserId: string;
  uploadedAtUtc: string;
}

export interface AttachmentUrl {
  url: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
}

export type ScheduleRecurrenceType =
  | 'Daily'
  | 'Weekly'
  | 'Hourly'
  | 'Every30Minutes'
  | 'Every15Minutes'
  | 'Every10Minutes'
  | 'Every5Minutes';
export type RunScheduleExecutionStatus = 'Running' | 'Completed' | 'Failed';

export interface RunSchedule {
  id: string;
  projectId: string;
  name: string;
  description: string | null;
  environment: string | null;
  recurrenceType: ScheduleRecurrenceType;
  timeOfDayMinutes: number;
  weekDaysMask: number;
  timeZoneId: string;
  startAtUtc: string | null;
  endAtUtc: string | null;
  isActive: boolean;
  nextRunAtUtc: string | null;
  lastRunAtUtc: string | null;
  lastRunId: string | null;
  caseCount: number;
  loops: number;
  threads: number;
  rampup: number;
  skippable: boolean;
  gateway: string | null;
  frontend: string | null;
  createdByUserId: string;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export interface RunScheduleCase {
  id: string;
  testCaseId: string;
  testCaseCode: string;
  testCaseTitle: string;
  testCaseVersion: number;
  automationScenarioPath: string | null;
  automationEngine: AutomationEngine;
  automationStatus: AutomationStatus;
  automationDefinitionVersion: number | null;
}

export interface RunScheduleDetail {
  schedule: RunSchedule;
  cases: RunScheduleCase[];
}

export interface RunScheduleExecution {
  id: string;
  runScheduleId: string;
  runId: string | null;
  status: RunScheduleExecutionStatus;
  trigger: string;
  startedAtUtc: string;
  completedAtUtc: string | null;
  totalCases: number;
  passed: number;
  failed: number;
  errorMessage: string | null;
}

export type RunAutomationExecutionStatus = 'Queued' | 'Running' | 'Completed' | 'Failed';

export interface RunAutomationExecution {
  id: string;
  runId: string;
  status: RunAutomationExecutionStatus;
  trigger: string;
  startedAtUtc: string;
  completedAtUtc: string | null;
  totalCases: number;
  passed: number;
  failed: number;
  errorMessage: string | null;
}

// ---------- Reporting ----------
export interface StatusBreakdown {
  untested: number;
  passed: number;
  failed: number;
  blocked: number;
  retest: number;
  skipped: number;
  total: number;
  executed: number;
  passRate: number;
}

export interface RecentRun {
  runId: string;
  projectId: string;
  projectCode: string;
  name: string;
  code: string;
  status: RunStatus;
  caseCount: number;
  passed: number;
  failed: number;
  passRate: number;
  createdAtUtc: string;
  completedAtUtc: string | null;
}

export interface ProjectSummary {
  projectId: string;
  code: string;
  name: string;
  isArchived: boolean;
  totalRuns: number;
  openRuns: number;
  testCases: number;
  recordedResults: number;
  passRate: number;
}

export interface WorkspaceDashboard {
  totalProjects: number;
  totalRuns: number;
  openRuns: number;
  totalTestCases: number;
  recordedResults: number;
  counts: StatusBreakdown;
  projects: ProjectSummary[];
  recentRuns: RecentRun[];
}

export interface ProjectDashboard {
  projectId: string;
  projectCode: string;
  projectName: string;
  isArchived: boolean;
  totalRuns: number;
  openRuns: number;
  completedRuns: number;
  totalTestCases: number;
  recordedResults: number;
  counts: StatusBreakdown;
  recentRuns: RecentRun[];
}

export interface TrendBucket {
  bucketStartUtc: string;
  passed: number;
  failed: number;
  blocked: number;
  skipped: number;
  retest: number;
  total: number;
  passRate: number;
}

export interface TopFailingTest {
  testCaseId: string;
  code: string;
  title: string;
  failures: number;
  executions: number;
  failureRate: number;
}

export interface UserActivity {
  userId: string;
  executions: number;
  passed: number;
  failed: number;
  blocked: number;
  skipped: number;
  retest: number;
  passRate: number;
}

// ---------- Members ----------
export type WorkspaceRole = 'Owner' | 'Admin' | 'Tester' | 'Viewer';
export type AccessScope = 'Workspace' | 'ProjectScoped';

export interface Membership {
  userId: string;
  email: string;
  displayName: string;
  avatarUrl?: string | null;
  role: WorkspaceRole;
  accessScope?: AccessScope;
  joinedAtUtc: string;
  lastLoginAtUtc?: string | null;
  isActive: boolean;
}

export interface MemberCandidate {
  userId: string;
  email: string;
  displayName: string;
  avatarUrl?: string | null;
  isInWorkspace: boolean;
  workspaceRole?: WorkspaceRole | null;
}

export interface AdminCreateUserResult {
  member: Membership;
  temporaryPassword: string;
}

export type ProjectMemberRole = 'Owner' | 'Admin' | 'Tester' | 'Viewer' | 'Excluded';

export interface ProjectMembersListResponse {
  members: ProjectMember[];
  currentUserProjectRole: ProjectMemberRole | null;
}

export interface UserProjectRole {
  projectId: string;
  projectName: string;
  projectCode: string;
  role: ProjectMemberRole;
}

export interface ProjectMember {
  userId: string;
  email: string;
  displayName: string;
  avatarUrl?: string | null;
  /** "Inherited" when no override; otherwise role name or "Excluded" */
  effectiveRole: string;
  /** "workspace" | "override" | "excluded" */
  source: 'workspace' | 'override' | 'excluded';
  overrideRole?: string | null;
  addedAtUtc?: string | null;
  isActive: boolean;
}

export interface ProjectMemberCandidate {
  userId: string;
  email: string;
  displayName: string;
  avatarUrl?: string | null;
  isInProject: boolean;
  projectRole?: ProjectMemberRole | null;
}
