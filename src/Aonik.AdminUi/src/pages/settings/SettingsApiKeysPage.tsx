import { useEffect, useMemo, useState } from 'react';
import { Copy, Cog, KeyRound, Plus, ShieldCheck, Trash2 } from 'lucide-react';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/badge';
import { Breadcrumb } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { cn } from '@/lib/utils';

const apiKeysStorageKey = 'aonik:settings:api-keys';

type ApiKeyStatus = 'Active' | 'Revoked';

interface ApiKeyItem {
  id: string;
  name: string;
  maskedToken: string;
  createdAt: string;
  lastUsedAt: string | null;
  scopes: string[];
  status: ApiKeyStatus;
}

const availableScopes = [
  'orders:read',
  'orders:write',
  'payments:read',
  'payments:write',
  'ledger:read',
  'catalog:read',
  'compliance:read',
];

const defaultApiKeys: ApiKeyItem[] = [
  {
    id: 'key_ops_001',
    name: 'Operations Worker',
    maskedToken: 'ak_live_4hd9...7k2q',
    createdAt: '2026-01-10T09:15:00Z',
    lastUsedAt: '2026-02-27T11:03:00Z',
    scopes: ['orders:read', 'payments:read', 'ledger:read'],
    status: 'Active',
  },
  {
    id: 'key_reporting_002',
    name: 'Reporting Pipeline',
    maskedToken: 'ak_live_2mz8...k1ax',
    createdAt: '2025-12-18T14:22:00Z',
    lastUsedAt: '2026-02-25T19:40:00Z',
    scopes: ['orders:read', 'payments:read', 'ledger:read', 'compliance:read'],
    status: 'Active',
  },
];

function randomTokenSegment(length: number) {
  const alphabet = 'abcdefghijklmnopqrstuvwxyz0123456789';
  let result = '';

  if (typeof window !== 'undefined' && window.crypto?.getRandomValues) {
    const bytes = new Uint8Array(length);
    window.crypto.getRandomValues(bytes);
    for (let index = 0; index < length; index += 1) {
      result += alphabet[bytes[index] % alphabet.length];
    }
    return result;
  }

  for (let index = 0; index < length; index += 1) {
    result += alphabet[Math.floor(Math.random() * alphabet.length)];
  }

  return result;
}

function buildApiToken() {
  return `ak_live_${randomTokenSegment(28)}`;
}

function maskToken(token: string) {
  return `${token.slice(0, 12)}...${token.slice(-4)}`;
}

function getInitialApiKeys() {
  try {
    const raw = localStorage.getItem(apiKeysStorageKey);
    if (!raw) return defaultApiKeys;

    const parsed = JSON.parse(raw) as ApiKeyItem[];
    if (!Array.isArray(parsed)) return defaultApiKeys;
    return parsed;
  } catch {
    return defaultApiKeys;
  }
}

export function SettingsApiKeysPage() {
  const [apiKeys, setApiKeys] = useState<ApiKeyItem[]>(() => getInitialApiKeys());
  const [newKeyName, setNewKeyName] = useState('');
  const [selectedScopes, setSelectedScopes] = useState<string[]>(['orders:read']);
  const [generatedToken, setGeneratedToken] = useState<string | null>(null);

  useEffect(() => {
    localStorage.setItem(apiKeysStorageKey, JSON.stringify(apiKeys));
  }, [apiKeys]);

  const activeKeyCount = useMemo(() => apiKeys.filter((key) => key.status === 'Active').length, [apiKeys]);

  const toggleScope = (scope: string) => {
    setSelectedScopes((prev) => {
      if (prev.includes(scope)) {
        return prev.filter((item) => item !== scope);
      }

      return [...prev, scope];
    });
  };

  const handleCreateKey = () => {
    const trimmedName = newKeyName.trim();
    if (!trimmedName) {
      toast.error('Provide a label for the key.');
      return;
    }

    if (selectedScopes.length === 0) {
      toast.error('Select at least one scope.');
      return;
    }

    const token = buildApiToken();
    const createdAt = new Date().toISOString();

    const newKey: ApiKeyItem = {
      id: `key_${randomTokenSegment(10)}`,
      name: trimmedName,
      maskedToken: maskToken(token),
      createdAt,
      lastUsedAt: null,
      scopes: [...selectedScopes],
      status: 'Active',
    };

    setApiKeys((prev) => [newKey, ...prev]);
    setGeneratedToken(token);
    setNewKeyName('');
    toast.success('API key generated. Copy it now; it is shown once.');
  };

  const handleCopyGeneratedToken = async () => {
    if (!generatedToken) return;
    try {
      await navigator.clipboard.writeText(generatedToken);
      toast.success('API key copied to clipboard.');
    } catch {
      toast.error('Clipboard copy failed.');
    }
  };

  const handleRevokeKey = (keyId: string) => {
    setApiKeys((prev) => prev.map((key) => (key.id === keyId ? { ...key, status: 'Revoked' } : key)));
    toast.success('API key revoked.');
  };

  const formatDateTime = (value: string | null) => {
    if (!value) return 'Never used';
    return new Date(value).toLocaleString();
  };

  return (
    <div className="h-full overflow-auto p-6">
      <Breadcrumb
        items={[
          { label: 'Settings', href: '/settings', icon: <Cog className="h-3.5 w-3.5" /> },
          { label: 'API Keys', icon: <KeyRound className="h-3.5 w-3.5" /> },
        ]}
        className="mb-4"
      />

      <div className="mb-6">
        <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">API Keys</h1>
        <p className="text-[var(--color-text-secondary)]">
          Issue scoped credentials for trusted integrations and automation workers.
        </p>
      </div>

      <div className="space-y-6">
        <Card>
          <CardHeader className="flex flex-row items-start justify-between gap-4">
            <div>
              <CardTitle>Create API Key</CardTitle>
              <CardDescription>Define a clear label and minimum required permissions.</CardDescription>
            </div>
            <Badge variant="secondary">{activeKeyCount} active keys</Badge>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="api-key-label">Key label</Label>
              <Input
                id="api-key-label"
                placeholder="Example: Reconciliation Worker"
                value={newKeyName}
                onChange={(event) => setNewKeyName(event.target.value)}
              />
            </div>

            <div className="space-y-2">
              <Label>Scopes</Label>
              <div className="flex flex-wrap gap-2">
                {availableScopes.map((scope) => {
                  const selected = selectedScopes.includes(scope);
                  return (
                    <button
                      key={scope}
                      type="button"
                      className={cn(
                        'rounded-sm border px-2.5 py-1.5 text-xs transition-colors',
                        selected
                          ? 'border-[var(--color-brand-primary)] bg-[var(--color-brand-primary-light)] text-[var(--color-brand-primary)]'
                          : 'border-[var(--color-border)] text-[var(--color-text-secondary)] hover:text-[var(--color-text-primary)]'
                      )}
                      onClick={() => toggleScope(scope)}
                    >
                      {scope}
                    </button>
                  );
                })}
              </div>
            </div>

            <div className="flex justify-end">
              <Button onClick={handleCreateKey}>
                <Plus className="mr-2 h-4 w-4" />
                Generate key
              </Button>
            </div>

            {generatedToken && (
              <div className="rounded-md border border-[var(--color-warning)] bg-[var(--color-warning-light)] p-3">
                <div className="mb-2 flex items-center justify-between gap-2">
                  <p className="text-sm font-medium text-[var(--color-warning)]">Copy your new key now</p>
                  <Button size="sm" variant="secondary" onClick={handleCopyGeneratedToken}>
                    <Copy className="mr-2 h-4 w-4" />
                    Copy
                  </Button>
                </div>
                <p className="rounded-sm bg-black/10 px-2 py-1 font-mono text-xs text-[var(--color-warning)]">
                  {generatedToken}
                </p>
              </div>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <ShieldCheck className="h-4 w-4 text-[var(--color-brand-primary)]" />
              Issued Keys
            </CardTitle>
            <CardDescription>Revoke keys that are no longer needed.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {apiKeys.length === 0 ? (
              <p className="text-sm text-[var(--color-text-tertiary)]">No API keys have been issued.</p>
            ) : (
              apiKeys.map((apiKey) => (
                <div
                  key={apiKey.id}
                  className="flex flex-col gap-3 rounded-md border border-[var(--color-border-light)] px-4 py-3 lg:flex-row lg:items-start lg:justify-between"
                >
                  <div className="space-y-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <p className="text-sm font-medium text-[var(--color-text-primary)]">{apiKey.name}</p>
                      <Badge variant={apiKey.status === 'Active' ? 'success' : 'outline'}>{apiKey.status}</Badge>
                    </div>
                    <p className="font-mono text-xs text-[var(--color-text-tertiary)]">{apiKey.maskedToken}</p>
                    <p className="text-xs text-[var(--color-text-tertiary)]">
                      Created {formatDateTime(apiKey.createdAt)} · Last used {formatDateTime(apiKey.lastUsedAt)}
                    </p>
                    <p className="text-xs text-[var(--color-text-secondary)]">Scopes: {apiKey.scopes.join(', ')}</p>
                  </div>

                  <div>
                    {apiKey.status === 'Active' ? (
                      <Button size="sm" variant="outline" onClick={() => handleRevokeKey(apiKey.id)}>
                        <Trash2 className="mr-2 h-4 w-4" />
                        Revoke
                      </Button>
                    ) : (
                      <Badge variant="outline">Inactive</Badge>
                    )}
                  </div>
                </div>
              ))
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
