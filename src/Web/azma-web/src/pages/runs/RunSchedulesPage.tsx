import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  CalendarClock,
  CheckSquare,
  Clock3,
  Edit2,
  History,
  Pause,
  Play,
  Plus,
  RefreshCw,
  Search,
  Square,
  Trash2,
  X,
} from 'lucide-react';
import {
  ProjectsApi,
  RunSchedulesApi,
  TestCasesApi,
  type RunSchedulePayload,
  type RunSchedulesListParams,
  type UpdateRunSchedulePayload,
} from '@/lib/queries/endpoints';
import type {
  Project,
  RunSchedule,
  RunScheduleExecution,
  ScheduleRecurrenceType,
  TestCaseSummary,
} from '@/lib/types';
import { PageHeader } from '@/components/common/PageHeader';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select } from '@/components/ui/native-select';
import { Badge } from '@/components/ui/badge';
import { ConfirmDialog } from '@/components/ui/ConfirmDialog';
import { EmptyState } from '@/components/common/EmptyState';
import { Skeleton, TableRowSkeleton } from '@/components/common/Skeletons';
import { fmtDateTime, fmtRelative } from '@/lib/date';
import { cn, formatNumber } from '@/lib/utils';
import { extractError } from '@/lib/api';
import { toast } from 'sonner';
import { motion } from 'framer-motion';

const DEFAULT_TIMEZONE = 'Asia/Tehran';
const DEFAULT_WEEK_MASK = 0b1111111;

const WEEK_DAYS = [
  { value: 6, label: 'شنبه' },
  { value: 0, label: 'یکشنبه' },
  { value: 1, label: 'دوشنبه' },
  { value: 2, label: 'سه شنبه' },
  { value: 3, label: 'چهارشنبه' },
  { value: 4, label: 'پنجشنبه' },
  { value: 5, label: 'جمعه' },
];

const RECURRENCE_OPTIONS: Array<{ value: ScheduleRecurrenceType; label: string }> = [
  { value: 'Every5Minutes', label: 'هر ۵ دقیقه' },
  { value: 'Every10Minutes', label: 'هر ۱۰ دقیقه' },
  { value: 'Every15Minutes', label: 'هر ۱۵ دقیقه' },
  { value: 'Every30Minutes', label: 'هر ۳۰ دقیقه' },
  { value: 'Hourly', label: 'ساعتی' },
  { value: 'Daily', label: 'روزانه' },
  { value: 'Weekly', label: 'هفتگی' },
];

type ScheduleFormState = {
  projectId: string;
  name: string;
  description: string;
  environment: string;
  recurrenceType: ScheduleRecurrenceType;
  time: string;
  selectedDays: Set<number>;
  timeZoneId: string;
  startAt: string;
  endAt: string;
  loops: number;
  threads: number;
  rampup: number;
  skippable: boolean;
  gateway: string;
  frontend: string;
  selectedCaseIds: Set<string>;
};

export default function RunSchedulesPage() {
  const qc = useQueryClient();
  const [projectId, setProjectId] = useState('');
  const [includeInactive, setIncludeInactive] = useState(true);
  const [page, setPage] = useState(1);
  const [dialog, setDialog] = useState<{ mode: 'create' | 'edit'; schedule: RunSchedule | null } | null>(null);
  const [historySchedule, setHistorySchedule] = useState<RunSchedule | null>(null);
  const [deleteSchedule, setDeleteSchedule] = useState<RunSchedule | null>(null);
  const pageSize = 20;

  const { data: projects } = useQuery({
    queryKey: ['projects'],
    queryFn: () => ProjectsApi.list(),
  });

  const queryParams = useMemo<RunSchedulesListParams>(
    () => ({
      projectId: projectId || undefined,
      includeInactive,
      page,
      pageSize,
    }),
    [projectId, includeInactive, page],
  );

  const { data, isLoading } = useQuery({
    queryKey: ['run-schedules', queryParams],
    queryFn: () => RunSchedulesApi.list(queryParams),
  });

  const projectById = useMemo(() => {
    const map = new Map<string, Project>();
    projects?.forEach((p) => map.set(p.id, p));
    return map;
  }, [projects]);

  const totalPages = data ? Math.max(1, Math.ceil(data.total / pageSize)) : 1;

  const stateMutation = useMutation({
    mutationFn: async (p: { id: string; action: 'pause' | 'resume' | 'runNow' }) => {
      if (p.action === 'pause') return RunSchedulesApi.pause(p.id);
      if (p.action === 'resume') return RunSchedulesApi.resume(p.id);
      return RunSchedulesApi.runNow(p.id);
    },
    onSuccess: (_r, vars) => {
      toast.success(vars.action === 'runNow' ? 'اجرا در صف قرار گرفت' : 'وضعیت زمان بندی به روز شد');
      qc.invalidateQueries({ queryKey: ['run-schedules'] });
    },
    onError: (err) => toast.error(extractError(err)),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => RunSchedulesApi.remove(id),
    onSuccess: () => {
      toast.success('زمان بندی حذف شد');
      setDeleteSchedule(null);
      qc.invalidateQueries({ queryKey: ['run-schedules'] });
    },
    onError: (err) => toast.error(extractError(err)),
  });

  return (
    <div className="space-y-6">
      <PageHeader
        title="زمان بندی اجراها"
        description="اجرای خودکار سناریوهای دارای اتوماسیون، با Run مستقل برای هر نوبت."
        actions={
          <Button onClick={() => setDialog({ mode: 'create', schedule: null })}>
            <Plus className="size-4" /> زمان بندی جدید
          </Button>
        }
      />

      <Card>
        <CardContent className="p-4">
          <div className="grid grid-cols-1 md:grid-cols-12 gap-3">
            <div className="md:col-span-5">
              <Select
                value={projectId}
                onChange={(e) => {
                  setProjectId(e.target.value);
                  setPage(1);
                }}
              >
                <option value="">همه پروژه ها</option>
                {projects?.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.code} - {p.name}
                  </option>
                ))}
              </Select>
            </div>
            <div className="md:col-span-4 flex items-center gap-2 rounded-lg border border-input px-3 h-9">
              <input
                id="includeInactive"
                type="checkbox"
                checked={includeInactive}
                onChange={(e) => {
                  setIncludeInactive(e.target.checked);
                  setPage(1);
                }}
                className="size-4 rounded border-input"
              />
              <label htmlFor="includeInactive" className="text-sm text-muted-foreground cursor-pointer">
                نمایش غیرفعال ها
              </label>
            </div>
            <div className="md:col-span-3 flex items-center justify-end">
              <Badge variant="muted" className="num-fa">
                {formatNumber(data?.total ?? 0)} زمان بندی
              </Badge>
            </div>
          </div>
        </CardContent>
      </Card>

      <Card>
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="border-b border-border bg-muted/40 text-muted-foreground text-xs">
              <tr>
                <th className="text-start font-medium px-4 py-3">نام</th>
                <th className="text-start font-medium px-4 py-3">پروژه</th>
                <th className="text-start font-medium px-4 py-3">برنامه</th>
                <th className="text-start font-medium px-4 py-3">اجرای بعدی</th>
                <th className="text-start font-medium px-4 py-3">آخرین اجرا</th>
                <th className="text-start font-medium px-4 py-3">سناریوها</th>
                <th className="text-start font-medium px-4 py-3">وضعیت</th>
                <th className="text-start font-medium px-4 py-3"></th>
              </tr>
            </thead>
            <tbody>
              {isLoading ? (
                Array.from({ length: 6 }).map((_, i) => <TableRowSkeleton key={i} cols={8} />)
              ) : !data || data.items.length === 0 ? (
                <tr>
                  <td colSpan={8}>
                    <EmptyState
                      icon={<CalendarClock className="size-7" />}
                      title="زمان بندی ای یافت نشد"
                      action={
                        <Button onClick={() => setDialog({ mode: 'create', schedule: null })}>
                          <Plus className="size-4" /> زمان بندی جدید
                        </Button>
                      }
                    />
                  </td>
                </tr>
              ) : (
                data.items.map((schedule) => {
                  const project = projectById.get(schedule.projectId);
                  return (
                    <motion.tr
                      key={schedule.id}
                      initial={{ opacity: 0 }}
                      animate={{ opacity: 1 }}
                      className="border-b border-border hover:bg-accent/30 transition-base"
                    >
                      <td className="px-4 py-3 min-w-56">
                        <div className="font-medium">{schedule.name}</div>
                        {schedule.environment && (
                          <div className="text-[11px] text-muted-foreground mt-0.5">
                            {schedule.environment}
                          </div>
                        )}
                      </td>
                      <td className="px-4 py-3">
                        <Badge variant="muted">{project?.code ?? '—'}</Badge>
                      </td>
                      <td className="px-4 py-3 min-w-44">
                        <div className="flex items-center gap-1.5 text-xs">
                          <Clock3 className="size-3.5 text-muted-foreground" />
                          <span>{scheduleLabel(schedule)}</span>
                        </div>
                        <div className="text-[11px] text-muted-foreground mt-0.5" dir="ltr">
                          {schedule.timeZoneId}
                        </div>
                      </td>
                      <td className="px-4 py-3 text-xs text-muted-foreground whitespace-nowrap">
                        {schedule.nextRunAtUtc ? (
                          <div title={fmtDateTime(schedule.nextRunAtUtc)}>
                            {fmtRelative(schedule.nextRunAtUtc)}
                          </div>
                        ) : (
                          '—'
                        )}
                      </td>
                      <td className="px-4 py-3 text-xs text-muted-foreground whitespace-nowrap">
                        {schedule.lastRunAtUtc ? (
                          <Link
                            to={schedule.lastRunId ? `/runs/${schedule.lastRunId}` : '#'}
                            className="hover:text-primary"
                            title={fmtDateTime(schedule.lastRunAtUtc)}
                          >
                            {fmtRelative(schedule.lastRunAtUtc)}
                          </Link>
                        ) : (
                          'هنوز اجرا نشده'
                        )}
                      </td>
                      <td className="px-4 py-3 num-fa">{formatNumber(schedule.caseCount)}</td>
                      <td className="px-4 py-3">
                        <Badge variant={schedule.isActive ? 'success' : 'muted'} dot>
                          {schedule.isActive ? 'فعال' : 'متوقف'}
                        </Badge>
                      </td>
                      <td className="px-4 py-3">
                        <div className="flex items-center justify-end gap-1">
                          <Button
                            variant="ghost"
                            size="icon-sm"
                            title="اجرای فوری"
                            loading={stateMutation.isPending}
                            onClick={() => stateMutation.mutate({ id: schedule.id, action: 'runNow' })}
                          >
                            <Play className="size-4" />
                          </Button>
                          <Button
                            variant="ghost"
                            size="icon-sm"
                            title={schedule.isActive ? 'توقف' : 'فعال سازی'}
                            loading={stateMutation.isPending}
                            onClick={() =>
                              stateMutation.mutate({
                                id: schedule.id,
                                action: schedule.isActive ? 'pause' : 'resume',
                              })
                            }
                          >
                            {schedule.isActive ? <Pause className="size-4" /> : <RefreshCw className="size-4" />}
                          </Button>
                          <Button
                            variant="ghost"
                            size="icon-sm"
                            title="تاریخچه"
                            onClick={() => setHistorySchedule(schedule)}
                          >
                            <History className="size-4" />
                          </Button>
                          <Button
                            variant="ghost"
                            size="icon-sm"
                            title="ویرایش"
                            onClick={() => setDialog({ mode: 'edit', schedule })}
                          >
                            <Edit2 className="size-4" />
                          </Button>
                          <Button
                            variant="ghost"
                            size="icon-sm"
                            title="حذف"
                            onClick={() => setDeleteSchedule(schedule)}
                          >
                            <Trash2 className="size-4 text-destructive" />
                          </Button>
                        </div>
                      </td>
                    </motion.tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>

        {data && data.total > pageSize && (
          <div className="flex items-center justify-between px-4 py-3 border-t border-border text-sm">
            <div className="text-muted-foreground num-fa">
              صفحه {formatNumber(page)} از {formatNumber(totalPages)}
            </div>
            <div className="flex items-center gap-1">
              <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage(page - 1)}>
                قبلی
              </Button>
              <Button variant="outline" size="sm" disabled={page >= totalPages} onClick={() => setPage(page + 1)}>
                بعدی
              </Button>
            </div>
          </div>
        )}
      </Card>

      {dialog && (
        <RunScheduleDialog
          mode={dialog.mode}
          schedule={dialog.schedule}
          defaultProjectId={projectId}
          projects={projects ?? []}
          onClose={() => setDialog(null)}
        />
      )}

      {historySchedule && (
        <ScheduleExecutionsDialog
          schedule={historySchedule}
          onClose={() => setHistorySchedule(null)}
        />
      )}

      <ConfirmDialog
        open={!!deleteSchedule}
        title="حذف زمان بندی"
        description={`زمان بندی «${deleteSchedule?.name ?? ''}» حذف شود؟`}
        confirmText="حذف"
        destructive
        icon={<Trash2 className="size-5" />}
        onClose={() => setDeleteSchedule(null)}
        onConfirm={() => {
          if (deleteSchedule) deleteMutation.mutate(deleteSchedule.id);
        }}
      />
    </div>
  );
}

function RunScheduleDialog({
  mode,
  schedule,
  defaultProjectId,
  projects,
  onClose,
}: {
  mode: 'create' | 'edit';
  schedule: RunSchedule | null;
  defaultProjectId: string;
  projects: Project[];
  onClose: () => void;
}) {
  const qc = useQueryClient();
  const browserZone = getBrowserTimeZone();
  const [search, setSearch] = useState('');
  const [form, setForm] = useState<ScheduleFormState>(() =>
    initialForm(schedule, defaultProjectId || projects[0]?.id || '', browserZone),
  );

  const { data: detail, isLoading: detailLoading } = useQuery({
    queryKey: ['run-schedule', schedule?.id],
    queryFn: () => RunSchedulesApi.get(schedule!.id),
    enabled: mode === 'edit' && !!schedule,
  });

  useEffect(() => {
    if (mode === 'edit' && detail) {
      setForm(initialForm(detail.schedule, detail.schedule.projectId, browserZone, detail.cases.map((c) => c.testCaseId)));
      return;
    }
    if (mode === 'create') {
      setForm((current) => {
        if (current.projectId || !projects[0]?.id) return current;
        return { ...current, projectId: defaultProjectId || projects[0].id };
      });
    }
  }, [mode, detail, projects, defaultProjectId, browserZone]);

  const listParams = useMemo(
    () => ({
      projectId: form.projectId,
      status: 'Active' as const,
      hasAutomation: true,
      search: search || undefined,
      page: 1,
      pageSize: 200,
    }),
    [form.projectId, search],
  );

  const builtInListParams = useMemo(
    () => ({
      ...listParams,
      automationEngine: 'ScenarioRecording' as const,
    }),
    [listParams],
  );

  const { data: testcases, isLoading: casesLoading } = useQuery({
    queryKey: ['testcases-automation-picker', listParams],
    queryFn: () => TestCasesApi.list(listParams),
    enabled: !!form.projectId,
  });
  const { data: builtInTestcases, isLoading: builtInCasesLoading } = useQuery({
    queryKey: ['testcases-automation-picker-built-in', builtInListParams],
    queryFn: () => TestCasesApi.list(builtInListParams),
    enabled: !!form.projectId,
  });

  const automatedCases = useMemo(
    () => {
      const byId = new Map<string, TestCaseSummary>();
      [...(builtInTestcases?.items ?? []), ...(testcases?.items ?? [])]
        .filter((tc) => tc.hasAutomation && tc.automationEngine === 'ScenarioRecording')
        .forEach((tc) => byId.set(tc.id, tc));
      return Array.from(byId.values()).sort((a, b) => b.createdAtUtc.localeCompare(a.createdAtUtc));
    },
    [builtInTestcases, testcases],
  );

  const saveMutation = useMutation({
    mutationFn: () => {
      const payload = buildPayload(form);
      if (mode === 'edit' && schedule) {
        return RunSchedulesApi.update(schedule.id, payload as UpdateRunSchedulePayload);
      }
      return RunSchedulesApi.create(payload as RunSchedulePayload);
    },
    onSuccess: () => {
      toast.success(mode === 'edit' ? 'زمان بندی به روز شد' : 'زمان بندی ساخته شد');
      qc.invalidateQueries({ queryKey: ['run-schedules'] });
      onClose();
    },
    onError: (err) => toast.error(extractError(err)),
  });

  function toggleCase(id: string) {
    setForm((prev) => {
      const next = new Set(prev.selectedCaseIds);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return { ...prev, selectedCaseIds: next };
    });
  }

  function toggleDay(day: number) {
    setForm((prev) => {
      const selectedDays = new Set(prev.selectedDays);
      if (selectedDays.has(day)) selectedDays.delete(day);
      else selectedDays.add(day);
      return { ...prev, selectedDays };
    });
  }

  function submit() {
    if (!form.projectId || !form.name.trim() || form.selectedCaseIds.size === 0) {
      toast.error('پروژه، نام و حداقل یک سناریوی اتوماتیک الزامی است');
      return;
    }
    if (form.recurrenceType === 'Weekly' && form.selectedDays.size === 0) {
      toast.error('برای اجرای هفتگی حداقل یک روز انتخاب کنید');
      return;
    }
    saveMutation.mutate();
  }

  function onSubmit(e: FormEvent) {
    e.preventDefault();
    submit();
  }

  const selectedCount = form.selectedCaseIds.size;
  const currentDetailCases = detail?.cases ?? [];
  const selectedMissingFromList = currentDetailCases.filter(
    (c) => c.automationEngine === 'ScenarioRecording' && form.selectedCaseIds.has(c.testCaseId) && !automatedCases.some((tc) => tc.id === c.testCaseId),
  );

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm p-4" onClick={onClose}>
      <motion.div
        initial={{ opacity: 0, scale: 0.96, y: 8 }}
        animate={{ opacity: 1, scale: 1, y: 0 }}
        onClick={(e) => e.stopPropagation()}
        className="bg-card border border-border rounded-2xl shadow-soft-lg w-full max-w-5xl max-h-[92vh] flex flex-col"
      >
        <div className="flex items-center justify-between p-5 border-b border-border">
          <div className="flex items-center gap-3">
            <div className="size-10 rounded-xl bg-primary/10 text-primary flex items-center justify-center">
              <CalendarClock className="size-5" />
            </div>
            <div>
              <h3 className="font-semibold">{mode === 'edit' ? 'ویرایش زمان بندی' : 'زمان بندی جدید'}</h3>
              <p className="text-xs text-muted-foreground">هر نوبت یک Run جدید می سازد.</p>
            </div>
          </div>
          <Button variant="ghost" size="icon-sm" onClick={onClose}>
            <X className="size-4" />
          </Button>
        </div>

        {detailLoading ? (
          <div className="p-5 space-y-3">
            <Skeleton className="h-9 w-full" />
            <Skeleton className="h-32 w-full" />
            <Skeleton className="h-52 w-full" />
          </div>
        ) : (
          <form onSubmit={onSubmit} className="flex-1 overflow-y-auto p-5 space-y-5">
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div>
                <Label>پروژه *</Label>
                <Select
                  value={form.projectId}
                  disabled={mode === 'edit'}
                  onChange={(e) =>
                    setForm((prev) => ({
                      ...prev,
                      projectId: e.target.value,
                      selectedCaseIds: new Set(),
                    }))
                  }
                  required
                >
                  <option value="">انتخاب کنید</option>
                  {projects.map((p) => (
                    <option key={p.id} value={p.id}>
                      {p.code} - {p.name}
                    </option>
                  ))}
                </Select>
              </div>
              <div>
                <Label>نام *</Label>
                <Input
                  value={form.name}
                  onChange={(e) => setForm((prev) => ({ ...prev, name: e.target.value }))}
                  placeholder="Regression هر شب"
                  required
                />
              </div>
              <div className="md:col-span-2">
                <Label>توضیحات</Label>
                <Textarea
                  value={form.description}
                  onChange={(e) => setForm((prev) => ({ ...prev, description: e.target.value }))}
                  rows={2}
                />
              </div>
              <div>
                <Label>محیط اجرا</Label>
                <Input
                  value={form.environment}
                  onChange={(e) => setForm((prev) => ({ ...prev, environment: e.target.value }))}
                  placeholder="Staging, QA, Production..."
                />
              </div>
              <div>
                <Label>Timezone</Label>
                <Input
                  value={form.timeZoneId}
                  dir="ltr"
                  onChange={(e) => setForm((prev) => ({ ...prev, timeZoneId: e.target.value }))}
                />
              </div>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-12 gap-4 rounded-lg border border-border bg-secondary/20 p-4">
              <div className="md:col-span-3">
                <Label>تکرار</Label>
                <Select
                  value={form.recurrenceType}
                  onChange={(e) =>
                    setForm((prev) => ({
                      ...prev,
                      recurrenceType: e.target.value as ScheduleRecurrenceType,
                    }))
                  }
                >
                  {RECURRENCE_OPTIONS.map((option) => (
                    <option key={option.value} value={option.value}>
                      {option.label}
                    </option>
                  ))}
                </Select>
              </div>
              <div className="md:col-span-3">
                {isIntervalRecurrence(form.recurrenceType) ? (
                  <>
                    <Label>بازه اجرا</Label>
                    <div className="h-9 flex items-center rounded-lg border border-input bg-muted/30 px-3 text-sm text-muted-foreground">
                      {intervalLabel(form.recurrenceType)}
                    </div>
                  </>
                ) : (
                  <>
                    <Label>ساعت اجرا</Label>
                    <Input
                      type="time"
                      value={form.time}
                      onChange={(e) => setForm((prev) => ({ ...prev, time: e.target.value }))}
                    />
                  </>
                )}
              </div>
              <div className="md:col-span-3">
                <Label>شروع اختیاری</Label>
                <Input
                  type="datetime-local"
                  value={form.startAt}
                  onChange={(e) => setForm((prev) => ({ ...prev, startAt: e.target.value }))}
                />
              </div>
              <div className="md:col-span-3">
                <Label>پایان اختیاری</Label>
                <Input
                  type="datetime-local"
                  value={form.endAt}
                  onChange={(e) => setForm((prev) => ({ ...prev, endAt: e.target.value }))}
                />
              </div>
              {form.recurrenceType === 'Weekly' && (
                <div className="md:col-span-12 flex flex-wrap gap-2">
                  {WEEK_DAYS.map((day) => {
                    const active = form.selectedDays.has(day.value);
                    return (
                      <button
                        key={day.value}
                        type="button"
                        onClick={() => toggleDay(day.value)}
                        className={cn(
                          'h-8 px-3 rounded-lg border text-xs transition-base',
                          active
                            ? 'border-primary bg-primary/10 text-primary'
                            : 'border-border hover:bg-accent text-muted-foreground',
                        )}
                      >
                        {day.label}
                      </button>
                    );
                  })}
                </div>
              )}
            </div>

            <div className="grid grid-cols-2 md:grid-cols-6 gap-4">
              <div>
                <Label>Loops</Label>
                <Input
                  type="number"
                  min={1}
                  value={form.loops}
                  onChange={(e) => setForm((prev) => ({ ...prev, loops: Number(e.target.value) || 1 }))}
                />
              </div>
              <div>
                <Label>Threads</Label>
                <Input
                  type="number"
                  min={1}
                  value={form.threads}
                  onChange={(e) => setForm((prev) => ({ ...prev, threads: Number(e.target.value) || 1 }))}
                />
              </div>
              <div>
                <Label>Rampup</Label>
                <Input
                  type="number"
                  min={0}
                  value={form.rampup}
                  onChange={(e) => setForm((prev) => ({ ...prev, rampup: Number(e.target.value) || 0 }))}
                />
              </div>
              <div className="flex items-end">
                <label className="h-9 w-full flex items-center gap-2 rounded-lg border border-input px-3 text-sm cursor-pointer">
                  <input
                    type="checkbox"
                    checked={form.skippable}
                    onChange={(e) => setForm((prev) => ({ ...prev, skippable: e.target.checked }))}
                    className="size-4"
                  />
                  Skippable
                </label>
              </div>
              <div className="md:col-span-2">
                <Label>Gateway</Label>
                <Input
                  value={form.gateway}
                  onChange={(e) => setForm((prev) => ({ ...prev, gateway: e.target.value }))}
                  dir="ltr"
                />
              </div>
              <div className="md:col-span-3">
                <Label>Frontend</Label>
                <Input
                  value={form.frontend}
                  onChange={(e) => setForm((prev) => ({ ...prev, frontend: e.target.value }))}
                  dir="ltr"
                />
              </div>
            </div>

            <div className="space-y-3">
              <div className="flex items-center justify-between gap-3">
                <Label className="mb-0">سناریوهای اتوماتیک *</Label>
                <Badge variant="info" className="num-fa">
                  {formatNumber(selectedCount)} انتخاب شده
                </Badge>
              </div>
              <div className="relative">
                <Search className="absolute right-3 top-1/2 -translate-y-1/2 size-4 text-muted-foreground pointer-events-none" />
                <Input
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                  placeholder="جست وجوی سناریوی اتوماتیک..."
                  className="ps-10"
                />
              </div>
              <div className="border border-border rounded-lg max-h-72 overflow-y-auto">
                {casesLoading || builtInCasesLoading ? (
                  <div className="p-4 space-y-2">
                    {Array.from({ length: 5 }).map((_, i) => (
                      <Skeleton key={i} className="h-10" />
                    ))}
                  </div>
                ) : !form.projectId ? (
                  <div className="p-6 text-center text-sm text-muted-foreground">ابتدا پروژه را انتخاب کنید.</div>
                ) : automatedCases.length === 0 && selectedMissingFromList.length === 0 ? (
                  <div className="p-6 text-center text-sm text-muted-foreground">سناریوی اتوماتیکی یافت نشد.</div>
                ) : (
                  <ul className="divide-y divide-border">
                    {selectedMissingFromList.map((tc) => (
                      <ScheduleCasePickerItem
                        key={tc.testCaseId}
                        code={tc.testCaseCode}
                        title={tc.testCaseTitle}
                        engine={tc.automationEngine}
                        checked
                        onClick={() => toggleCase(tc.testCaseId)}
                      />
                    ))}
                    {automatedCases.map((tc) => (
                      <ScheduleCasePickerItem
                        key={tc.id}
                        code={tc.code}
                        title={tc.title}
                        engine={tc.automationEngine}
                        checked={form.selectedCaseIds.has(tc.id)}
                        onClick={() => toggleCase(tc.id)}
                      />
                    ))}
                  </ul>
                )}
              </div>
            </div>
          </form>
        )}

        <div className="p-5 border-t border-border flex justify-end gap-2">
          <Button variant="outline" onClick={onClose}>
            انصراف
          </Button>
          <Button onClick={submit} loading={saveMutation.isPending}>
            {mode === 'edit' ? 'ذخیره تغییرات' : 'ساخت زمان بندی'}
          </Button>
        </div>
      </motion.div>
    </div>
  );
}

function ScheduleCasePickerItem({
  code,
  title,
  engine,
  checked,
  onClick,
}: {
  code: string;
  title: string;
  engine?: string | null;
  checked: boolean;
  onClick: () => void;
}) {
  const engineLabel = engine === 'ScenarioRecording' ? 'Azma Built-in' : 'Manual';
  return (
    <li>
      <button
        type="button"
        onClick={onClick}
        className="w-full flex items-center gap-3 px-4 py-2.5 cursor-pointer hover:bg-accent/40 text-start"
      >
        {checked ? (
          <CheckSquare className="size-4 text-primary shrink-0" />
        ) : (
          <Square className="size-4 text-muted-foreground shrink-0" />
        )}
        <span className="font-mono text-xs text-muted-foreground w-20 shrink-0">{code}</span>
        <span className="flex-1 truncate text-sm">{title}</span>
        <span className="hidden md:flex max-w-56 items-center gap-1 truncate text-[11px] text-muted-foreground" dir="ltr">
          <Badge variant={engine === 'ScenarioRecording' ? 'info' : 'muted'} size="sm">{engineLabel}</Badge>
        </span>
      </button>
    </li>
  );
}

function ScheduleExecutionsDialog({
  schedule,
  onClose,
}: {
  schedule: RunSchedule;
  onClose: () => void;
}) {
  const { data, isLoading } = useQuery({
    queryKey: ['run-schedule-executions', schedule.id],
    queryFn: () => RunSchedulesApi.executions(schedule.id, { page: 1, pageSize: 20 }),
  });

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm p-4" onClick={onClose}>
      <motion.div
        initial={{ opacity: 0, scale: 0.96, y: 8 }}
        animate={{ opacity: 1, scale: 1, y: 0 }}
        onClick={(e) => e.stopPropagation()}
        className="bg-card border border-border rounded-2xl shadow-soft-lg w-full max-w-3xl max-h-[86vh] flex flex-col"
      >
        <div className="flex items-center justify-between p-5 border-b border-border">
          <div>
            <h3 className="font-semibold">تاریخچه اجرا</h3>
            <p className="text-xs text-muted-foreground mt-0.5">{schedule.name}</p>
          </div>
          <Button variant="ghost" size="icon-sm" onClick={onClose}>
            <X className="size-4" />
          </Button>
        </div>
        <div className="overflow-y-auto">
          <table className="w-full text-sm">
            <thead className="border-b border-border bg-muted/40 text-muted-foreground text-xs sticky top-0">
              <tr>
                <th className="text-start font-medium px-4 py-3">شروع</th>
                <th className="text-start font-medium px-4 py-3">وضعیت</th>
                <th className="text-start font-medium px-4 py-3">Run</th>
                <th className="text-start font-medium px-4 py-3">نتیجه</th>
                <th className="text-start font-medium px-4 py-3">خطا</th>
              </tr>
            </thead>
            <tbody>
              {isLoading ? (
                Array.from({ length: 4 }).map((_, i) => <TableRowSkeleton key={i} cols={5} />)
              ) : !data || data.items.length === 0 ? (
                <tr>
                  <td colSpan={5} className="p-8 text-center text-sm text-muted-foreground">
                    هنوز اجرایی ثبت نشده است.
                  </td>
                </tr>
              ) : (
                data.items.map((item) => <ExecutionRow key={item.id} item={item} />)
              )}
            </tbody>
          </table>
        </div>
      </motion.div>
    </div>
  );
}

function ExecutionRow({ item }: { item: RunScheduleExecution }) {
  return (
    <tr className="border-b border-border">
      <td className="px-4 py-3 text-xs text-muted-foreground">
        <div>{fmtRelative(item.startedAtUtc)}</div>
        <div className="text-[10px] mt-0.5">{fmtDateTime(item.startedAtUtc)}</div>
      </td>
      <td className="px-4 py-3">
        <Badge
          variant={item.status === 'Completed' ? 'success' : item.status === 'Failed' ? 'destructive' : 'info'}
          dot
        >
          {executionStatusLabel(item.status)}
        </Badge>
      </td>
      <td className="px-4 py-3">
        {item.runId ? (
          <Button asChild variant="outline" size="xs">
            <Link to={`/runs/${item.runId}`}>مشاهده Run</Link>
          </Button>
        ) : (
          <span className="text-muted-foreground">—</span>
        )}
      </td>
      <td className="px-4 py-3 text-xs text-muted-foreground num-fa">
        {formatNumber(item.passed)} موفق / {formatNumber(item.failed)} ناموفق از {formatNumber(item.totalCases)}
      </td>
      <td className="px-4 py-3 max-w-60 truncate text-xs text-destructive">
        {item.errorMessage ?? '—'}
      </td>
    </tr>
  );
}

function initialForm(
  schedule: RunSchedule | null,
  projectId: string,
  timeZoneId: string,
  caseIds: string[] = [],
): ScheduleFormState {
  const selectedDays = schedule
    ? maskToDays(schedule.weekDaysMask || DEFAULT_WEEK_MASK)
    : maskToDays(DEFAULT_WEEK_MASK);
  return {
    projectId: schedule?.projectId ?? projectId,
    name: schedule?.name ?? '',
    description: schedule?.description ?? '',
    environment: schedule?.environment ?? '',
    recurrenceType: schedule?.recurrenceType ?? 'Daily',
    time: isIntervalRecurrence(schedule?.recurrenceType) ? '09:00' : minutesToTime(schedule?.timeOfDayMinutes ?? 9 * 60),
    selectedDays,
    timeZoneId: schedule?.timeZoneId ?? timeZoneId,
    startAt: toLocalInput(schedule?.startAtUtc),
    endAt: toLocalInput(schedule?.endAtUtc),
    loops: schedule?.loops ?? 1,
    threads: schedule?.threads ?? 1,
    rampup: schedule?.rampup ?? 0,
    skippable: schedule?.skippable ?? false,
    gateway: schedule?.gateway ?? '',
    frontend: schedule?.frontend ?? '',
    selectedCaseIds: new Set(caseIds),
  };
}

function buildPayload(form: ScheduleFormState): RunSchedulePayload {
  return {
    projectId: form.projectId,
    name: form.name.trim(),
    description: form.description.trim() || null,
    environment: form.environment.trim() || null,
    recurrenceType: form.recurrenceType,
    timeOfDayMinutes: recurrenceIntervalMinutes(form.recurrenceType) ?? timeToMinutes(form.time),
    weekDaysMask: form.recurrenceType === 'Weekly' ? daysToMask(form.selectedDays) : DEFAULT_WEEK_MASK,
    timeZoneId: form.timeZoneId.trim() || DEFAULT_TIMEZONE,
    startAtUtc: fromLocalInput(form.startAt),
    endAtUtc: fromLocalInput(form.endAt),
    testCaseIds: Array.from(form.selectedCaseIds),
    loops: form.loops,
    threads: form.threads,
    rampup: form.rampup,
    skippable: form.skippable,
    gateway: form.gateway.trim() || null,
    frontend: form.frontend.trim() || null,
  };
}

function scheduleLabel(schedule: RunSchedule) {
  if (isIntervalRecurrence(schedule.recurrenceType)) return intervalLabel(schedule.recurrenceType);

  const time = minutesToTime(schedule.timeOfDayMinutes);
  if (schedule.recurrenceType === 'Daily') return `هر روز، ساعت ${time}`;
  const days = WEEK_DAYS.filter((d) => (schedule.weekDaysMask & (1 << d.value)) !== 0)
    .map((d) => d.label)
    .join('، ');
  return `${days || 'هفتگی'}، ساعت ${time}`;
}

function isIntervalRecurrence(value?: ScheduleRecurrenceType | null) {
  return recurrenceIntervalMinutes(value) != null;
}

function recurrenceIntervalMinutes(value?: ScheduleRecurrenceType | null) {
  if (value === 'Hourly') return 60;
  if (value === 'Every30Minutes') return 30;
  if (value === 'Every15Minutes') return 15;
  if (value === 'Every10Minutes') return 10;
  if (value === 'Every5Minutes') return 5;
  return null;
}

function intervalLabel(value: ScheduleRecurrenceType) {
  const minutes = recurrenceIntervalMinutes(value);
  if (minutes === 60) return 'هر ۱ ساعت';
  if (minutes != null) return `هر ${formatNumber(minutes)} دقیقه`;
  return '';
}

function executionStatusLabel(status: RunScheduleExecution['status']) {
  if (status === 'Completed') return 'کامل';
  if (status === 'Failed') return 'ناموفق';
  return 'در حال اجرا';
}

function minutesToTime(minutes: number) {
  const safe = Math.max(0, Math.min(1439, minutes));
  const h = Math.floor(safe / 60);
  const m = safe % 60;
  return `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}`;
}

function timeToMinutes(value: string) {
  const [h, m] = value.split(':').map((x) => Number(x));
  return Math.max(0, Math.min(1439, (h || 0) * 60 + (m || 0)));
}

function daysToMask(days: Set<number>) {
  let mask = 0;
  days.forEach((day) => {
    mask |= 1 << day;
  });
  return mask;
}

function maskToDays(mask: number) {
  const days = new Set<number>();
  WEEK_DAYS.forEach((day) => {
    if ((mask & (1 << day.value)) !== 0) days.add(day.value);
  });
  return days;
}

function toLocalInput(value?: string | null) {
  if (!value) return '';
  const d = new Date(value);
  const local = new Date(d.getTime() - d.getTimezoneOffset() * 60_000);
  return local.toISOString().slice(0, 16);
}

function fromLocalInput(value: string) {
  return value ? new Date(value).toISOString() : null;
}

function getBrowserTimeZone() {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || DEFAULT_TIMEZONE;
  } catch {
    return DEFAULT_TIMEZONE;
  }
}
