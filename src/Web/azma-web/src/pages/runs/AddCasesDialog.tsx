import { useState, useMemo, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { RunsApi, TestCasesApi, ProjectsApi } from '@/lib/queries/endpoints';
import type { TestCasePriority } from '@/lib/types';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select } from '@/components/ui/native-select';
import { Badge } from '@/components/ui/badge';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { X, Search, CheckSquare, Square, Plus } from 'lucide-react';
import { Skeleton } from '@/components/common/Skeletons';
import { toast } from 'sonner';
import { extractError } from '@/lib/api';
import { cn } from '@/lib/utils';

interface Props {
  runId: string;
  projectId: string;
  existingCaseIds: string[];
  onClose: () => void;
}

export default function AddCasesDialog({ runId, projectId, existingCaseIds, onClose }: Props) {
  const qc = useQueryClient();
  const [search, setSearch] = useState('');
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [filterSuiteId, setFilterSuiteId] = useState('');
  const [filterPriority, setFilterPriority] = useState('');
  const [page, setPage] = useState(1);
  const pageSize = 50;

  const existingSet = useMemo(() => new Set(existingCaseIds), [existingCaseIds]);

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
      page,
      pageSize,
    }),
    [projectId, search, filterSuiteId, filterPriority, page],
  );

  const { data: testcases, isLoading } = useQuery({
    queryKey: ['testcases-add', listParams],
    queryFn: () => TestCasesApi.list(listParams),
    enabled: !!projectId,
  });

  const items = useMemo(() => {
    if (!testcases) return [];
    return testcases.items.filter((tc) => !existingSet.has(tc.id));
  }, [testcases, existingSet]);

  const totalPages = testcases ? Math.max(1, Math.ceil(testcases.total / pageSize)) : 1;

  const addMut = useMutation({
    mutationFn: () => RunsApi.addCases(runId, Array.from(selected)),
    onSuccess: () => {
      toast.success(`${selected.size} سناریو به اجرا اضافه شد`);
      qc.invalidateQueries({ queryKey: ['run-cases', runId] });
      qc.invalidateQueries({ queryKey: ['run', runId] });
      onClose();
    },
    onError: (err) => toast.error(extractError(err)),
  });

  function toggle(id: string) {
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  return (
    <Dialog open onOpenChange={(next) => !next && onClose()}>
      <DialogContent className="flex max-h-[85vh] flex-col gap-0 overflow-hidden p-0 sm:max-w-3xl" showCloseButton={false}>
        <DialogHeader className="border-b p-5">
          <DialogTitle>افزودن سناریو به اجرا</DialogTitle>
          <DialogDescription>سناریوهای موجود در اجرا نمایش داده نمی‌شوند.</DialogDescription>
        </DialogHeader>

        <div className="flex-1 overflow-y-auto p-5 space-y-3">
          <div className="flex items-center gap-3">
            <div className="relative flex-1">
              <Search className="absolute right-3 top-1/2 -translate-y-1/2 size-4 text-muted-foreground pointer-events-none" />
              <Input
                value={search}
                onChange={(e) => { setSearch(e.target.value); setPage(1); }}
                placeholder="جستجو در نام یا کد سناریو…"
                className="ps-10"
              />
            </div>
            <Select
              value={filterSuiteId}
              onChange={(e) => { setFilterSuiteId(e.target.value); setPage(1); }}
              className="w-40"
            >
              <option value="">همه سوئیت‌ها</option>
              {suites?.map((s) => (
                <option key={s.id} value={s.id}>{s.name}</option>
              ))}
            </Select>
            <Select
              value={filterPriority}
              onChange={(e) => { setFilterPriority(e.target.value); setPage(1); }}
              className="w-32"
            >
              <option value="">همه اولویت‌ها</option>
              <option value="Critical">بحرانی</option>
              <option value="High">بالا</option>
              <option value="Medium">متوسط</option>
              <option value="Low">کم</option>
            </Select>
          </div>

          <div className="border border-border rounded-lg max-h-72 overflow-y-auto">
            {isLoading ? (
              <div className="p-4 space-y-2">
                {Array.from({ length: 4 }).map((_, i) => (
                  <Skeleton key={i} className="h-10" />
                ))}
              </div>
            ) : items.length === 0 ? (
              <div className="p-6 text-center text-sm text-muted-foreground">
                سناریوی جدیدی یافت نشد (یا همه قبلاً اضافه شده‌اند).
              </div>
            ) : (
              <ul className="divide-y divide-border">
                {items.map((tc) => (
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
                      {tc.tags.slice(0, 2).map((t) => (
                        <Badge key={t} variant="muted" size="sm">{t}</Badge>
                      ))}
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </div>

          {testcases && testcases.total > pageSize && (
            <div className="flex items-center justify-between text-xs text-muted-foreground">
              <span>
                صفحه {page} از {totalPages}
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

          {selected.size > 0 && (
            <div className="flex items-center gap-2 p-2.5 rounded-lg bg-success/5 border border-success/20 text-sm">
              <Badge variant="default" size="lg">{selected.size}</Badge>
              <span className="text-muted-foreground">سناریو برای افزودن</span>
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

        <div className="flex justify-end gap-2 border-t p-5">
          <Button variant="outline" onClick={onClose}>انصراف</Button>
          <Button
            onClick={() => addMut.mutate()}
            loading={addMut.isPending}
            disabled={selected.size === 0}
          >
            افزودن {selected.size > 0 ? `(${selected.size})` : ''}
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
