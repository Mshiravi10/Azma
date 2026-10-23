import { useEffect, useState, useMemo, type FormEvent, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ProjectsApi, RunsApi, TestCasesApi } from '@/lib/queries/endpoints';
import type { TestCasePriority, TestCaseSummary } from '@/lib/types';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select } from '@/components/ui/native-select';
import { Badge } from '@/components/ui/badge';
import { X, Search, PlayCircle, CheckSquare, Square, Filter, ChevronDown, ChevronUp, RotateCcw } from 'lucide-react';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Skeleton } from '@/components/common/Skeletons';
import { toast } from 'sonner';
import { extractError } from '@/lib/api';
import { motion, AnimatePresence } from 'framer-motion';
import { cn } from '@/lib/utils';

const PRIORITY_OPTIONS: { value: TestCasePriority; label: string }[] = [
  { value: 'Critical', label: 'بحرانی' },
  { value: 'High', label: 'بالا' },
  { value: 'Medium', label: 'متوسط' },
  { value: 'Low', label: 'کم' },
];

const TYPE_OPTIONS: { value: string; label: string }[] = [
  { value: 'Functional', label: 'عملکردی' },
  { value: 'Integration', label: 'یکپارچگی' },
  { value: 'EndToEnd', label: 'سرتاسری' },
  { value: 'Performance', label: 'کارایی' },
  { value: 'Security', label: 'امنیت' },
  { value: 'Usability', label: 'کاربردپذیری' },
  { value: 'Other', label: 'سایر' },
];

interface Props {
  onClose: () => void;
  /** Pre-selected test case IDs (for Quick Run from test case list) */
  preSelectedIds?: string[];
  /** Pre-selected project ID */
  preProjectId?: string;
  /** For re-run: source run info */
  rerunSource?: { runId: string; runName: string; caseIds: string[]; projectId: string };
}

export default function NewRunDialog({ onClose, preSelectedIds, preProjectId, rerunSource }: Props) {
  const navigate = useNavigate();
  const qc = useQueryClient();
  const [projectId, setProjectId] = useState(rerunSource?.projectId ?? preProjectId ?? '');
  const [name, setName] = useState(rerunSource ? `اجرای مجدد — ${rerunSource.runName}` : '');
  const [description, setDescription] = useState('');
  const [environment, setEnvironment] = useState('');
  const [dueDate, setDueDate] = useState('');
  const [search, setSearch] = useState('');
  const [selected, setSelected] = useState<Set<string>>(
    new Set(rerunSource?.caseIds ?? preSelectedIds ?? []),
  );
  const [selectedAutomation, setSelectedAutomation] = useState<Map<string, boolean>>(new Map());
  const [showFilters, setShowFilters] = useState(false);
  const [filterSuiteId, setFilterSuiteId] = useState('');
  const [filterPriority, setFilterPriority] = useState('');
  const [filterType, setFilterType] = useState('');
  const [filterTag, setFilterTag] = useState('');
  const [page, setPage] = useState(1);
  const pageSize = 50;

  const { data: projects } = useQuery({ queryKey: ['projects'], queryFn: () => ProjectsApi.list() });
  const { data: suites } = useQuery({
    queryKey: ['suites', projectId],
    queryFn: () => ProjectsApi.suites(projectId),
    enabled: !!projectId,
  });

  const listParams = useMemo(
    () => ({
      projectId,
      search: search || undefined,
      status: 'Active' as const,
      suiteId: filterSuiteId || undefined,
      priority: (filterPriority as TestCasePriority) || undefined,
      tag: filterTag || undefined,
      page,
      pageSize,
    }),
    [projectId, search, filterSuiteId, filterPriority, filterTag, page],
  );

  const { data: testcases, isLoading: tcLoading } = useQuery({
    queryKey: ['testcases-pick', listParams],
    queryFn: () => TestCasesApi.list(listParams),
    enabled: !!projectId,
  });

  useEffect(() => {
    if (!testcases) return;
    setSelectedAutomation((current) => {
      const next = new Map(current);
      testcases.items.forEach((tc) => next.set(tc.id, tc.hasAutomation));
      return next;
    });
  }, [testcases]);

  const allTags = useMemo(() => {
    if (!testcases) return [];
    const tags = new Set<string>();
    testcases.items.forEach((tc) => tc.tags.forEach((t) => tags.add(t)));
    return [...tags].sort();
  }, [testcases]);

  const activeFilterCount = [filterSuiteId, filterPriority, filterType, filterTag].filter(Boolean).length;

  const filteredItems = useMemo(() => {
    if (!testcases) return [];
    let items = testcases.items;
    if (filterType) {
      items = items.filter((tc) => tc.type === filterType);
    }
    return items;
  }, [testcases, filterType]);

  const totalPages = testcases ? Math.max(1, Math.ceil(testcases.total / pageSize)) : 1;
  const allSelectedAutomatic =
    selected.size > 0 && Array.from(selected).every((id) => selectedAutomation.get(id) === true);

  const createMut = useMutation({
    mutationFn: () =>
      RunsApi.create({
        projectId,
        name,
        description: description || undefined,
        environment: environment || undefined,
        dueDateUtc: dueDate ? new Date(dueDate).toISOString() : undefined,
        testCaseIds: Array.from(selected),
        autoExecute: allSelectedAutomatic,
      }),
    onSuccess: (run) => {
      toast.success(allSelectedAutomatic ? 'اجرا ساخته شد و اجرای خودکار در صف قرار گرفت' : 'اجرا ساخته شد');
      qc.invalidateQueries({ queryKey: ['runs'] });
      onClose();
      navigate(allSelectedAutomatic ? `/runs/${run.id}` : `/runs/${run.id}/execute`);
    },
    onError: (err) => toast.error(extractError(err)),
  });

  function toggle(id: string) {
    const next = new Set(selected);
    if (next.has(id)) next.delete(id);
    else next.add(id);
    setSelected(next);
  }

  function togglePage() {
    if (!filteredItems.length) return;
    const pageIds = filteredItems.map((t) => t.id);
    const allOnPage = pageIds.every((id) => selected.has(id));
    const next = new Set(selected);
    if (allOnPage) {
      pageIds.forEach((id) => next.delete(id));
    } else {
      pageIds.forEach((id) => next.add(id));
    }
    setSelected(next);
  }

  const selectAllFiltered = useCallback(async () => {
    if (!projectId) return;
    const all = await TestCasesApi.list({
      ...listParams,
      page: 1,
      pageSize: 2000,
    });
    const next = new Set(selected);
    let items = all.items;
    if (filterType) items = items.filter((tc) => tc.type === filterType);
    setSelectedAutomation((current) => {
      const nextMeta = new Map(current);
      items.forEach((tc) => nextMeta.set(tc.id, tc.hasAutomation));
      return nextMeta;
    });
    items.forEach((tc) => next.add(tc.id));
    setSelected(next);
    toast.success(`${items.length} سناریو انتخاب شد`);
  }, [projectId, listParams, filterType, selected]);

  function clearFilters() {
    setFilterSuiteId('');
    setFilterPriority('');
    setFilterType('');
    setFilterTag('');
    setSearch('');
    setPage(1);
  }

  function onSubmit(e: FormEvent) {
    e.preventDefault();
    if (!projectId || !name.trim() || selected.size === 0) {
      toast.error('پروژه، نام و حداقل یک سناریو الزامی است');
      return;
    }
    createMut.mutate();
  }

  const pageAllSelected = filteredItems.length > 0 && filteredItems.every((t) => selected.has(t.id));

  return (
    <Dialog open onOpenChange={(next) => !next && onClose()}>
      <DialogContent className="flex max-h-[90vh] flex-col gap-0 overflow-hidden p-0 sm:max-w-4xl" showCloseButton={false}>
        <DialogHeader className="border-b p-5">
          <DialogTitle>{rerunSource ? 'اجرای مجدد' : 'اجرای جدید'}</DialogTitle>
          <DialogDescription>
            {rerunSource
              ? `ساخت اجرای جدید بر اساس ${rerunSource.runName}`
              : 'سناریوهایی که می‌خواهید تست شوند را انتخاب کنید.'}
          </DialogDescription>
        </DialogHeader>

        {/* Body */}
        <form onSubmit={onSubmit} className="flex-1 overflow-y-auto p-5 space-y-4">
          {/* Basic fields */}
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <Label>پروژه *</Label>
              <Select
                value={projectId}
                onChange={(e) => {
                  setProjectId(e.target.value);
                  setSelected(new Set());
                  setSelectedAutomation(new Map());
                  clearFilters();
                }}
                disabled={!!rerunSource}
                required
              >
                <option value="">انتخاب کنید</option>
                {projects?.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.code} — {p.name}
                  </option>
                ))}
              </Select>
            </div>
            <div>
              <Label>نام اجرا *</Label>
              <Input value={name} onChange={(e) => setName(e.target.value)} required placeholder="Sprint 12 — Smoke" />
            </div>
            <div className="md:col-span-2">
              <Label>توضیحات</Label>
              <Textarea value={description} onChange={(e) => setDescription(e.target.value)} rows={2} />
            </div>
            <div>
              <Label>محیط اجرا</Label>
              <Input value={environment} onChange={(e) => setEnvironment(e.target.value)} placeholder="Production, Staging, QA..." />
            </div>
            <div>
              <Label>مهلت اجرا</Label>
              <Input type="date" value={dueDate} onChange={(e) => setDueDate(e.target.value)} />
            </div>
          </div>

          {/* Test case picker */}
          {projectId && (
            <div className="space-y-3">
              <div className="flex items-center justify-between gap-3">
                <h4 className="font-medium text-sm">انتخاب سناریوها</h4>
                <div className="flex items-center gap-2">
                  <Badge variant="info">{selected.size} انتخاب شده</Badge>
                  <Button
                    type="button"
                    variant={showFilters ? 'subtle' : 'ghost'}
                    size="xs"
                    onClick={() => setShowFilters(!showFilters)}
                  >
                    <Filter className="size-3.5" />
                    فیلترها
                    {activeFilterCount > 0 && (
                      <Badge variant="default" size="sm">{activeFilterCount}</Badge>
                    )}
                    {showFilters ? <ChevronUp className="size-3" /> : <ChevronDown className="size-3" />}
                  </Button>
                </div>
              </div>

              {/* Smart filters panel */}
              <AnimatePresence>
                {showFilters && (
                  <motion.div
                    initial={{ height: 0, opacity: 0 }}
                    animate={{ height: 'auto', opacity: 1 }}
                    exit={{ height: 0, opacity: 0 }}
                    className="overflow-hidden"
                  >
                    <div className="grid grid-cols-2 md:grid-cols-4 gap-3 p-3 rounded-lg bg-secondary/30 border border-border">
                      <div>
                        <div className="text-[11px] text-muted-foreground mb-1 font-medium">سوئیت</div>
                        <Select
                          value={filterSuiteId}
                          onChange={(e) => { setFilterSuiteId(e.target.value); setPage(1); }}
                        >
                          <option value="">همه</option>
                          {suites?.map((s) => (
                            <option key={s.id} value={s.id}>{s.name}</option>
                          ))}
                        </Select>
                      </div>
                      <div>
                        <div className="text-[11px] text-muted-foreground mb-1 font-medium">اولویت</div>
                        <Select
                          value={filterPriority}
                          onChange={(e) => { setFilterPriority(e.target.value); setPage(1); }}
                        >
                          <option value="">همه</option>
                          {PRIORITY_OPTIONS.map((p) => (
                            <option key={p.value} value={p.value}>{p.label}</option>
                          ))}
                        </Select>
                      </div>
                      <div>
                        <div className="text-[11px] text-muted-foreground mb-1 font-medium">نوع</div>
                        <Select
                          value={filterType}
                          onChange={(e) => { setFilterType(e.target.value); setPage(1); }}
                        >
                          <option value="">همه</option>
                          {TYPE_OPTIONS.map((t) => (
                            <option key={t.value} value={t.value}>{t.label}</option>
                          ))}
                        </Select>
                      </div>
                      <div>
                        <div className="text-[11px] text-muted-foreground mb-1 font-medium">برچسب</div>
                        <Select
                          value={filterTag}
                          onChange={(e) => { setFilterTag(e.target.value); setPage(1); }}
                        >
                          <option value="">همه</option>
                          {allTags.map((t) => (
                            <option key={t} value={t}>{t}</option>
                          ))}
                        </Select>
                      </div>
                      <div className="col-span-2 md:col-span-4 flex items-center gap-2">
                        <Button type="button" variant="outline" size="xs" onClick={clearFilters}>
                          پاک‌سازی فیلترها
                        </Button>
                        <Button type="button" variant="subtle" size="xs" onClick={selectAllFiltered}>
                          انتخاب همه نتایج فیلتر
                        </Button>
                      </div>
                    </div>
                  </motion.div>
                )}
              </AnimatePresence>

              {/* Search */}
              <div className="relative">
                <Search className="absolute right-3 top-1/2 -translate-y-1/2 size-4 text-muted-foreground pointer-events-none" />
                <Input
                  value={search}
                  onChange={(e) => { setSearch(e.target.value); setPage(1); }}
                  placeholder="جستجو در نام یا کد سناریو…"
                  className="ps-10"
                />
              </div>

              {/* Test case list */}
              <div className="border border-border rounded-lg max-h-80 overflow-y-auto">
                {tcLoading ? (
                  <div className="p-4 space-y-2">
                    {Array.from({ length: 4 }).map((_, i) => (
                      <Skeleton key={i} className="h-10" />
                    ))}
                  </div>
                ) : filteredItems.length === 0 ? (
                  <div className="p-6 text-center text-sm text-muted-foreground">سناریوی فعالی یافت نشد.</div>
                ) : (
                  <>
                    <button
                      type="button"
                      onClick={togglePage}
                      className="w-full px-4 py-2 text-xs flex items-center gap-2 border-b border-border hover:bg-accent/50 sticky top-0 bg-card z-10"
                    >
                      {pageAllSelected ? (
                        <CheckSquare className="size-4 text-primary" />
                      ) : (
                        <Square className="size-4" />
                      )}
                      انتخاب صفحه فعلی
                      <span className="text-muted-foreground">({filteredItems.length} مورد)</span>
                    </button>
                    <ul className="divide-y divide-border">
                      {filteredItems.map((tc) => (
                        <li key={tc.id}>
                          <button
                            type="button"
                            onClick={() => toggle(tc.id)}
                            className="w-full flex items-center gap-3 px-4 py-2.5 cursor-pointer hover:bg-accent/40 text-sm text-start"
                          >
                            <div
                              className={cn(
                                'shrink-0 size-4.5 rounded border flex items-center justify-center transition-colors',
                                selected.has(tc.id)
                                  ? 'bg-primary border-primary text-primary-foreground'
                                  : 'border-border',
                              )}
                            >
                              {selected.has(tc.id) && <CheckSquare className="size-3" />}
                            </div>
                            <span className="font-mono text-xs text-muted-foreground w-20 shrink-0">{tc.code}</span>
                            <span className="flex-1 truncate">{tc.title}</span>
                            <div className="flex items-center gap-1.5 shrink-0">
                              {tc.automationEngine === 'ScenarioRecording' && (
                                <Badge variant="info" size="sm">
                                  Recorded
                                </Badge>
                              )}
                              <PriorityDot priority={tc.priority} />
                              {tc.tags.slice(0, 2).map((t) => (
                                <Badge key={t} variant="muted" size="sm">{t}</Badge>
                              ))}
                            </div>
                          </button>
                        </li>
                      ))}
                    </ul>
                  </>
                )}
              </div>

              {/* Pagination */}
              {testcases && testcases.total > pageSize && (
                <div className="flex items-center justify-between text-xs text-muted-foreground">
                  <span>
                    مجموع {testcases.total} سناریو · صفحه {page} از {totalPages}
                  </span>
                  <div className="flex items-center gap-1">
                    <Button type="button" variant="outline" size="xs" disabled={page <= 1} onClick={() => setPage(page - 1)}>
                      قبلی
                    </Button>
                    <Button type="button" variant="outline" size="xs" disabled={page >= totalPages} onClick={() => setPage(page + 1)}>
                      بعدی
                    </Button>
                  </div>
                </div>
              )}

              {/* Selected summary */}
              {selected.size > 0 && (
                <div className="flex items-center gap-2 p-2.5 rounded-lg bg-primary/5 border border-primary/20 text-sm">
                  <Badge variant="default" size="lg">{selected.size}</Badge>
                  <span className="text-muted-foreground">سناریو انتخاب شده</span>
                  <Button
                    type="button"
                    variant="ghost"
                    size="xs"
                    onClick={() => setSelected(new Set())}
                    className="ms-auto text-muted-foreground"
                  >
                    <X className="size-3" /> پاک کردن
                  </Button>
                </div>
              )}
            </div>
          )}
        </form>

        <div className="flex justify-end gap-2 border-t p-5">
          <Button variant="outline" onClick={onClose}>انصراف</Button>
          <Button onClick={onSubmit} loading={createMut.isPending} disabled={selected.size === 0}>
            {allSelectedAutomatic ? 'ساخت و اجرای خودکار' : rerunSource ? 'ساخت اجرای مجدد' : 'ساخت و شروع اجرا'}
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}

function PriorityDot({ priority }: { priority: string }) {
  const color =
    priority === 'Critical' ? 'bg-destructive' :
    priority === 'High' ? 'bg-warning' :
    priority === 'Medium' ? 'bg-info' :
    'bg-muted-foreground/40';
  return <span className={cn('size-2 rounded-full shrink-0', color)} title={priority} />;
}
