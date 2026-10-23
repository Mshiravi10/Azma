import { useState, type FormEvent } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { ProjectsApi } from '@/lib/queries/endpoints';
import { Card, CardContent } from '@/components/ui/card';
import { PageHeader } from '@/components/common/PageHeader';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/common/Skeletons';
import { EmptyState } from '@/components/common/EmptyState';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Badge } from '@/components/ui/badge';
import { FolderKanban, Plus, Archive, ArchiveRestore, MoreVertical, X } from 'lucide-react';
import { fmtDate } from '@/lib/date';
import { formatPercent } from '@/lib/utils';
import { toast } from 'sonner';
import { extractError } from '@/lib/api';
import { useAuthStore } from '@/lib/auth/store';
import { isWorkspaceAdmin } from '@/lib/auth/permissions';
import { motion, AnimatePresence } from 'framer-motion';

const PROJECT_COLORS = [
  'from-violet-500 to-indigo-500',
  'from-blue-500 to-cyan-500',
  'from-emerald-500 to-teal-500',
  'from-orange-500 to-amber-500',
  'from-pink-500 to-rose-500',
  'from-fuchsia-500 to-purple-500',
];

function getProjectColor(id: string): string {
  let hash = 0;
  for (let i = 0; i < id.length; i++) hash = id.charCodeAt(i) + ((hash << 5) - hash);
  return PROJECT_COLORS[Math.abs(hash) % PROJECT_COLORS.length];
}

export default function ProjectsPage() {
  const qc = useQueryClient();
  const me = useAuthStore((s) => s.user);
  const canManageProjects = isWorkspaceAdmin(me);
  const [showNew, setShowNew] = useState(false);
  const [code, setCode] = useState('');
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');

  const { data, isLoading } = useQuery({
    queryKey: ['projects', { includeArchived: true }],
    queryFn: () => ProjectsApi.list({ includeArchived: true }),
  });

  const createMut = useMutation({
    mutationFn: () => ProjectsApi.create({ code, name, description }),
    onSuccess: () => {
      toast.success('پروژه ساخته شد');
      qc.invalidateQueries({ queryKey: ['projects'] });
      setCode('');
      setName('');
      setDescription('');
      setShowNew(false);
    },
    onError: (err) => toast.error(extractError(err)),
  });

  const archiveMut = useMutation({
    mutationFn: (p: { id: string; archived: boolean }) =>
      p.archived ? ProjectsApi.unarchive(p.id) : ProjectsApi.archive(p.id),
    onSuccess: (_d, v) => {
      toast.success(v.archived ? 'پروژه فعال شد' : 'پروژه آرشیو شد');
      qc.invalidateQueries({ queryKey: ['projects'] });
    },
    onError: (err) => toast.error(extractError(err)),
  });

  function onCreate(e: FormEvent) {
    e.preventDefault();
    createMut.mutate();
  }

  const active = data?.filter((p) => !p.isArchived) ?? [];
  const archived = data?.filter((p) => p.isArchived) ?? [];

  return (
    <div className="space-y-6">
      <PageHeader
        title="پروژه‌ها"
        description="هر پروژه شامل سناریوها، اجراها و گزارش‌های مستقل است."
        actions={
          canManageProjects ? (
            <Button onClick={() => setShowNew(true)}>
              <Plus className="size-4" /> پروژه جدید
            </Button>
          ) : undefined
        }
      />

      <AnimatePresence>
        {showNew && (
          <motion.div
            initial={{ opacity: 0, height: 0 }}
            animate={{ opacity: 1, height: 'auto' }}
            exit={{ opacity: 0, height: 0 }}
            transition={{ duration: 0.2 }}
            className="overflow-hidden"
          >
            <Card variant="raised">
              <CardContent className="p-5">
                <div className="flex items-center justify-between mb-4">
                  <h3 className="font-semibold text-sm">پروژه جدید</h3>
                  <Button variant="ghost" size="icon-sm" onClick={() => setShowNew(false)}>
                    <X className="size-4" />
                  </Button>
                </div>
                <form onSubmit={onCreate} className="grid grid-cols-1 md:grid-cols-3 gap-4">
                  <div>
                    <Label>کد (یکتا)</Label>
                    <Input
                      value={code}
                      onChange={(e) => setCode(e.target.value.toUpperCase())}
                      placeholder="MOB"
                      maxLength={10}
                      required
                    />
                  </div>
                  <div className="md:col-span-2">
                    <Label>نام پروژه</Label>
                    <Input value={name} onChange={(e) => setName(e.target.value)} required />
                  </div>
                  <div className="md:col-span-3">
                    <Label>توضیحات</Label>
                    <Textarea
                      value={description}
                      onChange={(e) => setDescription(e.target.value)}
                    />
                  </div>
                  <div className="md:col-span-3 flex justify-end gap-2">
                    <Button type="button" variant="outline" onClick={() => setShowNew(false)}>
                      انصراف
                    </Button>
                    <Button type="submit" loading={createMut.isPending}>
                      ساخت پروژه
                    </Button>
                  </div>
                </form>
              </CardContent>
            </Card>
          </motion.div>
        )}
      </AnimatePresence>

      {isLoading ? (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {Array.from({ length: 6 }).map((_, i) => (
            <Skeleton key={i} className="h-44" />
          ))}
        </div>
      ) : !data || data.length === 0 ? (
        <EmptyState
          icon={<FolderKanban className="size-7" />}
          title="هنوز پروژه‌ای ساخته نشده"
          description="اولین پروژه‌ خود را بسازید تا سناریوها و اجراها را سازمان‌دهی کنید."
          action={
            canManageProjects ? (
            <Button onClick={() => setShowNew(true)}>
              <Plus className="size-4" /> ساخت اولین پروژه
            </Button>
            ) : undefined
          }
        />
      ) : (
        <>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {active.map((p) => (
              <motion.div
                key={p.id}
                layout
                initial={{ opacity: 0, scale: 0.97 }}
                animate={{ opacity: 1, scale: 1 }}
                transition={{ duration: 0.2 }}
              >
                <Card className="group h-full flex flex-col">
                  <CardContent className="p-5 flex-1 flex flex-col">
                    <div className="flex items-start justify-between gap-2 mb-3">
                      <div className="flex items-center gap-3">
                        <div
                          className={`size-11 rounded-xl bg-gradient-to-br ${getProjectColor(p.id)} text-white font-bold flex items-center justify-center shadow-soft text-sm`}
                        >
                          {p.code.slice(0, 2)}
                        </div>
                        <div>
                          <Link
                            to={`/projects/${p.id}`}
                            className="font-semibold text-sm hover:text-primary transition-base block leading-snug"
                          >
                            {p.name}
                          </Link>
                          <div className="text-xs text-muted-foreground font-mono">{p.code}</div>
                        </div>
                      </div>
                    </div>
                    <p className="text-xs text-muted-foreground line-clamp-2 flex-1 leading-relaxed">
                      {p.description || 'توضیحی برای این پروژه ثبت نشده است.'}
                    </p>
                    <div className="mt-4 pt-3 border-t border-border flex items-center justify-between gap-2">
                      <div className="text-xs text-muted-foreground">{fmtDate(p.createdAtUtc)}</div>
                      <div className="flex items-center gap-1 opacity-0 group-hover:opacity-100 focus-within:opacity-100 transition-base">
                        {canManageProjects && (
                          <Button
                            size="icon-sm"
                            variant="ghost"
                            title={p.isArchived ? 'بازگردانی' : 'آرشیو'}
                            onClick={() => archiveMut.mutate({ id: p.id, archived: p.isArchived })}
                          >
                            {p.isArchived ? (
                              <ArchiveRestore className="size-3.5" />
                            ) : (
                              <Archive className="size-3.5" />
                            )}
                          </Button>
                        )}
                        <Button size="icon-sm" variant="ghost" asChild>
                          <Link to={`/projects/${p.id}`}>
                            <MoreVertical className="size-3.5" />
                          </Link>
                        </Button>
                      </div>
                    </div>
                  </CardContent>
                </Card>
              </motion.div>
            ))}
          </div>

          {archived.length > 0 && (
            <details className="group">
              <summary className="flex items-center gap-2 text-sm text-muted-foreground cursor-pointer hover:text-foreground transition-base list-none">
                <span className="size-4 border border-current rounded-sm flex items-center justify-center text-[10px] transition-base group-open:rotate-90">▶</span>
                آرشیو شده‌ها ({archived.length})
              </summary>
              <div className="mt-3 grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                {archived.map((p) => (
                  <Card key={p.id} variant="flat" className="group opacity-60 hover:opacity-100 transition-base">
                    <CardContent className="p-5">
                      <div className="flex items-center gap-3 mb-2">
                        <div
                          className={`size-9 rounded-lg bg-gradient-to-br ${getProjectColor(p.id)} text-white font-bold flex items-center justify-center text-xs`}
                        >
                          {p.code.slice(0, 2)}
                        </div>
                        <div>
                          <div className="font-medium text-sm">{p.name}</div>
                          <div className="text-xs text-muted-foreground font-mono">{p.code}</div>
                        </div>
                        <Badge variant="muted" size="sm" className="ms-auto">
                          آرشیو
                        </Badge>
                      </div>
                      {canManageProjects && (
                      <Button
                        size="sm"
                        variant="ghost"
                        className="w-full mt-2"
                        onClick={() => archiveMut.mutate({ id: p.id, archived: true })}
                      >
                        <ArchiveRestore className="size-3.5" /> بازگردانی
                      </Button>
                      )}
                    </CardContent>
                  </Card>
                ))}
              </div>
            </details>
          )}
        </>
      )}
    </div>
  );
}
