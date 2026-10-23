import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { Bell, CheckCheck } from 'lucide-react';
import { ReportingApi } from '@/lib/queries/endpoints';
import { fmtRelative } from '@/lib/date';
import { formatNumber, formatPercent } from '@/lib/utils';
import type { TopFailingTest, WorkspaceDashboard } from '@/lib/types';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Empty,
  EmptyDescription,
  EmptyHeader,
  EmptyMedia,
  EmptyTitle,
} from '@/components/ui/empty';
import {
  Popover,
  PopoverContent,
  PopoverDescription,
  PopoverHeader,
  PopoverTitle,
  PopoverTrigger,
} from '@/components/ui/popover';
import { ScrollArea } from '@/components/ui/scroll-area';
import { Separator } from '@/components/ui/separator';
import { Skeleton } from '@/components/ui/skeleton';

const READ_KEY = 'azma.notifications.read';

type NoticeVariant = 'destructive' | 'warning' | 'success';

interface Notice {
  id: string;
  title: string;
  description: string;
  href: string;
  at?: string;
  badge: string;
  variant: NoticeVariant;
}

function loadRead(): string[] {
  try {
    const raw = localStorage.getItem(READ_KEY);
    const parsed = raw ? JSON.parse(raw) : [];
    return Array.isArray(parsed) ? parsed.filter((item) => typeof item === 'string') : [];
  } catch {
    return [];
  }
}

function saveRead(ids: string[]) {
  localStorage.setItem(READ_KEY, JSON.stringify(ids.slice(-200)));
}

function buildNotices(
  dashboard: WorkspaceDashboard | undefined,
  failures: TopFailingTest[] | undefined,
): Notice[] {
  const notices: Notice[] = [];

  for (const run of dashboard?.recentRuns ?? []) {
    if (run.failed > 0) {
      notices.push({
        id: `run-failed-${run.runId}`,
        title: run.name,
        description: `${formatNumber(run.failed)} سناریو ناموفق از ${formatNumber(run.caseCount)} · ${run.projectCode}`,
        href: `/runs/${run.runId}`,
        at: run.completedAtUtc ?? run.createdAtUtc,
        badge: 'ناموفق',
        variant: 'destructive',
      });
      continue;
    }
    if (run.status === 'Open') {
      notices.push({
        id: `run-open-${run.runId}`,
        title: run.name,
        description: `${formatNumber(run.caseCount)} سناریو · ${run.projectCode} · هنوز باز است`,
        href: `/runs/${run.runId}`,
        at: run.createdAtUtc,
        badge: 'باز',
        variant: 'warning',
      });
      continue;
    }
    notices.push({
      id: `run-done-${run.runId}`,
      title: run.name,
      description: `نرخ موفقیت ${formatPercent(run.passRate)} · ${run.projectCode}`,
      href: `/runs/${run.runId}`,
      at: run.completedAtUtc ?? run.createdAtUtc,
      badge: 'تکمیل',
      variant: 'success',
    });
  }

  for (const failure of failures ?? []) {
    if (failure.failures <= 0) continue;
    notices.push({
      id: `case-fail-${failure.testCaseId}`,
      title: failure.title,
      description: `${failure.code} · ${formatNumber(failure.failures)} شکست از ${formatNumber(failure.executions)} اجرا`,
      href: `/testcases/${failure.testCaseId}`,
      badge: 'پرتکرار',
      variant: 'warning',
    });
  }

  const rank: Record<NoticeVariant, number> = { destructive: 0, warning: 1, success: 2 };
  return notices.sort((a, b) => rank[a.variant] - rank[b.variant] || (b.at ?? '').localeCompare(a.at ?? ''));
}

export function NotificationsMenu() {
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);
  const [read, setRead] = useState<string[]>(() => loadRead());

  const dashboardQuery = useQuery({
    queryKey: ['notifications', 'dashboard'],
    queryFn: () => ReportingApi.workspaceDashboard(),
    refetchInterval: 60_000,
  });
  const failuresQuery = useQuery({
    queryKey: ['notifications', 'failures'],
    queryFn: () => ReportingApi.topFailures({ days: 14, limit: 5 }),
    refetchInterval: 60_000,
  });

  const notices = useMemo(
    () => buildNotices(dashboardQuery.data, failuresQuery.data),
    [dashboardQuery.data, failuresQuery.data],
  );
  const readSet = useMemo(() => new Set(read), [read]);
  const unread = notices.filter((notice) => !readSet.has(notice.id)).length;
  const loading = dashboardQuery.isLoading || failuresQuery.isLoading;
  const failed = dashboardQuery.isError && failuresQuery.isError;

  function mark(ids: string[]) {
    const next = Array.from(new Set([...read, ...ids]));
    setRead(next);
    saveRead(next);
  }

  function openNotice(notice: Notice) {
    mark([notice.id]);
    setOpen(false);
    navigate(notice.href);
  }

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button
          variant="ghost"
          size="icon"
          title="اعلان‌ها"
          aria-label={unread > 0 ? `${formatNumber(unread)} اعلان خوانده‌نشده` : 'اعلان‌ها'}
          className="relative"
        >
          <Bell />
          {unread > 0 ? (
            <Badge
              size="sm"
              className="absolute -top-1 -end-1 min-w-4 px-1"
            >
              {unread > 9 ? `${formatNumber(9)}+` : formatNumber(unread)}
            </Badge>
          ) : null}
        </Button>
      </PopoverTrigger>
      <PopoverContent align="end" className="w-96 max-w-[calc(100vw-2rem)] gap-0 p-0">
        <PopoverHeader className="flex-row items-center justify-between gap-2 px-3 py-2.5">
          <div className="flex flex-col gap-0.5">
            <PopoverTitle>اعلان‌ها</PopoverTitle>
            <PopoverDescription>
              {unread > 0 ? `${formatNumber(unread)} خوانده‌نشده` : 'فعالیت اخیر فضای کاری'}
            </PopoverDescription>
          </div>
          {unread > 0 ? (
            <Button
              variant="ghost"
              size="sm"
              onClick={() => mark(notices.map((notice) => notice.id))}
            >
              <CheckCheck data-icon="inline-start" />
              خواندم
            </Button>
          ) : null}
        </PopoverHeader>
        <Separator />

        {loading ? (
          <div className="flex flex-col gap-2 p-3">
            <Skeleton className="h-12 w-full" />
            <Skeleton className="h-12 w-full" />
            <Skeleton className="h-12 w-full" />
          </div>
        ) : failed ? (
          <Empty className="border-0 py-8">
            <EmptyHeader>
              <EmptyMedia variant="icon">
                <Bell />
              </EmptyMedia>
              <EmptyTitle>اعلان‌ها بارگذاری نشد</EmptyTitle>
              <EmptyDescription>اتصال به سرویس گزارش را بررسی کنید.</EmptyDescription>
            </EmptyHeader>
          </Empty>
        ) : notices.length === 0 ? (
          <Empty className="border-0 py-8">
            <EmptyHeader>
              <EmptyMedia variant="icon">
                <Bell />
              </EmptyMedia>
              <EmptyTitle>اعلانی نیست</EmptyTitle>
              <EmptyDescription>وقتی اجرایی ناموفق شود یا باز بماند، اینجا دیده می‌شود.</EmptyDescription>
            </EmptyHeader>
          </Empty>
        ) : (
          <ScrollArea className="[&>[data-slot=scroll-area-viewport]]:max-h-80">
            <ul className="flex flex-col">
              {notices.map((notice) => {
                const seen = readSet.has(notice.id);
                return (
                  <li key={notice.id}>
                    <button
                      type="button"
                      onClick={() => openNotice(notice)}
                      className="flex w-full items-start gap-2 px-3 py-2.5 text-start outline-none hover:bg-muted focus-visible:bg-muted"
                    >
                      <span
                        className={
                          seen
                            ? 'mt-1.5 size-2 shrink-0 rounded-full bg-transparent'
                            : 'mt-1.5 size-2 shrink-0 rounded-full bg-primary'
                        }
                      />
                      <span className="flex min-w-0 flex-1 flex-col gap-1">
                        <span className="flex items-center gap-2">
                          <span className="min-w-0 flex-1 truncate text-sm font-medium">{notice.title}</span>
                          <Badge variant={notice.variant} size="sm">
                            {notice.badge}
                          </Badge>
                        </span>
                        <span className="truncate text-xs text-muted-foreground">{notice.description}</span>
                        {notice.at ? (
                          <span className="text-xs text-muted-foreground">{fmtRelative(notice.at)}</span>
                        ) : null}
                      </span>
                    </button>
                  </li>
                );
              })}
            </ul>
          </ScrollArea>
        )}
      </PopoverContent>
    </Popover>
  );
}
