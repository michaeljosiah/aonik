import { BookOpen, CheckCircle2, GitBranch, LockKeyhole, Search } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';

const toolGroups = [
  {
    title: 'Billing',
    tools: [
      { name: 'CreateInvoice', version: 'v3', mode: 'Approval required', enabled: true, desc: 'Draft an invoice with line items and tax metadata.' },
      { name: 'IssueInvoice', version: 'v2', mode: 'Approval required', enabled: true, desc: 'Issue a draft invoice and notify the customer.' },
      { name: 'MarkInvoicePaid', version: 'v1', mode: 'Approval required', enabled: false, desc: 'Mark an invoice as settled after payment proof.' },
    ],
  },
  {
    title: 'Payments',
    tools: [
      { name: 'CreatePaymentIntent', version: 'v2', mode: 'Approval required', enabled: true, desc: 'Create funding intent for an order.' },
      { name: 'CapturePayment', version: 'v2', mode: 'Approval required', enabled: true, desc: 'Capture authorized funds through the selected gateway.' },
      { name: 'CancelPayment', version: 'v1', mode: 'Approval required', enabled: true, desc: 'Cancel a pending payment intent.' },
    ],
  },
  {
    title: 'Read tools',
    tools: [
      { name: 'GetCustomerProfile', version: 'v4', mode: 'Read direct', enabled: true, desc: 'Resolve customer and party references for agent context.' },
      { name: 'SearchLedgerEntries', version: 'v2', mode: 'Read direct', enabled: true, desc: 'Search ledger facts without mutating financial state.' },
      { name: 'ListOpenApprovals', version: 'v1', mode: 'Read direct', enabled: true, desc: 'Inspect pending human-in-the-loop requests.' },
    ],
  },
];

export function SettingsToolCatalogPage() {
  return (
    <div className="h-full overflow-auto px-8 py-6">

      <div className="mb-6 flex flex-wrap items-start justify-between gap-3">
        <div>
          <p className="mb-1 text-[11px] font-semibold uppercase tracking-[0.1em] text-[var(--color-text-tertiary)]">Settings · AI</p>
          <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Tool Catalog</h1>
          <p className="max-w-3xl text-[var(--color-text-secondary)]">
            Browse, enable, and version the tools available to agents. Mutating tools remain approval-gated.
          </p>
        </div>
        <div className="flex gap-2">
          <div className="relative">
            <Search className="absolute left-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-[var(--color-text-tertiary)]" />
            <Input placeholder="Search tools..." className="h-8 w-56 pl-8" />
          </div>
          <Button variant="outline" size="sm" className="gap-1.5"><BookOpen className="h-3.5 w-3.5" />Docs</Button>
        </div>
      </div>

      <div className="mb-4 grid gap-3 md:grid-cols-3">
        <div className="rounded-xl border border-[var(--color-border-light)] bg-[var(--color-surface)] p-4">
          <p className="text-[11px] text-[var(--color-text-tertiary)]">Enabled tools</p>
          <p className="mt-1 text-2xl font-semibold text-[var(--color-text-primary)]">8</p>
        </div>
        <div className="rounded-xl border border-[var(--color-border-light)] bg-[var(--color-surface)] p-4">
          <p className="text-[11px] text-[var(--color-text-tertiary)]">Approval-gated</p>
          <p className="mt-1 text-2xl font-semibold text-[var(--color-text-primary)]">6</p>
        </div>
        <div className="rounded-xl border border-[var(--color-border-light)] bg-[var(--color-surface)] p-4">
          <p className="text-[11px] text-[var(--color-text-tertiary)]">Published versions</p>
          <p className="mt-1 text-2xl font-semibold text-[var(--color-text-primary)]">21</p>
        </div>
      </div>

      <div className="space-y-4">
        {toolGroups.map((group) => (
          <section key={group.title} className="rounded-xl border border-[var(--color-border-light)] bg-[var(--color-surface)]">
            <div className="border-b border-[var(--color-border-light)] px-5 py-4">
              <h2 className="text-sm font-semibold text-[var(--color-text-primary)]">{group.title}</h2>
            </div>
            <div className="divide-y divide-[var(--color-border-light)]">
              {group.tools.map((tool) => (
                <div key={tool.name} className="flex flex-wrap items-center justify-between gap-4 px-5 py-4">
                  <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <h3 className="font-mono text-sm font-semibold text-[var(--color-text-primary)]">{tool.name}</h3>
                      <Badge variant="secondary" className="gap-1"><GitBranch className="h-3 w-3" />{tool.version}</Badge>
                      <Badge variant={tool.mode === 'Read direct' ? 'success' : 'warning'} className="gap-1">
                        {tool.mode === 'Read direct' ? <CheckCircle2 className="h-3 w-3" /> : <LockKeyhole className="h-3 w-3" />}
                        {tool.mode}
                      </Badge>
                    </div>
                    <p className="mt-1 text-xs leading-5 text-[var(--color-text-secondary)]">{tool.desc}</p>
                  </div>
                  <button
                    type="button"
                    className={`h-5 w-9 rounded-full p-0.5 transition-colors ${tool.enabled ? 'bg-[var(--color-brand-primary)]' : 'bg-[var(--color-border)]'}`}
                    aria-label={`${tool.enabled ? 'Disable' : 'Enable'} ${tool.name}`}
                  >
                    <span className={`block h-4 w-4 rounded-full bg-white transition-transform ${tool.enabled ? 'translate-x-4' : ''}`} />
                  </button>
                </div>
              ))}
            </div>
          </section>
        ))}
      </div>
    </div>
  );
}
