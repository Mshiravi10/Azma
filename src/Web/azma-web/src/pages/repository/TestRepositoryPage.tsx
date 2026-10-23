import { useEffect, useMemo, useRef, useState, type DragEvent } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  ProjectsApi,
  SuitesApi,
  SectionsApi,
  TestCasesApi,
  CustomFieldsApi,
} from '@/lib/queries/endpoints';
import type {
  SectionNode,
  SuiteTree,
  Suite,
  TestCaseSummary,
  TestCasePriority,
  TestCaseStatus,
} from '@/lib/types';
import { PageHeader } from '@/components/common/PageHeader';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/native-select';
import { Badge } from '@/components/ui/badge';
import { PromptDialog } from '@/components/ui/PromptDialog';
import { ConfirmDialog } from '@/components/ui/ConfirmDialog';
import { Skeleton } from '@/components/common/Skeletons';
import { EmptyState } from '@/components/common/EmptyState';
import {
  PriorityBadge,
  TestCaseStatusBadge,
} from '@/components/common/StatusBadges';
import { ExecutionSummary } from '@/components/common/ExecutionStatus';
import { ScenarioAuthor } from '@/components/common/ScenarioAuthor';
import {
  Plus,
  Search,
  FolderTree,
  Layers,
  ChevronRight,
  ChevronsDown,
  ChevronsUp,
  FilePlus2,
  CheckSquare,
  Square,
  Trash2,
  X,
  Move,
  PackageOpen,
  ChevronLeft,
  ArrowRight,
  Edit2,
  FileText,
  Tag,
  Sparkles,
  ExternalLink,
  PlayCircle,
  MoreVertical,
  Pencil,
} from 'lucide-react';
import { fmtRelative, fmtDateTime } from '@/lib/date';
import { formatNumber } from '@/lib/utils';
import { toast } from 'sonner';
import { extractError } from '@/lib/api';
import { motion, AnimatePresence } from 'framer-motion';
import { SectionTree, writeDrag } from '@/components/repository/SectionTree';
import { MarkdownView } from '@/components/editor/MarkdownEditor';
import { CustomFieldsView } from '@/components/customfields/CustomFieldsForm';
import NewRunDialog from '@/pages/runs/NewRunDialog';

export default function TestRepositoryPage() {
  const params = useParams<{ projectId?: string }>();
  const [search, setSearch] = useSearchParams();
  const navigate = useNavigate();
  const qc = useQueryClient();

  // ── Project selection ──────────────────────────────────────────
  const { data: projects } = useQuery({
    queryKey: ['projects'],
    queryFn: () => ProjectsApi.list(),
  });

  const LAST_PROJECT_KEY = 'repo-last-project';
  const preferredProjectId =
    params.projectId ??
    search.get('projectId') ??
    sessionStorage.getItem(LAST_PROJECT_KEY);
  const activeProjectId =
    projects == null
      ? (preferredProjectId ?? null)
      : (preferredProjectId && projects.some((p) => p.id === preferredProjectId)
          ? preferredProjectId
          : (projects[0]?.id ?? null));

  // Persist last visited project
  useEffect(() => {
    if (activeProjectId) sessionStorage.setItem(LAST_PROJECT_KEY, activeProjectId);
  }, [activeProjectId]);

  // Stay on an active project; archived ids in the URL/session fall back to the first live project
  useEffect(() => {
    if (!projects || !activeProjectId) return;
    if (params.projectId !== activeProjectId) {
      navigate(`/repository/${activeProjectId}`, { replace: true });
    }
  }, [params.projectId, activeProjectId, projects, navigate]);

  // ── Suites ─────────────────────────────────────────────────────
  const { data: suites } = useQuery({
    queryKey: ['suites', activeProjectId],
    queryFn: () => ProjectsApi.suites(activeProjectId!),
    enabled: !!activeProjectId,
  });

  const LAST_SUITE_KEY = activeProjectId ? `repo-last-suite-${activeProjectId}` : null;
  const savedSuiteId = LAST_SUITE_KEY ? sessionStorage.getItem(LAST_SUITE_KEY) : null;

  const [activeSuiteId, setActiveSuiteId] = useState<string | null>(savedSuiteId);
  useEffect(() => {
    if (suites && suites.length && !suites.find((s) => s.id === activeSuiteId)) {
      setActiveSuiteId(suites[0].id);
    } else if (suites && suites.length === 0) {
      setActiveSuiteId(null);
    }
  }, [suites, activeSuiteId]);
  useEffect(() => {
    if (LAST_SUITE_KEY && activeSuiteId) sessionStorage.setItem(LAST_SUITE_KEY, activeSuiteId);
  }, [LAST_SUITE_KEY, activeSuiteId]);

  // ── Tree of sections ───────────────────────────────────────────
  const { data: tree } = useQuery({
    queryKey: ['suite-tree', activeSuiteId],
    queryFn: () => SuitesApi.tree(activeSuiteId!),
    enabled: !!activeSuiteId,
  });

  // ── Test cases (whole suite) for counts + display ──────────────
  const { data: allCases } = useQuery({
    queryKey: ['testcases', { suiteId: activeSuiteId, all: true }],
    queryFn: () => TestCasesApi.list({ suiteId: activeSuiteId!, pageSize: 500 }),
    enabled: !!activeSuiteId,
  });

  // ── Tree state (persisted in sessionStorage per suite) ────────
  const treeReady = useRef(false);

  function readSavedTree(suiteId: string) {
    try {
      const raw = sessionStorage.getItem(`repo-tree-${suiteId}`);
      if (raw) {
        const parsed = JSON.parse(raw) as { expanded: string[]; selected: string };
        return { expanded: new Set(parsed.expanded), selected: parsed.selected as string | 'ROOT' };
      }
    } catch { /* ignore */ }
    return null;
  }

  const [expanded, setExpanded] = useState<Set<string>>(() => {
    if (activeSuiteId) {
      const saved = readSavedTree(activeSuiteId);
      if (saved) { treeReady.current = true; return saved.expanded; }
    }
    return new Set<string>();
  });
  const [selectedNode, setSelectedNode] = useState<string | 'ROOT'>(() => {
    if (activeSuiteId) {
      const saved = readSavedTree(activeSuiteId);
      if (saved) return saved.selected;
    }
    return 'ROOT';
  });
  const [treeFilter, setTreeFilter] = useState('');
  const allSectionIds = useMemo(
    () => (tree ? collectSectionIds(tree.sections) : []),
    [tree],
  );
  const hasSections = allSectionIds.length > 0;
  const allSectionsExpanded = hasSections && allSectionIds.every((id) => expanded.has(id));

  // When activeSuiteId changes at runtime, restore saved state
  const prevSuiteRef = useRef(activeSuiteId);
  useEffect(() => {
    if (!activeSuiteId || activeSuiteId === prevSuiteRef.current) return;
    prevSuiteRef.current = activeSuiteId;
    const saved = readSavedTree(activeSuiteId);
    if (saved) {
      setExpanded(saved.expanded);
      setSelectedNode(saved.selected);
      treeReady.current = true;
    } else {
      treeReady.current = false;
    }
  }, [activeSuiteId]);

  // When tree loads for the first time (no saved state), expand top-level sections
  useEffect(() => {
    if (!tree || treeReady.current) return;
    setExpanded(new Set(tree.sections.map((s) => s.id)));
    setSelectedNode('ROOT');
    treeReady.current = true;
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [tree?.id]);

  // Persist tree state whenever it changes — only after tree is initialized
  useEffect(() => {
    if (!activeSuiteId || !treeReady.current) return;
    sessionStorage.setItem(`repo-tree-${activeSuiteId}`, JSON.stringify({
      expanded: Array.from(expanded),
      selected: selectedNode,
    }));
  }, [activeSuiteId, expanded, selectedNode]);

  function toggleNode(id: string) {
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  function expandAllSections() {
    setExpanded(new Set(allSectionIds));
  }

  function collapseAllSections() {
    setExpanded(new Set());
  }

  // ── Counts per section (recursive aggregate) ──────────────────
  const counts = useMemo(() => {
    if (!tree || !allCases) return {} as Record<string, number>;
    // direct counts
    const direct: Record<string, number> = {};
    let rootCount = 0;
    for (const tc of allCases.items) {
      if (tc.sectionId) direct[tc.sectionId] = (direct[tc.sectionId] ?? 0) + 1;
      else rootCount += 1;
    }
    // aggregate: each section's count = own + sum(children)
    const result: Record<string, number> = { ROOT: rootCount };
    function visit(n: SectionNode): number {
      let total = direct[n.id] ?? 0;
      for (const c of n.children) total += visit(c);
      result[n.id] = total;
      return total;
    }
    tree.sections.forEach(visit);
    return result;
  }, [tree, allCases]);

  // ── Cases for the selected node ────────────────────────────────
  const visibleCases = useMemo(() => {
    if (!allCases) return [] as TestCaseSummary[];
    if (selectedNode === 'ROOT') return allCases.items;
    // find the section in tree, include direct + descendants
    const ids = collectDescendants(tree, selectedNode);
    return allCases.items.filter((tc) => tc.sectionId && ids.has(tc.sectionId));
  }, [allCases, selectedNode, tree]);

  const [caseSearch, setCaseSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState<TestCaseStatus | ''>('');
  const [priorityFilter, setPriorityFilter] = useState<TestCasePriority | ''>('');
  const filteredCases = useMemo(() => {
    let v = visibleCases;
    if (caseSearch) {
      const q = caseSearch.toLowerCase();
      v = v.filter((c) => c.title.toLowerCase().includes(q) || c.code.toLowerCase().includes(q));
    }
    if (statusFilter) v = v.filter((c) => c.status === statusFilter);
    if (priorityFilter) v = v.filter((c) => c.priority === priorityFilter);
    return v;
  }, [visibleCases, caseSearch, statusFilter, priorityFilter]);

  // ── Inline detail view ─────────────────────────────────────────
  const [viewingCaseId, setViewingCaseId] = useState<string | null>(null);

  // ── Selection (bulk) ───────────────────────────────────────────
  const [selectedCases, setSelectedCases] = useState<Set<string>>(new Set());
  function toggleCase(id: string) {
    setSelectedCases((p) => {
      const n = new Set(p);
      if (n.has(id)) n.delete(id);
      else n.add(id);
      return n;
    });
  }
  function toggleAll() {
    if (selectedCases.size === filteredCases.length) setSelectedCases(new Set());
    else setSelectedCases(new Set(filteredCases.map((c) => c.id)));
  }

  // ── Mutations ──────────────────────────────────────────────────
  const createSuiteMut = useMutation({
    mutationFn: (name: string) => ProjectsApi.createSuite(activeProjectId!, { name }),
    onSuccess: (s) => {
      toast.success('Suite ساخته شد');
      qc.invalidateQueries({ queryKey: ['suites', activeProjectId] });
      setActiveSuiteId(s.id);
    },
    onError: (err) => toast.error(extractError(err)),
  });

  const updateSuiteMut = useMutation({
    mutationFn: (p: { id: string; name: string }) => SuitesApi.update(p.id, { name: p.name }),
    onSuccess: () => {
      toast.success('Suite ویرایش شد');
      qc.invalidateQueries({ queryKey: ['suites', activeProjectId] });
    },
    onError: (err) => toast.error(extractError(err)),
  });

  const deleteSuiteMut = useMutation({
    mutationFn: (id: string) => SuitesApi.remove(id),
    onSuccess: (_d, id) => {
      toast.success('Suite حذف شد');
      qc.invalidateQueries({ queryKey: ['suites', activeProjectId] });
      qc.invalidateQueries({ queryKey: ['testcases'] });
      if (activeSuiteId === id) setActiveSuiteId(null);
    },
    onError: (err) => toast.error(extractError(err)),
  });

  const createSectionMut = useMutation({
    mutationFn: (p: { parentId: string | null; name: string }) =>
      SuitesApi.createSection(activeSuiteId!, p),
    onSuccess: (created, vars) => {
      toast.success('پوشه ساخته شد');
      qc.invalidateQueries({ queryKey: ['suite-tree', activeSuiteId] });
      if (vars.parentId) setExpanded((p) => new Set(p).add(vars.parentId!));
      setSelectedNode(created.id);
    },
    onError: (err) => toast.error(extractError(err)),
  });

  const renameSectionMut = useMutation({
    mutationFn: (p: { id: string; name: string }) => SectionsApi.update(p.id, { name: p.name }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['suite-tree', activeSuiteId] }),
    onError: (err) => toast.error(extractError(err)),
  });

  const deleteSectionMut = useMutation({
    mutationFn: (id: string) => SectionsApi.remove(id),
    onSuccess: () => {
      toast.success('پوشه حذف شد');
      qc.invalidateQueries({ queryKey: ['suite-tree', activeSuiteId] });
      qc.invalidateQueries({ queryKey: ['testcases'] });
      setSelectedNode('ROOT');
    },
    onError: (err) => toast.error(extractError(err)),
  });

  const moveSectionMut = useMutation({
    mutationFn: (p: { id: string; newParentId: string | null }) =>
      SectionsApi.move(p.id, { newParentId: p.newParentId, newPosition: 9999 }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['suite-tree', activeSuiteId] }),
    onError: (err) => toast.error(extractError(err)),
  });

  const moveCaseMut = useMutation({
    mutationFn: async (p: { id: string; newSectionId: string | null }) => {
      const tc = await TestCasesApi.get(p.id);
      return TestCasesApi.update(p.id, {
        suiteId: tc.suiteId,
        sectionId: p.newSectionId,
        title: tc.title,
        preconditions: tc.preconditions,
        type: tc.type,
        priority: tc.priority,
        status: tc.status,
        changeReason: 'تغییر پوشه',
      });
    },
    onSuccess: () => {
      toast.success('سناریو منتقل شد');
      qc.invalidateQueries({ queryKey: ['testcases'] });
    },
    onError: (err) => toast.error(extractError(err)),
  });

  const bulkDeleteMut = useMutation({
    mutationFn: async (ids: string[]) => {
      for (const id of ids) await TestCasesApi.remove(id);
    },
    onSuccess: () => {
      toast.success('سناریوها حذف شدند');
      qc.invalidateQueries({ queryKey: ['testcases'] });
      setSelectedCases(new Set());
    },
    onError: (err) => toast.error(extractError(err)),
  });

  // Actions
  const [suiteDialogOpen, setSuiteDialogOpen] = useState(false);
  const [suiteEditDialog, setSuiteEditDialog] = useState<{ open: boolean; suite: Suite | null }>({ open: false, suite: null });
  const [suiteDeleteConfirm, setSuiteDeleteConfirm] = useState<{ open: boolean; suite: Suite | null }>({ open: false, suite: null });
  const [sectionDialog, setSectionDialog] = useState<{ open: boolean; parentId: string | null }>({
    open: false,
    parentId: null,
  });
  const [bulkDeleteOpen, setBulkDeleteOpen] = useState(false);
  const [showQuickRun, setShowQuickRun] = useState(false);

  function newSuite() {
    setSuiteDialogOpen(true);
  }
  function newSection(parentId: string | null) {
    setSectionDialog({ open: true, parentId });
  }

  // ── Render ─────────────────────────────────────────────────────
  if (!activeProjectId)
    return (
      <EmptyState
        icon={<PackageOpen className="size-7" />}
        title="هیچ پروژه‌ای موجود نیست"
        description="ابتدا یک پروژه بسازید."
        action={<Button asChild><Link to="/projects">رفتن به پروژه‌ها</Link></Button>}
      />
    );

  const breadcrumb = breadcrumbForNode(tree, selectedNode);

  return (
    <div className="space-y-4">
      <PageHeader
        title="مخزن سناریوها"
        description="ساختار درختی پوشه‌بندی، کشیدن‌ورهاکردن سناریوها و عملیات گروهی — همانند یک IDE."
        actions={
          <Select
            value={activeProjectId}
            onChange={(e) => navigate(`/repository/${e.target.value}`)}
            className="min-w-44"
          >
            {projects?.map((p) => (
              <option key={p.id} value={p.id}>
                {p.code} — {p.name}
              </option>
            ))}
          </Select>
        }
      />

      <div className="grid grid-cols-12 gap-4 min-h-[calc(100vh-220px)]">
        {/* ─── Suite rail ───────────────────────────── */}
        <Card className="col-span-12 md:col-span-2 overflow-hidden">
          <div className="p-3 border-b border-border flex items-center justify-between">
            <h3 className="font-semibold text-xs text-muted-foreground flex items-center gap-1.5">
              <Layers className="size-3.5" /> SUITES
            </h3>
            <button
              onClick={newSuite}
              className="size-6 rounded-md flex items-center justify-center hover:bg-accent text-muted-foreground hover:text-foreground"
              title="Suite جدید"
            >
              <Plus className="size-3.5" />
            </button>
          </div>
          <ul className="p-2 space-y-0.5">
            {!suites ? (
              Array.from({ length: 3 }).map((_, i) => <Skeleton key={i} className="h-8 mb-1" />)
            ) : suites.length === 0 ? (
              <li className="text-xs text-muted-foreground italic px-2 py-3">
                Suite ای وجود ندارد.
              </li>
            ) : (
              suites.map((s) => (
                <SuiteRailItem
                  key={s.id}
                  suite={s}
                  active={s.id === activeSuiteId}
                  onClick={() => setActiveSuiteId(s.id)}
                  onEdit={() => setSuiteEditDialog({ open: true, suite: s })}
                  onDelete={() => setSuiteDeleteConfirm({ open: true, suite: s })}
                />
              ))
            )}
          </ul>
        </Card>

        {/* ─── Sections tree ───────────────────────── */}
        <Card className="col-span-12 md:col-span-3 overflow-hidden flex flex-col">
          <div className="p-3 border-b border-border space-y-2">
            <div className="flex items-center justify-between">
              <h3 className="font-semibold text-xs text-muted-foreground flex items-center gap-1.5">
                <FolderTree className="size-3.5" /> ساختار پوشه‌ها
              </h3>
              <div className="flex items-center gap-1">
                <button
                  onClick={expandAllSections}
                  disabled={!hasSections || allSectionsExpanded}
                  className="size-6 rounded-md flex items-center justify-center hover:bg-accent text-muted-foreground hover:text-foreground disabled:opacity-40"
                  title="باز کردن همه پوشه‌ها"
                  aria-label="باز کردن همه پوشه‌ها"
                >
                  <ChevronsDown className="size-3.5" />
                </button>
                <button
                  onClick={collapseAllSections}
                  disabled={!hasSections || expanded.size === 0}
                  className="size-6 rounded-md flex items-center justify-center hover:bg-accent text-muted-foreground hover:text-foreground disabled:opacity-40"
                  title="بستن همه پوشه‌ها"
                  aria-label="بستن همه پوشه‌ها"
                >
                  <ChevronsUp className="size-3.5" />
                </button>
                <button
                  onClick={() => newSection(null)}
                  disabled={!activeSuiteId}
                  className="size-6 rounded-md flex items-center justify-center hover:bg-accent text-muted-foreground hover:text-foreground disabled:opacity-50"
                  title="پوشه ریشه جدید"
                  aria-label="پوشه ریشه جدید"
                >
                  <Plus className="size-3.5" />
                </button>
              </div>
            </div>
            <div className="relative">
              <Search className="absolute right-2.5 top-1/2 -translate-y-1/2 size-3.5 text-muted-foreground pointer-events-none" />
              <Input
                value={treeFilter}
                onChange={(e) => setTreeFilter(e.target.value)}
                placeholder="جست‌وجوی پوشه…"
                className="ps-8 h-8 text-xs"
              />
            </div>
          </div>
          <div className="flex-1 overflow-y-auto p-2">
            {!tree ? (
              <div className="space-y-1.5">
                {Array.from({ length: 6 }).map((_, i) => <Skeleton key={i} className="h-7" />)}
              </div>
            ) : (
              <SectionTree
                rootSections={tree.sections}
                counts={counts}
                expanded={expanded}
                selectedId={selectedNode}
                filter={treeFilter}
                onToggle={toggleNode}
                onSelect={setSelectedNode}
                onCreate={(parentId) => newSection(parentId)}
                onRename={(id, name) => renameSectionMut.mutate({ id, name })}
                onDelete={(id) => deleteSectionMut.mutate(id)}
                onMoveSection={(id, newParentId) => moveSectionMut.mutate({ id, newParentId })}
                onDropTestCase={(id, newSectionId) => moveCaseMut.mutate({ id, newSectionId })}
              />
            )}
          </div>
        </Card>

        {/* ─── Test cases pane / Inline detail ──── */}
        <Card className="col-span-12 md:col-span-7 overflow-hidden flex flex-col">
          <AnimatePresence mode="wait" initial={false}>
            {viewingCaseId ? (
              <motion.div
                key="detail"
                initial={{ opacity: 0, x: 30 }}
                animate={{ opacity: 1, x: 0 }}
                exit={{ opacity: 0, x: 30 }}
                transition={{ duration: 0.15 }}
                className="flex-1 flex flex-col overflow-hidden"
              >
                <InlineCaseDetail
                  caseId={viewingCaseId}
                  projectId={activeProjectId!}
                  onBack={() => setViewingCaseId(null)}
                />
              </motion.div>
            ) : (
              <motion.div
                key="list"
                initial={{ opacity: 0, x: -30 }}
                animate={{ opacity: 1, x: 0 }}
                exit={{ opacity: 0, x: -30 }}
                transition={{ duration: 0.15 }}
                className="flex-1 flex flex-col overflow-hidden"
              >
                <div className="p-4 border-b border-border space-y-3">
                  <div className="flex items-center gap-1 text-xs text-muted-foreground">
                    <Layers className="size-3" />
                    <span>{tree?.name ?? '—'}</span>
                    {breadcrumb.map((b) => (
                      <span key={b.id} className="flex items-center gap-1">
                        <ChevronLeft className="size-3" />
                        <span className="text-foreground">{b.name}</span>
                      </span>
                    ))}
                  </div>
                  <div className="flex items-center justify-between gap-3 flex-wrap">
                    <h2 className="font-bold text-lg">
                      {selectedNode === 'ROOT' ? 'همه سناریوها' : breadcrumb.at(-1)?.name ?? 'پوشه'}
                      <Badge variant="muted" className="ms-2 num-fa">
                        {formatNumber(filteredCases.length)}
                      </Badge>
                    </h2>
                    <Button
                      size="sm"
                      onClick={() => {
                        const sectionParam =
                          selectedNode !== 'ROOT' ? `&sectionId=${selectedNode}` : '';
                        navigate(`/testcases/new?projectId=${activeProjectId}&suiteId=${activeSuiteId}${sectionParam}`);
                      }}
                    >
                      <FilePlus2 className="size-4" /> سناریوی جدید
                    </Button>
                  </div>
                  <div className="flex items-center gap-2 flex-wrap">
                    <div className="relative flex-1 min-w-[140px]">
                      <Search className="absolute right-3 top-1/2 -translate-y-1/2 size-4 text-muted-foreground pointer-events-none" />
                      <Input
                        value={caseSearch}
                        onChange={(e) => setCaseSearch(e.target.value)}
                        placeholder="جست‌وجو…"
                        className="ps-10"
                      />
                    </div>
                    <Select
                      value={statusFilter}
                      onChange={(e) => setStatusFilter(e.target.value as TestCaseStatus | '')}
                      className="w-28"
                    >
                      <option value="">وضعیت</option>
                      <option value="Draft">پیش‌نویس</option>
                      <option value="Active">فعال</option>
                      <option value="Deprecated">منسوخ</option>
                    </Select>
                    <Select
                      value={priorityFilter}
                      onChange={(e) => setPriorityFilter(e.target.value as TestCasePriority | '')}
                      className="w-28"
                    >
                      <option value="">اولویت</option>
                      <option value="Critical">بحرانی</option>
                      <option value="High">بالا</option>
                      <option value="Medium">متوسط</option>
                      <option value="Low">پایین</option>
                    </Select>
                  </div>
                </div>

                <AnimatePresence>
                  {selectedCases.size > 0 && (
                    <motion.div
                      initial={{ opacity: 0, y: -8, height: 0 }}
                      animate={{ opacity: 1, y: 0, height: 'auto' }}
                      exit={{ opacity: 0, y: -8, height: 0 }}
                      className="border-b border-border bg-primary/5 px-4 py-2 flex items-center gap-3 overflow-hidden"
                    >
                      <Badge variant="info" className="num-fa">
                        {formatNumber(selectedCases.size)} انتخاب شده
                      </Badge>
                      <span className="text-xs text-muted-foreground hidden md:inline">
                        <Move className="size-3 inline" /> برای جابجایی، روی پوشه مقصد در درخت رها کنید
                      </span>
                      <div className="ms-auto flex items-center gap-1">
                        <Button
                          size="sm"
                          variant="default"
                          onClick={() => setShowQuickRun(true)}
                        >
                          <PlayCircle className="size-3.5" /> اجرای سریع
                        </Button>
                        <Button
                          size="sm"
                          variant="destructive"
                          onClick={() => setBulkDeleteOpen(true)}
                        >
                          <Trash2 className="size-3.5" /> حذف انتخاب‌ها
                        </Button>
                        <Button size="sm" variant="ghost" onClick={() => setSelectedCases(new Set())}>
                          <X className="size-3.5" />
                        </Button>
                      </div>
                    </motion.div>
                  )}
                </AnimatePresence>

                <div className="flex-1 overflow-y-auto">
                  {!allCases ? (
                    <div className="p-4 space-y-2">
                      {Array.from({ length: 8 }).map((_, i) => <Skeleton key={i} className="h-12" />)}
                    </div>
                  ) : filteredCases.length === 0 ? (
                    <EmptyState
                      icon={<FilePlus2 className="size-7" />}
                      title="در این پوشه سناریویی نیست"
                      description="یک سناریوی جدید اضافه کنید یا فیلتر را تغییر دهید."
                    />
                  ) : (
                    <table className="w-full text-sm">
                      <thead className="border-b border-border bg-muted/40 text-muted-foreground text-xs sticky top-0">
                        <tr>
                          <th className="w-8 ps-4 py-2">
                            <button onClick={toggleAll} className="hover:text-foreground">
                              {selectedCases.size === filteredCases.length && filteredCases.length > 0 ? (
                                <CheckSquare className="size-4 text-primary" />
                              ) : (
                                <Square className="size-4" />
                              )}
                            </button>
                          </th>
                          <th className="text-start font-medium px-3 py-2">کد</th>
                          <th className="text-start font-medium px-3 py-2">عنوان</th>
                          <th className="text-start font-medium px-3 py-2">وضعیت</th>
                          <th className="text-start font-medium px-3 py-2">اولویت</th>
                          <th className="text-start font-medium px-3 py-2">اضافه‌کننده</th>
                          <th className="text-start font-medium px-3 py-2">آخرین اجرا</th>
                          <th className="text-start font-medium px-3 py-2">به‌روزرسانی</th>
                        </tr>
                      </thead>
                      <tbody>
                        {filteredCases.map((tc) => (
                          <CaseRow
                            key={tc.id}
                            tc={tc}
                            checked={selectedCases.has(tc.id)}
                            onToggle={() => toggleCase(tc.id)}
                            onView={() => setViewingCaseId(tc.id)}
                          />
                        ))}
                      </tbody>
                    </table>
                  )}
                </div>
              </motion.div>
            )}
          </AnimatePresence>
        </Card>
      </div>

      <PromptDialog
        open={suiteDialogOpen}
        title="ساخت Suite جدید"
        description="یک مجموعه تست (Suite) برای دسته‌بندی پوشه‌ها و سناریوها."
        label="نام Suite"
        placeholder="مثلاً Master یا Regression"
        defaultValue="Master"
        confirmText="ساخت"
        icon={<Layers className="size-5" />}
        onClose={() => setSuiteDialogOpen(false)}
        onConfirm={(name) => {
          createSuiteMut.mutate(name);
          setSuiteDialogOpen(false);
        }}
      />

      <PromptDialog
        open={suiteEditDialog.open}
        title="ویرایش Suite"
        description="نام Suite را تغییر دهید."
        label="نام Suite"
        placeholder="نام Suite"
        defaultValue={suiteEditDialog.suite?.name ?? ''}
        confirmText="ذخیره"
        icon={<Pencil className="size-5" />}
        onClose={() => setSuiteEditDialog({ open: false, suite: null })}
        onConfirm={(name) => {
          if (suiteEditDialog.suite) updateSuiteMut.mutate({ id: suiteEditDialog.suite.id, name });
          setSuiteEditDialog({ open: false, suite: null });
        }}
      />

      <ConfirmDialog
        open={suiteDeleteConfirm.open}
        title="حذف Suite"
        description={`آیا مطمئن هستید که Suite «${suiteDeleteConfirm.suite?.name}» حذف شود؟ تمام پوشه‌ها و سناریوهای مرتبط نیز حذف خواهند شد. این عمل قابل بازگشت نیست.`}
        confirmText="حذف"
        destructive
        icon={<Trash2 className="size-5" />}
        onClose={() => setSuiteDeleteConfirm({ open: false, suite: null })}
        onConfirm={() => {
          if (suiteDeleteConfirm.suite) deleteSuiteMut.mutate(suiteDeleteConfirm.suite.id);
          setSuiteDeleteConfirm({ open: false, suite: null });
        }}
      />

      <PromptDialog
        open={sectionDialog.open}
        title={sectionDialog.parentId ? 'ساخت زیرپوشه' : 'ساخت پوشه ریشه'}
        description={
          sectionDialog.parentId
            ? 'یک پوشه فرزند زیر پوشه انتخاب‌شده ایجاد می‌شود.'
            : 'یک پوشه در ریشه این Suite ساخته می‌شود.'
        }
        label="نام پوشه"
        placeholder="مثلاً Authentication"
        confirmText="ساخت"
        icon={<FolderTree className="size-5" />}
        onClose={() => setSectionDialog({ open: false, parentId: null })}
        onConfirm={(name) => {
          createSectionMut.mutate({ parentId: sectionDialog.parentId, name });
          setSectionDialog({ open: false, parentId: null });
        }}
      />

      <ConfirmDialog
        open={bulkDeleteOpen}
        title="حذف سناریوهای انتخاب‌شده"
        description={`آیا مطمئن هستید که ${formatNumber(selectedCases.size)} سناریو حذف شود؟ این عمل قابل بازگشت نیست.`}
        confirmText="حذف"
        destructive
        icon={<Trash2 className="size-5" />}
        onClose={() => setBulkDeleteOpen(false)}
        onConfirm={() => {
          bulkDeleteMut.mutate(Array.from(selectedCases));
          setBulkDeleteOpen(false);
        }}
      />

      {showQuickRun && (
        <NewRunDialog
          onClose={() => {
            setShowQuickRun(false);
            setSelectedCases(new Set());
          }}
          preSelectedIds={Array.from(selectedCases)}
          preProjectId={activeProjectId!}
        />
      )}
    </div>
  );
}

function SuiteRailItem({
  suite, active, onClick, onEdit, onDelete,
}: {
  suite: Suite;
  active: boolean;
  onClick: () => void;
  onEdit: () => void;
  onDelete: () => void;
}) {
  const [menu, setMenu] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!menu) return;
    function handler(e: MouseEvent) {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) setMenu(false);
    }
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, [menu]);

  return (
    <li className="relative group">
      <button
        onClick={onClick}
        className={`w-full text-start rounded-lg px-2.5 py-2 text-sm transition-colors ${
          active
            ? 'bg-primary/15 text-primary font-medium'
            : 'hover:bg-accent/60 text-foreground/80'
        }`}
      >
        <div className="flex items-center gap-2">
          <Layers className="size-3.5 shrink-0" />
          <span className="truncate flex-1">{suite.name}</span>
        </div>
      </button>
      <button
        onClick={(e) => { e.stopPropagation(); setMenu((m) => !m); }}
        className="absolute end-1 top-1/2 -translate-y-1/2 size-6 rounded-md flex items-center justify-center opacity-0 group-hover:opacity-100 hover:bg-accent text-muted-foreground hover:text-foreground transition-opacity"
        title="گزینه‌ها"
      >
        <MoreVertical className="size-3.5" />
      </button>
      <AnimatePresence>
        {menu && (
          <motion.div
            ref={menuRef}
            initial={{ opacity: 0, scale: 0.95, y: -4 }}
            animate={{ opacity: 1, scale: 1, y: 0 }}
            exit={{ opacity: 0, scale: 0.95 }}
            transition={{ duration: 0.12 }}
            className="absolute z-30 end-1 top-9 min-w-[140px] rounded-lg border border-border bg-popover shadow-soft-lg py-1 text-sm"
          >
            <button
              onClick={() => { setMenu(false); onEdit(); }}
              className="w-full flex items-center gap-2 px-3 py-1.5 hover:bg-accent text-start"
            >
              <Pencil className="size-3.5" /> ویرایش
            </button>
            <div className="my-1 border-t border-border" />
            <button
              onClick={() => { setMenu(false); onDelete(); }}
              className="w-full flex items-center gap-2 px-3 py-1.5 hover:bg-destructive/10 text-destructive text-start"
            >
              <Trash2 className="size-3.5" /> حذف
            </button>
          </motion.div>
        )}
      </AnimatePresence>
    </li>
  );
}

function CaseRow({
  tc, checked, onToggle, onView,
}: { tc: TestCaseSummary; checked: boolean; onToggle: () => void; onView: () => void }) {
  function handleDragStart(e: DragEvent) {
    writeDrag(e, { kind: 'testcase', id: tc.id });
    e.dataTransfer.effectAllowed = 'move';
  }
  return (
    <tr
      draggable
      onDragStart={handleDragStart}
      onClick={onView}
      className={`border-b border-border hover:bg-accent/40 transition-colors cursor-pointer ${checked ? 'bg-primary/5' : ''}`}
    >
      <td className="ps-4 py-2.5 align-middle" onClick={(e) => e.stopPropagation()}>
        <button onClick={onToggle}>
          {checked ? (
            <CheckSquare className="size-4 text-primary" />
          ) : (
            <Square className="size-4 text-muted-foreground" />
          )}
        </button>
      </td>
      <td className="px-3 py-2.5 font-mono text-[11px] text-muted-foreground align-middle">{tc.code}</td>
      <td className="px-3 py-2.5">
        <span className="hover:text-primary font-medium block">
          {tc.title}
        </span>
        {tc.tags?.length > 0 && (
          <div className="flex items-center gap-1 mt-1 flex-wrap">
            {tc.tags.slice(0, 3).map((t) => (
              <span key={t} className="text-[10px] px-1.5 py-0.5 rounded bg-muted text-muted-foreground">#{t}</span>
            ))}
          </div>
        )}
      </td>
      <td className="px-3 py-2.5"><TestCaseStatusBadge status={tc.status} /></td>
      <td className="px-3 py-2.5"><PriorityBadge priority={tc.priority} /></td>
      <td className="px-3 py-2.5">
        <ScenarioAuthor userId={tc.createdByUserId} />
      </td>
      <td className="px-3 py-2.5">
        <ExecutionSummary
          compact
          lastExecutedAtUtc={tc.lastExecutedAtUtc}
          status={tc.lastExecutionStatus}
          runId={tc.lastExecutionRunId}
        />
      </td>
      <td className="px-3 py-2.5 text-xs text-muted-foreground">
        {fmtRelative(tc.updatedAtUtc ?? tc.createdAtUtc)}
      </td>
    </tr>
  );
}

// ── Inline case detail ─────────────────────────────────────────
function InlineCaseDetail({
  caseId,
  projectId,
  onBack,
}: {
  caseId: string;
  projectId: string;
  onBack: () => void;
}) {
  const navigate = useNavigate();
  const qc = useQueryClient();
  const [showRunDialog, setShowRunDialog] = useState(false);

  const { data: tc, isLoading } = useQuery({
    queryKey: ['testcase', caseId],
    queryFn: () => TestCasesApi.get(caseId),
  });
  const { data: cfDefs } = useQuery({
    queryKey: ['custom-fields', projectId],
    queryFn: () => CustomFieldsApi.list(projectId),
    enabled: !!projectId,
  });
  const removeMut = useMutation({
    mutationFn: () => TestCasesApi.remove(caseId),
    onSuccess: () => {
      toast.success('سناریو حذف شد');
      qc.invalidateQueries({ queryKey: ['testcases'] });
      onBack();
    },
    onError: (err) => toast.error(extractError(err)),
  });

  if (isLoading || !tc) {
    return (
      <div className="p-5 space-y-3">
        <Skeleton className="h-8 w-1/3" />
        <Skeleton className="h-32 w-full" />
        <Skeleton className="h-48 w-full" />
      </div>
    );
  }

  return (
    <div className="flex-1 overflow-y-auto">
      {/* Header bar */}
      <div className="sticky top-0 z-10 bg-card border-b border-border px-4 py-3 flex items-center gap-3">
        <Button variant="ghost" size="icon-sm" onClick={onBack} title="بازگشت به لیست">
          <ArrowRight className="size-4" />
        </Button>
        <div className="flex-1 min-w-0">
          <div className="font-mono text-[11px] text-muted-foreground">{tc.code}</div>
          <h2 className="font-bold text-sm truncate">{tc.title}</h2>
        </div>
        <div className="flex items-center justify-end gap-1.5 shrink-0 flex-wrap">
          <Button
            variant="outline"
            size="sm"
            onClick={() => navigate(`/testcases/${caseId}/edit`)}
          >
            <Edit2 className="size-3.5" /> ویرایش
          </Button>
          <Button
            size="sm"
            onClick={() => setShowRunDialog(true)}
          >
            <PlayCircle className="size-3.5" /> اجرای سناریو
          </Button>
          <Button
            variant="ghost"
            size="icon-sm"
            asChild
            title="باز کردن در صفحه جداگانه"
          >
            <Link to={`/testcases/${caseId}`}>
              <ExternalLink className="size-3.5" />
            </Link>
          </Button>
          <Button
            variant="ghost"
            size="icon-sm"
            onClick={() => {
              if (confirm('آیا از حذف این سناریو مطمئن هستید؟')) removeMut.mutate();
            }}
            title="حذف"
          >
            <Trash2 className="size-3.5 text-destructive" />
          </Button>
        </div>
      </div>

      {/* Body */}
      <div className="p-4 space-y-4">
        {/* Meta row */}
        <div className="flex flex-wrap items-center gap-2">
          <TestCaseStatusBadge status={tc.status} />
          <PriorityBadge priority={tc.priority} />
          <Badge variant="info" className="num-fa">v{formatNumber(tc.version)}</Badge>
          <ExecutionSummary
            compact
            lastExecutedAtUtc={tc.lastExecutedAtUtc}
            status={tc.lastExecutionStatus}
            runId={tc.lastExecutionRunId}
          />
          <span className="inline-flex items-center gap-1.5 text-[11px] text-muted-foreground">
            اضافه‌کننده
            <ScenarioAuthor userId={tc.createdByUserId} />
          </span>
          {tc.tags.map((t) => (
            <Badge key={t} variant="secondary" className="text-[11px]">
              <Tag className="size-2.5 me-0.5" />{t}
            </Badge>
          ))}
          <span className="text-[11px] text-muted-foreground ms-auto">
            {fmtDateTime(tc.updatedAtUtc ?? tc.createdAtUtc)}
          </span>
        </div>

        {/* Preconditions */}
        {tc.preconditions && (
          <div className="rounded-lg bg-info/5 border border-info/20 p-3">
            <div className="text-xs text-info font-semibold mb-1 flex items-center gap-1.5">
              <FileText className="size-3" /> پیش‌نیازها
            </div>
            <MarkdownView source={tc.preconditions} />
          </div>
        )}

        {/* Steps */}
        {tc.steps.length > 0 && (
          <div>
            <h3 className="text-xs font-semibold text-muted-foreground mb-2 uppercase tracking-wider">
              مراحل اجرا ({formatNumber(tc.steps.length)})
            </h3>
            <ol className="space-y-2">
              {tc.steps.map((s) => (
                <li key={s.order} className="rounded-lg border border-border p-3">
                  <div className="flex items-start gap-3">
                    <span className="size-6 rounded-full bg-primary text-primary-foreground text-xs font-bold flex items-center justify-center shrink-0 mt-0.5 num-fa">
                      {formatNumber(s.order)}
                    </span>
                    <div className="flex-1 min-w-0 grid grid-cols-1 md:grid-cols-2 gap-2">
                      <div>
                        <div className="text-[10px] text-muted-foreground mb-0.5">عمل</div>
                        <MarkdownView source={s.action} />
                      </div>
                      {s.expectedResult && (
                        <div>
                          <div className="text-[10px] text-muted-foreground mb-0.5">نتیجه مورد انتظار</div>
                          <MarkdownView source={s.expectedResult} className="text-success" />
                        </div>
                      )}
                    </div>
                  </div>
                </li>
              ))}
            </ol>
          </div>
        )}

        {/* Custom fields */}
        {cfDefs && cfDefs.filter((d) => !d.isArchived).length > 0 && tc.customFields && Object.keys(tc.customFields).length > 0 && (
          <div>
            <h3 className="text-xs font-semibold text-muted-foreground mb-2 uppercase tracking-wider flex items-center gap-1.5">
              <Sparkles className="size-3" /> فیلدهای سفارشی
            </h3>
            <CustomFieldsView
              definitions={cfDefs}
              values={tc.customFields}
            />
          </div>
        )}
      </div>
      {showRunDialog && (
        <NewRunDialog
          onClose={() => setShowRunDialog(false)}
          preSelectedIds={[tc.id]}
          preProjectId={tc.projectId}
        />
      )}
    </div>
  );
}

// ── helpers ────────────────────────────────────────────────────
function collectSectionIds(nodes: SectionNode[]): string[] {
  const ids: string[] = [];
  function visit(n: SectionNode) {
    ids.push(n.id);
    n.children.forEach(visit);
  }
  nodes.forEach(visit);
  return ids;
}

function collectDescendants(tree: SuiteTree | undefined, id: string): Set<string> {
  const out = new Set<string>();
  if (!tree) return out;
  function visit(n: SectionNode) {
    out.add(n.id);
    n.children.forEach(visit);
  }
  function find(nodes: SectionNode[]): SectionNode | null {
    for (const n of nodes) {
      if (n.id === id) return n;
      const f = find(n.children);
      if (f) return f;
    }
    return null;
  }
  const root = find(tree.sections);
  if (root) visit(root);
  return out;
}

function breadcrumbForNode(
  tree: SuiteTree | undefined,
  id: string | 'ROOT',
): SectionNode[] {
  if (!tree || id === 'ROOT') return [];
  function dfs(nodes: SectionNode[], path: SectionNode[]): SectionNode[] | null {
    for (const n of nodes) {
      const next = [...path, n];
      if (n.id === id) return next;
      const r = dfs(n.children, next);
      if (r) return r;
    }
    return null;
  }
  return dfs(tree.sections, []) ?? [];
}
