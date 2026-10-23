import { useState, type FormEvent } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { toast } from 'sonner';
import { Sparkles } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Field, FieldGroup, FieldLabel } from '@/components/ui/field';
import { AuthApi } from '@/lib/queries/endpoints';
import { useAuthStore } from '@/lib/auth/store';
import { extractError } from '@/lib/api';

export default function RegisterPage() {
  const navigate = useNavigate();
  const setSession = useAuthStore((s) => s.setSession);
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [fullName, setFullName] = useState('');
  const [workspaceName, setWorkspaceName] = useState('');
  const [submitting, setSubmitting] = useState(false);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setSubmitting(true);
    try {
      const r = await AuthApi.register({ email, password, fullName, workspaceName });
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
          workspaceName: r.workspace?.name ?? workspaceName,
          workspaceAccessScope: r.workspace?.accessScope ?? 'Workspace',
        },
      });
      toast.success('حساب با موفقیت ساخته شد');
      navigate('/dashboard', { replace: true });
    } catch (err) {
      toast.error(extractError(err, 'ثبت‌نام ناموفق بود'));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div dir="rtl" className="flex min-h-svh items-center justify-center bg-background p-6">
      <Card className="w-full max-w-md">
        <CardHeader>
          <div className="flex items-center gap-3">
            <div className="flex size-11 items-center justify-center rounded-xl bg-primary text-primary-foreground">
              <Sparkles />
            </div>
            <div>
              <CardTitle>ثبت‌نام در آزما</CardTitle>
              <CardDescription>یک فضای کاری جدید بسازید</CardDescription>
            </div>
          </div>
        </CardHeader>
        <CardContent>
          <form onSubmit={onSubmit}>
            <FieldGroup>
              <Field>
                <FieldLabel htmlFor="fullName">نام و نام خانوادگی</FieldLabel>
                <Input id="fullName" value={fullName} onChange={(e) => setFullName(e.target.value)} required />
              </Field>
              <Field>
                <FieldLabel htmlFor="workspaceName">نام فضای کاری</FieldLabel>
                <Input
                  id="workspaceName"
                  value={workspaceName}
                  onChange={(e) => setWorkspaceName(e.target.value)}
                  placeholder="مثلاً تیم محصول"
                  required
                />
              </Field>
              <Field>
                <FieldLabel htmlFor="email">ایمیل</FieldLabel>
                <Input
                  id="email"
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  dir="ltr"
                  className="text-start"
                  required
                />
              </Field>
              <Field>
                <FieldLabel htmlFor="password">گذرواژه</FieldLabel>
                <Input
                  id="password"
                  type="password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  dir="ltr"
                  className="text-start"
                  required
                />
              </Field>
              <Button type="submit" loading={submitting} className="w-full" size="lg">
                ساخت حساب
              </Button>
            </FieldGroup>
          </form>
          <div className="mt-6 text-center text-sm text-muted-foreground">
            حساب دارید؟{' '}
            <Link to="/login" className="font-medium text-primary hover:underline">
              وارد شوید
            </Link>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
