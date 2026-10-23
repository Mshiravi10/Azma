import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import {
  BookOpen,
  ExternalLink,
  FileQuestion,
  Image as ImageIcon,
  ListChecks,
  Search,
  Sparkles,
  X,
} from 'lucide-react';
import { PageHeader } from '@/components/common/PageHeader';
import { EmptyState } from '@/components/common/EmptyState';
import { MarkdownView } from '@/components/editor/MarkdownEditor';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { cn } from '@/lib/utils';
import {
  documentationQuickTips,
  documentationSections,
  type DocumentationImage,
  type DocumentationRole,
  type DocumentationSection,
} from './documentationContent.ts';

const roleVariant: Record<DocumentationRole, 'default' | 'success' | 'info' | 'warning' | 'muted'> = {
  کاربر: 'muted',
  تستر: 'success',
  'مدیر پروژه': 'info',
  ادمین: 'warning',
  مالک: 'default',
};

function normalizeText(value: string) {
  return value
    .toLocaleLowerCase('fa-IR')
    .replace(/ي/g, 'ی')
    .replace(/ك/g, 'ک')
    .replace(/\s+/g, ' ')
    .trim();
}

function searchableText(section: DocumentationSection) {
  return [
    section.title,
    section.description,
    section.roles.join(' '),
    section.highlights.join(' '),
    section.body,
    section.route?.label ?? '',
  ].join(' ');
}

export default function DocumentationPage() {
  const [query, setQuery] = useState('');

  const filteredSections = useMemo(() => {
    const q = normalizeText(query);
    if (!q) return documentationSections;
    return documentationSections.filter((section) =>
      normalizeText(searchableText(section)).includes(q),
    );
  }, [query]);

  const allHighlights = useMemo(
    () => Array.from(new Set(documentationSections.flatMap((section) => section.highlights))),
    [],
  );

  return (
    <div className="space-y-6" dir="rtl">
      <PageHeader
        title="راهنمای آزما"
        description="مسیر کامل کار با آزما؛ از ساخت پروژه و سناریو تا اجرا، گزارش، زمان‌بندی و مدیریت دسترسی‌ها."
        actions={
          <Button asChild variant="outline">
            <Link to="/dashboard">
              <ExternalLink className="size-4" />
              داشبورد
            </Link>
          </Button>
        }
      />

      <section className="rounded-xl border border-border bg-card px-4 py-4 shadow-soft sm:px-5">
        <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_minmax(18rem,26rem)] lg:items-center">
          <div className="space-y-3">
            <div className="inline-flex items-center gap-2 rounded-full border border-primary/20 bg-primary/10 px-3 py-1 text-xs font-medium text-primary">
              <Sparkles className="size-3.5" />
              مستندات داخلی محصول
            </div>
            <p className="max-w-3xl text-sm leading-7 text-muted-foreground">
              این راهنما برای همه نقش‌ها نوشته شده و روی کارهای واقعی کاربر تمرکز دارد:
              ساختاردهی پروژه، نوشتن سناریو، ساخت اجرا، ثبت نتیجه، تحلیل گزارش و مدیریت اعضا.
            </p>
            <div className="flex flex-wrap gap-2">
              {allHighlights.slice(0, 10).map((item) => (
                <Badge key={item} variant="secondary">
                  {item}
                </Badge>
              ))}
            </div>
          </div>

          <label className="relative block">
            <Search className="pointer-events-none absolute right-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground/60" />
            <input
              value={query}
              onChange={(event) => setQuery(event.target.value)}
              type="search"
              placeholder="جست‌وجو در راهنما..."
              className={cn(
                'h-11 w-full rounded-lg border border-input bg-background py-2 ps-10 pe-10 text-sm shadow-sm',
                'placeholder:text-muted-foreground/50 transition-base',
                'focus-visible:border-transparent focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
              )}
            />
            {query && (
              <button
                type="button"
                onClick={() => setQuery('')}
                className="absolute left-2 top-1/2 flex size-7 -translate-y-1/2 items-center justify-center rounded-md text-muted-foreground transition-base hover:bg-accent hover:text-foreground"
                aria-label="پاک کردن جست‌وجو"
              >
                <X className="size-4" />
              </button>
            )}
          </label>
        </div>
      </section>

      <div className="grid gap-5 xl:grid-cols-[18rem_minmax(0,1fr)] xl:items-start">
        <aside className="space-y-4 xl:sticky xl:top-20">
          <Card variant="outlined" className="bg-card shadow-soft">
            <CardHeader className="p-4 pb-2">
              <CardTitle className="flex items-center gap-2 text-sm">
                <BookOpen className="size-4 text-primary" />
                فصل‌های راهنما
              </CardTitle>
            </CardHeader>
            <CardContent className="p-3 pt-0">
              <nav className="space-y-1">
                {documentationSections.map((section) => {
                  const visible = filteredSections.some((item) => item.id === section.id);
                  return (
                    <a
                      key={section.id}
                      href={`#${section.id}`}
                      className={cn(
                        'flex items-center gap-2 rounded-lg px-2.5 py-2 text-sm transition-base',
                        visible
                          ? 'text-foreground hover:bg-accent'
                          : 'pointer-events-none text-muted-foreground/35',
                      )}
                    >
                      <span className="size-1.5 rounded-full bg-current opacity-50" />
                      <span className="truncate">{section.title}</span>
                    </a>
                  );
                })}
              </nav>
            </CardContent>
          </Card>

          <Card variant="flat" className="border-transparent">
            <CardHeader className="p-4 pb-2">
              <CardTitle className="flex items-center gap-2 text-sm">
                <ListChecks className="size-4 text-success" />
                نکات سریع
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-2 p-4 pt-0">
              {documentationQuickTips.map((tip) => (
                <div key={tip} className="flex gap-2 text-xs leading-6 text-muted-foreground">
                  <span className="mt-2 size-1.5 shrink-0 rounded-full bg-success" />
                  <span>{tip}</span>
                </div>
              ))}
            </CardContent>
          </Card>
        </aside>

        <main className="min-w-0 space-y-5">
          {filteredSections.length === 0 ? (
            <Card variant="outlined" className="bg-card">
              <CardContent className="p-8">
                <EmptyState
                  icon={<FileQuestion className="size-7" />}
                  title="موردی پیدا نشد"
                  description="عبارت دیگری جست‌وجو کنید یا فیلتر را پاک کنید."
                  action={
                    <Button type="button" variant="outline" onClick={() => setQuery('')}>
                      پاک کردن جست‌وجو
                    </Button>
                  }
                />
              </CardContent>
            </Card>
          ) : (
            filteredSections.map((section) => <DocumentationSectionCard key={section.id} section={section} />)
          )}
        </main>
      </div>
    </div>
  );
}

function DocumentationSectionCard({ section }: { section: DocumentationSection }) {
  return (
    <article id={section.id} className="scroll-mt-24">
      <Card variant="outlined" className="overflow-hidden bg-card shadow-soft">
        <CardHeader className="border-b border-border p-5">
          <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
            <div className="min-w-0 flex-1 space-y-3">
              <div>
                <h2 className="text-lg font-bold leading-8 text-foreground">{section.title}</h2>
                <p className="mt-1 text-sm leading-7 text-muted-foreground">{section.description}</p>
              </div>
              <div className="flex flex-wrap gap-2">
                {section.roles.map((role) => (
                  <Badge key={role} variant={roleVariant[role]} dot>
                    {role}
                  </Badge>
                ))}
              </div>
            </div>
            {section.route && (
              <Button asChild variant="subtle" className="shrink-0">
                <Link to={section.route.to}>
                  <ExternalLink className="size-4" />
                  {section.route.label}
                </Link>
              </Button>
            )}
          </div>
        </CardHeader>

        <CardContent className="space-y-5 p-5">
          <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
            {section.highlights.map((item) => (
              <div key={item} className="rounded-lg border border-border bg-secondary/35 px-3 py-2 text-xs font-medium text-muted-foreground">
                {item}
              </div>
            ))}
          </div>

          <MarkdownView source={section.body} className="text-[0.94rem]" />

          <div
            className={cn(
              'grid gap-4',
              section.images.length > 1 ? 'lg:grid-cols-2' : 'grid-cols-1',
            )}
          >
            {section.images.map((image) => (
              <ScreenshotFigure key={image.src} image={image} />
            ))}
          </div>
        </CardContent>
      </Card>
    </article>
  );
}

function ScreenshotFigure({ image }: { image: DocumentationImage }) {
  const [failed, setFailed] = useState(false);

  return (
    <figure className="overflow-hidden rounded-lg border border-border bg-secondary/30">
      <div className="aspect-[16/10] bg-muted/60">
        {failed ? (
          <div className="flex h-full flex-col items-center justify-center gap-2 p-6 text-center text-muted-foreground">
            <ImageIcon className="size-8" />
            <span className="text-xs leading-6">تصویر راهنما بعد از گرفتن اسکرین‌شات در این بخش نمایش داده می‌شود.</span>
          </div>
        ) : (
          <img
            src={image.src}
            alt={image.alt}
            loading="lazy"
            onError={() => setFailed(true)}
            className="h-full w-full object-cover object-top"
          />
        )}
      </div>
      <figcaption className="border-t border-border bg-card px-3 py-2 text-xs leading-6 text-muted-foreground">
        {image.caption}
      </figcaption>
    </figure>
  );
}
