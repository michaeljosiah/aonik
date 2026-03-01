import type { ElementType } from 'react';
import { Link } from 'react-router-dom';
import {
  ArrowRightLeft,
  ArrowUpRight,
  Cog,
  Hash,
  KeyRound,
  ScrollText,
  SlidersHorizontal,
  Webhook,
  Wrench,
} from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Breadcrumb } from '@/components/ui/breadcrumb';
import { Card, CardContent } from '@/components/ui/card';

type SettingsSection = 'Platform' | 'Finance';

interface SettingsTile {
  section: SettingsSection;
  title: string;
  description: string;
  href: string;
  icon: ElementType;
  badge: string;
}

const settingsTiles: SettingsTile[] = [
  {
    section: 'Platform',
    title: 'General',
    description: 'Manage workspace profile, timezone, locale, and approval controls.',
    href: '/settings/general',
    icon: SlidersHorizontal,
    badge: 'Core',
  },
  {
    section: 'Platform',
    title: 'Webhooks',
    description: 'Configure endpoint delivery, signing secrets, and event subscriptions.',
    href: '/settings/webhooks',
    icon: Webhook,
    badge: 'Integration',
  },
  {
    section: 'Platform',
    title: 'API Keys',
    description: 'Create and revoke credentials for programmatic platform access.',
    href: '/settings/api-keys',
    icon: KeyRound,
    badge: 'Security',
  },
  {
    section: 'Platform',
    title: 'Audit Logs',
    description: 'Review operator actions, authentication events, and security decisions.',
    href: '/settings/audit-logs',
    icon: ScrollText,
    badge: 'Observability',
  },
  {
    section: 'Platform',
    title: 'System Tools',
    description: 'Run maintenance utilities such as cache invalidation and seed operations.',
    href: '/settings/system-tools',
    icon: Wrench,
    badge: 'Ops',
  },
  {
    section: 'Finance',
    title: 'FX Rates',
    description: 'Manage FX quote sources and maintain exchange rate governance.',
    href: '/settings/fx-rates',
    icon: ArrowRightLeft,
    badge: 'Pricing',
  },
  {
    section: 'Finance',
    title: 'Autonumbering',
    description: 'Control reference generation strategy and sequence profiles.',
    href: '/settings/autonumbering',
    icon: Hash,
    badge: 'References',
  },
];

function SettingsTileGrid({ title, tiles }: { title: string; tiles: SettingsTile[] }) {
  return (
    <div>
      <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-[var(--color-text-tertiary)]">{title}</h2>
      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
        {tiles.map((tile) => (
          <Link key={tile.title} to={tile.href} className="group">
            <Card className="h-full transition-all duration-200 group-hover:-translate-y-0.5 group-hover:shadow-md">
              <CardContent className="p-5">
                <div className="mb-3 flex items-center justify-between">
                  <div className="flex h-10 w-10 items-center justify-center rounded-md bg-[var(--color-brand-primary-light)]">
                    <tile.icon className="h-5 w-5 text-[var(--color-brand-primary)]" />
                  </div>
                  <ArrowUpRight className="h-4 w-4 text-[var(--color-text-tertiary)]" />
                </div>
                <h3 className="mb-2 text-lg font-semibold text-[var(--color-text-primary)]">{tile.title}</h3>
                <p className="mb-4 text-sm text-[var(--color-text-secondary)]">{tile.description}</p>
                <Badge variant="secondary">{tile.badge}</Badge>
              </CardContent>
            </Card>
          </Link>
        ))}
      </div>
    </div>
  );
}

export function SettingsLandingPage() {
  const platformTiles = settingsTiles.filter((tile) => tile.section === 'Platform');
  const financeTiles = settingsTiles.filter((tile) => tile.section === 'Finance');

  return (
    <div className="h-full overflow-auto p-6">
      <Breadcrumb items={[{ label: 'Settings', icon: <Cog className="h-3.5 w-3.5" /> }]} className="mb-4" />

      <div className="mb-6">
        <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Settings</h1>
        <p className="text-[var(--color-text-secondary)]">
          Centralized controls for workspace behavior, integration security, and operational governance.
        </p>
      </div>

      <div className="space-y-8">
        <SettingsTileGrid title="Platform Settings" tiles={platformTiles} />
        <SettingsTileGrid title="Finance Settings" tiles={financeTiles} />
      </div>
    </div>
  );
}
