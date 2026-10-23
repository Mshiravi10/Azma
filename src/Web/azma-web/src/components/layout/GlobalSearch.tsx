import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import {
  BarChart3,
  BookOpen,
  CalendarClock,
  FlaskConical,
  FolderKanban,
  LayoutDashboard,
  PlayCircle,
  Search,
  Settings,
} from 'lucide-react';
import { ProjectsApi, RunsApi, TestCasesApi } from '@/lib/queries/endpoints';
import { formatNumber } from '@/lib/utils';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Command,
  CommandDialog,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
  CommandSeparator,
} from '@/components/ui/command';
import { Empty, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle } from '@/components/ui/empty';
import { Kbd } from '@/components/ui/kbd';
import { Spinner } from '@/components/ui/spinner';
import type { Project, Run, TestCaseSummary } from '@/lib/types';

const PAGES: Array<{
  href: string;
  label: string;
  keywords: string[];
  icon: typeof LayoutDashboard;
}> = [
  { href: '/dashboard', label: 'داشبورد', keywords: ['dashboard', 'خانه'], icon: LayoutDashboard },
  { href: '/projects', label: 'پروژه‌ها', keywords: ['project', 'projects'], icon: FolderKanban },
  { href: '/testcases', label: 'سناریوها', keywords: ['test', 'testcase', 'تست', 'سناریو'], icon: FlaskConical },
  { href: '/runs', label: 'اجراها', keywords: ['run', 'runs', 'اجرا'], icon: PlayCircle },
  { href: '/run-schedules', label: 'زمان‌بندی', keywords: ['schedule', 'زمان'], icon: CalendarClock },
  { href: '/reports', label: 'گزارش‌ها', keywords: ['report', 'گزارش'], icon: BarChart3 },
  { href: '/documentation', label: 'راهنما', keywords: ['docs', 'help', 'راهنما'], icon: BookOpen },
  { href: '/settings', label: 'تنظیمات', keywords: ['settings', 'تنظیم'], icon: Settings },
];

function useDebounced(value: string, delay = 250) {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const id = window.setTimeout(() => setDebounced(value), delay);
    return () => window.clearTimeout(id);
  }, [value, delay]);
  return debounced;
}

export function GlobalSearch() {
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');
  const trimmedQuery = query.trim();
  const debounced = useDebounced(trimmedQuery);
  const canSearch = open && trimmedQuery.length > 0 && debounced.length > 0;

  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === 'k') {
        event.preventDefault();
        setOpen((current) => !current);
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, []);

  const casesQuery = useQuery({
    queryKey: ['global-search', 'cases', debounced],
    queryFn: () => TestCasesApi.list({ search: debounced, page: 1, pageSize: 6 }),
    enabled: canSearch,
  });
  const runsQuery = useQuery({
    queryKey: ['global-search', 'runs', debounced],
    queryFn: () => RunsApi.list({ search: debounced, page: 1, pageSize: 6 }),
    enabled: canSearch,
  });
  const projectsQuery = useQuery({
    queryKey: ['global-search', 'projects', debounced],
    queryFn: () => ProjectsApi.list({ search: debounced, page: 1, pageSize: 6 }),
    enabled: canSearch,
  });

  const pages = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!q) return PAGES;
    return PAGES.filter(
      (page) => page.label.includes(query.trim()) || page.keywords.some((keyword) => keyword.includes(q) || q.includes(keyword)),
    );
  }, [query]);

  const cases: TestCaseSummary[] = canSearch ? (casesQuery.data?.items ?? []) : [];
  const runs: Run[] = canSearch ? (runsQuery.data?.items ?? []) : [];
  const projects: Project[] = canSearch ? (projectsQuery.data ?? []) : [];
  const searching =
    canSearch && (casesQuery.isLoading || runsQuery.isLoading || projectsQuery.isLoading);
  const failed =
    canSearch && casesQuery.isError && runsQuery.isError && projectsQuery.isError;
  const resultCount = pages.length + cases.length + runs.length + projects.length;

  function close() {
    setOpen(false);
    setQuery('');
  }

  function go(href: string) {
    close();
    navigate(href);
  }

  function openWith(nextQuery = '') {
    setQuery(nextQuery);
    setOpen(true);
  }

  return (
    <>
      <Button
        variant="ghost"
        size="icon"
        className="sm:hidden"
        aria-label="جست‌وجو"
        onClick={() => openWith()}
      >
        <Search />
      </Button>
      <button
        type="button"
        onClick={() => openWith()}
        onKeyDown={(event) => {
          if (event.key.length === 1 && !event.metaKey && !event.ctrlKey && !event.altKey) {
            event.preventDefault();
            openWith(event.key);
          }
        }}
        className="hidden h-8 w-72 max-w-[40vw] shrink-0 cursor-pointer items-center gap-2 rounded-lg border border-input bg-transparent px-2 text-sm text-muted-foreground outline-none transition-colors hover:bg-muted/50 focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50 sm:flex dark:bg-input/30 [&_svg]:size-4"
      >
        <Search />
        <span className="flex-1 truncate text-start">جست‌وجو…</span>
        <span className="hidden items-center gap-1 md:inline-flex">
          <Kbd>⌘</Kbd>
          <Kbd>K</Kbd>
        </span>
      </button>

      <CommandDialog
        open={open}
        onOpenChange={(next) => {
          setOpen(next);
          if (!next) setQuery('');
        }}
        title="جست‌وجو"
        description="جست‌وجو در سناریوها، اجراها، پروژه‌ها و صفحات"
        className="sm:max-w-lg"
      >
        <Command shouldFilter={false}>
          <CommandInput
            autoFocus
            placeholder="نام یا کد سناریو، اجرا یا پروژه…"
            value={query}
            onValueChange={setQuery}
          />
          <CommandList className="max-h-80">
            {searching ? (
              <div className="flex items-center justify-center gap-2 py-3 text-sm text-muted-foreground">
                <Spinner />
                در حال جست‌وجو…
              </div>
            ) : null}

            {!searching && debounced && resultCount === 0 ? (
              <CommandEmpty>
                <Empty className="border-0 py-6">
                  <EmptyHeader>
                    <EmptyMedia variant="icon">
                      <Search />
                    </EmptyMedia>
                    <EmptyTitle>{failed ? 'جست‌وجو انجام نشد' : 'نتیجه‌ای پیدا نشد'}</EmptyTitle>
                    <EmptyDescription>
                      {failed
                        ? 'اتصال به سرویس را بررسی کنید و دوباره امتحان کنید.'
                        : `چیزی برای «${debounced}» پیدا نشد.`}
                    </EmptyDescription>
                  </EmptyHeader>
                </Empty>
              </CommandEmpty>
            ) : null}

            {pages.length > 0 ? (
              <CommandGroup heading="صفحات">
                {pages.map((page) => {
                  const Icon = page.icon;
                  return (
                    <CommandItem key={page.href} value={page.href} onSelect={() => go(page.href)}>
                      <Icon />
                      <span>{page.label}</span>
                    </CommandItem>
                  );
                })}
              </CommandGroup>
            ) : null}

            {cases.length > 0 ? (
              <>
                {pages.length > 0 ? <CommandSeparator /> : null}
                <CommandGroup
                  heading={
                    (casesQuery.data?.total ?? 0) > cases.length
                      ? `سناریوها (${formatNumber(casesQuery.data?.total)})`
                      : 'سناریوها'
                  }
                >
                  {cases.map((item) => (
                    <CommandItem
                      key={item.id}
                      value={`case-${item.id}`}
                      onSelect={() => go(`/testcases/${item.id}`)}
                    >
                      <FlaskConical />
                      <Badge variant="muted">{item.code}</Badge>
                      <span className="min-w-0 flex-1 truncate">{item.title}</span>
                    </CommandItem>
                  ))}
                </CommandGroup>
              </>
            ) : null}

            {runs.length > 0 ? (
              <>
                {pages.length > 0 || cases.length > 0 ? <CommandSeparator /> : null}
                <CommandGroup
                  heading={
                    (runsQuery.data?.total ?? 0) > runs.length
                      ? `اجراها (${formatNumber(runsQuery.data?.total)})`
                      : 'اجراها'
                  }
                >
                  {runs.map((item) => (
                    <CommandItem
                      key={item.id}
                      value={`run-${item.id}`}
                      onSelect={() => go(`/runs/${item.id}`)}
                    >
                      <PlayCircle />
                      <Badge variant="muted">{item.code}</Badge>
                      <span className="min-w-0 flex-1 truncate">{item.name}</span>
                      <span className="text-xs text-muted-foreground">{item.projectCode}</span>
                    </CommandItem>
                  ))}
                </CommandGroup>
              </>
            ) : null}

            {projects.length > 0 ? (
              <>
                {pages.length > 0 || cases.length > 0 || runs.length > 0 ? <CommandSeparator /> : null}
                <CommandGroup heading="پروژه‌ها">
                  {projects.map((item) => (
                    <CommandItem
                      key={item.id}
                      value={`project-${item.id}`}
                      onSelect={() => go(`/projects/${item.id}`)}
                    >
                      <FolderKanban />
                      <Badge variant="muted">{item.code}</Badge>
                      <span className="min-w-0 flex-1 truncate">{item.name}</span>
                    </CommandItem>
                  ))}
                </CommandGroup>
              </>
            ) : null}
          </CommandList>
        </Command>
      </CommandDialog>
    </>
  );
}
