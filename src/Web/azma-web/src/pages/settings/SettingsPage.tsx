import { Routes, Route, Navigate, useLocation, useNavigate } from 'react-router-dom';
import { lazy, Suspense } from 'react';
import { Bot, User, Users, KeyRound, ListTree } from 'lucide-react';
import { PageHeader } from '@/components/common/PageHeader';
import { PageSkeleton } from '@/components/common/Skeletons';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';

const ProfilePage = lazy(() => import('./ProfilePage'));
const MembersPage = lazy(() => import('./MembersPage'));
const ChangePasswordPage = lazy(() => import('./ChangePasswordPage'));
const McpSettingsPage = lazy(() => import('./McpSettingsPage'));

const TABS = [
  { to: '/settings/profile', value: 'profile', label: 'پروفایل', icon: User },
  { to: '/settings/members', value: 'members', label: 'اعضا', icon: Users },
  { to: '/settings/password', value: 'password', label: 'گذرواژه', icon: KeyRound },
  { to: '/settings/mcp', value: 'mcp', label: 'MCP', icon: Bot },
  { to: '/settings/custom-fields', value: 'fields', label: 'فیلدهای سفارشی', icon: ListTree },
];

export default function SettingsPage() {
  const location = useLocation();
  const navigate = useNavigate();
  const current = TABS.find((tab) => location.pathname.startsWith(tab.to))?.value ?? 'profile';

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="تنظیمات" description="مدیریت حساب، اعضا و فضای کاری شما." />

      <Tabs
        value={current}
        onValueChange={(value) => {
          const tab = TABS.find((item) => item.value === value);
          if (tab) navigate(tab.to);
        }}
      >
        <TabsList variant="line">
          {TABS.map((tab) => {
            const Icon = tab.icon;
            return (
              <TabsTrigger key={tab.to} value={tab.value}>
                <Icon />
                {tab.label}
              </TabsTrigger>
            );
          })}
        </TabsList>
      </Tabs>

      <Suspense fallback={<PageSkeleton />}>
        <Routes>
          <Route index element={<Navigate to="/settings/profile" replace />} />
          <Route path="profile" element={<ProfilePage />} />
          <Route path="members" element={<MembersPage />} />
          <Route path="password" element={<ChangePasswordPage />} />
          <Route path="mcp" element={<McpSettingsPage />} />
          <Route path="*" element={<Navigate to="/settings/profile" replace />} />
        </Routes>
      </Suspense>
    </div>
  );
}
