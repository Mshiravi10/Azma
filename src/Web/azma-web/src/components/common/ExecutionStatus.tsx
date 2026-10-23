import { Link } from 'react-router-dom';
import { Clock3, AlertTriangle, CircleDashed } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { ResultBadge } from '@/components/common/StatusBadges';
import { fmtDateTime, fmtRelative } from '@/lib/date';
import type { ResultStatus } from '@/lib/types';
import { cn } from '@/lib/utils';

const STALE_DAYS = 14;

export function isStaleExecution(value: string | null | undefined, days = STALE_DAYS) {
  if (!value) return false;
  return Date.now() - new Date(value).getTime() > days * 24 * 60 * 60 * 1000;
}

export function ExecutionSummary({
  lastExecutedAtUtc,
  status,
  runId,
  compact = false,
  className,
}: {
  lastExecutedAtUtc?: string | null;
  status?: ResultStatus | null;
  runId?: string | null;
  compact?: boolean;
  className?: string;
}) {
  if (!lastExecutedAtUtc) {
    return (
      <Badge variant="warning" className={cn('whitespace-nowrap', className)}>
        <CircleDashed className="size-3" />
        اجرا نشده
      </Badge>
    );
  }

  const stale = isStaleExecution(lastExecutedAtUtc);
  const body = (
    <div className={cn('flex items-center gap-2 min-w-0', className)} title={fmtDateTime(lastExecutedAtUtc)}>
      {status ? <ResultBadge status={status} /> : null}
      <span className={cn('text-xs text-muted-foreground whitespace-nowrap', stale && 'text-warning')}>
        {stale ? <AlertTriangle className="inline size-3 me-1" /> : <Clock3 className="inline size-3 me-1" />}
        {compact ? fmtRelative(lastExecutedAtUtc) : `آخرین اجرا ${fmtRelative(lastExecutedAtUtc)}`}
      </span>
    </div>
  );

  return runId ? (
    <Link to={`/runs/${runId}`} className="hover:opacity-80 transition-base">
      {body}
    </Link>
  ) : body;
}
