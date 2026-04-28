import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import {
  ArrowUpRight,
  AudioLines,
  ShieldCheck,
  SlidersHorizontal,
} from 'lucide-react';

import { AonikTemplateIcon } from '@/components/layout/aonik/AonikTemplateIcon';
import { Badge } from '@/components/ui/badge';
import { Breadcrumb } from '@/components/ui/breadcrumb';

type SettingsSection = 'Platform' | 'Finance' | 'AI & Agents';

interface SettingsTile {
  section: SettingsSection;
  title: string;
  description: string;
  href: string;
  icon: ReactNode;
  badge: string;
}

const settingsTiles: SettingsTile[] = [
  {
    section: 'Platform',
    title: 'Platform Settings',
    description: 'Workspace profile, AI provider, storage, communication, feature flags, and platform configuration.',
    href: '/settings/global',
    icon: <AonikTemplateIcon name="settings" size={18} />,
    badge: 'Configuration',
  },
  {
    section: 'Platform',
    title: 'Authentication',
    description: 'Identity providers, SSO, OAuth callbacks, and management client credentials.',
    href: '/settings/authentication',
    icon: <AonikTemplateIcon name="shield" size={18} />,
    badge: 'Security',
  },
  {
    section: 'Platform',
    title: 'Audit Logs',
    description: 'Review operator actions, authentication events, and security decisions.',
    href: '/settings/audit-logs',
    icon: <AonikTemplateIcon name="invoice" size={18} />,
    badge: 'Observability',
  },
  {
    section: 'Platform',
    title: 'System Tools',
    description: 'Run maintenance utilities such as cache invalidation and seed operations.',
    href: '/settings/system-tools',
    icon: <AonikTemplateIcon name="wrench" size={18} />,
    badge: 'Ops',
  },
  {
    section: 'Finance',
    title: 'FX Rates',
    description: 'Manage FX quote sources and maintain exchange rate governance.',
    href: '/settings/fx-rates',
    icon: <AonikTemplateIcon name="arrows" size={18} />,
    badge: 'Pricing',
  },
  {
    section: 'Finance',
    title: 'Autonumbering',
    description: 'Control reference generation strategy and sequence profiles.',
    href: '/settings/autonumbering',
    icon: <AonikTemplateIcon name="invoice" size={18} />,
    badge: 'References',
  },
  {
    section: 'AI & Agents',
    title: 'Text-to-Speech',
    description: 'Manage TTS provider credentials, voice selection, preview playback, and usage limits.',
    href: '/settings/text-to-speech',
    icon: <AudioLines className="h-[18px] w-[18px]" />,
    badge: 'AI',
  },
  {
    section: 'AI & Agents',
    title: 'AI Policies',
    description: 'Approval thresholds, kill switch, model policy routing, and tool governance.',
    href: '/ai/policies',
    icon: <ShieldCheck className="h-[18px] w-[18px]" />,
    badge: 'Governance',
  },
];

function SettingsTileGrid({ title, tiles }: { title: string; tiles: SettingsTile[] }) {
  return (
    <div>
      <h2 className="mb-3 text-[11px] font-semibold uppercase tracking-[0.1em] text-[var(--color-text-tertiary)]">{title}</h2>
      <div className="grid gap-4 xl:grid-cols-3">
        {tiles.map((tile) => (
          <Link
            key={tile.title}
            to={tile.href}
            className="group flex h-full flex-col gap-3 rounded-xl border border-[var(--color-border-light)] bg-[var(--color-surface)] p-5 transition-all duration-150 hover:-translate-y-0.5 hover:shadow-md"
          >
            <div className="flex items-center justify-between">
              <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-[var(--color-brand-primary-light)] text-[var(--color-brand-primary)]">
                {tile.icon}
              </div>
              <ArrowUpRight className="h-4 w-4 text-[var(--color-text-tertiary)]" />
            </div>
            <div>
              <h3 className="mb-1 text-[15px] font-semibold text-[var(--color-text-primary)]">{tile.title}</h3>
              <p className="text-[13px] leading-6 text-[var(--color-text-secondary)]">{tile.description}</p>
            </div>
            <div>
              <Badge variant="secondary">{tile.badge}</Badge>
            </div>
          </Link>
        ))}
      </div>
    </div>
  );
}

export function SettingsLandingPage() {
  const platformTiles = settingsTiles.filter((tile) => tile.section === 'Platform');
  const financeTiles = settingsTiles.filter((tile) => tile.section === 'Finance');
  const aiTiles = settingsTiles.filter((tile) => tile.section === 'AI & Agents');

  return (
    <div className="h-full overflow-auto p-6">
      <Breadcrumb items={[{ label: 'Settings', icon: <SlidersHorizontal className="h-3.5 w-3.5" /> }]} className="mb-4" />

      <div className="mb-7">
        <p className="mb-1 text-[11px] font-semibold uppercase tracking-[0.1em] text-[var(--color-text-tertiary)]">Admin</p>
        <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Settings</h1>
        <p className="max-w-3xl text-[var(--color-text-secondary)]">
          Centralized controls for workspace behavior, integration security, and operational governance.
        </p>
      </div>

      <div className="space-y-8">
        <SettingsTileGrid title="Platform" tiles={platformTiles} />
        <SettingsTileGrid title="Finance" tiles={financeTiles} />
        <SettingsTileGrid title="AI & Agents" tiles={aiTiles} />
      </div>
    </div>
  );
}
