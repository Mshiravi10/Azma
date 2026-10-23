import { useState, type FormEvent } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { RunsApi } from '@/lib/queries/endpoints';
import type { Run } from '@/lib/types';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Field, FieldGroup, FieldLabel } from '@/components/ui/field';
import { toast } from 'sonner';
import { extractError } from '@/lib/api';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';

interface Props {
  run: Run;
  onClose: () => void;
}

export default function EditRunDialog({ run, onClose }: Props) {
  const qc = useQueryClient();
  const [name, setName] = useState(run.name);
  const [description, setDescription] = useState(run.description ?? '');
  const [environment, setEnvironment] = useState(run.environment ?? '');
  const [dueDate, setDueDate] = useState(run.dueDateUtc ? run.dueDateUtc.slice(0, 10) : '');

  const updateMut = useMutation({
    mutationFn: () =>
      RunsApi.update(run.id, {
        name: name.trim(),
        description: description.trim() || undefined,
        environment: environment.trim() || undefined,
        dueDateUtc: dueDate ? new Date(dueDate).toISOString() : undefined,
      }),
    onSuccess: () => {
      toast.success('اطلاعات اجرا به‌روزرسانی شد');
      qc.invalidateQueries({ queryKey: ['run', run.id] });
      qc.invalidateQueries({ queryKey: ['runs'] });
      onClose();
    },
    onError: (err) => toast.error(extractError(err)),
  });

  function onSubmit(e: FormEvent) {
    e.preventDefault();
    if (!name.trim()) return;
    updateMut.mutate();
  }

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-lg">
        <form onSubmit={onSubmit} className="flex flex-col gap-4">
          <DialogHeader>
            <DialogTitle>ویرایش اجرا</DialogTitle>
            <DialogDescription>{run.code}</DialogDescription>
          </DialogHeader>
          <FieldGroup>
            <Field>
              <FieldLabel htmlFor="run-name">نام اجرا</FieldLabel>
              <Input id="run-name" value={name} onChange={(e) => setName(e.target.value)} required autoFocus />
            </Field>
            <Field>
              <FieldLabel htmlFor="run-desc">توضیحات</FieldLabel>
              <Textarea id="run-desc" value={description} onChange={(e) => setDescription(e.target.value)} rows={3} />
            </Field>
            <Field>
              <FieldLabel htmlFor="run-env">محیط اجرا</FieldLabel>
              <Input
                id="run-env"
                value={environment}
                onChange={(e) => setEnvironment(e.target.value)}
                placeholder="Production, Staging, QA..."
              />
            </Field>
            <Field>
              <FieldLabel htmlFor="run-due">مهلت اجرا</FieldLabel>
              <Input id="run-due" type="date" value={dueDate} onChange={(e) => setDueDate(e.target.value)} />
            </Field>
          </FieldGroup>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose}>
              انصراف
            </Button>
            <Button type="submit" loading={updateMut.isPending}>
              ذخیره تغییرات
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
