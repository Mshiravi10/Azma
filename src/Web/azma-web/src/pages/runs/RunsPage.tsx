import { useState, useMemo } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { RunsApi, ProjectsApi } from '@/lib/queries/endpoints';
import { PageHeader } from '@/components/common/PageHeader';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Skeleton, TableRowSkeleton } from '@/components/common/Skeletons';
import { EmptyState } from '@/components/common/EmptyState';
import { RunStatusBadge } from '@/components/common/StatusBadges';
import { StatsCard } from '@/components/common/StatsCard';
import { Plus, Search, PlayCircle, ChevronLeft, ChevronRight, ListChecks, CheckCircle2, Activity, CalendarClock } from 'lucide-react';
import { Select } from '@/components/ui/native-select';
import { fmtRelative } from '@/lib/date';
import { formatNumber } from '@/lib/utils';
import { motion } from 'framer-motion';
import NewRunDialog from './NewRunDialog';

export default function RunsPage() {
  const [params, setParams] = useSearchParams();
  const projectId = params.get('projectId') ?? undefined;
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState<'Open' | 'Closed' | undefined>();
  const [page, setPage] = useState(1);
  const [showNew, setShowNew] = useState(false);
  const pageSize = 20;

  const { data: projects } = useQuery({ queryKey: ['projects'], queryFn: () => ProjectsApi.list() });

  const queryParams = useMemo(
    () => ({ projectId, search: search || undefined, status, page, pageSize }),
    [projectId, search, status, page],
  );
  const { data, isLoading } = useQuery({
    queryKey: ['runs', queryParams],
    queryFn: () => RunsApi.list(queryParams),
  });

  const totalPages = data ? Math.max(1, Math.ceil(data.total / pageSize)) : 1;

  const pageStats = useMemo(() => {
    if (!data) return null;
    const open = data.items.filter(r => r.status === 'Open').length;
    const closed = data.items.filter(r => r.status === 'Closed').length;
    const totalCases = data.items.reduce((sum, r) => sum + r.caseCount, 0);
    return { total: data.total, open, closed, totalCases };
  }, [data]);

  function setProject(id: string | undefined) {
    if (id) params.set('projectId', id);
    else params.delete('projectId');
    setParams(params);
    setPage(1);
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="اجراهای آزمون"
        description="گردش‌های اجرای سناریوها — هر اجرا پیشرفت لحظه‌ای و گزارش مستقل دارد."
        actions={
          <>
            <Button variant="outline" asChild>
              <Link to="/run-schedules">
                <CalendarClock className="size-4" /> زمان بندی
              </Link>
            </Button>
            <Button onClick={() => setShowNew(true)}>
              <Plus className="size-4" /> اجرای جدید
            </Button>
          </>
        }
      />

      <Card>
        <CardContent className="p-4">
          <div className="grid grid-cols-1 md:grid-cols-12 gap-3">
            <div className="md:col-span-6">
              <div className="relative">
                <Search className="absolute right-3 top-1/2 -translate-y-1/2 size-4 text-muted-foreground pointer-events-none" />
                <Input
                  value={search}
                  onChange={(e) => {
                    setSearch(e.target.value);
                    setPage(1);
                  }}
                  placeholder="جست‌و‌جو در نام یا کد اجرا…"
                  className="ps-10"
                />
              </div>
            </div>
            <div className="md:col-span-3">
              <Select
                value={projectId ?? ''}
                onChange={(e) => setProject(e.target.value || undefined)}
              >
                <option value="">همه پروژه‌ها</option>
                {projects?.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.code}
                  </option>
                ))}
              </Select>
            </div>
            <div className="md:col-span-3">
              <Select
                value={status ?? ''}
                onChange={(e) => {
                  setStatus((e.target.value as 'Open' | 'Closed') || undefined);
                  setPage(1);
                }}
              >
                <option value="">همه وضعیت‌ها</option>
                <option value="Open">باز</option>
                <option value="Closed">بسته</option>
              </Select>
            </div>
          </div>
        </CardContent>
      </Card>

      {pageStats && (
        <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
          <StatsCard icon={ListChecks} label="مجموع اجراها" value={formatNumber(pageStats.total)} variant="default" />
          <StatsCard icon={PlayCircle} label="باز" value={formatNumber(pageStats.open)} variant="success" />
          <StatsCard icon={CheckCircle2} label="بسته" value={formatNumber(pageStats.closed)} variant="muted" />
          <StatsCard icon={Activity} label="سناریوها" value={formatNumber(pageStats.totalCases)} variant="info" />
        </div>
      )}

      <Card>
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="border-b border-border bg-muted/40 text-muted-foreground text-xs">
              <tr>
                <th className="text-start font-medium px-4 py-3">کد</th>
                <th className="text-start font-medium px-4 py-3">نام اجرا</th>
                <th className="text-start font-medium px-4 py-3">پروژه</th>
                <th className="text-start font-medium px-4 py-3">وضعیت</th>
                <th className="text-start font-medium px-4 py-3">سناریوها</th>
                <th className="text-start font-medium px-4 py-3">پیشرفت</th>
                <th className="text-start font-medium px-4 py-3">ایجاد</th>
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
                      icon={<PlayCircle className="size-7" />}
                      title="اجرایی یافت نشد"
                      action={
                        <Button onClick={() => setShowNew(true)}>
                          <Plus className="size-4" /> اجرای جدید
                        </Button>
                      }
                    />
                  </td>
                </tr>
              ) : (
                data.items.map((r) => (
                  <motion.tr
                    key={r.id}
                    initial={{ opacity: 0 }}
                    animate={{ opacity: 1 }}
                    className="group border-b border-border hover:bg-accent/30 transition-base cursor-pointer"
                  >
                    <td className="px-4 py-3 font-mono text-xs text-muted-foreground">{r.code}</td>
                    <td className="px-4 py-3">
                      <Link to={`/runs/${r.id}`} className="font-medium hover:text-primary">
                        {r.name}
                      </Link>
                    </td>
                    <td className="px-4 py-3"><Badge variant="muted">{r.projectCode}</Badge></td>
                    <td className="px-4 py-3"><RunStatusBadge status={r.status} /></td>
                    <td className="px-4 py-3 num-fa">{formatNumber(r.caseCount)}</td>
                    <td className="px-4 py-3">
                      <div className="w-20 h-1.5 rounded-full bg-muted overflow-hidden">
                        {r.status === 'Closed' ? (
                          <div className="h-full bg-success w-full" />
                        ) : (
                          <div className="h-full bg-primary/40 w-1/3" />
                        )}
                      </div>
                    </td>
                    <td className="px-4 py-3 text-xs text-muted-foreground">
                      <div>{fmtRelative(r.createdAtUtc)}</div>
                      {r.closedAtUtc && (
                        <div className="text-[10px] text-muted-foreground/60 mt-0.5">
                          بسته: {fmtRelative(r.closedAtUtc)}
                        </div>
                      )}
                    </td>
                    <td className="px-4 py-3 text-end">
                      <Button size="sm" variant="outline" asChild>
                        <Link to={`/runs/${r.id}/execute`}>
                          <PlayCircle className="size-3.5" /> اجرا
                        </Link>
                      </Button>
                    </td>
                  </motion.tr>
                ))
              )}
            </tbody>
          </table>
        </div>

        {data && data.total > pageSize && (
          <div className="flex items-center justify-between px-4 py-3 border-t border-border text-sm">
            <div className="text-muted-foreground num-fa">
              مجموع {formatNumber(data.total)} مورد · صفحه {formatNumber(page)} از {formatNumber(totalPages)}
            </div>
            <div className="flex items-center gap-1">
              <Button variant="outline" size="icon-sm" disabled={page <= 1} onClick={() => setPage(page - 1)}>
                <ChevronRight className="size-4" />
              </Button>
              <Button variant="outline" size="icon-sm" disabled={page >= totalPages} onClick={() => setPage(page + 1)}>
                <ChevronLeft className="size-4" />
              </Button>
            </div>
          </div>
        )}
      </Card>

      {showNew && <NewRunDialog onClose={() => setShowNew(false)} />}
    </div>
  );
}
