import { create } from 'zustand';
import { persist } from 'zustand/middleware';

export interface AuthUser {
  id: string;
  email: string;
  displayName: string | null;
  avatarUrl?: string | null;
  mustChangePassword?: boolean;
  workspaceId: string | null;
  workspaceName?: string | null;
  workspaceAccessScope?: 'Workspace' | 'ProjectScoped';
  workspaces?: Array<{ id: string; name: string; role?: string; accessScope?: 'Workspace' | 'ProjectScoped' }>;
}

interface AuthState {
  accessToken: string | null;
  refreshToken: string | null;
  user: AuthUser | null;
  setSession: (p: { accessToken: string; refreshToken: string; user: AuthUser }) => void;
  setUser: (u: AuthUser | null) => void;
  setWorkspace: (workspaceId: string, workspaceName?: string) => void;
  logout: () => void;
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      accessToken: null,
      refreshToken: null,
      user: null,
      setSession: ({ accessToken, refreshToken, user }) =>
        set({ accessToken, refreshToken, user }),
      setUser: (user) => set({ user }),
      setWorkspace: (workspaceId, workspaceName) =>
        set((s) => ({
          user: s.user ? { ...s.user, workspaceId, workspaceName: workspaceName ?? s.user.workspaceName } : s.user,
        })),
      logout: () => set({ accessToken: null, refreshToken: null, user: null }),
    }),
    {
      name: 'azma.auth',
      version: 2,
      // v1 used `fullName`; v2 uses `displayName`. Migrate so existing sessions don't render “؟”.
      migrate: (persistedState: unknown, _version) => {
        const s = (persistedState ?? {}) as { user?: Record<string, unknown> | null };
        if (s.user && typeof s.user === 'object') {
          const u = s.user as Record<string, unknown>;
          if (u.displayName == null && typeof u.fullName === 'string') {
            u.displayName = u.fullName;
          }
          delete (u as { fullName?: unknown }).fullName;
        }
        return s as AuthState;
      },
    },
  ),
);
