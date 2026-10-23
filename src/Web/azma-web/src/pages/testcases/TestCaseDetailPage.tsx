import { useState } from 'react';
import { useParams, Link, useNavigate } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { TestCasesApi, CustomFieldsApi } from '@/lib/queries/endpoints';
import { PageHeader } from '@/components/common/PageHeader';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/common/Skeletons';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { TestCaseStatusBadge, PriorityBadge } from '@/components/common/StatusBadges';
import { ExecutionSummary } from '@/components/common/ExecutionStatus';
import { ScenarioAuthor } from '@/components/common/ScenarioAuthor';
import { ChevronRight, Edit2, Trash2, History, Tag, FileText, ArrowLeft, Sparkles, PlayCircle } from 'lucide-react';
import { fmtDateTime } from '@/lib/date';
import { formatNumber } from '@/lib/utils';
import { toast } from 'sonner';
import { extractError } from '@/lib/api';
import { motion } from 'framer-motion';
import { MarkdownView } from '@/components/editor/MarkdownEditor';
import { CustomFieldsView } from '@/components/customfields/CustomFieldsForm';
import NewRunDialog from '@/pages/runs/NewRunDialog';

export default function TestCaseDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const qc = useQueryClient();
  const [showRunDialog, setShowRunDialog] = useState(false);

  const { data: tc, isLoading } = useQuery({
    queryKey: ['testcase', id],
    queryFn: () => TestCasesApi.get(id!),
    enabled: !!id,
  });
  const { data: versions } = useQuery({
    queryKey: ['testcase-versions', id],
    queryFn: () => TestCasesApi.versions(id!),
    enabled: !!id,
  });

  const { data: cfDefs } = useQuery({
    queryKey: ['custom-fields', tc?.projectId],
    queryFn: () => CustomFieldsApi.list(tc!.projectId),
    enabled: !!tc?.projectId,
  });

  const removeMut = useMutation({
    mutationFn: () => TestCasesApi.remove(id!),
    onSuccess: () => {
      toast.success('سناریو حذف شد');
      qc.invalidateQueries({ queryKey: ['testcases'] });
      navigate('/testcases');
    },
    onError: (err) => toast.error(extractError(err)),
  });

  if (isLoading || !tc)
    return (
      <div className="space-y-4">
        <Skeleton className="h-9 w-2/3" />
        <Skeleton className="h-40 w-full" />
        <Skeleton className="h-60 w-full" />
      </div>
    );

  const automationEngineLabel = tc.automationEngine === 'ScenarioRecording'
    ? 'Recorded replay'
    : 'Manual';

  return (
    <motion.div initial={{ opacity: 0, y: 6 }} animate={{ opacity: 1, y: 0 }} className="space-y-6">
      <PageHeader
        breadcrumbs={
          <div className="flex items-center gap-1 text-xs text-muted-foreground">
            <Link
              to={tc.projectId ? `/repository/${tc.projectId}` : '/testcases'}
              className="hover:text-foreground"
            >سناریوها</Link>
            <ChevronRight className="size-3 rotate-180" />
            <span className="font-mono">{tc.code}</span>
          </div>
        }
        title={tc.title}
        description={`نسخه v${formatNumber(tc.version)} · آخرین ویرایش ${fmtDateTime(tc.updatedAtUtc ?? tc.createdAtUtc)}`}
        actions={
          <>
            <Button variant="outline" asChild>
              <Link to={`/testcases/${tc.id}/edit`}>
                <Edit2 className="size-4" /> ویرایش
              </Link>
            </Button>
            <Button onClick={() => setShowRunDialog(true)}>
              <PlayCircle className="size-4" /> اجرای سناریو
            </Button>
            <Button
              variant="destructive"
              onClick={() => {
                if (confirm('آیا از حذف این سناریو مطمئن هستید؟')) removeMut.mutate();
              }}
              loading={removeMut.isPending}
            >
              <Trash2 className="size-4" /> حذف
            </Button>
          </>
        }
      />

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div className="lg:col-span-2 space-y-6">
          {/* Meta */}
          <Card>
            <CardContent className="p-5 grid grid-cols-2 md:grid-cols-3 xl:grid-cols-6 gap-4">
              <Meta label="وضعیت"><TestCaseStatusBadge status={tc.status} /></Meta>
              <Meta label="اولویت"><PriorityBadge priority={tc.priority} /></Meta>
              <Meta label="کد"><Badge variant="muted">{tc.code}</Badge></Meta>
              <Meta label="نسخه"><Badge variant="info">v{formatNumber(tc.version)}</Badge></Meta>
              <Meta label="اضافه‌کننده"><ScenarioAuthor userId={tc.createdByUserId} /></Meta>
              <Meta label="آخرین اجرا">
                <ExecutionSummary
                  compact
                  lastExecutedAtUtc={tc.lastExecutedAtUtc}
                  status={tc.lastExecutionStatus}
                  runId={tc.lastExecutionRunId}
                />
              </Meta>
            </CardContent>
          </Card>

          {tc.hasAutomation && (
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <PlayCircle className="size-4 text-info" /> اتوماسیون سناریو
                  <Badge variant={tc.automationEngine === 'ScenarioRecording' ? 'info' : 'muted'} size="sm">
                    {automationEngineLabel}
                  </Badge>
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-2">
                <div className="rounded-lg border border-info/20 bg-info/5 px-3 py-2 text-xs" dir="ltr">
                  <span>Published recording v{tc.automationDefinitionVersion ?? 1}</span>
                </div>
              </CardContent>
            </Card>
          )}

          {/* Preconditions */}
          {tc.preconditions && (
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <FileText className="size-4 text-info" /> پیش‌نیازها
                </CardTitle>
              </CardHeader>
              <CardContent>
                <MarkdownView source={tc.preconditions} />
              </CardContent>
            </Card>
          )}

          {/* Steps */}
          <Card>
            <CardHeader>
              <CardTitle>مراحل اجرا ({formatNumber(tc.steps.length)})</CardTitle>
            </CardHeader>
            <CardContent className="px-0 pb-0">
              {tc.steps.length === 0 ? (
                <div className="px-6 pb-6 text-sm text-muted-foreground">مرحله‌ای ثبت نشده.</div>
              ) : (
                <ol className="divide-y divide-border">
                  {tc.steps.map((s) => (
                    <li key={s.order} className="px-6 py-4 grid grid-cols-12 gap-4">
                      <div className="col-span-1">
                        <div className="size-8 rounded-full bg-primary text-primary-foreground font-bold text-sm flex items-center justify-center num-fa">
                          {formatNumber(s.order)}
                        </div>
                      </div>
                      <div className="col-span-11 md:col-span-5">
                        <div className="text-xs text-muted-foreground mb-1">عمل</div>
                        <MarkdownView source={s.action} />
                      </div>
                      <div className="col-span-12 md:col-span-6">
                        <div className="text-xs text-muted-foreground mb-1">نتیجه مورد انتظار</div>
                        <MarkdownView source={s.expectedResult} className="text-success" />
                      </div>
                    </li>
                  ))}
                </ol>
              )}
            </CardContent>
          </Card>

          {cfDefs && cfDefs.filter((d) => !d.isArchived).length > 0 && (
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <Sparkles className="size-4 text-info" /> فیلدهای سفارشی
                </CardTitle>
              </CardHeader>
              <CardContent>
                <CustomFieldsView
                  definitions={cfDefs}
                  values={tc.customFields ?? {}}
                />
              </CardContent>
            </Card>
          )}
        </div>

        <div className="space-y-6">
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Tag className="size-4 text-warning" /> برچسب‌ها
              </CardTitle>
            </CardHeader>
            <CardContent>
              <div className="flex flex-wrap gap-1.5">
                {tc.tags.length === 0 ? (
                  <span className="text-sm text-muted-foreground">بدون برچسب</span>
                ) : (
                  tc.tags.map((t) => (
                    <Badge key={t} variant="secondary">
                      {t}
                    </Badge>
                  ))
                )}
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <History className="size-4 text-info" /> تاریخچه نسخه‌ها
              </CardTitle>
            </CardHeader>
            <CardContent className="px-0 pb-0">
              {!versions || versions.length === 0 ? (
                <div className="px-6 pb-6 text-sm text-muted-foreground">نسخه‌ای ثبت نشده.</div>
              ) : (
                <ul className="divide-y divide-border max-h-96 overflow-y-auto">
                  {versions.map((v) => (
                    <li key={v.version} className="px-6 py-3 flex items-center justify-between">
                      <div>
                        <div className="font-medium num-fa text-sm">v{formatNumber(v.version)}</div>
                        <div className="text-xs text-muted-foreground">{fmtDateTime(v.createdAtUtc)}</div>
                      </div>
                      <ArrowLeft className="size-4 text-muted-foreground" />
                    </li>
                  ))}
                </ul>
              )}
            </CardContent>
          </Card>
        </div>
      </div>
      {showRunDialog && (
        <NewRunDialog
          onClose={() => setShowRunDialog(false)}
          preSelectedIds={[tc.id]}
          preProjectId={tc.projectId}
        />
      )}
    </motion.div>
  );
}

function Meta({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <div className="text-xs text-muted-foreground mb-1.5">{label}</div>
      {children}
    </div>
  );
}
