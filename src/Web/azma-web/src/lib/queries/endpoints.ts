import { api } from '../api';
import type {
  LoginResponse,
  MeResponse,
  McpToken,
  CreateMcpTokenResponse,
  PagedResult,
  Project,
  Milestone,
  Suite,
  SectionNode,
  SuiteTree,
  TestCase,
  TestCaseSummary,
  TestCaseVersion,
  AutomationRecordingSession,
  TestCaseAutomation,
  RecordedScenarioDefinition,
  AutomationRunResponse,
  TestCaseStatus,
  TestCaseExecutionState,
  TestCasePriority,
  TestCaseType,
  CustomFieldDefinition,
  CustomFieldType,
  TestStep,
  Run,
  RunCase,
  RunComment,
  RunResult,
  AutomaticRunResult,
  RunAttachment,
  AttachmentUrl,
  RunSchedule,
  RunScheduleDetail,
  RunScheduleExecution,
  RunAutomationExecution,
  ScheduleRecurrenceType,
  ResultStatus,
  WorkspaceDashboard,
  ProjectDashboard,
  TrendBucket,
  TopFailingTest,
  UserActivity,
  Membership,
  MemberCandidate,
  AdminCreateUserResult,
  WorkspaceRole,
  ProjectMember,
  ProjectMemberCandidate,
  ProjectMemberRole,
  ProjectMembersListResponse,
  UserProjectRole,
  AutomationEngine,
} from '../types';

const RESULT_STATUS_NAMES: ResultStatus[] = [
  'Untested',
  'Passed',
  'Failed',
  'Blocked',
  'Retest',
  'Skipped',
];
function normalizeResultStatus(s: ResultStatus | number): ResultStatus {
  return typeof s === 'number' ? (RESULT_STATUS_NAMES[s] ?? 'Untested') : s;
}

// ---------- Users (avatar etc.) ----------
export interface UserSettingsDto {
  id: string;
  email: string;
  displayName: string | null;
  avatarUrl: string | null;
  mustChangePassword: boolean;
}

export const UsersApi = {
  uploadMyAvatar: (file: File) => {
    const fd = new FormData();
    fd.append('file', file);
    return api
      .post<UserSettingsDto>(
        '/users/me/avatar',
        fd,
        { headers: { 'Content-Type': 'multipart/form-data' } },
      )
      .then((r) => r.data);
  },
  deleteMyAvatar: () => api.delete<UserSettingsDto>('/users/me/avatar').then((r) => r.data),
  /** Public absolute path to an arbitrary user's avatar (no auth required). */
  avatarPath: (userId: string, version?: number | string) =>
    `/api/users/${userId}/avatar${version ? `?v=${version}` : ''}`,
};

// ---------- Auth ----------
export const AuthApi = {
  login: (email: string, password: string) =>
    api.post<LoginResponse>('/auth/login', { email, password }).then((r) => r.data),
  register: (payload: { email: string; password: string; fullName: string; workspaceName: string }) =>
    api
      .post<LoginResponse>('/auth/register', {
        email: payload.email,
        password: payload.password,
        displayName: payload.fullName,
        workspaceName: payload.workspaceName,
      })
      .then((r) => r.data),
  me: () => api.get<MeResponse>('/auth/me').then((r) => r.data),
  switchWorkspace: (workspaceId: string) =>
    api.post<LoginResponse>(`/auth/switch-workspace/${workspaceId}`).then((r) => r.data),
  changePassword: (p: { currentPassword: string; newPassword: string }) =>
    api.post('/auth/change-password', p).then((r) => r.data),
  listMcpTokens: () => api.get<McpToken[]>('/auth/mcp-tokens').then((r) => r.data),
  createMcpToken: (p: { name: string; currentPassword: string; expiresInDays?: number }) =>
    api.post<CreateMcpTokenResponse>('/auth/mcp-tokens', p).then((r) => r.data),
  revokeMcpToken: (id: string) => api.delete(`/auth/mcp-tokens/${id}`).then((r) => r.data),
};

// ---------- Members (Workspace) ----------
export const MembersApi = {
  list: () => api.get<Membership[]>('/members').then((r) => r.data),
  candidates: (params: { search?: string; page?: number; pageSize?: number } = {}) =>
    api
      .get<PagedResult<MemberCandidate>>('/members/candidates', {
        params: { page: 1, pageSize: 50, ...params },
      })
      .then((r) => r.data),
  invitations: () =>
    api
      .get<Array<{ id: string; email: string; role: WorkspaceRole; createdAtUtc: string; expiresAtUtc?: string }>>(
        '/members/invitations',
      )
      .then((r) => r.data)
      .catch(() => [] as Array<{ id: string; email: string; role: WorkspaceRole; createdAtUtc: string; expiresAtUtc?: string }>),
  invite: (email: string, role: WorkspaceRole) =>
    api.post('/members/invitations', { email, role }).then((r) => r.data),
  adminCreate: (p: { email: string; displayName: string; role: WorkspaceRole }) =>
    api.post<AdminCreateUserResult>('/admin/users', p).then((r) => r.data),
  addExisting: (p: { userId: string; role?: WorkspaceRole }) =>
    api.post<Membership>('/members', p).then((r) => r.data),
  updateRole: (userId: string, role: WorkspaceRole) =>
    api.patch<Membership>(`/members/${userId}`, { role }).then((r) => r.data),
  remove: (userId: string) => api.delete(`/members/${userId}`).then((r) => r.data),
  transferOwnership: (userId: string) =>
    api.post(`/members/${userId}/transfer-ownership`).then((r) => r.data),
};

// ---------- Project Members ----------
export const ProjectMembersApi = {
  list: (projectId: string) =>
    api.get<ProjectMembersListResponse>(`/projects/${projectId}/members`).then((r) => r.data),
  candidates: (
    projectId: string,
    params: { search?: string; page?: number; pageSize?: number } = {},
  ) =>
    api
      .get<PagedResult<ProjectMemberCandidate>>(`/projects/${projectId}/member-candidates`, {
        params: { page: 1, pageSize: 25, ...params },
      })
      .then((r) => r.data),
  add: (projectId: string, p: { userId: string; role?: ProjectMemberRole }) =>
    api.post<ProjectMember>(`/projects/${projectId}/members`, p).then((r) => r.data),
  setRole: (projectId: string, userId: string, role: ProjectMemberRole) =>
    api.put<ProjectMember>(`/projects/${projectId}/members/${userId}`, { role }).then((r) => r.data),
  reset: (projectId: string, userId: string) =>
    api.delete(`/projects/${projectId}/members/${userId}`).then((r) => r.data),
  transferOwnership: (projectId: string, userId: string) =>
    api.post(`/projects/${projectId}/members/${userId}/transfer-ownership`).then((r) => r.data),
  userProjectRoles: (userId: string) =>
    api.get<UserProjectRole[]>(`/members/${userId}/project-roles`).then((r) => r.data),
};

// ---------- Projects ----------
export const ProjectsApi = {
  list: (params: { search?: string; includeArchived?: boolean; page?: number; pageSize?: number } = {}) =>
    api
      .get<PagedResult<Project>>('/projects', { params: { pageSize: 100, includeArchived: false, ...params } })
      .then((r) => r.data.items),
  get: (id: string) => api.get<Project>(`/projects/${id}`).then((r) => r.data),
  create: (p: { code: string; name: string; description?: string }) =>
    api.post<Project>('/projects', p).then((r) => r.data),
  update: (id: string, p: { name: string; description?: string }) =>
    api.put<Project>(`/projects/${id}`, p).then((r) => r.data),
  archive: (id: string) => api.post(`/projects/${id}/archive`).then((r) => r.data),
  unarchive: (id: string) => api.post(`/projects/${id}/restore`).then((r) => r.data),

  milestones: (id: string) => api.get<Milestone[]>(`/projects/${id}/milestones`).then((r) => r.data),
  createMilestone: (id: string, p: { name: string; description?: string; dueDateUtc?: string }) =>
    api.post<Milestone>(`/projects/${id}/milestones`, p).then((r) => r.data),

  suites: (id: string) => api.get<Suite[]>(`/projects/${id}/suites`).then((r) => r.data),
  createSuite: (projectId: string, p: { name: string; description?: string }) =>
    api.post<Suite>(`/projects/${projectId}/suites`, p).then((r) => r.data),
};

// ---------- Suites & Sections ----------
export const SuitesApi = {
  tree: (suiteId: string) => api.get<SuiteTree>(`/suites/${suiteId}/tree`).then((r) => r.data),
  update: (suiteId: string, p: { name: string; description?: string | null }) =>
    api.put<Suite>(`/suites/${suiteId}`, p).then((r) => r.data),
  remove: (suiteId: string) => api.delete(`/suites/${suiteId}`).then((r) => r.data),
  reorder: (suiteId: string, newPosition: number) =>
    api.post(`/suites/${suiteId}/reorder`, { newPosition }).then((r) => r.data),
  createSection: (suiteId: string, p: { parentId?: string | null; name: string; description?: string }) =>
    api.post<SectionNode>(`/suites/${suiteId}/sections`, p).then((r) => r.data),
};

export const SectionsApi = {
  update: (sectionId: string, p: { name: string; description?: string | null }) =>
    api.put<SectionNode>(`/sections/${sectionId}`, p).then((r) => r.data),
  remove: (sectionId: string) => api.delete(`/sections/${sectionId}`).then((r) => r.data),
  move: (sectionId: string, p: { newParentId: string | null; newPosition: number }) =>
    api.post(`/sections/${sectionId}/move`, p).then((r) => r.data),
};

// ---------- Test Cases ----------
export interface TestCaseListParams {
  projectId?: string;
  suiteId?: string;
  sectionId?: string;
  search?: string;
  tag?: string;
  status?: TestCaseStatus;
  priority?: TestCasePriority;
  executionState?: TestCaseExecutionState;
  hasAutomation?: boolean;
  automationEngine?: AutomationEngine;
  staleDays?: number;
  includeDeleted?: boolean;
  page?: number;
  pageSize?: number;
}

export const TestCasesApi = {
  list: (params: TestCaseListParams = {}) =>
    api.get<PagedResult<TestCaseSummary>>('/testcases', { params }).then((r) => r.data),
  get: (id: string) => api.get<TestCase>(`/testcases/${id}`).then((r) => r.data),
  create: (p: {
    projectId: string;
    suiteId?: string | null;
    sectionId?: string | null;
    title: string;
    preconditions?: string;
    type?: TestCaseType;
    priority?: TestCasePriority;
    status?: TestCaseStatus;
    tags?: string[];
    steps?: Array<{ action: string; expectedResult?: string | null }>;
    automationScenarioPath?: string | null;
    customFields?: Record<string, unknown>;
  }) => api.post<TestCase>('/testcases', p).then((r) => r.data),
  update: (
    id: string,
    p: {
      suiteId?: string | null;
      sectionId?: string | null;
      title: string;
      preconditions?: string | null;
      type: TestCaseType;
      status: TestCaseStatus;
      priority: TestCasePriority;
      changeReason?: string | null;
      automationScenarioPath?: string | null;
      customFields?: Record<string, unknown>;
    },
  ) => api.put<TestCase>(`/testcases/${id}`, p).then((r) => r.data),
  remove: (id: string) => api.delete(`/testcases/${id}`).then((r) => r.data),
  setSteps: (id: string, steps: Array<{ action: string; expectedResult?: string | null }>) =>
    api.put<TestCase>(`/testcases/${id}/steps`, steps).then((r) => r.data),
  setTags: (id: string, tags: string[]) =>
    api.put<TestCase>(`/testcases/${id}/tags`, tags).then((r) => r.data),
  versions: (id: string) => api.get<TestCaseVersion[]>(`/testcases/${id}/versions`).then((r) => r.data),
  startRecording: (id: string) =>
    api.post<AutomationRecordingSession>(`/testcases/${id}/automation/recordings/start`).then((r) => r.data),
  appendRecordingEvents: (id: string, sessionId: string, p: { sessionToken: string; events: unknown[] }) =>
    api.post(`/testcases/${id}/automation/recordings/${sessionId}/events`, p).then((r) => r.data),
  finishRecording: (
    id: string,
    sessionId: string,
    p: { sessionToken: string; definition?: RecordedScenarioDefinition | null },
  ) =>
    api
      .post<TestCaseAutomation>(`/testcases/${id}/automation/recordings/${sessionId}/finish`, p)
      .then((r) => r.data),
  validateAutomation: (id: string) =>
    api.post<TestCaseAutomation>(`/testcases/${id}/automation/validate`).then((r) => r.data),
  dryRunAutomation: (id: string, p: { gateway?: string | null; frontend?: string | null } = {}) =>
    api.post<AutomationRunResponse>(`/testcases/${id}/automation/dry-run`, p).then((r) => r.data),
  publishAutomation: (id: string) =>
    api.post<TestCaseAutomation>(`/testcases/${id}/automation/publish`).then((r) => r.data),
  setAutomationPrerequisite: (id: string, prerequisiteTestCaseId: string | null) =>
    api
      .put<TestCase>(`/testcases/${id}/automation/prerequisite`, { prerequisiteTestCaseId })
      .then((r) => r.data),
};

// ---------- Custom Fields ----------
export const CustomFieldsApi = {
  list: (projectId: string, includeArchived = false) =>
    api
      .get<CustomFieldDefinition[]>('/custom-fields', { params: { projectId, includeArchived } })
      .then((r) => r.data),
  create: (p: {
    projectId: string;
    key: string;
    label: string;
    description?: string | null;
    type: CustomFieldType;
    options?: string[] | null;
    required: boolean;
    position: number;
  }) => api.post<CustomFieldDefinition>('/custom-fields', p).then((r) => r.data),
  update: (
    id: string,
    p: {
      label: string;
      description?: string | null;
      options?: string[] | null;
      required: boolean;
      position: number;
    },
  ) => api.put<CustomFieldDefinition>(`/custom-fields/${id}`, p).then((r) => r.data),
  remove: (id: string) => api.delete(`/custom-fields/${id}`).then((r) => r.data),
};

// ---------- Runs ----------
export interface RunsListParams {
  projectId?: string;
  milestoneId?: string;
  status?: 'Open' | 'Closed';
  search?: string;
  includeDeleted?: boolean;
  page?: number;
  pageSize?: number;
}

export const RunsApi = {
  list: (params: RunsListParams = {}) =>
    api.get<PagedResult<Run>>('/runs', { params }).then((r) => r.data),
  get: (id: string) => api.get<Run>(`/runs/${id}`).then((r) => r.data),
  create: (p: { projectId: string; name: string; description?: string; milestoneId?: string; testCaseIds: string[]; environment?: string; dueDateUtc?: string; autoExecute?: boolean }) =>
    api.post<Run>('/runs', p).then((r) => r.data),
  update: (id: string, p: { name?: string; description?: string; environment?: string; dueDateUtc?: string }) =>
    api.patch<Run>(`/runs/${id}`, p).then((r) => r.data),
  duplicate: (id: string, name?: string) =>
    api.post<Run>(`/runs/${id}/duplicate`, { name }).then((r) => r.data),
  addCases: (id: string, testCaseIds: string[]) =>
    api.post<Run>(`/runs/${id}/cases`, { testCaseIds }).then((r) => r.data),
  removeCase: (runId: string, caseId: string) =>
    api.delete(`/runs/${runId}/cases/${caseId}`).then((r) => r.data),
  remove: (id: string) => api.delete(`/runs/${id}`).then((r) => r.data),
  close: (id: string) => api.post<Run>(`/runs/${id}/close`).then((r) => r.data),
  reopen: (id: string) => api.post<Run>(`/runs/${id}/reopen`).then((r) => r.data),
  cases: (id: string) =>
    api
      .get<PagedResult<RunCase>>(`/runs/${id}/cases`, {
        params: { pageSize: 500 },
      })
      .then((r) =>
        r.data.items.map((c) => ({
          ...c,
          assignedToUserId: c.assignedToUserId ?? null,
          currentStatus: normalizeResultStatus(c.currentStatus),
        })),
      ),
  comments: (id: string) => api.get<RunComment[]>(`/runs/${id}/comments`).then((r) => r.data),
  addComment: (id: string, body: string) =>
    api.post<RunComment>(`/runs/${id}/comments`, { body }).then((r) => r.data),
  recordResult: (p: { runCaseId: string; status: ResultStatus; comment?: string }) =>
    api.post<RunResult>('/results', p).then((r) => r.data),
  runAutomatic: (
    runCaseId: string,
    p: {
      gateway?: string;
      frontend?: string;
    },
  ) => api.post<AutomaticRunResult>(`/results/${runCaseId}/automatic`, p).then((r) => r.data),
  runAutomaticAll: (runId: string) =>
    api.post<RunAutomationExecution>(`/runs/${runId}/automatic`).then((r) => r.data),
  automationExecutions: (runId: string, params: { page?: number; pageSize?: number } = {}) =>
    api
      .get<PagedResult<RunAutomationExecution>>(`/runs/${runId}/automation-executions`, { params })
      .then((r) => r.data),
  bulkRecordResult: (p: { runCaseIds: string[]; status: ResultStatus; comment?: string }) =>
    api.post<{ succeeded: number; failed: number }>('/results/bulk', p).then((r) => r.data),
  resultHistory: (runCaseId: string) =>
    api.get<RunResult[]>(`/results/${runCaseId}`).then((r) => r.data),
  setAssignee: (runCaseId: string, userId: string | null) =>
    api.put(`/results/${runCaseId}/assignee`, { assignedToUserId: userId }).then((r) => r.data),
};

// ---------- Attachments ----------
export const AttachmentsApi = {
  list: (runId: string, runCaseId?: string) =>
    api
      .get<PagedResult<RunAttachment>>(`/runs/${runId}/attachments`, {
        params: { runCaseId, pageSize: 200 },
      })
      .then((r) => r.data.items),
  upload: (
    runId: string,
    file: File,
    opts: { runCaseId?: string; runResultId?: string; onProgress?: (pct: number) => void } = {},
  ) => {
    const fd = new FormData();
    fd.append('file', file);
    if (opts.runCaseId) fd.append('runCaseId', opts.runCaseId);
    if (opts.runResultId) fd.append('runResultId', opts.runResultId);
    return api
      .post<RunAttachment>(`/runs/${runId}/attachments`, fd, {
        headers: { 'Content-Type': 'multipart/form-data' },
        onUploadProgress: (e) => {
          if (opts.onProgress && e.total) opts.onProgress(Math.round((e.loaded / e.total) * 100));
        },
      })
      .then((r) => r.data);
  },
  url: (id: string) => api.get<AttachmentUrl>(`/attachments/${id}/url`).then((r) => r.data),
  remove: (id: string) => api.delete(`/attachments/${id}`).then((r) => r.data),
};

// ---------- Run Schedules ----------
export interface RunSchedulesListParams {
  projectId?: string;
  includeInactive?: boolean;
  page?: number;
  pageSize?: number;
}

export interface RunSchedulePayload {
  projectId: string;
  name: string;
  description?: string | null;
  environment?: string | null;
  recurrenceType: ScheduleRecurrenceType;
  timeOfDayMinutes: number;
  weekDaysMask: number;
  timeZoneId?: string | null;
  startAtUtc?: string | null;
  endAtUtc?: string | null;
  testCaseIds: string[];
  loops?: number;
  threads?: number;
  rampup?: number;
  skippable?: boolean;
  gateway?: string | null;
  frontend?: string | null;
}

export type UpdateRunSchedulePayload = Partial<Omit<RunSchedulePayload, 'projectId'>> & {
  testCaseIds?: string[];
};

export const RunSchedulesApi = {
  list: (params: RunSchedulesListParams = {}) =>
    api.get<PagedResult<RunSchedule>>('/run-schedules', { params }).then((r) => r.data),
  get: (id: string) => api.get<RunScheduleDetail>(`/run-schedules/${id}`).then((r) => r.data),
  create: (p: RunSchedulePayload) =>
    api.post<RunScheduleDetail>('/run-schedules', p).then((r) => r.data),
  update: (id: string, p: UpdateRunSchedulePayload) =>
    api.patch<RunScheduleDetail>(`/run-schedules/${id}`, p).then((r) => r.data),
  pause: (id: string) => api.post<RunSchedule>(`/run-schedules/${id}/pause`).then((r) => r.data),
  resume: (id: string) => api.post<RunSchedule>(`/run-schedules/${id}/resume`).then((r) => r.data),
  runNow: (id: string) => api.post<RunSchedule>(`/run-schedules/${id}/run-now`).then((r) => r.data),
  remove: (id: string) => api.delete(`/run-schedules/${id}`).then((r) => r.data),
  executions: (id: string, params: { page?: number; pageSize?: number } = {}) =>
    api.get<PagedResult<RunScheduleExecution>>(`/run-schedules/${id}/executions`, { params }).then((r) => r.data),
};

// ---------- Inline Media (images in markdown) ----------
export const MediaApi = {
  uploadImage: (file: File) => {
    const fd = new FormData();
    fd.append('file', file);
    return api
      .post<{ id: string; url: string }>('/media/images', fd, {
        headers: { 'Content-Type': 'multipart/form-data' },
      })
      .then((r) => r.data);
  },
};

// ---------- Reporting ----------
export const ReportingApi = {
  workspaceDashboard: () =>
    api.get<WorkspaceDashboard>('/dashboards/workspace').then((r) => r.data),
  projectDashboard: (projectId: string) =>
    api.get<ProjectDashboard>(`/dashboards/project/${projectId}`).then((r) => r.data),
  trends: (params: { from?: string; to?: string; granularity?: 'day' | 'week' | 'month'; projectId?: string }) =>
    api.get<TrendBucket[]>('/reporting/trends', { params }).then((r) => r.data),
  topFailures: (params: { days?: number; limit?: number; projectId?: string } = {}) =>
    api.get<TopFailingTest[]>('/reporting/top-failures', { params }).then((r) => r.data),
  users: (params: { days?: number; limit?: number; projectId?: string } = {}) =>
    api.get<UserActivity[]>('/reporting/users', { params }).then((r) => r.data),
};
