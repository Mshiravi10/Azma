import { useEffect, useRef, useState, type ChangeEvent } from 'react';
import { AnimatePresence, motion } from 'framer-motion';
import { FileInput, FileJson, Upload, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { cn } from '@/lib/utils';

export interface ImportedStepDraft {
  action: string;
  expectedResult: string;
}

type ImportMode = 'append' | 'replace';

interface StepsJsonImportDialogProps {
  open: boolean;
  existingCount: number;
  onClose: () => void;
  onImport: (steps: ImportedStepDraft[], mode: ImportMode) => void;
}

const ACTION_KEYS = ['step', 'action', 'description', 'title', 'name'];
const EXPECTED_KEYS = ['expectedResult', 'expected', 'result', 'expected_result', 'expectedText'];
const STEPS_KEYS = ['steps', 'testSteps', 'items'];

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function readKey(record: Record<string, unknown>, key: string): unknown {
  if (Object.prototype.hasOwnProperty.call(record, key)) return record[key];
  const normalized = key.toLowerCase();
  const actualKey = Object.keys(record).find((k) => k.toLowerCase() === normalized);
  return actualKey ? record[actualKey] : undefined;
}

function toText(value: unknown): string {
  if (typeof value === 'string') return value.trim();
  if (value === null || value === undefined) return '';
  if (typeof value === 'number' || typeof value === 'boolean') return String(value);
  return '';
}

function firstText(record: Record<string, unknown>, keys: string[]): string {
  for (const key of keys) {
    const value = toText(readKey(record, key));
    if (value) return value;
  }
  return '';
}

export function parseStepsJson(jsonText: string): ImportedStepDraft[] {
  let parsed: unknown;
  try {
    parsed = JSON.parse(jsonText);
  } catch {
    throw new Error('JSON معتبر نیست.');
  }

  let rawSteps: unknown;
  if (Array.isArray(parsed)) {
    rawSteps = parsed;
  } else if (isRecord(parsed)) {
    rawSteps = STEPS_KEYS.map((key) => readKey(parsed, key)).find(Array.isArray);
  }

  if (!Array.isArray(rawSteps)) {
    throw new Error('ساختار JSON باید آرایه مراحل یا آبجکت دارای فیلد steps باشد.');
  }

  const steps = rawSteps
    .map((item) => {
      if (!isRecord(item)) return null;
      const action = firstText(item, ACTION_KEYS);
      const expectedResult = firstText(item, EXPECTED_KEYS);
      return action ? { action, expectedResult } : null;
    })
    .filter((step): step is ImportedStepDraft => step !== null);

  if (steps.length === 0) {
    throw new Error('هیچ گام قابل وارد کردنی پیدا نشد.');
  }

  return steps;
}

export function StepsJsonImportDialog({
  open,
  existingCount,
  onClose,
  onImport,
}: StepsJsonImportDialogProps) {
  const [jsonText, setJsonText] = useState('');
  const [mode, setMode] = useState<ImportMode>('append');
  const [error, setError] = useState<string | null>(null);
  const textareaRef = useRef<HTMLTextAreaElement>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (!open) return;
    setJsonText('');
    setMode('append');
    setError(null);
    const timer = setTimeout(() => textareaRef.current?.focus(), 60);
    return () => clearTimeout(timer);
  }, [open]);

  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      if (open && e.key === 'Escape') onClose();
    }
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [open, onClose]);

  async function handleFileChange(e: ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;
    try {
      setJsonText(await file.text());
      setError(null);
    } catch {
      setError('فایل انتخاب شده قابل خواندن نیست.');
    } finally {
      e.target.value = '';
    }
  }

  function handleImport() {
    try {
      const importedSteps = parseStepsJson(jsonText.trim());
      onImport(importedSteps, mode);
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'خطای نامشخص در خواندن JSON.');
    }
  }

  return (
    <AnimatePresence>
      {open && (
        <motion.div
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0 }}
          transition={{ duration: 0.15 }}
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm p-4"
          onClick={onClose}
        >
          <motion.div
            initial={{ opacity: 0, scale: 0.96, y: 8 }}
            animate={{ opacity: 1, scale: 1, y: 0 }}
            exit={{ opacity: 0, scale: 0.96, y: 8 }}
            transition={{ duration: 0.18 }}
            onClick={(e) => e.stopPropagation()}
            className="bg-card border border-border rounded-2xl shadow-soft-lg w-full max-w-2xl overflow-hidden"
          >
            <div className="flex items-start justify-between gap-3 p-5 border-b border-border">
              <div className="flex items-start gap-3 flex-1">
                <div className="size-10 shrink-0 rounded-xl bg-primary/10 text-primary flex items-center justify-center">
                  <FileJson className="size-5" />
                </div>
                <div className="flex-1 min-w-0">
                  <h3 className="font-semibold text-base leading-tight">ورود مراحل از JSON</h3>
                  <p className="text-xs text-muted-foreground mt-1 leading-relaxed">
                    آبجکت دارای steps یا آرایه مستقیم از step و expectedResult را وارد کنید.
                  </p>
                </div>
              </div>
              <Button type="button" variant="ghost" size="icon-sm" onClick={onClose} aria-label="بستن">
                <X className="size-4" />
              </Button>
            </div>

            <div className="p-5 space-y-4">
              <div className="flex items-center justify-between gap-3">
                <Label className="mb-0">JSON مراحل</Label>
                <input
                  ref={fileInputRef}
                  type="file"
                  accept="application/json,.json"
                  className="hidden"
                  onChange={handleFileChange}
                />
                <Button type="button" variant="outline" size="sm" onClick={() => fileInputRef.current?.click()}>
                  <FileInput className="size-3.5" />
                  انتخاب فایل
                </Button>
              </div>

              <Textarea
                ref={textareaRef}
                value={jsonText}
                onChange={(e) => {
                  setJsonText(e.target.value);
                  if (error) setError(null);
                }}
                dir="ltr"
                spellCheck={false}
                rows={14}
                className={cn(
                  'font-mono text-xs leading-6 resize-y',
                  error && 'border-destructive focus-visible:ring-destructive',
                )}
                placeholder={'{\n  "steps": [\n    {\n      "step": "...",\n      "expectedResult": "..."\n    }\n  ]\n}'}
              />
              {error && <p className="text-xs text-destructive">{error}</p>}

              <div className="flex flex-wrap items-center gap-2">
                <Button
                  type="button"
                  variant={mode === 'append' ? 'default' : 'outline'}
                  size="sm"
                  onClick={() => setMode('append')}
                >
                  افزودن به انتها
                </Button>
                <Button
                  type="button"
                  variant={mode === 'replace' ? 'default' : 'outline'}
                  size="sm"
                  onClick={() => setMode('replace')}
                  disabled={existingCount === 0}
                >
                  جایگزینی مراحل
                </Button>
              </div>
            </div>

            <div className="flex items-center justify-end gap-2 px-5 py-4 border-t border-border bg-muted/30">
              <Button type="button" variant="ghost" onClick={onClose}>
                انصراف
              </Button>
              <Button type="button" onClick={handleImport} disabled={!jsonText.trim()}>
                <Upload className="size-4" />
                وارد کردن
              </Button>
            </div>
          </motion.div>
        </motion.div>
      )}
    </AnimatePresence>
  );
}
