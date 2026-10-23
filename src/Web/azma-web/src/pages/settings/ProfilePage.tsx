import { useRef, useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { useAuthStore } from '@/lib/auth/store';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { UserAvatar } from '@/components/common/UserAvatar';
import { UsersApi } from '@/lib/queries/endpoints';
import { extractError } from '@/lib/api';
import { toast } from 'sonner';
import { User, Building2, Upload, Trash2 } from 'lucide-react';

const MAX_BYTES = 5 * 1024 * 1024;
const ACCEPT = 'image/png,image/jpeg,image/webp,image/gif';

export default function ProfilePage() {
  const user = useAuthStore((s) => s.user);
  const setUser = useAuthStore((s) => s.setUser);
  const fileRef = useRef<HTMLInputElement | null>(null);
  const [busy, setBusy] = useState(false);

  function applyAvatar(avatarUrl: string | null) {
    if (!user) return;
    setUser({ ...user, avatarUrl });
  }

  async function onPick(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    e.target.value = '';
    if (!file) return;
    if (!ACCEPT.split(',').includes(file.type)) {
      toast.error('فرمت فایل پشتیبانی نمی‌شود');
      return;
    }
    if (file.size > MAX_BYTES) {
      toast.error('حجم فایل حداکثر ۵ مگابایت است');
      return;
    }
    setBusy(true);
    try {
      const updated = await UsersApi.uploadMyAvatar(file);
      applyAvatar(updated.avatarUrl);
      toast.success('تصویر پروفایل به‌روزرسانی شد');
    } catch (err) {
      toast.error(extractError(err, 'خطا در آپلود تصویر'));
    } finally {
      setBusy(false);
    }
  }

  async function onDelete() {
    if (!user?.avatarUrl) return;
    setBusy(true);
    try {
      await UsersApi.deleteMyAvatar();
      applyAvatar(null);
      toast.success('تصویر پروفایل حذف شد');
    } catch (err) {
      toast.error(extractError(err, 'خطا در حذف تصویر'));
    } finally {
      setBusy(false);
    }
  }

  const myWorkspace = user?.workspaces?.find((w) => w.id === user?.workspaceId);

  return (
    <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
      <Card className="lg:col-span-2">
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <User className="size-4 text-primary" /> حساب کاربری
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex items-center gap-4">
            {user && (
              <UserAvatar
                user={{
                  id: user.id,
                  displayName: user.displayName,
                  email: user.email,
                  avatarUrl: user.avatarUrl,
                }}
                size="xl"
              />
            )}
            <div className="flex-1 min-w-0">
              <div className="font-semibold text-lg truncate">{user?.displayName || '—'}</div>
              <div className="text-sm text-muted-foreground truncate" dir="ltr">{user?.email}</div>
              <div className="mt-3 flex items-center gap-2">
                <input
                  ref={fileRef}
                  type="file"
                  className="hidden"
                  accept={ACCEPT}
                  onChange={onPick}
                />
                <Button type="button" size="sm" variant="outline" disabled={busy}
                  onClick={() => fileRef.current?.click()}>
                  <Upload className="size-3.5 ml-1" />
                  آپلود تصویر
                </Button>
                {user?.avatarUrl && (
                  <Button type="button" size="sm" variant="ghost" disabled={busy}
                    onClick={onDelete} className="text-destructive hover:text-destructive">
                    <Trash2 className="size-3.5 ml-1" />
                    حذف
                  </Button>
                )}
              </div>
              <div className="mt-1 text-xs text-muted-foreground">PNG, JPEG, WebP, GIF — حداکثر ۵ مگابایت</div>
            </div>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Building2 className="size-4 text-info" /> فضای کاری
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-2">
          <div className="font-medium">{user?.workspaceName || 'پیش‌فرض'}</div>
          <div className="text-xs text-muted-foreground font-mono" dir="ltr">{user?.workspaceId}</div>
          <div className="flex items-center gap-2 mt-2">
            <Badge variant="success">فعال</Badge>
            {myWorkspace?.role && <Badge variant="info">{myWorkspace.role}</Badge>}
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
