import { NavLink, useLocation, Link, useNavigate } from 'react-router-dom';
import {
  LayoutDashboard,
  FolderKanban,
  FlaskConical,
  PlayCircle,
  CalendarClock,
  BarChart3,
  Settings,
  Sparkles,
  ListTree,
  Users,
  LogOut,
  Check,
  Building2,
  BookOpen,
  ChevronsUpDown,
} from 'lucide-react';
import { useAuthStore } from '@/lib/auth/store';
import { UserAvatar } from '@/components/common/UserAvatar';
import { AuthApi } from '@/lib/queries/endpoints';
import { toast } from 'sonner';
import { useEffect, useState } from 'react';
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarGroup,
  SidebarGroupContent,
  SidebarGroupLabel,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarRail,
} from '@/components/ui/sidebar';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Spinner } from '@/components/ui/spinner';

interface NavItem {
  to: string;
  label: string;
  icon: React.ComponentType<{ className?: string }>;
}

interface NavGroup {
  label?: string;
  items: NavItem[];
}

const NAV_GROUPS: NavGroup[] = [
  {
    items: [
      { to: '/dashboard', label: 'داشبورد', icon: LayoutDashboard },
      { to: '/projects', label: 'پروژه‌ها', icon: FolderKanban },
      { to: '/documentation', label: 'راهنمای آزما', icon: BookOpen },
    ],
  },
  {
    label: 'تست',
    items: [
      { to: '/testcases', label: 'سناریوها', icon: FlaskConical },
      { to: '/runs', label: 'اجراها', icon: PlayCircle },
      { to: '/run-schedules', label: 'زمان‌بندی', icon: CalendarClock },
      { to: '/reports', label: 'گزارش‌ها', icon: BarChart3 },
    ],
  },
  {
    label: 'فضای کاری',
    items: [
      { to: '/settings/members', label: 'اعضا', icon: Users },
      { to: '/settings/custom-fields', label: 'فیلدهای سفارشی', icon: ListTree },
      { to: '/settings', label: 'تنظیمات', icon: Settings },
    ],
  },
];

export function AppSidebar() {
  const location = useLocation();
  const navigate = useNavigate();
  const user = useAuthStore((s) => s.user);
  const logout = useAuthStore((s) => s.logout);
  const setSession = useAuthStore((s) => s.setSession);
  const [switchingWs, setSwitchingWs] = useState<string | null>(null);

  useEffect(() => {
    if (!user || user.workspaces) return;
    AuthApi.me()
      .then((me) => {
        useAuthStore.getState().setUser({
          ...user,
          workspaceId: me.current?.id ?? user.workspaceId,
          workspaceName: me.current?.name ?? user.workspaceName,
          workspaceAccessScope: me.current?.accessScope ?? user.workspaceAccessScope ?? 'Workspace',
          workspaces: me.workspaces,
        });
      })
      .catch(() => {});
  }, [user]);

  const workspaces =
    user?.workspaces ??
    (user?.workspaceId && user?.workspaceName
      ? [{ id: user.workspaceId, name: user.workspaceName }]
      : []);

  async function switchWorkspace(wsId: string) {
    if (wsId === user?.workspaceId || switchingWs) return;
    setSwitchingWs(wsId);
    try {
      const r = await AuthApi.switchWorkspace(wsId);
      setSession({
        accessToken: r.accessToken,
        refreshToken: r.refreshToken,
        user: {
          id: r.user.id,
          email: r.user.email,
          displayName: r.user.displayName,
          avatarUrl: r.user.avatarUrl ?? null,
          mustChangePassword: r.user.mustChangePassword ?? false,
          workspaceId: r.workspace?.id ?? null,
          workspaceName: r.workspace?.name ?? null,
          workspaceAccessScope: r.workspace?.accessScope ?? 'Workspace',
          workspaces: user?.workspaces,
        },
      });
      toast.success(`فضای کاری تغییر کرد: ${r.workspace?.name}`);
      navigate('/dashboard', { replace: true });
      window.location.reload();
    } catch {
      toast.error('خطا در تغییر فضای کاری');
    } finally {
      setSwitchingWs(null);
    }
  }

  function handleLogout() {
    logout();
    navigate('/login', { replace: true });
  }

  return (
    <Sidebar collapsible="icon" side="right" dir="rtl">
      <SidebarHeader>
        <SidebarMenu>
          <SidebarMenuItem>
            <SidebarMenuButton size="lg" asChild tooltip="آزما">
              <Link to="/dashboard">
                <span className="flex size-8 items-center justify-center rounded-lg bg-sidebar-primary text-sidebar-primary-foreground">
                  <Sparkles />
                </span>
                <span className="grid flex-1 text-start text-sm leading-tight">
                  <span className="truncate font-semibold">آزما</span>
                  <span className="truncate text-xs text-sidebar-foreground/70">مدیریت تست</span>
                </span>
              </Link>
            </SidebarMenuButton>
          </SidebarMenuItem>
        </SidebarMenu>
      </SidebarHeader>

      <SidebarContent>
        {NAV_GROUPS.map((group, gi) => (
          <SidebarGroup key={gi}>
            {group.label ? <SidebarGroupLabel>{group.label}</SidebarGroupLabel> : null}
            <SidebarGroupContent>
              <SidebarMenu>
                {group.items.map((item) => {
                  const active =
                    location.pathname === item.to || location.pathname.startsWith(`${item.to}/`);
                  const Icon = item.icon;
                  return (
                    <SidebarMenuItem key={item.to}>
                      <SidebarMenuButton asChild isActive={active} tooltip={item.label}>
                        <NavLink to={item.to}>
                          <Icon />
                          <span>{item.label}</span>
                        </NavLink>
                      </SidebarMenuButton>
                    </SidebarMenuItem>
                  );
                })}
              </SidebarMenu>
            </SidebarGroupContent>
          </SidebarGroup>
        ))}
      </SidebarContent>

      <SidebarFooter>
        <SidebarMenu>
          <SidebarMenuItem>
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <SidebarMenuButton size="lg" tooltip={user?.displayName || user?.email || 'حساب'}>
                  {user ? (
                    <UserAvatar
                      size="sm"
                      user={{
                        id: user.id,
                        displayName: user.displayName,
                        email: user.email,
                        avatarUrl: user.avatarUrl,
                      }}
                    />
                  ) : null}
                  <span className="grid flex-1 text-start text-sm leading-tight">
                    <span className="truncate font-medium">{user?.displayName || user?.email}</span>
                    <span className="truncate text-xs" dir="ltr">
                      {user?.workspaceName || 'فضای کاری'}
                    </span>
                  </span>
                  <ChevronsUpDown className="ms-auto" />
                </SidebarMenuButton>
              </DropdownMenuTrigger>
              <DropdownMenuContent side="top" align="start" className="min-w-56">
                {workspaces.length > 0 ? (
                  <DropdownMenuGroup>
                    <DropdownMenuLabel className="flex items-center gap-1">
                      <Building2 />
                      فضاهای کاری
                    </DropdownMenuLabel>
                    {workspaces.map((ws) => {
                      const isCurrent = ws.id === user?.workspaceId;
                      return (
                        <DropdownMenuItem
                          key={ws.id}
                          disabled={isCurrent || !!switchingWs}
                          onClick={() => switchWorkspace(ws.id)}
                        >
                          <span className="flex-1 truncate">{ws.name}</span>
                          {isCurrent ? <Check /> : null}
                          {switchingWs === ws.id ? <Spinner /> : null}
                        </DropdownMenuItem>
                      );
                    })}
                  </DropdownMenuGroup>
                ) : null}
                <DropdownMenuSeparator />
                <DropdownMenuItem variant="destructive" onClick={handleLogout}>
                  <LogOut />
                  خروج از حساب
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          </SidebarMenuItem>
        </SidebarMenu>
      </SidebarFooter>
      <SidebarRail />
    </Sidebar>
  );
}

export { AppSidebar as Sidebar };
