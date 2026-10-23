import { useEffect, useRef, useState, type FormEvent, type ReactNode } from 'react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Field, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { Spinner } from '@/components/ui/spinner';

interface PromptDialogProps {
  open: boolean;
  title: string;
  description?: ReactNode;
  label?: string;
  placeholder?: string;
  defaultValue?: string;
  confirmText?: string;
  cancelText?: string;
  icon?: ReactNode;
  validate?: (value: string) => string | null | undefined;
  onConfirm: (value: string) => void | Promise<void>;
  onClose: () => void;
}

export function PromptDialog({
  open,
  title,
  description,
  label,
  placeholder,
  defaultValue = '',
  confirmText = 'تایید',
  cancelText = 'انصراف',
  validate,
  onConfirm,
  onClose,
}: PromptDialogProps) {
  const [value, setValue] = useState(defaultValue);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (!open) return;
    setValue(defaultValue);
    setError(null);
    setBusy(false);
    const timer = setTimeout(() => {
      inputRef.current?.focus();
      inputRef.current?.select();
    }, 60);
    return () => clearTimeout(timer);
  }, [open, defaultValue]);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    const trimmed = value.trim();
    if (!trimmed) {
      setError('این فیلد الزامی است');
      return;
    }
    const validationError = validate?.(trimmed);
    if (validationError) {
      setError(validationError);
      return;
    }
    try {
      setBusy(true);
      await onConfirm(trimmed);
    } finally {
      setBusy(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={(next) => !next && onClose()}>
      <DialogContent>
        <form onSubmit={onSubmit} className="flex flex-col gap-4">
          <DialogHeader>
            <DialogTitle>{title}</DialogTitle>
            {description ? <DialogDescription>{description}</DialogDescription> : null}
          </DialogHeader>
          <FieldGroup>
            <Field data-invalid={error ? true : undefined}>
              {label ? <FieldLabel htmlFor="prompt-input">{label}</FieldLabel> : null}
              <Input
                id="prompt-input"
                ref={inputRef}
                value={value}
                onChange={(event) => {
                  setValue(event.target.value);
                  if (error) setError(null);
                }}
                placeholder={placeholder}
                aria-invalid={!!error}
              />
              {error ? <FieldError>{error}</FieldError> : null}
            </Field>
          </FieldGroup>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose} disabled={busy}>
              {cancelText}
            </Button>
            <Button type="submit" disabled={busy || !value.trim()}>
              {busy ? <Spinner data-icon="inline-start" /> : null}
              {busy ? 'در حال انجام…' : confirmText}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
