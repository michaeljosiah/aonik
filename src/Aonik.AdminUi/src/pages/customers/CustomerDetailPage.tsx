import { useCallback, useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';

import { AlertCircle, RefreshCw, User, UsersRound } from 'lucide-react';

import { Breadcrumb } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';

import { customerService } from '@/services/customerService';
import type { CustomerDetail } from '@/types';

export function CustomerDetailPage() {
  const navigate = useNavigate();
  const { partyId } = useParams<{ partyId: string }>();

  const [customer, setCustomer] = useState<CustomerDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

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

  useEffect(() => {
    loadCustomer();
  }, [loadCustomer]);

  const formatDate = (dateString?: string | null) => {
    if (!dateString) return '—';
    return new Date(dateString).toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
    });
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

  const primaryEmail = customer.contacts?.find((c) => c.type === 'Email' && c.isPrimary)?.value;
  const primaryPhone = customer.contacts?.find((c) => c.type === 'Phone' && c.isPrimary)?.value;

  const verificationStatus =
    customer.partyType === 'Business'
      ? customer.businessProfile?.kybStatus
      : customer.personProfile?.idvStatus;

  return (
    <div className="h-full overflow-auto p-6">
      <Breadcrumb items={breadcrumbItems} className="mb-4" />

      {error && (
        <Card className="mb-6 border-[var(--color-error)] bg-[var(--color-error-light)]">
          <CardContent className="p-4 flex items-center gap-3 text-[var(--color-error)]">
            <AlertCircle className="w-5 h-5" />
            <span>{error}</span>
            <Button variant="outline" size="sm" onClick={loadCustomer} className="ml-auto">
              Retry
            </Button>
          </CardContent>
        </Card>
      )}

      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">{customer.displayName}</h1>
          <p className="text-[var(--color-text-secondary)]">
            {primaryEmail || primaryPhone || '—'}
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Badge variant="outline" className="text-xs">
            {customer.partyType}
          </Badge>
          <Badge variant="outline" className="text-xs">
            {customer.status}
          </Badge>
        </div>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <Card>
          <CardContent className="p-4">
            <p className="text-xs uppercase tracking-wide text-[var(--color-text-tertiary)]">Verification</p>
            <p className="text-sm text-[var(--color-text-primary)] mt-1">{verificationStatus || '—'}</p>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="p-4">
            <p className="text-xs uppercase tracking-wide text-[var(--color-text-tertiary)]">Created</p>
            <p className="text-sm text-[var(--color-text-primary)] mt-1">{formatDate(customer.createdAt)}</p>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="p-4">
            <p className="text-xs uppercase tracking-wide text-[var(--color-text-tertiary)]">Customer Tier</p>
            <p className="text-sm text-[var(--color-text-primary)] mt-1">{customer.customerTierCode || '—'}</p>
          </CardContent>
        </Card>
      </div>

      <div className="mt-6 grid grid-cols-1 md:grid-cols-2 gap-4">
        <Card>
          <CardContent className="p-4">
            <p className="text-sm font-medium text-[var(--color-text-primary)] mb-3">Contacts</p>
            {customer.contacts.length === 0 ? (
              <p className="text-sm text-[var(--color-text-tertiary)]">No contacts</p>
            ) : (
              <div className="space-y-2">
                {customer.contacts.map((c) => (
                  <div key={c.contactId} className="flex items-center justify-between">
                    <span className="text-sm text-[var(--color-text-secondary)]">{c.type}</span>
                    <span className="text-sm text-[var(--color-text-primary)]">
                      {c.value}{c.isPrimary ? ' (primary)' : ''}
                    </span>
                  </div>
                ))}
              </div>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardContent className="p-4">
            <p className="text-sm font-medium text-[var(--color-text-primary)] mb-3">Addresses</p>
            {customer.addresses.length === 0 ? (
              <p className="text-sm text-[var(--color-text-tertiary)]">No addresses</p>
            ) : (
              <div className="space-y-2">
                {customer.addresses.map((a) => (
                  <div key={a.addressId} className="flex items-start justify-between gap-4">
                    <span className="text-sm text-[var(--color-text-secondary)]">{a.type}</span>
                    <span className="text-sm text-[var(--color-text-primary)] text-right">
                      {[a.line1, a.city, a.state, a.postcode, a.country].filter(Boolean).join(', ')}
                    </span>
                  </div>
                ))}
              </div>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
