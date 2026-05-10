import { useMemo, useState } from 'react';
import {
  ArrowDown,
  Cog,
  Loader2,
  Mic,
  Plug,
  Save,
  Send,
  SpeakerIcon,
  Speaker,
  Sparkles,
  Webhook,
} from 'lucide-react';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { SheetBody, SheetFooter, SheetHeader } from '@/components/ui/sheet';
import { Switch } from '@/components/ui/switch';
import { voiceRecipeLibraryService } from '@/services/voiceRecipeLibraryService';
import type { SpeechProvider, SpeechProviderType } from '@/types/speechLibrary';
import type {
  ChainedRecipeBody,
  CompositeRecipeBody,
  VoiceRecipe,
  VoiceRecipeKind,
} from '@/types/voiceRecipes';

interface RecipeStackEditorProps {
  /** Set when editing or cloning. Null = "Add recipe" form. */
  initial: VoiceRecipe | null;
  defaultKind: VoiceRecipeKind;
  /** Provider library for dropdown population. */
  providers: SpeechProvider[];
  onSaved: (saved: VoiceRecipe) => void;
  onCancel: () => void;
}

/**
 * Stack-of-cards recipe editor (spec 024 §"Tab 2"). Renders the chain top-to-bottom with one
 * card per pipeline step; STT and TTS step cards have provider dropdowns sourced from the
 * library. The visual format is intentionally linear — ReactFlow canvas authoring is deferred
 * to v1.2 if/when branching topology arrives.
 */
export function RecipeStackEditor({
  initial,
  defaultKind,
  providers,
  onSaved,
  onCancel,
}: RecipeStackEditorProps) {
  const isEditing = initial !== null;

  const [kind, setKind] = useState<VoiceRecipeKind>(initial?.kind ?? defaultKind);
  const [displayName, setDisplayName] = useState<string>(initial?.displayName ?? '');
  const [description, setDescription] = useState<string>(initial?.description ?? '');

  // Chained body state — populated whether or not Kind is currently Chained, so flipping kind
  // doesn't lose the user's earlier inputs.
  const [chainedSttId, setChainedSttId] = useState<string>(
    initial?.chained?.sttProviderId ?? defaultProviderId(providers, 'Stt'),
  );
  const [chainedTtsId, setChainedTtsId] = useState<string>(
    initial?.chained?.ttsProviderId ?? defaultProviderId(providers, 'Tts'),
  );
  const [pinnedAgentMode, setPinnedAgentMode] = useState<'use-client' | 'pin'>(
    initial?.chained?.pinnedAgentId || initial?.composite?.pinnedAgentId ? 'pin' : 'use-client',
  );
  const [pinnedAgentId, setPinnedAgentId] = useState<string>(
    initial?.chained?.pinnedAgentId ?? initial?.composite?.pinnedAgentId ?? '',
  );
  const [vad, setVad] = useState<string>(initial?.chained?.vad ?? 'energy');
  const [vadStopMs, setVadStopMs] = useState<string>(
    initial?.chained?.vadStopMs?.toString() ?? '800',
  );
  const [transcriptionFilter, setTranscriptionFilter] = useState<boolean>(
    initial?.chained?.transcriptionFilter ?? true,
  );
  const [sentenceAggregator, setSentenceAggregator] = useState<boolean>(
    initial?.chained?.sentenceAggregator ?? true,
  );

  // Composite body state.
  const [compositeProviderId, setCompositeProviderId] = useState<string>(
    initial?.composite?.compositeProviderId ?? defaultProviderId(providers, 'Composite'),
  );

  const [saving, setSaving] = useState(false);

  // Group providers by type for dropdown rendering.
  const sttProviders = useMemo(() => providers.filter((p) => p.type === 'Stt'), [providers]);
  const ttsProviders = useMemo(() => providers.filter((p) => p.type === 'Tts'), [providers]);
  const compositeProviders = useMemo(
    () => providers.filter((p) => p.type === 'Composite'),
    [providers],
  );

  const handleSave = async () => {
    if (!displayName.trim()) {
      toast.error('Display name is required.');
      return;
    }

    const chained: ChainedRecipeBody | null =
      kind === 'Chained'
        ? {
            sttProviderId: chainedSttId,
            ttsProviderId: chainedTtsId,
            pinnedAgentId: pinnedAgentMode === 'pin' && pinnedAgentId.trim() ? pinnedAgentId.trim() : null,
            vad,
            vadStopMs: vadStopMs.trim() ? Number.parseInt(vadStopMs, 10) : null,
            transcriptionFilter,
            sentenceAggregator,
          }
        : null;

    const composite: CompositeRecipeBody | null =
      kind === 'Composite'
        ? {
            compositeProviderId,
            pinnedAgentId: pinnedAgentMode === 'pin' && pinnedAgentId.trim() ? pinnedAgentId.trim() : null,
          }
        : null;

    setSaving(true);
    try {
      let saved: VoiceRecipe;
      if (isEditing) {
        saved = await voiceRecipeLibraryService.update(initial!.id, {
          displayName: displayName.trim(),
          description: description.trim() || null,
          chained,
          composite,
        });
      } else {
        saved = await voiceRecipeLibraryService.create({
          displayName: displayName.trim(),
          description: description.trim() || null,
          kind,
          chained,
          composite,
        });
      }
      toast.success(`Recipe "${saved.displayName}" saved.`);
      onSaved(saved);
    } catch (err) {
      const message =
        (err as { response?: { data?: { error?: string } } })?.response?.data?.error ??
        (err as { message?: string })?.message ??
        'Failed to save recipe.';
      toast.error(message);
    } finally {
      setSaving(false);
    }
  };

  const headerTitle = isEditing ? 'Edit recipe' : 'Add recipe';
  const headerSubtitle = 'Compose a voice pipeline. Pick providers from your library and tune per-step options.';

  return (
    <>
      <SheetHeader
        icon={<Webhook className="h-4 w-4" />}
        title={headerTitle}
        subtitle={headerSubtitle}
      />
      <SheetBody className="gap-5">
        {/* Header: name + description + kind */}
        <div className="grid gap-3 md:grid-cols-2">
          <div className="space-y-2">
            <Label htmlFor="recipe-display-name">Display name</Label>
            <Input
              id="recipe-display-name"
              value={displayName}
              onChange={(e) => setDisplayName(e.target.value)}
              placeholder="e.g. My cost-efficient chain"
              maxLength={200}
            />
          </div>
          <div className="space-y-2">
            <Label>Kind</Label>
            <Select
              value={kind}
              onValueChange={(v) => setKind(v as VoiceRecipeKind)}
              disabled={isEditing}
            >
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="Chained">Chained (STT → agent → TTS)</SelectItem>
                <SelectItem value="Composite">Composite (single-vendor realtime)</SelectItem>
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2 md:col-span-2">
            <Label htmlFor="recipe-description">Description (optional)</Label>
            <Input
              id="recipe-description"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="What this recipe is for"
              maxLength={1000}
            />
          </div>
        </div>

        {/* Pipeline stack */}
        {kind === 'Chained' ? (
          <ChainedStack
            sttProviders={sttProviders}
            ttsProviders={ttsProviders}
            chainedSttId={chainedSttId}
            setChainedSttId={setChainedSttId}
            chainedTtsId={chainedTtsId}
            setChainedTtsId={setChainedTtsId}
            pinnedAgentMode={pinnedAgentMode}
            setPinnedAgentMode={setPinnedAgentMode}
            pinnedAgentId={pinnedAgentId}
            setPinnedAgentId={setPinnedAgentId}
            vad={vad}
            setVad={setVad}
            vadStopMs={vadStopMs}
            setVadStopMs={setVadStopMs}
            transcriptionFilter={transcriptionFilter}
            setTranscriptionFilter={setTranscriptionFilter}
            sentenceAggregator={sentenceAggregator}
            setSentenceAggregator={setSentenceAggregator}
          />
        ) : (
          <CompositeStack
            compositeProviders={compositeProviders}
            compositeProviderId={compositeProviderId}
            setCompositeProviderId={setCompositeProviderId}
            pinnedAgentMode={pinnedAgentMode}
            setPinnedAgentMode={setPinnedAgentMode}
            pinnedAgentId={pinnedAgentId}
            setPinnedAgentId={setPinnedAgentId}
          />
        )}

      </SheetBody>
      <SheetFooter className="justify-end">
        <Button variant="outline" size="sm" onClick={onCancel} disabled={saving}>
          Cancel
        </Button>
        <Button size="sm" onClick={() => void handleSave()} disabled={saving}>
          {saving ? <Loader2 className="h-4 w-4 animate-spin" /> : <Save className="h-4 w-4" />}
          {isEditing ? 'Save changes' : 'Create'}
        </Button>
      </SheetFooter>
    </>
  );
}

// ── Chained stack ────────────────────────────────────────────────────────────

interface ChainedStackProps {
  sttProviders: SpeechProvider[];
  ttsProviders: SpeechProvider[];
  chainedSttId: string;
  setChainedSttId: (v: string) => void;
  chainedTtsId: string;
  setChainedTtsId: (v: string) => void;
  pinnedAgentMode: 'use-client' | 'pin';
  setPinnedAgentMode: (v: 'use-client' | 'pin') => void;
  pinnedAgentId: string;
  setPinnedAgentId: (v: string) => void;
  vad: string;
  setVad: (v: string) => void;
  vadStopMs: string;
  setVadStopMs: (v: string) => void;
  transcriptionFilter: boolean;
  setTranscriptionFilter: (v: boolean) => void;
  sentenceAggregator: boolean;
  setSentenceAggregator: (v: boolean) => void;
}

function ChainedStack(props: ChainedStackProps) {
  return (
    <div className="space-y-0">
      <StepCard step={1} icon={<Webhook className="h-4 w-4" />} title="Audio input" readonly>
        <p className="text-xs text-muted-foreground">
          WebSocket binary frames · 16-bit PCM @ 16 kHz
        </p>
      </StepCard>

      <StepConnector />

      <StepCard step={2} icon={<Mic className="h-4 w-4" />} title="Speech-to-text">
        <div className="space-y-3">
          <div className="space-y-1.5">
            <Label>Provider</Label>
            <Select value={props.chainedSttId} onValueChange={props.setChainedSttId}>
              <SelectTrigger>
                <SelectValue placeholder="Pick an STT provider…" />
              </SelectTrigger>
              <SelectContent>
                {props.sttProviders.map((p) => (
                  <SelectItem key={p.id} value={p.id}>
                    {p.displayName}
                    {p.isBuiltIn && ' (built-in)'}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="grid gap-3 md:grid-cols-2">
            <div className="space-y-1.5">
              <Label>VAD</Label>
              <Select value={props.vad} onValueChange={props.setVad}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="energy">Energy gate (default)</SelectItem>
                  <SelectItem value="silero">Silero ML</SelectItem>
                  <SelectItem value="none">None (vendor handles segmentation)</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="vad-stop-ms">Stop silence (ms)</Label>
              <Input
                id="vad-stop-ms"
                type="number"
                value={props.vadStopMs}
                onChange={(e) => props.setVadStopMs(e.target.value)}
                placeholder="800"
              />
            </div>
          </div>
          <div className="flex items-center justify-between rounded-md border p-2">
            <Label htmlFor="filter-toggle" className="text-sm">
              Drop hallucinations (Whisper "Thank you" / "Bye" / ".")
            </Label>
            <Switch
              id="filter-toggle"
              checked={props.transcriptionFilter}
              onCheckedChange={props.setTranscriptionFilter}
            />
          </div>
        </div>
      </StepCard>

      <StepConnector />

      <StepCard step={3} icon={<Sparkles className="h-4 w-4" />} title="Voice agent">
        <AgentRoutingControls
          mode={props.pinnedAgentMode}
          setMode={props.setPinnedAgentMode}
          pinnedAgentId={props.pinnedAgentId}
          setPinnedAgentId={props.setPinnedAgentId}
        />
      </StepCard>

      <StepConnector />

      <StepCard step={4} icon={<Cog className="h-4 w-4" />} title="Sentence aggregator">
        <div className="flex items-center justify-between rounded-md border p-2">
          <div className="text-xs">
            Buffer LLM tokens into whole sentences before TTS. Lets TTS speak the first
            sentence while the LLM is still generating.
          </div>
          <Switch
            checked={props.sentenceAggregator}
            onCheckedChange={props.setSentenceAggregator}
          />
        </div>
      </StepCard>

      <StepConnector />

      <StepCard step={5} icon={<Speaker className="h-4 w-4" />} title="Text-to-speech">
        <div className="space-y-1.5">
          <Label>Provider</Label>
          <Select value={props.chainedTtsId} onValueChange={props.setChainedTtsId}>
            <SelectTrigger>
              <SelectValue placeholder="Pick a TTS provider…" />
            </SelectTrigger>
            <SelectContent>
              {props.ttsProviders.map((p) => (
                <SelectItem key={p.id} value={p.id}>
                  {p.displayName}
                  {p.isBuiltIn && ' (built-in)'}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      </StepCard>

      <StepConnector />

      <StepCard step={6} icon={<Send className="h-4 w-4" />} title="Audio output" readonly>
        <p className="text-xs text-muted-foreground">
          WebSocket binary frames · 24 kHz PCM
        </p>
      </StepCard>
    </div>
  );
}

// ── Composite stack ──────────────────────────────────────────────────────────

interface CompositeStackProps {
  compositeProviders: SpeechProvider[];
  compositeProviderId: string;
  setCompositeProviderId: (v: string) => void;
  pinnedAgentMode: 'use-client' | 'pin';
  setPinnedAgentMode: (v: 'use-client' | 'pin') => void;
  pinnedAgentId: string;
  setPinnedAgentId: (v: string) => void;
}

function CompositeStack(props: CompositeStackProps) {
  return (
    <div className="space-y-0">
      <StepCard step={1} icon={<Webhook className="h-4 w-4" />} title="Audio input" readonly>
        <p className="text-xs text-muted-foreground">
          WebSocket binary frames · raw PCM
        </p>
      </StepCard>

      <StepConnector />

      <StepCard step={2} icon={<Plug className="h-4 w-4" />} title="Composite provider">
        <div className="space-y-3">
          <div className="space-y-1.5">
            <Label>Provider</Label>
            <Select value={props.compositeProviderId} onValueChange={props.setCompositeProviderId}>
              <SelectTrigger>
                <SelectValue placeholder="Pick a composite provider…" />
              </SelectTrigger>
              <SelectContent>
                {props.compositeProviders.map((p) => (
                  <SelectItem key={p.id} value={p.id}>
                    {p.displayName}
                    {p.isBuiltIn && ' (built-in)'}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <p className="text-xs text-muted-foreground">
              Composite providers handle STT + agent + TTS + VAD inside the vendor's own
              realtime API. Configure the vendor + voice + instructions on the provider entry
              itself.
            </p>
          </div>
          <AgentRoutingControls
            mode={props.pinnedAgentMode}
            setMode={props.setPinnedAgentMode}
            pinnedAgentId={props.pinnedAgentId}
            setPinnedAgentId={props.setPinnedAgentId}
          />
        </div>
      </StepCard>

      <StepConnector />

      <StepCard step={3} icon={<Send className="h-4 w-4" />} title="Audio output" readonly>
        <p className="text-xs text-muted-foreground">
          WebSocket binary frames · vendor sample rate
        </p>
      </StepCard>
    </div>
  );
}

// ── Reusable step primitives ─────────────────────────────────────────────────

function StepCard({
  step,
  icon,
  title,
  readonly,
  children,
}: {
  step: number;
  icon: React.ReactNode;
  title: string;
  readonly?: boolean;
  children: React.ReactNode;
}) {
  return (
    <div className="rounded-md border bg-muted/20 p-3">
      <div className="mb-2 flex items-center gap-2">
        <Badge variant="outline" className="font-mono text-[10px]">
          Step {step}
        </Badge>
        <div className="flex h-5 w-5 items-center justify-center text-muted-foreground">
          {icon}
        </div>
        <span className="text-sm font-medium">{title}</span>
        {readonly && <Badge variant="secondary">read-only</Badge>}
      </div>
      <div>{children}</div>
    </div>
  );
}

function StepConnector() {
  return (
    <div className="flex justify-center py-1">
      <ArrowDown className="h-4 w-4 text-muted-foreground" />
    </div>
  );
}

function AgentRoutingControls({
  mode,
  setMode,
  pinnedAgentId,
  setPinnedAgentId,
}: {
  mode: 'use-client' | 'pin';
  setMode: (v: 'use-client' | 'pin') => void;
  pinnedAgentId: string;
  setPinnedAgentId: (v: string) => void;
}) {
  return (
    <div className="space-y-2">
      <Label className="text-sm font-medium">Agent routing</Label>
      <div className="space-y-1">
        <label className="flex items-center gap-2 text-sm">
          <input
            type="radio"
            checked={mode === 'use-client'}
            onChange={() => setMode('use-client')}
          />
          Use the client's requested agent (default)
        </label>
        <label className="flex items-center gap-2 text-sm">
          <input
            type="radio"
            checked={mode === 'pin'}
            onChange={() => setMode('pin')}
          />
          Pin to a specific agent
        </label>
      </div>
      {mode === 'pin' && (
        <Input
          value={pinnedAgentId}
          onChange={(e) => setPinnedAgentId(e.target.value)}
          placeholder="agent id, e.g. personal-finance-agent"
        />
      )}
    </div>
  );
}

function defaultProviderId(providers: SpeechProvider[], type: SpeechProviderType): string {
  return providers.find((p) => p.type === type)?.id ?? '';
}

// Suppress an unused import warning until lucide-react re-exports stabilise.
const _unused: typeof SpeakerIcon = SpeakerIcon;
void _unused;
