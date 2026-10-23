import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import { toast } from 'sonner';
import { KeyRound, Eye, EyeOff, ShieldCheck, AlertCircle } from 'lucide-react';

import { PageHeader } from '@/components/common/PageHeader';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { AuthApi } from '@/lib/queries/endpoints';
import { useAuthStore } from '@/lib/auth/store';
import { extractError } from '@/lib/api';

export interface ChangePasswordPageProps {
  /** When true, hides cancel actions and renders a simpler centered layout. */
  forced?: boolean;
}

export default function ChangePasswordPage({ forced }: ChangePasswordPageProps) {
  const nav = useNavigate();
  const user = useAuthStore((s) => s.user);
  const setUser = useAuthStore((s) => s.setUser);

  const [current, setCurrent] = useState('');
  const [next, setNext] = useState('');
  const [confirm, setConfirm] = useState('');
  const [showCurrent, setShowCurrent] = useState(false);
  const [showNext, setShowNext] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const strong =
    next.length >= 8 &&
    /[A-Za-z]/.test(next) &&
    /[0-9]/.test(next);

  const matches = next.length > 0 && next === confirm;

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    if (!strong) {
      setError('گذرواژه باید حداقل ۸ کاراکتر و شامل حرف و عدد باشد.');
      return;
    }
    if (!matches) {
      setError('تکرار گذرواژه با گذرواژه جدید مطابقت ندارد.');
      return;
    }
    setBusy(true);
    try {
      await AuthApi.changePassword({ currentPassword: current, newPassword: next });
      if (user) setUser({ ...user, mustChangePassword: false });
      toast.success('گذرواژه با موفقیت تغییر کرد');
      nav(forced ? '/dashboard' : '/settings', { replace: true });
    } catch (err) {
      setError(extractError(err, 'خطا در تغییر گذرواژه'));
    } finally {
      setBusy(false);
    }
  }

  const form = (
    <form onSubmit={submit} className="space-y-4">
      {forced && (
        <div className="rounded-lg border border-warning/30 bg-warning/10 text-warning p-3 text-sm flex items-start gap-2">
          <AlertCircle className="size-4 mt-0.5 shrink-0" />
          <div>
            <div className="font-medium">تغییر گذرواژه الزامی است</div>
            <div className="text-xs mt-0.5 opacity-90">
              برای ادامهٔ کار با آزما، باید گذرواژهٔ پیش‌فرض را با یک گذرواژهٔ جدید جایگزین کنید.
            </div>
          </div>
        </div>
      )}

      <div>
        <Label htmlFor="cur">گذرواژه فعلی</Label>
        <div className="relative">
          <Input
            id="cur"
            type={showCurrent ? 'text' : 'password'}
            value={current}
            onChange={(e) => setCurrent(e.target.value)}
            autoComplete="current-password"
            dir="ltr"
            required
            className="pl-10"
          />
          <button
            type="button"
            onClick={() => setShowCurrent((s) => !s)}
            className="absolute left-2 top-1/2 -translate-y-1/2 p-1.5 rounded text-muted-foreground hover:text-foreground"
            tabIndex={-1}
          >
            {showCurrent ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
          </button>
        </div>
      </div>

      <div>
        <Label htmlFor="new">گذرواژه جدید</Label>
        <div className="relative">
          <Input
            id="new"
            type={showNext ? 'text' : 'password'}
            value={next}
            onChange={(e) => setNext(e.target.value)}
            autoComplete="new-password"
            dir="ltr"
            required
            minLength={8}
            className="pl-10"
          />
          <button
            type="button"
            onClick={() => setShowNext((s) => !s)}
            className="absolute left-2 top-1/2 -translate-y-1/2 p-1.5 rounded text-muted-foreground hover:text-foreground"
            tabIndex={-1}
          >
            {showNext ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
          </button>
        </div>
        <PasswordStrength value={next} />
      </div>

      <div>
        <Label htmlFor="conf">تکرار گذرواژه جدید</Label>
        <Input
          id="conf"
          type={showNext ? 'text' : 'password'}
          value={confirm}
          onChange={(e) => setConfirm(e.target.value)}
          autoComplete="new-password"
          dir="ltr"
          required
        />
        {confirm.length > 0 && !matches && (
          <p className="text-xs text-destructive mt-1">گذرواژه‌ها مطابقت ندارند.</p>
        )}
      </div>

      {error && (
        <div className="rounded-lg border border-destructive/30 bg-destructive/10 text-destructive p-3 text-sm flex items-start gap-2">
          <AlertCircle className="size-4 mt-0.5 shrink-0" />
          <div>{error}</div>
        </div>
      )}

      <div className="flex items-center justify-end gap-2 pt-2">
        {!forced && (
          <Button type="button" variant="ghost" onClick={() => nav(-1)} disabled={busy}>
            انصراف
          </Button>
        )}
        <Button type="submit" disabled={busy || !current || !strong || !matches}>
          {busy ? 'در حال ذخیره…' : (
            <>
              <ShieldCheck className="size-4" />
              تغییر گذرواژه
            </>
          )}
        </Button>
      </div>
    </form>
  );

  if (forced) {
    return (
      <motion.div
        initial={{ opacity: 0, y: 8 }}
        animate={{ opacity: 1, y: 0 }}
        className="min-h-[80vh] flex items-center justify-center"
      >
        <Card className="w-full max-w-md">
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <KeyRound className="size-4 text-primary" />
              تغییر گذرواژهٔ اولیه
            </CardTitle>
          </CardHeader>
          <CardContent>{form}</CardContent>
        </Card>
      </motion.div>
    );
  }

  return (
    <div className="space-y-6 max-w-xl">
      <PageHeader title="تغییر گذرواژه" description="گذرواژهٔ فعلی و گذرواژهٔ جدید خود را وارد کنید." />
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <KeyRound className="size-4 text-primary" />
            گذرواژهٔ حساب کاربری
          </CardTitle>
        </CardHeader>
        <CardContent>{form}</CardContent>
      </Card>
    </div>
  );
}

function PasswordStrength({ value }: { value: string }) {
  let score = 0;
  if (value.length >= 8) score++;
  if (value.length >= 12) score++;
  if (/[A-Z]/.test(value)) score++;
  if (/[0-9]/.test(value)) score++;
  if (/[^A-Za-z0-9]/.test(value)) score++;

  if (!value) return null;

  const labels = ['ضعیف', 'ضعیف', 'متوسط', 'خوب', 'قوی', 'بسیار قوی'];
  const colors = ['bg-destructive', 'bg-destructive', 'bg-warning', 'bg-warning', 'bg-success', 'bg-success'];

  return (
    <div className="mt-2 flex items-center gap-2">
      <div className="flex-1 h-1.5 bg-muted rounded-full overflow-hidden">
        <div
          className={`h-full transition-all ${colors[score]}`}
          style={{ width: `${(score / 5) * 100}%` }}
        />
      </div>
      <span className="text-xs text-muted-foreground w-16 text-left">{labels[score]}</span>
    </div>
  );
}
