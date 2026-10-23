import type { ComponentType } from 'react';
import { TrendingDown, TrendingUp } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import { cn } from '@/lib/utils';

interface StatsCardProps {
  icon: ComponentType<{ className?: string }>;
  label: string;
  value: string | number;
  hint?: string;
  trend?: { value: number; label: string };
  variant?: 'default' | 'success' | 'destructive' | 'warning' | 'info' | 'muted';
  className?: string;
}

const iconTone: Record<NonNullable<StatsCardProps['variant']>, string> = {
  default: 'bg-primary/10 text-primary',
  success: 'bg-success/10 text-success',
  destructive: 'bg-destructive/10 text-destructive',
  warning: 'bg-warning/10 text-warning',
  info: 'bg-info/10 text-info',
  muted: 'bg-muted text-muted-foreground',
};

export function StatsCard({
  icon: Icon,
  label,
  value,
  hint,
  trend,
  variant = 'default',
  className,
}: StatsCardProps) {
  const positive = trend && trend.value >= 0;

  return (
    <Card className={className}>
      <CardContent className="flex flex-col gap-3">
        <div className="flex items-start justify-between gap-3">
          <span className="text-sm leading-none text-muted-foreground">{label}</span>
          <div className={cn('flex size-9 shrink-0 items-center justify-center rounded-lg', iconTone[variant])}>
            <Icon />
          </div>
        </div>
        <div className="flex items-end gap-3">
          <span className="text-2xl leading-none font-bold">{value}</span>
          {trend ? (
            <Badge variant={positive ? 'success' : 'destructive'}>
              {positive ? <TrendingUp /> : <TrendingDown />}
              {Math.abs(trend.value)}%
            </Badge>
          ) : null}
        </div>
        {hint || trend?.label ? (
          <p className="text-xs leading-tight text-muted-foreground">{hint || trend?.label}</p>
        ) : null}
      </CardContent>
    </Card>
  );
}
