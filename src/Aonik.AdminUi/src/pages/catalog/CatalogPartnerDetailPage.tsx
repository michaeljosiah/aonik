import { useCallback, useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { toast } from 'sonner';

import {
  AlertCircle,
  ArrowLeft,
  Building2,
  Cable,
  Clock3,
  Link2,
  MapPin,
  Network,
  Pencil,
  RefreshCw,
  Route,
  Save,
  Store,
} from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Breadcrumb } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';

import { partnerService } from '@/services/partnerService';
import type { PartnerDetail, UpdatePartnerRequest } from '@/types/partners';

const statusStyles: Record<string, { text: string; bg: string }> = {
  Active: { text: 'text-[var(--color-success)]', bg: 'bg-[var(--color-success-light)]' },
  Pending: { text: 'text-[var(--color-warning)]', bg: 'bg-[var(--color-warning-light)]' },
  Suspended: { text: 'text-[var(--color-error)]', bg: 'bg-[var(--color-error-light)]' },
  Inactive: { text: 'text-[var(--color-text-tertiary)]', bg: 'bg-[var(--color-surface-inset)]' },
};

const statusOptions = [
  { value: 'Active', label: 'Active' },
  { value: 'Pending', label: 'Pending' },
  { value: 'Suspended', label: 'Suspended' },
  { value: 'Inactive', label: 'Inactive' },
];

function DetailRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-start justify-between gap-4 py-2">
      <span className="text-xs text-[var(--color-text-tertiary)]">{label}</span>
      <span className="text-right text-sm text-[var(--color-text-primary)]">{value}</span>
    </div>
  );
}

const formatDate = (value?: string | null) => {
  if (!value) {
    return '—';
  }

  return new Date(value).toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });
};

const formatDateTime = (value?: string | null) => {
  if (!value) {
    return '—';
  }

  return new Date(value).toLocaleString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
};

const parseCapabilities = (value?: string | null) => {
  if (!value) {
    return [] as string[];
  }

  try {
    const parsed = JSON.parse(value) as unknown;
    if (Array.isArray(parsed)) {
      return parsed.map((item) => String(item)).filter(Boolean);
    }

    return [] as string[];
  } catch {
    return [] as string[];
  }
};

const parseOperatingHoursSummary = (value?: string | null) => {
  if (!value) {
    return '';
  }

  try {
    const parsed = JSON.parse(value) as { summary?: unknown } | string;
    if (typeof parsed === 'string') {
      return parsed;
    }
    if (parsed && typeof parsed === 'object' && 'summary' in parsed && typeof parsed.summary === 'string') {
      return parsed.summary;
    }
  } catch {
    return value;
  }

  return '';
};

const toCapabilitiesJson = (value: string) => {
  const capabilities = value
    .split(',')
    .map((item) => item.trim())
    .filter(Boolean);
  return JSON.stringify(capabilities);
};

const toOperatingHoursJson = (value: string) => {
  const summary = value.trim();
  if (!summary) {
    return JSON.stringify({});
  }
  return JSON.stringify({ summary });
};

export function CatalogPartnerDetailPage() {
  const navigate = useNavigate();
  const { partnerId } = useParams<{ partnerId: string }>();

  const [partner, setPartner] = useState<PartnerDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState('overview');
  const [isEditing, setIsEditing] = useState(false);
  const [saving, setSaving] = useState(false);

  const [name, setName] = useState('');
  const [status, setStatus] = useState('Active');
  const [capabilitiesText, setCapabilitiesText] = useState('');
  const [operatingHoursText, setOperatingHoursText] = useState('');

  const hydrateForm = useCallback((data: PartnerDetail) => {
    setName(data.name);
    setStatus(data.status || 'Active');
    setCapabilitiesText(parseCapabilities(data.capabilitiesJson).join(', '));
    setOperatingHoursText(parseOperatingHoursSummary(data.operatingHoursJson));
  }, []);

  const loadPartner = useCallback(async () => {
    if (!partnerId) {
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const result = await partnerService.get(partnerId);
      setPartner(result);
      hydrateForm(result);
    } catch (err: unknown) {
      console.error('Failed to load partner:', err);
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(message || 'Failed to load partner details.');
      setPartner(null);
    } finally {
      setLoading(false);
    }
  }, [hydrateForm, partnerId]);

  useEffect(() => {
    void loadPartner();
  }, [loadPartner]);

  const handleCancelEdit = () => {
    if (partner) {
      hydrateForm(partner);
    }
    setIsEditing(false);
  };

  const handleSave = async () => {
    if (!partnerId || !partner) {
      return;
    }

    const request: UpdatePartnerRequest = {
      name: name.trim(),
      status,
      capabilitiesJson: toCapabilitiesJson(capabilitiesText),
      operatingHoursJson: toOperatingHoursJson(operatingHoursText),
    };

    setSaving(true);
    setError(null);

    try {
      const updated = await partnerService.update(partnerId, request);
      setPartner(updated);
      hydrateForm(updated);
      setIsEditing(false);
      toast.success('Partner updated.');
    } catch (err: unknown) {
      console.error('Failed to update partner:', err);
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(message || 'Failed to update partner.');
      toast.error(message || 'Failed to update partner.');
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <div className="flex flex-1 items-center justify-center">
        <div className="text-center">
          <RefreshCw className="mx-auto mb-3 h-8 w-8 animate-spin text-[var(--color-brand-primary)]" />
          <p className="text-[var(--color-text-secondary)]">Loading partner...</p>
        </div>
      </div>
    );
  }

  if (!partner) {
    return (
      <div className="flex flex-1 items-center justify-center">
        <div className="text-center">
          <AlertCircle className="mx-auto mb-3 h-12 w-12 text-[var(--color-error)]" />
          <h2 className="mb-2 text-xl font-semibold text-[var(--color-text-primary)]">Partner Not Found</h2>
          <p className="mb-4 text-[var(--color-text-secondary)]">
            The partner might have been deleted, or you no longer have access.
          </p>
          <Button onClick={() => navigate('/catalog/partners')}>
            <ArrowLeft className="mr-2 h-4 w-4" />
            Back to Partners
          </Button>
        </div>
      </div>
    );
  }

  const statusStyle =
    statusStyles[partner.status] ??
    ({ text: 'text-[var(--color-text-secondary)]', bg: 'bg-[var(--color-surface-inset)]' } as const);

  const branches = partner.branches ?? [];
  const connectors = partner.connectors ?? [];
  const routingRules = partner.routingRules ?? [];
  const transmissions = partner.recentTransmissions ?? [];
  const linkedBillers = partner.linkedBillers ?? [];

  const countriesCovered = new Set(branches.map((branch) => branch.country).filter(Boolean)).size;

  const activeRoutes = partner.activeRoutingRuleCount ?? routingRules.filter((rule) => rule.isActive).length;
  const connectorCount = partner.connectorCount ?? connectors.length;
  const branchCount = partner.branchCount ?? branches.length;
  const linkedBillerCount = partner.linkedBillerCount ?? linkedBillers.length;

  const breadcrumbItems = [
    { label: 'Catalog', href: '/catalog' },
    { label: 'Partners', href: '/catalog/partners', icon: <Network className="h-3.5 w-3.5" /> },
    { label: partner.name, icon: <Building2 className="h-3.5 w-3.5" /> },
  ];

  return (
    <div className="h-full overflow-auto bg-[var(--color-background)]">
      <div className="flex items-center justify-between border-b border-[var(--color-border-light)] bg-[var(--color-surface)] px-6 py-4">
        <div>
          <h1 className="text-lg font-semibold text-[var(--color-text-primary)]">Partner Details</h1>
          <Breadcrumb items={breadcrumbItems} className="mt-1" />
        </div>

        <div className="flex items-center gap-2">
          <Button variant="outline" size="sm" onClick={loadPartner}>
            <RefreshCw className="mr-2 h-4 w-4" />
            Refresh
          </Button>

          {!isEditing ? (
            <Button size="sm" onClick={() => setIsEditing(true)}>
              <Pencil className="mr-2 h-4 w-4" />
              Edit
            </Button>
          ) : (
            <>
              <Button variant="outline" size="sm" onClick={handleCancelEdit} disabled={saving}>
                Cancel
              </Button>
              <Button size="sm" onClick={handleSave} disabled={saving || name.trim().length < 2}>
                {saving ? (
                  <>
                    <RefreshCw className="mr-2 h-4 w-4 animate-spin" />
                    Saving...
                  </>
                ) : (
                  <>
                    <Save className="mr-2 h-4 w-4" />
                    Save
                  </>
                )}
              </Button>
            </>
          )}
        </div>
      </div>

      {error && (
        <div className="px-6 pt-4">
          <Card className="border-[var(--color-error)] bg-[var(--color-error-light)]">
            <CardContent className="flex items-center gap-3 p-4 text-[var(--color-error)]">
              <AlertCircle className="h-5 w-5" />
              <span className="flex-1">{error}</span>
              <Button variant="ghost" size="sm" onClick={loadPartner}>
                Retry
              </Button>
            </CardContent>
          </Card>
        </div>
      )}

      <div className="p-6">
        <div className="flex flex-col gap-6 xl:flex-row">
          <div className="w-full flex-shrink-0 space-y-6 xl:w-80">
            <Card>
              <CardContent className="p-6">
                <div className="mb-6 text-center">
                  <div className="mx-auto mb-3 flex h-20 w-20 items-center justify-center rounded-full bg-[var(--color-brand-primary-light)]">
                    <Building2 className="h-10 w-10 text-[var(--color-brand-primary)]" />
                  </div>
                  <h2 className="text-lg font-semibold text-[var(--color-text-primary)]">{partner.name}</h2>
                  <p className="text-sm text-[var(--color-text-tertiary)]">Partner ID {partner.partnerId.slice(0, 8)}</p>
                  <div className="mt-3 flex flex-wrap items-center justify-center gap-2">
                    <Badge className={`${statusStyle.bg} ${statusStyle.text} text-xs`}>{partner.status}</Badge>
                    <Badge variant="outline" className="text-xs">
                      {countriesCovered} markets
                    </Badge>
                  </div>
                </div>

                <div className="space-y-3 border-t border-[var(--color-border-light)] pt-4">
                  <div className="flex items-center gap-2 text-sm text-[var(--color-text-secondary)]">
                    <MapPin className="h-4 w-4 text-[var(--color-text-tertiary)]" />
                    <span>{countriesCovered} countries covered</span>
                  </div>
                  <div className="flex items-center gap-2 text-sm text-[var(--color-text-secondary)]">
                    <Cable className="h-4 w-4 text-[var(--color-text-tertiary)]" />
                    <span>{connectorCount} active connectors</span>
                  </div>
                  <div className="flex items-center gap-2 text-sm text-[var(--color-text-secondary)]">
                    <Route className="h-4 w-4 text-[var(--color-text-tertiary)]" />
                    <span>{activeRoutes} routing rules</span>
                  </div>
                </div>

                <div className="mt-6 grid grid-cols-2 gap-3">
                  <div className="rounded-lg border border-[var(--color-border-light)] p-3">
                    <p className="text-xs text-[var(--color-text-tertiary)]">Branches</p>
                    <p className="text-lg font-semibold text-[var(--color-text-primary)]">{branchCount}</p>
                  </div>
                  <div className="rounded-lg border border-[var(--color-border-light)] p-3">
                    <p className="text-xs text-[var(--color-text-tertiary)]">Linked billers</p>
                    <p className="text-lg font-semibold text-[var(--color-text-primary)]">{linkedBillerCount}</p>
                  </div>
                  <div className="col-span-2 rounded-lg border border-[var(--color-border-light)] p-3">
                    <p className="text-xs text-[var(--color-text-tertiary)]">Last updated</p>
                    <p className="text-sm font-medium text-[var(--color-text-primary)]">
                      {formatDateTime(partner.updatedAt ?? partner.createdAt)}
                    </p>
                  </div>
                </div>
              </CardContent>
            </Card>
          </div>

          <div className="min-w-0 flex-1">
            <Card>
              <CardContent className="p-0">
                <Tabs value={activeTab} onValueChange={setActiveTab}>
                  <div className="border-b border-[var(--color-border-light)] px-4">
                    <TabsList className="h-auto flex-wrap gap-0 bg-transparent p-0">
                      {[ 
                        { value: 'overview', label: 'Overview' },
                        { value: 'coverage', label: 'Coverage' },
                        { value: 'connectivity', label: 'Connectivity' },
                        { value: 'billers', label: 'Billers' },
                        { value: 'activity', label: 'Activity' },
                      ].map((tab) => (
                        <TabsTrigger
                          key={tab.value}
                          value={tab.value}
                          className="rounded-none border-b-2 border-transparent px-4 py-3 text-sm data-[state=active]:border-[var(--color-brand-primary)] data-[state=active]:bg-transparent data-[state=active]:text-[var(--color-brand-primary)]"
                        >
                          {tab.label}
                        </TabsTrigger>
                      ))}
                    </TabsList>
                  </div>

                  <div className="p-6">
                    <TabsContent value="overview" className="mt-0">
                      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
                        <Card>
                          <CardHeader>
                            <CardTitle className="text-sm">Partner Profile</CardTitle>
                          </CardHeader>
                          <CardContent className="space-y-3 text-sm">
                            {isEditing ? (
                              <div className="space-y-4">
                                <div className="space-y-2">
                                  <Label htmlFor="partner-name">Partner name</Label>
                                  <Input
                                    id="partner-name"
                                    value={name}
                                    onChange={(event) => setName(event.target.value)}
                                  />
                                </div>
                                <div className="space-y-2">
                                  <Label htmlFor="partner-status">Status</Label>
                                  <Select value={status} onValueChange={setStatus}>
                                    <SelectTrigger id="partner-status" aria-label="Partner status">
                                      <SelectValue placeholder="Select status" />
                                    </SelectTrigger>
                                    <SelectContent>
                                      {statusOptions.map((option) => (
                                        <SelectItem key={option.value} value={option.value}>
                                          {option.label}
                                        </SelectItem>
                                      ))}
                                    </SelectContent>
                                  </Select>
                                </div>
                                <div className="space-y-2">
                                  <Label htmlFor="partner-capabilities">Capabilities</Label>
                                  <Input
                                    id="partner-capabilities"
                                    value={capabilitiesText}
                                    onChange={(event) => setCapabilitiesText(event.target.value)}
                                    placeholder="BillPay, Collections"
                                  />
                                </div>
                                <div className="space-y-2">
                                  <Label htmlFor="partner-hours">Operating hours</Label>
                                  <Textarea
                                    id="partner-hours"
                                    value={operatingHoursText}
                                    onChange={(event) => setOperatingHoursText(event.target.value)}
                                    rows={3}
                                  />
                                </div>
                              </div>
                            ) : (
                              <div className="space-y-1">
                                <DetailRow label="Partner name" value={partner.name} />
                                <DetailRow label="Status" value={partner.status} />
                                <DetailRow label="Capabilities" value={capabilitiesText || '—'} />
                                <DetailRow label="Operating hours" value={operatingHoursText || '—'} />
                              </div>
                            )}
                          </CardContent>
                        </Card>

                        <Card>
                          <CardHeader>
                            <CardTitle className="text-sm">Operational Summary</CardTitle>
                          </CardHeader>
                          <CardContent className="space-y-1 text-sm">
                            <DetailRow label="Branches" value={String(branchCount)} />
                            <DetailRow label="Connectors" value={String(connectorCount)} />
                            <DetailRow label="Active routes" value={String(activeRoutes)} />
                            <DetailRow label="Linked billers" value={String(linkedBillerCount)} />
                            <DetailRow label="Created" value={formatDate(partner.createdAt)} />
                            <DetailRow label="Updated" value={formatDate(partner.updatedAt ?? partner.createdAt)} />
                          </CardContent>
                        </Card>
                      </div>
                    </TabsContent>

                    <TabsContent value="coverage" className="mt-0">
                      <Card>
                        <CardHeader>
                          <CardTitle className="text-sm">Branch Coverage</CardTitle>
                        </CardHeader>
                        <CardContent className="space-y-4">
                          {branches.length === 0 ? (
                            <p className="text-sm text-[var(--color-text-tertiary)]">
                              No branches are configured for this partner yet.
                            </p>
                          ) : (
                            branches.map((branch) => (
                              <div
                                key={branch.branchId}
                                className="flex items-start justify-between gap-4 border-b border-[var(--color-border-light)] pb-3 last:border-b-0"
                              >
                                <div className="flex items-start gap-3">
                                  <MapPin className="mt-0.5 h-4 w-4 text-[var(--color-text-tertiary)]" />
                                  <div>
                                    <div className="text-sm font-medium text-[var(--color-text-primary)]">{branch.name}</div>
                                    <div className="text-xs text-[var(--color-text-tertiary)]">
                                      {branch.city}, {branch.country}
                                    </div>
                                    {branch.metadataJson && (
                                      <div className="mt-1 text-xs text-[var(--color-text-tertiary)]">{branch.metadataJson}</div>
                                    )}
                                  </div>
                                </div>
                                <span className="text-xs text-[var(--color-text-tertiary)]">
                                  {formatDate(branch.updatedAt ?? branch.createdAt)}
                                </span>
                              </div>
                            ))
                          )}
                        </CardContent>
                      </Card>
                    </TabsContent>

                    <TabsContent value="connectivity" className="mt-0">
                      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
                        <Card>
                          <CardHeader>
                            <CardTitle className="text-sm">Connectors</CardTitle>
                          </CardHeader>
                          <CardContent className="space-y-4">
                            {connectors.length === 0 ? (
                              <p className="text-sm text-[var(--color-text-tertiary)]">No connectors configured.</p>
                            ) : (
                              connectors.map((connector) => (
                                <div
                                  key={connector.connectorId}
                                  className="flex items-start justify-between gap-4 border-b border-[var(--color-border-light)] pb-3 last:border-b-0"
                                >
                                  <div className="flex items-start gap-3">
                                    <Cable className="mt-0.5 h-4 w-4 text-[var(--color-text-tertiary)]" />
                                    <div>
                                      <div className="text-sm font-medium text-[var(--color-text-primary)]">
                                        {connector.connectorType}
                                      </div>
                                      <div className="text-xs text-[var(--color-text-tertiary)]">
                                        Ref {connector.connectorId.slice(0, 8)}
                                      </div>
                                      {connector.credentialsRef && (
                                        <div className="text-xs text-[var(--color-text-tertiary)]">
                                          Credentials {connector.credentialsRef}
                                        </div>
                                      )}
                                    </div>
                                  </div>
                                  <Badge variant="outline" className="text-xs">
                                    {connector.status}
                                  </Badge>
                                </div>
                              ))
                            )}
                          </CardContent>
                        </Card>

                        <Card>
                          <CardHeader>
                            <CardTitle className="text-sm">Routing Rules</CardTitle>
                          </CardHeader>
                          <CardContent className="space-y-4">
                            {routingRules.length === 0 ? (
                              <p className="text-sm text-[var(--color-text-tertiary)]">No routing rules assigned.</p>
                            ) : (
                              routingRules.map((rule) => (
                                <div
                                  key={rule.routingRuleId}
                                  className="flex items-start justify-between gap-4 border-b border-[var(--color-border-light)] pb-3 last:border-b-0"
                                >
                                  <div className="flex items-start gap-3">
                                    <Route className="mt-0.5 h-4 w-4 text-[var(--color-text-tertiary)]" />
                                    <div>
                                      <div className="text-sm font-medium text-[var(--color-text-primary)]">
                                        Priority {rule.priority}
                                      </div>
                                      <div className="text-xs text-[var(--color-text-tertiary)]">
                                        {rule.conditionsJson || 'No condition payload'}
                                      </div>
                                      {rule.targetConnectorId && (
                                        <div className="text-xs text-[var(--color-text-tertiary)]">
                                          Connector {rule.targetConnectorId.slice(0, 8)}
                                        </div>
                                      )}
                                    </div>
                                  </div>
                                  <Badge variant="outline" className="text-xs">
                                    {rule.isActive ? 'Active' : 'Inactive'}
                                  </Badge>
                                </div>
                              ))
                            )}
                          </CardContent>
                        </Card>
                      </div>
                    </TabsContent>

                    <TabsContent value="billers" className="mt-0">
                      <Card>
                        <CardHeader>
                          <CardTitle className="text-sm">Linked Billers</CardTitle>
                        </CardHeader>
                        <CardContent className="space-y-4">
                          {linkedBillers.length === 0 ? (
                            <p className="text-sm text-[var(--color-text-tertiary)]">
                              No billers are currently mapped to this partner.
                            </p>
                          ) : (
                            linkedBillers.map((biller) => (
                              <div
                                key={biller.billerId}
                                className="flex items-start justify-between gap-4 border-b border-[var(--color-border-light)] pb-3 last:border-b-0"
                              >
                                <div className="flex items-start gap-3">
                                  <Store className="mt-0.5 h-4 w-4 text-[var(--color-text-tertiary)]" />
                                  <div>
                                    <div className="text-sm font-medium text-[var(--color-text-primary)]">{biller.name}</div>
                                    <div className="text-xs text-[var(--color-text-tertiary)]">
                                      {biller.countryCode} · {biller.serviceCount} services
                                    </div>
                                  </div>
                                </div>

                                <div className="flex items-center gap-2">
                                  <Badge variant="outline" className="text-xs">
                                    {biller.isActive ? 'Active' : 'Inactive'}
                                  </Badge>
                                  <Button
                                    variant="ghost"
                                    size="sm"
                                    onClick={() => navigate(`/catalog/billers/${biller.billerId}`)}
                                  >
                                    View
                                  </Button>
                                </div>
                              </div>
                            ))
                          )}
                        </CardContent>
                      </Card>
                    </TabsContent>

                    <TabsContent value="activity" className="mt-0">
                      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
                        <Card>
                          <CardHeader>
                            <CardTitle className="text-sm">Recent Transmissions</CardTitle>
                          </CardHeader>
                          <CardContent className="space-y-4">
                            {transmissions.length === 0 ? (
                              <p className="text-sm text-[var(--color-text-tertiary)]">No transmissions recorded yet.</p>
                            ) : (
                              transmissions.map((transmission) => (
                                <div
                                  key={transmission.transmissionId}
                                  className="flex items-start justify-between gap-4 border-b border-[var(--color-border-light)] pb-3 last:border-b-0"
                                >
                                  <div className="flex items-start gap-3">
                                    <Link2 className="mt-0.5 h-4 w-4 text-[var(--color-text-tertiary)]" />
                                    <div>
                                      <div className="text-sm font-medium text-[var(--color-text-primary)]">
                                        {transmission.connectorType || 'Connector'} · {transmission.status}
                                      </div>
                                      <div className="text-xs text-[var(--color-text-tertiary)]">
                                        Retries {transmission.retryCount}
                                      </div>
                                      {transmission.lastError && (
                                        <div className="text-xs text-[var(--color-error)]">{transmission.lastError}</div>
                                      )}
                                    </div>
                                  </div>
                                  <span className="text-xs text-[var(--color-text-tertiary)]">
                                    {formatDateTime(transmission.createdAt)}
                                  </span>
                                </div>
                              ))
                            )}
                          </CardContent>
                        </Card>

                        <Card>
                          <CardHeader>
                            <CardTitle className="text-sm">Metadata</CardTitle>
                          </CardHeader>
                          <CardContent className="space-y-1 text-sm">
                            <DetailRow label="Created" value={formatDateTime(partner.createdAt)} />
                            <DetailRow label="Updated" value={formatDateTime(partner.updatedAt ?? partner.createdAt)} />
                            <DetailRow label="Partner reference" value={partner.partnerId} />
                            <DetailRow label="Status" value={partner.status} />
                            <div className="mt-4 flex items-center gap-2 rounded-lg border border-[var(--color-border-light)] p-3">
                              <Clock3 className="h-4 w-4 text-[var(--color-text-tertiary)]" />
                              <span className="text-xs text-[var(--color-text-secondary)]">
                                Operational events and audits will expand here as partner telemetry is integrated.
                              </span>
                            </div>
                          </CardContent>
                        </Card>
                      </div>
                    </TabsContent>
                  </div>
                </Tabs>
              </CardContent>
            </Card>
          </div>
        </div>
      </div>
    </div>
  );
}
