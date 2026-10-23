import { useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CustomFieldsApi, ProjectsApi } from '@/lib/queries/endpoints';
import type { CustomFieldDefinition, CustomFieldType } from '@/lib/types';
import { PageHeader } from '@/components/common/PageHeader';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select } from '@/components/ui/native-select';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/common/Skeletons';
import { EmptyState } from '@/components/common/EmptyState';
import {
  Plus, Pencil, Trash2, GripVertical, X, Save, Sparkles,
  Type as TypeIcon, AlignLeft, Hash, Calendar, ToggleLeft,
  ChevronDown, List, Link as LinkIcon,
} from 'lucide-react';
import { toast } from 'sonner';
import { extractError } from '@/lib/api';
import { motion, AnimatePresence } from 'framer-motion';
import { cn } from '@/lib/utils';

const TYPE_META: Record<CustomFieldType, { label: string; icon: React.ElementType; color: string }> = {
  Text:        { label: 'متن کوتاه',   icon: TypeIcon,    color: 'text-info' },
  LongText:    { label: 'متن بلند',    icon: AlignLeft,   color: 'text-info' },
  Number:      { label: 'عدد',          icon: Hash,        color: 'text-primary' },
  Date:        { label: 'تاریخ',        icon: Calendar,    color: 'text-success' },
  Boolean:     { label: 'بله/خیر',     icon: ToggleLeft,  color: 'text-warning' },
  Select:      { label: 'انتخابی',      icon: ChevronDown, color: 'text-primary' },
  MultiSelect: { label: 'چندگزینه‌ای', icon: List,        color: 'text-primary' },
  Url:         { label: 'لینک',         icon: LinkIcon,    color: 'text-info' },
};

interface DraftField {
  id?: string;
  key: string;
  label: string;
  description: string;
  type: CustomFieldType;
  options: string[];
  required: boolean;
  position: number;
}

const EMPTY_DRAFT: DraftField = {
  key: '', label: '', description: '', type: 'Text',
  options: [], required: false, position: 0,
};

export default function CustomFieldsPage() {
  const params = useParams<{ projectId?: string }>();
  const [search, setSearch] = useSearchParams();
  const navigate = useNavigate();
  const qc = useQueryClient();

  const { data: projects } = useQuery({ queryKey: ['projects'], queryFn: () => ProjectsApi.list() });
  const preferredProjectId = params.projectId ?? search.get('projectId');
  const projectId =
    projects == null
      ? (preferredProjectId ?? null)
      : (preferredProjectId && projects.some((p) => p.id === preferredProjectId)
          ? preferredProjectId
          : (projects[0]?.id ?? null));

  useEffect(() => {
    if (!projects || !projectId) return;
    if (params.projectId !== projectId) {
      navigate(`/settings/custom-fields/${projectId}`, { replace: true });
    }
  }, [params.projectId, projectId, projects, navigate]);

  const { data: fields, isLoading } = useQuery({
    queryKey: ['custom-fields', projectId],
    queryFn: () => CustomFieldsApi.list(projectId!, true),
    enabled: !!projectId,
  });

  const [editing, setEditing] = useState<DraftField | null>(null);

  const createMut = useMutation({
    mutationFn: (d: DraftField) => CustomFieldsApi.create({
      projectId: projectId!, key: d.key, label: d.label,
      description: d.description || null, type: d.type,
      options: d.type === 'Select' || d.type === 'MultiSelect' ? d.options : null,
      required: d.required, position: d.position,
    }),
    onSuccess: () => {
      toast.success('فیلد ساخته شد');
      qc.invalidateQueries({ queryKey: ['custom-fields', projectId] });
      setEditing(null);
    },
    onError: (e) => toast.error(extractError(e)),
  });

  const updateMut = useMutation({
    mutationFn: (d: DraftField) => CustomFieldsApi.update(d.id!, {
      label: d.label, description: d.description || null,
      options: d.type === 'Select' || d.type === 'MultiSelect' ? d.options : null,
      required: d.required, position: d.position,
    }),
    onSuccess: () => {
      toast.success('تغییرات ذخیره شد');
      qc.invalidateQueries({ queryKey: ['custom-fields', projectId] });
      setEditing(null);
    },
    onError: (e) => toast.error(extractError(e)),
  });

  const deleteMut = useMutation({
    mutationFn: (id: string) => CustomFieldsApi.remove(id),
    onSuccess: () => {
      toast.success('فیلد حذف شد');
      qc.invalidateQueries({ queryKey: ['custom-fields', projectId] });
    },
    onError: (e) => toast.error(extractError(e)),
  });

  const reorderMut = useMutation({
    mutationFn: (d: CustomFieldDefinition & { newPosition: number }) =>
      CustomFieldsApi.update(d.id, {
        label: d.label, description: d.description,
        options: d.options, required: d.required, position: d.newPosition,
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['custom-fields', projectId] }),
  });

  const sorted = useMemo(
    () => (fields ?? []).slice().sort((a, b) => a.position - b.position),
    [fields],
  );

  function move(idx: number, dir: -1 | 1) {
    const target = idx + dir;
    if (target < 0 || target >= sorted.length) return;
    reorderMut.mutate({ ...sorted[idx], newPosition: sorted[target].position });
    reorderMut.mutate({ ...sorted[target], newPosition: sorted[idx].position });
  }

  if (!projectId)
    return <EmptyState icon={<Sparkles className="size-7" />} title="هیچ پروژه‌ای موجود نیست" description="ابتدا یک پروژه بسازید." />;

  return (
    <div className="space-y-6">
      <PageHeader
        title="فیلدهای سفارشی سناریوها"
        description="فیلدهای دلخواه را برای این پروژه تعریف کنید — هر چیزی از Browser/Device تا Owner و Sprint."
        actions={
          <Select
            value={projectId}
            onChange={(e) => navigate(`/settings/custom-fields/${e.target.value}`)}
            className="min-w-44"
          >
            {projects?.map((p) => (
              <option key={p.id} value={p.id}>{p.code} — {p.name}</option>
            ))}
          </Select>
        }
      />

      <div className="grid grid-cols-12 gap-6">
        {/* List */}
        <Card className="col-span-12 lg:col-span-8">
          <CardHeader className="flex flex-row items-center justify-between">
            <CardTitle>فیلدهای تعریف‌شده ({sorted.length})</CardTitle>
            <Button size="sm" onClick={() => setEditing({ ...EMPTY_DRAFT, position: (sorted.at(-1)?.position ?? 0) + 1 })}>
              <Plus className="size-4" /> فیلد جدید
            </Button>
          </CardHeader>
          <CardContent className="px-0 pb-0">
            {isLoading ? (
              <div className="p-6 space-y-2">
                {Array.from({ length: 4 }).map((_, i) => <Skeleton key={i} className="h-16" />)}
              </div>
            ) : sorted.length === 0 ? (
              <EmptyState
                icon={<Sparkles className="size-7" />}
                title="هیچ فیلد سفارشی تعریف نشده"
                description="با ساخت فیلدهایی مثل Browser, Device, Module به سناریوها ساختار بدهید."
                action={
                  <Button size="sm" onClick={() => setEditing({ ...EMPTY_DRAFT })}>
                    <Plus className="size-4" /> اولین فیلد را بسازید
                  </Button>
                }
              />
            ) : (
              <ul className="divide-y divide-border">
                {sorted.map((f, idx) => {
                  const meta = TYPE_META[f.type];
                  const Icon = meta.icon;
                  return (
                    <motion.li
                      key={f.id}
                      layout
                      initial={{ opacity: 0, y: 4 }}
                      animate={{ opacity: 1, y: 0 }}
                      className={cn(
                        'group px-6 py-3 flex items-center gap-3 hover:bg-accent/40 transition-colors',
                        f.isArchived && 'opacity-50',
                      )}
                    >
                      <div className="flex flex-col">
                        <button onClick={() => move(idx, -1)} disabled={idx === 0} className="hover:text-primary disabled:opacity-30">
                          <GripVertical className="size-3 -mb-1" />
                        </button>
                        <span className="text-[10px] text-muted-foreground tabular-nums">{idx + 1}</span>
                      </div>
                      <div className={cn('size-9 rounded-lg flex items-center justify-center bg-muted', meta.color)}>
                        <Icon className="size-4" />
                      </div>
                      <div className="flex-1 min-w-0">
                        <div className="flex items-center gap-2">
                          <span className="font-medium">{f.label}</span>
                          {f.required && <Badge variant="warning">الزامی</Badge>}
                          {f.isArchived && <Badge variant="muted">آرشیو</Badge>}
                        </div>
                        <div className="text-xs text-muted-foreground flex items-center gap-2 mt-0.5">
                          <code className="font-mono">{f.key}</code>
                          <span>•</span>
                          <span>{meta.label}</span>
                          {(f.type === 'Select' || f.type === 'MultiSelect') && f.options && (
                            <>
                              <span>•</span>
                              <span>{f.options.length} گزینه</span>
                            </>
                          )}
                        </div>
                      </div>
                      <div className="opacity-0 group-hover:opacity-100 flex items-center gap-1 transition-opacity">
                        <Button
                          size="icon-sm"
                          variant="ghost"
                          onClick={() =>
                            setEditing({
                              id: f.id, key: f.key, label: f.label,
                              description: f.description ?? '', type: f.type,
                              options: f.options ?? [], required: f.required,
                              position: f.position,
                            })
                          }
                        >
                          <Pencil className="size-3.5" />
                        </Button>
                        <Button
                          size="icon-sm"
                          variant="ghost"
                          onClick={() => {
                            if (confirm(`حذف فیلد «${f.label}»؟ مقادیر روی سناریوهای موجود حفظ می‌شوند.`)) {
                              deleteMut.mutate(f.id);
                            }
                          }}
                        >
                          <Trash2 className="size-3.5 text-destructive" />
                        </Button>
                      </div>
                    </motion.li>
                  );
                })}
              </ul>
            )}
          </CardContent>
        </Card>

        {/* Editor pane */}
        <div className="col-span-12 lg:col-span-4">
          <AnimatePresence mode="wait">
            {editing ? (
              <motion.div key="form" initial={{ opacity: 0, x: 8 }} animate={{ opacity: 1, x: 0 }} exit={{ opacity: 0, x: 8 }}>
                <FieldForm
                  draft={editing}
                  onChange={setEditing}
                  onCancel={() => setEditing(null)}
                  onSave={() => editing.id ? updateMut.mutate(editing) : createMut.mutate(editing)}
                  saving={createMut.isPending || updateMut.isPending}
                />
              </motion.div>
            ) : (
              <motion.div key="hint" initial={{ opacity: 0 }} animate={{ opacity: 1 }} className="rounded-2xl border border-dashed border-border p-6 text-center text-sm text-muted-foreground">
                <Sparkles className="size-6 mx-auto mb-3 text-info" />
                برای ساخت یا ویرایش، روی فیلد کلیک کنید یا "فیلد جدید" را بزنید.
              </motion.div>
            )}
          </AnimatePresence>
        </div>
      </div>
    </div>
  );
}

function FieldForm({
  draft, onChange, onCancel, onSave, saving,
}: {
  draft: DraftField;
  onChange: (d: DraftField) => void;
  onCancel: () => void;
  onSave: () => void;
  saving: boolean;
}) {
  const isNew = !draft.id;
  const showOptions = draft.type === 'Select' || draft.type === 'MultiSelect';
  const [optInput, setOptInput] = useState('');

  function autoKey(label: string) {
    return label
      .toLowerCase()
      .normalize('NFKD')
      .replace(/[^a-z0-9\u0600-\u06ff]+/g, '_')
      .replace(/^_+|_+$/g, '')
      .slice(0, 64);
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">{isNew ? 'فیلد جدید' : 'ویرایش فیلد'}</CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        <div>
          <Label>عنوان نمایشی *</Label>
          <Input
            value={draft.label}
            onChange={(e) => {
              const v = e.target.value;
              onChange({ ...draft, label: v, key: isNew && (!draft.key || draft.key === autoKey(draft.label)) ? autoKey(v) : draft.key });
            }}
            placeholder="مثلاً: مرورگر مورد آزمایش"
          />
        </div>
        <div>
          <Label>کلید (تغییرناپذیر)</Label>
          <Input
            value={draft.key}
            onChange={(e) => onChange({ ...draft, key: e.target.value })}
            disabled={!isNew}
            dir="ltr"
            className="font-mono"
            placeholder="browser"
          />
          <p className="text-[11px] text-muted-foreground mt-1">حروف کوچک، اعداد، آندرلاین. غیرقابل ویرایش پس از ساخت.</p>
        </div>
        <div>
          <Label>نوع داده *</Label>
          <Select
            value={draft.type}
            onChange={(e) => onChange({ ...draft, type: e.target.value as CustomFieldType })}
            disabled={!isNew}
          >
            {Object.entries(TYPE_META).map(([k, m]) => (
              <option key={k} value={k}>{m.label}</option>
            ))}
          </Select>
        </div>
        {showOptions && (
          <div>
            <Label>گزینه‌ها</Label>
            <div className="rounded-lg border border-input bg-background p-2 space-y-2">
              <div className="flex flex-wrap gap-1.5 min-h-[1.5rem]">
                {draft.options.length === 0 && (
                  <span className="text-xs text-muted-foreground italic">هنوز گزینه‌ای اضافه نشده.</span>
                )}
                {draft.options.map((opt) => (
                  <Badge key={opt} variant="secondary" className="cursor-pointer">
                    {opt}
                    <button
                      type="button"
                      onClick={() => onChange({ ...draft, options: draft.options.filter((o) => o !== opt) })}
                      className="ms-1"
                    >
                      <X className="size-3" />
                    </button>
                  </Badge>
                ))}
              </div>
              <div className="flex gap-1.5">
                <Input
                  value={optInput}
                  onChange={(e) => setOptInput(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter' && optInput.trim()) {
                      e.preventDefault();
                      const v = optInput.trim();
                      if (!draft.options.includes(v)) onChange({ ...draft, options: [...draft.options, v] });
                      setOptInput('');
                    }
                  }}
                  placeholder="گزینه + Enter"
                />
              </div>
            </div>
          </div>
        )}
        <div>
          <Label>توضیح کوتاه (اختیاری)</Label>
          <Textarea
            value={draft.description}
            onChange={(e) => onChange({ ...draft, description: e.target.value })}
            rows={2}
            placeholder="راهنمای کاربر برای پر کردن این فیلد"
          />
        </div>
        <label className="flex items-center gap-2 cursor-pointer">
          <input
            type="checkbox"
            checked={draft.required}
            onChange={(e) => onChange({ ...draft, required: e.target.checked })}
            className="size-4 accent-primary"
          />
          <span className="text-sm">پر کردن این فیلد الزامی است</span>
        </label>
        <div className="flex items-center gap-2 pt-2">
          <Button onClick={onSave} loading={saving} disabled={!draft.label || !draft.key} className="flex-1">
            <Save className="size-4" /> {isNew ? 'ساخت فیلد' : 'ذخیره'}
          </Button>
          <Button onClick={onCancel} variant="ghost">انصراف</Button>
        </div>
      </CardContent>
    </Card>
  );
}
