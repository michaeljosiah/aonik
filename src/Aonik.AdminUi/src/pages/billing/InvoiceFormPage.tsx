import { useState, useEffect, useCallback, useMemo } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import {
  Plus,
  Trash2,
  Send,
  CheckCircle2,
  XCircle,
  ArrowLeft,
} from 'lucide-react';
import { toast } from 'sonner';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Badge } from '@/components/ui/badge';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { billingService } from '@/services/billingService';
import { customerService } from '@/services/customerService';
import type {
  InvoiceResponse,
  CustomerListItem,
  CreateInvoiceLineItemRequest,
} from '@/types';

// ── Helpers ─────────────────────────────────────────────────────────

interface LineItemForm {
  id?: string;
  description: string;
  quantity: number;
  unitPrice: number;
}

function formatMoney(amount: number, currency: string): string {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency,
    minimumFractionDigits: 2,
  }).format(amount);
}

function formatDate(dateStr: string): string {
  return new Date(dateStr).toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  });
}

function toDateInputValue(date: Date): string {
  return date.toISOString().split('T')[0];
}

const CURRENCIES = ['USD', 'EUR', 'GBP', 'NGN', 'KES', 'ZAR', 'GHS', 'CAD', 'AUD'];

const STATUS_STYLES: Record<string, string> = {
  Draft: 'bg-zinc-100 text-zinc-600 dark:bg-zinc-800 dark:text-zinc-400',
  Issued: 'bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-400',
  Paid: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/30 dark:text-emerald-400',
  Cancelled: 'bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-400',
};

// ── Component ───────────────────────────────────────────────────────

export function InvoiceFormPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const isCreate = !id;

  // Existing invoice (edit/view mode)
  const [invoice, setInvoice] = useState<InvoiceResponse | null>(null);
  const [loading, setLoading] = useState(!isCreate);
  const [saving, setSaving] = useState(false);

  // Form state
  const [currency, setCurrency] = useState('USD');
  const [dueDate, setDueDate] = useState(
    toDateInputValue(new Date(Date.now() + 30 * 24 * 60 * 60 * 1000)),
  );
  const [customerId, setCustomerId] = useState('');
  const [lines, setLines] = useState<LineItemForm[]>([
    { description: '', quantity: 1, unitPrice: 0 },
  ]);
  const [discount, setDiscount] = useState(0);

  // Customer lookup
  const [customers, setCustomers] = useState<CustomerListItem[]>([]);
  const [customersLoading, setCustomersLoading] = useState(false);

  // Derived state
  const status = invoice?.status ?? 'Draft';
  const isReadOnly = status === 'Issued' || status === 'Paid' || status === 'Cancelled';
  const isDraft = status === 'Draft';

  // ── Computed totals ───────────────────────────────────────────────

  const subtotal = useMemo(
    () => lines.reduce((sum, l) => sum + l.quantity * l.unitPrice, 0),
    [lines],
  );
  const total = useMemo(() => Math.max(0, subtotal - discount), [subtotal, discount]);

  // ── Load customers ────────────────────────────────────────────────

  useEffect(() => {
    setCustomersLoading(true);
    customerService
      .list({ pageSize: 200 })
      .then((result) => setCustomers(result.items))
      .catch(() => setCustomers([]))
      .finally(() => setCustomersLoading(false));
  }, []);

  // ── Load existing invoice ─────────────────────────────────────────

  const loadInvoice = useCallback(async () => {
    if (!id) return;
    setLoading(true);
    try {
      const result = await billingService.getInvoice(id);
      setInvoice(result);
      setCurrency(result.currency);
      setDueDate(toDateInputValue(new Date(result.dueUtc)));
      setCustomerId(result.customerId);
      setDiscount(0); // discount is baked into totalAmount on server
      setLines(
        result.lineItems.map((li) => ({
          id: li.id,
          description: li.description,
          quantity: li.quantity,
          unitPrice: li.unitPrice,
        })),
      );
    } catch {
      toast.error('Failed to load invoice.');
      navigate('/billing/invoices');
    } finally {
      setLoading(false);
    }
  }, [id, navigate]);

  useEffect(() => {
    void loadInvoice();
  }, [loadInvoice]);

  // ── Line item management ──────────────────────────────────────────

  const addLine = () => {
    setLines([...lines, { description: '', quantity: 1, unitPrice: 0 }]);
  };

  const removeLine = (index: number) => {
    if (lines.length <= 1) return;
    setLines(lines.filter((_, i) => i !== index));
  };

  const updateLine = (index: number, patch: Partial<LineItemForm>) => {
    setLines(lines.map((l, i) => (i === index ? { ...l, ...patch } : l)));
  };

  // ── Actions ───────────────────────────────────────────────────────

  const handleSaveDraft = async () => {
    if (!customerId) {
      toast.error('Please select a customer.');
      return;
    }
    if (lines.every((l) => !l.description.trim())) {
      toast.error('Please add at least one line item.');
      return;
    }

    setSaving(true);
    try {
      const lineItems: CreateInvoiceLineItemRequest[] = lines
        .filter((l) => l.description.trim())
        .map((l) => ({
          description: l.description,
          quantity: l.quantity,
          unitPrice: l.unitPrice,
        }));

      const result = await billingService.createInvoice({
        customerId,
        invoiceNumber: '', // auto-generated by backend
        currency,
        dueUtc: new Date(dueDate).toISOString(),
        lineItems,
      });

      toast.success('Invoice saved as draft.');
      navigate(`/billing/invoices/${result.id}`);
    } catch {
      toast.error('Failed to create invoice.');
    } finally {
      setSaving(false);
    }
  };

  const handleSaveAndIssue = async () => {
    if (!customerId) {
      toast.error('Please select a customer.');
      return;
    }

    setSaving(true);
    try {
      const lineItems: CreateInvoiceLineItemRequest[] = lines
        .filter((l) => l.description.trim())
        .map((l) => ({
          description: l.description,
          quantity: l.quantity,
          unitPrice: l.unitPrice,
        }));

      const created = await billingService.createInvoice({
        customerId,
        invoiceNumber: '',
        currency,
        dueUtc: new Date(dueDate).toISOString(),
        lineItems,
      });

      await billingService.issueInvoice(created.id);
      toast.success('Invoice created and issued.');
      navigate(`/billing/invoices/${created.id}`);
    } catch {
      toast.error('Failed to create and issue invoice.');
    } finally {
      setSaving(false);
    }
  };

  const handleIssue = async () => {
    if (!id) return;
    setSaving(true);
    try {
      await billingService.issueInvoice(id);
      toast.success('Invoice issued.');
      void loadInvoice();
    } catch {
      toast.error('Failed to issue invoice.');
    } finally {
      setSaving(false);
    }
  };

  const handleMarkPaid = async () => {
    if (!id) return;
    setSaving(true);
    try {
      await billingService.markPaid(id);
      toast.success('Invoice marked as paid.');
      void loadInvoice();
    } catch {
      toast.error('Failed to mark invoice as paid.');
    } finally {
      setSaving(false);
    }
  };

  const handleCancel = async () => {
    if (!id) return;
    setSaving(true);
    try {
      await billingService.cancelInvoice(id);
      toast.success('Invoice cancelled.');
      void loadInvoice();
    } catch {
      toast.error('Failed to cancel invoice.');
    } finally {
      setSaving(false);
    }
  };

  // ── Customer name resolution ──────────────────────────────────────

  const selectedCustomerName = useMemo(() => {
    if (!customerId) return '';
    return customers.find((c) => c.partyId === customerId)?.displayName ?? customerId;
  }, [customerId, customers]);

  // ── Breadcrumbs ───────────────────────────────────────────────────
  if (loading) {
    return (
      <div className="h-full overflow-auto p-6">
        <p className="text-sm text-[var(--color-text-tertiary)]">Loading invoice...</p>
      </div>
    );
  }

  return (
    <div className="h-full overflow-auto p-6">

      {/* Header */}
      <div className="flex items-start justify-between gap-4 mb-6">
        <div className="flex items-center gap-3">
          <Button
            size="sm"
            variant="ghost"
            onClick={() => navigate('/billing/invoices')}
          >
            <ArrowLeft className="w-4 h-4" />
          </Button>
          <div>
            <div className="flex items-center gap-2">
              <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">
                {isCreate ? 'New Invoice' : `Invoice #${invoice?.invoiceNumber?.slice(0, 8) ?? ''}`}
              </h1>
              {!isCreate && (
                <Badge className={STATUS_STYLES[status] ?? STATUS_STYLES.Draft}>
                  {status}
                </Badge>
              )}
            </div>
            {!isCreate && invoice && (
              <p className="text-sm text-[var(--color-text-secondary)] mt-0.5">
                Created {formatDate(invoice.issuedUtc)}
              </p>
            )}
          </div>
        </div>
      </div>

      {/* Two-column layout */}
      <div className="grid grid-cols-1 lg:grid-cols-5 gap-6">
        {/* Left column — Form */}
        <div className="lg:col-span-3 space-y-6">
          {/* Invoice Details */}
          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="text-sm">Invoice Details</CardTitle>
            </CardHeader>
            <CardContent>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                {!isCreate && (
                  <div>
                    <Label className="text-xs text-[var(--color-text-tertiary)]">Invoice Number</Label>
                    <Input
                      value={invoice?.invoiceNumber ?? ''}
                      disabled
                      className="mt-1 font-mono"
                    />
                  </div>
                )}
                <div>
                  <Label className="text-xs text-[var(--color-text-tertiary)]">Currency</Label>
                  <Select
                    value={currency}
                    onValueChange={setCurrency}
                    disabled={isReadOnly}
                  >
                    <SelectTrigger className="mt-1">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {CURRENCIES.map((c) => (
                        <SelectItem key={c} value={c}>
                          {c}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div>
                  <Label className="text-xs text-[var(--color-text-tertiary)]">Due Date</Label>
                  <Input
                    type="date"
                    value={dueDate}
                    onChange={(e) => setDueDate(e.target.value)}
                    disabled={isReadOnly}
                    className="mt-1"
                  />
                </div>
              </div>
            </CardContent>
          </Card>

          {/* Customer */}
          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="text-sm">Customer</CardTitle>
            </CardHeader>
            <CardContent>
              {isReadOnly ? (
                <div className="text-sm text-[var(--color-text-primary)]">
                  {selectedCustomerName || 'Unknown customer'}
                </div>
              ) : (
                <Select
                  value={customerId}
                  onValueChange={setCustomerId}
                  disabled={!isCreate || customersLoading}
                >
                  <SelectTrigger>
                    <SelectValue placeholder={customersLoading ? 'Loading customers...' : 'Select a customer'} />
                  </SelectTrigger>
                  <SelectContent>
                    {customers.map((c) => (
                      <SelectItem key={c.partyId} value={c.partyId}>
                        {c.displayName}
                        {c.primaryEmail && (
                          <span className="text-[var(--color-text-tertiary)] ml-2">
                            {c.primaryEmail}
                          </span>
                        )}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              )}
            </CardContent>
          </Card>

          {/* Line Items */}
          <Card>
            <CardHeader className="pb-3">
              <div className="flex items-center justify-between">
                <CardTitle className="text-sm">Line Items</CardTitle>
                {!isReadOnly && (
                  <Button size="sm" variant="ghost" onClick={addLine} className="h-7 text-xs">
                    <Plus className="w-3 h-3 mr-1" />
                    Add item
                  </Button>
                )}
              </div>
            </CardHeader>
            <CardContent>
              {/* Column headers */}
              <div className="grid grid-cols-12 gap-3 mb-2 text-xs font-medium text-[var(--color-text-tertiary)] uppercase tracking-wide">
                <div className="col-span-5">Description</div>
                <div className="col-span-2">Qty</div>
                <div className="col-span-2">Unit Price</div>
                <div className="col-span-2 text-right">Total</div>
                <div className="col-span-1" />
              </div>

              {/* Line rows */}
              <div className="space-y-2">
                {lines.map((line, index) => {
                  const lineTotal = line.quantity * line.unitPrice;
                  return (
                    <div
                      key={index}
                      className="grid grid-cols-12 gap-3 items-center"
                    >
                      <div className="col-span-5">
                        <Input
                          value={line.description}
                          onChange={(e) =>
                            updateLine(index, { description: e.target.value })
                          }
                          placeholder="Item description"
                          disabled={isReadOnly}
                          className="text-sm"
                        />
                      </div>
                      <div className="col-span-2">
                        <Input
                          type="number"
                          value={line.quantity}
                          onChange={(e) =>
                            updateLine(index, {
                              quantity: parseFloat(e.target.value) || 0,
                            })
                          }
                          min={0.01}
                          step={0.01}
                          disabled={isReadOnly}
                          className="text-sm"
                        />
                      </div>
                      <div className="col-span-2">
                        <Input
                          type="number"
                          value={line.unitPrice}
                          onChange={(e) =>
                            updateLine(index, {
                              unitPrice: parseFloat(e.target.value) || 0,
                            })
                          }
                          min={0}
                          step={0.01}
                          disabled={isReadOnly}
                          className="text-sm"
                        />
                      </div>
                      <div className="col-span-2 text-right text-sm font-medium text-[var(--color-text-primary)]">
                        {formatMoney(lineTotal, currency)}
                      </div>
                      <div className="col-span-1 flex justify-center">
                        {!isReadOnly && lines.length > 1 && (
                          <Button
                            size="sm"
                            variant="ghost"
                            className="h-7 w-7 p-0 text-[var(--color-text-tertiary)] hover:text-red-500"
                            onClick={() => removeLine(index)}
                          >
                            <Trash2 className="w-3.5 h-3.5" />
                          </Button>
                        )}
                      </div>
                    </div>
                  );
                })}
              </div>
            </CardContent>
          </Card>

          {/* Summary + Discount */}
          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="text-sm">Summary</CardTitle>
            </CardHeader>
            <CardContent>
              <div className="space-y-3">
                <div className="flex items-center justify-between text-sm">
                  <span className="text-[var(--color-text-secondary)]">Subtotal</span>
                  <span className="text-[var(--color-text-primary)]">
                    {formatMoney(subtotal, currency)}
                  </span>
                </div>
                <div className="flex items-center justify-between text-sm">
                  <span className="text-[var(--color-text-secondary)]">Discount</span>
                  {isReadOnly ? (
                    <span className="text-[var(--color-text-primary)]">
                      -{formatMoney(discount, currency)}
                    </span>
                  ) : (
                    <Input
                      type="number"
                      value={discount}
                      onChange={(e) => setDiscount(parseFloat(e.target.value) || 0)}
                      min={0}
                      step={0.01}
                      className="w-32 text-sm text-right"
                    />
                  )}
                </div>
                <div className="flex items-center justify-between text-sm">
                  <span className="text-[var(--color-text-secondary)]">Tax</span>
                  <span className="text-[var(--color-text-tertiary)]">
                    {formatMoney(0, currency)}
                  </span>
                </div>
                <div className="border-t border-[var(--color-border-light)] pt-3 flex items-center justify-between">
                  <span className="text-base font-semibold text-[var(--color-text-primary)]">
                    Total
                  </span>
                  <span className="text-xl font-bold text-[var(--color-text-primary)]">
                    {formatMoney(isCreate ? total : (invoice?.totalAmount ?? total), currency)}
                  </span>
                </div>
              </div>
            </CardContent>
          </Card>

          {/* Action buttons */}
          <div className="flex items-center gap-3">
            {isCreate && (
              <>
                <Button onClick={() => void handleSaveDraft()} disabled={saving}>
                  {saving ? 'Saving...' : 'Save as Draft'}
                </Button>
                <Button variant="secondary" onClick={() => void handleSaveAndIssue()} disabled={saving}>
                  <Send className="w-4 h-4 mr-2" />
                  Save & Issue
                </Button>
              </>
            )}
            {!isCreate && isDraft && (
              <>
                <Button onClick={() => void handleIssue()} disabled={saving}>
                  <Send className="w-4 h-4 mr-2" />
                  Issue Invoice
                </Button>
                <Button
                  variant="outline"
                  className="text-red-600 border-red-200 hover:bg-red-50"
                  onClick={() => void handleCancel()}
                  disabled={saving}
                >
                  <XCircle className="w-4 h-4 mr-2" />
                  Cancel Invoice
                </Button>
              </>
            )}
            {!isCreate && status === 'Issued' && (
              <>
                <Button onClick={() => void handleMarkPaid()} disabled={saving}>
                  <CheckCircle2 className="w-4 h-4 mr-2" />
                  Mark as Paid
                </Button>
                <Button
                  variant="outline"
                  className="text-red-600 border-red-200 hover:bg-red-50"
                  onClick={() => void handleCancel()}
                  disabled={saving}
                >
                  <XCircle className="w-4 h-4 mr-2" />
                  Cancel Invoice
                </Button>
              </>
            )}
          </div>
        </div>

        {/* Right column — Live Preview */}
        <div className="lg:col-span-2">
          <div className="sticky top-6">
            <Card>
              <CardContent className="pt-6 pb-6">
                {/* Preview header */}
                <div className="flex items-center justify-between mb-6">
                  <div>
                    <div className="text-xs uppercase tracking-wide text-[var(--color-text-tertiary)] mb-1">
                      Invoice
                    </div>
                    <div className="text-lg font-bold text-[var(--color-text-primary)]">
                      {invoice ? `#${invoice.invoiceNumber.slice(0, 8)}` : '#New'}
                    </div>
                  </div>
                  <Badge className={STATUS_STYLES[status] ?? STATUS_STYLES.Draft}>
                    {status}
                  </Badge>
                </div>

                {/* Dates */}
                <div className="grid grid-cols-2 gap-4 mb-6 text-xs">
                  <div>
                    <div className="text-[var(--color-text-tertiary)]">Issue Date</div>
                    <div className="text-[var(--color-text-primary)] mt-0.5">
                      {invoice ? formatDate(invoice.issuedUtc) : formatDate(new Date().toISOString())}
                    </div>
                  </div>
                  <div>
                    <div className="text-[var(--color-text-tertiary)]">Due Date</div>
                    <div className="text-[var(--color-text-primary)] mt-0.5">
                      {formatDate(new Date(dueDate).toISOString())}
                    </div>
                  </div>
                </div>

                {/* Customer */}
                <div className="mb-6">
                  <div className="text-xs text-[var(--color-text-tertiary)] mb-1">Bill To</div>
                  <div className="text-sm font-medium text-[var(--color-text-primary)]">
                    {selectedCustomerName || 'No customer selected'}
                  </div>
                </div>

                {/* Line items table */}
                <div className="border border-[var(--color-border-light)] rounded-sm overflow-hidden mb-6">
                  <table className="w-full text-xs">
                    <thead>
                      <tr className="bg-[var(--color-surface-inset)]">
                        <th className="text-left p-2 font-medium text-[var(--color-text-tertiary)]">
                          Item
                        </th>
                        <th className="text-right p-2 font-medium text-[var(--color-text-tertiary)]">
                          Qty
                        </th>
                        <th className="text-right p-2 font-medium text-[var(--color-text-tertiary)]">
                          Price
                        </th>
                        <th className="text-right p-2 font-medium text-[var(--color-text-tertiary)]">
                          Total
                        </th>
                      </tr>
                    </thead>
                    <tbody>
                      {lines
                        .filter((l) => l.description.trim())
                        .map((line, i) => (
                          <tr
                            key={i}
                            className="border-t border-[var(--color-border-light)]"
                          >
                            <td className="p-2 text-[var(--color-text-primary)]">
                              {line.description}
                            </td>
                            <td className="p-2 text-right text-[var(--color-text-secondary)]">
                              {line.quantity}
                            </td>
                            <td className="p-2 text-right text-[var(--color-text-secondary)]">
                              {formatMoney(line.unitPrice, currency)}
                            </td>
                            <td className="p-2 text-right font-medium text-[var(--color-text-primary)]">
                              {formatMoney(line.quantity * line.unitPrice, currency)}
                            </td>
                          </tr>
                        ))}
                      {lines.every((l) => !l.description.trim()) && (
                        <tr>
                          <td
                            colSpan={4}
                            className="p-4 text-center text-[var(--color-text-tertiary)]"
                          >
                            No items yet
                          </td>
                        </tr>
                      )}
                    </tbody>
                  </table>
                </div>

                {/* Totals */}
                <div className="space-y-2 text-xs">
                  <div className="flex justify-between">
                    <span className="text-[var(--color-text-tertiary)]">Subtotal</span>
                    <span className="text-[var(--color-text-primary)]">
                      {formatMoney(subtotal, currency)}
                    </span>
                  </div>
                  {discount > 0 && (
                    <div className="flex justify-between">
                      <span className="text-[var(--color-text-tertiary)]">Discount</span>
                      <span className="text-red-500">
                        -{formatMoney(discount, currency)}
                      </span>
                    </div>
                  )}
                  <div className="border-t border-[var(--color-border-light)] pt-2 flex justify-between">
                    <span className="text-sm font-semibold text-[var(--color-text-primary)]">
                      Total
                    </span>
                    <span className="text-sm font-bold text-[var(--color-text-primary)]">
                      {formatMoney(isCreate ? total : (invoice?.totalAmount ?? total), currency)}
                    </span>
                  </div>
                </div>
              </CardContent>
            </Card>
          </div>
        </div>
      </div>
    </div>
  );
}
