import type { CustomFieldDefinition, CustomFieldType } from '@/lib/types';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select } from '@/components/ui/native-select';
import { MarkdownEditor } from '@/components/editor/MarkdownEditor';
import { Badge } from '@/components/ui/badge';
import { Sparkles, X } from 'lucide-react';
import { cn } from '@/lib/utils';

export interface CustomFieldsFormProps {
  definitions: CustomFieldDefinition[];
  values: Record<string, unknown>;
  onChange: (next: Record<string, unknown>) => void;
  className?: string;
  onUploadImage?: (file: File) => Promise<string>;
}

/**
 * Dynamic form rendering all (non-archived) custom-field definitions of a project.
 * Each field type has its own renderer. The values object is keyed by `definition.key`.
 */
export function CustomFieldsForm({ definitions, values, onChange, className, onUploadImage }: CustomFieldsFormProps) {
  if (!definitions.length) return null;

  function set(key: string, v: unknown) {
    const next = { ...values };
    if (v === '' || v === null || v === undefined || (Array.isArray(v) && v.length === 0)) {
      delete next[key];
    } else {
      next[key] = v;
    }
    onChange(next);
  }

  return (
    <div className={cn('space-y-4', className)}>
      <div className="flex items-center gap-2 text-xs text-muted-foreground">
        <Sparkles className="size-3.5 text-info" />
        <span>فیلدهای سفارشی این پروژه</span>
      </div>
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {definitions
          .slice()
          .sort((a, b) => a.position - b.position)
          .map((def) => (
            <FieldRenderer key={def.id} def={def} value={values[def.key]} onChange={(v) => set(def.key, v)} onUploadImage={onUploadImage} />
          ))}
      </div>
    </div>
  );
}

function FieldRenderer({
  def, value, onChange, onUploadImage,
}: { def: CustomFieldDefinition; value: unknown; onChange: (v: unknown) => void; onUploadImage?: (file: File) => Promise<string> }) {
  const span = def.type === 'LongText' ? 'md:col-span-2' : '';
  return (
    <div className={span}>
      <Label className="flex items-center gap-1">
        {def.label}
        {def.required && <span className="text-destructive">*</span>}
      </Label>
      {def.description && (
        <p className="text-[11px] text-muted-foreground mb-1.5">{def.description}</p>
      )}
      {renderInput(def, value, onChange, onUploadImage)}
    </div>
  );
}

function renderInput(def: CustomFieldDefinition, value: unknown, onChange: (v: unknown) => void, onUploadImage?: (file: File) => Promise<string>) {
  switch (def.type) {
    case 'Text':
      return <Input value={(value as string) ?? ''} onChange={(e) => onChange(e.target.value)} />;

    case 'LongText':
      return (
        <MarkdownEditor
          value={(value as string) ?? ''}
          onChange={onChange}
          minRows={4}
          compact
          onUploadImage={onUploadImage}
        />
      );

    case 'Number':
      return (
        <Input
          type="number"
          value={value === undefined || value === null ? '' : String(value)}
          onChange={(e) => onChange(e.target.value === '' ? null : Number(e.target.value))}
        />
      );

    case 'Date':
      return (
        <Input
          type="date"
          value={(value as string)?.slice(0, 10) ?? ''}
          onChange={(e) => onChange(e.target.value || null)}
        />
      );

    case 'Boolean':
      return (
        <label className="flex items-center gap-2 h-10 px-3 rounded-lg border border-input bg-background cursor-pointer">
          <input
            type="checkbox"
            checked={Boolean(value)}
            onChange={(e) => onChange(e.target.checked)}
            className="size-4 accent-primary"
          />
          <span className="text-sm">{value ? 'بله' : 'خیر'}</span>
        </label>
      );

    case 'Url':
      return (
        <Input
          type="url"
          dir="ltr"
          placeholder="https://…"
          value={(value as string) ?? ''}
          onChange={(e) => onChange(e.target.value)}
        />
      );

    case 'Select':
      return (
        <Select
          value={(value as string) ?? ''}
          onChange={(e) => onChange(e.target.value || null)}
        >
          <option value="">— انتخاب کنید —</option>
          {def.options?.map((opt) => (
            <option key={opt} value={opt}>{opt}</option>
          ))}
        </Select>
      );

    case 'MultiSelect': {
      const arr = Array.isArray(value) ? (value as string[]) : [];
      return (
        <div className="rounded-lg border border-input bg-background p-2">
          <div className="flex flex-wrap gap-1.5 mb-2 min-h-[1.5rem]">
            {arr.length === 0 && (
              <span className="text-xs text-muted-foreground italic">هیچ مقداری انتخاب نشده</span>
            )}
            {arr.map((v) => (
              <Badge key={v} variant="secondary" className="cursor-pointer">
                {v}
                <button
                  type="button"
                  onClick={() => onChange(arr.filter((x) => x !== v))}
                  className="ms-1"
                >
                  <X className="size-3" />
                </button>
              </Badge>
            ))}
          </div>
          <div className="flex flex-wrap gap-1.5">
            {def.options?.filter((o) => !arr.includes(o)).map((o) => (
              <button
                key={o}
                type="button"
                onClick={() => onChange([...arr, o])}
                className="text-xs px-2 py-1 rounded-md bg-muted hover:bg-primary/10 hover:text-primary transition-colors"
              >
                + {o}
              </button>
            ))}
          </div>
        </div>
      );
    }

    default:
      return <Textarea value={String(value ?? '')} onChange={(e) => onChange(e.target.value)} rows={2} />;
  }
}

/** Read-only view of values used in TestCaseDetailPage. */
export function CustomFieldsView({
  definitions, values,
}: { definitions: CustomFieldDefinition[]; values: Record<string, unknown> }) {
  const visible = definitions.filter((d) => !d.isArchived);
  if (!visible.length) return null;
  return (
    <dl className="grid grid-cols-1 md:grid-cols-2 gap-x-6 gap-y-3 text-sm">
      {visible
        .slice()
        .sort((a, b) => a.position - b.position)
        .map((def) => {
          const v = values?.[def.key];
          return (
            <div key={def.id} className="flex items-start gap-2 border-b border-border/60 pb-2">
              <dt className="w-32 shrink-0 text-muted-foreground text-xs">{def.label}</dt>
              <dd className="flex-1 text-foreground">{formatValue(def.type, v)}</dd>
            </div>
          );
        })}
    </dl>
  );
}

function formatValue(type: CustomFieldType, v: unknown): React.ReactNode {
  if (v === undefined || v === null || v === '') return <span className="text-muted-foreground italic">—</span>;
  switch (type) {
    case 'Boolean': return v ? 'بله' : 'خیر';
    case 'Date': return new Date(v as string).toLocaleDateString('fa-IR');
    case 'MultiSelect':
      return (
        <div className="flex flex-wrap gap-1">
          {(v as string[]).map((x) => (
            <Badge key={x} variant="secondary">{x}</Badge>
          ))}
        </div>
      );
    case 'Url':
      return (
        <a href={String(v)} target="_blank" rel="noreferrer" dir="ltr" className="text-primary underline-offset-2 hover:underline break-all">
          {String(v)}
        </a>
      );
    case 'LongText': return <span className="whitespace-pre-wrap text-sm leading-7">{String(v)}</span>;
    default: return String(v);
  }
}
