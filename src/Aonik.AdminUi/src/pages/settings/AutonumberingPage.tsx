import { Hash } from 'lucide-react';
import { useMemo, useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';

type AutonumberProfile = {
  id: string;
  name: string;
  entityType: string;
  strategy: string;
  resetPolicy: string;
  paddingLength: number;
  range: string;
  lastIssued: string;
  status: 'Active' | 'Paused';
};

const profiles: AutonumberProfile[] = [
  {
    id: 'inv-default',
    name: 'Invoice Default',
    entityType: 'Invoice',
    strategy: 'Sequential',
    resetPolicy: 'Monthly',
    paddingLength: 4,
    range: '1 - 9,999',
    lastIssued: 'INV-2026-0421',
    status: 'Active',
  },
  {
    id: 'order-standard',
    name: 'Order Standard',
    entityType: 'Order',
    strategy: 'Sequential',
    resetPolicy: 'Yearly',
    paddingLength: 6,
    range: '1 - 999,999',
    lastIssued: 'ORD-2026-000932',
    status: 'Active',
  },
  {
    id: 'credit-note',
    name: 'Credit Note',
    entityType: 'CreditNote',
    strategy: 'Sequential',
    resetPolicy: 'None',
    paddingLength: 5,
    range: '100 - 99,999',
    lastIssued: 'CRN-01042',
    status: 'Paused',
  },
];

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

export function AutonumberingPage() {
  const [entityType, setEntityType] = useState('Invoice');
  const [prefix, setPrefix] = useState('INV-{YYYY}-');
  const [suffix, setSuffix] = useState('');
  const [paddingLength, setPaddingLength] = useState('4');
  const [sequenceValue, setSequenceValue] = useState('421');
  const [resetPolicy, setResetPolicy] = useState('Monthly');
  const [strategy, setStrategy] = useState('Sequential');

  const preview = useMemo(() => {
    const padding = Number.parseInt(paddingLength, 10);
    const nextValue = Number.parseInt(sequenceValue, 10);
    const safePadding = Number.isNaN(padding) ? 0 : Math.max(padding, 0);
    const safeValue = Number.isNaN(nextValue) ? 0 : Math.max(nextValue, 0);
    const date = new Date();
    const padded = safePadding > 0 ? safeValue.toString().padStart(safePadding, '0') : safeValue.toString();
    return `${tokenizedDate(prefix, date)}${padded}${tokenizedDate(suffix, date)}`;
  }, [paddingLength, prefix, sequenceValue, suffix]);

  return (
    <div className="h-full overflow-auto p-6 space-y-6">
      <Breadcrumb
        items={[
          { label: 'Settings', href: '/settings', icon: <Hash className="w-3.5 h-3.5" /> },
          { label: 'Autonumbering' },
        ]}
      />

      <div className="flex flex-col gap-2">
        <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Autonumbering</h1>
        <p className="text-[var(--color-text-secondary)]">
          Configure and validate reference sequences for invoices, orders, and other financial documents.
        </p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Configurations</CardTitle>
          <CardDescription>Active tenant-scoped numbering profiles and last issued references.</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="text-left text-[var(--color-text-tertiary)] border-b border-[var(--color-border)]">
                  <th className="py-2 pr-4 font-medium">Name</th>
                  <th className="py-2 pr-4 font-medium">Entity</th>
                  <th className="py-2 pr-4 font-medium">Strategy</th>
                  <th className="py-2 pr-4 font-medium">Reset</th>
                  <th className="py-2 pr-4 font-medium">Range</th>
                  <th className="py-2 pr-4 font-medium">Last Issued</th>
                  <th className="py-2 pr-4 font-medium">Status</th>
                </tr>
              </thead>
              <tbody>
                {profiles.map((profile) => (
                  <tr
                    key={profile.id}
                    className="border-b border-[var(--color-border-light)] text-[var(--color-text-primary)]"
                  >
                    <td className="py-3 pr-4 font-medium">{profile.name}</td>
                    <td className="py-3 pr-4">{profile.entityType}</td>
                    <td className="py-3 pr-4">{profile.strategy}</td>
                    <td className="py-3 pr-4">{profile.resetPolicy}</td>
                    <td className="py-3 pr-4">{profile.range}</td>
                    <td className="py-3 pr-4">{profile.lastIssued}</td>
                    <td className="py-3 pr-4">
                      <Badge variant={profile.status === 'Active' ? 'secondary' : 'outline'}>
                        {profile.status}
                      </Badge>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Test a Reference</CardTitle>
          <CardDescription>
            Validate tokens, padding, and reset rules without issuing a live reference.
          </CardDescription>
        </CardHeader>
        <CardContent>
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
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="strategy">Strategy</Label>
              <Select value={strategy} onValueChange={setStrategy}>
                <SelectTrigger id="strategy">
                  <SelectValue placeholder="Select strategy" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Sequential">Sequential</SelectItem>
                  <SelectItem value="Random">Random</SelectItem>
                  <SelectItem value="Hybrid">Hybrid</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="reset-policy">Reset Policy</Label>
              <Select value={resetPolicy} onValueChange={setResetPolicy}>
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
            <Button variant="default">Run Test</Button>
            <span className="text-xs text-[var(--color-text-tertiary)]">
              Preview uses the current date with tokens {`{YYYY}`}, {`{MM}`}, {`{DD}`}.
            </span>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
