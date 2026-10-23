import { useEffect, useState, type ReactNode } from 'react';
import { AlertTriangle } from 'lucide-react';
import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogMedia,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { Button } from '@/components/ui/button';
import { Spinner } from '@/components/ui/spinner';

interface ConfirmDialogProps {
  open: boolean;
  title: string;
  description?: ReactNode;
  confirmText?: string;
  cancelText?: string;
  destructive?: boolean;
  icon?: ReactNode;
  onConfirm: () => void | Promise<void>;
  onClose: () => void;
}

export function ConfirmDialog({
  open,
  title,
  description,
  confirmText = 'تایید',
  cancelText = 'انصراف',
  destructive = false,
  icon,
  onConfirm,
  onClose,
}: ConfirmDialogProps) {
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (open) setBusy(false);
  }, [open]);

  async function handleConfirm() {
    try {
      setBusy(true);
      await onConfirm();
    } finally {
      setBusy(false);
    }
  }

  return (
    <AlertDialog open={open} onOpenChange={(next) => !next && onClose()}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogMedia>{icon ?? <AlertTriangle />}</AlertDialogMedia>
          <AlertDialogTitle>{title}</AlertDialogTitle>
          {description ? <AlertDialogDescription>{description}</AlertDialogDescription> : null}
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={busy}>{cancelText}</AlertDialogCancel>
          <Button
            variant={destructive ? 'destructive' : 'default'}
            onClick={handleConfirm}
            disabled={busy}
          >
            {busy ? <Spinner data-icon="inline-start" /> : null}
            {busy ? 'در حال انجام…' : confirmText}
          </Button>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
