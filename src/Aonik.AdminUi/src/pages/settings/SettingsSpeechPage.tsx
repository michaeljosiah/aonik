import { useEffect, useState } from 'react';
import {
  Layers,
  Mic,
  Radio,
  Speaker,
  type LucideIcon,
} from 'lucide-react';

import { cn } from '@/lib/utils';

import { ChatSpeechTab } from './speech/ChatSpeechTab';
import { ProvidersTab } from './speech/ProvidersTab';
import { RecipesTab } from './speech/RecipesTab';
import { VoiceModeTab } from './speech/VoiceModeTab';

/**
 * Consolidated Speech & Voice settings page (spec 024). Inner left-rail layout
 * adapted from `Templates/aonik-admin-starterkit/screens/speech.jsx`:
 *
 *   ┌── 240px rail ──┐┌─────── content ───────┐
 *   │ Settings · AI  ││  PageHeader           │
 *   │ Speech & Voice ││  KPI / banner         │
 *   │ ─ Providers    ││  Filter pills         │
 *   │ ─ Recipes      ││  Cards / sections     │
 *   │ ─ Voice mode   ││                       │
 *   │ ─ Chat speech  ││                       │
 *   │ ── now active ─│└───────────────────────┘
 *   └────────────────┘
 *
 * Providers + Recipes are wired to the new library backend (Phase A + B).
 * Voice mode + Chat speech are visual previews — Phase C ships the persistence
 * layer that backs them; the legacy `/settings/voice` and
 * `/settings/text-to-speech` pages remain functional in the meantime.
 */

type TabId = 'providers' | 'recipes' | 'voice-mode' | 'chat-speech';

interface SpeechTab {
  id: TabId;
  label: string;
  description: string;
  icon: LucideIcon;
}

const TABS: SpeechTab[] = [
  {
    id: 'providers',
    label: 'Providers',
    description: 'STT, TTS, and realtime services',
    icon: Layers,
  },
  {
    id: 'recipes',
    label: 'Recipes',
    description: 'Reusable voice configurations',
    icon: Radio,
  },
  {
    id: 'voice-mode',
    label: 'Voice mode',
    description: 'Live spoken conversations',
    icon: Mic,
  },
  {
    id: 'chat-speech',
    label: 'Chat speech',
    description: 'Speak chat responses aloud',
    icon: Speaker,
  },
];

export function SettingsSpeechPage() {
  // Persist active tab to URL query so deep links work (e.g.
  // `/settings/speech?tab=voice-mode` from a "Configure" link in another panel).
  const [activeTab, setActiveTab] = useState<TabId>(() => {
    if (typeof window === 'undefined') return 'providers';
    const params = new URLSearchParams(window.location.search);
    const candidate = params.get('tab') as TabId | null;
    return TABS.some((t) => t.id === candidate) ? (candidate as TabId) : 'providers';
  });

  useEffect(() => {
    if (typeof window === 'undefined') return;
    const url = new URL(window.location.href);
    url.searchParams.set('tab', activeTab);
    window.history.replaceState(null, '', url.toString());
  }, [activeTab]);

  return (
    <div className="flex h-full min-h-0">
      {/* Inner left rail */}
      <aside className="flex w-[240px] shrink-0 flex-col border-r border-[var(--color-border-light)] bg-[var(--color-surface-inset)] p-5">
        <p className="mb-2 text-[10px] font-semibold uppercase tracking-[0.1em] text-[var(--color-text-tertiary)]">
          Settings · AI
        </p>
        <h2 className="text-base font-semibold text-[var(--color-text-primary)]">Speech &amp; Voice</h2>
        <p className="mt-1 mb-4 text-xs leading-relaxed text-[var(--color-text-secondary)]">
          Configure the providers, recipes, and live experiences that power voice in this workspace.
        </p>

        <nav className="flex flex-col gap-0.5">
          {TABS.map((tab) => {
            const active = tab.id === activeTab;
            return (
              <button
                key={tab.id}
                type="button"
                onClick={() => setActiveTab(tab.id)}
                className={cn(
                  'flex items-start gap-2.5 rounded-md px-3 py-2.5 text-left transition-colors',
                  active
                    ? 'bg-[var(--color-brand-primary-10)] text-[var(--color-brand-primary)]'
                    : 'text-[var(--color-text-primary)] hover:bg-[var(--color-surface)]',
                )}
              >
                <tab.icon
                  className={cn(
                    'mt-0.5 h-3.5 w-3.5 shrink-0',
                    active ? 'text-[var(--color-brand-primary)]' : 'text-[var(--color-text-secondary)]',
                  )}
                />
                <div className="min-w-0 flex-1">
                  <div className={cn('text-[13px]', active ? 'font-semibold' : 'font-medium')}>
                    {tab.label}
                  </div>
                  <div
                    className={cn(
                      'mt-0.5 text-[11px]',
                      active
                        ? 'text-[var(--color-brand-primary)]/85'
                        : 'text-[var(--color-text-tertiary)]',
                    )}
                  >
                    {tab.description}
                  </div>
                </div>
              </button>
            );
          })}
        </nav>

        {/* "Now active" footer — placeholder until Phase C wires real settings */}
        <div className="mt-auto border-t border-[var(--color-border-light)] pt-4">
          <p className="mb-2 text-[10px] font-semibold uppercase tracking-[0.08em] text-[var(--color-text-tertiary)]">
            Now active
          </p>
          <div className="flex items-center gap-1.5 text-xs text-[var(--color-text-primary)]">
            <span className="h-1.5 w-1.5 rounded-full bg-[var(--color-brand-primary)]" />
            Voice mode · <span className="font-semibold">Premium chained</span>
          </div>
          <div className="mt-1 flex items-center gap-1.5 text-xs text-[var(--color-text-primary)]">
            <span className="h-1.5 w-1.5 rounded-full bg-[var(--color-brand-primary)]" />
            Chat speech · <span className="font-semibold">Aria</span>
          </div>
        </div>
      </aside>

      {/* Right column */}
      <div className="min-w-0 flex-1 overflow-auto p-8">
        {activeTab === 'providers' && <ProvidersTab />}
        {activeTab === 'recipes' && <RecipesTab />}
        {activeTab === 'voice-mode' && <VoiceModeTab onJump={(id) => setActiveTab(id)} />}
        {activeTab === 'chat-speech' && <ChatSpeechTab onJump={(id) => setActiveTab(id)} />}
      </div>
    </div>
  );
}

export type { TabId };
