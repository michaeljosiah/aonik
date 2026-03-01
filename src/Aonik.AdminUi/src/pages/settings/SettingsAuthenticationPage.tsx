import { useEffect, useState } from 'react';
import { AlertCircle, Cog, RefreshCw, Save, ShieldCheck } from 'lucide-react';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/badge';
import { Breadcrumb } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { authProviderSettingsService } from '@/services/authProviderSettingsService';
import { cn } from '@/lib/utils';
import type {
  AuthProviderSettingsResponse,
  AuthProviderSettingsUpdateRequest,
  AuthProviderType,
} from '@/types';

interface AuthProviderFormState {
  activeProvider: AuthProviderType;
  auth0Domain: string;
  auth0Audience: string;
  auth0ClientId: string;
  auth0ManagementClientId: string;
  auth0Connection: string;
  auth0ManagementAudience: string;
  auth0ManagementClientSecret: string;
  hasAuth0ManagementClientSecret: boolean;
  azureAdAuthority: string;
  azureAdAudience: string;
  azureAdClientId: string;
  azureAdTenantId: string;
  azureAdUpnDomain: string;
  azureAdClientSecret: string;
  hasAzureAdClientSecret: boolean;
}

function toInputValue(value?: string | null) {
  return value ?? '';
}

function toTrimmed(value: string) {
  return value.trim();
}

function resolveUserMessage(error: unknown, fallbackMessage: string) {
  const message = error && typeof error === 'object' && 'userMessage' in error
    ? String((error as { userMessage?: string }).userMessage ?? '')
    : '';

  return message || fallbackMessage;
}

function buildFormState(snapshot: AuthProviderSettingsResponse): AuthProviderFormState {
  return {
    activeProvider: snapshot.activeProvider,
    auth0Domain: toInputValue(snapshot.auth0.domain),
    auth0Audience: toInputValue(snapshot.auth0.audience),
    auth0ClientId: toInputValue(snapshot.auth0.clientId),
    auth0ManagementClientId: toInputValue(snapshot.auth0.managementClientId),
    auth0Connection: toInputValue(snapshot.auth0.connection),
    auth0ManagementAudience: toInputValue(snapshot.auth0.managementAudience),
    auth0ManagementClientSecret: '',
    hasAuth0ManagementClientSecret: snapshot.auth0.hasManagementClientSecret,
    azureAdAuthority: toInputValue(snapshot.azureAd.authority),
    azureAdAudience: toInputValue(snapshot.azureAd.audience),
    azureAdClientId: toInputValue(snapshot.azureAd.clientId),
    azureAdTenantId: toInputValue(snapshot.azureAd.tenantId),
    azureAdUpnDomain: toInputValue(snapshot.azureAd.userPrincipalNameDomain),
    azureAdClientSecret: '',
    hasAzureAdClientSecret: snapshot.azureAd.hasClientSecret,
  };
}

function SettingField({
  htmlFor,
  label,
  keyName,
  value,
  placeholder,
  onChange,
  type = 'text',
}: {
  htmlFor: string;
  label: string;
  keyName: string;
  value: string;
  placeholder?: string;
  onChange: (value: string) => void;
  type?: 'text' | 'password';
}) {
  return (
    <div className="space-y-2">
      <div className="flex items-center justify-between gap-3">
        <Label htmlFor={htmlFor}>{label}</Label>
        <span className="font-mono text-[11px] text-[var(--color-text-tertiary)]">{keyName}</span>
      </div>
      <Input
        id={htmlFor}
        type={type}
        value={value}
        placeholder={placeholder}
        onChange={(event) => onChange(event.target.value)}
      />
    </div>
  );
}

export function SettingsAuthenticationPage() {
  const [formState, setFormState] = useState<AuthProviderFormState | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const loadSettings = async () => {
    setLoading(true);
    setError(null);

    try {
      const snapshot = await authProviderSettingsService.get();
      setFormState(buildFormState(snapshot));
    } catch (err: unknown) {
      setError(resolveUserMessage(err, 'Failed to load authentication settings.'));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void loadSettings();
  }, []);

  const updateField = <K extends keyof AuthProviderFormState>(key: K, value: AuthProviderFormState[K]) => {
    setFormState((prev) => {
      if (!prev) return prev;
      return {
        ...prev,
        [key]: value,
      };
    });
  };

  const handleSave = async () => {
    if (!formState) return;

    setSaving(true);
    setError(null);

    const auth0ManagementSecret = toTrimmed(formState.auth0ManagementClientSecret);
    const azureAdSecret = toTrimmed(formState.azureAdClientSecret);

    const request: AuthProviderSettingsUpdateRequest = {
      activeProvider: formState.activeProvider,
      auth0: {
        domain: toTrimmed(formState.auth0Domain),
        audience: toTrimmed(formState.auth0Audience),
        clientId: toTrimmed(formState.auth0ClientId),
        managementClientId: toTrimmed(formState.auth0ManagementClientId),
        connection: toTrimmed(formState.auth0Connection),
        managementAudience: toTrimmed(formState.auth0ManagementAudience),
        managementClientSecret: auth0ManagementSecret.length > 0 ? auth0ManagementSecret : null,
      },
      azureAd: {
        authority: toTrimmed(formState.azureAdAuthority),
        audience: toTrimmed(formState.azureAdAudience),
        clientId: toTrimmed(formState.azureAdClientId),
        tenantId: toTrimmed(formState.azureAdTenantId),
        userPrincipalNameDomain: toTrimmed(formState.azureAdUpnDomain),
        clientSecret: azureAdSecret.length > 0 ? azureAdSecret : null,
      },
    };

    try {
      const updated = await authProviderSettingsService.update(request);
      setFormState(buildFormState(updated));
      toast.success('Authentication settings saved.');
    } catch (err: unknown) {
      setError(resolveUserMessage(err, 'Failed to save authentication settings.'));
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="h-full overflow-auto p-6">
      <Breadcrumb
        items={[
          { label: 'Settings', href: '/settings', icon: <Cog className="h-3.5 w-3.5" /> },
          { label: 'Authentication', icon: <ShieldCheck className="h-3.5 w-3.5" /> },
        ]}
        className="mb-4"
      />

      <div className="mb-6 flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Authentication Settings</h1>
          <p className="text-[var(--color-text-secondary)]">
            Manage provider-level identity settings, including keys like `Auth.AzureAd.TenantId` and `Auth.Auth0.Domain`.
          </p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" onClick={() => void loadSettings()} disabled={loading || saving}>
            <RefreshCw className={cn('mr-2 h-4 w-4', loading ? 'animate-spin' : '')} />
            Refresh
          </Button>
          <Button onClick={() => void handleSave()} disabled={loading || saving || !formState}>
            <Save className="mr-2 h-4 w-4" />
            Save settings
          </Button>
        </div>
      </div>

      {error && (
        <Card className="mb-6 border-[var(--color-error)] bg-[var(--color-error-light)]">
          <CardContent className="flex items-center gap-3 p-4 text-[var(--color-error)]">
            <AlertCircle className="h-5 w-5" />
            <span className="flex-1 text-sm">{error}</span>
            <Button variant="ghost" size="sm" onClick={() => void loadSettings()}>
              Retry
            </Button>
          </CardContent>
        </Card>
      )}

      {loading || !formState ? (
        <Card>
          <CardContent className="flex items-center justify-center py-12">
            <div className="flex items-center gap-3 text-[var(--color-text-secondary)]">
              <RefreshCw className="h-5 w-5 animate-spin" />
              <span>Loading authentication settings...</span>
            </div>
          </CardContent>
        </Card>
      ) : (
        <div className="space-y-6">
          <Card>
            <CardHeader>
              <CardTitle>Active Provider</CardTitle>
              <CardDescription>Select which identity provider is currently active for authentication.</CardDescription>
            </CardHeader>
            <CardContent className="space-y-2">
              <Label>Auth.Provider</Label>
              <Select
                value={formState.activeProvider}
                onValueChange={(value) => updateField('activeProvider', value as AuthProviderType)}
              >
                <SelectTrigger className="w-full sm:w-72">
                  <SelectValue placeholder="Select provider" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="AzureAd">Azure AD</SelectItem>
                  <SelectItem value="Auth0">Auth0</SelectItem>
                </SelectContent>
              </Select>
            </CardContent>
          </Card>

          <div className="grid gap-6 xl:grid-cols-2">
            <Card className={cn(formState.activeProvider === 'Auth0' && 'border-[var(--color-brand-primary)]')}>
              <CardHeader>
                <div className="flex items-center justify-between gap-2">
                  <CardTitle>Auth0 Configuration</CardTitle>
                  <div className="flex items-center gap-2">
                    {formState.activeProvider === 'Auth0' && <Badge variant="success">Active</Badge>}
                    <Badge variant={formState.hasAuth0ManagementClientSecret ? 'success' : 'outline'}>
                      {formState.hasAuth0ManagementClientSecret ? 'Secret configured' : 'Secret missing'}
                    </Badge>
                  </div>
                </div>
                <CardDescription>Provider settings stored under `Auth.Auth0.*` keys.</CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
                <SettingField
                  htmlFor="auth0-domain"
                  label="Domain"
                  keyName="Auth.Auth0.Domain"
                  value={formState.auth0Domain}
                  onChange={(value) => updateField('auth0Domain', value)}
                />
                <SettingField
                  htmlFor="auth0-audience"
                  label="Audience"
                  keyName="Auth.Auth0.Audience"
                  value={formState.auth0Audience}
                  onChange={(value) => updateField('auth0Audience', value)}
                />
                <SettingField
                  htmlFor="auth0-client-id"
                  label="Application Client ID"
                  keyName="Auth.Auth0.ClientId"
                  value={formState.auth0ClientId}
                  onChange={(value) => updateField('auth0ClientId', value)}
                />
                <SettingField
                  htmlFor="auth0-management-client-id"
                  label="Management Client ID"
                  keyName="Auth.Auth0.ManagementClientId"
                  value={formState.auth0ManagementClientId}
                  onChange={(value) => updateField('auth0ManagementClientId', value)}
                />
                <SettingField
                  htmlFor="auth0-connection"
                  label="Connection"
                  keyName="Auth.Auth0.Connection"
                  value={formState.auth0Connection}
                  onChange={(value) => updateField('auth0Connection', value)}
                />
                <SettingField
                  htmlFor="auth0-management-audience"
                  label="Management Audience"
                  keyName="Auth.Auth0.ManagementAudience"
                  value={formState.auth0ManagementAudience}
                  onChange={(value) => updateField('auth0ManagementAudience', value)}
                />
                <SettingField
                  htmlFor="auth0-management-client-secret"
                  label="Management Client Secret (update only)"
                  keyName="Auth.Auth0.ManagementClientSecret"
                  value={formState.auth0ManagementClientSecret}
                  placeholder="Leave empty to keep existing secret"
                  onChange={(value) => updateField('auth0ManagementClientSecret', value)}
                  type="password"
                />
              </CardContent>
            </Card>

            <Card className={cn(formState.activeProvider === 'AzureAd' && 'border-[var(--color-brand-primary)]')}>
              <CardHeader>
                <div className="flex items-center justify-between gap-2">
                  <CardTitle>Azure AD Configuration</CardTitle>
                  <div className="flex items-center gap-2">
                    {formState.activeProvider === 'AzureAd' && <Badge variant="success">Active</Badge>}
                    <Badge variant={formState.hasAzureAdClientSecret ? 'success' : 'outline'}>
                      {formState.hasAzureAdClientSecret ? 'Secret configured' : 'Secret missing'}
                    </Badge>
                  </div>
                </div>
                <CardDescription>Provider settings stored under `Auth.AzureAd.*` keys.</CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
                <SettingField
                  htmlFor="azure-ad-authority"
                  label="Authority"
                  keyName="Auth.AzureAd.Authority"
                  value={formState.azureAdAuthority}
                  onChange={(value) => updateField('azureAdAuthority', value)}
                />
                <SettingField
                  htmlFor="azure-ad-audience"
                  label="Audience"
                  keyName="Auth.AzureAd.Audience"
                  value={formState.azureAdAudience}
                  onChange={(value) => updateField('azureAdAudience', value)}
                />
                <SettingField
                  htmlFor="azure-ad-client-id"
                  label="Client ID"
                  keyName="Auth.AzureAd.ClientId"
                  value={formState.azureAdClientId}
                  onChange={(value) => updateField('azureAdClientId', value)}
                />
                <SettingField
                  htmlFor="azure-ad-tenant-id"
                  label="Tenant ID"
                  keyName="Auth.AzureAd.TenantId"
                  value={formState.azureAdTenantId}
                  onChange={(value) => updateField('azureAdTenantId', value)}
                />
                <SettingField
                  htmlFor="azure-ad-upn-domain"
                  label="UPN Domain"
                  keyName="Auth.AzureAd.UserPrincipalNameDomain"
                  value={formState.azureAdUpnDomain}
                  onChange={(value) => updateField('azureAdUpnDomain', value)}
                />
                <SettingField
                  htmlFor="azure-ad-client-secret"
                  label="Client Secret (update only)"
                  keyName="Auth.AzureAd.ClientSecret"
                  value={formState.azureAdClientSecret}
                  placeholder="Leave empty to keep existing secret"
                  onChange={(value) => updateField('azureAdClientSecret', value)}
                  type="password"
                />
              </CardContent>
            </Card>
          </div>
        </div>
      )}
    </div>
  );
}
