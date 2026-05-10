import { Edit3, Mic, Plug, Speaker, TestTube2, Trash2 } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import type { SpeechProvider, SpeechProviderType } from '@/types/speechLibrary';

interface ProviderCardProps {
  provider: SpeechProvider;
  onEdit: () => void;
  onTest: () => void;
  onDisable: () => void;
}

/**
 * One row in the Providers tab. Shows display name + vendor + config summary +
 * status badge + action buttons. Built-in archetypes get a "Built-in · Clone to edit"
 * badge instead of an Edit button.
 */
export function ProviderCard({ provider, onEdit, onTest, onDisable }: ProviderCardProps) {
  return (
    <Card className="transition-colors hover:border-primary/40">
      <CardContent className="flex items-center gap-4 p-4">
        <TypeIcon type={provider.type} />

        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-2">
            <span className="font-medium truncate">{provider.displayName}</span>
            <StatusBadge provider={provider} />
          </div>
          <div className="mt-0.5 truncate text-xs text-muted-foreground">
            {provider.vendor} · {summariseConfig(provider)}
          </div>
        </div>

        <div className="flex shrink-0 items-center gap-2">
          {provider.type !== 'Composite' && (
            <Button variant="ghost" size="sm" onClick={onTest}>
              <TestTube2 className="h-4 w-4" />
              Test
            </Button>
          )}
          <Button variant="ghost" size="sm" onClick={onEdit}>
            <Edit3 className="h-4 w-4" />
            {provider.isBuiltIn ? 'Clone' : 'Edit'}
          </Button>
          {!provider.isBuiltIn && provider.status === 'Active' && (
            <Button variant="ghost" size="sm" onClick={onDisable}>
              <Trash2 className="h-4 w-4" />
              Disable
            </Button>
          )}
        </div>
      </CardContent>
    </Card>
  );
}

function TypeIcon({ type }: { type: SpeechProviderType }) {
  const palette: Record<SpeechProviderType, string> = {
    Stt: 'bg-blue-100 text-blue-700 dark:bg-blue-950/30 dark:text-blue-300',
    Tts: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/30 dark:text-emerald-300',
    Composite: 'bg-purple-100 text-purple-700 dark:bg-purple-950/30 dark:text-purple-300',
  };
  const Icon = type === 'Stt' ? Mic : type === 'Tts' ? Speaker : Plug;
  return (
    <div
      className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-md ${palette[type]}`}
    >
      <Icon className="h-4 w-4" />
    </div>
  );
}

function StatusBadge({ provider }: { provider: SpeechProvider }) {
  if (provider.isBuiltIn) {
    return <Badge variant="outline">Built-in</Badge>;
  }
  if (provider.status === 'Disabled') {
    return <Badge variant="secondary">Disabled</Badge>;
  }
  if (provider.status === 'SoftDeleted') {
    return <Badge variant="error">Deleted</Badge>;
  }
  return null;
}

/**
 * Render a one-line summary of the provider's vendor-specific config — the bit users
 * actually read at-a-glance to disambiguate "OpenAI TTS · alloy · tts-1" from
 * "OpenAI TTS HD · onyx · tts-1-hd".
 */
function summariseConfig(provider: SpeechProvider): string {
  const c = provider.config;
  switch (c.kind) {
    case 'openai-whisper':
      return [c.model ?? 'whisper-1', c.language].filter(Boolean).join(' · ');
    case 'azure-stt':
      return [c.region, c.language].filter(Boolean).join(' · ');
    case 'openai-tts':
      return [c.voiceId, c.modelId ?? 'tts-1'].filter(Boolean).join(' · ');
    case 'azure-tts':
      return [c.voiceId, c.region].filter(Boolean).join(' · ');
    case 'elevenlabs-tts':
      return [c.voiceId.slice(0, 8) + '…', c.modelId ?? 'eleven_multilingual_v2']
        .filter(Boolean)
        .join(' · ');
    case 'mistral-tts':
      return [c.voiceId, c.modelId ?? 'voxtral-tts'].filter(Boolean).join(' · ');
    case 'openai-realtime':
      return [c.voice, c.model ?? 'gpt-realtime-mini'].filter(Boolean).join(' · ');
    case 'azure-voice-live':
      return [c.region, c.voice, c.model ?? 'gpt-realtime-mini'].filter(Boolean).join(' · ');
    default: {
      // Exhaustive; satisfies the `never` check on c.
      const _exhaustive: never = c;
      return _exhaustive;
    }
  }
}
