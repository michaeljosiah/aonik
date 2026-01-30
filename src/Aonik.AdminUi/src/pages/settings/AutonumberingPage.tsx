import { createPortal } from 'react-dom';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { Hash, RefreshCw, AlertCircle, Plus, X, Beaker, Pencil } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { autonumberingService } from '@/services/autonumberingService';
import type { AutonumberProfile, AutonumberStrategy, AutonumberResetPolicy, GenerateAutonumberResponse } from '@/types';

const tokenizedDate = (template: string, date: Date) => {
  const year = date.getFullYear().toString();
  const shortYear = year.slice(-2);
  const month = (date.getMonth() + 1).toString().padStart(2, '0');
  const day = date.getDate().toString().padStart(2, '0');

  return template
    .replace(/\{YYYY\}/gi, year)
    .replace(/\{YY\}/gi, shortYear)
    .replace(/\{MM\}/gi, month)
    .replace(/\{DD\}/gi, day);
};

// Test Reference Dialog Component
function TestReferenceDialog({
  isOpen,
  onClose,
  onTest,
}: {
  isOpen: boolean;
  onClose: () => void;
  onTest: (entityType: string, prefix: string, suffix: string, paddingLength: number, sequenceValue: number) => Promise<string>;
}) {
  const [entityType, setEntityType] = useState('Invoice');
  const [prefix, setPrefix] = useState('INV-{YYYY}-');
  const [suffix, setSuffix] = useState('');
  const [paddingLength, setPaddingLength] = useState('4');
  const [sequenceValue, setSequenceValue] = useState('421');
  const [isTesting, setIsTesting] = useState(false);
  const [testResult, setTestResult] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!isOpen) return;
    setTestResult(null);
    setError(null);
  }, [isOpen]);

  const preview = useMemo(() => {
    const padding = Number.parseInt(paddingLength, 10);
    const nextValue = Number.parseInt(sequenceValue, 10);
    const safePadding = Number.isNaN(padding) ? 0 : Math.max(padding, 0);
    const safeValue = Number.isNaN(nextValue) ? 0 : Math.max(nextValue, 0);
    const date = new Date();
    const padded = safePadding > 0 ? safeValue.toString().padStart(safePadding, '0') : safeValue.toString();
    return `${tokenizedDate(prefix, date)}${padded}${tokenizedDate(suffix, date)}`;
  }, [paddingLength, prefix, sequenceValue, suffix]);

  const handleTest = async () => {
    setIsTesting(true);
    setError(null);
    setTestResult(null);
    try {
      const result = await onTest(
        entityType,
        prefix,
        suffix,
        Number.parseInt(paddingLength, 10),
        Number.parseInt(sequenceValue, 10)
      );
      setTestResult(result);
    } catch (err: unknown) {
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setError(message || 'Failed to test reference generation.');
    } finally {
      setIsTesting(false);
    }
  };

  if (!isOpen) return null;

  return createPortal(
    <div className="fixed inset-0 z-[120] flex items-center justify-center bg-black/50 p-4">
      <div className="w-[min(92vw,40rem)] rounded-md bg-[var(--color-surface)] border border-[var(--color-border)] shadow-lg">
        <div className="flex items-center justify-between border-b border-[var(--color-border-light)] px-4 py-3">
          <h3 className="text-sm font-semibold text-[var(--color-text-primary)]">Test a Reference</h3>
          <button
            type="button"
            className="rounded-sm p-1 text-[var(--color-text-tertiary)] hover:text-[var(--color-text-primary)]"
            onClick={onClose}
          >
            <X className="w-4 h-4" />
          </button>
        </div>
        <div className="space-y-4 px-4 py-4 max-h-[70vh] overflow-auto">
          {error && (
            <div className="rounded-sm border border-[var(--color-error)] bg-[var(--color-error-light)] px-3 py-2 text-xs text-[var(--color-error)]">
              {error}
            </div>
          )}
          {testResult && (
            <div className="rounded-sm border border-[var(--color-success)] bg-[var(--color-success-light)] px-3 py-2 text-xs text-[var(--color-success)]">
              <strong>Generated:</strong> {testResult}
            </div>
          )}
          <div className="grid gap-4 md:grid-cols-3">
            <div className="space-y-2">
              <Label htmlFor="entity-type">Entity Type</Label>
              <Select value={entityType} onValueChange={setEntityType}>
                <SelectTrigger id="entity-type">
                  <SelectValue placeholder="Select entity type" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Invoice">Invoice</SelectItem>
                  <SelectItem value="Order">Order</SelectItem>
                  <SelectItem value="CreditNote">Credit Note</SelectItem>
                  <SelectItem value="Payment">Payment</SelectItem>
                  <SelectItem value="Payout">Payout</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="strategy">Strategy</Label>
              <Select value="Sequential" disabled>
                <SelectTrigger id="strategy">
                  <SelectValue placeholder="Select strategy" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Sequential">Sequential</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="reset-policy">Reset Policy</Label>
              <Select value="Monthly" disabled>
                <SelectTrigger id="reset-policy">
                  <SelectValue placeholder="Select reset policy" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="None">None</SelectItem>
                  <SelectItem value="Monthly">Monthly</SelectItem>
                  <SelectItem value="Yearly">Yearly</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2 md:col-span-2">
              <Label htmlFor="prefix">Prefix Template</Label>
              <Input id="prefix" value={prefix} onChange={(event) => setPrefix(event.target.value)} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="suffix">Suffix Template</Label>
              <Input id="suffix" value={suffix} onChange={(event) => setSuffix(event.target.value)} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="padding-length">Padding Length</Label>
              <Input
                id="padding-length"
                type="number"
                min="0"
                value={paddingLength}
                onChange={(event) => setPaddingLength(event.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="sequence-value">Sequence Value</Label>
              <Input
                id="sequence-value"
                type="number"
                min="0"
                value={sequenceValue}
                onChange={(event) => setSequenceValue(event.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label>Preview</Label>
              <div className="h-9 flex items-center rounded-md border border-dashed border-[var(--color-border)] px-3 text-sm text-[var(--color-text-primary)]">
                {preview}
              </div>
            </div>
          </div>

          <div className="flex flex-wrap items-center gap-3 pt-4">
            <Button variant="default" onClick={handleTest} disabled={isTesting}>
              {isTesting ? (
                <>
                  <RefreshCw className="w-4 h-4 mr-2 animate-spin" />
                  Testing...
                </>
              ) : (
                <>
                  <Beaker className="w-4 h-4 mr-2" />
                  Run Test
                </>
              )}
            </Button>
            <span className="text-xs text-[var(--color-text-tertiary)]">
              Preview uses the current date with tokens {'{YYYY}'}, {'{MM}'}, {'{DD}'}.
            </span>
          </div>
        </div>
      </div>
    </div>,
    document.body
  );
}

// Edit Profile Dialog Component
function EditProfileDialog({
  profile,
  isOpen,
  onClose,
  onSave,
}: {
  profile: AutonumberProfile | null;
  isOpen: boolean;
  onClose: () => void;
  onSave: (profile: AutonumberProfile) => Promise<void>;
}) {
  const [form, setForm] = useState<Partial<AutonumberProfile>>({});
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!isOpen || !profile) return;
    setForm({ ...profile });
    setError(null);
  }, [isOpen, profile]);

  if (!isOpen || !profile) return null;

  const updateField = <K extends keyof AutonumberProfile>(key: K, value: AutonumberProfile[K]) => {
    setForm((prev) => ({ ...prev, [key]: value }));
  };

  const handleSave = async () => {
    setIsSaving(true);
    setError(null);
    try {
      const updatedProfile = { ...profile, ...form } as AutonumberProfile;
      await onSave(updatedProfile);
      onClose();
    } catch (err: unknown) {
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setError(message || 'Failed to save profile.');
    } finally {
      setIsSaving(false);
    }
  };

  return createPortal(
    <div className="fixed inset-0 z-[120] flex items-center justify-center bg-black/50 p-4">
      <div className="w-[min(92vw,32rem)] rounded-md bg-[var(--color-surface)] border border-[var(--color-border)] shadow-lg">
        <div className="flex items-center justify-between border-b border-[var(--color-border-light)] px-4 py-3">
          <h3 className="text-sm font-semibold text-[var(--color-text-primary)]">
            Edit Configuration: {profile.entityType}
          </h3>
          <button
            type="button"
            className="rounded-sm p-1 text-[var(--color-text-tertiary)] hover:text-[var(--color-text-primary)]"
            onClick={onClose}
          >
            <X className="w-4 h-4" />
          </button>
        </div>
        <div className="space-y-4 px-4 py-4 max-h-[70vh] overflow-auto">
          {error && (
            <div className="rounded-sm border border-[var(--color-error)] bg-[var(--color-error-light)] px-3 py-2 text-xs text-[var(--color-error)]">
              {error}
            </div>
          )}
          <div className="grid gap-4">
            <div className="space-y-2">
              <Label htmlFor="edit-prefix">Prefix Template</Label>
              <Input
                id="edit-prefix"
                value={form.prefixTemplate || ''}
                onChange={(e) => updateField('prefixTemplate', e.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="edit-suffix">Suffix Template</Label>
              <Input
                id="edit-suffix"
                value={form.suffixTemplate || ''}
                onChange={(e) => updateField('suffixTemplate', e.target.value)}
              />
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="edit-padding">Padding Length</Label>
                <Input
                  id="edit-padding"
                  type="number"
                  min="0"
                  value={form.paddingLength || 0}
                  onChange={(e) => updateField('paddingLength', Number.parseInt(e.target.value, 10) || 0)}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="edit-strategy">Strategy</Label>
                <Select
                  value={form.strategy || 'Sequential'}
                  onValueChange={(value) => updateField('strategy', value as AutonumberStrategy)}
                >
                  <SelectTrigger id="edit-strategy">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Sequential">Sequential</SelectItem>
                    <SelectItem value="Random">Random</SelectItem>
                    <SelectItem value="Hybrid">Hybrid</SelectItem>
                  </SelectContent>
                </Select>
              </div>
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="edit-reset">Reset Policy</Label>
                <Select
                  value={form.resetPolicy || 'None'}
                  onValueChange={(value) => updateField('resetPolicy', value as AutonumberResetPolicy)}
                >
                  <SelectTrigger id="edit-reset">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="None">None</SelectItem>
                    <SelectItem value="Monthly">Monthly</SelectItem>
                    <SelectItem value="Yearly">Yearly</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label htmlFor="edit-status">Status</Label>
                <Select
                  value={form.isActive ? 'Active' : 'Paused'}
                  onValueChange={(value) => updateField('isActive', value === 'Active')}
                >
                  <SelectTrigger id="edit-status">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Active">Active</SelectItem>
                    <SelectItem value="Paused">Paused</SelectItem>
                  </SelectContent>
                </Select>
              </div>
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="edit-min">Min Value</Label>
                <Input
                  id="edit-min"
                  type="number"
                  min="0"
                  value={form.minValue || 1}
                  onChange={(e) => updateField('minValue', Number.parseInt(e.target.value, 10) || 1)}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="edit-max">Max Value</Label>
                <Input
                  id="edit-max"
                  type="number"
                  min="0"
                  value={form.maxValue || 999999}
                  onChange={(e) => updateField('maxValue', Number.parseInt(e.target.value, 10) || 999999)}
                />
              </div>
            </div>
          </div>

          <div className="flex justify-end gap-3 pt-4 border-t border-[var(--color-border-light)]">
            <Button variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button onClick={handleSave} disabled={isSaving}>
              {isSaving ? (
                <>
                  <RefreshCw className="w-4 h-4 mr-2 animate-spin" />
                  Saving...
                </>
              ) : (
                'Save Changes'
              )}
            </Button>
          </div>
        </div>
      </div>
    </div>,
    document.body
  );
}

export function AutonumberingPage() {
  const [profiles, setProfiles] = useState<AutonumberProfile[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [testDialogOpen, setTestDialogOpen] = useState(false);
  const [editDialogOpen, setEditDialogOpen] = useState(false);
  const [editingProfile, setEditingProfile] = useState<AutonumberProfile | null>(null);

  const loadProfiles = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await autonumberingService.list();
      setProfiles(result);
    } catch (err: unknown) {
      console.error('Failed to load autonumbering profiles:', err);
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setError(message || 'Failed to load autonumbering configurations. Please try again.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadProfiles();
  }, [loadProfiles]);

  const handleTest = async (
    entityType: string,
    prefix: string,
    suffix: string,
    paddingLength: number,
    sequenceValue: number
  ): Promise<string> => {
    // First ensure the profile exists with the test settings
    await autonumberingService.upsert({
      entityType,
      prefixTemplate: prefix,
      suffixTemplate: suffix,
      strategy: 'Sequential',
      resetPolicy: 'None',
      paddingLength,
      minValue: 1,
      maxValue: 999999,
      isActive: true,
    });

    // Then generate a reference
    const result: GenerateAutonumberResponse = await autonumberingService.generate({ entityType });
    return result.reference;
  };

  const handleEdit = (profile: AutonumberProfile) => {
    setEditingProfile(profile);
    setEditDialogOpen(true);
  };

  const handleSave = async (profile: AutonumberProfile) => {
    await autonumberingService.upsert({
      entityType: profile.entityType,
      prefixTemplate: profile.prefixTemplate,
      suffixTemplate: profile.suffixTemplate,
      strategy: profile.strategy,
      resetPolicy: profile.resetPolicy,
      paddingLength: profile.paddingLength,
      minValue: profile.minValue,
      maxValue: profile.maxValue,
      isActive: profile.isActive,
    });
    await loadProfiles();
  };

  const formatRange = (profile: AutonumberProfile) => {
    return `${profile.minValue.toLocaleString()} - ${profile.maxValue.toLocaleString()}`;
  };

  const formatLastIssued = (profile: AutonumberProfile) => {
    if (!profile.lastIssuedValue || profile.lastIssuedValue < profile.minValue) {
      return 'Never';
    }
    const prefix = tokenizedDate(profile.prefixTemplate, new Date());
    const suffix = tokenizedDate(profile.suffixTemplate, new Date());
    const padded = profile.paddingLength > 0
      ? profile.lastIssuedValue.toString().padStart(profile.paddingLength, '0')
      : profile.lastIssuedValue.toString();
    return `${prefix}${padded}${suffix}`;
  };

  return (
    <div className="h-full overflow-auto p-6">
      <Breadcrumb
        items={[
          { label: 'Settings', href: '/settings', icon: <Hash className="w-3.5 h-3.5" /> },
          { label: 'Autonumbering' },
        ]}
        className="mb-4"
      />

      {/* Page Header */}
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Autonumbering</h1>
          <p className="text-[var(--color-text-secondary)]">
            Configure and validate reference sequences for invoices, orders, and other financial documents.
          </p>
        </div>
        <div className="flex gap-3">
          <Button variant="outline" onClick={() => setTestDialogOpen(true)} className="rounded-sm">
            <Beaker className="w-4 h-4 mr-2" />
            Test Reference
          </Button>
          <Button className="rounded-sm">
            <Plus className="w-4 h-4 mr-2" />
            New Configuration
          </Button>
        </div>
      </div>

      {/* Error State */}
      {error && (
        <Card className="mb-6 border-[var(--color-error)] bg-[var(--color-error-light)]">
          <CardContent className="p-4 flex items-center gap-3 text-[var(--color-error)]">
            <AlertCircle className="w-5 h-5" />
            <span>{error}</span>
            <Button variant="outline" size="sm" onClick={loadProfiles} className="ml-auto">
              Retry
            </Button>
          </CardContent>
        </Card>
      )}

      {/* Configurations Card */}
      <Card>
        <CardHeader>
          <CardTitle>Configurations</CardTitle>
          <CardDescription>Active tenant-scoped numbering profiles and last issued references.</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="mt-3 rounded-md border border-[var(--color-border-light)] overflow-hidden">
            <div className="overflow-x-auto">
              <table className="w-full">
                <thead>
                  <tr className="border-b border-[var(--color-border-light)] bg-[var(--color-surface-inset)]/50">
                    <th className="text-left px-4 py-3 text-xs font-medium uppercase tracking-wider text-[var(--color-text-secondary)]">
                      Entity Type
                    </th>
                    <th className="text-left px-4 py-3 text-xs font-medium uppercase tracking-wider text-[var(--color-text-secondary)]">
                      Strategy
                    </th>
                    <th className="text-left px-4 py-3 text-xs font-medium uppercase tracking-wider text-[var(--color-text-secondary)]">
                      Reset
                    </th>
                    <th className="text-left px-4 py-3 text-xs font-medium uppercase tracking-wider text-[var(--color-text-secondary)]">
                      Range
                    </th>
                    <th className="text-left px-4 py-3 text-xs font-medium uppercase tracking-wider text-[var(--color-text-secondary)]">
                      Last Issued
                    </th>
                    <th className="text-left px-4 py-3 text-xs font-medium uppercase tracking-wider text-[var(--color-text-secondary)]">
                      Status
                    </th>
                    <th className="text-right px-4 py-3 text-xs font-medium uppercase tracking-wider text-[var(--color-text-secondary)]">
                      Actions
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {loading ? (
                    <tr>
                      <td colSpan={7} className="px-4 py-12 text-center">
                        <RefreshCw className="w-6 h-6 animate-spin mx-auto mb-2 text-[var(--color-text-tertiary)]" />
                        <p className="text-sm text-[var(--color-text-secondary)]">Loading configurations...</p>
                      </td>
                    </tr>
                  ) : profiles.length === 0 ? (
                    <tr>
                      <td colSpan={7} className="px-4 py-12 text-center">
                        <div className="mb-3 flex justify-center text-[var(--color-text-tertiary)]">
                          <Hash className="w-12 h-12" />
                        </div>
                        <p className="text-[var(--color-text-primary)] font-medium mb-1">No configurations found</p>
                        <p className="text-sm text-[var(--color-text-secondary)] mb-4">
                          Get started by creating your first autonumbering configuration
                        </p>
                        <Button className="rounded-sm">
                          <Plus className="w-4 h-4 mr-2" />
                          New Configuration
                        </Button>
                      </td>
                    </tr>
                  ) : (
                    profiles.map((profile) => (
                      <tr
                        key={profile.id}
                        className="border-b border-[var(--color-border-light)] hover:bg-[var(--color-surface-inset)] transition-colors"
                      >
                        <td className="px-4 py-3">
                          <div className="flex items-center gap-3">
                            <div className="w-10 h-10 rounded-md bg-[var(--color-brand-primary-light)] flex items-center justify-center">
                              <Hash className="w-5 h-5 text-[var(--color-brand-primary)]" />
                            </div>
                            <div>
                              <p className="font-medium text-[var(--color-text-primary)]">{profile.entityType}</p>
                              <p className="text-xs text-[var(--color-text-tertiary)] font-mono">
                                {profile.prefixTemplate || 'No prefix'}
                              </p>
                            </div>
                          </div>
                        </td>
                        <td className="px-4 py-3">
                          <span className="text-sm text-[var(--color-text-primary)]">{profile.strategy}</span>
                        </td>
                        <td className="px-4 py-3">
                          <span className="text-sm text-[var(--color-text-primary)]">{profile.resetPolicy}</span>
                        </td>
                        <td className="px-4 py-3">
                          <span className="text-sm text-[var(--color-text-primary)]">{formatRange(profile)}</span>
                        </td>
                        <td className="px-4 py-3">
                          <span className="text-sm text-[var(--color-text-secondary)] font-mono">
                            {formatLastIssued(profile)}
                          </span>
                        </td>
                        <td className="px-4 py-3">
                          <Badge variant={profile.isActive ? 'secondary' : 'outline'}>
                            {profile.isActive ? 'Active' : 'Paused'}
                          </Badge>
                        </td>
                        <td className="px-4 py-3 text-right">
                          <Button
                            variant="ghost"
                            size="sm"
                            className="rounded-sm"
                            onClick={() => handleEdit(profile)}
                          >
                            <Pencil className="w-4 h-4 mr-2" />
                            Edit
                          </Button>
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Test Reference Dialog */}
      <TestReferenceDialog
        isOpen={testDialogOpen}
        onClose={() => setTestDialogOpen(false)}
        onTest={handleTest}
      />

      {/* Edit Profile Dialog */}
      <EditProfileDialog
        profile={editingProfile}
        isOpen={editDialogOpen}
        onClose={() => {
          setEditDialogOpen(false);
          setEditingProfile(null);
        }}
        onSave={handleSave}
      />
    </div>
  );
}
