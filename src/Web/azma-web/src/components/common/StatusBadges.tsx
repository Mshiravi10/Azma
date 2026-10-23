import { cn } from '@/lib/utils';
import type { ResultStatus, RunStatus, TestCasePriority, TestCaseStatus } from '@/lib/types';
import { Badge } from '@/components/ui/badge';

const RESULT_LABEL: Record<ResultStatus, string> = {
  Untested: 'تست نشده',
  Passed: 'موفق',
  Failed: 'ناموفق',
  Blocked: 'مسدود',
  Retest: 'تست مجدد',
  Skipped: 'رد شده',
};

const RESULT_VARIANT: Record<
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

const RESULT_DOT_COLOR: Record<ResultStatus, string> = {
  Passed: 'bg-success',
  Failed: 'bg-destructive',
  Blocked: 'bg-warning',
  Retest: 'bg-info',
  Skipped: 'bg-muted-foreground',
  Untested: 'bg-muted-foreground/40',
};

export function ResultBadge({ status, className }: { status: ResultStatus; className?: string }) {
  return (
    <Badge variant={RESULT_VARIANT[status]} className={cn(className)}>
      {RESULT_LABEL[status]}
    </Badge>
  );
}

export function ResultDot({ status, className }: { status: ResultStatus; className?: string }) {
  return (
    <span
      className={cn('inline-block size-2 rounded-full shrink-0', RESULT_DOT_COLOR[status], className)}
    />
  );
}

const RUN_LABEL: Record<RunStatus, string> = { Open: 'باز', Closed: 'بسته' };
export function RunStatusBadge({ status }: { status: RunStatus }) {
  return <Badge variant={status === 'Open' ? 'info' : 'muted'}>{RUN_LABEL[status]}</Badge>;
}

const TC_STATUS_LABEL: Record<TestCaseStatus, string> = {
  Draft: 'پیش‌نویس',
  Active: 'فعال',
  Deprecated: 'منسوخ',
};
export function TestCaseStatusBadge({ status }: { status: TestCaseStatus }) {
  const v = status === 'Active' ? 'success' : status === 'Draft' ? 'warning' : 'muted';
  return <Badge variant={v}>{TC_STATUS_LABEL[status]}</Badge>;
}

const PRIORITY_LABEL: Record<TestCasePriority, string> = {
  Low: 'کم',
  Medium: 'متوسط',
  High: 'بالا',
  Critical: 'بحرانی',
};
export function PriorityBadge({ priority }: { priority: TestCasePriority }) {
  const v =
    priority === 'Critical'
      ? 'destructive'
      : priority === 'High'
        ? 'warning'
        : priority === 'Medium'
          ? 'info'
          : 'muted';
  return <Badge variant={v}>{PRIORITY_LABEL[priority]}</Badge>;
}

export const ResultLabels = RESULT_LABEL;
export const ResultVariants = RESULT_VARIANT;
