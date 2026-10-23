import { useMemo } from 'react';
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar';
import { cn } from '@/lib/utils';

export interface UserAvatarUser {
  id: string;
  displayName?: string | null;
  email?: string | null;
  avatarUrl?: string | null;
}

export interface UserAvatarProps {
  user: UserAvatarUser;
  size?: 'xs' | 'sm' | 'md' | 'lg' | 'xl';
  className?: string;
  status?: 'online' | 'offline' | 'idle' | null;
}

const SIZE_MAP = {
  xs: 'sm',
  sm: 'sm',
  md: 'default',
  lg: 'lg',
  xl: 'lg',
} as const;

function hashStr(input: string): number {
  let h = 0;
  for (let i = 0; i < input.length; i++) h = (h * 31 + input.charCodeAt(i)) | 0;
  return Math.abs(h);
}

export function userColor(id: string): { bg: string; fg: string } {
  const hue = hashStr(id) % 360;
  return { bg: `hsl(${hue} 70% 45%)`, fg: 'white' };
}

function initialsFor(user: UserAvatarUser): string {
  const source = (user.displayName || user.email || '').trim();
  if (!source) return '؟';
  const parts = source.split(/\s+/).filter(Boolean);
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
}

export function UserAvatar({ user, size = 'md', className, status }: UserAvatarProps) {
  const initials = useMemo(() => initialsFor(user), [user.displayName, user.email]);
  const color = useMemo(() => userColor(user.id), [user.id]);

  return (
    <span className={cn('relative inline-flex shrink-0', className)}>
      <Avatar
        size={SIZE_MAP[size]}
        className={cn(size === 'xs' && 'size-6', size === 'xl' && 'size-20')}
        title={user.displayName || user.email || ''}
      >
        {user.avatarUrl ? (
          <AvatarImage src={user.avatarUrl} alt={user.displayName || user.email || ''} />
        ) : null}
        <AvatarFallback style={{ backgroundColor: color.bg, color: color.fg }}>{initials}</AvatarFallback>
      </Avatar>
      {status ? (
        <span
          className={cn(
            'absolute end-0 bottom-0 block rounded-full ring-2 ring-background',
            status === 'online' && 'bg-success',
            status === 'offline' && 'bg-muted-foreground',
            status === 'idle' && 'bg-warning',
            size === 'xs' ? 'size-1.5' : size === 'sm' ? 'size-2' : 'size-2.5',
          )}
        />
      ) : null}
    </span>
  );
}

export default UserAvatar;
