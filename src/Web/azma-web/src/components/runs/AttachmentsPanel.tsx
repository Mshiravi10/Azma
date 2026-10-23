import { useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AttachmentsApi } from '@/lib/queries/endpoints';
import type { RunAttachment } from '@/lib/types';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Paperclip, Upload, Download, Trash2, FileText, Image as ImageIcon, Loader2 } from 'lucide-react';
import { fmtRelative } from '@/lib/date';
import { formatNumber } from '@/lib/utils';
import { toast } from 'sonner';
import { extractError } from '@/lib/api';

const MAX_BYTES = 25 * 1024 * 1024;

function humanSize(b: number): string {
  if (b < 1024) return `${b} B`;
  if (b < 1024 * 1024) return `${(b / 1024).toFixed(1)} KB`;
  return `${(b / 1024 / 1024).toFixed(2)} MB`;
}

function iconFor(ct: string) {
  if (ct.startsWith('image/')) return ImageIcon;
  return FileText;
}

interface Props {
  runId: string;
  runCaseId?: string;
}

export function AttachmentsPanel({ runId, runCaseId }: Props) {
  const qc = useQueryClient();
  const fileRef = useRef<HTMLInputElement | null>(null);
  const [dragOver, setDragOver] = useState(false);
  const [progress, setProgress] = useState<number | null>(null);

  const queryKey = ['attachments', runId, runCaseId ?? null];

  const { data: items, isLoading } = useQuery({
    queryKey,
    queryFn: () => AttachmentsApi.list(runId, runCaseId),
  });

  const uploadMut = useMutation({
    mutationFn: (file: File) =>
      AttachmentsApi.upload(runId, file, {
        runCaseId,
        onProgress: (p) => setProgress(p),
      }),
    onSuccess: () => {
      toast.success('پیوست بارگذاری شد');
      qc.invalidateQueries({ queryKey });
    },
    onError: (e) => toast.error(extractError(e)),
    onSettled: () => setProgress(null),
  });

  const deleteMut = useMutation({
    mutationFn: (id: string) => AttachmentsApi.remove(id),
    onSuccess: () => {
      toast.success('پیوست حذف شد');
      qc.invalidateQueries({ queryKey });
    },
    onError: (e) => toast.error(extractError(e)),
  });

  function handleFiles(files: FileList | null) {
    if (!files || files.length === 0) return;
    for (const f of Array.from(files)) {
      if (f.size === 0) {
        toast.error(`فایل ${f.name} خالی است`);
        continue;
      }
      if (f.size > MAX_BYTES) {
        toast.error(`فایل ${f.name} بزرگ‌تر از ۲۵ مگابایت است`);
        continue;
      }
      uploadMut.mutate(f);
    }
  }

  async function openAttachment(att: RunAttachment) {
    try {
      const u = await AttachmentsApi.url(att.id);
      window.open(u.url, '_blank', 'noopener,noreferrer');
    } catch (e) {
      toast.error(extractError(e));
    }
  }

  return (
    <Card>
      <CardContent className="p-5 space-y-3">
        <div className="flex items-center gap-2">
          <Paperclip className="size-4 text-info" />
          <h3 className="font-semibold text-sm">پیوست‌ها</h3>
          {items && <Badge variant="muted">{formatNumber(items.length)}</Badge>}
        </div>

        {/* drop zone */}
        <div
          onDragOver={(e) => {
            e.preventDefault();
            setDragOver(true);
          }}
          onDragLeave={() => setDragOver(false)}
          onDrop={(e) => {
            e.preventDefault();
            setDragOver(false);
            handleFiles(e.dataTransfer.files);
          }}
          onClick={() => fileRef.current?.click()}
          className={`cursor-pointer rounded-lg border-2 border-dashed p-4 text-center transition-colors ${
            dragOver ? 'border-primary bg-primary/5' : 'border-border hover:border-primary/40 hover:bg-accent/30'
          }`}
        >
          <input
            ref={fileRef}
            type="file"
            multiple
            className="hidden"
            onChange={(e) => {
              handleFiles(e.target.files);
              e.target.value = '';
            }}
          />
          {progress !== null ? (
            <div className="flex flex-col items-center gap-2">
              <Loader2 className="size-5 animate-spin text-primary" />
              <div className="text-xs text-muted-foreground num-fa">در حال بارگذاری… {formatNumber(progress)}٪</div>
              <div className="h-1.5 w-full max-w-xs rounded-full bg-muted overflow-hidden">
                <div className="h-full bg-primary transition-all" style={{ width: `${progress}%` }} />
              </div>
            </div>
          ) : (
            <div className="flex flex-col items-center gap-1 text-muted-foreground">
              <Upload className="size-5" />
              <div className="text-xs">فایل را اینجا رها کنید یا کلیک کنید</div>
              <div className="text-[10px]">حداکثر ۲۵ مگابایت</div>
            </div>
          )}
        </div>

        {/* list */}
        {isLoading ? (
          <div className="text-xs text-muted-foreground">در حال بارگذاری…</div>
        ) : !items || items.length === 0 ? (
          <div className="text-xs text-muted-foreground text-center py-2">بدون پیوست</div>
        ) : (
          <ul className="space-y-2">
            {items.map((a) => {
              const Icon = iconFor(a.contentType);
              return (
                <li
                  key={a.id}
                  className="flex items-center gap-3 rounded-md bg-secondary/40 p-2 hover:bg-secondary/70 transition-colors"
                >
                  <Icon className="size-4 text-muted-foreground shrink-0" />
                  <div className="flex-1 min-w-0">
                    <button
                      onClick={() => openAttachment(a)}
                      className="block text-sm text-start truncate hover:text-primary hover:underline w-full"
                      title={a.fileName}
                    >
                      {a.fileName}
                    </button>
                    <div className="text-[11px] text-muted-foreground num-fa">
                      {humanSize(a.sizeBytes)} · {fmtRelative(a.uploadedAtUtc)}
                    </div>
                  </div>
                  <Button
                    variant="ghost"
                    size="icon-sm"
                    onClick={() => openAttachment(a)}
                    title="دانلود"
                  >
                    <Download className="size-4" />
                  </Button>
                  <Button
                    variant="ghost"
                    size="icon-sm"
                    onClick={() => {
                      if (confirm(`حذف «${a.fileName}»؟`)) deleteMut.mutate(a.id);
                    }}
                    loading={deleteMut.isPending && deleteMut.variables === a.id}
                    title="حذف"
                  >
                    <Trash2 className="size-4 text-destructive" />
                  </Button>
                </li>
              );
            })}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}
