import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { ProjectsApi, ReportingApi } from '@/lib/queries/endpoints';
import { PageHeader } from '@/components/common/PageHeader';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { Select } from '@/components/ui/native-select';
import { Skeleton } from '@/components/common/Skeletons';
import { EmptyState } from '@/components/common/EmptyState';
import { Badge } from '@/components/ui/badge';
import { TrendingUp, AlertTriangle, Users, Trophy, Activity, CheckCircle2, XCircle } from 'lucide-react';
import {
  ResponsiveContainer,
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  Legend,
} from 'recharts';
import { fmtDate } from '@/lib/date';
import { formatNumber, formatPercent, initials } from '@/lib/utils';
import { motion } from 'framer-motion';
import { StatsCard } from '@/components/common/StatsCard';
import { StatCardSkeleton } from '@/components/common/Skeletons';

type Granularity = 'day' | 'week' | 'month';
const GRANULARITY_OPTS: { value: Granularity; label: string }[] = [
  { value: 'day', label: 'روزانه' },
  { value: 'week', label: 'هفتگی' },
  { value: 'month', label: 'ماهانه' },
];
const DAYS_OPTS = [
  { value: 7, label: '۷ روز' },
  { value: 30, label: '۳۰ روز' },
  { value: 90, label: '۹۰ روز' },
  { value: 180, label: '۱۸۰ روز' },
];

export default function ReportsPage() {
  const [projectId, setProjectId] = useState<string>('');
  const [granularity, setGranularity] = useState<Granularity>('day');
  const [days, setDays] = useState(30);

  const { data: projects } = useQuery({
    queryKey: ['projects'],
    queryFn: () => ProjectsApi.list(),
  });

  const { data: trends, isLoading: tl } = useQuery({
    queryKey: ['report-trends', projectId, granularity],
    queryFn: () => ReportingApi.trends({ projectId: projectId || undefined, granularity }),
  });
  const { data: topFails, isLoading: ttl } = useQuery({
    queryKey: ['report-topfails', projectId, days],
    queryFn: () =>
      ReportingApi.topFailures({ projectId: projectId || undefined, days, limit: 10 }),
  });
  const { data: users, isLoading: ul } = useQuery({
    queryKey: ['report-users', projectId, days],
    queryFn: () => ReportingApi.users({ projectId: projectId || undefined, days, limit: 25 }),
  });

  const chartData = (trends ?? []).map((b) => ({
    date:
      granularity === 'month'
        ? fmtDate(b.bucketStartUtc, 'yyyy/MM')
        : fmtDate(b.bucketStartUtc, 'MM/dd'),
    موفق: b.passed,
    ناموفق: b.failed,
    مسدود: b.blocked,
    'تست مجدد': b.retest,
    'رد شده': b.skipped,
  }));

  const totalExecs = (trends ?? []).reduce(
    (s, b) => s + b.passed + b.failed + b.blocked + b.retest + b.skipped,
    0,
  );
  const totalPassed = (trends ?? []).reduce((s, b) => s + b.passed, 0);
  const totalFailed = (trends ?? []).reduce((s, b) => s + b.failed, 0);
  const overallPassRate = totalExecs > 0 ? (totalPassed * 100) / totalExecs : 0;

  return (
    <motion.div
      initial={{ opacity: 0, y: 6 }}
      animate={{ opacity: 1, y: 0 }}
      className="space-y-6"
    >
      <PageHeader
        title="گزارش‌ها و تحلیل‌ها"
        description="روند زمانی، سناریوهای پرشکست و فعالیت کاربران را تحلیل کنید."
      />

      {/* KPI strip */}
      {tl ? (
        <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
          {Array.from({ length: 4 }).map((_, i) => <StatCardSkeleton key={i} />)}
        </div>
      ) : (
        <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
          <StatsCard icon={Activity} label="کل اجراها" value={formatNumber(totalExecs)} variant="default" />
          <StatsCard
            icon={CheckCircle2}
            label="نرخ موفقیت"
            value={formatPercent(overallPassRate)}
            variant={overallPassRate >= 80 ? 'success' : overallPassRate >= 50 ? 'warning' : 'destructive'}
          />
          <StatsCard icon={XCircle} label="کل شکست‌ها" value={formatNumber(totalFailed)} variant="destructive" />
          <StatsCard
            icon={Users}
            label="کاربران فعال"
            value={formatNumber(users?.length ?? 0)}
            variant="info"
          />
        </div>
      )}

      {/* Filters */}
      <Card>
        <CardContent className="p-4">
          <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
            <Select
              value={projectId}
              onChange={(e) => setProjectId(e.target.value)}
            >
              <option value="">همه پروژه‌ها</option>
              {projects?.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.code} — {p.name}
                </option>
              ))}
            </Select>

            {/* Tab-style granularity picker */}
            <div className="flex rounded-lg border border-border overflow-hidden">
              {GRANULARITY_OPTS.map((o) => (
                <button
                  key={o.value}
                  onClick={() => setGranularity(o.value)}
                  className={[
                    'flex-1 py-2 text-xs font-medium transition-base',
                    granularity === o.value
                      ? 'bg-primary text-primary-foreground'
                      : 'text-muted-foreground hover:bg-accent hover:text-foreground',
                  ].join(' ')}
                >
                  {o.label}
                </button>
              ))}
            </div>

            {/* Tab-style days picker */}
            <div className="flex rounded-lg border border-border overflow-hidden">
              {DAYS_OPTS.map((o) => (
                <button
                  key={o.value}
                  onClick={() => setDays(o.value)}
                  className={[
                    'flex-1 py-2 text-xs font-medium transition-base',
                    days === o.value
                      ? 'bg-primary text-primary-foreground'
                      : 'text-muted-foreground hover:bg-accent hover:text-foreground',
                  ].join(' ')}
                >
                  {o.label}
                </button>
              ))}
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Trend chart */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <TrendingUp className="size-4 text-primary" /> روند نتایج
          </CardTitle>
          <CardDescription>
            تعداد نتایج{' '}
            {granularity === 'day' ? 'روزانه' : granularity === 'week' ? 'هفتگی' : 'ماهانه'}
          </CardDescription>
        </CardHeader>
        <CardContent>
          {tl ? (
            <Skeleton className="h-72 w-full rounded-lg" />
          ) : !chartData.length ? (
            <EmptyState
              icon={<TrendingUp className="size-7" />}
              title="داده‌ای برای روند موجود نیست"
            />
          ) : (
            <ResponsiveContainer width="100%" height={280}>
              <BarChart data={chartData} margin={{ top: 10, right: 0, left: -20, bottom: 0 }}>
                <CartesianGrid
                  strokeDasharray="3 3"
                  stroke="hsl(var(--border))"
                  vertical={false}
                />
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
                  formatter={(v) => (
                    <span style={{ fontFamily: 'Vazirmatn', fontSize: 11 }}>{v}</span>
                  )}
                  iconSize={8}
                />
                <Bar dataKey="موفق" stackId="a" fill="#16a34a" />
                <Bar dataKey="ناموفق" stackId="a" fill="#dc2626" />
                <Bar dataKey="مسدود" stackId="a" fill="#f59e0b" />
                <Bar dataKey="تست مجدد" stackId="a" fill="#0ea5e9" />
                <Bar dataKey="رد شده" stackId="a" fill="#94a3b8" radius={[4, 4, 0, 0]} />
              </BarChart>
            </ResponsiveContainer>
          )}
        </CardContent>
      </Card>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Top failures */}
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <AlertTriangle className="size-4 text-destructive" /> سناریوهای پرشکست
            </CardTitle>
            <CardDescription>۱۰ سناریوی با بیشترین نرخ شکست در بازه انتخابی</CardDescription>
          </CardHeader>
          <CardContent className="px-0 pb-0">
            {ttl ? (
              <div className="px-5 pb-5 space-y-2">
                {Array.from({ length: 5 }).map((_, i) => (
                  <Skeleton key={i} className="h-12" />
                ))}
              </div>
            ) : !topFails || topFails.length === 0 ? (
              <EmptyState icon={<Trophy className="size-7" />} title="شکستی ثبت نشده — عالی!" />
            ) : (
              <ul className="divide-y divide-border">
                {topFails.map((t, i) => (
                  <li key={t.testCaseId} className="px-5 py-3">
                    <div className="flex items-center gap-3">
                      <div className="size-7 rounded-md bg-destructive/10 text-destructive text-xs font-bold flex items-center justify-center num-fa shrink-0">
                        {formatNumber(i + 1)}
                      </div>
                      <div className="flex-1 min-w-0">
                        <div className="font-medium text-sm truncate">{t.title}</div>
                        <div className="text-xs text-muted-foreground font-mono">{t.code}</div>
                      </div>
                      <div className="text-end shrink-0">
                        <Badge variant="destructive" size="sm">
                          {formatPercent(t.failureRate)}
                        </Badge>
                        <div className="text-[10px] text-muted-foreground mt-0.5 num-fa">
                          {formatNumber(t.failures)}/{formatNumber(t.executions)}
                        </div>
                      </div>
                    </div>
                    <div className="mt-1.5 h-1 rounded-full bg-destructive/10 overflow-hidden">
                      <div
                        className="h-full rounded-full bg-destructive/60"
                        style={{ width: `${Math.min(100, t.failureRate)}%` }}
                      />
                    </div>
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>

        {/* User activity */}
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <Users className="size-4 text-info" /> فعالیت کاربران
            </CardTitle>
            <CardDescription>اعضای فعال و سهم آن‌ها از نتایج ثبت‌شده</CardDescription>
          </CardHeader>
          <CardContent className="px-0 pb-0">
            {ul ? (
              <div className="px-5 pb-5 space-y-2">
                {Array.from({ length: 5 }).map((_, i) => (
                  <Skeleton key={i} className="h-12" />
                ))}
              </div>
            ) : !users || users.length === 0 ? (
              <EmptyState icon={<Users className="size-7" />} title="هیچ فعالیتی ثبت نشده" />
            ) : (
              <ul className="divide-y divide-border">
                {users.map((u) => (
                  <li key={u.userId} className="px-5 py-3 flex items-center gap-3">
                    <div className="size-9 rounded-full bg-gradient-to-br from-primary/70 to-info text-white text-xs font-bold flex items-center justify-center shrink-0">
                      {u.userId.slice(0, 2).toUpperCase()}
                    </div>
                    <div className="flex-1 min-w-0">
                      <div className="text-sm font-medium truncate font-mono">
                        {u.userId.slice(0, 8)}…
                      </div>
                      <div className="flex items-center gap-2 mt-0.5 num-fa text-[10px] text-muted-foreground">
                        <span>{formatNumber(u.executions)} اجرا</span>
                        <span className="text-success">{formatNumber(u.passed)} ✓</span>
                        <span className="text-destructive">{formatNumber(u.failed)} ✗</span>
                      </div>
                    </div>
                    <Badge
                      variant={
                        u.passRate >= 80 ? 'success' : u.passRate >= 50 ? 'warning' : 'destructive'
                      }
                      size="sm"
                    >
                      {formatPercent(u.passRate)}
                    </Badge>
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
