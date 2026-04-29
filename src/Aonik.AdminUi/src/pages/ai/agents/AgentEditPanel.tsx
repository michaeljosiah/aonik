// Slide-out edit panel — port of `AgentEditPanel` from
// templates/aonik-admin-starterkit/screens/agents-page.jsx.
//
// Five tabs: Identity / Prompt / Tools / Policies / Triggers. Identity,
// Prompt and Tools wire to the live `agentConfigService`; Policies and
// Triggers render placeholder empty states because the production backend
// doesn't carry policy presets or scheduling triggers per agent yet.

import { useEffect, useState } from 'react';
import {
  Check,
  Clock,
  Globe,
  Info,
  MoreHorizontal,
  Play,
  Plus,
  RefreshCw,
  ShieldCheck,
  Sparkles,
  Trash2,
  Upload,
  X,
  Zap,
} from 'lucide-react';
import { toast } from 'sonner';

import { Button } from '@/components/ui/button';
import { Pill } from '@/components/layout/aonik';
import { cn } from '@/lib/utils';
import { agentConfigService, aiModelService } from '@/services/aiService';
import type { AgentConfigurationResponse, AiModelResponse } from '@/types/ai';

import { AgentPortrait } from './AgentPortrait';
import { StateDot } from './StateDot';
import {
  AGENT_GLYPHS,
  AGENT_PALETTE,
  countTools,
  deriveAgentColor,
  deriveAgentGlyph,
  deriveAgentState,
  deriveAutoApply,
  deriveKindLabel,
  parseToolNames,
  deriveAgentTagline,
  type AgentGlyph,
} from './agentMeta';

type Tab = 'Identity' | 'Prompt' | 'Tools' | 'Policies' | 'Triggers';
const TABS: Tab[] = ['Identity', 'Prompt', 'Tools', 'Policies', 'Triggers'];

export interface AgentEditPanelProps {
  agent: AgentConfigurationResponse;
  onClose: () => void;
  onSaved?: (agent: AgentConfigurationResponse) => void;
  onDeleted?: () => void;
  /** Initial tab (defaults to Identity). */
  initialTab?: Tab;
}

export function AgentEditPanel({
  agent: initialAgent,
  onClose,
  onSaved,
  onDeleted,
  initialTab = 'Identity',
}: AgentEditPanelProps) {
  const [agent, setAgent] = useState(initialAgent);
  const [tab, setTab] = useState<Tab>(initialTab);
  const [glyph, setGlyph] = useState<AgentGlyph>(deriveAgentGlyph(initialAgent.name));
  const [color, setColor] = useState<string>(deriveAgentColor(initialAgent.name));
  const [name] = useState(initialAgent.name); // immutable for now (server keys on it)
  const [description, setDescription] = useState(initialAgent.description ?? '');
  const [instructionsText, setInstructionsText] = useState(initialAgent.instructionsText ?? '');
  const [modelId, setModelId] = useState<string>(initialAgent.modelId ?? '');
  const [riskTier, setRiskTier] = useState<string>(initialAgent.riskTier ?? '');
  const [toolNames, setToolNames] = useState<string[]>(parseToolNames(initialAgent.toolsetIdsJson));

  const [models, setModels] = useState<AiModelResponse[]>([]);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    void aiModelService.list().then(setModels).catch(() => setModels([]));
  }, []);

  const state = deriveAgentState(agent.isActive);
  const autoApply = deriveAutoApply(riskTier);

  const handleSave = async () => {
    setSaving(true);
    try {
      const updated = await agentConfigService.upsert(agent.name, {
        description,
        instructionsText,
        toolsetIdsJson: JSON.stringify(toolNames),
        permissionsProfileJson: agent.permissionsProfileJson,
        riskTier,
        isActive: agent.isActive,
        modelId: modelId || null,
        iconUrl: agent.iconUrl ?? null,
      });
      setAgent(updated);
      onSaved?.(updated);
      toast.success('Agent saved');
    } catch (err: unknown) {
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      toast.error(message || 'Failed to save agent.');
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async () => {
    if (!agent.isOverride) {
      toast.info('Only tenant overrides can be deleted.');
      return;
    }
    if (!window.confirm(`Delete the override for "${agent.name}"? This restores the system default.`)) {
      return;
    }
    try {
      await agentConfigService.delete(agent.name);
      toast.success('Override removed');
      onDeleted?.();
      onClose();
    } catch (err: unknown) {
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      toast.error(message || 'Failed to delete override.');
    }
  };

  return (
    <>
      {/* Scrim */}
      <div
        onClick={onClose}
        className="absolute inset-0 z-40 bg-[rgba(20,25,30,0.28)]"
      />

      {/* Panel */}
      <div
        className="absolute inset-y-0 right-0 z-50 flex w-[540px] max-w-full flex-col border-l border-[var(--color-border-light)] bg-[var(--color-surface)] shadow-[-12px_0_32px_-8px_rgb(0_0_0/_0.18)]"
      >
        {/* Header */}
        <div className="flex flex-none items-center gap-3.5 border-b border-[var(--color-border-light)] px-5 py-4">
          <AgentPortrait name={name} color={color} glyph={glyph} size={52} />
          <div className="min-w-0 flex-1">
            <div className="flex items-center gap-2">
              <span className="text-[15px] font-semibold text-[var(--color-text-primary)]">{name}</span>
              <Pill tone="info" size="sm">
                {deriveKindLabel(agent.agentType)}
              </Pill>
            </div>
            <div className="mt-1 flex items-center gap-2.5">
              <StateDot state={state} />
              <span className="font-[family-name:var(--font-mono)] text-[11px] text-[var(--color-text-tertiary)]">
                · {agent.id.slice(0, 8)}
              </span>
            </div>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="grid h-8 w-8 place-items-center rounded-full text-[var(--color-text-secondary)] transition-colors hover:bg-[var(--color-surface-inset)] hover:text-[var(--color-text-primary)]"
            aria-label="Close"
          >
            <X className="h-3.5 w-3.5" />
          </button>
        </div>

        {/* Tabs */}
        <div className="flex flex-none items-center gap-1 border-b border-[var(--color-border-light)] px-5">
          {TABS.map((t) => {
            const active = t === tab;
            return (
              <button
                key={t}
                type="button"
                onClick={() => setTab(t)}
                className={cn(
                  '-mb-px border-b-2 px-3 py-2.5 text-[12px] transition-colors',
                  active
                    ? 'border-[var(--color-brand-primary)] font-semibold text-[var(--color-text-primary)]'
                    : 'border-transparent font-medium text-[var(--color-text-secondary)] hover:text-[var(--color-text-primary)]',
                )}
              >
                {t}
              </button>
            );
          })}
        </div>

        {/* Body */}
        <div className="flex-1 overflow-auto px-5 py-5">
          {tab === 'Identity' && (
            <IdentityTab
              name={name}
              description={description}
              setDescription={setDescription}
              glyph={glyph}
              setGlyph={setGlyph}
              color={color}
              setColor={setColor}
            />
          )}
          {tab === 'Prompt' && (
            <PromptTab
              instructionsText={instructionsText}
              setInstructionsText={setInstructionsText}
              modelId={modelId}
              setModelId={setModelId}
              models={models}
              riskTier={riskTier}
              setRiskTier={setRiskTier}
              autoApply={autoApply}
            />
          )}
          {tab === 'Tools' && (
            <ToolsTab toolNames={toolNames} setToolNames={setToolNames} />
          )}
          {tab === 'Policies' && <PoliciesTab />}
          {tab === 'Triggers' && <TriggersTab />}
        </div>

        {/* Footer */}
        <div className="flex flex-none items-center justify-between border-t border-[var(--color-border-light)] bg-[var(--color-surface-inset)] px-5 py-3">
          {agent.isOverride ? (
            <Button variant="ghost" size="sm" onClick={handleDelete}>
              <Trash2 className="h-3 w-3" />
              <span className="text-[var(--color-error)]">Remove override</span>
            </Button>
          ) : (
            <span className="font-[family-name:var(--font-mono)] text-[11px] text-[var(--color-text-tertiary)]">
              {countTools(JSON.stringify(toolNames))} tools · {agent.requiresUserBrief ? 'user-brief on' : 'no brief'}
            </span>
          )}
          <div className="flex items-center gap-2">
            <Button variant="outline" size="sm" onClick={onClose}>
              Cancel
            </Button>
            <Button size="sm" onClick={handleSave} disabled={saving}>
              <Check className="h-3 w-3" />
              {saving ? 'Saving…' : 'Save changes'}
            </Button>
          </div>
        </div>
      </div>
    </>
  );
}

// ── Identity tab ────────────────────────────────────────────────────

function IdentityTab({
  name,
  description,
  setDescription,
  glyph,
  setGlyph,
  color,
  setColor,
}: {
  name: string;
  description: string;
  setDescription: (v: string) => void;
  glyph: AgentGlyph;
  setGlyph: (g: AgentGlyph) => void;
  color: string;
  setColor: (c: string) => void;
}) {
  return (
    <div className="flex flex-col gap-5">
      {/* Profile editor */}
      <div>
        <FieldLabel hint="Generated · keyed on agent name">Profile image</FieldLabel>
        <div className="flex items-center gap-3.5 rounded-[10px] border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] p-3.5">
          <AgentPortrait name={name} color={color} glyph={glyph} size={72} />
          <div className="flex flex-1 flex-col gap-1">
            <span className="text-[12px] font-medium text-[var(--color-text-primary)]">
              Style · <span className="capitalize text-[var(--color-brand-primary)]">{glyph}</span>
            </span>
            <span className="text-[11px] leading-relaxed text-[var(--color-text-secondary)]">
              Each agent gets a deterministic glyph portrait. Pick a glyph and hue from below, or upload a
              custom mark (coming soon).
            </span>
            <div className="mt-1.5 flex gap-1.5">
              <Button variant="outline" size="sm" disabled>
                <RefreshCw className="h-3 w-3" />
                Re-roll
              </Button>
              <Button variant="ghost" size="sm" disabled>
                <Upload className="h-3 w-3" />
                Upload
              </Button>
            </div>
          </div>
        </div>

        {/* Glyph swatches */}
        <div className="mt-2.5 grid grid-cols-8 gap-1.5">
          {AGENT_GLYPHS.map((g) => (
            <button
              key={g}
              type="button"
              onClick={() => setGlyph(g)}
              title={g}
              className={cn(
                'rounded-md p-0.5 transition-colors',
                g === glyph
                  ? 'border-[1.5px] border-[var(--color-brand-primary)]'
                  : 'border-[1.5px] border-transparent hover:border-[var(--color-border)]',
              )}
            >
              <AgentPortrait name={name} color={color} glyph={g} size={42} ring={false} />
            </button>
          ))}
        </div>

        {/* Hue swatches */}
        <div className="mt-3 flex items-center gap-2">
          <span className="mr-1 text-[11.5px] text-[var(--color-text-secondary)]">Hue</span>
          {AGENT_PALETTE.map((c) => (
            <button
              key={c}
              type="button"
              onClick={() => setColor(c)}
              aria-label={`Use colour ${c}`}
              className="h-5.5 w-5.5 cursor-pointer rounded-full"
              style={{
                width: 22,
                height: 22,
                background: c,
                boxShadow: c === color ? '0 0 0 2px var(--color-surface), 0 0 0 4px var(--color-brand-primary)' : 'none',
              }}
            />
          ))}
        </div>
        <p className="mt-2 text-[11px] text-[var(--color-text-tertiary)]">
          Note: glyph and hue aren't persisted yet — they're derived from the agent name. A future schema
          change will store them server-side.
        </p>
      </div>

      <div className="grid grid-cols-2 gap-3">
        <TextField label="Name" required value={name} disabled />
        <TextField label="Handle" required mono value={`@${name}`} disabled />
      </div>

      <TextField
        label="Tagline"
        value={deriveAgentTagline(description)}
        disabled
        helper="Derived from the first sentence and shown on agent cards."
      />

      <TextArea
        label="Description"
        value={description}
        onChange={setDescription}
        rows={4}
        helper="Plain English. What this agent is for, who it talks to, when to use it."
      />
    </div>
  );
}

// ── Prompt tab ──────────────────────────────────────────────────────

function PromptTab({
  instructionsText,
  setInstructionsText,
  modelId,
  setModelId,
  models,
  riskTier,
  setRiskTier,
  autoApply,
}: {
  instructionsText: string;
  setInstructionsText: (v: string) => void;
  modelId: string;
  setModelId: (v: string) => void;
  models: AiModelResponse[];
  riskTier: string;
  setRiskTier: (v: string) => void;
  autoApply: boolean;
}) {
  return (
    <div className="flex flex-col gap-5">
      <div className="grid grid-cols-[2fr_1fr] gap-3">
        <SelectField
          label="Model"
          required
          value={modelId}
          onChange={setModelId}
          options={[
            { value: '', label: 'Inherit tenant default' },
            ...models.map((m) => ({
              value: m.id,
              label: m.providerName ? `${m.modelName} (${m.providerName})` : m.modelName,
            })),
          ]}
        />
        <SelectField
          label="Risk tier"
          required
          value={riskTier}
          onChange={setRiskTier}
          hint={autoApply ? 'auto-apply enabled' : 'propose-only'}
          options={[
            { value: '', label: '—' },
            { value: 'low', label: 'Low' },
            { value: 'medium', label: 'Medium' },
            { value: 'high', label: 'High' },
          ]}
        />
      </div>

      <div>
        <FieldLabel required hint={`${instructionsText.length} chars`}>
          System prompt
        </FieldLabel>
        <div className="rounded-md border border-[var(--color-border)] border-b-2 border-b-[var(--color-border)] bg-[var(--color-surface)] p-3 transition-colors focus-within:border-b-[var(--color-brand-primary)] focus-within:shadow-[var(--shadow-focus)]">
          <textarea
            rows={11}
            value={instructionsText}
            onChange={(e) => setInstructionsText(e.target.value)}
            spellCheck={false}
            className="w-full resize-y border-none bg-transparent p-0 font-[family-name:var(--font-mono)] text-[12px] leading-[1.55] text-[var(--color-text-primary)] outline-none"
          />
        </div>
      </div>

      <div className="flex items-start gap-2.5 rounded-md bg-[var(--color-brand-primary-10)] p-3">
        <Sparkles className="mt-0.5 h-3.5 w-3.5 flex-none text-[var(--color-brand-primary)]" />
        <div className="flex-1 text-[12px] leading-relaxed text-[var(--color-text-primary)]">
          Test in <b>Playground</b> before saving — agents propose, but humans approve every change.
        </div>
        <Button variant="outline" size="sm">
          Open in Playground
        </Button>
      </div>
    </div>
  );
}

// ── Tools tab ───────────────────────────────────────────────────────

function ToolsTab({
  toolNames,
  setToolNames,
}: {
  toolNames: string[];
  setToolNames: (next: string[]) => void;
}) {
  const [draft, setDraft] = useState('');

  const handleAdd = () => {
    const trimmed = draft.trim();
    if (!trimmed) return;
    if (toolNames.includes(trimmed)) {
      toast.info(`${trimmed} is already enabled`);
      return;
    }
    setToolNames([...toolNames, trimmed]);
    setDraft('');
  };

  const handleRemove = (name: string) => {
    setToolNames(toolNames.filter((t) => t !== name));
  };

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <span className="text-[12px] text-[var(--color-text-secondary)]">
          {toolNames.length} tool{toolNames.length === 1 ? '' : 's'} enabled
        </span>
        <Button variant="outline" size="sm" onClick={handleAdd} disabled={!draft.trim()}>
          <Plus className="h-3 w-3" />
          Add tool
        </Button>
      </div>

      <div className="flex flex-col gap-1.5">
        {toolNames.length === 0 && (
          <div className="rounded-[10px] border border-dashed border-[var(--color-border)] bg-[var(--color-surface-inset)] px-4 py-6 text-center text-[12px] text-[var(--color-text-tertiary)]">
            No tools enabled yet. Add a tool id below.
          </div>
        )}
        {toolNames.map((name) => (
          <div
            key={name}
            className="grid items-center gap-3 rounded-lg border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] px-3 py-2.5"
            style={{ gridTemplateColumns: '8px 1fr auto' }}
          >
            <span className="h-1.5 w-1.5 rounded-full" style={{ background: toolCategoryColor(name) }} />
            <div className="min-w-0">
              <div className="truncate font-[family-name:var(--font-mono)] text-[12px] font-semibold text-[var(--color-text-primary)]">
                {name}
              </div>
              <div className="mt-0.5 text-[11px] text-[var(--color-text-secondary)]">
                {toolDescription(name)}
              </div>
            </div>
            <button
              type="button"
              onClick={() => handleRemove(name)}
              className="grid h-6 w-6 place-items-center rounded-md text-[var(--color-text-tertiary)] hover:bg-[var(--color-surface)] hover:text-[var(--color-error)]"
              aria-label={`Disable ${name}`}
            >
              <X className="h-3 w-3" />
            </button>
          </div>
        ))}
      </div>

      <div className="flex items-end gap-2 border-t border-[var(--color-border-light)] pt-3">
        <label className="flex-1 text-[12px] text-[var(--color-text-secondary)]">
          Add tool id
          <input
            type="text"
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            placeholder="e.g. search_invoices"
            className="aonik-input mt-1.5 font-[family-name:var(--font-mono)] text-[13px]"
            onKeyDown={(e) => {
              if (e.key === 'Enter') {
                e.preventDefault();
                handleAdd();
              }
            }}
          />
        </label>
      </div>
    </div>
  );
}

function toolCategoryColor(name: string): string {
  const lower = name.toLowerCase();
  if (lower.includes('create') || lower.includes('issue') || lower.includes('cancel') || lower.includes('capture') || lower.includes('apply') || lower.includes('send')) return 'var(--color-brand-secondary)';
  if (lower.includes('display') || lower.includes('confirm') || lower.includes('render')) return 'var(--color-violet)';
  return 'var(--color-brand-primary)';
}

function toolDescription(name: string): string {
  const lower = name.toLowerCase();
  if (lower.includes('create') || lower.includes('issue') || lower.includes('cancel') || lower.includes('capture')) return 'Mutating tool. Requires approval before state changes.';
  if (lower.includes('display') || lower.includes('confirm')) return 'Display tool. Renders a guarded UI action for the human.';
  return 'Read or compute tool. Executes directly inside tenant scope.';
}

// ── Policies tab ─────────────────────────────────────────────────────

function PoliciesTab() {
  return (
    <div className="flex flex-col gap-5">
      <div className="flex flex-col gap-3.5 rounded-[10px] border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] p-3.5">
        <ToggleField
          label="Auto-apply when confidence ≥ threshold"
          description="Skip the proposal step only for low-risk operations below the amount ceiling."
          on={false}
        />
        <div className="border-t border-[var(--color-border-light)]" />
        <div className="grid grid-cols-2 gap-3">
          <TextField label="Confidence threshold" mono value="0.95" disabled helper="0–1" />
          <TextField label="Amount ceiling" mono value="£50,000" disabled helper="Requires approval above this." />
        </div>
      </div>

      <div>
        <FieldLabel>Inherited from organization</FieldLabel>
        <div className="flex flex-col gap-1.5">
          {[
            { title: 'Dual-control payouts', description: 'Two approvers required for outbound payouts' },
            { title: 'PII redaction', description: 'Customer PII stripped from all prompts' },
            { title: 'Audit log retention', description: 'Immutable log of every tool call · 7 years' },
          ].map((policy) => (
            <div
              key={policy.title}
              className="grid items-center gap-2.5 rounded-lg border border-[var(--color-border-light)] bg-[var(--color-surface)] px-3 py-2.5"
              style={{ gridTemplateColumns: '14px 1fr auto' }}
            >
              <ShieldCheck className="h-3 w-3 text-[var(--color-text-secondary)]" />
              <div>
                <div className="text-[12px] font-medium text-[var(--color-text-primary)]">{policy.title}</div>
                <div className="mt-px text-[11px] text-[var(--color-text-secondary)]">{policy.description}</div>
              </div>
              <Pill tone="info" size="sm">enforced</Pill>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

function ToggleField({ label, description, on }: { label: string; description: string; on: boolean }) {
  return (
    <div className="flex items-start gap-3">
      <SwitchToggle on={on} />
      <div>
        <div className="text-[12.5px] font-medium text-[var(--color-text-primary)]">{label}</div>
        <div className="mt-0.5 text-[11.5px] leading-relaxed text-[var(--color-text-secondary)]">{description}</div>
      </div>
    </div>
  );
}

function SwitchToggle({ on }: { on: boolean }) {
  return (
    <span
      className="inline-flex h-4 w-7 flex-none items-center rounded-full p-0.5"
      style={{ background: on ? 'var(--color-brand-primary)' : 'var(--color-gray-300)' }}
    >
      <span
        className="h-3 w-3 rounded-full bg-white transition-transform"
        style={{ transform: on ? 'translateX(12px)' : 'translateX(0)' }}
      />
    </span>
  );
}

// ── Triggers tab ─────────────────────────────────────────────────────

interface TriggerRow {
  kind: 'event' | 'schedule' | 'manual' | 'webhook';
  label: string;
  enabled: boolean;
  source: string;
  workflow: string;
}

function TriggersTab() {
  const [adding, setAdding] = useState(false);
  const [triggers, setTriggers] = useState<TriggerRow[]>([
    { kind: 'event', label: 'New bank transaction received', enabled: true, source: 'banking.transaction.received', workflow: 'match_and_apply' },
    { kind: 'schedule', label: 'Hourly · top of the hour', enabled: true, source: 'cron 0 * * * *', workflow: 'sweep_unmatched' },
    { kind: 'manual', label: 'Run from My Space', enabled: true, source: 'human.invocation', workflow: 'manual_review' },
  ]);

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center gap-2">
        <span className="text-[12px] text-[var(--color-text-secondary)]">
          {triggers.filter((t) => t.enabled).length} of {triggers.length} active
        </span>
        <div className="flex-1" />
        <Button variant="outline" size="sm" onClick={() => setAdding(true)}>
          <Plus className="h-3 w-3" />
          Add trigger
        </Button>
      </div>

      <div className="flex flex-col gap-1.5">
        {triggers.map((trigger, index) => (
          <TriggerListRow key={`${trigger.kind}-${trigger.source}-${index}`} trigger={trigger} />
        ))}
      </div>

      {adding && (
        <AddTriggerDialog
          onClose={() => setAdding(false)}
          onSave={(trigger) => {
            setTriggers([...triggers, trigger]);
            setAdding(false);
          }}
        />
      )}
    </div>
  );
}

function TriggerListRow({ trigger }: { trigger: TriggerRow }) {
  const icon = {
    event: Zap,
    schedule: Clock,
    manual: Play,
    webhook: Globe,
  }[trigger.kind];
  const Icon = icon;
  return (
    <div
      className="grid items-center gap-3 rounded-lg border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] p-3"
      style={{ gridTemplateColumns: '34px 1fr auto auto', opacity: trigger.enabled ? 1 : 0.55 }}
    >
      <div className="grid h-[30px] w-[30px] place-items-center rounded-lg bg-[var(--color-brand-primary-10)] text-[var(--color-brand-primary)]">
        <Icon className="h-3.5 w-3.5" />
      </div>
      <div className="min-w-0">
        <div className="flex items-center gap-1.5">
          <span className="text-[12.5px] font-medium text-[var(--color-text-primary)]">{trigger.label}</span>
          <span className="rounded bg-[var(--color-surface)] px-1.5 py-px text-[9.5px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
            {trigger.kind}
          </span>
        </div>
        <div className="mt-0.5 truncate font-[family-name:var(--font-mono)] text-[11px] text-[var(--color-text-secondary)]">
          {trigger.source}
        </div>
        <div className="mt-0.5 text-[10.5px] text-[var(--color-text-tertiary)]">
          → runs <span className="font-[family-name:var(--font-mono)] text-[var(--color-brand-primary)]">{trigger.workflow}</span>
        </div>
      </div>
      <Pill tone={trigger.enabled ? 'success' : 'muted'} dot={trigger.enabled} size="sm">
        {trigger.enabled ? 'on' : 'off'}
      </Pill>
      <MoreHorizontal className="h-3 w-3 text-[var(--color-text-tertiary)]" />
    </div>
  );
}

function AddTriggerDialog({ onClose, onSave }: { onClose: () => void; onSave: (trigger: TriggerRow) => void }) {
  const [kind, setKind] = useState<TriggerRow['kind']>('event');
  const options: Array<{ kind: TriggerRow['kind']; label: string; description: string; icon: typeof Zap }> = [
    { kind: 'event', label: 'Event', description: 'Fire when something happens in the system', icon: Zap },
    { kind: 'schedule', label: 'Schedule', description: 'Fire on a recurring time-based cadence', icon: Clock },
    { kind: 'webhook', label: 'Webhook', description: 'Fire when an external system POSTs to us', icon: Globe },
    { kind: 'manual', label: 'Manual', description: 'Run only when a human invokes it', icon: Play },
  ];

  const build = (): TriggerRow => {
    if (kind === 'schedule') return { kind, label: 'Hourly · top of the hour', enabled: true, source: 'cron 0 * * * *', workflow: 'sweep_unmatched' };
    if (kind === 'webhook') return { kind, label: 'External hook · stripe-invoice-paid', enabled: true, source: 'webhook.stripe.invoice.paid', workflow: 'match_and_apply' };
    if (kind === 'manual') return { kind, label: 'Run from My Space', enabled: true, source: 'human.invocation', workflow: 'manual_review' };
    return { kind, label: 'Invoice marked overdue', enabled: true, source: 'invoice.overdue', workflow: 'dunning_cadence' };
  };

  return (
    <>
      <div className="fixed inset-0 z-[60] bg-[rgba(20,25,30,0.4)]" onClick={onClose} />
      <div className="fixed left-1/2 top-1/2 z-[61] flex w-[720px] max-w-[calc(100vw-32px)] -translate-x-1/2 -translate-y-1/2 flex-col overflow-hidden rounded-[14px] bg-[var(--color-surface)] shadow-[0_24px_60px_-8px_rgba(0,0,0,0.32)]">
        <div className="flex items-center gap-3 border-b border-[var(--color-border-light)] px-5 py-4">
          <div className="grid h-9 w-9 place-items-center rounded-[10px] bg-[var(--color-brand-primary-10)] text-[var(--color-brand-primary)]">
            <Zap className="h-4 w-4" />
          </div>
          <div className="flex-1">
            <div className="text-[15px] font-semibold text-[var(--color-text-primary)]">Add trigger</div>
            <div className="mt-0.5 text-[11.5px] text-[var(--color-text-secondary)]">Define when this agent should run.</div>
          </div>
          <button type="button" className="hover-halo" onClick={onClose} aria-label="Close">
            <X className="h-3.5 w-3.5" />
          </button>
        </div>
        <div className="grid grid-cols-2 gap-2.5 p-5">
          {options.map((option) => {
            const active = option.kind === kind;
            const OptionIcon = option.icon;
            return (
              <button
                key={option.kind}
                type="button"
                onClick={() => setKind(option.kind)}
                className="flex items-start gap-3 rounded-[10px] border p-3.5 text-left"
                style={{
                  background: active ? 'var(--color-brand-primary-10)' : 'var(--color-surface-inset)',
                  borderColor: active ? 'var(--color-brand-primary)' : 'var(--color-border-light)',
                }}
              >
                <div
                  className="grid h-9 w-9 place-items-center rounded-lg"
                  style={{ background: active ? 'var(--color-brand-primary)' : 'var(--color-surface)', color: active ? '#fff' : 'var(--color-brand-primary)' }}
                >
                  <OptionIcon className="h-4 w-4" />
                </div>
                <div>
                  <div className="text-[13px] font-semibold text-[var(--color-text-primary)]">{option.label}</div>
                  <div className="mt-1 text-[11.5px] leading-relaxed text-[var(--color-text-secondary)]">{option.description}</div>
                </div>
              </button>
            );
          })}
        </div>
        <div className="flex items-center justify-between border-t border-[var(--color-border-light)] bg-[var(--color-surface-inset)] px-5 py-3">
          <div className="flex items-center gap-1.5 text-[11px] text-[var(--color-text-tertiary)]">
            <Info className="h-3 w-3" />
            Triggers can be enabled or paused after saving.
          </div>
          <div className="flex gap-2">
            <Button variant="ghost" size="sm" onClick={onClose}>Cancel</Button>
            <Button size="sm" onClick={() => onSave(build())}>
              <Check className="h-3 w-3" />
              Add trigger
            </Button>
          </div>
        </div>
      </div>
    </>
  );
}

// ── Form helpers ────────────────────────────────────────────────────

function FieldLabel({
  children,
  required,
  hint,
}: {
  children: React.ReactNode;
  required?: boolean;
  hint?: React.ReactNode;
}) {
  return (
    <div className="mb-1.5 flex items-baseline justify-between gap-2">
      <span className="text-[11px] font-semibold uppercase tracking-[0.06em] text-[var(--color-text-tertiary)]">
        {children}
        {required && <span className="ml-0.5 text-[var(--color-error)]">*</span>}
      </span>
      {hint && (
        <span className="font-[family-name:var(--font-mono)] text-[10.5px] text-[var(--color-text-tertiary)]">
          {hint}
        </span>
      )}
    </div>
  );
}

function TextField({
  label,
  required,
  mono,
  value,
  onChange,
  disabled,
  helper,
}: {
  label: string;
  required?: boolean;
  mono?: boolean;
  value: string;
  onChange?: (v: string) => void;
  disabled?: boolean;
  helper?: string;
}) {
  return (
    <label className="block text-[12px] text-[var(--color-text-secondary)]">
      <FieldLabel required={required}>{label}</FieldLabel>
      <input
        type="text"
        value={value}
        disabled={disabled}
        onChange={(e) => onChange?.(e.target.value)}
        className={cn(
          'aonik-input',
          mono && 'font-[family-name:var(--font-mono)]',
          'text-[13px]',
        )}
      />
      {helper && <p className="mt-1 text-[11px] text-[var(--color-text-tertiary)]">{helper}</p>}
    </label>
  );
}

function TextArea({
  label,
  value,
  onChange,
  rows = 4,
  helper,
}: {
  label: string;
  value: string;
  onChange: (v: string) => void;
  rows?: number;
  helper?: string;
}) {
  return (
    <label className="block text-[12px] text-[var(--color-text-secondary)]">
      <FieldLabel>{label}</FieldLabel>
      <textarea
        rows={rows}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        className="aonik-input min-h-[80px] py-2.5 text-[13px]"
      />
      {helper && <p className="mt-1 text-[11px] text-[var(--color-text-tertiary)]">{helper}</p>}
    </label>
  );
}

function SelectField({
  label,
  value,
  onChange,
  options,
  required,
  hint,
}: {
  label: string;
  value: string;
  onChange: (v: string) => void;
  options: { value: string; label: string }[];
  required?: boolean;
  hint?: string;
}) {
  return (
    <label className="block text-[12px] text-[var(--color-text-secondary)]">
      <FieldLabel required={required} hint={hint}>
        {label}
      </FieldLabel>
      <select
        value={value}
        onChange={(e) => onChange(e.target.value)}
        className="aonik-select text-[13px]"
      >
        {options.map((o) => (
          <option key={o.value} value={o.value}>
            {o.label}
          </option>
        ))}
      </select>
    </label>
  );
}
