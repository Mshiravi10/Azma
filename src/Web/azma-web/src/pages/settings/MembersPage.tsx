import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { motion, AnimatePresence } from 'framer-motion';
import {
  Users,
  UserPlus,
  Crown,
  Shield,
  Mail,
  Trash2,
  Copy,
  Check,
  X,
  Search,
  AlertTriangle,
  Eye,
  Pencil,
  Hash,
  FolderKanban,
} from 'lucide-react';

import { PageHeader } from '@/components/common/PageHeader';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select } from '@/components/ui/native-select';
import { ConfirmDialog } from '@/components/ui/ConfirmDialog';
import { UserAvatar } from '@/components/common/UserAvatar';
import { EmptyState } from '@/components/common/EmptyState';
import { Skeleton } from '@/components/common/Skeletons';
import { extractError } from '@/lib/api';
import { fmtRelative } from '@/lib/date';
import { formatNumber } from '@/lib/utils';
import { MembersApi, ProjectMembersApi, ProjectsApi } from '@/lib/queries/endpoints';
import { useAuthStore } from '@/lib/auth/store';
import { isWorkspaceAdmin } from '@/lib/auth/permissions';
import type { Membership, MemberCandidate, WorkspaceRole, ProjectMemberRole } from '@/lib/types';

const ROLE_META: Record<WorkspaceRole, { label: string; tone: 'default' | 'success' | 'info' | 'muted'; icon: React.ComponentType<{ className?: string }> }> = {
  Owner:  { label: 'مالک',     tone: 'success', icon: Crown },
  Admin:  { label: 'مدیر',     tone: 'info',    icon: Shield },
  Tester: { label: 'تستر',     tone: 'default', icon: Pencil },
  Viewer: { label: 'بیننده',   tone: 'muted',   icon: Eye },
};

const PROJECT_ROLE_LABEL: Record<ProjectMemberRole, string> = {
  Owner: 'مالک',
  Admin: 'مدیر',
  Tester: 'تستر',
  Viewer: 'بیننده',
  Excluded: 'محروم',
};

const ASSIGNABLE: WorkspaceRole[] = ['Admin', 'Tester', 'Viewer'];
const PROJECT_ASSIGNABLE: ProjectMemberRole[] = ['Admin', 'Tester', 'Viewer', 'Excluded'];

function RoleBadge({ role }: { role: WorkspaceRole }) {
  const meta = ROLE_META[role] ?? ROLE_META.Tester;
  const Icon = meta.icon;
  return (
    <Badge variant={meta.tone} className="gap-1">
      <Icon className="size-3" />
      {meta.label}
    </Badge>
  );
}

export default function MembersPage() {
  const qc = useQueryClient();
  const me = useAuthStore((s) => s.user);
  const myId = me?.id;
  const myRole: WorkspaceRole | undefined = useMemo(() => {
    return me?.workspaces?.find((w) => w.id === me?.workspaceId)?.role as WorkspaceRole | undefined;
  }, [me]);

  const isOwner = myRole === 'Owner';
  const isAdmin = isWorkspaceAdmin(me);

  const [search, setSearch] = useState('');
  const [showAddDialog, setShowAddDialog] = useState(false);
  const [tempPassword, setTempPassword] = useState<{ email: string; password: string } | null>(null);
  const [removeTarget, setRemoveTarget] = useState<Membership | null>(null);
  const [transferTarget, setTransferTarget] = useState<Membership | null>(null);
  const [selectedUserId, setSelectedUserId] = useState<string | null>(null);

  const { data: members, isLoading } = useQuery({
    queryKey: ['members'],
    queryFn: () => MembersApi.list(),
  });

  const selectedMember = useMemo(
    () => members?.find((m) => m.userId === selectedUserId) ?? null,
    [members, selectedUserId],
  );

  const updateRole = useMutation({
    mutationFn: ({ userId, role }: { userId: string; role: WorkspaceRole; displayName: string }) =>
      MembersApi.updateRole(userId, role),
    onSuccess: (_d, vars) => {
      toast.success(
        `نقش فضای کاری «${vars.displayName}» به «${ROLE_META[vars.role].label}» تغییر کرد.`,
      );
      qc.invalidateQueries({ queryKey: ['members'] });
    },
    onError: (err) => toast.error(extractError(err, 'خطا در تغییر نقش')),
  });

  const remove = useMutation({
    mutationFn: (userId: string) => MembersApi.remove(userId),
    onSuccess: () => {
      toast.success('عضو حذف شد');
      setRemoveTarget(null);
      if (removeTarget?.userId === selectedUserId) setSelectedUserId(null);
      qc.invalidateQueries({ queryKey: ['members'] });
    },
    onError: (err) => toast.error(extractError(err, 'خطا در حذف عضو')),
  });

  const transfer = useMutation({
    mutationFn: (userId: string) => MembersApi.transferOwnership(userId),
    onSuccess: () => {
      toast.success('مالکیت منتقل شد');
      setTransferTarget(null);
      qc.invalidateQueries({ queryKey: ['members'] });
    },
    onError: (err) => toast.error(extractError(err, 'خطا در انتقال مالکیت')),
  });

  const filtered = useMemo(() => {
    if (!members) return [];
    if (!search.trim()) return members;
    const s = search.trim().toLowerCase();
    return members.filter(
      (m) => m.displayName.toLowerCase().includes(s) || m.email.toLowerCase().includes(s),
    );
  }, [members, search]);

  return (
    <motion.div
      initial={{ opacity: 0, y: 6 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.2 }}
      className="space-y-6"
    >
      <PageHeader
        title="اعضا و دسترسی‌ها"
        description="مدیریت کاربران فضای کاری و تنظیم نقش‌های اختصاصی هر پروژه."
        actions={
          isAdmin && (
            <Button onClick={() => setShowAddDialog(true)}>
              <UserPlus className="size-4" />
              افزودن عضو
            </Button>
          )
        }
      />

      <div className="grid grid-cols-12 gap-4 min-h-[calc(100vh-220px)]">
        {/* Members list */}
        <Card className="col-span-12 lg:col-span-7 overflow-hidden flex flex-col">
          <CardHeader className="flex flex-row items-center justify-between gap-3 flex-wrap shrink-0">
            <CardTitle className="flex items-center gap-2">
              <Users className="size-4 text-primary" />
              اعضا
              {members && (
                <Badge variant="muted" className="font-mono">
                  {formatNumber(members.length)}
                </Badge>
              )}
            </CardTitle>
            <div className="relative w-full sm:w-64">
              <Search className="absolute right-3 top-1/2 -translate-y-1/2 size-4 text-muted-foreground pointer-events-none" />
              <Input
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder="جستجو در اعضا…"
                className="pr-9"
              />
            </div>
          </CardHeader>
          <CardContent className="px-0 flex-1 overflow-y-auto">
            {isLoading ? (
              <div className="space-y-2 p-6">
                {Array.from({ length: 4 }).map((_, i) => <Skeleton key={i} className="h-14" />)}
              </div>
            ) : filtered.length === 0 ? (
              <EmptyState
                icon={<Users className="size-7" />}
                title="عضوی یافت نشد"
                description={search ? 'با کلیدواژه‌ای دیگر جستجو کنید.' : 'هنوز عضوی اضافه نشده.'}
              />
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="text-xs text-muted-foreground border-b border-border bg-muted/30">
                      <th className="text-right font-medium px-6 py-3">کاربر</th>
                      <th className="text-right font-medium px-3 py-3">نقش</th>
                      <th className="text-right font-medium px-3 py-3 hidden md:table-cell">آخرین ورود</th>
                      <th className="text-left font-medium px-6 py-3">عملیات</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-border">
                    {filtered.map((m) => {
                      const isSelf = m.userId === myId;
                      const isTargetOwner = m.role === 'Owner';
                      const canChangeRole = isAdmin && !isTargetOwner && !isSelf;
                      const canRemove = isAdmin && !isTargetOwner && !isSelf;
                      const canTransfer = isOwner && !isTargetOwner && m.role === 'Admin';
                      const isSelected = m.userId === selectedUserId;

                      return (
                        <tr
                          key={m.userId}
                          onClick={() => setSelectedUserId(isSelected ? null : m.userId)}
                          className={[
                            'hover:bg-accent/40 transition-colors cursor-pointer',
                            isSelected ? 'bg-primary/5 border-s-2 border-s-primary' : '',
                          ].join(' ')}
                        >
                          <td className="px-6 py-3">
                            <div className="flex items-center gap-3 min-w-0">
                              <UserAvatar
                                user={{ id: m.userId, displayName: m.displayName, email: m.email, avatarUrl: m.avatarUrl }}
                                size="sm"
                              />
                              <div className="min-w-0">
                                <div className="font-medium truncate flex items-center gap-2">
                                  {m.displayName || '—'}
                                  {isSelf && <Badge variant="outline" className="text-[10px] py-0">شما</Badge>}
                                  {!m.isActive && <Badge variant="muted" className="text-[10px] py-0">غیرفعال</Badge>}
                                </div>
                                <div className="text-xs text-muted-foreground truncate" dir="ltr">{m.email}</div>
                              </div>
                            </div>
                          </td>
                          <td className="px-3 py-3" onClick={(e) => e.stopPropagation()}>
                            {canChangeRole ? (
                              <RoleSelect
                                value={m.role}
                                isOwner={isOwner}
                                busy={updateRole.isPending && updateRole.variables?.userId === m.userId}
                                onChange={(role) => updateRole.mutate({ userId: m.userId, role, displayName: m.displayName || m.email })}
                              />
                            ) : (
                              <RoleBadge role={m.role} />
                            )}
                          </td>
                          <td className="px-3 py-3 text-xs text-muted-foreground hidden md:table-cell">
                            {m.lastLoginAtUtc ? fmtRelative(m.lastLoginAtUtc) : '—'}
                          </td>
                          <td className="px-6 py-3" onClick={(e) => e.stopPropagation()}>
                            <div className="flex items-center justify-end gap-1">
                              {canTransfer && (
                                <Button variant="ghost" size="sm" title="انتقال مالکیت" onClick={() => setTransferTarget(m)}>
                                  <Crown className="size-3.5" />
                                </Button>
                              )}
                              {canRemove && (
                                <Button
                                  variant="ghost" size="sm"
                                  className="text-destructive hover:text-destructive"
                                  title="حذف از فضای کاری"
                                  onClick={() => setRemoveTarget(m)}
                                >
                                  <Trash2 className="size-3.5" />
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

        {/* Project roles side panel */}
        <Card className="col-span-12 lg:col-span-5 overflow-hidden flex flex-col">
          <AnimatePresence mode="wait">
            {selectedMember ? (
              <motion.div
                key={selectedMember.userId}
                initial={{ opacity: 0, x: 20 }}
                animate={{ opacity: 1, x: 0 }}
                exit={{ opacity: 0, x: 20 }}
                transition={{ duration: 0.15 }}
                className="flex-1 flex flex-col overflow-hidden"
              >
                <ProjectRolesPanel
                  member={selectedMember}
                  isAdmin={isAdmin}
                  onClose={() => setSelectedUserId(null)}
                />
              </motion.div>
            ) : (
              <motion.div
                key="empty"
                initial={{ opacity: 0 }}
                animate={{ opacity: 1 }}
                className="flex-1 flex items-center justify-center p-6"
              >
                <div className="text-center space-y-2">
                  <FolderKanban className="size-10 text-muted-foreground/30 mx-auto" />
                  <p className="text-sm text-muted-foreground">
                    یک عضو را از لیست انتخاب کنید تا نقش‌های پروژه‌ای وی نمایش داده شود.
                  </p>
                </div>
              </motion.div>
            )}
          </AnimatePresence>
        </Card>
      </div>

      <AddMemberDialog
        open={showAddDialog}
        onClose={() => setShowAddDialog(false)}
        canAssignAdmin={isOwner}
        onSuccess={(email, password) => {
          setShowAddDialog(false);
          if (password) setTempPassword({ email, password });
          qc.invalidateQueries({ queryKey: ['members'] });
        }}
      />

      <TempPasswordDialog info={tempPassword} onClose={() => setTempPassword(null)} />

      <ConfirmDialog
        open={!!removeTarget}
        title="حذف عضو از فضای کاری"
        description={
          removeTarget && (
            <span>
              «<strong>{removeTarget.displayName}</strong>» از این فضای کاری حذف خواهد شد.
            </span>
          )
        }
        confirmText="حذف"
        destructive
        icon={<AlertTriangle className="size-5" />}
        onConfirm={async () => { if (removeTarget) await remove.mutateAsync(removeTarget.userId); }}
        onClose={() => setRemoveTarget(null)}
      />

      <ConfirmDialog
        open={!!transferTarget}
        title="انتقال مالکیت فضای کاری"
        description={
          transferTarget && (
            <span>
              مالکیت به «<strong>{transferTarget.displayName}</strong>» منتقل می‌شود و نقش شما به «مدیر» تغییر می‌کند.
            </span>
          )
        }
        confirmText="انتقال مالکیت"
        icon={<Crown className="size-5" />}
        onConfirm={async () => { if (transferTarget) await transfer.mutateAsync(transferTarget.userId); }}
        onClose={() => setTransferTarget(null)}
      />
    </motion.div>
  );
}

// ─── Project Roles Panel ────────────────────────────────────────────────
function ProjectRolesPanel({
  member,
  isAdmin,
  onClose,
}: {
  member: Membership;
  isAdmin: boolean;
  onClose: () => void;
}) {
  const qc = useQueryClient();

  const { data: projectRoles, isLoading: rolesLoading } = useQuery({
    queryKey: ['user-project-roles', member.userId],
    queryFn: () => ProjectMembersApi.userProjectRoles(member.userId),
  });

  const { data: allProjects, isLoading: projectsLoading } = useQuery({
    queryKey: ['projects'],
    queryFn: () => ProjectsApi.list({ pageSize: 200 }),
    enabled: isAdmin,
  });

  const setProjectRole = useMutation({
    mutationFn: ({
      projectId,
      role,
    }: {
      projectId: string;
      role: ProjectMemberRole;
      projectName: string;
    }) => ProjectMembersApi.setRole(projectId, member.userId, role),
    onSuccess: (_data, vars) => {
      toast.success(
        `نقش «${member.displayName}» در پروژه «${vars.projectName}» به «${PROJECT_ROLE_LABEL[vars.role]}» تغییر کرد.`,
      );
      qc.invalidateQueries({ queryKey: ['user-project-roles', member.userId] });
      qc.invalidateQueries({ queryKey: ['project-members'] });
    },
    onError: (err) => toast.error(extractError(err, 'خطا در تعیین نقش پروژه')),
  });

  const resetProjectRole = useMutation({
    mutationFn: ({ projectId }: { projectId: string; projectName: string }) =>
      ProjectMembersApi.reset(projectId, member.userId),
    onSuccess: (_data, vars) => {
      const inherited = ROLE_META[member.role]?.label ?? member.role;
      toast.success(
        `نقش اختصاصی «${member.displayName}» در پروژه «${vars.projectName}» برداشته شد؛ از این به بعد نقش فضای کاری («${inherited}») اعمال می‌شود.`,
      );
      qc.invalidateQueries({ queryKey: ['user-project-roles', member.userId] });
      qc.invalidateQueries({ queryKey: ['project-members'] });
    },
    onError: (err) => toast.error(extractError(err, 'خطا در بازنشانی نقش')),
  });

  return (
    <>
      {/* Header */}
      <div className="sticky top-0 z-10 bg-card border-b border-border px-4 py-3 flex items-center gap-3 shrink-0">
        <UserAvatar
          user={{ id: member.userId, displayName: member.displayName, email: member.email, avatarUrl: member.avatarUrl }}
          size="sm"
        />
        <div className="flex-1 min-w-0">
          <div className="font-semibold text-sm truncate">{member.displayName}</div>
          <div className="text-xs text-muted-foreground truncate" dir="ltr">{member.email}</div>
        </div>
        <Button variant="ghost" size="icon-sm" onClick={onClose}>
          <X className="size-4" />
        </Button>
      </div>

      {/* Content */}
      <div className="flex-1 overflow-y-auto p-4 space-y-4">
        {/* Workspace role summary */}
        <div className="flex items-center gap-2 text-sm">
          <span className="text-muted-foreground">نقش فضای کاری:</span>
          <RoleBadge role={member.role} />
        </div>
        <p className="text-xs text-muted-foreground leading-relaxed">
          نقش فضا برای همهٔ پروژه‌هاست مگر این‌که برای یک پروژه نقش اختصاصی بگذارید.
          «محروم» یعنی این کاربر آن پروژه را اصلاً نمی‌بیند.
        </p>

        <div>
          <h3 className="text-xs font-semibold text-muted-foreground mb-3 uppercase tracking-wider flex items-center gap-1.5">
            <FolderKanban className="size-3" /> نقش‌های پروژه‌ای
          </h3>

          {rolesLoading || (isAdmin && projectsLoading) ? (
            <div className="space-y-2">
              {Array.from({ length: 3 }).map((_, i) => <Skeleton key={i} className="h-10" />)}
            </div>
          ) : (
            <div className="space-y-2">
              {/* Per-project roles: every live project, inherited or override */}
              {isAdmin && (
                <div className="space-y-2">
                  {(allProjects ?? [])
                    .filter((p) => !p.isArchived)
                    .map((p) => {
                      const override = (projectRoles ?? []).find((r) => r.projectId === p.id);
                      const inheritedLabel = ROLE_META[member.role]?.label ?? member.role;
                      const isProjectOwnerRow = override?.role === 'Owner';
                      const busy =
                        (setProjectRole.isPending && setProjectRole.variables?.projectId === p.id) ||
                        (resetProjectRole.isPending && resetProjectRole.variables?.projectId === p.id);

                      return (
                        <div
                          key={p.id}
                          className="flex items-center gap-3 rounded-lg border border-border px-3 py-2"
                        >
                          <div className="size-7 rounded-md bg-primary/10 text-primary text-[10px] font-mono flex items-center justify-center shrink-0">
                            {p.code.slice(0, 3)}
                          </div>
                          <div className="flex-1 min-w-0">
                            <div className="text-sm font-medium truncate">{p.name}</div>
                            <div className="text-[10px] text-muted-foreground font-mono">{p.code}</div>
                          </div>
                          {isProjectOwnerRow ? (
                            <Badge variant="success" className="shrink-0 gap-1">
                              <Crown className="size-3" />
                              مالک پروژه
                            </Badge>
                          ) : (
                            <Select
                              value={override?.role ?? ''}
                              disabled={busy}
                              className="h-8 text-xs w-44"
                              title="نقش این کاربر فقط در همین پروژه"
                              onChange={(e) => {
                                const next = e.target.value as ProjectMemberRole | '';
                                if (!next) {
                                  resetProjectRole.mutate({ projectId: p.id, projectName: p.name });
                                  return;
                                }
                                setProjectRole.mutate({ projectId: p.id, role: next, projectName: p.name });
                              }}
                            >
                              <option value="">
                                ارث‌بری از فضا ({inheritedLabel})
                              </option>
                              {PROJECT_ASSIGNABLE.map((r) => (
                                <option key={r} value={r}>
                                  {PROJECT_ROLE_LABEL[r]}
                                </option>
                              ))}
                            </Select>
                          )}
                        </div>
                      );
                    })}
                  {(allProjects ?? []).filter((p) => !p.isArchived).length === 0 && (
                    <p className="text-sm text-muted-foreground py-2">پروژهٔ فعالی برای تعیین نقش نیست.</p>
                  )}
                </div>
              )}
              {!isAdmin && (
                <div className="space-y-2">
                  {(projectRoles ?? []).length === 0 ? (
                    <p className="text-sm text-muted-foreground py-2">
                      نقش اختصاصی پروژه تنظیم نشده؛ نقش فضای کاری («{ROLE_META[member.role]?.label}») اعمال می‌شود.
                    </p>
                  ) : (
                    (projectRoles ?? []).map((pr) => (
                      <div
                        key={pr.projectId}
                        className="flex items-center gap-3 rounded-lg border border-border px-3 py-2"
                      >
                        <div className="size-7 rounded-md bg-primary/10 text-primary text-[10px] font-mono flex items-center justify-center shrink-0">
                          {pr.projectCode.slice(0, 3)}
                        </div>
                        <div className="flex-1 min-w-0">
                          <div className="text-sm font-medium truncate">{pr.projectName}</div>
                          <div className="text-[10px] text-muted-foreground font-mono">{pr.projectCode}</div>
                        </div>
                        <Badge
                          variant={pr.role === 'Excluded' ? 'destructive' : 'info'}
                          className="text-xs shrink-0"
                        >
                          {PROJECT_ROLE_LABEL[pr.role]}
                        </Badge>
                      </div>
                    ))
                  )}
                </div>
              )}
            </div>
          )}
        </div>
      </div>
    </>
  );
}

// ─── Inline role selector ────────────────────────────────────────────────
function RoleSelect({
  value,
  isOwner,
  busy,
  onChange,
}: {
  value: WorkspaceRole;
  isOwner: boolean;
  busy: boolean;
  onChange: (role: WorkspaceRole) => void;
}) {
  const options: WorkspaceRole[] = isOwner ? ASSIGNABLE : ASSIGNABLE.filter((r) => r !== 'Admin');
  return (
    <Select
      value={value}
      disabled={busy}
      onChange={(e) => onChange(e.target.value as WorkspaceRole)}
      className="h-8 text-xs font-medium w-28"
    >
      {options.map((r) => (
        <option key={r} value={r}>
          {ROLE_META[r].label}
        </option>
      ))}
    </Select>
  );
}

// ─── Add Member Dialog ──────────────────────────────────────────────────
function AddMemberDialog({
  open,
  onClose,
  canAssignAdmin,
  onSuccess,
}: {
  open: boolean;
  onClose: () => void;
  canAssignAdmin: boolean;
  onSuccess: (email: string, tempPassword: string | null) => void;
}) {
  const [search, setSearch] = useState('');
  const [role, setRole] = useState<WorkspaceRole>('Tester');
  const querySearch = search.trim();

  const { data, isFetching } = useQuery({
    queryKey: ['member-candidates', querySearch],
    queryFn: () =>
      MembersApi.candidates({
        search: querySearch || undefined,
        page: 1,
        pageSize: 50,
      }),
    enabled: open,
  });

  const addExisting = useMutation({
    mutationFn: ({ userId, selectedRole }: { userId: string; selectedRole: WorkspaceRole }) =>
      MembersApi.addExisting({ userId, role: selectedRole }),
    onSuccess: (member, vars) => {
      toast.success(
        `«${member.displayName || member.email}» با نقش «${ROLE_META[vars.selectedRole].label}» به فضای کاری اضافه شد.`,
      );
      onSuccess(member.email, null);
    },
    onError: (err) => toast.error(extractError(err, 'خطا در افزودن عضو')),
  });

  function addCandidate(candidate: MemberCandidate) {
    if (candidate.isInWorkspace || addExisting.isPending) return;
    addExisting.mutate({ userId: candidate.userId, selectedRole: role });
  }

  if (!open) return null;

  const candidates = data?.items ?? [];

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm p-4"
      onClick={onClose}
    >
      <motion.div
        initial={{ opacity: 0, scale: 0.96 }}
        animate={{ opacity: 1, scale: 1 }}
        onClick={(e) => e.stopPropagation()}
        className="bg-card border border-border rounded-2xl shadow-soft-lg w-full max-w-2xl"
      >
        <>
          <div className="flex items-start justify-between gap-3 p-5 border-b border-border">
            <div className="flex items-start gap-3">
              <div className="size-10 rounded-xl bg-primary/10 text-primary flex items-center justify-center">
                <UserPlus className="size-5" />
              </div>
              <div>
                <h3 className="font-semibold text-base">افزودن عضو</h3>
                <p className="text-xs text-muted-foreground mt-1">
                  از بین کاربران فعال ثبت‌نام‌شده انتخاب کنید.
                </p>
              </div>
            </div>
            <Button type="button" variant="ghost" size="icon-sm" onClick={onClose} aria-label="بستن">
              <X className="size-4" />
            </Button>
          </div>

          <div className="p-5 space-y-4">
            <div className="grid gap-3 sm:grid-cols-[1fr_180px]">
              <div>
                <Label htmlFor="m-search">جست‌وجوی کاربر</Label>
                <div className="relative">
                  <Search className="absolute right-3 top-1/2 -translate-y-1/2 size-4 text-muted-foreground pointer-events-none" />
                  <Input
                    id="m-search"
                    value={search}
                    onChange={(e) => setSearch(e.target.value)}
                    placeholder="نام یا ایمیل"
                    className="pr-9"
                    autoFocus
                  />
                </div>
              </div>
              <div>
                <Label htmlFor="m-role">نقش</Label>
                <Select
                  id="m-role" value={role}
                  onChange={(e) => setRole(e.target.value as WorkspaceRole)}
                >
                  {(canAssignAdmin ? ASSIGNABLE : ASSIGNABLE.filter((r) => r !== 'Admin')).map((r) => (
                    <option key={r} value={r}>{ROLE_META[r].label}</option>
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
                <EmptyState
                  icon={<Search className="size-7" />}
                  title="کاربری یافت نشد"
                  description="کاربر فعال ثبت‌نام‌شده‌ای با این جست‌وجو پیدا نشد."
                />
              ) : (
                <div className="divide-y divide-border">
                  {candidates.map((candidate) => {
                    const addingThis = addExisting.isPending && addExisting.variables?.userId === candidate.userId;
                    const candidateRole = candidate.workspaceRole as WorkspaceRole | null | undefined;
                    return (
                      <div
                        key={candidate.userId}
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
                        {candidate.isInWorkspace && (
                          <Badge variant="muted" className="shrink-0">
                            {candidateRole ? ROLE_META[candidateRole]?.label ?? candidateRole : 'عضو فعلی'}
                          </Badge>
                        )}
                        <Button
                          type="button"
                          size="sm"
                          disabled={candidate.isInWorkspace || addExisting.isPending}
                          loading={addingThis}
                          onClick={() => addCandidate(candidate)}
                        >
                          افزودن
                        </Button>
                      </div>
                    );
                  })}
                </div>
              )}
            </div>
          </div>
        </>
      </motion.div>
    </div>
  );
}

// ─── Temporary Password Reveal ──────────────────────────────────────────
function TempPasswordDialog({
  info,
  onClose,
}: {
  info: { email: string; password: string } | null;
  onClose: () => void;
}) {
  const [copied, setCopied] = useState(false);
  if (!info) return null;

  async function copy() {
    await navigator.clipboard.writeText(info!.password);
    setCopied(true);
    setTimeout(() => setCopied(false), 1500);
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm p-4">
      <motion.div
        initial={{ opacity: 0, scale: 0.96 }}
        animate={{ opacity: 1, scale: 1 }}
        className="bg-card border border-border rounded-2xl shadow-soft-lg w-full max-w-md"
      >
        <div className="p-5 border-b border-border flex items-start gap-3">
          <div className="size-10 rounded-xl bg-success/10 text-success flex items-center justify-center">
            <Check className="size-5" />
          </div>
          <div className="flex-1">
            <h3 className="font-semibold">کاربر با موفقیت ساخته شد</h3>
            <p className="text-xs text-muted-foreground mt-1">
              این گذرواژه فقط یک‌بار قابل مشاهده است. آن را به صاحب حساب تحویل دهید.
            </p>
          </div>
        </div>
        <div className="p-5 space-y-3">
          <div className="text-xs text-muted-foreground flex items-center gap-1">
            <Mail className="size-3" />
            <span dir="ltr">{info.email}</span>
          </div>
          <div className="flex items-stretch gap-2">
            <code className="flex-1 px-4 py-3 bg-muted rounded-lg font-mono text-sm tracking-wider select-all" dir="ltr">
              {info.password}
            </code>
            <Button type="button" variant="outline" onClick={copy} className="shrink-0">
              {copied ? <Check className="size-4 text-success" /> : <Copy className="size-4" />}
              {copied ? 'کپی شد' : 'کپی'}
            </Button>
          </div>
          <p className="text-xs text-muted-foreground flex items-center gap-1">
            <Hash className="size-3" />
            کاربر هنگام اولین ورود مجبور به تغییر گذرواژه خواهد شد.
          </p>
        </div>
        <div className="flex items-center justify-end px-5 py-4 border-t border-border bg-muted/30 rounded-b-2xl">
          <Button onClick={onClose}>متوجه شدم</Button>
        </div>
      </motion.div>
    </div>
  );
}
