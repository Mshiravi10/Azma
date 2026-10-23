import { useEffect, useId, useMemo, useRef, useState } from 'react';
import { marked } from 'marked';
import DOMPurify from 'dompurify';
import {
  Bold, Italic, Code, Link as LinkIcon, List, ListOrdered, Quote,
  Heading1, Heading2, Heading3, Image as ImageIcon, Strikethrough,
  CheckSquare, Table, Eye, Pencil, Columns, Maximize2, Minimize2, Loader2,
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { motion, AnimatePresence } from 'framer-motion';

marked.setOptions({ gfm: true, breaks: true });

export type EditorMode = 'edit' | 'preview' | 'split';

export interface MarkdownEditorProps {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  minRows?: number;
  /** When true, expand to full height (for big editors). */
  full?: boolean;
  className?: string;
  /** Allow only inline formatting (no headings, lists, blocks). Used for compact step fields. */
  compact?: boolean;
  ariaLabel?: string;
  /**
   * When provided, images pasted from the clipboard or drag-dropped onto
   * the editor are uploaded via this function and embedded as Markdown.
   * Should return the public URL of the uploaded image.
   */
  onUploadImage?: (file: File) => Promise<string>;
}

/**
 * GitHub-style Markdown editor: toolbar + textarea + live preview.
 * Supports edit / preview / split modes, Ctrl+B/I/K shortcuts, and a fullscreen toggle.
 */
export function MarkdownEditor(props: MarkdownEditorProps) {
  const {
    value, onChange, placeholder, minRows = 6, full,
    className, compact = false, ariaLabel, onUploadImage,
  } = props;
  const id = useId();
  const ref = useRef<HTMLTextAreaElement>(null);
  const valueRef = useRef(value);
  const [mode, setMode] = useState<EditorMode>('edit');
  const [fullscreen, setFullscreen] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [dropOver, setDropOver] = useState(false);

  // Keep a ref so async upload callbacks always see the latest value.
  useEffect(() => { valueRef.current = value; }, [value]);

  const html = useMemo(() => {
    if (!value.trim()) return '';
    const raw = marked.parse(value, { async: false }) as string;
    return DOMPurify.sanitize(raw, { ADD_ATTR: ['target', 'rel'] });
  }, [value]);

  function wrap(before: string, after = before, placeholderText = '') {
    const ta = ref.current;
    if (!ta) return;
    const start = ta.selectionStart;
    const end = ta.selectionEnd;
    const selected = value.slice(start, end) || placeholderText;
    const next = value.slice(0, start) + before + selected + after + value.slice(end);
    onChange(next);
    requestAnimationFrame(() => {
      ta.focus();
      const pos = start + before.length;
      ta.setSelectionRange(pos, pos + selected.length);
    });
  }

  function prefixLines(prefix: string, eachLine = true) {
    const ta = ref.current;
    if (!ta) return;
    const start = ta.selectionStart;
    const end = ta.selectionEnd;
    const lineStart = value.lastIndexOf('\n', start - 1) + 1;
    const lineEnd = value.indexOf('\n', end);
    const block = value.slice(lineStart, lineEnd === -1 ? value.length : lineEnd);
    const transformed = eachLine
      ? block.split('\n').map((l, i) => (eachLine ? prefix.replace('{n}', String(i + 1)) : prefix) + l).join('\n')
      : prefix + block;
    const next = value.slice(0, lineStart) + transformed + value.slice(lineEnd === -1 ? value.length : lineEnd);
    onChange(next);
    requestAnimationFrame(() => {
      ta.focus();
      ta.setSelectionRange(lineStart, lineStart + transformed.length);
    });
  }

  function insertLink() {
    const url = prompt('آدرس لینک:');
    if (!url) return;
    wrap('[', `](${url})`, 'متن لینک');
  }
  function insertImage() {
    const url = prompt('آدرس تصویر:');
    if (!url) return;
    const alt = prompt('متن جایگزین (alt):') ?? '';
    wrap(`![${alt}](`, ')', url);
  }
  function insertTable() {
    const tpl = '\n| ستون ۱ | ستون ۲ | ستون ۳ |\n|---|---|---|\n| ... | ... | ... |\n';
    const ta = ref.current;
    if (!ta) return;
    const pos = ta.selectionStart;
    onChange(value.slice(0, pos) + tpl + value.slice(pos));
    requestAnimationFrame(() => {
      ta.focus();
      ta.setSelectionRange(pos + tpl.length, pos + tpl.length);
    });
  }

  function onKeyDown(e: React.KeyboardEvent<HTMLTextAreaElement>) {
    if (!(e.ctrlKey || e.metaKey)) return;
    const k = e.key.toLowerCase();
    if (k === 'b') { e.preventDefault(); wrap('**', '**', 'متن'); }
    else if (k === 'i') { e.preventDefault(); wrap('_', '_', 'متن'); }
    else if (k === 'k') { e.preventDefault(); insertLink(); }
    else if (k === 'e') { e.preventDefault(); wrap('`', '`', 'code'); }
  }

  // ── Image upload (paste / drag-drop) ────────────────────────────────
  async function uploadAndInsert(file: File) {
    if (!onUploadImage) return;
    const ta = ref.current;
    const insertAt = ta?.selectionStart ?? valueRef.current.length;
    const token = `uploading_${Date.now()}`;
    const placeholder = `![${token}]()`;
    onChange(valueRef.current.slice(0, insertAt) + placeholder + valueRef.current.slice(insertAt));
    setUploading(true);
    try {
      const url = await onUploadImage(file);
      onChange(valueRef.current.replace(placeholder, `![تصویر](${url})`));
    } catch {
      onChange(valueRef.current.replace(placeholder, ''));
    } finally {
      setUploading(false);
    }
  }

  function onPaste(e: React.ClipboardEvent<HTMLTextAreaElement>) {
    if (!onUploadImage) return;
    const images = Array.from(e.clipboardData.items)
      .filter((item) => item.kind === 'file' && item.type.startsWith('image/'))
      .map((item) => item.getAsFile())
      .filter((f): f is File => f !== null);
    if (!images.length) return;
    e.preventDefault();
    images.forEach((f) => uploadAndInsert(f));
  }

  function onDragOver(e: React.DragEvent<HTMLTextAreaElement>) {
    if (!onUploadImage) return;
    const hasImage = Array.from(e.dataTransfer.items).some(
      (item) => item.kind === 'file' && item.type.startsWith('image/'),
    );
    if (!hasImage) return;
    e.preventDefault();
    e.dataTransfer.dropEffect = 'copy';
    setDropOver(true);
  }

  function onDrop(e: React.DragEvent<HTMLTextAreaElement>) {
    if (!onUploadImage) return;
    const images = Array.from(e.dataTransfer.files).filter((f) => f.type.startsWith('image/'));
    if (!images.length) return;
    e.preventDefault();
    setDropOver(false);
    images.forEach((f) => uploadAndInsert(f));
  }

  // Auto-grow textarea
  useEffect(() => {
    const ta = ref.current;
    if (!ta) return;
    ta.style.height = 'auto';
    ta.style.height = Math.max(ta.scrollHeight, minRows * 22) + 'px';
  }, [value, minRows, mode, fullscreen]);

  const editorBody = (
    <div
      className={cn(
        'flex flex-col rounded-xl border border-input bg-background overflow-hidden',
        fullscreen && 'fixed inset-3 z-[60] shadow-soft-lg',
        className,
      )}
    >
      {/* Toolbar */}
      <div className="flex items-center gap-0.5 border-b border-border bg-muted/40 px-2 py-1.5 flex-wrap">
        {!compact && (
          <>
            <ToolBtn label="عنوان ۱" onClick={() => prefixLines('# ', false)}><Heading1 className="size-3.5" /></ToolBtn>
            <ToolBtn label="عنوان ۲" onClick={() => prefixLines('## ', false)}><Heading2 className="size-3.5" /></ToolBtn>
            <ToolBtn label="عنوان ۳" onClick={() => prefixLines('### ', false)}><Heading3 className="size-3.5" /></ToolBtn>
            <Divider />
          </>
        )}
        <ToolBtn label="پررنگ (Ctrl+B)" onClick={() => wrap('**', '**', 'متن')}><Bold className="size-3.5" /></ToolBtn>
        <ToolBtn label="مورب (Ctrl+I)" onClick={() => wrap('_', '_', 'متن')}><Italic className="size-3.5" /></ToolBtn>
        <ToolBtn label="خط‌خورده" onClick={() => wrap('~~', '~~', 'متن')}><Strikethrough className="size-3.5" /></ToolBtn>
        <ToolBtn label="کد inline (Ctrl+E)" onClick={() => wrap('`', '`', 'code')}><Code className="size-3.5" /></ToolBtn>
        <ToolBtn label="لینک (Ctrl+K)" onClick={insertLink}><LinkIcon className="size-3.5" /></ToolBtn>
        {!compact && (
          <>
            <ToolBtn label="تصویر" onClick={insertImage}><ImageIcon className="size-3.5" /></ToolBtn>
            <Divider />
            <ToolBtn label="فهرست نشان‌دار" onClick={() => prefixLines('- ')}><List className="size-3.5" /></ToolBtn>
            <ToolBtn label="فهرست شماره‌دار" onClick={() => prefixLines('{n}. ')}><ListOrdered className="size-3.5" /></ToolBtn>
            <ToolBtn label="چک‌لیست" onClick={() => prefixLines('- [ ] ')}><CheckSquare className="size-3.5" /></ToolBtn>
            <ToolBtn label="نقل‌قول" onClick={() => prefixLines('> ')}><Quote className="size-3.5" /></ToolBtn>
            <ToolBtn label="جدول" onClick={insertTable}><Table className="size-3.5" /></ToolBtn>
            <ToolBtn label="بلوک کد" onClick={() => wrap('\n```\n', '\n```\n', 'code')}><Code className="size-3.5" /></ToolBtn>
          </>
        )}
        <div className="ms-auto flex items-center gap-0.5">
          <ToolBtn label="ویرایش" active={mode === 'edit'} onClick={() => setMode('edit')}><Pencil className="size-3.5" /></ToolBtn>
          <ToolBtn label="دو ستونه" active={mode === 'split'} onClick={() => setMode('split')}><Columns className="size-3.5" /></ToolBtn>
          <ToolBtn label="پیش‌نمایش" active={mode === 'preview'} onClick={() => setMode('preview')}><Eye className="size-3.5" /></ToolBtn>
          <Divider />
          <ToolBtn label={fullscreen ? 'خروج از تمام‌صفحه' : 'تمام‌صفحه'} onClick={() => setFullscreen((f) => !f)}>
            {fullscreen ? <Minimize2 className="size-3.5" /> : <Maximize2 className="size-3.5" />}
          </ToolBtn>
          {uploading && (
            <span title="در حال بارگذاری تصویر…" className="flex items-center ms-1">
              <Loader2 className="size-3.5 animate-spin text-muted-foreground" />
            </span>
          )}
        </div>
      </div>

      {/* Body */}
      <div className={cn('grid', mode === 'split' ? 'grid-cols-2 divide-x divide-border' : 'grid-cols-1', full && 'flex-1')}>
        {(mode === 'edit' || mode === 'split') && (
          <textarea
            ref={ref}
            id={id}
            value={value}
            onChange={(e) => onChange(e.target.value)}
            onKeyDown={onKeyDown}
            onPaste={onPaste}
            onDragOver={onDragOver}
            onDragLeave={() => setDropOver(false)}
            onDrop={onDrop}
            placeholder={
              onUploadImage
                ? (placeholder ?? 'با پشتیبانی از Markdown… تصویر را paste یا drag-drop کنید')
                : (placeholder ?? 'با پشتیبانی از Markdown… **پررنگ**, _مورب_, `code`, - فهرست')
            }
            aria-label={ariaLabel}
            spellCheck
            dir="auto"
            className={cn(
              'w-full resize-none p-3 text-sm leading-7 bg-transparent outline-none font-mono transition-colors',
              dropOver && 'bg-primary/5 ring-2 ring-inset ring-primary/40',
            )}
            style={{ minHeight: minRows * 22 }}
          />
        )}
        {(mode === 'preview' || mode === 'split') && (
          <div
            className={cn(
              'p-4 prose prose-sm max-w-none overflow-auto markdown-preview',
              !value.trim() && 'text-muted-foreground italic',
            )}
            style={{ minHeight: minRows * 22 }}
          >
            {value.trim() ? (
              <div dangerouslySetInnerHTML={{ __html: html }} />
            ) : (
              <span>پیش‌نمایشی برای نمایش وجود ندارد.</span>
            )}
          </div>
        )}
      </div>
    </div>
  );

  return (
    <>
      {!fullscreen && editorBody}
      <AnimatePresence>
        {fullscreen && (
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 z-50 bg-background/80 backdrop-blur-sm"
            onClick={() => setFullscreen(false)}
          >
            <div onClick={(e) => e.stopPropagation()}>{editorBody}</div>
          </motion.div>
        )}
      </AnimatePresence>
    </>
  );
}

function ToolBtn({
  children, onClick, label, active,
}: { children: React.ReactNode; onClick: () => void; label: string; active?: boolean }) {
  return (
    <button
      type="button"
      title={label}
      onClick={onClick}
      className={cn(
        'size-7 rounded-md flex items-center justify-center transition-colors',
        active ? 'bg-primary/15 text-primary' : 'text-muted-foreground hover:bg-accent hover:text-foreground',
      )}
    >
      {children}
    </button>
  );
}
function Divider() {
  return <span className="mx-1 h-4 w-px bg-border" />;
}

/** Read-only renderer used in detail/preview pages. */
export function MarkdownView({ source, className }: { source: string | null | undefined; className?: string }) {
  const html = useMemo(() => {
    if (!source || !source.trim()) return '';
    const raw = marked.parse(source, { async: false }) as string;
    return DOMPurify.sanitize(raw, { ADD_ATTR: ['target', 'rel'] });
  }, [source]);

  if (!html) {
    return <p className={cn('text-muted-foreground italic text-sm', className)}>—</p>;
  }
  return (
    <div
      className={cn('prose prose-sm max-w-none markdown-preview', className)}
      dangerouslySetInnerHTML={{ __html: html }}
    />
  );
}
