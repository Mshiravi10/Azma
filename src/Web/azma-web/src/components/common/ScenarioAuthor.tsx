import { useQuery } from '@tanstack/react-query';
import { MembersApi } from '@/lib/queries/endpoints';
import { UserAvatar } from '@/components/common/UserAvatar';

export function ScenarioAuthor({ userId }: { userId?: string | null }) {
  const { data: members } = useQuery({
    queryKey: ['members'],
    queryFn: () => MembersApi.list(),
    staleTime: 60_000,
  });

  if (!userId) return <span className="text-xs text-muted-foreground">—</span>;

  const member = members?.find((m) => m.userId === userId);
  const name = member?.displayName || member?.email;

  return (
    <span className="inline-flex items-center gap-1.5 min-w-0 max-w-[9.5rem]" title={name ?? undefined}>
      <UserAvatar
        size="xs"
        user={{
          id: userId,
          displayName: member?.displayName,
          email: member?.email,
          avatarUrl: member?.avatarUrl,
        }}
      />
      <span className="truncate text-xs">{name ?? '—'}</span>
    </span>
  );
}
