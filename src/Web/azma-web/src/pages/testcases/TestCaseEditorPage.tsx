import { useState, useEffect, useCallback, useRef, type FormEvent } from 'react';
import { useParams, useNavigate, useSearchParams, Link } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { TestCasesApi, ProjectsApi, CustomFieldsApi, MediaApi } from '@/lib/queries/endpoints';
import type {
  AutomationRecordingSession,
  AutomationRunResponse,
  RecordedScenarioDefinition,
  TestCaseAutomation,
  TestCasePriority,
  TestCaseStatus,
  TestCaseType,
} from '@/lib/types';
import { PageHeader } from '@/components/common/PageHeader';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select } from '@/components/ui/native-select';
import { Badge } from '@/components/ui/badge';
import { MarkdownEditor } from '@/components/editor/MarkdownEditor';
import { CustomFieldsForm } from '@/components/customfields/CustomFieldsForm';
import { StepsJsonImportDialog } from './StepsJsonImportDialog';
import { Plus, Trash2, ChevronRight, X, Save, GripVertical, ChevronUp, ChevronDown, Copy, ClipboardPaste, CheckSquare, Square, XCircle, FileJson, Radio, ShieldCheck, PlayCircle, Rocket } from 'lucide-react';
import { toast } from 'sonner';
import { extractError } from '@/lib/api';
import { cn } from '@/lib/utils';
import { motion, AnimatePresence } from 'framer-motion';
import { useStepClipboard, type ClipboardStep } from '@/lib/stores/step-clipboard';

const DRAFT_KEY = 'azma-testcase-new-draft';

interface StepDraft {
  action: string;
  expectedResult: string;
}

function createDefaultRecording(title: string): RecordedScenarioDefinition {
  return {
    name: title || null,
    startUrl: '/',
    actions: [],
    networkCaptures: [],
    expectations: [],
    timeoutSeconds: 60,
    steps: [],
    schemaVersion: 2,
  };
}

function formatRecordingJson(value: RecordedScenarioDefinition) {
  return JSON.stringify(value, null, 2);
}

function hasMeaningfulRecordingDefinition(value: RecordedScenarioDefinition | null | undefined) {
  return Boolean(
    value &&
    ((value.actions?.length ?? 0) > 0 ||
      (value.networkCaptures?.length ?? 0) > 0 ||
      (value.expectations?.length ?? 0) > 0 ||
      (value.steps?.length ?? 0) > 0)
  );
}

function createRequestId() {
  const cryptoApi = globalThis.crypto;
  if (typeof cryptoApi?.randomUUID === 'function') {
    return cryptoApi.randomUUID();
  }

  if (typeof cryptoApi?.getRandomValues === 'function') {
    const bytes = new Uint8Array(16);
    cryptoApi.getRandomValues(bytes);
    bytes[6] = (bytes[6] & 0x0f) | 0x40;
    bytes[8] = (bytes[8] & 0x3f) | 0x80;
    const hex = Array.from(bytes, (value) => value.toString(16).padStart(2, '0')).join('');
    return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
  }

  return `azma-${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`;
}

function automationStatusLabel(status?: string | null) {
  switch (status) {
    case 'Draft': return 'Draft recording';
    case 'Validated': return 'Validated';
    case 'Published': return 'Published';
    case 'Broken': return 'Broken';
    default: return 'No automation';
  }
}

function automationStatusVariant(status?: string | null) {
  switch (status) {
    case 'Published': return 'success' as const;
    case 'Validated': return 'info' as const;
    case 'Draft': return 'warning' as const;
    case 'Broken': return 'destructive' as const;
    default: return 'muted' as const;
  }
}

type RecorderStatus =
  | 'idle'
  | 'extension-missing'
  | 'starting'
  | 'recording'
  | 'events-received'
  | 'ready-to-save'
  | 'error';

type RecorderExtensionMessage = {
  source?: string;
  type?: string;
  payload?: {
    eventCount?: number;
    stepCount?: number;
    message?: string;
    requestId?: string;
    targetTabId?: number;
    automation?: TestCaseAutomation | null;
    state?: {
      sessionId?: string;
      sessionToken?: string;
      expiresAtUtc?: string;
      testCaseId?: string;
      targetUrl?: string;
      active?: boolean;
      paused?: boolean;
      eventCount?: number;
      stepCount?: number;
      automation?: TestCaseAutomation | null;
    };
  };
};

function isValidTargetUrl(value: string) {
  try {
    const url = new URL(value);
    return url.protocol === 'http:' || url.protocol === 'https:';
  } catch {
    return false;
  }
}

function recordingStartUrlFromJson(value?: string | null) {
  if (!value) return '';
  try {
    const definition = JSON.parse(value) as RecordedScenarioDefinition;
    const recorded = definition.startUrl
      ?? definition.steps?.flatMap((step) => step.actions ?? []).find((x) => x.type === 'Navigate')?.url
      ?? definition.actions?.find((x) => x.type === 'Navigate')?.url
      ?? '';
    const baseUrl = definition.variables?.baseUrl?.replace(/\/$/, '') ?? '';
    return baseUrl ? recorded.replace(/\{\{\s*baseUrl\s*\}\}/gi, baseUrl) : recorded;
  } catch {
    return '';
  }
}

function recorderStatusLabel(status: RecorderStatus, extensionReady: boolean, eventCount: number, stepCount: number) {
  if (!extensionReady && status !== 'recording' && status !== 'events-received' && status !== 'ready-to-save') {
    return 'Extension not installed';
  }
  switch (status) {
    case 'starting': return 'Opening target tab';
    case 'recording': return 'Recording';
    case 'events-received': return `${stepCount} steps · ${eventCount} events`;
    case 'ready-to-save': return 'Ready to save';
    case 'error': return 'Recorder error';
    default: return 'Extension ready';
  }
}

function recorderStatusVariant(status: RecorderStatus, extensionReady: boolean) {
  if (!extensionReady && status !== 'recording' && status !== 'events-received' && status !== 'ready-to-save') {
    return 'destructive' as const;
  }
  switch (status) {
    case 'recording':
    case 'events-received':
      return 'warning' as const;
    case 'ready-to-save':
      return 'success' as const;
    case 'error':
      return 'destructive' as const;
    default:
      return 'info' as const;
  }
}

export default function TestCaseEditorPage() {
  const { id } = useParams<{ id: string }>();
  const isNew = !id;
  const navigate = useNavigate();
  const qc = useQueryClient();
  const [params] = useSearchParams();

  const { data: projects } = useQuery({ queryKey: ['projects'], queryFn: () => ProjectsApi.list() });
  const { data: existing } = useQuery({
    queryKey: ['testcase', id],
    queryFn: () => TestCasesApi.get(id!),
    enabled: !isNew,
  });

  const [projectId, setProjectId] = useState(params.get('projectId') ?? '');
  const [suiteId, setSuiteId] = useState(params.get('suiteId') ?? '');
  const [sectionId, setSectionId] = useState(params.get('sectionId') ?? '');
  const [title, setTitle] = useState('');
  const [preconditions, setPreconditions] = useState('');
  const [type, setType] = useState<TestCaseType>('Functional');
  const [status, setStatus] = useState<TestCaseStatus>('Active');
  const [priority, setPriority] = useState<TestCasePriority>('Medium');
  const [tags, setTags] = useState<string[]>([]);
  const [tagInput, setTagInput] = useState('');
  const [steps, setSteps] = useState<StepDraft[]>([{ action: '', expectedResult: '' }]);
  const [customFields, setCustomFields] = useState<Record<string, unknown>>({});
  const [dragIndex, setDragIndex] = useState<number | null>(null);
  const [dropIndex, setDropIndex] = useState<number | null>(null);
  const isDraggingFromHandle = useRef(false);
  const [selectedSteps, setSelectedSteps] = useState<Set<number>>(new Set());
  const [jsonImportOpen, setJsonImportOpen] = useState(false);
  const [recordingSession, setRecordingSession] = useState<AutomationRecordingSession | null>(null);
  const [recordingTargetUrl, setRecordingTargetUrl] = useState('');
  const [recordingJson, setRecordingJson] = useState(() => formatRecordingJson(createDefaultRecording('')));
  const [showDeveloperJson, setShowDeveloperJson] = useState(false);
  const [recorderExtensionReady, setRecorderExtensionReady] = useState(false);
  const [recorderStatus, setRecorderStatus] = useState<RecorderStatus>('idle');
  const [recordedEventCount, setRecordedEventCount] = useState(0);
  const [recordedStepCount, setRecordedStepCount] = useState(0);
  const [automationResult, setAutomationResult] = useState<TestCaseAutomation | null>(null);
  const [dryRunResult, setDryRunResult] = useState<AutomationRunResponse | null>(null);
  const [prerequisiteTestCaseId, setPrerequisiteTestCaseId] = useState('');
  const clipboard = useStepClipboard();

  const { data: cfDefs } = useQuery({
    queryKey: ['custom-fields', projectId],
    queryFn: () => CustomFieldsApi.list(projectId),
    enabled: !!projectId,
  });

  const { data: suites } = useQuery({
    queryKey: ['suites', projectId],
    queryFn: () => ProjectsApi.suites(projectId),
    enabled: !!projectId,
  });

  const { data: recordedScenarios } = useQuery({
    queryKey: ['testcases', 'scenario-prerequisites', projectId],
    queryFn: () => TestCasesApi.list({
      projectId,
      automationEngine: 'ScenarioRecording',
      pageSize: 200,
    }),
    enabled: !!projectId && !isNew,
  });

  useEffect(() => {
    if (existing) {
      setProjectId(existing.projectId);
      setSuiteId(existing.suiteId ?? '');
      setSectionId(existing.sectionId ?? '');
      setTitle(existing.title);
      setPreconditions(existing.preconditions ?? '');
      setType(existing.type);
      setStatus(existing.status);
      setPriority(existing.priority);
      setAutomationResult({
        testCaseId: existing.id,
        automationEngine: existing.automationEngine,
        automationStatus: existing.automationStatus,
        automationDefinitionVersion: existing.automationDefinitionVersion,
        automationRecordingJson: existing.automationRecordingJson,
        isValid: existing.automationStatus === 'Validated' || existing.automationStatus === 'Published',
        validationErrors: [],
      });
      const nextRecordingJson = existing.automationRecordingJson ??
        formatRecordingJson(createDefaultRecording(existing.title));
      setRecordingJson(nextRecordingJson);
      setRecordingTargetUrl(recordingStartUrlFromJson(nextRecordingJson));
      setPrerequisiteTestCaseId(existing.automationPrerequisiteTestCaseId ?? '');
      setTags(existing.tags);
      setSteps(existing.steps.map((s) => ({ action: s.action, expectedResult: s.expectedResult ?? '' })));
      setCustomFields(existing.customFields ?? {});
    }
  }, [existing]);

  useEffect(() => {
    if (suites && suites.length > 0 && !suiteId) {
      setSuiteId(suites[0].id);
    }
  }, [suites]); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    const onRecorderMessage = (event: MessageEvent<RecorderExtensionMessage>) => {
      if (event.source !== window || event.data?.source !== 'AZMA_RECORDER_EXTENSION') return;

      switch (event.data.type) {
        case 'AZMA_RECORDER_AVAILABLE':
          setRecorderExtensionReady(true);
          setRecorderStatus((current) => current === 'extension-missing' ? 'idle' : current);
          break;
        case 'AZMA_RECORDER_STARTED':
          setRecorderExtensionReady(true);
          setRecorderStatus('recording');
          break;
        case 'AZMA_RECORDER_EVENT_COUNT':
        case 'AZMA_RECORDER_SCENARIO_UPDATED':
          setRecorderExtensionReady(true);
          setRecordedEventCount(event.data.payload?.eventCount ?? 0);
          setRecordedStepCount(event.data.payload?.stepCount ?? 0);
          setRecorderStatus('events-received');
          break;
        case 'AZMA_RECORDER_STATE': {
          setRecorderExtensionReady(true);
          const state = event.data.payload?.state;
          if (state && state.testCaseId === id && state.active && state.sessionId && state.sessionToken) {
            setRecordingSession({
              sessionId: state.sessionId,
              sessionToken: state.sessionToken,
              expiresAtUtc: state.expiresAtUtc ?? new Date(Date.now() + 60 * 60 * 1000).toISOString(),
            });
            if (state.targetUrl) setRecordingTargetUrl(state.targetUrl);
            setRecordedEventCount(state.eventCount ?? 0);
            setRecordedStepCount(state.stepCount ?? 0);
            setRecorderStatus(state.paused ? 'ready-to-save' : 'recording');
          }
          if (state && state.testCaseId === id && state.automation) {
            setAutomationResult(state.automation);
            if (state.automation.automationRecordingJson) {
              setRecordingJson(state.automation.automationRecordingJson);
              setRecordingTargetUrl(recordingStartUrlFromJson(state.automation.automationRecordingJson));
            }
          }
          break;
        }
        case 'AZMA_RECORDER_STOPPED':
          setRecorderExtensionReady(true);
          if (typeof event.data.payload?.eventCount === 'number') {
            setRecordedEventCount(event.data.payload.eventCount);
          }
          if (typeof event.data.payload?.stepCount === 'number') {
            setRecordedStepCount(event.data.payload.stepCount);
          }
          if (event.data.payload?.automation) {
            const result = event.data.payload.automation;
            setAutomationResult(result);
            if (result.automationRecordingJson) {
              setRecordingJson(result.automationRecordingJson);
              setRecordingTargetUrl(recordingStartUrlFromJson(result.automationRecordingJson));
            }
            setRecordingSession(null);
            setRecorderStatus('idle');
            qc.invalidateQueries({ queryKey: ['testcase', id] });
            qc.invalidateQueries({ queryKey: ['testcases'] });
            if (!event.data.payload?.requestId) toast.success('Draft recording saved');
          } else {
            setRecorderStatus('ready-to-save');
          }
          break;
        case 'AZMA_RECORDER_ERROR':
          setRecorderStatus('error');
          toast.error(event.data.payload?.message ?? 'Recorder extension failed');
          break;
      }
    };

    window.addEventListener('message', onRecorderMessage);
    window.postMessage({ source: 'AZMA_WEB', type: 'AZMA_RECORDER_PING' }, window.location.origin);
    const missingTimer = window.setTimeout(() => {
      setRecorderExtensionReady((ready) => {
        if (!ready) setRecorderStatus('extension-missing');
        return ready;
      });
    }, 1200);

    return () => {
      window.removeEventListener('message', onRecorderMessage);
      window.clearTimeout(missingTimer);
    };
  }, [id, qc]);

  // Load draft for new test cases on mount
  useEffect(() => {
    if (!isNew) return;
    const raw = localStorage.getItem(DRAFT_KEY);
    if (!raw) return;
    try {
      const draft = JSON.parse(raw) as {
        projectId?: string; suiteId?: string; sectionId?: string; title?: string; preconditions?: string;
        type?: TestCaseType; priority?: TestCasePriority; tags?: string[];
        steps?: StepDraft[]; customFields?: Record<string, unknown>; savedAt?: number;
      };
      if (draft.savedAt && Date.now() - draft.savedAt > 24 * 60 * 60 * 1000) {
        localStorage.removeItem(DRAFT_KEY);
        return;
      }
      const hasMeaningfulContent = draft.title || draft.steps?.some((s) => s.action);
      if (!hasMeaningfulContent) return;
      if (draft.projectId) setProjectId(draft.projectId);
      if (draft.suiteId) setSuiteId(draft.suiteId);
      if (draft.sectionId) setSectionId(draft.sectionId);
      if (draft.title) setTitle(draft.title);
      if (draft.preconditions) setPreconditions(draft.preconditions);
      if (draft.type) setType(draft.type);
      if (draft.priority) setPriority(draft.priority);
      if (draft.tags?.length) setTags(draft.tags);
      if (draft.steps?.length) setSteps(draft.steps);
      if (draft.customFields) setCustomFields(draft.customFields);
      toast.info('پیش‌نویس قبلی بازیابی شد', { duration: 4000 });
    } catch {
      localStorage.removeItem(DRAFT_KEY);
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Auto-save draft for new test cases
  useEffect(() => {
    if (!isNew) return;
    const hasMeaningfulContent = title || preconditions || steps.some((s) => s.action);
    if (!hasMeaningfulContent) return;
    const timer = setTimeout(() => {
      localStorage.setItem(DRAFT_KEY, JSON.stringify({
        projectId, suiteId, sectionId, title, preconditions, type, priority,
        tags, steps, customFields,
        savedAt: Date.now(),
      }));
    }, 800);
    return () => clearTimeout(timer);
  }, [isNew, projectId, suiteId, sectionId, title, preconditions, type, priority, tags, steps, customFields]);

  const saveMut = useMutation({
    mutationFn: async () => {
      if (isNew) {
        const created = await TestCasesApi.create({
          projectId,
          suiteId,
          sectionId: sectionId || undefined,
          title,
          preconditions: preconditions || undefined,
          type,
          priority,
          status,
          tags,
          steps: steps.filter((s) => s.action.trim()).map((s) => ({ action: s.action, expectedResult: s.expectedResult || null })),
          customFields,
        });
        return created;
      } else {
        const updated = await TestCasesApi.update(id!, {
          suiteId: suiteId || null,
          sectionId: sectionId || null,
          title,
          preconditions,
          type,
          status,
          priority,
          customFields,
        });
        await TestCasesApi.setSteps(id!, steps.filter((s) => s.action.trim()).map((s) => ({ action: s.action, expectedResult: s.expectedResult || null })));
        await TestCasesApi.setTags(id!, tags);
        return updated;
      }
    },
    onSuccess: (tc) => {
      if (isNew) localStorage.removeItem(DRAFT_KEY);
      toast.success(isNew ? 'سناریو ساخته شد' : 'تغییرات ذخیره شد');
      qc.invalidateQueries({ queryKey: ['testcases'] });
      qc.invalidateQueries({ queryKey: ['testcase', tc.id] });
      navigate(projectId ? `/repository/${projectId}` : `/testcases/${tc.id}`);
    },
    onError: (err) => toast.error(extractError(err)),
  });

  function parseRecordingDefinition(): RecordedScenarioDefinition | null {
    try {
      const parsed = JSON.parse(recordingJson) as RecordedScenarioDefinition;
      return {
        ...parsed,
        name: parsed.name ?? (title || null),
        actions: parsed.actions ?? [],
        networkCaptures: parsed.networkCaptures ?? [],
        expectations: parsed.expectations ?? [],
        steps: parsed.steps ?? [],
        schemaVersion: parsed.schemaVersion ?? (parsed.steps ? 2 : 1),
      };
    } catch {
      toast.error('JSON آرتیفکت رکورد معتبر نیست');
      return null;
    }
  }

  function manualRecordingDefinitionOverride(): RecordedScenarioDefinition | null {
    if (!showDeveloperJson) return null;
    const definition = parseRecordingDefinition();
    if (!definition) throw new Error('Invalid recording JSON.');
    return hasMeaningfulRecordingDefinition(definition) ? definition : null;
  }

  function postRecorderMessage(type: string, payload?: Record<string, unknown>) {
    window.postMessage({ source: 'AZMA_WEB', type, payload }, window.location.origin);
  }

  function startRecorderAndWait(session: AutomationRecordingSession, targetUrl: string) {
    const requestId = createRequestId();
    return new Promise<void>((resolve, reject) => {
      const timeout = window.setTimeout(() => {
        cleanup();
        reject(new Error('Recorder extension did not open the target page. Reload this Azma page after installing/updating the extension, then try again.'));
      }, 10_000);

      const onMessage = (event: MessageEvent<RecorderExtensionMessage>) => {
        if (event.source !== window ||
            event.data?.source !== 'AZMA_RECORDER_EXTENSION' ||
            event.data.payload?.requestId !== requestId) return;

        if (event.data.type === 'AZMA_RECORDER_STARTED') {
          cleanup();
          resolve();
        } else if (event.data.type === 'AZMA_RECORDER_ERROR') {
          cleanup();
          reject(new Error(event.data.payload?.message ?? 'Recorder extension failed to open the target page.'));
        }
      };

      function cleanup() {
        window.clearTimeout(timeout);
        window.removeEventListener('message', onMessage);
      }

      window.addEventListener('message', onMessage);
      postRecorderMessage('AZMA_RECORDER_START', {
        requestId,
        sessionId: session.sessionId,
        sessionToken: session.sessionToken,
        expiresAtUtc: session.expiresAtUtc,
        testCaseId: id,
        targetUrl,
        gatewayBaseUrl: window.location.origin,
        initialDefinition: (() => {
          const definition = parseRecordingDefinition();
          return hasMeaningfulRecordingDefinition(definition) ? definition : null;
        })(),
        initialManualSteps: steps
          .filter((step) => step.action.trim())
          .map((step) => ({ action: step.action, expectedResult: step.expectedResult || null })),
      });
    });
  }

  function stopRecorderAndWait(definition?: RecordedScenarioDefinition | null) {
    const requestId = createRequestId();
    return new Promise<TestCaseAutomation | null>((resolve, reject) => {
      const timeout = window.setTimeout(() => {
        cleanup();
        reject(new Error('Recorder did not confirm that all events were saved. Please retry.'));
      }, 15_000);

      const onMessage = (event: MessageEvent<RecorderExtensionMessage>) => {
        if (event.source !== window ||
            event.data?.source !== 'AZMA_RECORDER_EXTENSION' ||
            event.data.payload?.requestId !== requestId) return;

        if (event.data.type === 'AZMA_RECORDER_STOPPED') {
          cleanup();
          resolve(event.data.payload?.automation ?? null);
        } else if (event.data.type === 'AZMA_RECORDER_ERROR') {
          cleanup();
          reject(new Error(event.data.payload?.message ?? 'Recorder failed to save captured events.'));
        }
      };

      function cleanup() {
        window.clearTimeout(timeout);
        window.removeEventListener('message', onMessage);
      }

      window.addEventListener('message', onMessage);
      postRecorderMessage('AZMA_RECORDER_STOP', { requestId, definition: definition ?? null });
    });
  }

  const startRecordingMut = useMutation({
    mutationFn: async (targetUrl: string) => {
      if (!id) throw new Error('Save the scenario before recording automation.');
      if (!isValidTargetUrl(targetUrl)) throw new Error('Target URL must be a valid http(s) URL.');
      if (!recorderExtensionReady) throw new Error('Azma Recorder extension is not installed or enabled.');
      const session = await TestCasesApi.startRecording(id);
      await startRecorderAndWait(session, targetUrl);
      return { session, targetUrl };
    },
    onSuccess: ({ session, targetUrl }) => {
      setRecordingSession(session);
      setDryRunResult(null);
      setRecordedEventCount(0);
      setRecordedStepCount(0);
      setRecorderStatus('recording');
      toast.success('Recording session started');
    },
    onError: (err) => toast.error(extractError(err)),
  });

  const finishRecordingMut = useMutation({
    mutationFn: async (options?: { silent?: boolean }) => {
      if (!id || !recordingSession) throw new Error('Recording session is not active.');
      const definition = manualRecordingDefinitionOverride();
      const extensionResult = await stopRecorderAndWait(definition);
      // Version 0.2+ saves directly from the target-page studio. Keep the
      // authenticated finish path as a compatibility fallback for older builds.
      const result = extensionResult ?? await TestCasesApi.finishRecording(id, recordingSession.sessionId, {
          sessionToken: recordingSession.sessionToken,
          definition,
        });
      return { result, silent: Boolean(options?.silent) };
    },
    onSuccess: ({ result, silent }) => {
      setAutomationResult(result);
      if (result.automationRecordingJson) setRecordingJson(result.automationRecordingJson);
      setRecordingTargetUrl(recordingStartUrlFromJson(result.automationRecordingJson));
      setRecordingSession(null);
      setRecorderStatus('idle');
      setRecordedEventCount(0);
      setRecordedStepCount(0);
      setDryRunResult(null);
      qc.invalidateQueries({ queryKey: ['testcase', id] });
      qc.invalidateQueries({ queryKey: ['testcases'] });
      if (!silent) toast.success('Draft recording saved');
    },
    onError: (err) => toast.error(extractError(err)),
  });

  async function ensureRecordingDraftSaved() {
    if (!recordingSession) return;
    await finishRecordingMut.mutateAsync({ silent: true });
  }

  const validateAutomationMut = useMutation({
    mutationFn: async () => {
      await ensureRecordingDraftSaved();
      return TestCasesApi.validateAutomation(id!);
    },
    onSuccess: (result) => {
      setAutomationResult(result);
      qc.invalidateQueries({ queryKey: ['testcase', id] });
      qc.invalidateQueries({ queryKey: ['testcases'] });
      toast.success('Automation validated');
    },
    onError: (err) => toast.error(extractError(err)),
  });

  const setPrerequisiteMut = useMutation({
    mutationFn: (nextId: string) => TestCasesApi.setAutomationPrerequisite(id!, nextId || null),
    onSuccess: (updated) => {
      setPrerequisiteTestCaseId(updated.automationPrerequisiteTestCaseId ?? '');
      qc.setQueryData(['testcase', id], updated);
      toast.success('پیش‌نیاز اجرا ذخیره شد');
    },
    onError: (err) => {
      setPrerequisiteTestCaseId(existing?.automationPrerequisiteTestCaseId ?? '');
      toast.error(extractError(err));
    },
  });

  const dryRunAutomationMut = useMutation({
    mutationFn: async () => {
      await ensureRecordingDraftSaved();
      return TestCasesApi.dryRunAutomation(id!);
    },
    onSuccess: (result) => {
      setDryRunResult(result);
      toast[result.success ? 'success' : 'error'](result.success ? 'Dry run passed' : (result.errorMessage ?? 'Dry run failed'));
    },
    onError: (err) => toast.error(extractError(err)),
  });

  const publishAutomationMut = useMutation({
    mutationFn: async () => {
      await ensureRecordingDraftSaved();
      return TestCasesApi.publishAutomation(id!);
    },
    onSuccess: (result) => {
      setAutomationResult(result);
      qc.invalidateQueries({ queryKey: ['testcase', id] });
      qc.invalidateQueries({ queryKey: ['testcases'] });
      toast.success('Automation published');
    },
    onError: (err) => toast.error(extractError(err)),
  });

  const uploadImage = useCallback(async (file: File) => {
    const result = await MediaApi.uploadImage(file);
    return result.url;
  }, []);

  function addStep() {
    setSteps((s) => [...s, { action: '', expectedResult: '' }]);
  }
  function insertStep(at: number) {
    setSteps((s) => {
      const next = [...s];
      next.splice(at, 0, { action: '', expectedResult: '' });
      return next;
    });
  }
  function removeStep(i: number) {
    setSteps((s) => s.filter((_, idx) => idx !== i));
    setSelectedSteps((prev) => {
      const next = new Set<number>();
      for (const idx of prev) {
        if (idx < i) next.add(idx);
        else if (idx > i) next.add(idx - 1);
      }
      return next;
    });
  }
  function updateStep(i: number, key: keyof StepDraft, value: string) {
    setSteps((s) => s.map((st, idx) => (idx === i ? { ...st, [key]: value } : st)));
  }
  function moveStep(from: number, to: number) {
    setSteps((s) => {
      const next = [...s];
      const [moved] = next.splice(from, 1);
      next.splice(to, 0, moved);
      return next;
    });
    setSelectedSteps((prev) => {
      if (prev.size === 0) return prev;
      const next = new Set<number>();
      for (const idx of prev) {
        if (idx === from) { next.add(to); }
        else if (from < to && idx > from && idx <= to) { next.add(idx - 1); }
        else if (from > to && idx >= to && idx < from) { next.add(idx + 1); }
        else { next.add(idx); }
      }
      return next;
    });
  }
  function toggleStepSelection(i: number) {
    setSelectedSteps((prev) => {
      const next = new Set(prev);
      if (next.has(i)) next.delete(i);
      else next.add(i);
      return next;
    });
  }
  function selectAllSteps() {
    setSelectedSteps(new Set(steps.map((_, i) => i)));
  }
  function clearSelection() {
    setSelectedSteps(new Set());
  }
  function copySelectedSteps() {
    const toCopy: ClipboardStep[] = [...selectedSteps]
      .sort((a, b) => a - b)
      .map((i) => steps[i])
      .filter((s) => s.action.trim());
    if (toCopy.length === 0) {
      toast.error('گام‌های انتخاب‌شده خالی هستند');
      return;
    }
    clipboard.copy(toCopy, existing?.code);
    toast.success(`${toCopy.length} گام کپی شد`);
    clearSelection();
  }
  function pasteSteps() {
    if (clipboard.steps.length === 0) return;
    setSteps((s) => [...s, ...clipboard.steps.map((c) => ({ action: c.action, expectedResult: c.expectedResult }))]);
    toast.success(`${clipboard.steps.length} گام از ${clipboard.sourceLabel ?? 'کلیپ‌بورد'} اضافه شد`);
  }

  function importStepsFromJson(importedSteps: StepDraft[], mode: 'append' | 'replace') {
    setSteps((current) => {
      const onlyEmptyStep =
        current.length === 1 &&
        !current[0].action.trim() &&
        !current[0].expectedResult.trim();
      if (mode === 'replace' || onlyEmptyStep) return importedSteps;
      return [...current, ...importedSteps];
    });
    clearSelection();
    toast.success(`${importedSteps.length} گام از JSON وارد شد`);
  }

  function addTag(e: React.KeyboardEvent<HTMLInputElement>) {
    if ((e.key === 'Enter' || e.key === ',') && tagInput.trim()) {
      e.preventDefault();
      const t = tagInput.trim().replace(/,/g, '');
      if (!tags.includes(t)) setTags((ts) => [...ts, t]);
      setTagInput('');
    }
  }

  function onSubmit(e: FormEvent) {
    e.preventDefault();
    if (!projectId || !suiteId || !title.trim()) {
      toast.error('پروژه، سوئیت و عنوان الزامی هستند');
      return;
    }
    saveMut.mutate();
  }

  const currentAutomationStatus = automationResult?.automationStatus ?? existing?.automationStatus ?? 'None';
  const currentAutomationEngine = automationResult?.automationEngine ?? existing?.automationEngine ?? 'None';
  const currentAutomationVersion = automationResult?.automationDefinitionVersion ?? existing?.automationDefinitionVersion ?? null;
  const currentValidationErrors = automationResult?.validationErrors ?? [];
  const canUseAutomationPanel = !isNew && !!id;
  const hasRecordedAutomation = currentAutomationEngine === 'ScenarioRecording' && currentAutomationStatus !== 'None';
  const hasAnyAutomation = hasRecordedAutomation;
  const targetUrlValid = isValidTargetUrl(recordingTargetUrl);
  const canStartRecording = canUseAutomationPanel && targetUrlValid && recorderExtensionReady && !recordingSession;
  const automationActionPending = finishRecordingMut.isPending ||
    validateAutomationMut.isPending ||
    dryRunAutomationMut.isPending ||
    publishAutomationMut.isPending;
  const canValidateRecordedAutomation = canUseAutomationPanel &&
    (Boolean(recordingSession) || hasRecordedAutomation);
  const canDryRunRecordedAutomation = canValidateRecordedAutomation;
  const canPublishRecordedAutomation = canValidateRecordedAutomation &&
    currentAutomationStatus !== 'Published';
  const dryRunFailureEvidence = dryRunResult?.steps.find((step) => !step.success) ?? null;

  return (
    <motion.div initial={{ opacity: 0, y: 6 }} animate={{ opacity: 1, y: 0 }} className="space-y-6">
      <PageHeader
        breadcrumbs={
          <div className="flex items-center gap-1 text-xs text-muted-foreground">
            <Link
              to={projectId ? `/repository/${projectId}` : '/testcases'}
              className="hover:text-foreground"
            >سناریوها</Link>
            <ChevronRight className="size-3 rotate-180" />
            <span>{isNew ? 'جدید' : (existing?.code ?? '...')}</span>
          </div>
        }
        title={isNew ? 'سناریوی جدید' : `ویرایش ${existing?.code ?? ''}`}
        description="عنوان، پیش‌نیازها، مراحل اجرا و برچسب‌های سناریو را تعریف کنید."
      />

      <StepsJsonImportDialog
        open={jsonImportOpen}
        existingCount={steps.filter((s) => s.action.trim()).length}
        onClose={() => setJsonImportOpen(false)}
        onImport={importStepsFromJson}
      />

      <form onSubmit={onSubmit} className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div className="lg:col-span-2 space-y-6">
          <Card>
            <CardHeader>
              <CardTitle>اطلاعات اصلی</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div>
                  <Label>پروژه *</Label>
                  <Select
                    value={projectId}
                    onChange={(e) => {
                      setProjectId(e.target.value);
                      setSuiteId('');
                    }}
                    disabled={!isNew}
                    required
                  >
                    <option value="">انتخاب کنید</option>
                    {projects?.map((p) => (
                      <option key={p.id} value={p.id}>
                        {p.code} — {p.name}
                      </option>
                    ))}
                  </Select>
                </div>
                <div>
                  <Label>سوئیت *</Label>
                  <Select
                    value={suiteId}
                    onChange={(e) => setSuiteId(e.target.value)}
                    disabled={!isNew || !projectId}
                    required
                  >
                    <option value="">{projectId ? 'انتخاب کنید' : 'ابتدا پروژه'}</option>
                    {suites?.map((s) => (
                      <option key={s.id} value={s.id}>
                        {s.name}
                      </option>
                    ))}
                  </Select>
                </div>
              </div>
              <div>
                <Label>عنوان *</Label>
                <Input value={title} onChange={(e) => setTitle(e.target.value)} required />
              </div>
              <div>
                <Label>پیش‌نیازها</Label>
                <MarkdownEditor
                  value={preconditions}
                  onChange={setPreconditions}
                  placeholder="شرایط اولیه‌ای که قبل از اجرای سناریو باید برقرار باشد…"
                  minRows={4}
                  ariaLabel="پیش‌نیازها"
                  onUploadImage={uploadImage}
                />
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="flex flex-row items-center justify-between gap-2">
              <CardTitle>مراحل اجرا</CardTitle>
              <div className="flex items-center gap-1.5">
                <Button
                  type="button" variant="outline" size="xs"
                  onClick={() => setJsonImportOpen(true)}
                  className="text-primary border-primary/30 hover:bg-primary/5"
                >
                  <FileJson className="size-3.5" />
                  ورود JSON
                </Button>
                {selectedSteps.size === 0 && steps.length > 1 && (
                  <Button
                    type="button" variant="ghost" size="xs"
                    onClick={selectAllSteps}
                    className="text-muted-foreground"
                  >
                    <CheckSquare className="size-3.5" /> انتخاب همه
                  </Button>
                )}
                {clipboard.steps.length > 0 && (
                  <Button
                    type="button" variant="outline" size="xs"
                    onClick={pasteSteps}
                    className="text-primary border-primary/30 hover:bg-primary/5"
                  >
                    <ClipboardPaste className="size-3.5" />
                    جای‌گذاری
                    <Badge variant="default" size="sm">{clipboard.steps.length}</Badge>
                  </Button>
                )}
              </div>
            </CardHeader>
            <CardContent className="space-y-0">
              <AnimatePresence initial={false}>
                {steps.map((s, i) => (
                  <motion.div key={i} layout>
                    {/* Insert-between separator */}
                    {i > 0 && (
                      <div className="group relative flex items-center my-1 h-5">
                        <div className="flex-1 h-px bg-border" />
                        <button
                          type="button"
                          onClick={() => insertStep(i)}
                          className="opacity-0 group-hover:opacity-100 mx-2 flex items-center gap-1 px-2 py-0.5 text-[11px] text-muted-foreground hover:text-primary rounded border border-dashed border-transparent hover:border-primary/40 hover:bg-primary/5 transition-all"
                        >
                          <Plus className="size-3" /> افزودن گام
                        </button>
                        <div className="flex-1 h-px bg-border" />
                      </div>
                    )}
                    <motion.div
                      initial={{ opacity: 0, y: 6 }}
                      animate={{ opacity: 1, y: 0 }}
                      exit={{ opacity: 0, y: -6 }}
                      draggable
                      onDragStartCapture={(e) => {
                        if (!isDraggingFromHandle.current) { e.preventDefault(); return; }
                        setDragIndex(i);
                        e.dataTransfer.effectAllowed = 'move';
                      }}
                      onDragEnd={() => {
                        setDragIndex(null);
                        setDropIndex(null);
                        isDraggingFromHandle.current = false;
                      }}
                      onDragOver={(e) => { e.preventDefault(); setDropIndex(i); }}
                      onDrop={(e) => {
                        e.preventDefault();
                        if (dragIndex !== null && dragIndex !== i) moveStep(dragIndex, i);
                        setDragIndex(null);
                        setDropIndex(null);
                      }}
                      className={cn(
                        'rounded-lg border bg-secondary/30 p-4 space-y-3 transition-all',
                        dragIndex === i ? 'opacity-40 border-primary/40' : 'border-border',
                        dropIndex === i && dragIndex !== i && 'ring-2 ring-primary/40 border-primary/30',
                        selectedSteps.has(i) && 'ring-2 ring-primary/30 border-primary/40 bg-primary/5',
                      )}
                    >
                      <div className="flex items-center justify-between">
                        <div className="flex items-center gap-2">
                          <button
                            type="button"
                            onClick={() => toggleStepSelection(i)}
                            className={cn(
                              'shrink-0 size-5 rounded border flex items-center justify-center transition-colors',
                              selectedSteps.has(i)
                                ? 'bg-primary border-primary text-primary-foreground'
                                : 'border-border hover:border-primary/50 text-transparent hover:text-muted-foreground',
                            )}
                          >
                            {selectedSteps.has(i) ? (
                              <CheckSquare className="size-3.5" />
                            ) : (
                              <Square className="size-3.5" />
                            )}
                          </button>
                          <div
                            className="cursor-grab active:cursor-grabbing text-muted-foreground/50 hover:text-muted-foreground touch-none"
                            onMouseDown={() => { isDraggingFromHandle.current = true; }}
                            onMouseUp={() => { isDraggingFromHandle.current = false; }}
                          >
                            <GripVertical className="size-4" />
                          </div>
                          <Badge variant="default">گام {i + 1}</Badge>
                        </div>
                        <div className="flex items-center gap-0.5">
                          <Button
                            type="button" variant="ghost" size="icon-sm"
                            disabled={i === 0}
                            onClick={() => moveStep(i, i - 1)}
                            title="انتقال به بالا"
                          >
                            <ChevronUp className="size-3.5" />
                          </Button>
                          <Button
                            type="button" variant="ghost" size="icon-sm"
                            disabled={i === steps.length - 1}
                            onClick={() => moveStep(i, i + 1)}
                            title="انتقال به پایین"
                          >
                            <ChevronDown className="size-3.5" />
                          </Button>
                          <Button
                            type="button" variant="ghost" size="icon-sm"
                            onClick={() => removeStep(i)}
                            disabled={steps.length === 1}
                          >
                            <Trash2 className="size-4 text-destructive" />
                          </Button>
                        </div>
                      </div>
                      <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                        <div>
                          <div className="text-[11px] text-muted-foreground mb-1 font-medium">عمل / Action</div>
                          <MarkdownEditor
                            value={s.action}
                            onChange={(v) => updateStep(i, 'action', v)}
                            compact
                            minRows={2}
                            placeholder="چه کاری انجام شود؟"
                            onUploadImage={uploadImage}
                          />
                        </div>
                        <div>
                          <div className="text-[11px] text-muted-foreground mb-1 font-medium">نتیجه مورد انتظار / Expected</div>
                          <MarkdownEditor
                            value={s.expectedResult}
                            onChange={(v) => updateStep(i, 'expectedResult', v)}
                            compact
                            minRows={2}
                            placeholder="چه چیزی باید رخ دهد؟"
                            onUploadImage={uploadImage}
                          />
                        </div>
                      </div>
                    </motion.div>
                  </motion.div>
                ))}
              </AnimatePresence>

              <motion.button
                type="button"
                onClick={addStep}
                layout
                className="mt-3 w-full flex items-center justify-center gap-2 rounded-lg border-2 border-dashed border-border py-3 text-sm text-muted-foreground hover:border-primary/50 hover:text-primary hover:bg-primary/5 transition-colors"
              >
                <Plus className="size-4" />
                افزودن گام جدید
              </motion.button>
            </CardContent>
          </Card>

          {/* Floating selection action bar */}
          <AnimatePresence>
            {selectedSteps.size > 0 && (
              <motion.div
                initial={{ opacity: 0, y: 20, scale: 0.95 }}
                animate={{ opacity: 1, y: 0, scale: 1 }}
                exit={{ opacity: 0, y: 20, scale: 0.95 }}
                transition={{ type: 'spring', stiffness: 400, damping: 30 }}
                className="fixed bottom-6 left-1/2 -translate-x-1/2 z-50 flex items-center gap-3 rounded-xl border border-border bg-background/95 backdrop-blur-md shadow-lg px-5 py-3"
              >
                <span className="text-sm font-medium whitespace-nowrap">
                  <Badge variant="default" size="lg">{selectedSteps.size}</Badge>
                  <span className="ms-2 text-muted-foreground">گام انتخاب شده</span>
                </span>
                <div className="h-5 w-px bg-border" />
                <Button type="button" variant="default" size="sm" onClick={copySelectedSteps}>
                  <Copy className="size-3.5" /> کپی
                </Button>
                <Button type="button" variant="ghost" size="sm" onClick={clearSelection}>
                  <XCircle className="size-3.5" /> لغو
                </Button>
              </motion.div>
            )}
          </AnimatePresence>

          {cfDefs && cfDefs.filter((d) => !d.isArchived).length > 0 && (
            <Card>
              <CardHeader>
                <CardTitle>فیلدهای سفارشی</CardTitle>
              </CardHeader>
              <CardContent>
                <CustomFieldsForm
                  definitions={cfDefs.filter((d) => !d.isArchived)}
                  values={customFields}
                  onChange={setCustomFields}
                  onUploadImage={uploadImage}
                />
              </CardContent>
            </Card>
          )}
        </div>

        <div className="space-y-6">
          <Card>
            <CardHeader>
              <CardTitle>وضعیت و اولویت</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div>
                <Label>وضعیت</Label>
                <Select
                  value={status}
                  onChange={(e) => setStatus(e.target.value as TestCaseStatus)}
                >
                  <option value="Draft">پیش‌نویس</option>
                  <option value="Active">فعال</option>
                  <option value="Deprecated">منسوخ</option>
                </Select>
              </div>
              <div>
                <Label>اولویت</Label>
                <Select
                  value={priority}
                  onChange={(e) => setPriority(e.target.value as TestCasePriority)}
                >
                  <option value="Low">کم</option>
                  <option value="Medium">متوسط</option>
                  <option value="High">بالا</option>
                  <option value="Critical">بحرانی</option>
                </Select>
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>برچسب‌ها</CardTitle>
            </CardHeader>
            <CardContent className="space-y-3">
              <Input
                value={tagInput}
                onChange={(e) => setTagInput(e.target.value)}
                onKeyDown={addTag}
                placeholder="برچسب جدید + Enter"
              />
              <div className="flex flex-wrap gap-1.5">
                {tags.map((t) => (
                  <Badge key={t} variant="secondary" className="cursor-pointer">
                    {t}
                    <button
                      type="button"
                      onClick={() => setTags((ts) => ts.filter((x) => x !== t))}
                      className="ms-1"
                    >
                      <X className="size-3" />
                    </button>
                  </Badge>
                ))}
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="flex flex-row items-center justify-between gap-2">
              <CardTitle>اتوماسیون سناریو</CardTitle>
              <Badge variant={hasAnyAutomation ? 'success' : 'muted'}>
                {hasAnyAutomation ? 'قابل اجرای خودکار' : 'Manual'}
              </Badge>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="flex flex-wrap gap-1.5">
                <Badge variant={hasRecordedAutomation ? automationStatusVariant(currentAutomationStatus) : 'muted'} size="sm">
                  Recorded · {hasRecordedAutomation ? automationStatusLabel(currentAutomationStatus) : 'Not configured'}
                </Badge>
                {currentAutomationVersion !== null && (
                  <Badge variant="outline" size="sm">v{currentAutomationVersion}</Badge>
                )}
                {recordingSession && <Badge variant="warning" size="sm">Recording</Badge>}
              </div>

              <div className="space-y-3 rounded-lg border border-border bg-secondary/20 p-3">
                <div className="flex items-center justify-between gap-2">
                  <Label className="mb-0">Azma Scenario Studio</Label>
                  <Badge variant={hasRecordedAutomation ? automationStatusVariant(currentAutomationStatus) : 'muted'} size="sm">
                    {hasRecordedAutomation ? automationStatusLabel(currentAutomationStatus) : 'Empty'}
                  </Badge>
                </div>
                <div className="text-xs leading-5 text-muted-foreground">
                  گام‌های انسانی و اتوماسیون اجرایی را همان‌جا روی صفحه هدف بسازید، مرتب کنید و با یک Stop به‌صورت Draft ذخیره کنید.
                </div>

                <div className="space-y-1.5">
                  <Label>Target URL</Label>
                  <Input
                    type="url"
                    value={recordingTargetUrl}
                    onChange={(e) => setRecordingTargetUrl(e.target.value)}
                    placeholder="https://example.com"
                    dir="ltr"
                    disabled={!canUseAutomationPanel || !!recordingSession}
                  />
                {recordingTargetUrl && !targetUrlValid && (
                  <div className="text-xs text-destructive">Use a full http(s) URL.</div>
                )}
              </div>

              <div className="space-y-1.5">
                <Label>قبل از این اجرا</Label>
                <Select
                  value={prerequisiteTestCaseId}
                  disabled={!canUseAutomationPanel || !!recordingSession || setPrerequisiteMut.isPending}
                  onChange={(e) => {
                    const nextId = e.target.value;
                    setPrerequisiteTestCaseId(nextId);
                    setPrerequisiteMut.mutate(nextId);
                  }}
                >
                  <option value="">بدون پیش‌نیاز</option>
                  {(recordedScenarios?.items ?? [])
                    .filter((scenario) => scenario.id !== id && scenario.automationStatus !== 'None')
                    .map((scenario) => (
                      <option key={scenario.id} value={scenario.id}>
                        {scenario.code} · {scenario.title}
                      </option>
                    ))}
                </Select>
                <div className="text-xs text-muted-foreground">
                  این سناریو اول، در همان مرورگر، اجرا می‌شود. برای صفحه‌هایی که ورود می‌خواهند، سناریوی لاگین را انتخاب کنید.
                </div>
              </div>

                <div className="flex flex-wrap items-center gap-1.5">
                  <Badge
                    variant={recorderStatusVariant(recorderStatus, recorderExtensionReady)}
                    size="sm"
                    dot
                  >
                    {recorderStatusLabel(recorderStatus, recorderExtensionReady, recordedEventCount, recordedStepCount)}
                  </Badge>
                  {recordingSession && (
                    <Badge variant="outline" size="sm">
                      session active
                    </Badge>
                  )}
                </div>

                {!recorderExtensionReady && (
                  <div className="rounded-md border border-destructive/25 bg-destructive/10 p-2 text-xs text-muted-foreground">
                    <div className="font-medium text-destructive">Azma Recorder Extension</div>
                    <div className="mt-1 font-mono break-all" dir="ltr">src/Web/azma-recorder-extension/dist/chrome</div>
                    <div className="font-mono break-all" dir="ltr">src/Web/azma-recorder-extension/dist/firefox</div>
                  </div>
                )}

                <div className="grid grid-cols-2 gap-2">
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    disabled={!canStartRecording}
                    loading={startRecordingMut.isPending}
                    onClick={() => startRecordingMut.mutate(recordingTargetUrl.trim())}
                  >
                    <Radio className="size-4" />
                    Start / Repair
                  </Button>
                  <Button
                    type="button"
                    variant="secondary"
                    size="sm"
                    disabled={!recordingSession}
                    loading={finishRecordingMut.isPending}
                    onClick={() => finishRecordingMut.mutate({})}
                  >
                    <Save className="size-4" />
                    Stop &amp; Save Draft
                  </Button>
                </div>

                {recordingSession && (
                  <div className="rounded-md border border-warning/30 bg-warning/10 px-2.5 py-2 text-xs text-muted-foreground" dir="ltr">
                    <div className="font-mono break-all">sessionId: {recordingSession.sessionId}</div>
                    <div className="mt-1">expires: {new Date(recordingSession.expiresAtUtc).toLocaleString()}</div>
                  </div>
                )}

                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  className="w-full justify-between"
                  onClick={() => setShowDeveloperJson((x) => !x)}
                >
                  <span className="inline-flex items-center gap-2">
                    <FileJson className="size-4" />
                    Advanced / Developer JSON
                  </span>
                  <ChevronDown className={cn('size-4 transition-transform', showDeveloperJson && 'rotate-180')} />
                </Button>

                {showDeveloperJson && (
                  <div>
                    <Label>Recording artifact JSON</Label>
                    <Textarea
                      value={recordingJson}
                      onChange={(e) => setRecordingJson(e.target.value)}
                      dir="ltr"
                      spellCheck={false}
                      disabled={!canUseAutomationPanel}
                      className="min-h-64 font-mono text-xs leading-5"
                    />
                  </div>
                )}

                <div className="grid grid-cols-1 gap-2">
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    disabled={!canValidateRecordedAutomation || automationActionPending}
                    loading={validateAutomationMut.isPending || (validateAutomationMut.isPending && finishRecordingMut.isPending)}
                    onClick={() => validateAutomationMut.mutate()}
                  >
                    <ShieldCheck className="size-4" />
                    Validate
                  </Button>
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    disabled={!canDryRunRecordedAutomation || automationActionPending}
                    loading={dryRunAutomationMut.isPending || (dryRunAutomationMut.isPending && finishRecordingMut.isPending)}
                    onClick={() => dryRunAutomationMut.mutate()}
                  >
                    <PlayCircle className="size-4" />
                    Dry Run
                  </Button>
                  <Button
                    type="button"
                    variant="success"
                    size="sm"
                    disabled={!canPublishRecordedAutomation || automationActionPending}
                    loading={publishAutomationMut.isPending}
                    onClick={() => publishAutomationMut.mutate()}
                  >
                    <Rocket className="size-4" />
                    Publish
                  </Button>
                </div>
              </div>

              {currentValidationErrors.length > 0 && (
                <div className="rounded-md border border-destructive/25 bg-destructive/10 p-2 text-xs text-destructive space-y-1">
                  {currentValidationErrors.map((error) => (
                    <div key={error}>{error}</div>
                  ))}
                </div>
              )}

              {dryRunResult && (
                <div className="rounded-md border bg-secondary/40 p-2 text-xs space-y-1">
                  <div className="flex items-center justify-between">
                    <span>Dry run</span>
                    <Badge variant={dryRunResult.success ? 'success' : 'destructive'} size="sm">
                      {dryRunResult.success ? 'Passed' : dryRunResult.failureKind}
                    </Badge>
                  </div>
                  <div className="text-muted-foreground">
                    {dryRunResult.steps.length} steps · {dryRunResult.network.length} requests · {dryRunResult.durationMs} ms
                  </div>
                  {dryRunResult.errorMessage && (
                    <div className="text-destructive">{dryRunResult.errorMessage}</div>
                  )}
                  {dryRunFailureEvidence?.pageUrl && (
                    <div className="font-mono break-all text-muted-foreground" dir="ltr">
                      {dryRunFailureEvidence.pageUrl}
                    </div>
                  )}
                  {dryRunFailureEvidence?.screenshotBase64 && (
                    <img
                      src={`data:image/jpeg;base64,${dryRunFailureEvidence.screenshotBase64}`}
                      alt="نمای صفحه در لحظه شکست اجرای خودکار"
                      className="mt-2 max-h-72 w-full rounded-md border object-contain bg-background"
                    />
                  )}
                </div>
              )}
            </CardContent>
          </Card>

          <Button type="submit" loading={saveMut.isPending} className="w-full" size="lg">
            <Save className="size-4" /> {isNew ? 'ساخت سناریو' : 'ذخیره تغییرات'}
          </Button>
        </div>
      </form>
    </motion.div>
  );
}
