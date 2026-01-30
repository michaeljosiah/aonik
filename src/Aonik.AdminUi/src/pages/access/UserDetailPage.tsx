import { useState, useEffect, useCallback } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Avatar, AvatarFallback } from '@/components/ui/avatar';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs';
import { Breadcrumb } from '@/components/ui/breadcrumb';
import {
  RefreshCw,
  AlertCircle,
  TrendingUp,
  TrendingDown,
  ChevronDown,
  ChevronRight,
  Plus,
  Pencil,
  Trash2,
  CreditCard,
  Download,
  CheckCircle,
  Clock,
  Shield,
  Link2,
} from 'lucide-react';
import { userService } from '@/services/userService';
import type { AccessUserDetail } from '@/types';

// Mock data for payment records
const mockPaymentRecords = [
  { id: '1', invoiceNo: '6172-4215', status: 'Successful', amount: 1200.00, date: '14 Dec 2020, 8:43 pm' },
  { id: '2', invoiceNo: '7753-7528', status: 'Successful', amount: 79.00, date: '01 Dec 2020, 10:12 am' },
  { id: '3', invoiceNo: '1283-9334', status: 'Successful', amount: 5500.00, date: '12 Nov 2020, 2:01 pm' },
  { id: '4', invoiceNo: '5455-7997', status: 'Pending', amount: 880.00, date: '21 Oct 2020, 5:54 pm' },
  { id: '5', invoiceNo: '1097-3346', status: 'Successful', amount: 7650.00, date: '19 Oct 2020, 7:32 am' },
];

// Mock data for payment methods
const mockPaymentMethods = [
  {
    id: '1',
    type: 'Mastercard',
    isPrimary: true,
    expiresAt: 'Dec 2024',
    details: {
      name: 'Emma Smith',
      number: '**** 6963',
      expires: '12/2024',
      cardType: 'Mastercard credit card',
      issuer: 'VICBANK',
      cardId: 'id_4325df90sdf8',
      billingAddress: 'AU',
      phone: 'No phone provided',
      email: 'smith@kpmg.com',
      origin: 'Australia',
      cvcCheck: 'Passed',
    },
  },
  {
    id: '2',
    type: 'Visa',
    isPrimary: false,
    expiresAt: 'Feb 2022',
    details: null,
  },
  {
    id: '3',
    type: 'American Express',
    isPrimary: false,
    expiresAt: 'Expired',
    isExpired: true,
    details: null,
  },
];

// Mock data for invoices
const mockInvoices = [
  { id: '1', orderId: '102445788', amount: 38.00, status: 'Approved', date: 'Nov 01, 2020' },
  { id: '2', orderId: '423445721', amount: -2.60, status: 'Pending', date: 'Oct 24, 2020' },
  { id: '3', orderId: '312445984', amount: 76.00, status: 'Approved', date: 'Oct 08, 2020' },
  { id: '4', orderId: '312445984', amount: 5.00, status: 'Pending', date: 'Sep 15, 2020' },
  { id: '5', orderId: '523445943', amount: -1.30, status: 'In progress', date: 'May 30, 2020' },
];

// Mock connected accounts
const mockConnectedAccounts = [
  { id: '1', name: 'Google', description: 'Plan properly your workflow', icon: 'G', color: 'bg-white', enabled: true },
  { id: '2', name: 'Github', description: 'Keep eye on your Repositories', icon: 'GH', color: 'bg-gray-900', enabled: true },
  { id: '3', name: 'Slack', description: 'Integrate Projects Discussions', icon: 'S', color: 'bg-purple-500', enabled: false },
];

const statusStyles: Record<string, { text: string; bg: string }> = {
  Active: { text: 'text-[var(--color-success)]', bg: 'bg-[var(--color-success-light)]' },
  Invited: { text: 'text-[var(--color-warning)]', bg: 'bg-[var(--color-warning-light)]' },
  Pending: { text: 'text-[var(--color-warning)]', bg: 'bg-[var(--color-warning-light)]' },
  Deactivated: { text: 'text-[var(--color-text-tertiary)]', bg: 'bg-[var(--color-surface-inset)]' },
  Suspended: { text: 'text-[var(--color-error)]', bg: 'bg-[var(--color-error-light)]' },
};

const paymentStatusStyles: Record<string, { text: string; bg: string }> = {
  Successful: { text: 'text-[var(--color-success)]', bg: 'bg-[var(--color-success-light)]' },
  Approved: { text: 'text-[var(--color-success)]', bg: 'bg-[var(--color-success-light)]' },
  Pending: { text: 'text-[var(--color-warning)]', bg: 'bg-[var(--color-warning-light)]' },
  'In progress': { text: 'text-[var(--color-info)]', bg: 'bg-[var(--color-info-light)]' },
  Failed: { text: 'text-[var(--color-error)]', bg: 'bg-[var(--color-error-light)]' },
};

// Detail Item Component
function DetailItem({ label, value }: { label: string; value: string }) {
  return (
    <div className="py-3 border-b border-[var(--color-border-light)] last:border-b-0">
      <p className="text-xs font-medium text-[var(--color-text-primary)] mb-0.5">{label}</p>
      <p className="text-sm text-[var(--color-text-secondary)]">{value}</p>
    </div>
  );
}

// Payment Method Card Component
function PaymentMethodCard({ method, isExpanded, onToggle }: { 
  method: typeof mockPaymentMethods[0]; 
  isExpanded: boolean;
  onToggle: () => void;
}) {
  const getCardIcon = (type: string) => {
    if (type === 'Mastercard') return (
      <div className="w-8 h-5 bg-gradient-to-r from-red-500 to-yellow-500 rounded flex items-center justify-center">
        <span className="text-[8px] text-white font-bold">MC</span>
      </div>
    );
    if (type === 'Visa') return (
      <div className="w-8 h-5 bg-blue-600 rounded flex items-center justify-center">
        <span className="text-[8px] text-white font-bold">VISA</span>
      </div>
    );
    return (
      <div className="w-8 h-5 bg-blue-400 rounded flex items-center justify-center">
        <span className="text-[8px] text-white font-bold">AMEX</span>
      </div>
    );
  };

  return (
    <div className="border border-[var(--color-border-light)] rounded-lg overflow-hidden">
      <div 
        className="flex items-center justify-between p-4 cursor-pointer hover:bg-[var(--color-surface-inset)]"
        onClick={onToggle}
      >
        <div className="flex items-center gap-3">
          <button className="text-[var(--color-text-tertiary)]">
            {isExpanded ? <ChevronDown className="w-4 h-4" /> : <ChevronRight className="w-4 h-4" />}
          </button>
          {getCardIcon(method.type)}
          <div>
            <div className="flex items-center gap-2">
              <span className="font-medium text-[var(--color-text-primary)]">{method.type}</span>
              {method.isPrimary && (
                <Badge className="bg-[var(--color-brand-primary-light)] text-[var(--color-brand-primary)] text-xs">
                  Primary
                </Badge>
              )}
              {method.isExpired && (
                <Badge className="bg-[var(--color-error-light)] text-[var(--color-error)] text-xs">
                  Expired
                </Badge>
              )}
            </div>
            <span className="text-xs text-[var(--color-text-tertiary)]">Expires {method.expiresAt}</span>
          </div>
        </div>
        <div className="flex items-center gap-2">
          <Button variant="ghost" size="sm" className="h-8 w-8 p-0">
            <Pencil className="w-4 h-4" />
          </Button>
          <Button variant="ghost" size="sm" className="h-8 w-8 p-0">
            <Trash2 className="w-4 h-4" />
          </Button>
          <Button variant="ghost" size="sm" className="h-8 w-8 p-0">
            <CreditCard className="w-4 h-4" />
          </Button>
        </div>
      </div>

      {isExpanded && method.details && (
        <div className="px-4 pb-4 border-t border-[var(--color-border-light)] bg-[var(--color-surface-inset)]">
          <div className="grid grid-cols-2 gap-x-8 gap-y-2 pt-4">
            <div className="space-y-2">
              <div className="flex justify-between">
                <span className="text-sm text-[var(--color-text-tertiary)]">Name</span>
                <span className="text-sm text-[var(--color-text-primary)]">{method.details.name}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-sm text-[var(--color-text-tertiary)]">Number</span>
                <span className="text-sm text-[var(--color-text-primary)]">{method.details.number}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-sm text-[var(--color-text-tertiary)]">Expires</span>
                <span className="text-sm text-[var(--color-text-primary)]">{method.details.expires}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-sm text-[var(--color-text-tertiary)]">Type</span>
                <span className="text-sm text-[var(--color-text-primary)]">{method.details.cardType}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-sm text-[var(--color-text-tertiary)]">Issuer</span>
                <span className="text-sm text-[var(--color-text-primary)]">{method.details.issuer}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-sm text-[var(--color-text-tertiary)]">ID</span>
                <span className="text-sm text-[var(--color-text-primary)]">{method.details.cardId}</span>
              </div>
            </div>
            <div className="space-y-2">
              <div className="flex justify-between">
                <span className="text-sm text-[var(--color-text-tertiary)]">Billing address</span>
                <span className="text-sm text-[var(--color-text-primary)]">{method.details.billingAddress}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-sm text-[var(--color-text-tertiary)]">Phone</span>
                <span className="text-sm text-[var(--color-text-primary)]">{method.details.phone}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-sm text-[var(--color-text-tertiary)]">Email</span>
                <span className="text-sm text-[var(--color-text-primary)]">{method.details.email}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-sm text-[var(--color-text-tertiary)]">Origin</span>
                <span className="text-sm text-[var(--color-text-primary)]">{method.details.origin}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-sm text-[var(--color-text-tertiary)]">CVC check</span>
                <span className="text-sm text-[var(--color-success)] flex items-center gap-1">
                  {method.details.cvcCheck} <CheckCircle className="w-3 h-3" />
                </span>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

// Connected Account Toggle
function ConnectedAccountToggle({ account }: { account: typeof mockConnectedAccounts[0] }) {
  const [enabled, setEnabled] = useState(account.enabled);

  return (
    <div className="flex items-center justify-between py-3">
      <div className="flex items-center gap-3">
        <div className={`w-8 h-8 rounded flex items-center justify-center ${account.color} text-white text-xs font-bold`}>
          {account.icon}
        </div>
        <div>
          <p className="text-sm font-medium text-[var(--color-text-primary)]">{account.name}</p>
          <p className="text-xs text-[var(--color-text-tertiary)]">{account.description}</p>
        </div>
      </div>
      <button 
        onClick={() => setEnabled(!enabled)}
        className={`w-10 h-6 rounded-full transition-colors ${enabled ? 'bg-[var(--color-brand-primary)]' : 'bg-[var(--color-border)]'}`}
      >
        <div className={`w-4 h-4 bg-white rounded-full transition-transform mx-1 ${enabled ? 'translate-x-4' : 'translate-x-0'}`} />
      </button>
    </div>
  );
}

export function UserDetailPage() {
  const navigate = useNavigate();
  const { userId } = useParams<{ userId: string }>();
  
  const [user, setUser] = useState<AccessUserDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState('overview');
  const [expandedPaymentMethod, setExpandedPaymentMethod] = useState<string | null>('1');
  const [detailsExpanded, setDetailsExpanded] = useState(true);

  const loadUser = useCallback(async () => {
    if (!userId) return;
    
    setLoading(true);
    setError(null);
    try {
      const data = await userService.get(userId);
      setUser(data);
    } catch (err: unknown) {
      console.error('Failed to load user:', err);
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setError(message || 'Failed to load user. Please try again.');
    } finally {
      setLoading(false);
    }
  }, [userId]);

  useEffect(() => {
    loadUser();
  }, [loadUser]);

  const getInitials = (name?: string | null, email?: string) => {
    if (name) {
      return name.split(' ').map(n => n[0]).join('').toUpperCase();
    }
    return email?.charAt(0).toUpperCase() || 'U';
  };

  const breadcrumbItems = [
    { label: 'Home', href: '/' },
    { label: 'Users', href: '/access/users' },
  ];

  if (loading) {
    return (
      <div className="flex-1 flex items-center justify-center">
        <div className="text-center">
          <RefreshCw className="w-8 h-8 animate-spin mx-auto mb-3 text-[var(--color-brand-primary)]" />
          <p className="text-[var(--color-text-secondary)]">Loading user...</p>
        </div>
      </div>
    );
  }

  if (!user) {
    return (
      <div className="flex-1 flex items-center justify-center">
        <div className="text-center">
          <AlertCircle className="w-12 h-12 mx-auto mb-3 text-[var(--color-error)]" />
          <h2 className="text-xl font-semibold text-[var(--color-text-primary)] mb-2">User Not Found</h2>
          <p className="text-[var(--color-text-secondary)] mb-4">The user you're looking for doesn't exist or has been deleted.</p>
          <Button onClick={() => navigate('/access/users')}>
            Back to Users
          </Button>
        </div>
      </div>
    );
  }

  const statusStyle = statusStyles[user.status] ?? { text: 'text-[var(--color-text-secondary)]', bg: 'bg-[var(--color-surface-inset)]' };

  return (
    <div className="h-full overflow-auto bg-[var(--color-background)]">
      {/* Header */}
      <div className="px-6 py-4 flex items-center justify-between border-b border-[var(--color-border-light)] bg-[var(--color-surface)]">
        <div>
          <h1 className="text-lg font-semibold text-[var(--color-text-primary)]">User Details</h1>
          <Breadcrumb items={breadcrumbItems} className="mt-1" />
        </div>
        <div className="flex items-center gap-2">
          <Button variant="outline" size="sm">
            <Shield className="w-4 h-4 mr-2" />
            Filter
          </Button>
          <Button size="sm">
            Create
          </Button>
        </div>
      </div>

      {/* Error Alert */}
      {error && (
        <div className="px-6 pt-4">
          <Card className="border-[var(--color-error)] bg-[var(--color-error-light)]">
            <CardContent className="p-4 flex items-center gap-3 text-[var(--color-error)]">
              <AlertCircle className="w-5 h-5 flex-shrink-0" />
              <span className="flex-1">{error}</span>
              <Button variant="ghost" size="sm" onClick={loadUser}>
                Retry
              </Button>
            </CardContent>
          </Card>
        </div>
      )}

      {/* Main Content */}
      <div className="p-6">
        <div className="flex gap-6">
          {/* Left Sidebar - Profile Card */}
          <div className="w-72 flex-shrink-0 space-y-6">
            {/* Profile Card */}
            <Card>
              <CardContent className="p-6">
                {/* Avatar & Name */}
                <div className="text-center mb-6">
                  <div className="relative inline-block mb-3">
                    <Avatar className="h-20 w-20 mx-auto">
                      <AvatarFallback className="text-xl">
                        {getInitials(user.displayName, user.email)}
                      </AvatarFallback>
                    </Avatar>
                    {user.status === 'Active' && (
                      <span className="absolute bottom-0 right-0 w-4 h-4 bg-[var(--color-success)] border-2 border-white rounded-full" />
                    )}
                  </div>
                  <h2 className="text-lg font-semibold text-[var(--color-text-primary)]">
                    {user.displayName || user.email}
                  </h2>
                  <p className="text-sm text-[var(--color-text-tertiary)]">
                    {user.partyType || 'User'}
                  </p>
                </div>

                {/* Stats */}
                <div className="flex justify-center gap-6 mb-6 pb-6 border-b border-[var(--color-border-light)]">
                  <div className="text-center">
                    <div className="flex items-center gap-1 justify-center">
                      <span className="text-lg font-semibold text-[var(--color-text-primary)]">
                        {user.roles?.length || 0}
                      </span>
                      <TrendingUp className="w-3 h-3 text-[var(--color-success)]" />
                    </div>
                    <p className="text-xs text-[var(--color-text-tertiary)]">Roles</p>
                  </div>
                  <div className="text-center">
                    <div className="flex items-center gap-1 justify-center">
                      <span className="text-lg font-semibold text-[var(--color-text-primary)]">
                        {user.permissions?.length || 0}
                      </span>
                      <TrendingDown className="w-3 h-3 text-[var(--color-error)]" />
                    </div>
                    <p className="text-xs text-[var(--color-text-tertiary)]">Permissions</p>
                  </div>
                  <div className="text-center">
                    <div className="flex items-center gap-1 justify-center">
                      <span className="text-lg font-semibold text-[var(--color-text-primary)]">
                        {user.lastLoginAt ? '1' : '0'}
                      </span>
                      <TrendingUp className="w-3 h-3 text-[var(--color-success)]" />
                    </div>
                    <p className="text-xs text-[var(--color-text-tertiary)]">Logins</p>
                  </div>
                </div>

                {/* Details Section */}
                <div>
                  <div 
                    className="flex items-center justify-between cursor-pointer mb-2"
                    onClick={() => setDetailsExpanded(!detailsExpanded)}
                  >
                    <div className="flex items-center gap-2">
                      <span className="text-sm font-medium text-[var(--color-text-primary)]">Details</span>
                      {detailsExpanded ? (
                        <ChevronDown className="w-4 h-4 text-[var(--color-text-tertiary)]" />
                      ) : (
                        <ChevronRight className="w-4 h-4 text-[var(--color-text-tertiary)]" />
                      )}
                    </div>
                    <Button variant="outline" size="sm" className="h-7 text-xs">
                      Edit
                    </Button>
                  </div>

                  {detailsExpanded && (
                    <div>
                      <div className="mb-3">
                        <Badge className={`${statusStyle.bg} ${statusStyle.text} text-xs`}>
                          {user.status === 'Active' ? 'Premium user' : user.status}
                        </Badge>
                      </div>

                      <DetailItem label="Account ID" value={`ID-${user.userId.substring(0, 8)}`} />
                      <DetailItem label="Billing Email" value={user.email} />
                      <DetailItem 
                        label="Billing Address" 
                        value={user.partyDisplayName || 'Not provided'} 
                      />
                      <DetailItem label="Language" value="English" />
                      <DetailItem label="Last Login" value={user.lastLoginAt ? new Date(user.lastLoginAt).toLocaleDateString() : 'Never'} />
                      <DetailItem label="User ID" value={user.userId.substring(0, 12)} />
                    </div>
                  )}
                </div>
              </CardContent>
            </Card>

            {/* Connected Accounts */}
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-base">Connected Accounts</CardTitle>
              </CardHeader>
              <CardContent>
                <div className="bg-[var(--color-brand-primary-light)] rounded-lg p-3 mb-4 flex items-start gap-3">
                  <Link2 className="w-5 h-5 text-[var(--color-brand-primary)] mt-0.5" />
                  <p className="text-xs text-[var(--color-text-secondary)]">
                    By connecting an account, you hereby agree to our{' '}
                    <a href="#" className="text-[var(--color-brand-primary)] hover:underline">privacy policy</a>
                    {' '}and{' '}
                    <a href="#" className="text-[var(--color-brand-primary)] hover:underline">terms of</a>
                  </p>
                </div>

                <div className="space-y-1">
                  {mockConnectedAccounts.map(account => (
                    <ConnectedAccountToggle key={account.id} account={account} />
                  ))}
                </div>

                <Button variant="default" size="sm" className="w-full mt-4">
                  Save Changes
                </Button>
              </CardContent>
            </Card>
          </div>

          {/* Right Content - Tabs */}
          <div className="flex-1 min-w-0">
            <Card>
              <CardContent className="p-0">
                {/* Tabs Header */}
                <div className="flex items-center justify-between border-b border-[var(--color-border-light)] px-4">
                  <Tabs value={activeTab} onValueChange={setActiveTab}>
                    <TabsList className="bg-transparent p-0 h-auto gap-0">
                      <TabsTrigger
                        value="overview"
                        className="px-4 py-3 text-sm rounded-none border-b-2 border-transparent data-[state=active]:border-[var(--color-brand-primary)] data-[state=active]:bg-transparent data-[state=active]:text-[var(--color-brand-primary)]"
                      >
                        Overview
                      </TabsTrigger>
                      <TabsTrigger
                        value="events"
                        className="px-4 py-3 text-sm rounded-none border-b-2 border-transparent data-[state=active]:border-[var(--color-brand-primary)] data-[state=active]:bg-transparent data-[state=active]:text-[var(--color-brand-primary)]"
                      >
                        Events & Logs
                      </TabsTrigger>
                      <TabsTrigger
                        value="statements"
                        className="px-4 py-3 text-sm rounded-none border-b-2 border-transparent data-[state=active]:border-[var(--color-brand-primary)] data-[state=active]:bg-transparent data-[state=active]:text-[var(--color-brand-primary)]"
                      >
                        Statements
                      </TabsTrigger>
                    </TabsList>
                  </Tabs>
                  <Button size="sm">
                    Actions <ChevronDown className="w-4 h-4 ml-2" />
                  </Button>
                </div>

                {/* Tab Content */}
                <div className="p-6">
                  <TabsContent value="overview" className="mt-0">
                    {/* Payment Records */}
                    <div className="mb-8">
                      <div className="flex items-center justify-between mb-4">
                        <h3 className="text-base font-semibold text-[var(--color-text-primary)]">Payment Records</h3>
                        <Button variant="outline" size="sm">
                          <Plus className="w-4 h-4 mr-2" />
                          Add payment
                        </Button>
                      </div>

                      <div className="border border-[var(--color-border-light)] rounded-lg overflow-hidden">
                        <table className="w-full">
                          <thead className="bg-[var(--color-surface-inset)]">
                            <tr>
                              <th className="text-left text-xs font-medium text-[var(--color-text-tertiary)] uppercase tracking-wider px-4 py-3">Invoice No.</th>
                              <th className="text-left text-xs font-medium text-[var(--color-text-tertiary)] uppercase tracking-wider px-4 py-3">Status</th>
                              <th className="text-left text-xs font-medium text-[var(--color-text-tertiary)] uppercase tracking-wider px-4 py-3">Amount</th>
                              <th className="text-left text-xs font-medium text-[var(--color-text-tertiary)] uppercase tracking-wider px-4 py-3">Date</th>
                              <th className="text-right text-xs font-medium text-[var(--color-text-tertiary)] uppercase tracking-wider px-4 py-3">Actions</th>
                            </tr>
                          </thead>
                          <tbody className="divide-y divide-[var(--color-border-light)]">
                            {mockPaymentRecords.map(record => {
                              const style = paymentStatusStyles[record.status] ?? paymentStatusStyles.Pending;
                              return (
                                <tr key={record.id} className="hover:bg-[var(--color-surface-inset)]">
                                  <td className="px-4 py-3 text-sm text-[var(--color-text-primary)]">{record.invoiceNo}</td>
                                  <td className="px-4 py-3">
                                    <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${style.text}`}>
                                      {record.status}
                                    </span>
                                  </td>
                                  <td className="px-4 py-3 text-sm text-[var(--color-text-primary)]">${record.amount.toLocaleString('en-US', { minimumFractionDigits: 2 })}</td>
                                  <td className="px-4 py-3 text-sm text-[var(--color-text-secondary)]">{record.date}</td>
                                  <td className="px-4 py-3 text-right">
                                    <Button variant="ghost" size="sm" className="h-8">
                                      Actions <ChevronDown className="w-3 h-3 ml-1" />
                                    </Button>
                                  </td>
                                </tr>
                              );
                            })}
                          </tbody>
                        </table>
                      </div>

                      {/* Pagination */}
                      <div className="flex items-center justify-end gap-2 mt-4">
                        <Button variant="ghost" size="sm" disabled>&lt;</Button>
                        <Button variant="default" size="sm" className="w-8 h-8 p-0">1</Button>
                        <Button variant="ghost" size="sm" className="w-8 h-8 p-0">2</Button>
                        <Button variant="ghost" size="sm">&gt;</Button>
                      </div>
                    </div>

                    {/* Payment Methods */}
                    <div className="mb-8">
                      <div className="flex items-center justify-between mb-4">
                        <h3 className="text-base font-semibold text-[var(--color-text-primary)]">Payment Methods</h3>
                        <Button variant="outline" size="sm">
                          <Plus className="w-4 h-4 mr-2" />
                          Add new method
                        </Button>
                      </div>

                      <div className="space-y-3">
                        {mockPaymentMethods.map(method => (
                          <PaymentMethodCard
                            key={method.id}
                            method={method}
                            isExpanded={expandedPaymentMethod === method.id}
                            onToggle={() => setExpandedPaymentMethod(
                              expandedPaymentMethod === method.id ? null : method.id
                            )}
                          />
                        ))}
                      </div>
                    </div>

                    {/* Credit Balance */}
                    <div className="mb-8">
                      <div className="flex items-center justify-between mb-4">
                        <h3 className="text-base font-semibold text-[var(--color-text-primary)]">Credit Balance</h3>
                        <Button variant="outline" size="sm">
                          <Pencil className="w-4 h-4 mr-2" />
                          Adjust Balance
                        </Button>
                      </div>

                      <div>
                        <p className="text-2xl font-bold text-[var(--color-text-primary)]">
                          $32,487.57 <span className="text-sm font-normal text-[var(--color-text-tertiary)]">USD</span>
                        </p>
                        <p className="text-sm text-[var(--color-text-tertiary)] mt-1">
                          Balance will increase the amount due on the customer's next invoice.
                        </p>
                      </div>
                    </div>

                    {/* Invoices */}
                    <div>
                      <div className="flex items-center justify-between mb-4">
                        <h3 className="text-base font-semibold text-[var(--color-text-primary)]">Invoices</h3>
                        <div className="flex items-center gap-2">
                          <Button variant="ghost" size="sm" className="text-[var(--color-brand-primary)]">This Year</Button>
                          <Button variant="ghost" size="sm">2020</Button>
                          <Button variant="ghost" size="sm">2019</Button>
                          <Button variant="ghost" size="sm">2018</Button>
                        </div>
                      </div>

                      <div className="border border-[var(--color-border-light)] rounded-lg overflow-hidden">
                        <table className="w-full">
                          <thead className="bg-[var(--color-surface-inset)]">
                            <tr>
                              <th className="text-left text-xs font-medium text-[var(--color-text-tertiary)] uppercase tracking-wider px-4 py-3">Order ID</th>
                              <th className="text-left text-xs font-medium text-[var(--color-text-tertiary)] uppercase tracking-wider px-4 py-3">Amount</th>
                              <th className="text-left text-xs font-medium text-[var(--color-text-tertiary)] uppercase tracking-wider px-4 py-3">Status</th>
                              <th className="text-left text-xs font-medium text-[var(--color-text-tertiary)] uppercase tracking-wider px-4 py-3">Date</th>
                              <th className="text-right text-xs font-medium text-[var(--color-text-tertiary)] uppercase tracking-wider px-4 py-3">Invoice</th>
                            </tr>
                          </thead>
                          <tbody className="divide-y divide-[var(--color-border-light)]">
                            {mockInvoices.map(invoice => {
                              const style = paymentStatusStyles[invoice.status] ?? paymentStatusStyles.Pending;
                              return (
                                <tr key={invoice.id} className="hover:bg-[var(--color-surface-inset)]">
                                  <td className="px-4 py-3 text-sm text-[var(--color-text-primary)]">{invoice.orderId}</td>
                                  <td className={`px-4 py-3 text-sm font-medium ${invoice.amount < 0 ? 'text-[var(--color-error)]' : 'text-[var(--color-success)]'}`}>
                                    ${invoice.amount < 0 ? '' : ''}{invoice.amount.toFixed(2)}
                                  </td>
                                  <td className="px-4 py-3">
                                    <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${style.text}`}>
                                      {invoice.status}
                                    </span>
                                  </td>
                                  <td className="px-4 py-3 text-sm text-[var(--color-text-secondary)]">{invoice.date}</td>
                                  <td className="px-4 py-3 text-right">
                                    <Button variant="ghost" size="sm" className="text-[var(--color-text-secondary)]">
                                      Download
                                    </Button>
                                  </td>
                                </tr>
                              );
                            })}
                          </tbody>
                        </table>
                      </div>

                      {/* Pagination */}
                      <div className="flex items-center justify-end gap-2 mt-4">
                        <Button variant="ghost" size="sm" disabled>&lt;</Button>
                        <Button variant="default" size="sm" className="w-8 h-8 p-0">1</Button>
                        <Button variant="ghost" size="sm" className="w-8 h-8 p-0">2</Button>
                        <Button variant="ghost" size="sm">&gt;</Button>
                      </div>
                    </div>
                  </TabsContent>

                  <TabsContent value="events" className="mt-0">
                    <div className="text-center py-12">
                      <Clock className="w-12 h-12 mx-auto mb-3 text-[var(--color-text-tertiary)]" />
                      <h3 className="text-lg font-medium text-[var(--color-text-primary)] mb-2">Events & Logs</h3>
                      <p className="text-[var(--color-text-secondary)]">User activity and event logs will appear here.</p>
                    </div>
                  </TabsContent>

                  <TabsContent value="statements" className="mt-0">
                    <div className="text-center py-12">
                      <Download className="w-12 h-12 mx-auto mb-3 text-[var(--color-text-tertiary)]" />
                      <h3 className="text-lg font-medium text-[var(--color-text-primary)] mb-2">Statements</h3>
                      <p className="text-[var(--color-text-secondary)]">Account statements will appear here.</p>
                    </div>
                  </TabsContent>
                </div>
              </CardContent>
            </Card>
          </div>
        </div>
      </div>
    </div>
  );
}
