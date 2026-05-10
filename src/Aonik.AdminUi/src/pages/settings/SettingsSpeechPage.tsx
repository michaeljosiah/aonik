import { useCallback, useEffect, useState } from 'react';
import {
  Layers,
  Mic,
  Radio,
  Speaker,
  type LucideIcon,
} from 'lucide-react';

import { cn } from '@/lib/utils';
import { chatSpeechSettingsService, voiceModeSettingsService } from '@/services/speechActiveSettingsService';
import { speechProviderLibraryService } from '@/services/speechProviderLibraryService';
import { voiceRecipeLibraryService } from '@/services/voiceRecipeLibraryService';

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
 * Phase A + B wired the Providers + Recipes library; Phase C.1 shipped the
 * `VoiceModeSettings` + `ChatSpeechSettings` singletons that drive the "Now active"
 * rail footer and the Voice mode / Chat speech tabs. Phase C.2 (now live) wires the
 * runtime: the WebSocket voice pipeline reads the active recipe from `VoiceModeSettings`,
 * and `TextToSpeechService` overlays `ChatSpeechSettings.ActiveTtsProviderId` on top of
 * the legacy default. The legacy `/settings/voice` + `/settings/text-to-speech` pages
 * remain for credential management and as a fallback profile.
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

interface ActiveStatus {
  enabled: boolean;
  /** Display name when the row is enabled AND a target is selected; null otherwise. */
  name: string | null;
}

interface ActiveLabels {
  voiceMode: ActiveStatus;
  chatSpeech: ActiveStatus;
}

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

  // "Now active" footer state. Tabs call onSettingsChanged when they save so the
  // footer can re-resolve names without us threading state down or up.
  const [refreshTick, setRefreshTick] = useState(0);
  const [activeLabels, setActiveLabels] = useState<ActiveLabels>({
    voiceMode: { enabled: false, name: null },
    chatSpeech: { enabled: false, name: null },
  });

  useEffect(() => {
    let cancelled = false;
    const load = async () => {
      try {
        const [voiceMode, chatSpeech, recipes, providers] = await Promise.all([
          voiceModeSettingsService.get(),
          chatSpeechSettingsService.get(),
          voiceRecipeLibraryService.list({ includeDisabled: true }),
          speechProviderLibraryService.list({ includeDisabled: true }),
        ]);
        if (cancelled) return;
        const recipe = voiceMode.activeRecipeId
          ? recipes.find((r) => r.id === voiceMode.activeRecipeId)
          : undefined;
        const provider = chatSpeech.activeTtsProviderId
          ? providers.find((p) => p.id === chatSpeech.activeTtsProviderId)
          : undefined;
        setActiveLabels({
          voiceMode: { enabled: voiceMode.enabled, name: recipe?.displayName ?? null },
          chatSpeech: { enabled: chatSpeech.enabled, name: provider?.displayName ?? null },
        });
      } catch {
        // Footer is decorative — silent failure is fine; next save attempt will retry.
      }
    };
    void load();
    return () => {
      cancelled = true;
    };
  }, [refreshTick]);

  const handleSettingsChanged = useCallback(() => {
    setRefreshTick((t) => t + 1);
  }, []);

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

        {/* "Now active" footer — pulled live from VoiceModeSettings + ChatSpeechSettings. */}
        <div className="mt-auto border-t border-[var(--color-border-light)] pt-4">
          <p className="mb-2 text-[10px] font-semibold uppercase tracking-[0.08em] text-[var(--color-text-tertiary)]">
            Now active
          </p>
          <ActiveFooterRow
            label="Voice mode"
            status={activeLabels.voiceMode}
            emptyLabel="no recipe"
          />
          <ActiveFooterRow
            label="Chat speech"
            status={activeLabels.chatSpeech}
            emptyLabel="no voice"
          />
        </div>
      </aside>

      {/* Right column */}
      <div className="min-w-0 flex-1 overflow-auto p-8">
        {activeTab === 'providers' && <ProvidersTab />}
        {activeTab === 'recipes' && (
          <RecipesTab settingsTick={refreshTick} onSettingsChanged={handleSettingsChanged} />
        )}
        {activeTab === 'voice-mode' && (
          <VoiceModeTab onJump={(id) => setActiveTab(id)} onSettingsChanged={handleSettingsChanged} />
        )}
        {activeTab === 'chat-speech' && (
          <ChatSpeechTab onJump={(id) => setActiveTab(id)} onSettingsChanged={handleSettingsChanged} />
        )}
      </div>
    </div>
  );
}

function ActiveFooterRow({
  label,
  status,
  emptyLabel,
}: {
  label: string;
  status: ActiveStatus;
  /** Shown when the row is enabled but no target is selected. */
  emptyLabel: string;
}) {
  const dotOn = status.enabled && status.name !== null;
  let trailing: React.ReactNode;
  if (!status.enabled) {
    trailing = <span className="text-[var(--color-text-tertiary)]">off</span>;
  } else if (status.name === null) {
    trailing = <span className="italic text-[var(--color-text-tertiary)]">{emptyLabel}</span>;
  } else {
    trailing = <span className="font-semibold">{status.name}</span>;
  }
  return (
    <div className="mt-1 flex items-start gap-1.5 text-xs first:mt-0">
      <span
        className={cn(
          'mt-1 h-1.5 w-1.5 shrink-0 rounded-full',
          dotOn ? 'bg-[var(--color-brand-primary)]' : 'bg-[var(--color-text-tertiary)]',
        )}
      />
      <span className="min-w-0 text-[var(--color-text-primary)]">
        {label} · {trailing}
      </span>
    </div>
  );
}

export type { TabId };
