import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { Bot, CheckCircle2, Copy, KeyRound, Plus, RefreshCw, Trash2 } from 'lucide-react';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { AuthApi } from '@/lib/queries/endpoints';
import type { CreateMcpTokenResponse, McpToken } from '@/lib/types';
import { extractError } from '@/lib/api';
import { fmtDateTime } from '@/lib/date';
import { useAuthStore } from '@/lib/auth/store';

const DEFAULT_EXPIRES_IN_DAYS = 90;

function statusVariant(status: McpToken['status']) {
  if (status === 'Active') return 'success';
  if (status === 'Expired') return 'warning';
  return 'muted';
}

function statusLabel(status: McpToken['status']) {
  if (status === 'Active') return 'فعال';
  if (status === 'Expired') return 'منقضی';
  return 'لغوشده';
}

export default function McpSettingsPage() {
  const user = useAuthStore((s) => s.user);
  const [tokens, setTokens] = useState<McpToken[]>([]);
  const [created, setCreated] = useState<CreateMcpTokenResponse | null>(null);
  const [name, setName] = useState('Cursor');
  const [currentPassword, setCurrentPassword] = useState('');
  const [expiresInDays, setExpiresInDays] = useState(DEFAULT_EXPIRES_IN_DAYS);
  const [loading, setLoading] = useState(true);
  const [creating, setCreating] = useState(false);
  const [revokingId, setRevokingId] = useState<string | null>(null);

  const mcpUrl = useMemo(() => {
    const host = window.location.hostname || 'localhost';
    return `http://${host}:8080/mcp`;
  }, []);

  const cursorConfig = useMemo(
    () =>
      JSON.stringify(
        {
          mcpServers: {
            azma: {
              url: mcpUrl,
              headers: {
                Authorization: 'Bearer ${env:AZMA_MCP_TOKEN}',
              },
            },
          },
        },
        null,
        2,
      ),
    [mcpUrl],
  );

  async function loadTokens() {
    setLoading(true);
    try {
      setTokens(await AuthApi.listMcpTokens());
    } catch (err) {
      toast.error(extractError(err, 'خطا در دریافت توکن‌های MCP'));
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void loadTokens();
  }, []);

  async function copy(text: string, message: string) {
    await navigator.clipboard.writeText(text);
    toast.success(message);
  }

  async function onCreate(e: FormEvent) {
    e.preventDefault();
    setCreating(true);
    try {
      const result = await AuthApi.createMcpToken({
        name,
        currentPassword,
        expiresInDays,
      });
      setCreated(result);
      setCurrentPassword('');
      await loadTokens();
      toast.success('توکن MCP ساخته شد');
    } catch (err) {
      toast.error(extractError(err, 'خطا در ساخت توکن MCP'));
    } finally {
      setCreating(false);
    }
  }

  async function revoke(id: string) {
    setRevokingId(id);
    try {
      await AuthApi.revokeMcpToken(id);
      await loadTokens();
      toast.success('توکن لغو شد');
    } catch (err) {
      toast.error(extractError(err, 'خطا در لغو توکن'));
    } finally {
      setRevokingId(null);
    }
  }

  return (
    <div className="grid grid-cols-1 xl:grid-cols-5 gap-6">
      <div className="xl:col-span-2 space-y-6">
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <Bot className="size-4 text-primary" />
              MCP
            </CardTitle>
            <CardDescription>
              {user?.workspaceName ? `فضای کاری فعلی: ${user.workspaceName}` : 'فضای کاری فعلی'}
            </CardDescription>
          </CardHeader>
          <CardContent>
            <form onSubmit={onCreate} className="space-y-4">
              <div>
                <Label htmlFor="mcp-token-name">نام</Label>
                <Input
                  id="mcp-token-name"
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  maxLength={120}
                  required
                />
              </div>
              <div>
                <Label htmlFor="mcp-token-password">گذرواژه فعلی</Label>
                <Input
                  id="mcp-token-password"
                  type="password"
                  value={currentPassword}
                  onChange={(e) => setCurrentPassword(e.target.value)}
                  autoComplete="current-password"
                  required
                  dir="ltr"
                />
              </div>
              <div>
                <Label htmlFor="mcp-token-expiry">اعتبار روز</Label>
                <Input
                  id="mcp-token-expiry"
                  type="number"
                  min={1}
                  max={365}
                  value={expiresInDays}
                  onChange={(e) => setExpiresInDays(Number(e.target.value))}
                  required
                  dir="ltr"
                />
              </div>
              <Button type="submit" loading={creating} className="w-full">
                <Plus className="size-4" />
                ساخت توکن
              </Button>
            </form>
          </CardContent>
        </Card>

        {created && (
          <Card variant="raised">
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <CheckCircle2 className="size-4 text-success" />
                توکن جدید
              </CardTitle>
              <CardDescription>این مقدار دوباره نمایش داده نمی‌شود.</CardDescription>
            </CardHeader>
            <CardContent className="space-y-3">
              <code className="block rounded-lg border bg-secondary/60 p-3 text-xs break-all text-start" dir="ltr">
                {created.token}
              </code>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                <Button type="button" variant="outline" onClick={() => copy(created.token, 'توکن کپی شد')}>
                  <Copy className="size-4" />
                  کپی توکن
                </Button>
                <Button
                  type="button"
                  variant="outline"
                  onClick={() => copy(`$env:AZMA_MCP_TOKEN="${created.token}"`, 'دستور env کپی شد')}
                >
                  <KeyRound className="size-4" />
                  کپی env
                </Button>
              </div>
            </CardContent>
          </Card>
        )}
      </div>

      <div className="xl:col-span-3 space-y-6">
        <Card>
          <CardHeader className="flex-row items-center justify-between gap-3">
            <div>
              <CardTitle>Cursor config</CardTitle>
              <CardDescription dir="ltr">{mcpUrl}</CardDescription>
            </div>
            <Button type="button" variant="outline" size="sm" onClick={() => copy(cursorConfig, 'تنظیمات کپی شد')}>
              <Copy className="size-4" />
              کپی
            </Button>
          </CardHeader>
          <CardContent>
            <pre className="max-h-80 overflow-auto rounded-lg border bg-secondary/60 p-4 text-xs text-start" dir="ltr">
              {cursorConfig}
            </pre>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex-row items-center justify-between gap-3">
            <div>
              <CardTitle>توکن‌ها</CardTitle>
              <CardDescription>توکن‌های همین فضای کاری</CardDescription>
            </div>
            <Button type="button" variant="ghost" size="icon-sm" onClick={loadTokens} disabled={loading}>
              <RefreshCw className={loading ? 'size-4 animate-spin' : 'size-4'} />
            </Button>
          </CardHeader>
          <CardContent>
            <div className="divide-y divide-border rounded-lg border">
              {tokens.length === 0 && (
                <div className="p-6 text-center text-sm text-muted-foreground">
                  توکنی ساخته نشده است.
                </div>
              )}
              {tokens.map((token) => (
                <div key={token.id} className="flex flex-col gap-3 p-4 sm:flex-row sm:items-center">
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2">
                      <span className="font-medium truncate">{token.name}</span>
                      <Badge variant={statusVariant(token.status)}>{statusLabel(token.status)}</Badge>
                    </div>
                    <div className="mt-1 text-xs text-muted-foreground" dir="ltr">
                      expires {fmtDateTime(token.expiresAtUtc)}
                      {token.lastUsedAtUtc ? ` · last used ${fmtDateTime(token.lastUsedAtUtc)}` : ''}
                    </div>
                  </div>
                  <Button
                    type="button"
                    variant="ghost"
                    size="sm"
                    className="text-destructive hover:text-destructive"
                    disabled={token.status === 'Revoked' || revokingId === token.id}
                    loading={revokingId === token.id}
                    onClick={() => revoke(token.id)}
                  >
                    <Trash2 className="size-4" />
                    لغو
                  </Button>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
