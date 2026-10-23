import { useQuery } from '@tanstack/react-query';
import { ReportingApi } from '@/lib/queries/endpoints';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { StatCardSkeleton, Skeleton, CardSkeleton } from '@/components/common/Skeletons';
import { PageHeader } from '@/components/common/PageHeader';
import { EmptyState } from '@/components/common/EmptyState';
import { StatsCard } from '@/components/common/StatsCard';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Link } from 'react-router-dom';
import { fmtRelative, fmtDate } from '@/lib/date';
import { formatNumber, formatPercent } from '@/lib/utils';
import {
  Activity,
  ListChecks,
  TrendingUp,
  ArrowLeft,
  PlayCircle,
  FolderKanban,
  Clock,
  AlertTriangle,
  CheckCircle2,
} from 'lucide-react';
import {
  ResponsiveContainer,
  AreaChart,
  Area,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  PieChart,
  Pie,
  Cell,
  Legend,
} from 'recharts';
import { motion } from 'framer-motion';
import type { StatusBreakdown } from '@/lib/types';
import { RunStatusBadge } from '@/components/common/StatusBadges';
import { useAuthStore } from '@/lib/auth/store';

const STATUS_COLORS: Record<keyof Omit<StatusBreakdown, 'total' | 'executed' | 'passRate'>, string> = {
  passed: 'var(--chart-2)',
  failed: 'var(--chart-3)',
  blocked: 'var(--chart-4)',
  retest: 'var(--chart-5)',
  skipped: 'var(--muted-foreground)',
  untested: 'var(--border)',
};
const STATUS_LABEL: Record<keyof Omit<StatusBreakdown, 'total' | 'executed' | 'passRate'>, string> = {
  passed: 'موفق',
  failed: 'ناموفق',
  blocked: 'مسدود',
  retest: 'تست مجدد',
  skipped: 'رد شده',
  untested: 'تست نشده',
};

function StatusPie({ counts }: { counts: StatusBreakdown }) {
  const data = (['passed', 'failed', 'blocked', 'retest', 'skipped', 'untested'] as const)
    .map((k) => ({ name: STATUS_LABEL[k], value: counts[k], color: STATUS_COLORS[k] }))
    .filter((d) => d.value > 0);

  if (data.length === 0) {
    return (
      <EmptyState
        icon={<ListChecks className="size-7" />}
        title="هنوز نتیجه‌ای ثبت نشده"
        description="برای دیدن توزیع وضعیت، یک اجرا بسازید."
      />
    );
  }

  return (
    <ResponsiveContainer width="100%" height={240}>
      <PieChart>
        <Pie
          data={data}
          dataKey="value"
          nameKey="name"
          innerRadius={55}
          outerRadius={88}
          paddingAngle={3}
          stroke="none"
        >
          {data.map((d, i) => (
            <Cell key={i} fill={d.color} />
          ))}
        </Pie>
        <Tooltip
          contentStyle={{
            background: 'hsl(var(--popover))',
            border: '1px solid hsl(var(--border))',
            borderRadius: 10,
            fontFamily: 'Vazirmatn',
            fontSize: 12,
          }}
        />
        <Legend
          formatter={(v) => <span style={{ fontFamily: 'Vazirmatn', fontSize: 11 }}>{v}</span>}
          iconType="circle"
          iconSize={8}
        />
      </PieChart>
    </ResponsiveContainer>
  );
}

function TrendChart({ projectId }: { projectId?: string }) {
  const { data, isLoading } = useQuery({
    queryKey: ['trends', projectId, 'day'],
    queryFn: () => ReportingApi.trends({ granularity: 'day', projectId }),
  });
  if (isLoading) return <Skeleton className="h-60 w-full rounded-lg" />;
  if (!data || data.length === 0)
    return (
      <EmptyState
        icon={<TrendingUp className="size-7" />}
        title="داده‌ای برای روند موجود نیست"
        description="پس از ثبت نتایج، روند روزانه نمایش داده می‌شود."
      />
    );

  const chartData = data.map((b) => ({
    date: fmtDate(b.bucketStartUtc, 'MM/dd'),
    موفق: b.passed,
    ناموفق: b.failed,
    مسدود: b.blocked,
  }));

  return (
    <ResponsiveContainer width="100%" height={240}>
      <AreaChart data={chartData} margin={{ top: 10, right: 0, left: -20, bottom: 0 }}>
        <defs>
          <linearGradient id="gPass" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor="var(--chart-2)" stopOpacity={0.3} />
            <stop offset="100%" stopColor="var(--chart-2)" stopOpacity={0} />
          </linearGradient>
          <linearGradient id="gFail" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor="var(--chart-3)" stopOpacity={0.3} />
            <stop offset="100%" stopColor="var(--chart-3)" stopOpacity={0} />
          </linearGradient>
          <linearGradient id="gBlock" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor="var(--chart-4)" stopOpacity={0.3} />
            <stop offset="100%" stopColor="var(--chart-4)" stopOpacity={0} />
          </linearGradient>
        </defs>
        <CartesianGrid strokeDasharray="3 3" stroke="hsl(var(--border))" vertical={false} />
        <XAxis
          dataKey="date"
          tick={{ fontFamily: 'Vazirmatn', fontSize: 11 }}
          tickLine={false}
          axisLine={false}
        />
        <YAxis
          tick={{ fontFamily: 'Vazirmatn', fontSize: 11 }}
          tickLine={false}
          axisLine={false}
        />
        <Tooltip
          contentStyle={{
            background: 'hsl(var(--popover))',
            border: '1px solid hsl(var(--border))',
            borderRadius: 10,
            fontFamily: 'Vazirmatn',
            fontSize: 12,
          }}
        />
        <Legend
          formatter={(v) => <span style={{ fontFamily: 'Vazirmatn', fontSize: 11 }}>{v}</span>}
          iconSize={8}
        />
        <Area type="monotone" dataKey="موفق" stroke="var(--chart-2)" strokeWidth={2} fill="url(#gPass)" />
        <Area type="monotone" dataKey="ناموفق" stroke="var(--chart-3)" strokeWidth={2} fill="url(#gFail)" />
        <Area type="monotone" dataKey="مسدود" stroke="var(--chart-4)" strokeWidth={2} fill="url(#gBlock)" />
      </AreaChart>
    </ResponsiveContainer>
  );
}

export default function DashboardPage() {
  const user = useAuthStore((s) => s.user);

  const { data, isLoading, error } = useQuery({
    queryKey: ['workspace-dashboard'],
    queryFn: ReportingApi.workspaceDashboard,
  });

  if (isLoading) {
    return (
      <div className="space-y-6">
        <Skeleton className="h-8 w-52" />
        <Skeleton className="h-16 w-full rounded-xl" />
        <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
          {Array.from({ length: 4 }).map((_, i) => (
            <StatCardSkeleton key={i} />
          ))}
        </div>
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
          <CardSkeleton />
          <CardSkeleton />
          <CardSkeleton />
        </div>
      </div>
    );
  }

  if (error || !data) {
    return (
      <EmptyState
        icon={<AlertTriangle className="size-7" />}
        title="خطا در بارگذاری داشبورد"
        description="ارتباط با سرویس گزارش‌گیری برقرار نشد."
      />
    );
  }

  const activeProjects = data.projects.filter((p) => !p.isArchived);

  return (
    <motion.div
      initial={{ opacity: 0, y: 8 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.3 }}
      className="space-y-6"
    >
      <PageHeader
        title="داشبورد فضای کاری"
        description="نمای کلی پروژه‌ها، اجراها و سلامت آزمون‌ها در یک نگاه."
        actions={
          <Button asChild>
            <Link to="/runs">
              <PlayCircle className="size-4" /> اجراهای فعال
            </Link>
          </Button>
        }
      />

      {/* Welcome strip */}
      <div className="flex items-center justify-between p-4 rounded-xl bg-gradient-to-l from-primary/6 to-transparent border border-border/60">
        <div>
          <p className="text-sm font-medium text-foreground">
            خوش آمدید{user?.displayName ? `، ${user.displayName}` : ''}!
          </p>
          <p className="text-xs text-muted-foreground mt-0.5">
            {formatNumber(data.openRuns)} اجرای باز در انتظار رسیدگی
          </p>
        </div>
        <Button size="sm" variant="outline" asChild>
          <Link to="/runs">
            <PlayCircle className="size-3.5" /> اجرای جدید
          </Link>
        </Button>
      </div>

      {/* KPIs */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
        <StatsCard
          icon={FolderKanban}
          label="پروژه‌ها"
          value={formatNumber(data.totalProjects)}
          variant="info"
        />
        <StatsCard
          icon={PlayCircle}
          label="اجراها"
          value={formatNumber(data.totalRuns)}
          hint={`${formatNumber(data.openRuns)} باز`}
          variant="default"
        />
        <StatsCard
          icon={ListChecks}
          label="سناریوها"
          value={formatNumber(data.totalTestCases)}
          variant="warning"
        />
        <StatsCard
          icon={Activity}
          label="نرخ موفقیت"
          value={formatPercent(data.counts.passRate)}
          hint={`${formatNumber(data.recordedResults)} نتیجه ثبت‌شده`}
          variant={
            data.counts.passRate >= 80
              ? 'success'
              : data.counts.passRate >= 50
                ? 'warning'
                : 'destructive'
          }
        />
      </div>

      {/* Charts */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <TrendingUp className="size-4 text-primary" />
              روند روزانه نتایج
            </CardTitle>
            <CardDescription>تعداد نتایج ثبت‌شده در روزهای اخیر</CardDescription>
          </CardHeader>
          <CardContent>
            <TrendChart />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <CheckCircle2 className="size-4 text-success" />
              توزیع وضعیت‌ها
            </CardTitle>
            <CardDescription>سهم هر وضعیت از کل نتایج</CardDescription>
          </CardHeader>
          <CardContent>
            <StatusPie counts={data.counts} />
          </CardContent>
        </Card>
      </div>

      {/* Lists */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
        <Card className="lg:col-span-2">
          <CardHeader className="flex flex-row items-center justify-between">
            <div>
              <CardTitle>اجراهای اخیر</CardTitle>
              <CardDescription>پنج اجرای آخر در فضای کاری</CardDescription>
            </div>
            <Button variant="ghost" size="sm" asChild>
              <Link to="/runs">
                همه <ArrowLeft className="size-3" />
              </Link>
            </Button>
          </CardHeader>
          <CardContent className="px-0 pt-0">
            {data.recentRuns.length === 0 ? (
              <EmptyState
                icon={<Clock className="size-7" />}
                title="اجرایی ثبت نشده"
                description="اولین اجرای آزمون خود را شروع کنید."
                action={
                  <Button asChild>
                    <Link to="/runs">شروع اجرا</Link>
                  </Button>
                }
              />
            ) : (
              <ul className="divide-y divide-border">
                {data.recentRuns.map((r) => (
                  <li key={r.runId} className="group hover:bg-accent/40 transition-base">
                    <Link to={`/runs/${r.runId}`} className="flex items-center gap-4 px-5 py-3.5">
                      <div className="size-9 rounded-lg bg-primary/8 text-primary flex items-center justify-center font-mono text-xs shrink-0 font-medium">
                        {r.code}
                      </div>
                      <div className="flex-1 min-w-0">
                        <div className="text-sm font-medium truncate">{r.name}</div>
                        <div className="text-xs text-muted-foreground mt-0.5 flex items-center gap-2">
                          <Badge variant="muted" size="sm">
                            {r.projectCode}
                          </Badge>
                          <span>{fmtRelative(r.createdAtUtc)}</span>
                        </div>
                      </div>
                      <div className="shrink-0 text-end">
                        {/* Mini pass rate bar */}
                        <div className="w-16 h-1.5 rounded-full bg-muted overflow-hidden mb-1">
                          <div
                            className="h-full rounded-full bg-success transition-all"
                            style={{ width: `${Math.min(100, r.passRate)}%` }}
                          />
                        </div>
                        <div className="text-xs text-muted-foreground">{formatPercent(r.passRate)}</div>
                      </div>
                      <RunStatusBadge status={r.status} />
                    </Link>
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>پروژه‌ها</CardTitle>
            <CardDescription>وضعیت پروژه‌های فضای کاری</CardDescription>
          </CardHeader>
          <CardContent className="px-0 pt-0">
            {activeProjects.length === 0 ? (
              <EmptyState
                icon={<FolderKanban className="size-7" />}
                title="پروژه فعالی نیست"
                description="پروژه‌های آرشیوشده در این فهرست نمایش داده نمی‌شوند."
              />
            ) : (
              <ul className="divide-y divide-border">
                {activeProjects.map((p) => (
                  <li key={p.projectId} className="hover:bg-accent/40 transition-base">
                    <Link to={`/projects/${p.projectId}`} className="flex items-center gap-3 px-5 py-3.5">
                      <div className="size-9 rounded-lg bg-gradient-to-br from-primary/70 to-info text-white text-xs font-bold flex items-center justify-center shrink-0">
                        {p.code.slice(0, 2)}
                      </div>
                      <div className="flex-1 min-w-0">
                        <div className="text-sm font-medium truncate">{p.name}</div>
                        <div className="text-xs text-muted-foreground num-fa">
                          {formatNumber(p.totalRuns)} اجرا · {formatNumber(p.testCases)} سناریو
                        </div>
                      </div>
                      <Badge
                        variant={
                          p.passRate >= 80 ? 'success' : p.passRate >= 50 ? 'warning' : 'muted'
                        }
                        size="sm"
                      >
                        {formatPercent(p.passRate)}
                      </Badge>
                    </Link>
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>
      </div>
    </motion.div>
  );
}
