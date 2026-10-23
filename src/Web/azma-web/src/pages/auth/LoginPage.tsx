import { useState, type FormEvent } from 'react';
import { useNavigate, Link, Navigate } from 'react-router-dom';
import { toast } from 'sonner';
import { Sparkles, ArrowLeft, ShieldCheck, Zap, Users, Building2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Field, FieldGroup, FieldLabel } from '@/components/ui/field';
import { useAuthStore } from '@/lib/auth/store';
import { AuthApi } from '@/lib/queries/endpoints';
import { extractError } from '@/lib/api';
import type { LoginResponse, MeResponse } from '@/lib/types';
import { Spinner } from '@/components/ui/spinner';
import { Badge } from '@/components/ui/badge';

type Step = 'credentials' | 'workspace';

export default function LoginPage() {
  const navigate = useNavigate();
  const setSession = useAuthStore((s) => s.setSession);
  const accessToken = useAuthStore((s) => s.accessToken);
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('Test12345!');
  const [submitting, setSubmitting] = useState(false);

  const [step, setStep] = useState<Step>('credentials');
  const [loginResult, setLoginResult] = useState<LoginResponse | null>(null);
  const [meResult, setMeResult] = useState<MeResponse | null>(null);
  const [switchingWs, setSwitchingWs] = useState<string | null>(null);

  if (accessToken && step === 'credentials' && !submitting) return <Navigate to="/dashboard" replace />;

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setSubmitting(true);
    try {
      const r = await AuthApi.login(email, password);
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
        },
      });

      setLoginResult(r);

      let me: MeResponse | null = null;
      try {
        me = await AuthApi.me();
        setMeResult(me);
        useAuthStore.getState().setUser({
          id: me.user.id,
          email: me.user.email,
          displayName: me.user.displayName,
          avatarUrl: me.user.avatarUrl ?? null,
          mustChangePassword: me.user.mustChangePassword ?? false,
          workspaceId: me.current?.id ?? null,
          workspaceName: me.current?.name ?? null,
          workspaceAccessScope: me.current?.accessScope ?? 'Workspace',
          workspaces: me.workspaces,
        });
      } catch {
        /* ignore */
      }

      if (me && me.workspaces.length > 1) {
        setStep('workspace');
      } else {
        toast.success('خوش آمدید!');
        navigate('/dashboard', { replace: true });
      }
    } catch (err) {
      toast.error(extractError(err, 'ورود ناموفق بود'));
    } finally {
      setSubmitting(false);
    }
  }

  async function selectWorkspace(wsId: string) {
    const user = useAuthStore.getState().user;
    if (wsId === user?.workspaceId) {
      toast.success('خوش آمدید!');
      navigate('/dashboard', { replace: true });
      return;
    }
    setSwitchingWs(wsId);
    try {
      const r = await AuthApi.switchWorkspace(wsId);
      const ws = meResult?.workspaces ?? user?.workspaces ?? [];
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
          workspaces: ws,
        },
      });
      toast.success(`خوش آمدید! فضای کاری: ${r.workspace?.name}`);
      navigate('/dashboard', { replace: true });
    } catch (err) {
      toast.error(extractError(err, 'خطا در انتخاب فضای کاری'));
    } finally {
      setSwitchingWs(null);
    }
  }

  return (
    <div dir="rtl" className="flex min-h-svh">
      <div className="relative hidden flex-col justify-between overflow-hidden bg-sidebar p-12 text-sidebar-foreground lg:flex lg:w-1/2">
        <div className="absolute inset-0 opacity-40">
          <div className="absolute top-20 -end-20 size-96 rounded-full bg-sidebar-primary/30 blur-3xl" />
          <div className="absolute bottom-20 -start-20 size-96 rounded-full bg-info/20 blur-3xl" />
        </div>
        <div className="relative">
          <div className="flex items-center gap-3">
            <div className="flex size-12 items-center justify-center rounded-2xl bg-sidebar-primary text-sidebar-primary-foreground">
              <Sparkles />
            </div>
            <div>
              <div className="text-xl font-bold text-sidebar-accent-foreground">آزما</div>
              <div className="text-xs text-sidebar-foreground/70">پلتفرم مدیریت تست</div>
            </div>
          </div>
        </div>
        <div className="relative flex max-w-md flex-col gap-8">
          <h2 className="text-4xl leading-tight font-bold text-sidebar-accent-foreground">
            کیفیت محصول شما،
            <br />
            یک قدم جلوتر.
          </h2>
          <p className="leading-loose text-sidebar-foreground/80">
            آزما به تیم‌های توسعه و کیفیت کمک می‌کند تا سناریوهای آزمون را مدیریت، اجرا و گزارش‌گیری کنند —
            با سرعت، دقت و آرامش خاطر.
          </p>
          <div className="flex flex-col gap-3 pt-4">
            {[
              { icon: ShieldCheck, t: 'امنیت و چندمستأجری', d: 'هر فضای کاری ایزوله و ایمن' },
              { icon: Zap, t: 'گزارش‌های لحظه‌ای', d: 'دیدن نتایج اجرا تا چند ثانیه' },
              { icon: Users, t: 'همکاری تیمی', d: 'اعضا، نقش‌ها و فعالیت کاربران' },
            ].map((f) => (
              <div
                key={f.t}
                className="flex items-center gap-3 rounded-xl border border-sidebar-border bg-sidebar-accent/60 p-4"
              >
                <div className="flex size-10 shrink-0 items-center justify-center rounded-lg bg-sidebar-primary/20 text-sidebar-primary-foreground">
                  <f.icon />
                </div>
                <div>
                  <div className="text-sm font-medium text-sidebar-accent-foreground">{f.t}</div>
                  <div className="text-xs text-sidebar-foreground/70">{f.d}</div>
                </div>
              </div>
            ))}
          </div>
        </div>
        <div className="relative text-xs text-sidebar-foreground/50">© ۱۴۰۵ آزما — تمامی حقوق محفوظ است</div>
      </div>

      <div className="flex flex-1 items-center justify-center bg-background p-6 sm:p-12">
        {step === 'credentials' ? (
          <div className="flex w-full max-w-md flex-col gap-6">
            <div>
              <h1 className="text-3xl font-bold tracking-tight">ورود به حساب</h1>
              <p className="mt-2 text-muted-foreground">
                با حساب کاربری خود وارد شوید تا مدیریت تست‌ها را آغاز کنید.
              </p>
            </div>

            <form onSubmit={onSubmit}>
              <FieldGroup>
                <Field>
                  <FieldLabel htmlFor="email">ایمیل</FieldLabel>
                  <Input
                    id="email"
                    type="email"
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    placeholder="example@domain.com"
                    autoComplete="email"
                    required
                    dir="ltr"
                    className="text-start"
                  />
                </Field>
                <Field>
                  <FieldLabel htmlFor="password">گذرواژه</FieldLabel>
                  <Input
                    id="password"
                    type="password"
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    placeholder="••••••••"
                    autoComplete="current-password"
                    required
                    dir="ltr"
                    className="text-start"
                  />
                </Field>
                <Button type="submit" className="w-full" loading={submitting} size="lg">
                  ورود
                  <ArrowLeft data-icon="inline-end" />
                </Button>
              </FieldGroup>
            </form>

            <div className="text-center text-sm text-muted-foreground">
              حساب کاربری ندارید؟{' '}
              <Link to="/register" className="font-medium text-primary hover:underline">
                ثبت‌نام کنید
              </Link>
            </div>
          </div>
        ) : (
          <div className="flex w-full max-w-md flex-col gap-6">
            <div>
              <h1 className="text-3xl font-bold tracking-tight">انتخاب فضای کاری</h1>
              <p className="mt-2 text-muted-foreground">
                شما عضو چند فضای کاری هستید. لطفاً فضای کاری مورد نظر را انتخاب کنید.
              </p>
            </div>

            <div className="flex flex-col gap-2">
              {(meResult?.workspaces ?? []).map((ws) => {
                const isCurrent = ws.id === loginResult?.workspace?.id;
                const isSwitching = switchingWs === ws.id;
                return (
                  <button
                    key={ws.id}
                    onClick={() => selectWorkspace(ws.id)}
                    disabled={!!switchingWs}
                    className="group flex w-full items-center gap-4 rounded-xl border p-4 text-start hover:bg-accent"
                  >
                    <div className="flex size-11 shrink-0 items-center justify-center rounded-xl bg-primary/10 text-primary">
                      <Building2 />
                    </div>
                    <div className="min-w-0 flex-1">
                      <div className="text-sm font-semibold">{ws.name}</div>
                      {ws.role ? (
                        <div className="mt-0.5 text-xs text-muted-foreground">
                          نقش:{' '}
                          {ws.role === 'Owner'
                            ? 'مالک'
                            : ws.role === 'Admin'
                              ? 'مدیر'
                              : ws.role === 'Tester'
                                ? 'تستر'
                                : 'بیننده'}
                        </div>
                      ) : null}
                    </div>
                    {isCurrent ? <Badge variant="muted">پیش‌فرض</Badge> : null}
                    {isSwitching ? <Spinner /> : <ArrowLeft className="text-muted-foreground group-hover:text-primary" />}
                  </button>
                );
              })}
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
