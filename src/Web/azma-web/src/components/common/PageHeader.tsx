import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { ChevronRight } from 'lucide-react';
import { cn } from '@/lib/utils';
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
} from '@/components/ui/breadcrumb';

interface PageHeaderProps {
  title: ReactNode;
  description?: ReactNode;
  actions?: ReactNode;
  breadcrumbs?: ReactNode;
  back?: string;
  backLabel?: string;
  sticky?: boolean;
  className?: string;
}

export function PageHeader({
  title,
  description,
  actions,
  breadcrumbs,
  back,
  backLabel,
  sticky,
  className,
}: PageHeaderProps) {
  return (
    <div
      className={cn(
        'mb-6',
        sticky &&
          'sticky top-14 z-20 -mx-6 border-b bg-background/85 px-6 py-4 backdrop-blur-sm',
        className,
      )}
    >
      {(back || breadcrumbs) && (
        <div className="mb-3 flex items-center gap-2">
          {back ? (
            <Breadcrumb>
              <BreadcrumbList>
                <BreadcrumbItem>
                  <BreadcrumbLink asChild>
                    <Link to={back} className="inline-flex items-center gap-1">
                      <ChevronRight />
                      {backLabel || 'بازگشت'}
                    </Link>
                  </BreadcrumbLink>
                </BreadcrumbItem>
              </BreadcrumbList>
            </Breadcrumb>
          ) : null}
          {breadcrumbs}
        </div>
      )}
      <div className="flex items-start justify-between gap-4">
        <div className="min-w-0 flex-1">
          <h1 className="text-xl leading-tight font-semibold tracking-tight">{title}</h1>
          {description ? (
            <p className="mt-1 text-sm leading-relaxed text-muted-foreground">{description}</p>
          ) : null}
        </div>
        {actions ? <div className="flex shrink-0 flex-wrap items-center gap-2">{actions}</div> : null}
      </div>
    </div>
  );
}
