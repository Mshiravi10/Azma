import { useQuery } from '@tanstack/react-query';
import {
  AlertTriangle,
  Clock,
  FileWarning,
  Server,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/common/Skeletons';
import { RunsApi } from '@/lib/queries/endpoints';
import type {
  AutomationFailureKind,
  AutomationRunResponse,
  RunResult,
} from '@/lib/types';
import { fmtDateTime } from '@/lib/date';
import { formatNumber } from '@/lib/utils';

interface RunCaseFailureDetailsProps {
  runCaseId: string;
}

const FAILURE_KIND_LABELS: Record<AutomationFailureKind, string> = {
  None: 'نامشخص',
  FrontendFailure: 'خطای فرانت‌اند',
  ApiFailure: 'خطای سرویس',
  AssertionFailure: 'عدم تطابق نتیجه',
  Timeout: 'پایان مهلت اجرا',
  LocatorFailure: 'المان صفحه پیدا نشد',
  ValidationFailure: 'خطای اعتبارسنجی',
  RunnerFailure: 'خطای موتور اجرا',
};

function parseDetail<T>(result: RunResult): T | null {
  if (!result.automationDetailJson) return null;

  try {
    return JSON.parse(result.automationDetailJson) as T;
  } catch {
    return null;
  }
}

function RecordedFailureDetails({ result }: { result: RunResult }) {
  const detail = parseDetail<AutomationRunResponse>(result);
  if (!detail) return null;

  const failedSteps = detail.steps.filter((step) => !step.success);
  const failedRequests = detail.network.filter(
    (request) => request.statusCode >= 400 || Boolean(request.errorMessage),
  );

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-2">
        <Badge variant="destructive" size="sm">Recorded</Badge>
        <span className="text-xs font-medium text-destructive">
          {FAILURE_KIND_LABELS[detail.failureKind] ?? detail.failureKind}
        </span>
      </div>

      {detail.errorMessage && (
        <div className="rounded-md border border-destructive/20 bg-destructive/5 p-3 text-xs leading-5 text-destructive" dir="auto">
          {detail.errorMessage}
        </div>
      )}

      {failedSteps.length > 0 && (
        <div className="space-y-1.5">
          <div className="flex items-center gap-1 text-xs font-medium">
            <FileWarning className="size-3.5 text-destructive" /> مرحله‌ی خطادار
          </div>
          {failedSteps.slice(0, 5).map((step) => (
            <div key={`${step.order}-${step.kind}`} className="rounded-md border border-border bg-background/70 p-2 text-xs">
              <div className="flex flex-wrap items-center gap-2">
                <code dir="ltr">#{step.order} {step.kind}</code>
                {step.label && <span className="text-muted-foreground">{step.label}</span>}
              </div>
              {step.errorMessage && (
                <p className="mt-1 whitespace-pre-wrap break-words text-destructive" dir="auto">
                  {step.errorMessage}
                </p>
              )}
            </div>
          ))}
        </div>
      )}

      {failedRequests.length > 0 && (
        <div className="space-y-1.5">
          <div className="flex items-center gap-1 text-xs font-medium">
            <Server className="size-3.5 text-destructive" /> پاسخ‌های خطادار سرویس‌ها
          </div>
          {failedRequests.slice(0, 5).map((request, index) => (
            <div key={`${request.method}-${request.url}-${index}`} className="rounded-md border border-border bg-background/70 p-2 text-xs">
              <div className="flex flex-wrap items-center gap-2" dir="ltr">
                <Badge variant="muted" size="sm">{request.method}</Badge>
                <code className="break-all">{request.url}</code>
                <Badge variant="destructive" size="sm">HTTP {request.statusCode}</Badge>
              </div>
              {request.errorMessage && (
                <p className="mt-2 whitespace-pre-wrap break-words text-destructive" dir="auto">
                  {request.errorMessage}
                </p>
              )}
              {request.responseBody && (
                <pre className="mt-2 max-h-40 overflow-auto whitespace-pre-wrap break-all rounded bg-muted/60 p-2 text-[11px] leading-5" dir="ltr">
                  {request.responseBody}
                </pre>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

export function RunCaseFailureDetails({ runCaseId }: RunCaseFailureDetailsProps) {
  const { data: history, isLoading, isError } = useQuery({
    queryKey: ['result-history', runCaseId],
    queryFn: () => RunsApi.resultHistory(runCaseId),
  });

  if (isLoading) {
    return (
      <div className="space-y-2 border-t border-border px-5 py-4">
        <Skeleton className="h-4 w-40" />
        <Skeleton className="h-16 w-full" />
      </div>
    );
  }

  if (isError) {
    return (
      <div className="flex items-center gap-2 border-t border-border px-5 py-4 text-xs text-destructive">
        <AlertTriangle className="size-4" /> دریافت جزئیات خطا ناموفق بود.
      </div>
    );
  }

  const result = history?.[0];
  if (!result) {
    return (
      <div className="border-t border-border px-5 py-4 text-xs text-muted-foreground">
        جزئیات این خطا ثبت نشده است.
      </div>
    );
  }

  return (
    <div className="space-y-3 border-t border-border bg-destructive/[0.025] px-5 py-4">
      <div className="flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
        <AlertTriangle className="size-4 text-destructive" />
        <span className="font-semibold text-foreground">جزئیات آخرین خطا</span>
        <span className="inline-flex items-center gap-1">
          <Clock className="size-3.5" /> {fmtDateTime(result.executedAtUtc)}
        </span>
        {result.elapsedSeconds && <span>{formatNumber(result.elapsedSeconds)} ثانیه</span>}
      </div>

      {result.automationEngine === 'ScenarioRecording' && <RecordedFailureDetails result={result} />}

      {result.comment && (
        <details className="rounded-md border border-border bg-background/70 px-3 py-2 text-xs">
          <summary className="cursor-pointer font-medium text-muted-foreground">گزارش خام اجرا</summary>
          <p className="mt-2 whitespace-pre-wrap break-words leading-5 text-muted-foreground" dir="auto">
            {result.comment}
          </p>
        </details>
      )}
    </div>
  );
}
