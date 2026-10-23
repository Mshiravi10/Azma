import { useParams, Link } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { RunsApi } from '@/lib/queries/endpoints';
import type { ResultStatus } from '@/lib/types';
import { PageHeader } from '@/components/common/PageHeader';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Textarea } from '@/components/ui/textarea';
import { ResultBadge, ResultDot, RunStatusBadge } from '@/components/common/StatusBadges';
import { Skeleton } from '@/components/common/Skeletons';
import { EmptyState } from '@/components/common/EmptyState';
import { StatsCard } from '@/components/common/StatsCard';
import {
  ChevronRight,
  PlayCircle,
  Lock,
  Unlock,
  MessageSquare,
  Clock,
  AlertTriangle,
  Send,
  CheckCircle2,
  XCircle,
  Shield,
  SkipForward,
  Activity,
  ListChecks,
  RotateCcw,
  Copy,
  Pencil,
  Plus,
  Trash2,
  ChevronDown,
} from 'lucide-react';
import { useState, type FormEvent, useMemo } from 'react';
import { fmtDateTime, fmtRelative } from '@/lib/date';
import { formatNumber, formatPercent, initials } from '@/lib/utils';
import { toast } from 'sonner';
import { extractError } from '@/lib/api';
import { motion } from 'framer-motion';
import NewRunDialog from './NewRunDialog';
import EditRunDialog from './EditRunDialog';
import AddCasesDialog from './AddCasesDialog';
import { RunCaseFailureDetails } from './RunCaseFailureDetails';

export default function RunDetailPage() {
  const { id } = useParams<{ id: string }>();
  const qc = useQueryClient();
  const [comment, setComment] = useState('');
  const [rerunMode, setRerunMode] = useState<null | 'all' | 'failed'>(null);
  const [showDuplicate, setShowDuplicate] = useState(false);
  const [showEdit, setShowEdit] = useState(false);
  const [showAddCases, setShowAddCases] = useState(false);
  const [expandedFailureId, setExpandedFailureId] = useState<string | null>(null);

  const { data: run, isLoading } = useQuery({
    queryKey: ['run', id],
    queryFn: () => RunsApi.get(id!),
    enabled: !!id,
  });
  const { data: cases } = useQuery({
    queryKey: ['run-cases', id],
    queryFn: () => RunsApi.cases(id!),
    enabled: !!id,
  });
  const { data: comments } = useQuery({
    queryKey: ['run-comments', id],
    queryFn: () => RunsApi.comments(id!),
    enabled: !!id,
  });
  const { data: automationExecutions } = useQuery({
    queryKey: ['run-automation-executions', id],
    queryFn: () => RunsApi.automationExecutions(id!, { page: 1, pageSize: 5 }),
    enabled: !!id,
    refetchInterval: 5000,
  });

  const closeMut = useMutation({
    mutationFn: () => RunsApi.close(id!),
    onSuccess: () => {
      toast.success('اجرا بسته شد');
      qc.invalidateQueries({ queryKey: ['run', id] });
    },
    onError: (err) => toast.error(extractError(err)),
  });
  const reopenMut = useMutation({
    mutationFn: () => RunsApi.reopen(id!),
    onSuccess: () => {
      toast.success('اجرا بازگشایی شد');
      qc.invalidateQueries({ queryKey: ['run', id] });
    },
    onError: (err) => toast.error(extractError(err)),
  });
  const commentMut = useMutation({
    mutationFn: (body: string) => RunsApi.addComment(id!, body),
    onSuccess: () => {
      setComment('');
      qc.invalidateQueries({ queryKey: ['run-comments', id] });
    },
    onError: (err) => toast.error(extractError(err)),
  });
  const removeCaseMut = useMutation({
    mutationFn: (caseId: string) => RunsApi.removeCase(id!, caseId),
    onSuccess: () => {
      toast.success('سناریو از اجرا حذف شد');
      qc.invalidateQueries({ queryKey: ['run-cases', id] });
      qc.invalidateQueries({ queryKey: ['run', id] });
    },
    onError: (err) => toast.error(extractError(err)),
  });
  const autoRunMut = useMutation({
    mutationFn: () => RunsApi.runAutomaticAll(id!),
    onSuccess: () => {
      toast.success('اجرای خودکار در صف قرار گرفت');
      qc.invalidateQueries({ queryKey: ['run-automation-executions', id] });
      qc.invalidateQueries({ queryKey: ['run', id] });
      qc.invalidateQueries({ queryKey: ['run-cases', id] });
    },
    onError: (err) => toast.error(extractError(err)),
  });

  const counts = useMemo(() => {
    const c: Record<ResultStatus, number> = {
      Untested: 0, Passed: 0, Failed: 0, Blocked: 0, Retest: 0, Skipped: 0,
    };
    cases?.forEach((rc) => (c[rc.currentStatus] = (c[rc.currentStatus] ?? 0) + 1));
    return c;
  }, [cases]);

  const total = cases?.length ?? 0;
  const executed = total - counts.Untested;
  const passRate = executed > 0 ? (counts.Passed * 100) / executed : 0;
  const allCasesAutomatic = total > 0 && !!cases?.every((rc) => rc.hasAutomation);
  const latestAutomation = automationExecutions?.items[0];
  const automationIsActive = latestAutomation?.status === 'Queued' || latestAutomation?.status === 'Running';

  if (isLoading || !run)
    return (
      <div className="space-y-4">
        <Skeleton className="h-8 w-2/3" />
        <div className="grid grid-cols-2 lg:grid-cols-6 gap-3">
          {Array.from({ length: 6 }).map((_, i) => (
            <Skeleton key={i} className="h-24" />
          ))}
        </div>
        <Skeleton className="h-80 w-full" />
      </div>
    );

  function onComment(e: FormEvent) {
    e.preventDefault();
    if (!comment.trim()) return;
    commentMut.mutate(comment.trim());
  }

  return (
    <>
    <motion.div initial={{ opacity: 0, y: 6 }} animate={{ opacity: 1, y: 0 }} className="space-y-6">
      <PageHeader
        back="/runs"
        backLabel="اجراها"
        title={
          <div className="flex items-center gap-2.5">
            <span>{run.name}</span>
            <RunStatusBadge status={run.status} />
          </div>
        }
        description={`${run.projectCode} · ${run.code} · ساخته شده ${fmtRelative(run.createdAtUtc)}`}
        actions={
          <>
            {run.status === 'Open' ? (
              <>
                <Button asChild>
                  <Link to={`/runs/${run.id}/execute`}>
                    <PlayCircle className="size-4" /> اجرا
                  </Link>
                </Button>
                {allCasesAutomatic && (
                  <Button
                    variant="outline"
                    onClick={() => autoRunMut.mutate()}
                    loading={autoRunMut.isPending}
                    disabled={automationIsActive}
                  >
                    <Activity className="size-4" /> اجرای خودکار
                  </Button>
                )}
                <Button
                  variant="outline"
                  onClick={() => closeMut.mutate()}
                  loading={closeMut.isPending}
                >
                  <Lock className="size-4" /> بستن
                </Button>
              </>
            ) : (
              <Button
                variant="outline"
                onClick={() => reopenMut.mutate()}
                loading={reopenMut.isPending}
              >
                <Unlock className="size-4" /> بازگشایی
              </Button>
            )}
            <Button variant="outline" onClick={() => setShowEdit(true)}>
              <Pencil className="size-4" /> ویرایش
            </Button>
            <Button variant="outline" onClick={() => setRerunMode('all')}>
              <RotateCcw className="size-4" /> اجرای مجدد
            </Button>
            <Button variant="outline" onClick={() => setShowDuplicate(true)}>
              <Copy className="size-4" /> تکرار
            </Button>
          </>
        }
      />

      {/* Stats row */}
      <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-3">
        <StatsCard icon={ListChecks} label="کل" value={formatNumber(total)} variant="default" />
        <StatsCard icon={CheckCircle2} label="موفق" value={formatNumber(counts.Passed)} variant="success" />
        <StatsCard icon={XCircle} label="ناموفق" value={formatNumber(counts.Failed)} variant="destructive" />
        <StatsCard icon={Shield} label="مسدود" value={formatNumber(counts.Blocked)} variant="warning" />
        <StatsCard icon={SkipForward} label="تست نشده" value={formatNumber(counts.Untested)} variant="default" />
        <StatsCard
          icon={Activity}
          label="نرخ موفقیت"
          value={formatPercent(passRate)}
          variant={passRate >= 80 ? 'success' : passRate >= 50 ? 'warning' : 'destructive'}
        />
      </div>

      {latestAutomation && (
        <Card variant="flat">
          <CardContent className="p-4 flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
            <div className="space-y-1">
              <div className="flex items-center gap-2 text-sm font-semibold">
                <Activity className="size-4 text-info" />
                اجرای خودکار
                <Badge
                  variant={
                    latestAutomation.status === 'Failed'
                      ? 'destructive'
                      : latestAutomation.status === 'Completed'
                        ? 'success'
                        : 'info'
                  }
                  size="sm"
                >
                  {automationStatusLabel(latestAutomation.status)}
                </Badge>
              </div>
              <p className="text-xs text-muted-foreground">
                {latestAutomation.startedAtUtc
                  ? `شروع ${fmtRelative(latestAutomation.startedAtUtc)}`
                  : 'در صف اجرا'}
                {latestAutomation.completedAtUtc
                  ? ` · پایان ${fmtRelative(latestAutomation.completedAtUtc)}`
                  : ''}
              </p>
              {latestAutomation.errorMessage && (
                <p className="text-xs text-destructive">{latestAutomation.errorMessage}</p>
              )}
            </div>
            <div className="grid grid-cols-3 gap-3 text-center text-xs">
              <div>
                <div className="font-semibold text-sm">{formatNumber(latestAutomation.totalCases)}</div>
                <div className="text-muted-foreground">کل</div>
              </div>
              <div>
                <div className="font-semibold text-sm text-success">{formatNumber(latestAutomation.passed)}</div>
                <div className="text-muted-foreground">موفق</div>
              </div>
              <div>
                <div className="font-semibold text-sm text-destructive">{formatNumber(latestAutomation.failed)}</div>
                <div className="text-muted-foreground">ناموفق</div>
              </div>
            </div>
          </CardContent>
        </Card>
      )}

      {(counts.Failed > 0 || counts.Blocked > 0 || counts.Retest > 0) && (
        <div className="flex items-center gap-2">
          <Button variant="subtle" size="sm" onClick={() => setRerunMode('failed')}>
            <RotateCcw className="size-3.5" />
            اجرای مجدد ناموفق‌ها
            <Badge variant="destructive" size="sm">{counts.Failed + counts.Blocked + counts.Retest}</Badge>
          </Button>
        </div>
      )}

      {(run.description || run.environment || run.dueDateUtc) && (
        <Card variant="flat">
          <CardContent className="p-4 text-sm leading-7 space-y-2">
            {run.description && <p className="whitespace-pre-wrap">{run.description}</p>}
            {(run.environment || run.dueDateUtc) && (
              <div className="flex items-center gap-4 text-muted-foreground text-xs pt-1">
                {run.environment && <span>محیط: <span className="text-foreground font-medium">{run.environment}</span></span>}
                {run.dueDateUtc && <span>مهلت: <span className="text-foreground font-medium">{fmtDateTime(run.dueDateUtc)}</span></span>}
              </div>
            )}
          </CardContent>
        </Card>
      )}

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <Card className="lg:col-span-2">
          <CardHeader>
            <div className="flex items-center justify-between">
              <div>
                <div className="font-semibold text-sm">سناریوهای اجرا</div>
                <p className="text-xs text-muted-foreground">وضعیت لحظه‌ای هر سناریو</p>
              </div>
              {run.status === 'Open' && (
                <Button size="sm" variant="outline" onClick={() => setShowAddCases(true)}>
                  <Plus className="size-3.5" /> افزودن سناریو
                </Button>
              )}
            </div>
          </CardHeader>
          <CardContent className="px-0 pb-0">
            {!cases || cases.length === 0 ? (
              <EmptyState
                icon={<AlertTriangle className="size-7" />}
                title="سناریویی در این اجرا نیست"
              />
            ) : (
              <ul className="divide-y divide-border">
                {cases.map((rc) => (
                  <li
                    key={rc.id}
                    className="transition-base group"
                  >
                    <div className="flex items-center gap-3 px-5 py-3 hover:bg-accent/30">
                      <button
                        type="button"
                        onClick={() => {
                          if (rc.currentStatus === 'Failed')
                            setExpandedFailureId((current) => current === rc.id ? null : rc.id);
                        }}
                        className="flex min-w-0 flex-1 items-center gap-3 text-start"
                        aria-expanded={rc.currentStatus === 'Failed' ? expandedFailureId === rc.id : undefined}
                      >
                        <ResultDot status={rc.currentStatus} className="shrink-0" />
                        <div className="font-mono text-xs text-muted-foreground shrink-0 w-20">
                          {rc.testCaseCode}
                        </div>
                        <div className="flex-1 min-w-0">
                          <div className="text-sm font-medium truncate">{rc.testCaseTitle}</div>
                          {rc.lastExecutedAtUtc && (
                            <div className="text-xs text-muted-foreground flex items-center gap-1 mt-0.5">
                              <Clock className="size-3" /> {fmtRelative(rc.lastExecutedAtUtc)}
                            </div>
                          )}
                        </div>
                        <ResultBadge status={rc.currentStatus} />
                        {rc.currentStatus === 'Failed' && (
                          <span className="flex items-center gap-1 text-[11px] font-medium text-destructive">
                            جزئیات خطا
                            <ChevronDown
                              className={`size-3.5 transition-transform ${expandedFailureId === rc.id ? 'rotate-180' : ''}`}
                            />
                          </span>
                        )}
                      </button>
                      {run.status === 'Open' && (
                        <button
                          onClick={() => {
                            if (confirm('آیا از حذف این سناریو از اجرا مطمئن هستید؟'))
                              removeCaseMut.mutate(rc.id);
                          }}
                          className="opacity-0 group-hover:opacity-100 p-1 rounded hover:bg-destructive/10 text-muted-foreground hover:text-destructive transition-all shrink-0"
                          title="حذف از اجرا"
                        >
                          <Trash2 className="size-3.5" />
                        </button>
                      )}
                    </div>
                    {rc.currentStatus === 'Failed' && expandedFailureId === rc.id && (
                      <RunCaseFailureDetails runCaseId={rc.id} />
                    )}
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div className="font-semibold text-sm flex items-center gap-2">
              <MessageSquare className="size-3.5 text-info" /> یادداشت‌ها
              {comments && comments.length > 0 && (
                <Badge variant="muted" size="sm">
                  {formatNumber(comments.length)}
                </Badge>
              )}
            </div>
          </CardHeader>
          <CardContent className="space-y-4">
            <form onSubmit={onComment} className="flex flex-col gap-2">
              <Textarea
                value={comment}
                onChange={(e) => setComment(e.target.value)}
                placeholder="یادداشت یا گزارش بنویسید…"
                rows={3}
              />
              <Button
                type="submit"
                size="sm"
                loading={commentMut.isPending}
                disabled={!comment.trim()}
              >
                <Send className="size-3.5" /> ارسال
              </Button>
            </form>
            <div className="space-y-2 max-h-96 overflow-y-auto">
              {!comments || comments.length === 0 ? (
                <p className="text-sm text-muted-foreground text-center py-4">
                  هنوز یادداشتی ثبت نشده.
                </p>
              ) : (
                comments.map((c) => (
                  <div key={c.id} className="rounded-lg bg-secondary/40 p-3">
                    <div className="flex items-center gap-2 mb-1.5">
                      <div className="size-6 rounded-full bg-primary/80 text-primary-foreground text-[10px] font-bold flex items-center justify-center shrink-0">
                        {initials(c.authorUserId.slice(0, 2))}
                      </div>
                      <span className="text-xs text-muted-foreground">
                        {fmtDateTime(c.createdAtUtc)}
                      </span>
                    </div>
                    <p className="text-sm whitespace-pre-wrap">{c.body}</p>
                  </div>
                ))
              )}
            </div>
          </CardContent>
        </Card>
      </div>
    </motion.div>
    {rerunMode && cases && (
      <NewRunDialog
        onClose={() => setRerunMode(null)}
        rerunSource={{
          runId: run.id,
          runName: run.name,
          projectId: run.projectId,
          caseIds: rerunMode === 'all'
            ? cases.map((c) => c.testCaseId)
            : cases
                .filter((c) => c.currentStatus === 'Failed' || c.currentStatus === 'Blocked' || c.currentStatus === 'Untested' || c.currentStatus === 'Retest')
                .map((c) => c.testCaseId),
        }}
      />
    )}
    {showDuplicate && cases && (
      <NewRunDialog
        onClose={() => setShowDuplicate(false)}
        rerunSource={{
          runId: run.id,
          runName: `کپی ${run.name}`,
          projectId: run.projectId,
          caseIds: cases.map((c) => c.testCaseId),
        }}
      />
    )}
    {showEdit && (
      <EditRunDialog
        run={run}
        onClose={() => setShowEdit(false)}
      />
    )}
    {showAddCases && cases && (
      <AddCasesDialog
        runId={run.id}
        projectId={run.projectId}
        existingCaseIds={cases.map((c) => c.testCaseId)}
        onClose={() => setShowAddCases(false)}
      />
    )}
    </>
  );
}

function automationStatusLabel(status: string) {
  if (status === 'Completed') return 'تکمیل شده';
  if (status === 'Failed') return 'ناموفق';
  if (status === 'Running') return 'در حال اجرا';
  return 'در صف';
}
