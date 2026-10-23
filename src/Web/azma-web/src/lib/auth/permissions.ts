import type { AuthUser } from './store';

export function currentWorkspaceRole(user: AuthUser | null | undefined): string | undefined {
  if (!user?.workspaceId) return undefined;
  return user.workspaces?.find((w) => w.id === user.workspaceId)?.role;
}

export function isWorkspaceAdmin(user: AuthUser | null | undefined): boolean {
  const role = currentWorkspaceRole(user);
  return role === 'Owner' || role === 'Admin';
}
