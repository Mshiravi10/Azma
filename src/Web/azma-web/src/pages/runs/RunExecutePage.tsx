import { useParams, useNavigate, Link } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useEffect, useMemo, useState } from 'react';
import { RunsApi, TestCasesApi, MembersApi } from '@/lib/queries/endpoints';
import type { ResultStatus, RunCase, Membership } from '@/lib/types';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Select } from '@/components/ui/native-select';
import { ResultBadge, ResultDot, ResultLabels } from '@/components/common/StatusBadges';
import { Skeleton } from '@/components/common/Skeletons';
import {
  ChevronRight,
  ChevronLeft,
  History,
  Save,
  X,
  Keyboard,
  ArrowLeft,
  Search,
  Filter,
  CheckSquare,
  Square,
  UserCircle,
  PlayCircle,
} from 'lucide-react';
import { fmtRelative } from '@/lib/date';
import { cn, formatNumber, formatPercent } from '@/lib/utils';
import { toast } from 'sonner';
import { extractError } from '@/lib/api';
import { motion, AnimatePresence } from 'framer-motion';
import { AttachmentsPanel } from '@/components/runs/AttachmentsPanel';

const STATUSES: ResultStatus[] = ['Passed', 'Failed', 'Blocked', 'Retest', 'Skipped'];
const SHORTCUTS: Record<string, ResultStatus> = {
  p: 'Passed',
  f: 'Failed',
  b: 'Blocked',
  r: 'Retest',
  s: 'Skipped',
};
const VARIANT: Record<
  ResultStatus,
  'success' | 'destructive' | 'warning' | 'info' | 'muted' | 'secondary'
> = {
  Untested: 'muted',
  Passed: 'success',
  Failed: 'destructive',
  Blocked: 'warning',
  Retest: 'info',
  Skipped: 'secondary',
};

export default function RunExecutePage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const qc = useQueryClient();
  const [activeIdx, setActiveIdx] = useState(0);
  const [comment, setComment] = useState('');
  const [showShortcuts, setShowShortcuts] = useState(true);
  const [sidebarSearch, setSidebarSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState<ResultStatus | ''>('');
  const [bulkSelected, setBulkSelected] = useState<Set<string>>(new Set());

  const { data: run } = useQuery({
    queryKey: ['run', id],
    queryFn: () => RunsApi.get(id!),
    enabled: !!id,
  });
  const { data: cases, isLoading } = useQuery({
    queryKey: ['run-cases', id],
    queryFn: () => RunsApi.cases(id!),
    enabled: !!id,
  });
  const active: RunCase | undefined = cases?.[activeIdx];
  const { data: tcDetail } = useQuery({
    queryKey: ['testcase', active?.testCaseId],
    queryFn: () => TestCasesApi.get(active!.testCaseId),
    enabled: !!active?.testCaseId,
  });
  const { data: history } = useQuery({
    queryKey: ['result-history', active?.id],
    queryFn: () => RunsApi.resultHistory(active!.id),
    enabled: !!active?.id,
  });
  const { data: members } = useQuery({
    queryKey: ['members'],
    queryFn: () => MembersApi.list(),
  });

  const recordMut = useMutation({
    mutationFn: (status: ResultStatus) =>
      RunsApi.recordResult({ runCaseId: active!.id, status, comment: comment || undefined }),
    onSuccess: (_d, status) => {
      toast.success(`ثبت شد: ${ResultLabels[status]}`);
      setComment('');
      qc.invalidateQueries({ queryKey: ['run-cases', id] });
      qc.invalidateQueries({ queryKey: ['result-history', active!.id] });
      if (cases && activeIdx < cases.length - 1) {
        setActiveIdx((i) => i + 1);
      }
    },
    onError: (err) => toast.error(extractError(err)),
  });

  const autoRunMut = useMutation({
    mutationFn: () =>
      RunsApi.runAutomatic(active!.id, {}),
    onMutate: () =>
      toast.loading('در حال اجرای اتوماسیون...', {
        description: automaticEngineLabel,
      }),
    onSuccess: (data, _variables, toastId) => {
      if (data.result.status === 'Passed') {
        toast.success('اجرای اتوماتیک موفق بود و نتیجه Passed ثبت شد', {
          id: toastId,
          description: data.scenarioName,
        });
      } else {
        toast.error('اجرای اتوماتیک Fail شد و نتیجه Failed ثبت شد', {
          id: toastId,
          description: data.errorMessage ?? data.scenarioName,
        });
      }
      qc.invalidateQueries({ queryKey: ['run-cases', id] });
      qc.invalidateQueries({ queryKey: ['result-history', active!.id] });
      if (cases && activeIdx < cases.length - 1) {
        setActiveIdx((i) => i + 1);
      }
    },
    onError: (err, _variables, toastId) => toast.error(extractError(err), { id: toastId }),
  });

  async function bulkRecord(status: ResultStatus) {
    const ids = Array.from(bulkSelected);
    try {
      const res = await RunsApi.bulkRecordResult({ runCaseIds: ids, status });
      toast.success(`${res.succeeded} سناریو به ${ResultLabels[status]} تغییر کرد`);
    } catch {
      toast.error('خطا در ثبت دسته‌جمعی');
    }
    setBulkSelected(new Set());
    qc.invalidateQueries({ queryKey: ['run-cases', id] });
  }

  const assignMut = useMutation({
    mutationFn: ({ runCaseId, userId }: { runCaseId: string; userId: string | null }) =>
      RunsApi.setAssignee(runCaseId, userId),
    onSuccess: () => {
      toast.success('تستر تغییر کرد');
      qc.invalidateQueries({ queryKey: ['run-cases', id] });
    },
    onError: (err: any) => toast.error(extractError(err)),
  });

  useEffect(() => {
    function handler(e: KeyboardEvent) {
      const tag = (e.target as HTMLElement)?.tagName?.toLowerCase();
      if (tag === 'textarea' || tag === 'input') return;
      const key = e.key.toLowerCase();
      if (SHORTCUTS[key]) {
        e.preventDefault();
        if (active) recordMut.mutate(SHORTCUTS[key]);
      } else if (e.key === 'ArrowDown' || e.key === 'j') {
        e.preventDefault();
        setActiveIdx((i) => Math.min((cases?.length ?? 1) - 1, i + 1));
      } else if (e.key === 'ArrowUp' || e.key === 'k') {
        e.preventDefault();
        setActiveIdx((i) => Math.max(0, i - 1));
      }
    }
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [active, cases, recordMut]);

  const counts = useMemo(() => {
    const c: Record<ResultStatus, number> = {
      Untested: 0,
      Passed: 0,
      Failed: 0,
      Blocked: 0,
      Retest: 0,
      Skipped: 0,
    };
    cases?.forEach((rc) => (c[rc.currentStatus] = (c[rc.currentStatus] ?? 0) + 1));
    return c;
  }, [cases]);

  const filteredCases = useMemo(() => {
    if (!cases) return [];
    let result = cases;
    if (statusFilter) {
      result = result.filter((c) => c.currentStatus === statusFilter);
    }
    if (sidebarSearch.trim()) {
      const q = sidebarSearch.toLowerCase();
      result = result.filter(
        (c) => c.testCaseTitle.toLowerCase().includes(q) || c.testCaseCode.toLowerCase().includes(q),
      );
    }
    return result;
  }, [cases, statusFilter, sidebarSearch]);

  const total = cases?.length ?? 0;
  const executed = total - counts.Untested;
  const progress = total > 0 ? (executed / total) * 100 : 0;
  const passRate = executed > 0 ? (counts.Passed / executed) * 100 : 0;
  const automaticEngine = active?.automationEngine ?? tcDetail?.automationEngine ?? 'None';
  const canRunAutomatic = automaticEngine === 'ScenarioRecording' && (active?.hasAutomation ?? tcDetail?.hasAutomation ?? false);
  const automaticEngineLabel = automaticEngine === 'ScenarioRecording'
    ? 'Recorded'
    : 'Manual';

  if (isLoading || !run || !cases)
    return (
      <div className="flex h-screen items-center justify-center">
        <div className="space-y-4 w-full max-w-2xl px-6">
          <Skeleton className="h-9 w-2/3" />
          <Skeleton className="h-96 w-full" />
        </div>
      </div>
    );

  return (
    <div className="flex flex-col h-screen bg-background overflow-hidden">
      {/* Thin sticky progress strip */}
      <div className="h-0.5 w-full bg-muted shrink-0 relative overflow-hidden">
        <motion.div
          animate={{ width: `${progress}%` }}
          transition={{ duration: 0.4 }}
          className="absolute inset-y-0 right-0 bg-gradient-to-l from-primary to-success"
        />
      </div>

      {/* Compact header */}
      <header className="h-12 border-b border-border glass shrink-0 flex items-center px-4 gap-3">
        <Button variant="ghost" size="icon-sm" asChild title="بازگشت به اجرا">
          <Link to={`/runs/${id}`}>
            <ArrowLeft className="size-4" />
          </Link>
        </Button>
        <div className="flex items-center gap-2 min-w-0">
          <span className="font-mono text-xs text-muted-foreground">{run.code}</span>
          <span className="text-muted-foreground/40">/</span>
          <span className="text-sm font-medium truncate">{run.name}</span>
        </div>
        <div className="flex-1" />
        {/* Progress stats */}
        <div className="hidden sm:flex items-center gap-3 text-xs text-muted-foreground">
          <div className="flex items-center gap-0.5 h-2 w-32 rounded-full overflow-hidden bg-muted">
            {counts.Passed > 0 && <div className="h-full bg-success" style={{ width: `${(counts.Passed / total) * 100}%` }} />}
            {counts.Failed > 0 && <div className="h-full bg-destructive" style={{ width: `${(counts.Failed / total) * 100}%` }} />}
            {counts.Blocked > 0 && <div className="h-full bg-warning" style={{ width: `${(counts.Blocked / total) * 100}%` }} />}
            {counts.Retest > 0 && <div className="h-full bg-info" style={{ width: `${(counts.Retest / total) * 100}%` }} />}
          </div>
          <span className="num-fa">
            {formatNumber(executed)}/{formatNumber(total)}
          </span>
          <Badge variant="info" size="sm">
            {formatPercent(passRate)}
          </Badge>
        </div>
        <Button
          variant="outline"
          size="sm"
          onClick={() => navigate(`/runs/${id}`)}
          className="shrink-0"
        >
          پایان اجرا
        </Button>
      </header>

      {/* Main content */}
      <div className="flex flex-1 overflow-hidden">
        {/* Cases sidebar */}
        <aside className="w-72 shrink-0 border-l border-border flex flex-col overflow-hidden">
          {/* Sidebar header with filters */}
          <div className="p-3 border-b border-border space-y-2">
            <div className="flex items-center justify-between">
              <div className="text-xs text-muted-foreground">
                سناریو{' '}
                <span className="num-fa font-medium text-foreground">
                  {formatNumber(activeIdx + 1)}
                </span>{' '}
                از{' '}
                <span className="num-fa font-medium text-foreground">{formatNumber(total)}</span>
              </div>
              {bulkSelected.size > 0 && (
                <Badge variant="default" size="sm">{bulkSelected.size} انتخاب</Badge>
              )}
            </div>
            <div className="relative">
              <Search className="absolute right-2 top-1/2 -translate-y-1/2 size-3.5 text-muted-foreground pointer-events-none" />
              <Input
                value={sidebarSearch}
                onChange={(e) => setSidebarSearch(e.target.value)}
                placeholder="جستجو…"
                className="h-7 text-xs ps-7 pe-2"
              />
            </div>
            <Select
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value as ResultStatus | '')}
              className="h-7 text-xs"
            >
              <option value="">همه وضعیت‌ها ({total})</option>
              <option value="Untested">تست نشده ({counts.Untested})</option>
              <option value="Passed">موفق ({counts.Passed})</option>
              <option value="Failed">ناموفق ({counts.Failed})</option>
              <option value="Blocked">مسدود ({counts.Blocked})</option>
              <option value="Retest">بازآزمون ({counts.Retest})</option>
              <option value="Skipped">رد شده ({counts.Skipped})</option>
            </Select>
          </div>
          {/* Bulk action bar */}
          {bulkSelected.size > 0 && (
            <div className="p-2 border-b border-border flex items-center gap-1 flex-wrap">
              <Button
                variant="success" size="xs"
                onClick={() => bulkRecord('Passed')}
              >موفق</Button>
              <Button
                variant="destructive" size="xs"
                onClick={() => bulkRecord('Failed')}
              >ناموفق</Button>
              <Button
                variant="ghost" size="xs"
                onClick={() => setBulkSelected(new Set())}
              ><X className="size-3" /></Button>
            </div>
          )}
          <ul className="flex-1 overflow-y-auto">
            {filteredCases.map((rc) => {
              const originalIdx = cases.indexOf(rc);
              return (
                <li key={rc.id}>
                  <button
                    onClick={() => setActiveIdx(originalIdx)}
                    className={cn(
                      'w-full text-start px-3 py-2.5 hover:bg-accent/50 transition-base flex items-center gap-2',
                      originalIdx === activeIdx && 'bg-primary/6 border-s-2 border-primary shadow-[inset_0_0_0_1px_hsl(var(--primary)/0.1)]',
                    )}
                  >
                    <button
                      type="button"
                      onClick={(e) => {
                        e.stopPropagation();
                        setBulkSelected((prev) => {
                          const next = new Set(prev);
                          if (next.has(rc.id)) next.delete(rc.id);
                          else next.add(rc.id);
                          return next;
                        });
                      }}
                      className={cn(
                        'shrink-0 size-4 rounded border flex items-center justify-center transition-colors',
                        bulkSelected.has(rc.id)
                          ? 'bg-primary border-primary text-primary-foreground'
                          : 'border-border hover:border-primary/50',
                      )}
                    >
                      {bulkSelected.has(rc.id) && <CheckSquare className="size-2.5" />}
                    </button>
                    <ResultDot status={rc.currentStatus} />
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-1 mb-0.5">
                        <div className="font-mono text-[10px] text-muted-foreground leading-none">
                          {rc.testCaseCode}
                        </div>
                        {rc.automationEngine === 'ScenarioRecording' && (
                          <Badge variant="info" size="sm" className="text-[9px]">
                            Recorded
                          </Badge>
                        )}
                      </div>
                      <div className="text-xs truncate leading-snug">{rc.testCaseTitle}</div>
                    </div>
                  </button>
                </li>
              );
            })}
            {filteredCases.length === 0 && (
              <li className="p-4 text-center text-xs text-muted-foreground">
                نتیجه‌ای یافت نشد
              </li>
            )}
          </ul>
        </aside>

        {/* Active case content */}
        <main className="flex-1 overflow-y-auto px-6 py-5">
          <AnimatePresence mode="wait">
            {active && (
              <motion.div
                key={active.id}
                initial={{ opacity: 0, y: 8 }}
                animate={{ opacity: 1, y: 0 }}
                exit={{ opacity: 0, y: -8 }}
                transition={{ duration: 0.18 }}
                className="max-w-3xl space-y-4"
              >
                <Card>
                  <CardContent className="p-5 space-y-5">
                    <div className="flex items-start justify-between gap-4">
                      <div>
                        <div className="font-mono text-xs text-muted-foreground">
                          {active.testCaseCode}
                        </div>
                        <h2 className="font-bold text-lg mt-1 leading-snug">
                          {active.testCaseTitle}
                        </h2>
                      </div>
                      <div className="flex items-center gap-2 shrink-0">
                        <div className="flex items-center gap-1.5">
                          <UserCircle className="size-3.5 text-muted-foreground" />
                          <Select
                            value={active.assignedToUserId ?? ''}
                            onChange={(e) =>
                              assignMut.mutate({
                                runCaseId: active.id,
                                userId: e.target.value || null,
                              })
                            }
                            className="h-7 text-xs w-36"
                          >
                            <option value="">بدون تستر</option>
                            {members?.map((m) => (
                              <option key={m.userId} value={m.userId}>
                                {m.displayName || m.email}
                              </option>
                            ))}
                          </Select>
                        </div>
                        <ResultBadge status={active.currentStatus} />
                      </div>
                    </div>

                    {tcDetail?.preconditions && (
                      <div className="rounded-lg bg-info/5 border border-info/20 p-3">
                        <div className="text-xs text-info font-semibold mb-1">پیش‌نیازها</div>
                        <p className="text-sm whitespace-pre-wrap leading-6">
                          {tcDetail.preconditions}
                        </p>
                      </div>
                    )}

                    {tcDetail?.steps && tcDetail.steps.length > 0 && (
                      <div>
                        <div className="text-xs text-muted-foreground font-semibold mb-2 uppercase tracking-wider">
                          مراحل اجرا
                        </div>
                        <ol className="space-y-2">
                          {tcDetail.steps.map((s) => (
                            <li key={s.order} className="rounded-lg border border-border p-3 hover:bg-accent/20 transition-colors group">
                              <div className="flex items-start gap-3">
                                <span className="font-mono text-xs text-muted-foreground mt-0.5 shrink-0 num-fa">
                                  {formatNumber(s.order)}.
                                </span>
                                <div className="flex-1 min-w-0">
                                  <p className="text-sm leading-6 whitespace-pre-wrap">
                                    {s.action}
                                  </p>
                                  {s.expectedResult && (
                                    <p className="text-sm leading-6 whitespace-pre-wrap text-success/90 mt-1 ps-3 border-s-2 border-success/30">
                                      {s.expectedResult}
                                    </p>
                                  )}
                                </div>
                              </div>
                            </li>
                          ))}
                        </ol>
                      </div>
                    )}

                    <div className="rounded-lg border border-info/20 bg-info/5 p-3 space-y-3">
                      <div className="flex flex-wrap items-center gap-2">
                        <PlayCircle className="size-4 text-info" />
                        <div className="text-sm font-semibold">اجرای اتوماتیک</div>
                        <Badge variant={canRunAutomatic ? 'info' : 'muted'} size="sm">
                          {canRunAutomatic ? automaticEngineLabel : 'Manual'}
                        </Badge>
                      </div>

                      <div className="grid gap-2 sm:grid-cols-[minmax(0,1fr)_auto]">
                        <div className="min-w-0 rounded-lg border border-border bg-background/70 px-3 py-2">
                          {canRunAutomatic ? (
                            <div className="text-xs text-foreground">
                              {automaticEngineLabel} automation
                            </div>
                          ) : (
                            <div className="text-xs text-muted-foreground">
                              این سناریو اتوماسیون منتشرشده ندارد
                            </div>
                          )}
                        </div>
                        <Button
                          type="button"
                          variant="outline"
                          size="sm"
                          disabled={!canRunAutomatic}
                          loading={autoRunMut.isPending}
                          onClick={() => autoRunMut.mutate()}
                        >
                          <PlayCircle className="size-3.5" />
                          {autoRunMut.isPending ? 'در حال اجرا...' : 'اجرا و ثبت نتیجه'}
                        </Button>
                      </div>

                      {autoRunMut.isPending && (
                        <div
                          className="flex items-center gap-2 rounded-md border border-info/25 bg-background/70 px-3 py-2 text-xs text-muted-foreground"
                          aria-live="polite"
                        >
                          <span className="inline-block size-3 animate-spin rounded-full border-2 border-info border-t-transparent" />
                          <span>در حال اجرای اتوماسیون سناریو؛ نتیجه بعد از پایان ثبت می‌شود.</span>
                        </div>
                      )}
                    </div>

                    <div>
                      <Textarea
                        value={comment}
                        onChange={(e) => setComment(e.target.value)}
                        placeholder="یادداشت اختیاری برای این نتیجه…"
                        rows={2}
                      />
                    </div>

                    {/* Action buttons */}
                    <div className="flex flex-wrap items-center gap-2">
                      <Button
                        variant="success"
                        size="sm"
                        onClick={() => recordMut.mutate('Passed')}
                        loading={recordMut.isPending && recordMut.variables === 'Passed'}
                        className="flex-1 sm:flex-none"
                      >
                        <Save className="size-3.5" />
                        موفق
                        <kbd className="ms-1 px-1 py-0 bg-black/15 text-[10px] rounded">P</kbd>
                      </Button>
                      <Button
                        variant="destructive"
                        size="sm"
                        onClick={() => recordMut.mutate('Failed')}
                        loading={recordMut.isPending && recordMut.variables === 'Failed'}
                        className="flex-1 sm:flex-none"
                      >
                        <Save className="size-3.5" />
                        ناموفق
                        <kbd className="ms-1 px-1 py-0 bg-black/15 text-[10px] rounded">F</kbd>
                      </Button>
                      {(['Blocked', 'Retest', 'Skipped'] as ResultStatus[]).map((s) => (
                        <Button
                          key={s}
                          variant="outline"
                          size="sm"
                          onClick={() => recordMut.mutate(s)}
                          loading={recordMut.isPending && recordMut.variables === s}
                        >
                          {ResultLabels[s]}
                          <kbd className="ms-1 px-1 py-0 bg-muted text-[10px] rounded border border-border">
                            {Object.entries(SHORTCUTS)
                              .find(([, v]) => v === s)?.[0]
                              .toUpperCase()}
                          </kbd>
                        </Button>
                      ))}

                      <div className="ms-auto flex items-center gap-1">
                        <Button
                          variant="ghost"
                          size="icon-sm"
                          disabled={activeIdx === 0}
                          onClick={() => setActiveIdx((i) => i - 1)}
                          title="قبلی (↑)"
                        >
                          <ChevronRight className="size-4" />
                        </Button>
                        <Button
                          variant="ghost"
                          size="icon-sm"
                          disabled={activeIdx >= cases.length - 1}
                          onClick={() => setActiveIdx((i) => i + 1)}
                          title="بعدی (↓)"
                        >
                          <ChevronLeft className="size-4" />
                        </Button>
                      </div>
                    </div>
                  </CardContent>
                </Card>

                {history && history.length > 0 && (
                  <Card variant="flat">
                    <CardContent className="p-4">
                      <div className="flex items-center gap-2 mb-3">
                        <History className="size-3.5 text-info" />
                        <h3 className="font-semibold text-sm">تاریخچه</h3>
                        <Badge variant="muted" size="sm">
                          {formatNumber(history.length)}
                        </Badge>
                      </div>
                      <ul className="space-y-1.5">
                        {history.map((h) => (
                          <li
                            key={h.id}
                            className="rounded-md bg-background/60 p-2 text-xs"
                          >
                            <div className="flex flex-wrap items-center gap-2">
                              <Badge variant={VARIANT[h.status]} size="sm">
                                {ResultLabels[h.status]}
                              </Badge>
                              <span className="text-muted-foreground">{fmtRelative(h.executedAtUtc)}</span>
                            </div>
                            {h.comment && (
                              <p className="mt-2 whitespace-pre-wrap break-all leading-5 text-muted-foreground" dir="auto">
                                {h.comment}
                              </p>
                            )}
                          </li>
                        ))}
                      </ul>
                    </CardContent>
                  </Card>
                )}

                <AttachmentsPanel runId={id!} runCaseId={active.id} />
              </motion.div>
            )}
          </AnimatePresence>
        </main>
      </div>

      {/* Floating keyboard shortcuts badge */}
      <AnimatePresence>
        {showShortcuts && (
          <motion.div
            initial={{ opacity: 0, y: 8 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: 8 }}
            className="fixed bottom-5 left-5 z-50"
          >
            <div className="flex items-center gap-2 rounded-xl bg-card border border-border shadow-soft-lg px-3 py-2">
              <Keyboard className="size-3.5 text-muted-foreground" />
              <div className="flex items-center gap-1 text-[11px] text-muted-foreground">
                <kbd className="px-1 bg-muted rounded border border-border">P</kbd>
                <kbd className="px-1 bg-muted rounded border border-border">F</kbd>
                <kbd className="px-1 bg-muted rounded border border-border">B</kbd>
                <kbd className="px-1 bg-muted rounded border border-border">↑↓</kbd>
              </div>
              <button
                onClick={() => setShowShortcuts(false)}
                className="text-muted-foreground/50 hover:text-muted-foreground transition-base"
              >
                <X className="size-3" />
              </button>
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}
