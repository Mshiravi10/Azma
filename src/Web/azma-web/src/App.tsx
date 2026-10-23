import { Routes, Route, Navigate } from 'react-router-dom';
import { lazy, Suspense, useEffect } from 'react';
import { useAuthStore } from '@/lib/auth/store';
import { AuthApi } from '@/lib/queries/endpoints';
import AppLayout from '@/components/layout/AppLayout';
import { PageSkeleton } from '@/components/common/Skeletons';

const LoginPage = lazy(() => import('@/pages/auth/LoginPage'));
const RegisterPage = lazy(() => import('@/pages/auth/RegisterPage'));
const DashboardPage = lazy(() => import('@/pages/dashboard/DashboardPage'));
const ProjectsPage = lazy(() => import('@/pages/projects/ProjectsPage'));
const ProjectDetailPage = lazy(() => import('@/pages/projects/ProjectDetailPage'));
const TestCasesPage = lazy(() => import('@/pages/testcases/TestCasesPage'));
const TestRepositoryPage = lazy(() => import('@/pages/repository/TestRepositoryPage'));
const TestCaseDetailPage = lazy(() => import('@/pages/testcases/TestCaseDetailPage'));
const TestCaseEditorPage = lazy(() => import('@/pages/testcases/TestCaseEditorPage.tsx'));
const RunsPage = lazy(() => import('@/pages/runs/RunsPage'));
const RunSchedulesPage = lazy(() => import('@/pages/runs/RunSchedulesPage'));
const RunDetailPage = lazy(() => import('@/pages/runs/RunDetailPage.tsx'));
const RunExecutePage = lazy(() => import('@/pages/runs/RunExecutePage'));
const ReportsPage = lazy(() => import('@/pages/reports/ReportsPage'));
const DocumentationPage = lazy(() => import('@/pages/documentation/DocumentationPage.tsx'));
const SettingsPage = lazy(() => import('@/pages/settings/SettingsPage'));
const CustomFieldsPage = lazy(() => import('@/pages/settings/CustomFieldsPage'));
const ChangePasswordPage = lazy(() => import('@/pages/settings/ChangePasswordPage'));
const NotFoundPage = lazy(() => import('@/pages/NotFoundPage'));

function ProtectedRoutes() {
  const token = useAuthStore((s) => s.accessToken);
  const user = useAuthStore((s) => s.user);
  const setUser = useAuthStore((s) => s.setUser);

  useEffect(() => {
    if (!token) return;
    if (user?.displayName && user?.email && user?.workspaceName) return;
    AuthApi.me()
      .then((me) =>
        setUser({
          id: me.user.id,
          email: me.user.email,
          displayName: me.user.displayName,
          avatarUrl: me.user.avatarUrl ?? null,
          mustChangePassword: me.user.mustChangePassword ?? false,
          workspaceId: me.current?.id ?? null,
          workspaceName: me.current?.name ?? null,
          workspaceAccessScope: me.current?.accessScope ?? 'Workspace',
          workspaces: me.workspaces,
        }),
      )
      .catch(() => {});
  }, [token, user?.displayName, user?.email, user?.workspaceName, setUser]);

  if (!token) return <Navigate to="/login" replace />;

  if (user?.mustChangePassword && !location.pathname.startsWith('/onboarding')) {
    return <Navigate to="/onboarding/change-password" replace />;
  }

  return (
    <Routes>
      {/* Immersive execute page — no sidebar/topbar, needs its own Route for useParams */}
      <Route
        path="runs/:id/execute"
        element={
          <Suspense fallback={<PageSkeleton />}>
            <RunExecutePage />
          </Suspense>
        }
      />
      <Route
        path="*"
        element={
          <AppLayout>
            <Suspense fallback={<PageSkeleton />}>
              <Routes>
                <Route index element={<Navigate to="/dashboard" replace />} />
                <Route path="dashboard" element={<DashboardPage />} />
                <Route path="projects" element={<ProjectsPage />} />
                <Route path="projects/:projectId" element={<ProjectDetailPage />} />
                <Route path="testcases" element={<TestRepositoryPage />} />
                <Route path="repository" element={<TestRepositoryPage />} />
                <Route path="repository/:projectId" element={<TestRepositoryPage />} />
                <Route path="testcases/all" element={<TestCasesPage />} />
                <Route path="testcases/new" element={<TestCaseEditorPage />} />
                <Route path="testcases/:id" element={<TestCaseDetailPage />} />
                <Route path="testcases/:id/edit" element={<TestCaseEditorPage />} />
                <Route path="runs" element={<RunsPage />} />
                <Route path="run-schedules" element={<RunSchedulesPage />} />
                <Route path="runs/:id" element={<RunDetailPage />} />
                <Route path="reports" element={<ReportsPage />} />
                <Route path="documentation" element={<DocumentationPage />} />
                <Route path="settings/custom-fields" element={<CustomFieldsPage />} />
                <Route path="settings/custom-fields/:projectId" element={<CustomFieldsPage />} />
                <Route path="settings/*" element={<SettingsPage />} />
                <Route path="*" element={<NotFoundPage />} />
              </Routes>
            </Suspense>
          </AppLayout>
        }
      />
    </Routes>
  );
}

function ForcedOnboarding() {
  const token = useAuthStore((s) => s.accessToken);
  const user = useAuthStore((s) => s.user);
  if (!token) return <Navigate to="/login" replace />;
  if (!user?.mustChangePassword) return <Navigate to="/dashboard" replace />;
  return (
    <Suspense fallback={<PageSkeleton />}>
      <ChangePasswordPage forced />
    </Suspense>
  );
}

export default function App() {
  return (
    <Suspense fallback={<PageSkeleton />}>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/register" element={<RegisterPage />} />
        <Route path="/onboarding/change-password" element={<ForcedOnboarding />} />
        <Route path="/*" element={<ProtectedRoutes />} />
      </Routes>
    </Suspense>
  );
}
