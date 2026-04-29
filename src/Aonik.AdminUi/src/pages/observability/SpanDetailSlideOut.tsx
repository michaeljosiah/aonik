// Slide-out span detail panel — visual port of `SpanDetailSlideOut` from
// templates/aonik-admin-starterkit/screens/obs-overview-traces.jsx.
//
// Right-anchored 540px panel that opens when a span is selected in the
// trace waterfall. Sections:
//   • Header: kind badge + span name + span/trace ids + close
//   • Tabs: Overview / Attributes / Events / Logs / Raw (Overview default;
//     other tabs are stubbed until we surface logs/event streams)
//   • Body: Timing strip (duration + offsets + mini-timeline), Attributes,
//     kind-specific detail (LLM / Tool / HTTP / DB / RPC), Events
//   • Footer: View logs / Copy span ID / Prev / Next span buttons
//
// All real fields from `AiTraceObservationResponse` flow through: model,
// tokens, cost, time-to-first-token, agent, service. Kind-specific blocks
// fall back to "—" for fields we don't have yet, but the structure mirrors
// the template so adding the data later is a drop-in.

import { useState } from 'react';
import { ArrowLeft, ArrowRight, Copy, Terminal, X } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import type { AiTraceObservationResponse } from '@/services/aiService';

const KIND_COLOR: Record<string, string> = {
  llm: '#3f41a0',
  tool: '#0097a9',
  http: '#7b76b6',
  db: '#5facbd',
  rpc: '#055a60',
  request: '#055a60',
  generation: '#3f41a0',
  span: 'var(--color-text-secondary)',
  default: 'var(--color-text-secondary)',
};

function getSpanKind(type: string): string {
  const lower = (type ?? '').toLowerCase();
  if (lower === 'generation') return 'llm';
  if (lower === 'request') return 'rpc';
  if (lower === 'span' || lower === '') return 'span';
  return lower;
}

function getKindColor(type: string): string {
  return KIND_COLOR[getSpanKind(type)] ?? KIND_COLOR.default;
}

export interface SpanDetailSlideOutProps {
  span: AiTraceObservationResponse | null;
  /** Total ms of the parent trace, used for the "% of trace" stat + mini-timeline. */
  totalMs: number;
  /** Start time (ms epoch) of the trace's first span, used for offsets. */
  traceStartMs: number;
  /** Span duration in ms (already computed by the page). */
  durationMs: number;
  onClose: () => void;
  /** Whether the prev/next buttons are enabled (no-op when at edge). */
  hasPrev: boolean;
  hasNext: boolean;
  onPrev: () => void;
  onNext: () => void;
}

const TABS = ['Overview', 'Attributes', 'Events', 'Logs', 'Raw'] as const;
type Tab = (typeof TABS)[number];

export function SpanDetailSlideOut({
  span,
  totalMs,
  traceStartMs,
  durationMs,
  onClose,
  hasPrev,
  hasNext,
  onPrev,
  onNext,
}: SpanDetailSlideOutProps) {
  const [tab, setTab] = useState<Tab>('Overview');
  if (!span) return null;

  const kind = getSpanKind(span.type);
  const color = getKindColor(span.type);
  const startMs = new Date(span.startTime).getTime();
  const startOffset = Math.max(0, startMs - traceStartMs);
  const endOffset = startOffset + durationMs;
  const startPct = totalMs > 0 ? (startOffset / totalMs) * 100 : 0;
  const widthPct = totalMs > 0 ? Math.max(0.5, (durationMs / totalMs) * 100) : 0;
  const traceShare = totalMs > 0 ? (durationMs / totalMs) * 100 : 0;
  const spanIdShort = (span.spanId ?? span.observationId).slice(0, 12);

  const handleCopySpanId = () => {
    void navigator.clipboard?.writeText(span.spanId ?? span.observationId).catch(() => {});
  };

  return (
    <div
      className="absolute inset-y-0 right-0 z-30 flex w-[540px] max-w-full flex-col border-l border-[var(--color-border-light)] bg-[var(--color-surface)] shadow-[-12px_0_32px_-8px_rgb(0_0_0/_0.10)]"
    >
      {/* Header */}
      <div className="flex flex-none items-center gap-3 border-b border-[var(--color-border-light)] px-5 py-3.5">
        <div
          className="grid h-[34px] w-[34px] flex-none place-items-center rounded-md font-[family-name:var(--font-mono)] text-[9px] font-bold uppercase tracking-[0.04em]"
          style={{ background: `${color}20`, color }}
        >
          {kind}
        </div>
        <div className="min-w-0 flex-1">
          <div className="truncate font-[family-name:var(--font-mono)] text-[13.5px] font-semibold text-[var(--color-text-primary)]">
            {span.name?.trim() || '—'}
          </div>
          <div className="mt-px font-[family-name:var(--font-mono)] text-[10.5px] text-[var(--color-text-tertiary)]">
            span_{spanIdShort} · {span.traceId.slice(0, 12)}
          </div>
        </div>
        <button
          type="button"
          onClick={onClose}
          aria-label="Close"
          className="grid h-7 w-7 place-items-center rounded-full text-[var(--color-text-secondary)] transition-colors hover:bg-[var(--color-surface-inset)] hover:text-[var(--color-text-primary)]"
        >
          <X className="h-3.5 w-3.5" />
        </button>
      </div>

      {/* Tabs */}
      <div className="flex flex-none items-center gap-1 border-b border-[var(--color-border-light)] px-5">
        {TABS.map((t) => {
          const active = t === tab;
          const count = t === 'Attributes' ? attrRowCount(span) : t === 'Events' ? 2 : undefined;
          return (
            <button
              key={t}
              type="button"
              onClick={() => setTab(t)}
              className={cn(
                '-mb-px inline-flex items-center gap-1.5 border-b-2 px-2.5 py-2.5 text-[12px] transition-colors',
                active
                  ? 'border-[var(--color-brand-primary)] font-semibold text-[var(--color-text-primary)]'
                  : 'border-transparent font-medium text-[var(--color-text-secondary)] hover:text-[var(--color-text-primary)]',
              )}
            >
              {t}
              {count != null && (
                <span className="rounded-full bg-[var(--color-surface-inset)] px-1.5 py-px font-[family-name:var(--font-mono)] text-[9.5px] font-semibold text-[var(--color-text-tertiary)]">
                  {count}
                </span>
              )}
            </button>
          );
        })}
      </div>

      {/* Body */}
      <div className="flex flex-1 flex-col gap-[18px] overflow-auto px-5 py-[18px]">
        {tab === 'Overview' && (
          <>
            <TimingSection
              durationMs={durationMs}
              startOffset={startOffset}
              endOffset={endOffset}
              traceShare={traceShare}
              startPct={startPct}
              widthPct={widthPct}
              totalMs={totalMs}
              accent={color}
            />

            <AttributesSection span={span} />

            {kind === 'llm' && <LlmSection span={span} />}
            {kind === 'tool' && <ToolSection span={span} />}
            {kind === 'http' && <HttpSection span={span} />}
            {kind === 'db' && <DbSection span={span} />}
            {kind === 'rpc' && <RpcSection span={span} />}

            <EventsSection durationMs={durationMs} />
          </>
        )}
        {tab === 'Attributes' && <AttributesSection span={span} expanded />}
        {tab === 'Events' && <EventsSection durationMs={durationMs} />}
        {tab === 'Logs' && (
          <EmptyTab title="Logs unavailable" description="Server-side log correlation isn't wired up yet for this trace store." />
        )}
        {tab === 'Raw' && <RawTab span={span} />}
      </div>

      {/* Footer */}
      <div className="flex flex-none items-center justify-between gap-2.5 border-t border-[var(--color-border-light)] bg-[var(--color-surface-inset)] px-5 py-3">
        <div className="flex gap-1.5">
          <Button variant="ghost" size="sm" disabled>
            <Terminal className="h-3 w-3" />
            View logs
          </Button>
          <Button variant="ghost" size="sm" onClick={handleCopySpanId}>
            <Copy className="h-3 w-3" />
            Copy span ID
          </Button>
        </div>
        <div className="flex gap-1.5">
          <Button variant="outline" size="sm" onClick={onPrev} disabled={!hasPrev}>
            <ArrowLeft className="h-3 w-3" />
            Prev span
          </Button>
          <Button variant="outline" size="sm" onClick={onNext} disabled={!hasNext}>
            Next span
            <ArrowRight className="h-3 w-3" />
          </Button>
        </div>
      </div>
    </div>
  );
}

// ── Sections ────────────────────────────────────────────────────────

function TimingSection({
  durationMs,
  startOffset,
  endOffset,
  traceShare,
  startPct,
  widthPct,
  totalMs,
  accent,
}: {
  durationMs: number;
  startOffset: number;
  endOffset: number;
  traceShare: number;
  startPct: number;
  widthPct: number;
  totalMs: number;
  accent: string;
}) {
  return (
    <div>
      <SectionLabel>Timing</SectionLabel>
      <div className="rounded-md border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] px-3.5 py-3">
        <div className="mb-2.5 flex justify-between gap-3">
          <Stat label="Duration" value={fmtMs(durationMs)} accent={accent} />
          <Stat label="Start offset" value={`+${fmtMs(startOffset)}`} />
          <Stat label="End offset" value={`+${fmtMs(endOffset)}`} />
          <Stat label="% of trace" value={`${traceShare.toFixed(1)}%`} />
        </div>
        <div className="relative h-3.5 rounded-[3px] border border-[var(--color-border-light)] bg-[var(--color-surface)]">
          {[0.25, 0.5, 0.75].map((p) => (
            <span
              key={p}
              aria-hidden
              className="absolute inset-y-0 w-px bg-[var(--color-border-light)]"
              style={{ left: `${p * 100}%` }}
            />
          ))}
          <div
            className="absolute rounded-[2px]"
            style={{
              left: `${startPct}%`,
              width: `${widthPct}%`,
              top: 2,
              bottom: 2,
              background: accent,
            }}
          />
        </div>
        <div className="mt-1 flex justify-between font-[family-name:var(--font-mono)] text-[9.5px] text-[var(--color-text-tertiary)]">
          <span>0ms</span>
          <span>{fmtMs(totalMs)}</span>
        </div>
      </div>
    </div>
  );
}

function AttributesSection({ span, expanded }: { span: AiTraceObservationResponse; expanded?: boolean }) {
  const rows: Array<[string, string]> = [
    ['service.name', span.serviceName ?? '—'],
    ['span.kind', getSpanKind(span.type)],
    ['span.type', span.type ?? '—'],
    ['agent.id', span.agentId ?? '—'],
    ['agent.name', span.agentName ?? '—'],
    ['source', span.source ?? '—'],
    ['operation.id', span.operationId ?? '—'],
    ['ai_run.id', span.aiRunId ?? '—'],
    ['parent.span.id', span.parentSpanId ?? span.parentObservationId ?? '—'],
    ['provided.model', span.providedModel ?? '—'],
    ['level', span.level ?? '—'],
  ];
  const visible = expanded ? rows : rows.slice(0, 8);
  return (
    <div>
      {!expanded && <SectionLabel>Attributes</SectionLabel>}
      <Attrs rows={visible} />
    </div>
  );
}

function LlmSection({ span }: { span: AiTraceObservationResponse }) {
  const rows: Array<[string, string]> = [
    ['model', span.providedModel ?? '—'],
    ['input.tokens', span.inputTokens?.toLocaleString() ?? '—'],
    ['output.tokens', span.outputTokens?.toLocaleString() ?? '—'],
    ['total.tokens', span.totalTokens?.toLocaleString() ?? '—'],
    ['cost.usd', span.costUsd != null ? `$${span.costUsd.toFixed(4)}` : '—'],
    [
      'time.to.first.token',
      span.timeToFirstTokenSeconds != null
        ? `${(span.timeToFirstTokenSeconds * 1000).toFixed(0)}ms`
        : '—',
    ],
  ];
  return (
    <div>
      <SectionLabel>LLM call</SectionLabel>
      <Attrs rows={rows} />
      {span.input && <CodeBlock label="Input" content={pretty(span.input)} />}
      {span.output && <CodeBlock label="Output" content={pretty(span.output)} />}
    </div>
  );
}

function ToolSection({ span }: { span: AiTraceObservationResponse }) {
  const toolName = span.name?.replace(/^tool\./, '') ?? span.name ?? '—';
  return (
    <div>
      <SectionLabel>Tool invocation</SectionLabel>
      <Attrs
        rows={[
          ['tool.name', toolName],
          ['agent', span.agentName ?? span.agentId ?? '—'],
          ['service', span.serviceName ?? '—'],
        ]}
      />
      {span.input && <CodeBlock label="Input" content={pretty(span.input)} />}
      {span.output && <CodeBlock label="Output" content={pretty(span.output)} />}
    </div>
  );
}

function HttpSection({ span }: { span: AiTraceObservationResponse }) {
  return (
    <div>
      <SectionLabel>HTTP request</SectionLabel>
      <Attrs
        rows={[
          ['operation', span.name ?? '—'],
          ['service', span.serviceName ?? '—'],
        ]}
      />
      {span.metadata && <CodeBlock label="Metadata" content={pretty(span.metadata)} />}
    </div>
  );
}

function DbSection({ span }: { span: AiTraceObservationResponse }) {
  return (
    <div>
      <SectionLabel>Database query</SectionLabel>
      <Attrs
        rows={[
          ['operation', span.name ?? '—'],
          ['service', span.serviceName ?? '—'],
        ]}
      />
      {span.input && <CodeBlock label="Statement" content={pretty(span.input)} />}
    </div>
  );
}

function RpcSection({ span }: { span: AiTraceObservationResponse }) {
  return (
    <div>
      <SectionLabel>Policy / RPC call</SectionLabel>
      <Attrs
        rows={[
          ['rpc.system', span.serviceName ?? '—'],
          ['operation', span.name ?? '—'],
          ['outcome', span.level === 'error' ? 'deny' : 'allow'],
        ]}
      />
      {span.metadata && <CodeBlock label="Metadata" content={pretty(span.metadata)} />}
    </div>
  );
}

function EventsSection({ durationMs }: { durationMs: number }) {
  const events: Array<{ t: string; name: string; color: string }> = [
    { t: '+0ms', name: 'span.started', color: 'var(--color-text-tertiary)' },
    { t: `+${fmtMs(durationMs)}`, name: 'span.ended', color: 'var(--color-success)' },
  ];
  return (
    <div>
      <SectionLabel>
        Events <span className="font-normal text-[var(--color-text-tertiary)]">· {events.length}</span>
      </SectionLabel>
      <div className="overflow-hidden rounded-md border border-[var(--color-border-light)] bg-[var(--color-surface)]">
        {events.map((e, i, arr) => (
          <div
            key={i}
            className={cn(
              'grid items-center gap-2.5 px-3 py-2 text-[11.5px]',
              i < arr.length - 1 && 'border-b border-[var(--color-border-light)]',
            )}
            style={{ gridTemplateColumns: '70px 12px 1fr' }}
          >
            <span className="font-[family-name:var(--font-mono)] text-[var(--color-text-tertiary)]">{e.t}</span>
            <span
              className="justify-self-center rounded-full"
              style={{ width: 8, height: 8, background: e.color }}
            />
            <span className="font-[family-name:var(--font-mono)] text-[var(--color-text-primary)]">{e.name}</span>
          </div>
        ))}
      </div>
    </div>
  );
}

function RawTab({ span }: { span: AiTraceObservationResponse }) {
  return (
    <div>
      <SectionLabel>Raw observation</SectionLabel>
      <CodeBlock content={JSON.stringify(span, null, 2)} />
    </div>
  );
}

function EmptyTab({ title, description }: { title: string; description: string }) {
  return (
    <div className="grid place-items-center rounded-md border border-dashed border-[var(--color-border)] bg-[var(--color-surface-inset)] px-5 py-12 text-center">
      <div>
        <div className="text-[13px] font-semibold text-[var(--color-text-primary)]">{title}</div>
        <div className="mt-1 max-w-[24rem] text-[11.5px] text-[var(--color-text-secondary)]">{description}</div>
      </div>
    </div>
  );
}

// ── Atoms ──────────────────────────────────────────────────────────

function SectionLabel({ children }: { children: React.ReactNode }) {
  return (
    <div className="mb-2 text-[10.5px] font-semibold uppercase tracking-[0.08em] text-[var(--color-text-tertiary)]">
      {children}
    </div>
  );
}

function Stat({ label, value, accent }: { label: string; value: string; accent?: string }) {
  return (
    <div>
      <div className="mb-px text-[10px] font-semibold uppercase tracking-[0.04em] text-[var(--color-text-tertiary)]">
        {label}
      </div>
      <div
        className="font-[family-name:var(--font-mono)] text-[14px] font-semibold"
        style={{ color: accent ?? 'var(--color-text-primary)' }}
      >
        {value}
      </div>
    </div>
  );
}

function Attrs({ rows }: { rows: Array<[string, string]> }) {
  return (
    <div className="overflow-hidden rounded-md border border-[var(--color-border-light)] bg-[var(--color-surface)]">
      {rows.map(([k, v], i, arr) => (
        <div
          key={k}
          className={cn(
            'grid gap-3 px-3 py-1.5 font-[family-name:var(--font-mono)] text-[11px]',
            i < arr.length - 1 && 'border-b border-[var(--color-border-light)]',
          )}
          style={{ gridTemplateColumns: '180px 1fr' }}
        >
          <span className="text-[var(--color-text-tertiary)]">{k}</span>
          <span className="truncate text-[var(--color-text-primary)]" title={v}>
            {v}
          </span>
        </div>
      ))}
    </div>
  );
}

function CodeBlock({ label, content }: { label?: string; content: string }) {
  return (
    <div className="mt-2.5">
      {label && (
        <div className="mb-1.5 text-[10.5px] font-semibold uppercase tracking-[0.04em] text-[var(--color-text-tertiary)]">
          {label}
        </div>
      )}
      <pre className="m-0 max-h-[200px] overflow-auto whitespace-pre-wrap rounded-md border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] px-3 py-2.5 font-[family-name:var(--font-mono)] text-[11px] leading-[1.5] text-[var(--color-text-primary)]">
        {content}
      </pre>
    </div>
  );
}

// ── Helpers ────────────────────────────────────────────────────────

function fmtMs(value: number): string {
  if (!Number.isFinite(value) || value <= 0) return '0ms';
  if (value >= 1000) return `${(value / 1000).toFixed(2)}s`;
  return `${Math.round(value)}ms`;
}

function pretty(raw: string): string {
  try {
    return JSON.stringify(JSON.parse(raw), null, 2);
  } catch {
    return raw;
  }
}

function attrRowCount(span: AiTraceObservationResponse): number {
  return (
    [
      span.serviceName,
      span.type,
      span.agentId,
      span.agentName,
      span.source,
      span.operationId,
      span.aiRunId,
      span.parentSpanId ?? span.parentObservationId,
      span.providedModel,
      span.level,
    ].filter(Boolean).length + 1
  );
}
