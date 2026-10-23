import { useMemo, useState, type FormEvent } from 'react';
import { useParams, Link } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { ProjectsApi, ProjectMembersApi, ReportingApi } from '@/lib/queries/endpoints';
import { PageHeader } from '@/components/common/PageHeader';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { Skeleton } from '@/components/common/Skeletons';
import { EmptyState } from '@/components/common/EmptyState';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select } from '@/components/ui/native-select';
import { ConfirmDialog } from '@/components/ui/ConfirmDialog';
import { UserAvatar } from '@/components/common/UserAvatar';
import { formatNumber, formatPercent } from '@/lib/utils';
import { fmtDate, fmtRelative } from '@/lib/date';
import { extractError } from '@/lib/api';
import { useAuthStore } from '@/lib/auth/store';
import { isWorkspaceAdmin } from '@/lib/auth/permissions';
import type { ProjectMember, ProjectMemberCandidate, ProjectMemberRole } from '@/lib/types';
import {
  ChevronRight,
  PlayCircle,
  ListChecks,
  Activity,
  Calendar,
  AlertTriangle,
  ArrowLeft,
  Users,
  UserPlus,
  Crown,
  Shield,
  Eye,
  Search,
  X,
  RotateCcw,
} from 'lucide-react';
import { RunStatusBadge } from '@/components/common/StatusBadges';
import { motion } from 'framer-motion';

const PROJECT_ROLE_LABEL: Record<ProjectMemberRole, string> = {
  Owner: 'مالک',
  Admin: 'مدیر',
  Tester: 'تستر',
  Viewer: 'بیننده',
  Excluded: 'محروم',
};

const PROJECT_ROLE_TONE: Record<ProjectMemberRole, 'default' | 'success' | 'info' | 'muted' | 'destructive'> = {
  Owner: 'success',
  Admin: 'info',
  Tester: 'default',
  Viewer: 'muted',
  Excluded: 'destructive',
};

const PROJECT_ASSIGNABLE_ROLES: ProjectMemberRole[] = ['Admin', 'Tester', 'Viewer', 'Excluded'];
const ADD_ASSIGNABLE_ROLES: ProjectMemberRole[] = ['Tester', 'Admin', 'Viewer'];

function normalizeProjectRole(role?: string | null): ProjectMemberRole | null {
  if (role === 'Owner' || role === 'Admin' || role === 'Tester' || role === 'Viewer' || role === 'Excluded') return role;
  return null;
}

function displayMemberRole(member: ProjectMember): ProjectMemberRole | 'Inherited' {
  return normalizeProjectRole(member.overrideRole) ?? normalizeProjectRole(member.effectiveRole) ?? 'Inherited';
}

export default function ProjectDetailPage() {
  const { projectId } = useParams<{ projectId: string }>();
  const qc = useQueryClient();
  const currentUserId = useAuthStore((s) => s.user?.id);
  const me = useAuthStore((s) => s.user);
  const isWorkspaceAdminUser = isWorkspaceAdmin(me);
  const [showAddMember, setShowAddMember] = useState(false);
  const [transferTarget, setTransferTarget] = useState<ProjectMember | null>(null);

  const { data: project, isLoading: pl } = useQuery({
    queryKey: ['project', projectId],
    queryFn: () => ProjectsApi.get(projectId!),
    enabled: !!projectId,
  });
  const { data: dashboard } = useQuery({
    queryKey: ['project-dashboard', projectId],
    queryFn: () => ReportingApi.projectDashboard(projectId!),
    enabled: !!projectId,
    retry: false,
  });
  const { data: milestones } = useQuery({
    queryKey: ['milestones', projectId],
    queryFn: () => ProjectsApi.milestones(projectId!),
    enabled: !!projectId,
  });
  const { data: suites } = useQuery({
    queryKey: ['suites', projectId],
    queryFn: () => ProjectsApi.suites(projectId!),
    enabled: !!projectId,
  });
  const { data: projectMembers, isLoading: membersLoading } = useQuery({
    queryKey: ['project-members', projectId],
    queryFn: () => ProjectMembersApi.list(projectId!),
    enabled: !!projectId,
  });

  const isProjectOwner = projectMembers?.currentUserProjectRole === 'Owner';
  const canManageMembers =
    isWorkspaceAdminUser ||
    projectMembers?.currentUserProjectRole === 'Owner' ||
    projectMembers?.currentUserProjectRole === 'Admin';

  const invalidateProjectMemberData = () => {
    qc.invalidateQueries({ queryKey: ['project-members', projectId] });
    qc.invalidateQueries({ queryKey: ['project-member-candidates', projectId] });
    qc.invalidateQueries({ queryKey: ['project', projectId] });
    qc.invalidateQueries({ queryKey: ['project-dashboard', projectId] });
    qc.invalidateQueries({ queryKey: ['projects'] });
  };

  const setProjectRole = useMutation({
    mutationFn: ({ userId, role, displayName }: { userId: string; role: ProjectMemberRole; displayName: string }) =>
      ProjectMembersApi.setRole(projectId!, userId, role),
    onSuccess: (_d, vars) => {
      toast.success(
        `نقش «${vars.displayName}» در این پروژه به «${PROJECT_ROLE_LABEL[vars.role]}» تغییر کرد.`,
      );
      invalidateProjectMemberData();
    },
    onError: (err) => toast.error(extractError(err, 'خطا در تغییر نقش پروژه')),
  });

  const resetProjectRole = useMutation({
    mutationFn: ({ userId }: { userId: string; displayName: string }) =>
      ProjectMembersApi.reset(projectId!, userId),
    onSuccess: (_d, vars) => {
      toast.success(
        `نقش اختصاصی «${vars.displayName}» در این پروژه برداشته شد؛ از این به بعد نقش فضای کاری اعمال می‌شود.`,
      );
      invalidateProjectMemberData();
    },
    onError: (err) => toast.error(extractError(err, 'خطا در حذف نقش اختصاصی')),
  });

  const transferOwnership = useMutation({
    mutationFn: (userId: string) => ProjectMembersApi.transferOwnership(projectId!, userId),
    onSuccess: () => {
      toast.success('مالکیت پروژه منتقل شد');
      setTransferTarget(null);
      invalidateProjectMemberData();
    },
    onError: (err) => toast.error(extractError(err, 'خطا در انتقال مالکیت پروژه')),
  });

  if (pl)
    return (
      <div className="space-y-6">
        <Skeleton className="h-9 w-80" />
        <div className="grid grid-cols-4 gap-4">
          {Array.from({ length: 4 }).map((_, i) => (
            <Skeleton key={i} className="h-24" />
          ))}
        </div>
      </div>
    );

  if (!project)
    return (
      <EmptyState
        icon={<AlertTriangle className="size-7" />}
        title="پروژه یافت نشد"
        description="این پروژه ممکن است حذف یا آرشیو شده باشد."
      />
    );

  // داشبورد اختیاری است؛ اگر سرویس Reporting هنوز این پروژه را نشناسد، با مقادیر صفر نمایش می‌دهیم.
  const stats = dashboard ?? {
    projectId: project.id,
    projectCode: project.code,
    projectName: project.name,
    isArchived: project.isArchived,
    totalRuns: 0,
    openRuns: 0,
    completedRuns: 0,
    totalTestCases: 0,
    recordedResults: 0,
    counts: { untested: 0, passed: 0, failed: 0, blocked: 0, retest: 0, skipped: 0, total: 0, executed: 0, passRate: 0 },
    recentRuns: [],
  };

  return (
    <motion.div
      initial={{ opacity: 0, y: 6 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.25 }}
      className="space-y-6"
    >
      <PageHeader
        breadcrumbs={
          <div className="flex items-center gap-1 text-xs text-muted-foreground">
            <Link to="/projects" className="hover:text-foreground">پروژه‌ها</Link>
            <ChevronRight className="size-3 rotate-180" />
            <span>{project.code}</span>
          </div>
        }
        title={
          <div className="flex items-center gap-3">
            <div className="size-11 rounded-xl bg-gradient-to-br from-primary to-info text-white font-bold flex items-center justify-center shadow-soft">
              {project.code.slice(0, 2)}
            </div>
            {project.name}
            {project.isArchived && <Badge variant="muted">آرشیو شده</Badge>}
          </div>
        }
        description={project.description || 'بدون توضیحات'}
        actions={
          <>
            <Button variant="outline" asChild>
              <Link to={`/testcases?projectId=${project.id}`}>سناریوها</Link>
            </Button>
            <Button asChild>
              <Link to={`/runs?projectId=${project.id}`}>
                <PlayCircle className="size-4" /> اجراها
              </Link>
            </Button>
          </>
        }
      />

      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
        <Card>
          <CardContent className="p-5">
            <div className="text-xs text-muted-foreground">سناریوها</div>
            <div className="text-3xl font-bold mt-2 num-fa">{formatNumber(stats.totalTestCases)}</div>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="p-5">
            <div className="text-xs text-muted-foreground">اجراها</div>
            <div className="text-3xl font-bold mt-2 num-fa">{formatNumber(stats.totalRuns)}</div>
            <div className="text-xs text-muted-foreground mt-1 num-fa">
              {formatNumber(stats.openRuns)} باز · {formatNumber(stats.completedRuns)} بسته
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="p-5">
            <div className="text-xs text-muted-foreground">نتایج ثبت شده</div>
            <div className="text-3xl font-bold mt-2 num-fa">{formatNumber(stats.recordedResults)}</div>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="p-5">
            <div className="flex items-center justify-between">
              <div>
                <div className="text-xs text-muted-foreground">نرخ موفقیت</div>
                <div className="text-3xl font-bold mt-2 num-fa">{formatPercent(stats.counts.passRate)}</div>
              </div>
              <Activity
                className={
                  stats.counts.passRate >= 80
                    ? 'size-8 text-success'
                    : stats.counts.passRate >= 50
                      ? 'size-8 text-warning'
                      : 'size-8 text-destructive'
                }
              />
            </div>
          </CardContent>
        </Card>
      </div>

      <ProjectMembersPanel
        members={projectMembers?.members ?? []}
        isLoading={membersLoading}
        isOwner={isProjectOwner}
        canManageMembers={canManageMembers}
        currentUserId={currentUserId ?? null}
        roleBusyUserId={setProjectRole.variables?.userId ?? null}
        resetBusyUserId={resetProjectRole.variables?.userId ?? null}
        onAdd={() => setShowAddMember(true)}
        onSetRole={(userId, role, displayName) => setProjectRole.mutate({ userId, role, displayName })}
        onResetRole={(userId, displayName) => resetProjectRole.mutate({ userId, displayName })}
        onTransfer={(member) => setTransferTarget(member)}
      />

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
        <Card className="lg:col-span-2">
          <CardHeader className="flex flex-row items-center justify-between">
            <div>
              <CardTitle>اجراهای اخیر</CardTitle>
              <CardDescription>۱۰ اجرای آخر این پروژه</CardDescription>
            </div>
            <Button variant="ghost" size="sm" asChild>
              <Link to={`/runs?projectId=${project.id}`}>
                همه <ArrowLeft className="size-3" />
              </Link>
            </Button>
          </CardHeader>
          <CardContent className="px-0">
            {stats.recentRuns.length === 0 ? (
              <EmptyState icon={<PlayCircle className="size-7" />} title="اجرایی ثبت نشده" />
            ) : (
              <ul className="divide-y divide-border">
                {stats.recentRuns.map((r) => (
                  <li key={r.runId} className="px-6 py-3 hover:bg-accent/40">
                    <Link to={`/runs/${r.runId}`} className="flex items-center gap-3">
                      <div className="size-8 rounded-md bg-primary/10 text-primary text-[10px] font-mono flex items-center justify-center shrink-0">
                        {r.code}
                      </div>
                      <div className="flex-1 min-w-0">
                        <div className="font-medium text-sm truncate">{r.name}</div>
                        <div className="text-xs text-muted-foreground">{fmtRelative(r.createdAtUtc)}</div>
                      </div>
                      <Badge variant="success">{formatNumber(r.passed)} موفق</Badge>
                      <Badge variant="destructive">{formatNumber(r.failed)} ناموفق</Badge>
                      <RunStatusBadge status={r.status} />
                    </Link>
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>

        <div className="space-y-4">
          <Card>
            <CardHeader className="flex flex-row items-center justify-between">
              <CardTitle className="flex items-center gap-2">
                <Calendar className="size-4 text-info" /> نقاط عطف
              </CardTitle>
            </CardHeader>
            <CardContent className="px-0 pb-0">
              {!milestones || milestones.length === 0 ? (
                <div className="px-6 pb-6 text-sm text-muted-foreground">نقطه عطفی تعریف نشده.</div>
              ) : (
                <ul className="divide-y divide-border">
                  {milestones.slice(0, 5).map((m) => (
                    <li key={m.id} className="px-6 py-3 text-sm">
                      <div className="font-medium">{m.name}</div>
                      <div className="text-xs text-muted-foreground mt-0.5">
                        {m.dueDateUtc ? `سررسید: ${fmtDate(m.dueDateUtc)}` : 'بدون سررسید'} · {m.status}
                      </div>
                    </li>
                  ))}
                </ul>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <ListChecks className="size-4 text-primary" /> سوئیت‌ها
              </CardTitle>
            </CardHeader>
            <CardContent className="px-0 pb-0">
              {!suites || suites.length === 0 ? (
                <div className="px-6 pb-6 text-sm text-muted-foreground">سوئیتی تعریف نشده.</div>
              ) : (
                <ul className="divide-y divide-border">
                  {suites.map((s) => (
                    <li key={s.id} className="px-6 py-3 text-sm flex items-center justify-between">
                      <span className="font-medium">{s.name}</span>
                      {s.description && <span className="text-xs text-muted-foreground">{s.description}</span>}
                    </li>
                  ))}
                </ul>
              )}
            </CardContent>
          </Card>
        </div>
      </div>

      <AddProjectMemberDialog
        open={showAddMember}
        projectId={project.id}
        onClose={() => setShowAddMember(false)}
        onAdded={() => {
          setShowAddMember(false);
          invalidateProjectMemberData();
        }}
      />

      <ConfirmDialog
        open={!!transferTarget}
        title="انتقال مالکیت پروژه"
        description={
          transferTarget && (
            <span>
              مالکیت کامل پروژه به «<strong>{transferTarget.displayName || transferTarget.email}</strong>» منتقل می‌شود و نقش شما در پروژه «مدیر» خواهد شد.
            </span>
          )
        }
        confirmText="انتقال مالکیت"
        icon={<Crown className="size-5" />}
        onConfirm={async () => {
          if (transferTarget) await transferOwnership.mutateAsync(transferTarget.userId);
        }}
        onClose={() => setTransferTarget(null)}
      />
    </motion.div>
  );
}

function ProjectRoleBadge({ role }: { role: ProjectMemberRole | 'Inherited' }) {
  if (role === 'Inherited') {
    return <Badge variant="muted">ارثی</Badge>;
  }
  const Icon = role === 'Owner' ? Crown : role === 'Admin' ? Shield : Eye;
  return (
    <Badge variant={PROJECT_ROLE_TONE[role]} className="gap-1">
      <Icon className="size-3" />
      {PROJECT_ROLE_LABEL[role]}
    </Badge>
  );
}

function ProjectMembersPanel({
  members,
  isLoading,
  isOwner,
  canManageMembers,
  currentUserId,
  roleBusyUserId,
  resetBusyUserId,
  onAdd,
  onSetRole,
  onResetRole,
  onTransfer,
}: {
  members: ProjectMember[];
  isLoading: boolean;
  isOwner: boolean;
  canManageMembers: boolean;
  currentUserId: string | null;
  roleBusyUserId: string | null;
  resetBusyUserId: string | null;
  onAdd: () => void;
  onSetRole: (userId: string, role: ProjectMemberRole, displayName: string) => void;
  onResetRole: (userId: string, displayName: string) => void;
  onTransfer: (member: ProjectMember) => void;
}) {
  const [search, setSearch] = useState('');
  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return members;
    return members.filter((m) =>
      `${m.displayName ?? ''} ${m.email ?? ''}`.toLowerCase().includes(q),
    );
  }, [members, search]);

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between gap-3 flex-wrap">
        <div>
          <CardTitle className="flex items-center gap-2">
            <Users className="size-4 text-primary" />
            اعضای پروژه
            <Badge variant="muted" className="font-mono">{formatNumber(members.length)}</Badge>
          </CardTitle>
          <CardDescription>مدیریت نقش‌های اختصاصی همین پروژه</CardDescription>
        </div>
        <div className="flex items-center gap-2 w-full sm:w-auto">
          <div className="relative flex-1 sm:w-64">
            <Search className="absolute right-3 top-1/2 -translate-y-1/2 size-4 text-muted-foreground pointer-events-none" />
            <Input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="جست‌وجوی عضو..."
              className="pr-9"
            />
          </div>
          {canManageMembers && (
            <Button onClick={onAdd} className="shrink-0">
              <UserPlus className="size-4" />
              افزودن عضو
            </Button>
          )}
        </div>
      </CardHeader>
      <CardContent className="px-0">
        {isLoading ? (
          <div className="space-y-2 px-6 pb-6">
            {Array.from({ length: 4 }).map((_, i) => <Skeleton key={i} className="h-14" />)}
          </div>
        ) : filtered.length === 0 ? (
          <EmptyState icon={<Users className="size-7" />} title="عضوی یافت نشد" />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-y border-border bg-muted/30 text-xs text-muted-foreground">
                  <th className="px-6 py-3 text-right font-medium">کاربر</th>
                  <th className="px-3 py-3 text-right font-medium">نقش پروژه</th>
                  <th className="hidden px-3 py-3 text-right font-medium md:table-cell">وضعیت</th>
                  <th className="px-6 py-3 text-left font-medium">عملیات</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {filtered.map((member) => {
                  const role = displayMemberRole(member);
                  const explicitRole = role === 'Inherited' ? 'Tester' : role;
                  const isCurrentUser = member.userId === currentUserId;
                  const isProjectOwnerMember = role === 'Owner';
                  const canEdit = canManageMembers && !isProjectOwnerMember && !isCurrentUser;
                  const canReset = canEdit && member.source !== 'workspace';
                  const canTransfer = isOwner && !isProjectOwnerMember && !isCurrentUser && member.isActive;

                  return (
                    <tr key={member.userId} className="transition-colors hover:bg-accent/40">
                      <td className="px-6 py-3">
                        <div className="flex min-w-0 items-center gap-3">
                          <UserAvatar
                            user={{
                              id: member.userId,
                              displayName: member.displayName,
                              email: member.email,
                              avatarUrl: member.avatarUrl,
                            }}
                            size="sm"
                          />
                          <div className="min-w-0">
                            <div className="flex items-center gap-2 font-medium">
                              <span className="truncate">{member.displayName || member.email}</span>
                              {isCurrentUser && <Badge variant="outline" size="sm">شما</Badge>}
                              {!member.isActive && <Badge variant="muted" size="sm">غیرفعال</Badge>}
                            </div>
                            <div className="truncate text-xs text-muted-foreground" dir="ltr">{member.email}</div>
                          </div>
                        </div>
                      </td>
                      <td className="px-3 py-3">
                        {canEdit ? (
                          <Select
                            value={member.source === 'workspace' || role === 'Inherited' ? '' : explicitRole}
                            disabled={roleBusyUserId === member.userId}
                            onChange={(e) => {
                              const next = e.target.value as ProjectMemberRole | '';
                              if (!next) {
                                onResetRole(member.userId, member.displayName || member.email);
                                return;
                              }
                              onSetRole(member.userId, next, member.displayName || member.email);
                            }}
                            className="h-8 w-36 text-xs"
                          >
                            <option value="">ارث‌بری از فضا</option>
                            {PROJECT_ASSIGNABLE_ROLES.map((r) => (
                              <option key={r} value={r}>{PROJECT_ROLE_LABEL[r]}</option>
                            ))}
                          </Select>
                        ) : (
                          <ProjectRoleBadge role={role} />
                        )}
                      </td>
                      <td className="hidden px-3 py-3 text-xs text-muted-foreground md:table-cell">
                        {member.source === 'workspace'
                          ? 'ارث از فضا'
                          : member.source === 'excluded'
                            ? 'محروم'
                            : member.addedAtUtc
                              ? fmtRelative(member.addedAtUtc)
                              : 'اختصاصی'}
                      </td>
                      <td className="px-6 py-3">
                        <div className="flex items-center justify-end gap-1">
                          {canTransfer && (
                            <Button
                              variant="ghost"
                              size="icon-sm"
                              title="انتقال مالکیت"
                              onClick={() => onTransfer(member)}
                            >
                              <Crown className="size-3.5" />
                            </Button>
                          )}
                          {canReset && (
                            <Button
                              variant="ghost"
                              size="icon-sm"
                              title="بازنشانی نقش"
                              disabled={resetBusyUserId === member.userId}
                              onClick={() => onResetRole(member.userId, member.displayName || member.email)}
                            >
                              <RotateCcw className="size-3.5" />
                            </Button>
                          )}
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </CardContent>
    </Card>
  );
}

function AddProjectMemberDialog({
  open,
  projectId,
  onClose,
  onAdded,
}: {
  open: boolean;
  projectId: string;
  onClose: () => void;
  onAdded: () => void;
}) {
  const [search, setSearch] = useState('');
  const [role, setRole] = useState<ProjectMemberRole>('Tester');
  const querySearch = search.trim();

  const { data, isFetching } = useQuery({
    queryKey: ['project-member-candidates', projectId, querySearch],
    queryFn: () =>
      ProjectMembersApi.candidates(projectId, {
        search: querySearch || undefined,
        page: 1,
        pageSize: 25,
      }),
    enabled: open && !!projectId,
  });

  const addMember = useMutation({
    mutationFn: ({ userId, selectedRole }: { userId: string; selectedRole: ProjectMemberRole }) =>
      ProjectMembersApi.add(projectId, { userId, role: selectedRole }),
    onSuccess: (_d, vars) => {
      toast.success(
        `عضو با نقش «${PROJECT_ROLE_LABEL[vars.selectedRole]}» به پروژه اضافه شد.`,
      );
      onAdded();
    },
    onError: (err) => toast.error(extractError(err, 'خطا در افزودن عضو به پروژه')),
  });

  function submitCandidate(e: FormEvent, candidate: ProjectMemberCandidate) {
    e.preventDefault();
    if (candidate.isInProject || addMember.isPending) return;
    addMember.mutate({ userId: candidate.userId, selectedRole: role });
  }

  if (!open) return null;

  const candidates = data?.items ?? [];

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4 backdrop-blur-sm"
      onClick={onClose}
    >
      <motion.div
        initial={{ opacity: 0, scale: 0.96, y: 8 }}
        animate={{ opacity: 1, scale: 1, y: 0 }}
        onClick={(e) => e.stopPropagation()}
        className="w-full max-w-2xl rounded-2xl border border-border bg-card shadow-soft-lg"
      >
        <div className="flex items-start justify-between gap-3 border-b border-border p-5">
          <div className="flex items-start gap-3">
            <div className="flex size-10 items-center justify-center rounded-xl bg-primary/10 text-primary">
              <UserPlus className="size-5" />
            </div>
            <div>
              <h3 className="font-semibold">افزودن عضو به پروژه</h3>
              <p className="mt-1 text-xs text-muted-foreground">کاربران فعال ثبت‌نام‌شده</p>
            </div>
          </div>
          <Button type="button" variant="ghost" size="icon-sm" onClick={onClose} aria-label="بستن">
            <X className="size-4" />
          </Button>
        </div>

        <div className="space-y-4 p-5">
          <div className="grid gap-3 sm:grid-cols-[1fr_180px]">
            <div>
              <Label htmlFor="project-member-search">جست‌وجو</Label>
              <div className="relative">
                <Search className="absolute right-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
                <Input
                  id="project-member-search"
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                  placeholder="نام یا ایمیل"
                  className="pr-9"
                  autoFocus
                />
              </div>
            </div>
            <div>
              <Label htmlFor="project-member-role">نقش</Label>
              <Select
                id="project-member-role"
                value={role}
                onChange={(e) => setRole(e.target.value as ProjectMemberRole)}
              >
                {ADD_ASSIGNABLE_ROLES.map((r) => (
                  <option key={r} value={r}>{PROJECT_ROLE_LABEL[r]}</option>
                ))}
              </Select>
            </div>
          </div>

          <div className="max-h-[420px] overflow-y-auto rounded-xl border border-border">
            {isFetching ? (
              <div className="space-y-2 p-4">
                {Array.from({ length: 5 }).map((_, i) => <Skeleton key={i} className="h-14" />)}
              </div>
            ) : candidates.length === 0 ? (
              <EmptyState icon={<Search className="size-7" />} title="کاربری یافت نشد" />
            ) : (
              <div className="divide-y divide-border">
                {candidates.map((candidate) => {
                  const candidateRole = normalizeProjectRole(candidate.projectRole);
                  const addingThis = addMember.isPending && addMember.variables?.userId === candidate.userId;
                  return (
                    <form
                      key={candidate.userId}
                      onSubmit={(e) => submitCandidate(e, candidate)}
                      className="flex items-center gap-3 px-4 py-3 transition-colors hover:bg-accent/40"
                    >
                      <UserAvatar
                        user={{
                          id: candidate.userId,
                          displayName: candidate.displayName,
                          email: candidate.email,
                          avatarUrl: candidate.avatarUrl,
                        }}
                        size="sm"
                      />
                      <div className="min-w-0 flex-1">
                        <div className="truncate text-sm font-medium">{candidate.displayName || candidate.email}</div>
                        <div className="truncate text-xs text-muted-foreground" dir="ltr">{candidate.email}</div>
                      </div>
                      {candidate.isInProject && (
                        <Badge variant="muted" className="shrink-0">
                          {candidateRole ? PROJECT_ROLE_LABEL[candidateRole] : 'عضو پروژه'}
                        </Badge>
                      )}
                      <Button
                        type="submit"
                        size="sm"
                        disabled={candidate.isInProject || addMember.isPending}
                        loading={addingThis}
                      >
                        افزودن
                      </Button>
                    </form>
                  );
                })}
              </div>
            )}
          </div>
        </div>
      </motion.div>
    </div>
  );
}
