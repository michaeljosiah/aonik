import { useState } from 'react';
import type { ReactNode } from 'react';
import { Copy, Plus, Save, TestTube2 } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { cn } from '@/lib/utils';

type ProviderTone = 'success' | 'warning' | 'outline';

interface Provider {
  id: string;
  name: string;
  desc: string;
  status: string;
  tone: ProviderTone;
  logo: string;
  color: string;
  volume: string;
  fee: string;
  region: string;
}

const providers: Provider[] = [
  { id: 'stripe', name: 'Stripe', desc: 'Cards · ACH · SEPA in 40 countries', status: 'Active', tone: 'success', logo: 'S', color: '#635bff', volume: '£312K', fee: '1.4% + 20p', region: 'Global' },
  { id: 'paystack', name: 'Paystack', desc: 'Cards · bank · USSD · QR — Nigeria', status: 'Active', tone: 'success', logo: 'P', color: '#00c3f7', volume: '₦142M', fee: '1.5%', region: 'NGN' },
  { id: 'wise', name: 'Wise Business', desc: 'Multi-currency payouts in 50+ currencies', status: 'Active', tone: 'success', logo: 'W', color: '#9fe870', volume: '£204K', fee: '0.43%', region: 'Global' },
  { id: 'flw', name: 'Flutterwave', desc: 'Cards · bank · mobile money — Africa', status: 'Sandbox', tone: 'warning', logo: 'F', color: '#f5a623', volume: '—', fee: '1.4%', region: 'NGN · KES' },
  { id: 'ach', name: 'Modern Treasury', desc: 'ACH origination, RTP — US', status: 'Disabled', tone: 'outline', logo: 'M', color: '#1e2228', volume: '—', fee: 'flat $0.50', region: 'USD' },
];

function Kpi({ label, value, delta }: { label: string; value: string; delta?: string }) {
  return (
    <div className="rounded-xl border border-[var(--color-border-light)] bg-[var(--color-surface)] p-4">
      <p className="text-[11px] font-medium text-[var(--color-text-tertiary)]">{label}</p>
      <div className="mt-1 flex items-baseline gap-2">
        <p className="text-xl font-semibold text-[var(--color-text-primary)]">{value}</p>
        {delta ? <span className="text-xs font-medium text-[var(--color-success)]">{delta}</span> : null}
      </div>
    </div>
  );
}

function SettingsSection({ title, description, children, action }: { title: string; description?: string; children: ReactNode; action?: ReactNode }) {
  return (
    <section className="mb-4 rounded-xl border border-[var(--color-border-light)] bg-[var(--color-surface)]">
      <div className="flex items-start justify-between gap-4 border-b border-[var(--color-border-light)] px-5 py-4">
        <div>
          <h2 className="text-sm font-semibold text-[var(--color-text-primary)]">{title}</h2>
          {description ? <p className="mt-1 max-w-2xl text-xs leading-5 text-[var(--color-text-secondary)]">{description}</p> : null}
        </div>
        {action}
      </div>
      <div className="space-y-4 p-5">{children}</div>
    </section>
  );
}

function Field({ label, code, help, children }: { label: string; code?: string; help?: string; children: ReactNode }) {
  return (
    <div className="grid gap-3 lg:grid-cols-[260px_minmax(0,1fr)] lg:gap-6">
      <div>
        <p className="text-[13px] font-medium text-[var(--color-text-primary)]">{label}</p>
        {code ? <p className="mt-1 font-mono text-[10.5px] text-[var(--color-text-tertiary)]">{code}</p> : null}
        {help ? <p className="mt-1.5 text-[11.5px] leading-5 text-[var(--color-text-tertiary)]">{help}</p> : null}
      </div>
      <div>{children}</div>
    </div>
  );
}

export function SettingsPaymentGatewaysPage() {
  const [selectedId, setSelectedId] = useState('stripe');
  const provider = providers.find((item) => item.id === selectedId) ?? providers[0];

  return (
    <div className="flex h-full min-h-0">
      <aside className="w-80 flex-none overflow-auto border-r border-[var(--color-border-light)] bg-[var(--color-surface-inset)] p-[18px]">
        <h1 className="text-[17px] font-semibold text-[var(--color-text-primary)]">Payment gateways</h1>
        <p className="mt-1 mb-4 text-[12.5px] leading-5 text-[var(--color-text-secondary)]">
          Configure providers, routing, and credentials per region.
        </p>
        <Button size="sm" className="mb-4 w-full justify-center gap-1.5">
          <Plus className="h-3 w-3" />
          Add provider
        </Button>
        <div className="flex flex-col gap-1.5">
          {providers.map((item) => {
            const active = item.id === selectedId;
            return (
              <button
                key={item.id}
                type="button"
                onClick={() => setSelectedId(item.id)}
                className={cn(
                  'flex items-center gap-2.5 rounded-[10px] border p-3 text-left transition-colors',
                  active ? 'border-[var(--color-brand-primary)] bg-[var(--color-surface)]' : 'border-transparent hover:bg-[var(--color-surface)]'
                )}
              >
                <span className="grid h-8 w-8 flex-none place-items-center rounded-md text-[13px] font-bold text-white" style={{ backgroundColor: item.color }}>{item.logo}</span>
                <span className="min-w-0 flex-1">
                  <span className="flex items-center justify-between gap-1.5">
                    <span className="truncate text-[13px] font-semibold text-[var(--color-text-primary)]">{item.name}</span>
                    <Badge variant={item.tone} className="gap-1 text-[10px]"><span className="h-1.5 w-1.5 rounded-full bg-current" />{item.status}</Badge>
                  </span>
                  <span className="mt-0.5 block truncate text-[11px] text-[var(--color-text-secondary)]">{item.desc}</span>
                </span>
              </button>
            );
          })}
        </div>
      </aside>

      <main className="min-w-0 flex-1 overflow-auto px-8 py-6">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <p className="mb-1 text-[11px] font-semibold uppercase tracking-[0.1em] text-[var(--color-text-tertiary)]">Settings · Gateways · {provider.region}</p>
            <h2 className="text-2xl font-bold text-[var(--color-text-primary)]">{provider.name}</h2>
            <p className="text-[var(--color-text-secondary)]">{provider.desc}</p>
          </div>
          <div className="flex gap-2">
            <Button variant="outline" size="sm" className="gap-1.5"><TestTube2 className="h-3 w-3" />Test connection</Button>
            <Button variant="outline" size="sm">Disable</Button>
            <Button size="sm" className="gap-1.5"><Save className="h-3 w-3" />Save</Button>
          </div>
        </div>

        <div className="mt-5 grid gap-3 md:grid-cols-2 xl:grid-cols-4">
          <Kpi label="Volume · 30d" value={provider.volume} delta="+18%" />
          <Kpi label="Avg fee" value={provider.fee} />
          <Kpi label="Success rate" value="99.4%" delta="+0.2pp" />
          <Kpi label="Last failure" value="2h ago" />
        </div>

        <div className="mt-4">
          <SettingsSection
            title="Credentials"
            description={`API credentials for ${provider.name}. Keys are encrypted at rest and never exposed to the browser.`}
            action={<Badge variant="success" className="gap-1"><span className="h-1.5 w-1.5 rounded-full bg-current" />Encrypted at rest</Badge>}
          >
            <Field label="Mode" code="Gateway.Mode" help="Live mode routes real money. Test mode uses sandbox endpoints.">
              <div className="inline-flex rounded-lg bg-[var(--color-surface-inset)] p-1">
                <span className="rounded-md bg-[var(--color-surface)] px-4 py-1.5 text-xs font-medium text-[var(--color-text-primary)] shadow-sm">Live</span>
                <span className="px-4 py-1.5 text-xs font-medium text-[var(--color-text-secondary)]">Test</span>
              </div>
            </Field>
            <Field label="Publishable key" code={`${provider.id}.publishable_key`}>
              <div className="flex gap-1.5">
                <Input value={`pk_live_51M${provider.id}9aBcD3eFgHiJkL...`} readOnly className="font-mono text-xs" />
                <Button variant="outline" size="sm"><Copy className="h-3 w-3" /></Button>
              </div>
            </Field>
            <Field label="Secret key" code={`${provider.id}.secret_key`}>
              <Input type="password" value="••••••••••••••••••••" readOnly className="font-mono text-xs" />
            </Field>
            <Field label="Webhook signing secret" code={`${provider.id}.webhook_secret`} help="Required for verifying incoming webhook integrity.">
              <Input type="password" value="••••••••••••••••••••" readOnly className="font-mono text-xs" />
            </Field>
            <Field label="Webhook URL" code={`${provider.id}.webhook_url`}>
              <div className="flex gap-1.5">
                <Input value={`https://api.aonik.com/webhooks/${provider.id}/primrose`} readOnly className="font-mono text-xs" />
                <Button variant="outline" size="sm">Copy</Button>
              </div>
            </Field>
          </SettingsSection>

          <SettingsSection title="Routing" description="Decides which provider receives a payment, by currency and method.">
            <Field label="Default for GBP"><Input defaultValue="Stripe (live)" /></Field>
            <Field label="Default for NGN" help="Native NGN settlement; auto-converted to GBP at end-of-day."><Input defaultValue="Paystack" /></Field>
            <Field label="Default for USD payouts"><Input defaultValue="Wise Business" /></Field>
            <Field label="Fallback strategy" help="If the default provider fails, retry on this one."><Input defaultValue="Wise Business → Modern Treasury" /></Field>
          </SettingsSection>

          <SettingsSection title="Limits & risk" description="Per-transaction thresholds and risk gates.">
            <Field label="Max single payment" code={`${provider.id}.limits.max_payment`}><Input defaultValue="£250,000.00" className="max-w-48 font-mono" /></Field>
            <Field label="Daily volume cap" code={`${provider.id}.limits.daily_cap`}><Input defaultValue="£2,000,000.00" className="max-w-48 font-mono" /></Field>
            <Field label="3DS challenge" code={`${provider.id}.risk.3ds`} help="Trigger 3D Secure on cards above the threshold."><Input defaultValue="Above £500" /></Field>
            <Field label="Block country list" code={`${provider.id}.risk.blocklist`}><Input defaultValue="IR, KP, RU, SY" className="font-mono" /></Field>
          </SettingsSection>
        </div>
      </main>
    </div>
  );
}
