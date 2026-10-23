import { useEffect, useRef, useState, type DragEvent, type MouseEvent } from 'react';
import type { SectionNode } from '@/lib/types';
import { cn, formatNumber } from '@/lib/utils';
import {
  ChevronRight,
  Folder,
  FolderOpen,
  MoreVertical,
  Plus,
  Pencil,
  Trash2,
  FolderPlus,
} from 'lucide-react';
import { motion, AnimatePresence } from 'framer-motion';
import { ConfirmDialog } from '@/components/ui/ConfirmDialog';

export interface DragPayload {
  kind: 'section' | 'testcase';
  id: string;
  parentId?: string | null;
}

export interface SectionTreeProps {
  /** Virtual root with id="ROOT", children = top-level sections */
  rootSections: SectionNode[];
  /** Map of sectionId → testcase count (use ROOT for cases not in any section) */
  counts: Record<string, number>;
  selectedId: string | 'ROOT' | null;
  expanded: Set<string>;
  onToggle: (id: string) => void;
  onSelect: (id: string | 'ROOT') => void;
  onRename: (id: string, name: string) => void;
  onCreate: (parentId: string | null) => void;
  onDelete: (id: string) => void;
  /** Move a section under newParentId (null = top of suite) at end of list */
  onMoveSection: (id: string, newParentId: string | null) => void;
  /** A test case was dropped on this section */
  onDropTestCase: (testCaseId: string, newSectionId: string | null) => void;
  filter?: string;
}

/**
 * Recursive tree of sections + a virtual root row showing the "all of suite" count.
 * Native HTML5 drag-and-drop (no extra dependency).
 */
export function SectionTree(props: SectionTreeProps) {
  // Sum only ROOT (uncategorized) + each top-level section's aggregated count.
  // Top-level section counts already include all descendants, so summing all
  // entries in `counts` would double-count every nested section.
  const total =
    (props.counts['ROOT'] ?? 0) +
    props.rootSections.reduce((s, sec) => s + (props.counts[sec.id] ?? 0), 0);
  return (
    <div className="select-none">
      <RootRow
        count={total}
        selected={props.selectedId === 'ROOT'}
        onSelect={() => props.onSelect('ROOT')}
        onCreate={() => props.onCreate(null)}
        onDropTestCase={(id) => props.onDropTestCase(id, null)}
        onDropSectionToTop={(id) => props.onMoveSection(id, null)}
      />
      <ul className="mt-1">
        {props.rootSections.map((s) => (
          <TreeNode key={s.id} node={s} depth={0} {...props} />
        ))}
        {props.rootSections.length === 0 && (
          <li className="px-3 py-3 text-xs text-muted-foreground italic">
            هنوز هیچ پوشه‌ای ساخته نشده — روی + بزنید تا شروع شود.
          </li>
        )}
      </ul>
    </div>
  );
}

function RootRow(props: {
  count: number;
  selected: boolean;
  onSelect: () => void;
  onCreate: () => void;
  onDropTestCase: (id: string) => void;
  onDropSectionToTop: (id: string) => void;
}) {
  const [over, setOver] = useState(false);
  function handleDrop(e: DragEvent) {
    e.preventDefault();
    setOver(false);
    const data = readDrag(e);
    if (!data) return;
    if (data.kind === 'testcase') props.onDropTestCase(data.id);
    else props.onDropSectionToTop(data.id);
  }
  return (
    <div
      onDragOver={(e) => {
        e.preventDefault();
        setOver(true);
      }}
      onDragLeave={() => setOver(false)}
      onDrop={handleDrop}
      onClick={props.onSelect}
      className={cn(
        'group flex items-center gap-2 px-3 py-2 rounded-lg cursor-pointer transition-colors',
        props.selected ? 'bg-primary/10 text-primary font-medium' : 'hover:bg-accent/60',
        over && 'ring-2 ring-primary/50 bg-primary/5',
      )}
    >
      <FolderOpen className="size-4 text-primary" />
      <span className="flex-1 text-sm">همه سناریوها</span>
      <span className="text-[10px] num-fa text-muted-foreground">{formatNumber(props.count)}</span>
      <button
        onClick={(e) => {
          e.stopPropagation();
          props.onCreate();
        }}
        title="پوشه ریشه جدید"
        className="size-6 rounded-md flex items-center justify-center opacity-0 group-hover:opacity-100 hover:bg-primary/10 hover:text-primary transition-opacity"
      >
        <Plus className="size-3.5" />
      </button>
    </div>
  );
}

interface NodeProps extends Omit<SectionTreeProps, 'rootSections'> {
  node: SectionNode;
  depth: number;
}

function TreeNode({ node, depth, ...rest }: NodeProps) {
  const {
    counts, selectedId, expanded, onToggle, onSelect, onRename,
    onCreate, onDelete, onMoveSection, onDropTestCase, filter,
  } = rest;
  const isOpen = expanded.has(node.id);
  const isSelected = selectedId === node.id;
  const count = counts[node.id] ?? 0;
  const [editing, setEditing] = useState(false);
  const [draftName, setDraftName] = useState(node.name);
  const [menu, setMenu] = useState(false);
  const [over, setOver] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [listOverflow, setListOverflow] = useState<'hidden' | 'visible'>('hidden');
  const menuRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  // Close menu on outside click
  useEffect(() => {
    if (!menu) return;
    function onDoc(e: globalThis.MouseEvent) {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) setMenu(false);
    }
    document.addEventListener('mousedown', onDoc);
    return () => document.removeEventListener('mousedown', onDoc);
  }, [menu]);

  useEffect(() => {
    if (editing) {
      inputRef.current?.focus();
      inputRef.current?.select();
    }
  }, [editing]);

  const matchesFilter = !filter ||
    node.name.toLowerCase().includes(filter.toLowerCase()) ||
    hasMatchingDescendant(node, filter.toLowerCase());

  if (!matchesFilter) return null;

  function commitRename() {
    setEditing(false);
    const v = draftName.trim();
    if (v && v !== node.name) onRename(node.id, v);
    else setDraftName(node.name);
  }

  function handleDragStart(e: DragEvent) {
    writeDrag(e, { kind: 'section', id: node.id, parentId: node.parentId });
    e.dataTransfer.effectAllowed = 'move';
  }
  function handleDragOver(e: DragEvent) {
    e.preventDefault();
    setOver(true);
  }
  function handleDrop(e: DragEvent) {
    e.preventDefault();
    e.stopPropagation();
    setOver(false);
    const data = readDrag(e);
    if (!data) return;
    if (data.kind === 'testcase') onDropTestCase(data.id, node.id);
    else if (data.kind === 'section' && data.id !== node.id && !isDescendant(node, data.id)) {
      onMoveSection(data.id, node.id);
    }
  }

  function ctxMenu(e: MouseEvent) {
    e.preventDefault();
    setMenu(true);
  }

  return (
    <>
    <li className="relative">
      <div
        draggable={!editing}
        onDragStart={handleDragStart}
        onDragOver={handleDragOver}
        onDragLeave={() => setOver(false)}
        onDrop={handleDrop}
        onClick={() => onSelect(node.id)}
        onContextMenu={ctxMenu}
        style={{ paddingInlineStart: depth * 16 + 8 }}
        className={cn(
          'group flex items-center gap-1.5 pe-2 py-1.5 rounded-lg cursor-pointer transition-colors',
          isSelected ? 'bg-primary/10 text-primary font-medium' : 'hover:bg-accent/60',
          over && 'ring-2 ring-primary/60 bg-primary/5',
        )}
      >
        {node.children.length > 0 ? (
          <button
            onClick={(e) => {
              e.stopPropagation();
              onToggle(node.id);
            }}
            className="size-5 rounded flex items-center justify-center hover:bg-accent text-muted-foreground"
          >
            <ChevronRight className={cn('size-3.5 transition-transform', isOpen && 'rotate-90')} />
          </button>
        ) : (
          <span className="size-5" />
        )}
        {isOpen ? (
          <FolderOpen className="size-4 text-warning shrink-0" />
        ) : (
          <Folder className="size-4 text-warning shrink-0" />
        )}
        {editing ? (
          <input
            ref={inputRef}
            value={draftName}
            onChange={(e) => setDraftName(e.target.value)}
            onClick={(e) => e.stopPropagation()}
            onBlur={commitRename}
            onKeyDown={(e) => {
              if (e.key === 'Enter') commitRename();
              else if (e.key === 'Escape') {
                setDraftName(node.name);
                setEditing(false);
              }
            }}
            className="flex-1 bg-background border border-primary rounded px-1.5 text-sm h-6 outline-none"
          />
        ) : (
          <span className="flex-1 text-sm truncate" title={node.name}>{node.name}</span>
        )}
        {!editing && (
          <>
            {count > 0 && (
              <span className="text-[10px] num-fa text-muted-foreground tabular-nums px-1.5 py-0.5 rounded bg-muted">
                {formatNumber(count)}
              </span>
            )}
            <button
              onClick={(e) => {
                e.stopPropagation();
                setMenu((m) => !m);
              }}
              className="size-6 rounded-md flex items-center justify-center opacity-0 group-hover:opacity-100 hover:bg-accent transition-opacity"
            >
              <MoreVertical className="size-3.5" />
            </button>
          </>
        )}
      </div>

      <AnimatePresence>
        {menu && (
          <motion.div
            ref={menuRef}
            initial={{ opacity: 0, scale: 0.95, y: -4 }}
            animate={{ opacity: 1, scale: 1, y: 0 }}
            exit={{ opacity: 0, scale: 0.95 }}
            transition={{ duration: 0.12 }}
            className="absolute z-30 left-2 top-8 min-w-[160px] rounded-lg border border-border bg-popover shadow-soft-lg py-1 text-sm"
          >
            <MenuItem icon={<FolderPlus className="size-3.5" />} onClick={() => { setMenu(false); onCreate(node.id); }}>
              زیرپوشه جدید
            </MenuItem>
            <MenuItem icon={<Pencil className="size-3.5" />} onClick={() => { setMenu(false); setEditing(true); }}>
              تغییر نام
            </MenuItem>
            <div className="my-1 border-t border-border" />
            <MenuItem
              danger
              icon={<Trash2 className="size-3.5" />}
              onClick={() => {
                setMenu(false);
                setConfirmDelete(true);
              }}
            >
              حذف پوشه
            </MenuItem>
          </motion.div>
        )}
      </AnimatePresence>

      <AnimatePresence initial={false}>
        {isOpen && node.children.length > 0 && (
          <motion.ul
            initial={{ height: 0, opacity: 0 }}
            animate={{ height: 'auto', opacity: 1 }}
            exit={{ height: 0, opacity: 0 }}
            transition={{ duration: 0.15 }}
            style={{ overflow: listOverflow }}
            onAnimationStart={() => setListOverflow('hidden')}
            onAnimationComplete={() => setListOverflow(isOpen ? 'visible' : 'hidden')}
          >
            {node.children.map((c) => (
              <TreeNode key={c.id} node={c} depth={depth + 1} {...rest} />
            ))}
          </motion.ul>
        )}
      </AnimatePresence>
    </li>

    <ConfirmDialog
      open={confirmDelete}
      title="حذف پوشه"
      description={`پوشه «${node.name}» و تمام زیرپوشه‌ها و سناریوهای مرتبط حذف خواهند شد. این عمل قابل بازگشت نیست.`}
      confirmText="حذف"
      destructive
      icon={<Trash2 className="size-5" />}
      onClose={() => setConfirmDelete(false)}
      onConfirm={() => {
        onDelete(node.id);
        setConfirmDelete(false);
      }}
    />
    </>
  );
}

function MenuItem({
  children, icon, onClick, danger,
}: { children: React.ReactNode; icon: React.ReactNode; onClick: () => void; danger?: boolean }) {
  return (
    <button
      onClick={onClick}
      className={cn(
        'flex items-center gap-2 w-full px-3 py-1.5 hover:bg-accent text-start',
        danger && 'text-destructive hover:bg-destructive/10',
      )}
    >
      {icon}
      <span>{children}</span>
    </button>
  );
}

function hasMatchingDescendant(n: SectionNode, q: string): boolean {
  for (const c of n.children) {
    if (c.name.toLowerCase().includes(q)) return true;
    if (hasMatchingDescendant(c, q)) return true;
  }
  return false;
}

function isDescendant(n: SectionNode, targetId: string): boolean {
  for (const c of n.children) {
    if (c.id === targetId) return true;
    if (isDescendant(c, targetId)) return true;
  }
  return false;
}

function writeDrag(e: DragEvent, p: DragPayload) {
  e.dataTransfer.setData('application/azma+json', JSON.stringify(p));
  e.dataTransfer.setData('text/plain', p.id);
}
function readDrag(e: DragEvent): DragPayload | null {
  try {
    const raw = e.dataTransfer.getData('application/azma+json');
    if (!raw) return null;
    return JSON.parse(raw);
  } catch {
    return null;
  }
}
export { writeDrag, readDrag };
