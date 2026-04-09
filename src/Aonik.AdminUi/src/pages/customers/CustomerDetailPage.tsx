import { useCallback, useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';

import {
  AlertCircle,
  Download,
  FileText,
  Lightbulb,
  Mail,
  MapPin,
  Phone,
  RefreshCw,
  ShieldCheck,
  User,
  Users,
  UsersRound,
  Wallet,
} from 'lucide-react';

import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';

import { customerService } from '@/services/customerService';
import type { CustomerInsightsResponse } from '@/services/customerService';
import { documentService } from '@/services/documentService';
import { AccountsSubTab } from './finance/AccountsSubTab';
import { BudgetsSubTab } from './finance/BudgetsSubTab';
import { CommitmentsSubTab } from './finance/CommitmentsSubTab';
import { TransactionsSubTab } from './finance/TransactionsSubTab';
import type { CurrencyAmount, CustomerDetail, CustomerStats, DocumentListItem } from '@/types';

const statusStyles: Record<string, { text: string; bg: string }> = {
  Active: { text: 'text-[var(--color-success)]', bg: 'bg-[var(--color-success-light)]' },
  Pending: { text: 'text-[var(--color-warning)]', bg: 'bg-[var(--color-warning-light)]' },
  Suspended: { text: 'text-[var(--color-error)]', bg: 'bg-[var(--color-error-light)]' },
  Inactive: { text: 'text-[var(--color-text-tertiary)]', bg: 'bg-[var(--color-surface-inset)]' },
};


function DetailRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-start justify-between gap-4 py-2">
      <span className="text-xs text-[var(--color-text-tertiary)]">{label}</span>
      <span className="text-sm text-[var(--color-text-primary)] text-right">{value}</span>
    </div>
  );
}

export function CustomerDetailPage() {
  const navigate = useNavigate();
  const { partyId } = useParams<{ partyId: string }>();

  const [customer, setCustomer] = useState<CustomerDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState('overview');
  const [financeSubTab, setFinanceSubTab] = useState<'accounts' | 'transactions' | 'budgets' | 'commitments'>('accounts');
  const [stats, setStats] = useState<CustomerStats | null>(null);
  const [statsLoading, setStatsLoading] = useState(false);
  const [documents, setDocuments] = useState<DocumentListItem[]>([]);
  const [documentsLoading, setDocumentsLoading] = useState(false);
  const [documentsError, setDocumentsError] = useState<string | null>(null);
  const [insights, setInsights] = useState<CustomerInsightsResponse | null>(null);
  const [insightsLoading, setInsightsLoading] = useState(false);
  const [insightsError, setInsightsError] = useState<string | null>(null);

  const loadCustomer = useCallback(async () => {
    if (!partyId) return;

    setLoading(true);
    setError(null);
    try {
      const data = await customerService.get(partyId);
      setCustomer(data);
    } catch (err: unknown) {
      console.error('Failed to load customer:', err);
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(message || 'Failed to load customer. Please try again.');
    } finally {
      setLoading(false);
    }
  }, [partyId]);

  const loadStats = useCallback(async () => {
    if (!partyId) return;

    setStatsLoading(true);
    try {
      const data = await customerService.getStats(partyId);
      setStats(data);
    } catch (err: unknown) {
      console.error('Failed to load customer stats:', err);
      setStats(null);
    } finally {
      setStatsLoading(false);
    }
  }, [partyId]);

  const loadDocuments = useCallback(async () => {
    if (!partyId) return;

    setDocumentsLoading(true);
    setDocumentsError(null);
    try {
      const result = await documentService.list({
        ownerPartyId: partyId,
        pageNumber: 1,
        pageSize: 5,
      });
      setDocuments(result.items);
    } catch (err: unknown) {
      console.error('Failed to load customer documents:', err);
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setDocumentsError(message || 'Failed to load documents.');
    } finally {
      setDocumentsLoading(false);
    }
  }, [partyId]);

  const loadInsights = useCallback(async () => {
    if (!partyId) return;

    setInsightsLoading(true);
    setInsightsError(null);
    try {
      const result = await customerService.listInsights(partyId);
      setInsights(result);
    } catch (err: unknown) {
      console.error('Failed to load customer insights:', err);
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setInsightsError(message || 'Failed to load insights.');
    } finally {
      setInsightsLoading(false);
    }
  }, [partyId]);

  useEffect(() => {
    loadCustomer();
    loadStats();
  }, [loadCustomer, loadStats]);

  useEffect(() => {
    if (activeTab === 'documents') {
      loadDocuments();
    }
    if (activeTab === 'insights') {
      loadInsights();
    }
  }, [activeTab, loadDocuments, loadInsights]);

  const formatDate = (dateString?: string | null) => {
    if (!dateString) return '—';
    return new Date(dateString).toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
    });
  };

  const formatDateTime = (dateString?: string | null) => {
    if (!dateString) return '—';
    return new Date(dateString).toLocaleString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  };

  const formatDateShort = (dateString?: string | null) => {
    if (!dateString) return '—';
    return new Date(dateString).toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
    });
  };

  const formatCurrencyValue = (amount: number, currency: string) => {
    try {
      return new Intl.NumberFormat('en-US', {
        style: 'currency',
        currency,
        maximumFractionDigits: 2,
      }).format(amount);
    } catch {
      return `${amount.toLocaleString('en-US')} ${currency}`;
    }
  };

  const formatCurrencySummary = (amounts?: CurrencyAmount[] | null) => {
    if (!amounts || amounts.length === 0) return '—';
    if (amounts.length === 1) {
      const entry = amounts[0];
      return formatCurrencyValue(entry.amount, entry.currency);
    }
    return `Multiple (${amounts.length})`;
  };

  const getInitials = (name?: string | null, email?: string | null) => {
    if (name) {
      return name
        .split(' ')
        .filter(Boolean)
        .map((part) => part[0])
        .join('')
        .toUpperCase();
    }
    return email?.charAt(0).toUpperCase() || 'C';
  };

  const getPhotoUrl = (size: 'original' | 'medium' | 'small' | 'tiny' = 'small') => {
    if (!customer?.personProfile) return null;

    let photoUrl: string | null | undefined;
    switch (size) {
      case 'original':
        photoUrl = customer.personProfile.photoUrl;
        break;
      case 'medium':
        photoUrl = customer.personProfile.photoUrlMedium || customer.personProfile.photoUrl;
        break;
      case 'small':
        photoUrl = customer.personProfile.photoUrlSmall || customer.personProfile.photoUrl;
        break;
      case 'tiny':
        photoUrl = customer.personProfile.photoUrlTiny || customer.personProfile.photoUrl;
        break;
    }

    if (!photoUrl) return null;

    if (photoUrl.startsWith('http')) {
      return photoUrl;
    }

    const apiBaseUrl = import.meta.env.VITE_API_URL || 'https://localhost:5001';
    return `${apiBaseUrl}${photoUrl}`;
  };

  const breadcrumbItems = [
    { label: 'Customers', href: '/customers', icon: <UsersRound className="w-3.5 h-3.5" /> },
    { label: 'Customer', icon: <User className="w-3.5 h-3.5" /> },
  ];

  if (loading) {
    return (
      <div className="flex-1 flex items-center justify-center">
        <div className="text-center">
          <RefreshCw className="w-8 h-8 animate-spin mx-auto mb-3 text-[var(--color-brand-primary)]" />
          <p className="text-[var(--color-text-secondary)]">Loading customer...</p>
        </div>
      </div>
    );
  }

  if (!customer) {
    return (
      <div className="flex-1 flex items-center justify-center">
        <div className="text-center">
          <AlertCircle className="w-12 h-12 mx-auto mb-3 text-[var(--color-error)]" />
          <h2 className="text-xl font-semibold text-[var(--color-text-primary)] mb-2">Customer Not Found</h2>
          <p className="text-[var(--color-text-secondary)] mb-4">
            The customer you're looking for doesn't exist or you don't have access.
          </p>
          <Button onClick={() => navigate('/customers')}>Back to Customers</Button>
        </div>
      </div>
    );
  }

  const contacts = customer.contacts ?? [];
  const addresses = customer.addresses ?? [];
  const consents = customer.consents ?? [];
  const externalAccounts = customer.externalAccounts ?? [];
  const relationships = customer.relationships ?? [];
  const roleAssignments = customer.roleAssignments ?? [];

  const primaryEmail = contacts.find((c) => c.type === 'Email' && c.isPrimary)?.value;
  const primaryPhone = contacts.find((c) => c.type === 'Phone' && c.isPrimary)?.value;

  const verificationStatus =
    customer.partyType === 'Business'
      ? customer.businessProfile?.kybStatus
      : customer.personProfile?.idvStatus;

  const statusStyle =
    statusStyles[customer.status] ??
    ({ text: 'text-[var(--color-text-secondary)]', bg: 'bg-[var(--color-surface-inset)]' } as const);

  const profileSubtitle =
    customer.partyType === 'Business'
      ? customer.businessProfile?.industry || 'Business customer'
      : customer.personProfile?.occupation || 'Individual customer';

  const lastActivityAt = stats?.lastActivityAt || customer.updatedAt || customer.createdAt;
  const totalOrders = stats?.totalOrders;
  const totalPaidSummary = formatCurrencySummary(stats?.totalPaidByCurrency);
  const outstandingSummary = formatCurrencySummary(stats?.outstandingByCurrency);
  const primaryAddress = addresses[0];

  return (
    <div className="h-full overflow-auto bg-[var(--color-background)]">
      <div className="px-6 py-4 flex items-center justify-between border-b border-[var(--color-border-light)] bg-[var(--color-surface)]">
        <div>
          <h1 className="text-lg font-semibold text-[var(--color-text-primary)]">Customer Details</h1>
          <Breadcrumb items={breadcrumbItems} className="mt-1" />
        </div>
        <div className="flex items-center gap-2">
          <Button
            variant="outline"
            size="sm"
            onClick={async () => {
              try {
                await customerService.exportData(partyId!);
              } catch {
                /* toast or ignore */
              }
            }}
          >
            <Download className="w-4 h-4 mr-1.5" />
            Export Data
          </Button>
          <Button variant="outline" size="sm" onClick={loadCustomer}>
            Refresh
          </Button>
        </div>
      </div>

      {error && (
        <div className="px-6 pt-4">
          <Card className="border-[var(--color-error)] bg-[var(--color-error-light)]">
            <CardContent className="p-4 flex items-center gap-3 text-[var(--color-error)]">
              <AlertCircle className="w-5 h-5" />
              <span className="flex-1">{error}</span>
              <Button variant="ghost" size="sm" onClick={loadCustomer}>
                Retry
              </Button>
            </CardContent>
          </Card>
        </div>
      )}

      <div className="p-6">
        <div className="flex flex-col xl:flex-row gap-6">
          <div className="w-full xl:w-80 flex-shrink-0 space-y-6">
            <Card>
              <CardContent className="p-6">
                <div className="text-center mb-6">
                  <Avatar className="h-20 w-20 mx-auto mb-3">
                    {getPhotoUrl('small') && (
                      <AvatarImage src={getPhotoUrl('small')!} alt={customer.displayName} />
                    )}
                    <AvatarFallback className="text-lg">
                      {getInitials(customer.displayName, primaryEmail)}
                    </AvatarFallback>
                  </Avatar>
                  <h2 className="text-lg font-semibold text-[var(--color-text-primary)]">
                    {customer.displayName}
                  </h2>
                  <p className="text-sm text-[var(--color-text-tertiary)]">{profileSubtitle}</p>
                  <div className="mt-3 flex items-center justify-center gap-2 flex-wrap">
                    <Badge className={`${statusStyle.bg} ${statusStyle.text} text-xs`}>{customer.status}</Badge>
                    <Badge variant="outline" className="text-xs">
                      {customer.partyType}
                    </Badge>
                    {customer.customerTierCode && (
                      <Badge variant="outline" className="text-xs">
                        Tier {customer.customerTierCode}
                      </Badge>
                    )}
                  </div>
                </div>

                <div className="space-y-3 border-t border-[var(--color-border-light)] pt-4">
                  <div className="flex items-center gap-2 text-sm text-[var(--color-text-secondary)]">
                    <Mail className="w-4 h-4 text-[var(--color-text-tertiary)]" />
                    <span>{primaryEmail || 'No primary email'}</span>
                  </div>
                  <div className="flex items-center gap-2 text-sm text-[var(--color-text-secondary)]">
                    <Phone className="w-4 h-4 text-[var(--color-text-tertiary)]" />
                    <span>{primaryPhone || 'No primary phone'}</span>
                  </div>
                </div>

                <div className="mt-6">
                  <div className="flex items-center justify-between mb-3">
                    <span className="text-sm font-medium text-[var(--color-text-primary)]">Live stats</span>
                    <Badge className="bg-[var(--color-info-light)] text-[var(--color-info)] text-xs">Live</Badge>
                  </div>
                  <div className="grid grid-cols-2 gap-3">
                    <div className="rounded-lg border border-[var(--color-border-light)] p-3">
                      <p className="text-xs text-[var(--color-text-tertiary)]">Total orders</p>
                      <p className="text-lg font-semibold text-[var(--color-text-primary)]">
                        {statsLoading && totalOrders === undefined ? 'Loading...' : totalOrders ?? '—'}
                      </p>
                    </div>
                    <div className="rounded-lg border border-[var(--color-border-light)] p-3">
                      <p className="text-xs text-[var(--color-text-tertiary)]">Total paid</p>
                      <p className="text-lg font-semibold text-[var(--color-text-primary)]">
                        {statsLoading && !stats ? 'Loading...' : totalPaidSummary}
                      </p>
                    </div>
                    <div className="rounded-lg border border-[var(--color-border-light)] p-3">
                      <p className="text-xs text-[var(--color-text-tertiary)]">Outstanding</p>
                      <p className="text-lg font-semibold text-[var(--color-text-primary)]">
                        {statsLoading && !stats ? 'Loading...' : outstandingSummary}
                      </p>
                    </div>
                    <div className="rounded-lg border border-[var(--color-border-light)] p-3">
                      <p className="text-xs text-[var(--color-text-tertiary)]">Accounts linked</p>
                      <p className="text-lg font-semibold text-[var(--color-text-primary)]">
                        {externalAccounts.length}
                      </p>
                    </div>
                    <div className="col-span-2 rounded-lg border border-[var(--color-border-light)] p-3">
                      <p className="text-xs text-[var(--color-text-tertiary)]">Last activity</p>
                      <p className="text-sm font-medium text-[var(--color-text-primary)]">
                        {formatDateTime(lastActivityAt)}
                      </p>
                    </div>
                  </div>
                </div>
              </CardContent>
            </Card>
          </div>

          <div className="flex-1 min-w-0">
            <Card>
              <CardContent className="p-0">
                <Tabs value={activeTab} onValueChange={setActiveTab}>
                  <div className="flex items-center justify-between border-b border-[var(--color-border-light)] px-4">
                    <TabsList className="bg-transparent p-0 h-auto flex flex-wrap gap-0">
                      {[
                        { value: 'overview', label: 'Overview' },
                        { value: 'finance', label: 'Finance' },
                        { value: 'contacts', label: 'Contacts' },
                        { value: 'addresses', label: 'Addresses' },
                        { value: 'accounts', label: 'Accounts' },
                        { value: 'relationships', label: 'Relationships' },
                        { value: 'consents', label: 'Consents' },
                        { value: 'insights', label: 'Insights' },
                        { value: 'documents', label: 'Documents' },
                      ].map((tab) => (
                        <TabsTrigger
                          key={tab.value}
                          value={tab.value}
                          className="px-4 py-3 text-sm rounded-none border-b-2 border-transparent data-[state=active]:border-[var(--color-brand-primary)] data-[state=active]:bg-transparent data-[state=active]:text-[var(--color-brand-primary)]"
                        >
                          {tab.label}
                        </TabsTrigger>
                      ))}
                    </TabsList>
                  </div>

                  <div className="p-6">
                    <TabsContent value="finance" className="mt-0">
                      {!customer?.userId ? (
                        <div className="flex flex-col items-center justify-center py-12 text-center">
                          <p className="text-sm text-[var(--color-text-tertiary)]">
                            No user account linked to this customer.
                          </p>
                        </div>
                      ) : (
                        <>
                          {/* Sub-tab navigation */}
                          <div className="flex items-center gap-1 border-b border-[var(--color-border-light)] -mx-6 px-6 mb-5">
                            {(['accounts', 'transactions', 'budgets', 'commitments'] as const).map((sub) => {
                              const labels: Record<string, string> = {
                                accounts: 'Accounts',
                                transactions: 'Transactions',
                                budgets: 'Budgets',
                                commitments: 'Commitments',
                              };
                              return (
                                <button
                                  key={sub}
                                  type="button"
                                  onClick={() => setFinanceSubTab(sub)}
                                  className={`pb-2.5 px-1 mr-4 text-sm border-b-2 transition-colors ${
                                    financeSubTab === sub
                                      ? 'border-[var(--color-brand-primary)] text-[var(--color-brand-primary)] font-medium'
                                      : 'border-transparent text-[var(--color-text-tertiary)] hover:text-[var(--color-text-primary)]'
                                  }`}
                                >
                                  {labels[sub]}
                                </button>
                              );
                            })}
                          </div>

                          {/* Sub-tab content */}
                          {financeSubTab === 'accounts' && (
                            <AccountsSubTab key="accounts" userId={customer.userId} />
                          )}
                          {financeSubTab === 'transactions' && (
                            <TransactionsSubTab key="transactions" userId={customer.userId} />
                          )}
                          {financeSubTab === 'budgets' && (
                            <BudgetsSubTab key="budgets" userId={customer.userId} />
                          )}
                          {financeSubTab === 'commitments' && (
                            <CommitmentsSubTab key="commitments" userId={customer.userId} />
                          )}
                        </>
                      )}
                    </TabsContent>

                    <TabsContent value="overview" className="mt-0">
                      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
                        <Card>
                          <CardHeader>
                            <CardTitle className="text-sm">Profile Summary</CardTitle>
                          </CardHeader>
                          <CardContent className="text-sm">
                            {customer.partyType === 'Business' ? (
                              <div className="space-y-1">
                                <DetailRow label="Business name" value={customer.displayName} />
                                <DetailRow
                                  label="Registration number"
                                  value={customer.businessProfile?.registrationNumber || '—'}
                                />
                                <DetailRow
                                  label="Incorporation country"
                                  value={customer.businessProfile?.incorporationCountry || '—'}
                                />
                                <DetailRow label="Industry" value={customer.businessProfile?.industry || '—'} />
                                <DetailRow label="Verification" value={verificationStatus || '—'} />
                              </div>
                            ) : (
                              <div className="space-y-1">
                                <DetailRow label="Full name" value={customer.displayName} />
                                <DetailRow
                                  label="Date of birth"
                                  value={formatDate(customer.personProfile?.dob || null)}
                                />
                                <DetailRow
                                  label="Nationality"
                                  value={customer.personProfile?.nationality || '—'}
                                />
                                <DetailRow
                                  label="Occupation"
                                  value={customer.personProfile?.occupation || '—'}
                                />
                                <DetailRow label="Verification" value={verificationStatus || '—'} />
                              </div>
                            )}
                          </CardContent>
                        </Card>

                        <Card>
                          <CardHeader>
                            <CardTitle className="text-sm">Account Information</CardTitle>
                          </CardHeader>
                          <CardContent className="text-sm">
                            <div className="space-y-1">
                              <DetailRow label="Status" value={customer.status} />
                              <DetailRow label="Party type" value={customer.partyType} />
                              <DetailRow label="Customer tier" value={customer.customerTierCode || '—'} />
                              <DetailRow label="Roles" value={String(roleAssignments.length)} />
                              <DetailRow label="Created" value={formatDate(customer.createdAt)} />
                              <DetailRow label="Updated" value={formatDate(customer.updatedAt)} />
                            </div>
                          </CardContent>
                        </Card>
                      </div>

                      <div className="mt-6 grid grid-cols-1 lg:grid-cols-2 gap-6">
                        <Card>
                          <CardHeader>
                            <CardTitle className="text-sm">Primary Contact</CardTitle>
                          </CardHeader>
                          <CardContent className="text-sm space-y-3">
                            <div className="flex items-center gap-2">
                              <Mail className="w-4 h-4 text-[var(--color-text-tertiary)]" />
                              <span>{primaryEmail || 'No primary email'}</span>
                            </div>
                            <div className="flex items-center gap-2">
                              <Phone className="w-4 h-4 text-[var(--color-text-tertiary)]" />
                              <span>{primaryPhone || 'No primary phone'}</span>
                            </div>
                          </CardContent>
                        </Card>

                        <Card>
                          <CardHeader>
                            <CardTitle className="text-sm">Primary Address</CardTitle>
                          </CardHeader>
                          <CardContent className="text-sm">
                            {primaryAddress ? (
                              <div className="space-y-1">
                                <DetailRow label="Type" value={primaryAddress.type} />
                                <DetailRow
                                  label="Address"
                                  value={
                                    [
                                      primaryAddress.line1,
                                      primaryAddress.line2,
                                      primaryAddress.city,
                                      primaryAddress.state,
                                      primaryAddress.postcode,
                                      primaryAddress.country,
                                    ]
                                      .filter(Boolean)
                                      .join(', ') || '—'
                                  }
                                />
                              </div>
                            ) : (
                              <p className="text-sm text-[var(--color-text-tertiary)]">No address on file</p>
                            )}
                          </CardContent>
                        </Card>
                      </div>
                    </TabsContent>

                    <TabsContent value="contacts" className="mt-0">
                      <Card>
                        <CardHeader>
                          <CardTitle className="text-sm">Contacts</CardTitle>
                        </CardHeader>
                        <CardContent className="space-y-4">
                          {contacts.length === 0 ? (
                            <p className="text-sm text-[var(--color-text-tertiary)]">No contacts available.</p>
                          ) : (
                            contacts.map((contact) => (
                              <div
                                key={contact.contactId}
                                className="flex items-start justify-between gap-4 border-b border-[var(--color-border-light)] pb-3 last:border-b-0"
                              >
                                <div className="flex items-start gap-3">
                                  {contact.type === 'Email' ? (
                                    <Mail className="w-4 h-4 text-[var(--color-text-tertiary)] mt-0.5" />
                                  ) : (
                                    <Phone className="w-4 h-4 text-[var(--color-text-tertiary)] mt-0.5" />
                                  )}
                                  <div>
                                    <div className="text-sm text-[var(--color-text-primary)]">{contact.value}</div>
                                    <div className="text-xs text-[var(--color-text-tertiary)]">{contact.type}</div>
                                  </div>
                                </div>
                                {contact.isPrimary && (
                                  <Badge className="bg-[var(--color-brand-primary-light)] text-[var(--color-brand-primary)] text-xs">
                                    Primary
                                  </Badge>
                                )}
                              </div>
                            ))
                          )}
                        </CardContent>
                      </Card>
                    </TabsContent>

                    <TabsContent value="addresses" className="mt-0">
                      <Card>
                        <CardHeader>
                          <CardTitle className="text-sm">Addresses</CardTitle>
                        </CardHeader>
                        <CardContent className="space-y-4">
                          {addresses.length === 0 ? (
                            <p className="text-sm text-[var(--color-text-tertiary)]">No addresses available.</p>
                          ) : (
                            addresses.map((address) => (
                              <div
                                key={address.addressId}
                                className="flex items-start justify-between gap-4 border-b border-[var(--color-border-light)] pb-3 last:border-b-0"
                              >
                                <div className="flex items-start gap-3">
                                  <MapPin className="w-4 h-4 text-[var(--color-text-tertiary)] mt-0.5" />
                                  <div>
                                    <div className="text-sm text-[var(--color-text-primary)]">{address.type}</div>
                                    <div className="text-xs text-[var(--color-text-tertiary)]">
                                      {[address.line1, address.line2, address.city, address.state, address.postcode, address.country]
                                        .filter(Boolean)
                                        .join(', ')}
                                    </div>
                                  </div>
                                </div>
                              </div>
                            ))
                          )}
                        </CardContent>
                      </Card>
                    </TabsContent>

                    <TabsContent value="accounts" className="mt-0">
                      <Card>
                        <CardHeader>
                          <CardTitle className="text-sm">External Accounts</CardTitle>
                        </CardHeader>
                        <CardContent className="space-y-4">
                          {externalAccounts.length === 0 ? (
                            <p className="text-sm text-[var(--color-text-tertiary)]">No external accounts linked.</p>
                          ) : (
                            externalAccounts.map((account) => (
                              <div
                                key={account.partyAccountId}
                                className="flex items-start justify-between gap-4 border-b border-[var(--color-border-light)] pb-3 last:border-b-0"
                              >
                                <div className="flex items-start gap-3">
                                  <Wallet className="w-4 h-4 text-[var(--color-text-tertiary)] mt-0.5" />
                                  <div>
                                    <div className="text-sm text-[var(--color-text-primary)]">
                                      {account.accountType}
                                    </div>
                                    <div className="text-xs text-[var(--color-text-tertiary)]">{account.maskedIdentifier}</div>
                                    {account.providerRef && (
                                      <div className="text-xs text-[var(--color-text-tertiary)]">
                                        Provider: {account.providerRef}
                                      </div>
                                    )}
                                  </div>
                                </div>
                                <Badge variant="outline" className="text-xs">
                                  {account.verificationStatus}
                                </Badge>
                              </div>
                            ))
                          )}
                        </CardContent>
                      </Card>
                    </TabsContent>

                    <TabsContent value="relationships" className="mt-0">
                      <Card>
                        <CardHeader>
                          <CardTitle className="text-sm">Relationships</CardTitle>
                        </CardHeader>
                        <CardContent className="space-y-4">
                          {relationships.length === 0 ? (
                            <p className="text-sm text-[var(--color-text-tertiary)]">No relationships available.</p>
                          ) : (
                            relationships.map((relationship) => (
                              <div
                                key={relationship.relationshipId}
                                className="flex items-start justify-between gap-4 border-b border-[var(--color-border-light)] pb-3 last:border-b-0"
                              >
                                <div className="flex items-start gap-3">
                                  <Users className="w-4 h-4 text-[var(--color-text-tertiary)] mt-0.5" />
                                  <div>
                                    <div className="text-sm text-[var(--color-text-primary)]">
                                      {relationship.relationshipTypeCode}
                                    </div>
                                    <div className="text-xs text-[var(--color-text-tertiary)]">
                                      From {relationship.fromPartyId} to {relationship.toPartyId}
                                    </div>
                                    {relationship.notes && (
                                      <div className="text-xs text-[var(--color-text-tertiary)]">{relationship.notes}</div>
                                    )}
                                  </div>
                                </div>
                                <Badge variant="outline" className="text-xs">
                                  {relationship.isActive ? 'Active' : 'Inactive'}
                                </Badge>
                              </div>
                            ))
                          )}
                        </CardContent>
                      </Card>
                    </TabsContent>

                    <TabsContent value="consents" className="mt-0">
                      <Card>
                        <CardHeader>
                          <CardTitle className="text-sm">Consents</CardTitle>
                        </CardHeader>
                        <CardContent className="space-y-4">
                          {consents.length === 0 ? (
                            <p className="text-sm text-[var(--color-text-tertiary)]">No consents recorded.</p>
                          ) : (
                            consents.map((consent) => (
                              <div
                                key={consent.consentId}
                                className="flex items-start justify-between gap-4 border-b border-[var(--color-border-light)] pb-3 last:border-b-0"
                              >
                                <div className="flex items-start gap-3">
                                  <ShieldCheck className="w-4 h-4 text-[var(--color-text-tertiary)] mt-0.5" />
                                  <div>
                                    <div className="text-sm text-[var(--color-text-primary)]">{consent.consentType}</div>
                                    <div className="text-xs text-[var(--color-text-tertiary)]">
                                      Granted {formatDate(consent.grantedAt)}
                                    </div>
                                    {consent.revokedAt && (
                                      <div className="text-xs text-[var(--color-text-tertiary)]">
                                        Revoked {formatDate(consent.revokedAt)}
                                      </div>
                                    )}
                                  </div>
                                </div>
                                <Badge variant="outline" className="text-xs">
                                  {consent.revokedAt ? 'Revoked' : 'Active'}
                                </Badge>
                              </div>
                            ))
                          )}
                        </CardContent>
                      </Card>
                    </TabsContent>

                    <TabsContent value="insights" className="mt-0">
                      <div className="space-y-4">
                        {insightsError && (
                          <div className="rounded-sm border border-[var(--color-error)] bg-[var(--color-error-light)] px-3 py-2 text-xs text-[var(--color-error)]">
                            {insightsError}
                          </div>
                        )}
                        {insightsLoading ? (
                          <Card>
                            <CardContent className="flex items-center justify-center py-10">
                              <div className="w-6 h-6 border-2 border-[var(--color-brand-primary)] border-t-transparent rounded-full animate-spin" />
                            </CardContent>
                          </Card>
                        ) : !insights?.aiSummary && !insights?.snapshot ? (
                          <Card>
                            <CardContent className="text-center py-10">
                              <Lightbulb className="w-8 h-8 mx-auto mb-2 text-[var(--color-text-tertiary)]" />
                              <p className="text-sm text-[var(--color-text-secondary)]">No customer insights generated yet.</p>
                              <p className="text-xs text-[var(--color-text-tertiary)] mt-1">
                                Insights appear after the customer insight snapshot and AI summary pipeline runs.
                              </p>
                            </CardContent>
                          </Card>
                        ) : (
                          <>
                            {insights.aiSummary && (
                              <Card>
                                <CardHeader>
                                  <div className="flex items-center gap-2">
                                    <Lightbulb className="w-4 h-4 text-[var(--color-warning)]" />
                                    <CardTitle className="text-sm">AI Summary</CardTitle>
                                    <Badge variant="outline" className="text-[10px] uppercase tracking-wide">AI</Badge>
                                  </div>
                                  <p className="text-base font-medium text-[var(--color-text-primary)] mt-1">
                                    {insights.aiSummary.headline}
                                  </p>
                                  <p className="text-sm text-[var(--color-text-secondary)] mt-1">
                                    {insights.aiSummary.summary}
                                  </p>
                                </CardHeader>
                                <CardContent className="space-y-4">
                                  {insights.aiSummary.keyObservations.length > 0 && (
                                    <div>
                                      <h4 className="text-xs font-medium text-[var(--color-text-tertiary)] uppercase tracking-wide mb-2">Key Observations</h4>
                                      <ul className="space-y-1">
                                        {insights.aiSummary.keyObservations.map((obs, i) => (
                                          <li key={i} className="text-sm text-[var(--color-text-secondary)] flex items-start gap-2">
                                            <span className="text-[var(--color-text-tertiary)] mt-0.5 shrink-0">&#8226;</span>
                                            <span>{obs}</span>
                                          </li>
                                        ))}
                                      </ul>
                                    </div>
                                  )}

                                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                                    {insights.aiSummary.positivePatterns.length > 0 && (
                                      <div className="border border-[var(--color-border-light)] rounded-md p-3">
                                        <h4 className="text-xs font-medium text-[var(--color-success)] uppercase tracking-wide mb-2">Positive Patterns</h4>
                                        <ul className="space-y-1">
                                          {insights.aiSummary.positivePatterns.map((p, i) => (
                                            <li key={i} className="text-sm text-[var(--color-text-secondary)]">{p}</li>
                                          ))}
                                        </ul>
                                      </div>
                                    )}
                                    {insights.aiSummary.riskPatterns.length > 0 && (
                                      <div className="border border-[var(--color-border-light)] rounded-md p-3">
                                        <h4 className="text-xs font-medium text-[var(--color-error)] uppercase tracking-wide mb-2">Risk Patterns</h4>
                                        <ul className="space-y-1">
                                          {insights.aiSummary.riskPatterns.map((p, i) => (
                                            <li key={i} className="text-sm text-[var(--color-text-secondary)]">{p}</li>
                                          ))}
                                        </ul>
                                      </div>
                                    )}
                                  </div>

                                  {insights.aiSummary.recommendedFocusAreas.length > 0 && (
                                    <div>
                                      <h4 className="text-xs font-medium text-[var(--color-text-tertiary)] uppercase tracking-wide mb-2">Recommended Focus Areas</h4>
                                      <ul className="space-y-1">
                                        {insights.aiSummary.recommendedFocusAreas.map((area, i) => (
                                          <li key={i} className="text-sm text-[var(--color-text-secondary)] flex items-start gap-2">
                                            <span className="text-[var(--color-brand-primary)] mt-0.5 shrink-0">&#8594;</span>
                                            <span>{area}</span>
                                          </li>
                                        ))}
                                      </ul>
                                    </div>
                                  )}

                                  {insights.aiSummary.caveats.length > 0 && (
                                    <div className="bg-[var(--color-surface-inset)] rounded-md p-3">
                                      <h4 className="text-xs font-medium text-[var(--color-text-tertiary)] uppercase tracking-wide mb-2">Caveats</h4>
                                      <ul className="space-y-1">
                                        {insights.aiSummary.caveats.map((c, i) => (
                                          <li key={i} className="text-xs text-[var(--color-text-tertiary)]">{c}</li>
                                        ))}
                                      </ul>
                                    </div>
                                  )}

                                  <div className="text-xs text-[var(--color-text-tertiary)] pt-2 border-t border-[var(--color-border-light)]">
                                    Generated {formatDateTime(insights.aiSummary.createdUtc)}
                                  </div>
                                </CardContent>
                              </Card>
                            )}

                            {insights.snapshot && !insights.aiSummary && (
                              <Card>
                                <CardHeader>
                                  <div className="flex items-center gap-2">
                                    <Lightbulb className="w-4 h-4 text-[var(--color-text-tertiary)]" />
                                    <CardTitle className="text-sm">Snapshot</CardTitle>
                                    {insights.snapshot.isPartial && (
                                      <Badge variant="outline" className="text-[10px] uppercase tracking-wide">Partial</Badge>
                                    )}
                                  </div>
                                </CardHeader>
                                <CardContent className="space-y-2">
                                  {insights.snapshot.topSignalTitle && (
                                    <div>
                                      <div className="text-sm font-medium text-[var(--color-text-primary)]">{insights.snapshot.topSignalTitle}</div>
                                      {insights.snapshot.topSignalDescription && (
                                        <div className="text-sm text-[var(--color-text-secondary)] mt-1">{insights.snapshot.topSignalDescription}</div>
                                      )}
                                    </div>
                                  )}
                                  {insights.snapshot.cashflowStressLevel && insights.snapshot.cashflowStressLevel !== 'Low' && (
                                    <div className="text-sm text-[var(--color-text-secondary)]">
                                      Cashflow stress: <span className="font-medium">{insights.snapshot.cashflowStressLevel}</span>
                                    </div>
                                  )}
                                  <div className="text-xs text-[var(--color-text-tertiary)]">
                                    Snapshot as of {formatDateTime(insights.snapshot.asOfUtc)}
                                  </div>
                                </CardContent>
                              </Card>
                            )}
                          </>
                        )}
                      </div>
                    </TabsContent>

                    <TabsContent value="documents" className="mt-0">
                      <Card>
                        <CardHeader className="flex flex-row items-center justify-between space-y-0">
                          <div>
                            <CardTitle className="text-sm">Documents</CardTitle>
                            <p className="text-xs text-[var(--color-text-tertiary)]">Recent compliance documents.</p>
                          </div>
                          <Button
                            variant="outline"
                            size="sm"
                            onClick={() => navigate('/compliance/documents')}
                          >
                            View all
                          </Button>
                        </CardHeader>
                        <CardContent className="space-y-4">
                          {documentsError && (
                            <div className="rounded-sm border border-[var(--color-error)] bg-[var(--color-error-light)] px-3 py-2 text-xs text-[var(--color-error)]">
                              {documentsError}
                            </div>
                          )}
                          {documentsLoading ? (
                            <div className="flex items-center justify-center py-6">
                              <div className="w-6 h-6 border-2 border-[var(--color-brand-primary)] border-t-transparent rounded-full animate-spin" />
                            </div>
                          ) : documents.length === 0 ? (
                            <div className="text-center py-6">
                              <FileText className="w-8 h-8 mx-auto mb-2 text-[var(--color-text-tertiary)]" />
                              <p className="text-sm text-[var(--color-text-secondary)]">No documents recorded.</p>
                            </div>
                          ) : (
                            documents.map((doc) => (
                              <div
                                key={doc.documentId}
                                className="flex items-start justify-between gap-4 border-b border-[var(--color-border-light)] pb-3 last:border-b-0"
                              >
                                <div>
                                  <div className="text-sm text-[var(--color-text-primary)] font-medium">{doc.documentType}</div>
                                  <div className="text-xs text-[var(--color-text-tertiary)]">
                                    Issued {formatDateShort(doc.issuedOn)} · Expires {formatDateShort(doc.expiresOn)}
                                  </div>
                                  <div className="text-xs text-[var(--color-text-tertiary)]">
                                    Reference {doc.referenceNumber || '—'}
                                  </div>
                                </div>
                                <div className="flex flex-col items-end gap-2">
                                  <Badge variant="outline" className="text-xs">
                                    {doc.status}
                                  </Badge>
                                  <Button
                                    variant="ghost"
                                    size="sm"
                                    onClick={() => navigate(`/compliance/documents/${doc.documentId}`)}
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
