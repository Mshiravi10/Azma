import { useState, useMemo } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { TestCasesApi, ProjectsApi } from '@/lib/queries/endpoints';
import type { TestCaseStatus, TestCasePriority, TestCaseExecutionState } from '@/lib/types';
import { PageHeader } from '@/components/common/PageHeader';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/native-select';
import { Badge } from '@/components/ui/badge';
import { Skeleton, TableRowSkeleton } from '@/components/common/Skeletons';
import { EmptyState } from '@/components/common/EmptyState';
import { TestCaseStatusBadge, PriorityBadge } from '@/components/common/StatusBadges';
import { ExecutionSummary } from '@/components/common/ExecutionStatus';
import { ScenarioAuthor } from '@/components/common/ScenarioAuthor';
import { Plus, Search, FlaskConical, ChevronLeft, ChevronRight, X, Pencil } from 'lucide-react';
import { formatNumber } from '@/lib/utils';
import { fmtDate } from '@/lib/date';
import { motion } from 'framer-motion';

const STATUSES: TestCaseStatus[] = ['Draft', 'Active', 'Deprecated'];
const PRIORITIES: TestCasePriority[] = ['Low', 'Medium', 'High', 'Critical'];

export default function TestCasesPage() {
  const [params, setParams] = useSearchParams();
  const projectId = params.get('projectId') ?? undefined;
  const [search, setSearch] = useState(params.get('search') ?? '');
  const [status, setStatus] = useState<TestCaseStatus | undefined>(
    (params.get('status') as TestCaseStatus | null) ?? undefined,
  );
  const [priority, setPriority] = useState<TestCasePriority | undefined>(
    (params.get('priority') as TestCasePriority | null) ?? undefined,
  );
  const [executionState, setExecutionState] = useState<TestCaseExecutionState | undefined>(
    (params.get('executionState') as TestCaseExecutionState | null) ?? undefined,
  );
  const [page, setPage] = useState(1);
  const pageSize = 20;

  const { data: projects } = useQuery({ queryKey: ['projects'], queryFn: () => ProjectsApi.list() });

  const queryParams = useMemo(
    () => ({ projectId, search: search || undefined, status, priority, executionState, staleDays: 14, page, pageSize }),
    [projectId, search, status, priority, executionState, page],
  );

  const { data, isLoading, isFetching } = useQuery({
    queryKey: ['testcases', queryParams],
    queryFn: () => TestCasesApi.list(queryParams),
  });

  const totalPages = data ? Math.max(1, Math.ceil(data.total / pageSize)) : 1;

  function setProject(id: string | undefined) {
    if (id) params.set('projectId', id);
    else params.delete('projectId');
    setParams(params);
    setPage(1);
  }

  function clearFilters() {
    setSearch('');
    setStatus(undefined);
    setPriority(undefined);
    setExecutionState(undefined);
    setProject(undefined);
  }

  const activeFilters = [
    search && { key: 'search', label: `"${search}"`, clear: () => setSearch('') },
    status && {
      key: 'status',
      label: status === 'Draft' ? 'پیش‌نویس' : status === 'Active' ? 'فعال' : 'منسوخ',
      clear: () => setStatus(undefined),
    },
    priority && {
      key: 'priority',
      label:
        priority === 'Low' ? 'کم' : priority === 'Medium' ? 'متوسط' : priority === 'High' ? 'بالا' : 'بحرانی',
      clear: () => setPriority(undefined),
    },
    executionState && {
      key: 'executionState',
      label:
        executionState === 'Never'
          ? 'اجرا نشده'
          : executionState === 'Stale'
            ? 'قدیمی'
            : executionState === 'Fresh'
              ? 'اجرا شده اخیر'
              : 'نیازمند اجرا',
      clear: () => setExecutionState(undefined),
    },
    projectId && {
      key: 'project',
      label: projects?.find((p) => p.id === projectId)?.code ?? projectId,
      clear: () => setProject(undefined),
    },
  ].filter(Boolean) as { key: string; label: string; clear: () => void }[];

  return (
    <div className="space-y-5">
      <PageHeader
        title="سناریوهای آزمون"
        description="کتابخانه سناریوها — قابل فیلتر، جست‌وجو و نسخه‌بندی."
        actions={
          <Button asChild>
            <Link to="/testcases/new">
              <Plus className="size-4" /> سناریوی جدید
            </Link>
          </Button>
        }
      />

      <Card>
        <CardContent className="p-4 space-y-3">
          <div className="grid grid-cols-1 md:grid-cols-12 gap-3 items-center">
            <div className="md:col-span-5 relative">
              <Search className="absolute right-3 top-1/2 -translate-y-1/2 size-3.5 text-muted-foreground/50 pointer-events-none" />
              <Input
                value={search}
                onChange={(e) => { setSearch(e.target.value); setPage(1); }}
                placeholder="جست‌وجو در عنوان یا کد سناریو…"
                className="ps-9"
              />
            </div>
            <div className="md:col-span-3">
              <Select
                value={projectId ?? ''}
                onChange={(e) => setProject(e.target.value || undefined)}
              >
                <option value="">همه پروژه‌ها</option>
                {projects?.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.code} — {p.name}
                  </option>
                ))}
              </Select>
            </div>
            <div className="md:col-span-2">
              <Select
                value={status ?? ''}
                onChange={(e) => { setStatus((e.target.value as TestCaseStatus) || undefined); setPage(1); }}
              >
                <option value="">همه وضعیت‌ها</option>
                {STATUSES.map((s) => (
                  <option key={s} value={s}>
                    {s === 'Draft' ? 'پیش‌نویس' : s === 'Active' ? 'فعال' : 'منسوخ'}
                  </option>
                ))}
              </Select>
            </div>
            <div className="md:col-span-2">
              <Select
                value={priority ?? ''}
                onChange={(e) => { setPriority((e.target.value as TestCasePriority) || undefined); setPage(1); }}
              >
                <option value="">همه اولویت‌ها</option>
                {PRIORITIES.map((p) => (
                  <option key={p} value={p}>
                    {p === 'Low' ? 'کم' : p === 'Medium' ? 'متوسط' : p === 'High' ? 'بالا' : 'بحرانی'}
                  </option>
                ))}
              </Select>
            </div>
            <div className="md:col-span-2">
              <Select
                value={executionState ?? ''}
                onChange={(e) => { setExecutionState((e.target.value as TestCaseExecutionState) || undefined); setPage(1); }}
              >
                <option value="">همه اجراها</option>
                <option value="NeedsRun">نیازمند اجرا</option>
                <option value="Never">اجرا نشده</option>
                <option value="Stale">قدیمی‌تر از ۱۴ روز</option>
                <option value="Fresh">اجرا شده اخیر</option>
              </Select>
            </div>
          </div>

          {/* Active filter chips */}
          {activeFilters.length > 0 && (
            <div className="flex flex-wrap items-center gap-2">
              {activeFilters.map((f) => (
                <div
                  key={f.key}
                  className="flex items-center gap-1 text-xs bg-primary/8 text-primary rounded-full px-2.5 py-1"
                >
                  {f.label}
                  <button
                    onClick={f.clear}
                    className="hover:text-primary/70 transition-base"
                  >
                    <X className="size-3" />
                  </button>
                </div>
              ))}
              <button
                onClick={clearFilters}
                className="text-xs text-muted-foreground hover:text-foreground transition-base"
              >
                پاک کردن همه
              </button>
            </div>
          )}
        </CardContent>
      </Card>

      <Card>
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-border bg-muted/30 text-muted-foreground text-xs">
                <th className="text-start font-medium px-4 py-3 w-20">کد</th>
                <th className="text-start font-medium px-4 py-3">عنوان</th>
                <th className="text-start font-medium px-4 py-3 w-24">وضعیت</th>
                <th className="text-start font-medium px-4 py-3 w-24">اولویت</th>
                <th className="text-start font-medium px-4 py-3 w-36">اضافه‌کننده</th>
                <th className="text-start font-medium px-4 py-3">برچسب‌ها</th>
                <th className="text-start font-medium px-4 py-3 w-44">آخرین اجرا</th>
                <th className="text-start font-medium px-4 py-3 w-28">ویرایش</th>
                <th className="text-start font-medium px-4 py-3 w-16">نسخه</th>
                <th className="w-10" />
              </tr>
            </thead>
            <tbody>
              {isLoading ? (
                Array.from({ length: 6 }).map((_, i) => <TableRowSkeleton key={i} cols={10} />)
              ) : !data || data.items.length === 0 ? (
                <tr>
                  <td colSpan={10}>
                    <EmptyState
                      icon={<FlaskConical className="size-7" />}
                      title="سناریویی یافت نشد"
                      description="ممکن است با تغییر فیلترها یا ساخت سناریوی جدید نتیجه بگیرید."
                      action={
                        <Button asChild>
                          <Link to="/testcases/new">
                            <Plus className="size-4" /> سناریوی جدید
                          </Link>
                        </Button>
                      }
                    />
                  </td>
                </tr>
              ) : (
                data.items.map((tc) => (
                  <motion.tr
                    key={tc.id}
                    initial={{ opacity: 0 }}
                    animate={{ opacity: 1 }}
                    className="group border-b border-border hover:bg-accent/30 transition-base cursor-pointer"
                  >
                    <td className="px-4 py-3 font-mono text-xs text-muted-foreground">{tc.code}</td>
                    <td className="px-4 py-3">
                      <Link
                        to={`/testcases/${tc.id}`}
                        className="font-medium hover:text-primary transition-base"
                      >
                        {tc.title}
                      </Link>
                    </td>
                    <td className="px-4 py-3">
                      <TestCaseStatusBadge status={tc.status} />
                    </td>
                    <td className="px-4 py-3">
                      <PriorityBadge priority={tc.priority} />
                    </td>
                    <td className="px-4 py-3">
                      <ScenarioAuthor userId={tc.createdByUserId} />
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex flex-wrap gap-1">
                        {tc.tags.slice(0, 3).map((t) => (
                          <Badge key={t} variant="secondary" size="sm">
                            #{t}
                          </Badge>
                        ))}
                        {tc.tags.length > 3 && (
                          <Badge variant="muted" size="sm">
                            +{formatNumber(tc.tags.length - 3)}
                          </Badge>
                        )}
                      </div>
                    </td>
                    <td className="px-4 py-3">
                      <ExecutionSummary
                        compact
                        lastExecutedAtUtc={tc.lastExecutedAtUtc}
                        status={tc.lastExecutionStatus}
                        runId={tc.lastExecutionRunId}
                      />
                    </td>
                    <td className="px-4 py-3 text-xs text-muted-foreground">
                      {fmtDate(tc.updatedAtUtc ?? tc.createdAtUtc)}
                    </td>
                    <td className="px-4 py-3 num-fa">
                      {tc.version > 1 && (
                        <Badge variant="muted" size="sm">
                          v{formatNumber(tc.version)}
                        </Badge>
                      )}
                    </td>
                    <td className="px-2 py-3">
                      <Link
                        to={`/testcases/${tc.id}/edit`}
                        className="opacity-0 group-hover:opacity-100 transition-base p-1 rounded hover:bg-muted text-muted-foreground hover:text-foreground inline-flex"
                        title="ویرایش"
                      >
                        <Pencil className="size-3.5" />
                      </Link>
                    </td>
                  </motion.tr>
                ))
              )}
            </tbody>
          </table>
        </div>

        {data && data.total > pageSize && (
          <div className="flex items-center justify-between px-4 py-3 border-t border-border text-sm">
            <div className="text-muted-foreground text-xs num-fa">
              {formatNumber(data.total)} سناریو · صفحه {formatNumber(page)} از{' '}
              {formatNumber(totalPages)}
            </div>
            <div className="flex items-center gap-1">
              <Button
                variant="outline"
                size="icon-sm"
                disabled={page <= 1 || isFetching}
                onClick={() => setPage((p) => Math.max(1, p - 1))}
              >
                <ChevronRight className="size-4" />
              </Button>
              <span className="px-2 text-xs text-muted-foreground num-fa">
                {formatNumber(page)}/{formatNumber(totalPages)}
              </span>
              <Button
                variant="outline"
                size="icon-sm"
                disabled={page >= totalPages || isFetching}
                onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
              >
                <ChevronLeft className="size-4" />
              </Button>
            </div>
          </div>
        )}
      </Card>
    </div>
  );
}
