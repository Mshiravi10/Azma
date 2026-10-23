declare var chrome: any;

type BackgroundScenarioDefinition = Record<string, unknown> & { steps?: unknown[] };

type BackgroundRecorderSession = {
  sessionId: string;
  sessionToken: string;
  expiresAtUtc?: string;
  testCaseId?: string;
  targetUrl: string;
  gatewayBaseUrl: string;
  azmaOrigin: string;
  azmaTabId?: number;
  targetTabId?: number;
  active: boolean;
  paused: boolean;
  eventCount: number;
  queuedEvents: Array<Record<string, unknown>>;
  scenario?: BackgroundScenarioDefinition | null;
  initialDefinition?: BackgroundScenarioDefinition | null;
  initialManualSteps?: Array<{ action?: string; expectedResult?: string | null }>;
  completedAutomation?: Record<string, unknown> | null;
};

(() => {
  const extensionSource = 'AZMA_RECORDER_EXTENSION';
  const sessionKey = 'azmaRecorderSessionV2';
  const flushBatchSize = 25;
  const maxQueuedEvents = 2000;
  let operationQueue: Promise<void> = Promise.resolve();

  function createId() {
    return typeof globalThis.crypto?.randomUUID === 'function'
      ? globalThis.crypto.randomUUID()
      : `azma-${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`;
  }

  function getSession(): Promise<BackgroundRecorderSession | null> {
    return new Promise((resolve) => {
      chrome.storage.local.get(sessionKey, (items: Record<string, BackgroundRecorderSession | undefined>) => {
        resolve(items[sessionKey] ?? null);
      });
    });
  }

  function setSession(session: BackgroundRecorderSession | null): Promise<void> {
    return new Promise((resolve) => {
      if (!session) {
        chrome.storage.local.remove(sessionKey, resolve);
        return;
      }
      chrome.storage.local.set({ [sessionKey]: session }, resolve);
    });
  }

  function sendToTab(tabId: number | undefined, type: string, payload?: Record<string, unknown>) {
    if (typeof tabId !== 'number') return;
    chrome.tabs.sendMessage(tabId, { source: extensionSource, type, payload }, () => {
      void chrome.runtime.lastError;
    });
  }

  function requestFromTab(tabId: number | undefined, type: string, payload?: Record<string, unknown>): Promise<any> {
    return new Promise((resolve) => {
      if (typeof tabId !== 'number') return resolve(null);
      chrome.tabs.sendMessage(tabId, { source: extensionSource, type, payload }, (response: any) => {
        if (chrome.runtime.lastError) return resolve(null);
        resolve(response ?? null);
      });
    });
  }

  function notifyAzma(session: BackgroundRecorderSession, type: string, payload?: Record<string, unknown>) {
    sendToTab(session.azmaTabId, type, payload);
  }

  function apiBase(session: BackgroundRecorderSession) {
    return session.gatewayBaseUrl.replace(/\/+$/, '');
  }

  function senderOrigin(sender: any) {
    try {
      return new URL(sender?.tab?.url ?? sender?.url).origin;
    } catch {
      return null;
    }
  }

  function publicState(session: BackgroundRecorderSession) {
    return {
      sessionId: session.sessionId,
      sessionToken: session.sessionToken,
      expiresAtUtc: session.expiresAtUtc,
      testCaseId: session.testCaseId,
      targetUrl: session.targetUrl,
      active: session.active,
      paused: session.paused,
      eventCount: session.eventCount,
      stepCount: Array.isArray(session.scenario?.steps) ? session.scenario!.steps!.length : 0,
      automation: session.completedAutomation ?? null,
    };
  }

  function normalizeEvent(event: unknown): Record<string, unknown> {
    const value: Record<string, unknown> = event && typeof event === 'object'
      ? { ...(event as Record<string, unknown>) }
      : { type: 'unknown', value: event };
    if (typeof value.eventId !== 'string' || !value.eventId) value.eventId = createId();
    if (typeof value.recordedAtUtc !== 'string') value.recordedAtUtc = new Date().toISOString();
    return value;
  }

  async function appendEvents(session: BackgroundRecorderSession, events: Array<Record<string, unknown>>) {
    if (events.length === 0) return;
    const response = await fetch(`${apiBase(session)}/api/testcases/automation/recordings/${session.sessionId}/events`, {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ sessionToken: session.sessionToken, events }),
    });
    if (!response.ok) {
      const body = await response.text().catch(() => '');
      throw new Error(body || `Recorder event append failed with ${response.status}.`);
    }
  }

  async function flushQueued(session: BackgroundRecorderSession) {
    let nextSession = session;
    while (nextSession.queuedEvents.length > 0) {
      const batch = nextSession.queuedEvents.slice(0, flushBatchSize);
      try {
        await appendEvents(nextSession, batch);
      } catch (error) {
        await setSession(nextSession);
        return { session: nextSession, error: error instanceof Error ? error : new Error(String(error)) };
      }
      nextSession = { ...nextSession, queuedEvents: nextSession.queuedEvents.slice(batch.length) };
      await setSession(nextSession);
    }
    return { session: nextSession, error: null };
  }

  function createTab(url: string): Promise<any> {
    return new Promise((resolve, reject) => {
      chrome.tabs.create({ url, active: true }, (tab: any) => {
        const error = chrome.runtime.lastError?.message;
        if (error) return reject(new Error(error));
        if (typeof tab?.id !== 'number') return reject(new Error('Recorder could not open the target tab.'));
        resolve(tab);
      });
    });
  }

  async function handleStart(payload: Partial<BackgroundRecorderSession> & { requestId?: string }, sender: any) {
    if (!payload.sessionId || !payload.sessionToken || !payload.targetUrl || !payload.gatewayBaseUrl)
      throw new Error('Recorder session payload is incomplete.');

    const azmaOrigin = senderOrigin(sender);
    let gatewayOrigin: string;
    let target: URL;
    try {
      gatewayOrigin = new URL(payload.gatewayBaseUrl).origin;
      target = new URL(payload.targetUrl);
    } catch {
      throw new Error('Recorder URLs are invalid.');
    }
    if (!azmaOrigin || gatewayOrigin !== azmaOrigin)
      throw new Error('Recorder gateway must use the same origin as the Azma page.');
    if (target.protocol !== 'http:' && target.protocol !== 'https:')
      throw new Error('Recorder target must use http or https.');

    const current = await getSession();
    if (current?.active)
      throw new Error('Another Azma recording session is already active. Stop it before starting a new one.');

    const session: BackgroundRecorderSession = {
      sessionId: payload.sessionId,
      sessionToken: payload.sessionToken,
      expiresAtUtc: payload.expiresAtUtc,
      testCaseId: payload.testCaseId,
      targetUrl: target.href,
      gatewayBaseUrl: gatewayOrigin,
      azmaOrigin,
      azmaTabId: sender.tab?.id,
      active: true,
      paused: false,
      eventCount: 0,
      queuedEvents: [],
      scenario: payload.scenario ?? null,
      initialDefinition: payload.initialDefinition ?? null,
      initialManualSteps: payload.initialManualSteps ?? [],
      completedAutomation: null,
    };
    await setSession(session);

    const targetTab = await createTab(session.targetUrl);
    const nextSession = { ...session, targetTabId: targetTab.id };
    await setSession(nextSession);
    activateTarget(nextSession);
    notifyAzma(nextSession, 'AZMA_RECORDER_STARTED', {
      requestId: payload.requestId,
      targetTabId: nextSession.targetTabId,
      state: publicState(nextSession),
    });
  }

  function activateTarget(session: BackgroundRecorderSession) {
    sendToTab(session.targetTabId, 'AZMA_TARGET_ACTIVATE', {
      sessionId: session.sessionId,
      targetUrl: session.targetUrl,
      eventCount: session.eventCount,
      paused: session.paused,
      scenario: session.scenario,
      initialDefinition: session.initialDefinition,
      initialManualSteps: session.initialManualSteps,
    });
  }

  async function handleTargetReady(sender: any) {
    const session = await getSession();
    if (!session?.active || sender.tab?.id !== session.targetTabId) return;
    activateTarget(session);
  }

  async function handleRecordEvents(events: unknown[], sender: any) {
    const session = await getSession();
    if (!session?.active || session.paused || sender.tab?.id !== session.targetTabId) return;
    const normalized = events.map(normalizeEvent);
    let nextSession = { ...session, eventCount: session.eventCount + normalized.length };
    try {
      const flush = await flushQueued(nextSession);
      nextSession = flush.session;
      if (flush.error) throw flush.error;
      await appendEvents(nextSession, normalized);
    } catch {
      const queuedEvents = [...nextSession.queuedEvents, ...normalized];
      if (queuedEvents.length > maxQueuedEvents)
        throw new Error('Recorder offline queue is full. Check the Azma connection before continuing.');
      nextSession = { ...nextSession, queuedEvents };
    }
    await setSession(nextSession);
    notifyAzma(nextSession, 'AZMA_RECORDER_EVENT_COUNT', {
      eventCount: nextSession.eventCount,
      stepCount: Array.isArray(nextSession.scenario?.steps) ? nextSession.scenario!.steps!.length : 0,
    });
  }

  async function handleScenarioSync(scenario: BackgroundScenarioDefinition, sender: any) {
    const session = await getSession();
    if (!session?.active || sender.tab?.id !== session.targetTabId) return;
    const nextSession = { ...session, scenario };
    await setSession(nextSession);
    notifyAzma(nextSession, 'AZMA_RECORDER_SCENARIO_UPDATED', {
      eventCount: nextSession.eventCount,
      stepCount: Array.isArray(scenario.steps) ? scenario.steps.length : 0,
    });
  }

  async function handlePause(paused: boolean) {
    const session = await getSession();
    if (!session?.active) return;
    const nextSession = { ...session, paused };
    await setSession(nextSession);
    sendToTab(nextSession.targetTabId, 'AZMA_TARGET_PAUSE', { paused });
    notifyAzma(nextSession, 'AZMA_RECORDER_STATE', { state: publicState(nextSession) });
  }

  async function finishOnServer(session: BackgroundRecorderSession) {
    const response = await fetch(`${apiBase(session)}/api/testcases/automation/recordings/${session.sessionId}/finish`, {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ sessionToken: session.sessionToken, definition: session.scenario ?? null }),
    });
    if (!response.ok) {
      const body = await response.text().catch(() => '');
      throw new Error(body || `Recorder finish failed with ${response.status}.`);
    }
    return await response.json() as Record<string, unknown>;
  }

  async function handleStop(requestId?: string, definitionOverride?: BackgroundScenarioDefinition | null) {
    const session = await getSession();
    if (!session) throw new Error('Recorder session was not found.');
    if (!session.active && session.completedAutomation) {
      notifyAzma(session, 'AZMA_RECORDER_STOPPED', {
        eventCount: session.eventCount,
        stepCount: Array.isArray(session.scenario?.steps) ? session.scenario!.steps!.length : 0,
        requestId,
        automation: session.completedAutomation,
      });
      return;
    }
    if (!session.active) throw new Error('Recorder session is not active.');

    const prepared = await requestFromTab(session.targetTabId, 'AZMA_TARGET_PREPARE_STOP');
    sendToTab(session.targetTabId, 'AZMA_TARGET_PAUSE', { paused: true });
    let nextSession: BackgroundRecorderSession = {
      ...session,
      paused: true,
      scenario: prepared?.scenario ?? session.scenario,
    };
    if (definitionOverride) nextSession.scenario = definitionOverride;
    await setSession(nextSession);
    if (nextSession.scenario) {
      nextSession = {
        ...nextSession,
        queuedEvents: [...nextSession.queuedEvents, normalizeEvent({
          type: 'scenarioSnapshot', definition: nextSession.scenario,
        })],
      };
    }
    const flush = await flushQueued(nextSession);
    nextSession = flush.session;
    if (flush.error) throw flush.error;

    const automation = await finishOnServer(nextSession);
    nextSession = { ...nextSession, active: false, paused: false, completedAutomation: automation };
    await setSession(nextSession);
    sendToTab(nextSession.targetTabId, 'AZMA_TARGET_STOP');
    notifyAzma(nextSession, 'AZMA_RECORDER_STOPPED', {
      eventCount: nextSession.eventCount,
      stepCount: Array.isArray(nextSession.scenario?.steps) ? nextSession.scenario!.steps!.length : 0,
      requestId,
      automation,
    });
    if (typeof nextSession.azmaTabId === 'number')
      chrome.tabs.update(nextSession.azmaTabId, { active: true }, () => void chrome.runtime.lastError);
  }

  async function handleGetState(sender: any) {
    const session = await getSession();
    if (!session || senderOrigin(sender) !== session.azmaOrigin) return null;
    return publicState(session);
  }

  function enqueue(operation: () => Promise<void>) {
    const result = operationQueue.then(operation, operation);
    operationQueue = result.catch(() => undefined);
    return result;
  }

  chrome.runtime.onMessage.addListener((message: any, sender: any, sendResponse: (value: unknown) => void) => {
    if (message?.source !== extensionSource) return false;
    if (message?.type === 'get-recorder-state') {
      handleGetState(sender).then((state) => sendResponse({ ok: true, state }));
      return true;
    }

    enqueue(async () => {
      switch (message?.type) {
        case 'start-recording': await handleStart(message.payload ?? {}, sender); break;
        case 'target-ready': await handleTargetReady(sender); break;
        case 'record-events': await handleRecordEvents(message.events ?? [], sender); break;
        case 'sync-scenario': await handleScenarioSync(message.scenario ?? {}, sender); break;
        case 'pause-recording': await handlePause(Boolean(message.paused)); break;
        case 'stop-recording': await handleStop(message.payload?.requestId, message.payload?.definition ?? null); break;
      }
    }).then(() => sendResponse({ ok: true })).catch((error) => {
      getSession().then((session) => {
        if (session) notifyAzma(session, 'AZMA_RECORDER_ERROR', {
          message: error?.message ?? String(error), requestId: message.payload?.requestId,
        });
      });
      sendResponse({ ok: false, error: error?.message ?? String(error) });
    });
    return true;
  });
})();
