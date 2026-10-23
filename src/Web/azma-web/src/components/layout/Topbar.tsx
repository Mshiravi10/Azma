import { useNavigate } from 'react-router-dom';
import { useAuthStore } from '@/lib/auth/store';
import { Button } from '@/components/ui/button';
import { LogOut, Sun, Moon, ChevronDown } from 'lucide-react';
import { UserAvatar } from '@/components/common/UserAvatar';
import { useTheme } from '@/components/theme-provider';
import { SidebarTrigger } from '@/components/ui/sidebar';
import { Separator } from '@/components/ui/separator';
import { GlobalSearch } from '@/components/layout/GlobalSearch';
import { NotificationsMenu } from '@/components/layout/NotificationsMenu';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';

export function Topbar() {
  const navigate = useNavigate();
  const user = useAuthStore((s) => s.user);
  const logout = useAuthStore((s) => s.logout);
  const { theme, toggle } = useTheme();

  const onLogout = () => {
    logout();
    navigate('/login', { replace: true });
  };

  return (
    <header className="flex h-14 shrink-0 items-center gap-2 border-b px-4">
      <SidebarTrigger />
      <Separator orientation="vertical" className="h-4" />

      <GlobalSearch />

      <div className="flex-1" />

      <div className="flex items-center gap-1">
        <Button variant="ghost" size="icon" onClick={toggle} title="تغییر تم">
          {theme === 'dark' ? <Sun /> : <Moon />}
        </Button>
        <NotificationsMenu />
        <Separator orientation="vertical" className="mx-1 h-4" />
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="ghost" className="gap-2 px-2">
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
              <ChevronDown />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="min-w-48">
            <DropdownMenuGroup>
              <DropdownMenuLabel>
                {user?.displayName || user?.email || 'کاربر'}
              </DropdownMenuLabel>
            </DropdownMenuGroup>
            <DropdownMenuSeparator />
            <DropdownMenuItem onClick={() => navigate('/settings/profile')}>پروفایل</DropdownMenuItem>
            <DropdownMenuItem onClick={() => navigate('/settings')}>تنظیمات</DropdownMenuItem>
            <DropdownMenuSeparator />
            <DropdownMenuItem variant="destructive" onClick={onLogout}>
              <LogOut />
              خروج
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
    </header>
  );
}
