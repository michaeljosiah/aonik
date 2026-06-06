import { useCallback, useEffect, useMemo, useState, type CSSProperties, type ReactNode } from 'react';
import {
  AlertTriangle, Ban, BookOpen, Check, Eye, FlaskConical, Lock, Pencil, Play,
  Plug, Plus, RefreshCw, Search, Server, Terminal, Upload, X,
} from 'lucide-react';
import { toast } from 'sonner';

import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import type { WorkspacePanelRenderProps } from '../types';
import {
  agentExtensionsService as svc,
  toExtensions,
  type Extension,
  type ExtState,
  type ExtType,
  type McpDryRun,
  type SkillPreview,
  type SkillValidation,
  type TenantHttpTool,
  type TenantMcpServer,
  type TenantSkill,
} from '@/services/agentExtensionsService';

// ─── Design tokens (from the Spec 033 starter-kit template) ────────────────
const TYPE_META: Record<ExtType, { label: string; color: string; addLabel: string; Icon: typeof BookOpen }> = {
  skill: { label: 'Skill', color: '#055a60', addLabel: 'Upload skill', Icon: BookOpen },
  mcp: { label: 'MCP server', color: '#7b76b6', addLabel: 'Connect server', Icon: Server },
  http: { label: 'HTTP tool', color: '#b4741e', addLabel: 'Declare tool', Icon: Plug },
};
const STATE_META: Record<ExtState, { label: string; color: string }> = {
  draft: { label: 'Draft', color: '#8a97a3' },
  review: { label: 'In review', color: '#b4741e' },
  approved: { label: 'Approved', color: '#055a60' },
  active: { label: 'Active', color: '#1f7a5e' },
  rejected: { label: 'Rejected', color: '#c44536' },
};
const TIER_META: Record<string, { label: string; color: string; bg: string }> = {
  readonly: { label: 'Read only', color: '#5a6a76', bg: '#eef0f2' },
  low: { label: 'Low', color: '#1f6b3a', bg: '#ecf6ee' },
  medium: { label: 'Medium', color: '#7a5a10', bg: '#fff5d9' },
  high: { label: 'High', color: '#b3261e', bg: '#fbe2dd' },
  mixed: { label: 'Mixed', color: '#5a6a76', bg: '#eef0f2' },
  na: { label: '', color: '#5a6a76', bg: '#eef0f2' },
};

// ─── Atoms ─────────────────────────────────────────────────────────────────
function TypeTile({ type, size = 40 }: { type: ExtType; size?: number }) {
  const t = TYPE_META[type];
  return (
    <div style={{
      width: size, height: size, borderRadius: Math.round(size * 0.24),
      background: t.color + '18', color: t.color, flex: 'none',
      display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
    }}>
      <t.Icon size={Math.round(size * 0.5)} />
    </div>
  );
}

function TypeChip({ type }: { type: ExtType }) {
  const t = TYPE_META[type];
  return (
    <span style={{
      display: 'inline-flex', alignItems: 'center', gap: 5, padding: '3px 9px', borderRadius: 999,
      background: t.color + '14', color: t.color, fontSize: 11, fontWeight: 600,
    }}>
      <t.Icon size={10} />{t.label}
    </span>
  );
}

function StateBadge({ state }: { state: ExtState }) {
  const s = STATE_META[state];
  return (
    <span style={{
      display: 'inline-flex', alignItems: 'center', gap: 5, padding: '2px 9px', borderRadius: 999,
      background: s.color + '18', color: s.color, fontSize: 11, fontWeight: 600,
    }}>
      <span style={{ width: 6, height: 6, borderRadius: 999, background: s.color }} />{s.label}
    </span>
  );
}

function TierPill({ tier }: { tier?: string }) {
  if (!tier || tier === 'na') return null;
  const t = TIER_META[tier] ?? TIER_META.readonly;
  return (
    <span style={{
      fontFamily: 'var(--font-mono, monospace)', fontSize: 10, fontWeight: 700, letterSpacing: '0.04em',
      textTransform: 'uppercase', padding: '2px 7px', borderRadius: 4, background: t.bg, color: t.color,
    }}>{t.label}</span>
  );
}

const TXT1 = 'var(--color-text-primary)';
const TXT2 = 'var(--color-text-secondary)';
const TXT3 = 'var(--color-text-tertiary)';
const SURFACE = 'var(--color-surface)';
const INSET = 'var(--color-surface-inset)';
const BORDER = 'var(--color-border-light)';
const MONO = 'var(--font-mono, ui-monospace, monospace)';

function factLine(e: Extension): string {
  if (e.type === 'skill') {
    const s = e.raw as TenantSkill;
    return `${s.allowedTools.length} allowed tools${s.scriptsPresent ? ' · has scripts' : ''}`;
  }
  if (e.type === 'mcp') {
    const s = e.raw as TenantMcpServer;
    return `${s.transportType} · ${s.authKind}`;
  }
  const s = e.raw as TenantHttpTool;
  return `${s.method} · ${s.authKind}`;
}

// ─── Panel ───────────────────────────────────────────────────────────────
type DrawerMode = 'detail' | 'review' | 'add' | 'harness' | null;

export function AgentExtensionsPanel({ title }: WorkspacePanelRenderProps) {
  const [extensions, setExtensions] = useState<Extension[]>([]);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [lens, setLens] = useState<'tenant' | 'platform'>('tenant');
  const [typeFilter, setTypeFilter] = useState<'all' | ExtType>('all');
  const [search, setSearch] = useState('');
  const [drawer, setDrawer] = useState<DrawerMode>(null);
  const [selId, setSelId] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      const [skills, servers, tools] = await Promise.all([
        svc.skills.list(),
        svc.mcp.list(),
        svc.http.list(),
      ]);
      setExtensions(toExtensions(skills, servers, tools));
    } catch {
      toast.error('Failed to load agent extensions.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { void load(); }, [load]);

  const runAction = useCallback(async (action: () => Promise<unknown>, success: string) => {
    setBusy(true);
    try {
      await action();
      toast.success(success);
      await load();
    } catch (err) {
      toast.error(getErrorMessage(err, 'Action failed.'));
    } finally {
      setBusy(false);
    }
  }, [load]);

  const isPlatform = lens === 'platform';
  const counts = useMemo(() => ({
    all: extensions.length,
    skill: extensions.filter((e) => e.type === 'skill').length,
    mcp: extensions.filter((e) => e.type === 'mcp').length,
    http: extensions.filter((e) => e.type === 'http').length,
  }), [extensions]);

  const items = useMemo(() => {
    let list = typeFilter === 'all' ? extensions : extensions.filter((e) => e.type === typeFilter);
    if (isPlatform) list = list.filter((e) => e.state === 'review');
    const q = search.trim().toLowerCase();
    if (q) list = list.filter((e) => e.name.toLowerCase().includes(q) || e.slug.toLowerCase().includes(q));
    return list;
  }, [extensions, typeFilter, isPlatform, search]);

  const reviewCount = useMemo(() => extensions.filter((e) => e.state === 'review').length, [extensions]);
  const activeCount = useMemo(() => extensions.filter((e) => e.state === 'active').length, [extensions]);
  const selected = selId ? extensions.find((e) => e.id === selId) ?? null : null;

  const openCard = (e: Extension) => { setSelId(e.id); setDrawer(isPlatform ? 'review' : 'detail'); };
  const close = () => setDrawer(null);

  return (
    <div style={{ position: 'relative', height: '100%', overflow: 'hidden' }}>
      <style>{`@keyframes aeDrawerIn { from { transform: translateX(24px); opacity: 0 } to { transform: translateX(0); opacity: 1 } }`}</style>

      <div style={{ height: '100%', overflow: 'auto', padding: '24px 28px' }}>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 18, maxWidth: 1120, margin: '0 auto' }}>
          {/* Header */}
          <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: 12, flexWrap: 'wrap' }}>
            <div>
              <div style={{ fontSize: 11, letterSpacing: '0.08em', textTransform: 'uppercase', color: TXT3, fontWeight: 600 }}>AI · Agents</div>
              <div style={{ fontSize: 22, fontWeight: 700, color: TXT1, letterSpacing: '-0.01em' }}>{title || 'Agent Extensions'}</div>
              <div style={{ fontSize: 12.5, color: TXT2, marginTop: 2 }}>
                {extensions.length} extensions · {activeCount} active · {reviewCount} in review
              </div>
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <RoleLens lens={lens} setLens={(l) => { setLens(l); setDrawer(null); }} reviewCount={reviewCount} />
              <Button variant="outline" size="sm" onClick={() => setDrawer('harness')}><FlaskConical className="w-3.5 h-3.5" /> Test harness</Button>
              <Button size="sm" onClick={() => { setDrawer('add'); }}><Plus className="w-3.5 h-3.5" /> Add extension</Button>
              <Button variant="ghost" size="icon-sm" onClick={() => void load()} title="Refresh"><RefreshCw className="w-4 h-4" /></Button>
            </div>
          </div>

          {isPlatform && (
            <div style={{ padding: '12px 14px', background: 'rgba(180,116,30,0.07)', border: '1px solid rgba(180,116,30,0.25)', borderRadius: 10, display: 'flex', alignItems: 'center', gap: 10, fontSize: 12.5, color: TXT1 }}>
              <span style={{ fontFamily: MONO, fontSize: 10, fontWeight: 700, padding: '2px 7px', borderRadius: 4, background: '#f3e7fb', color: '#6a2c8a' }}>PLATFORM ADMIN</span>
              <span><b>{reviewCount}</b> extension{reviewCount === 1 ? '' : 's'} awaiting review — code execution, money tools, and new network destinations cross the tenant trust boundary.</span>
            </div>
          )}

          {/* Controls */}
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 12, background: SURFACE, border: `1px solid ${BORDER}`, borderRadius: 10, padding: '10px 14px', flexWrap: 'wrap' }}>
            <TypeFilter value={typeFilter} onChange={setTypeFilter} counts={counts} />
            <div style={{ position: 'relative', width: 220 }}>
              <span style={{ position: 'absolute', left: 10, top: '50%', transform: 'translateY(-50%)', color: TXT3 }}><Search size={14} /></span>
              <Input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Search…" style={{ paddingLeft: 30, height: 32 }} />
            </div>
          </div>

          {/* Grid */}
          {loading ? (
            <div style={{ padding: 40, textAlign: 'center', color: TXT3 }}>Loading…</div>
          ) : (
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(320px, 1fr))', gap: 14 }}>
              {items.map((e) => (
                <LibCard key={e.id} e={e} platform={isPlatform} selected={e.id === selId && !!drawer} onClick={() => openCard(e)} />
              ))}
              {items.length === 0 && (
                <div style={{ gridColumn: '1 / -1', padding: '40px 10px', textAlign: 'center', color: TXT3 }}>
                  <Check size={22} color="var(--color-success)" />
                  <div style={{ fontSize: 13, fontWeight: 500, color: TXT2, marginTop: 8 }}>
                    {isPlatform ? 'Nothing in the review queue' : 'No extensions yet'}
                  </div>
                  <div style={{ fontSize: 11.5, marginTop: 2 }}>
                    {isPlatform ? 'Every extension is either live or still a draft.' : 'Add a skill, MCP server, or HTTP tool to get started.'}
                  </div>
                </div>
              )}
            </div>
          )}
        </div>
      </div>

      {/* Drawers */}
      {drawer === 'detail' && selected && <DetailDrawer e={selected} busy={busy} runAction={runAction} onClose={close} onActed={close} />}
      {drawer === 'review' && selected && <DetailDrawer e={selected} review busy={busy} runAction={runAction} onClose={close} onActed={close} />}
      {drawer === 'add' && <AddDrawer busy={busy} runAction={runAction} onClose={close} />}
      {drawer === 'harness' && <HarnessDrawer extensions={extensions} initial={selected?.id} onClose={close} />}
    </div>
  );
}

// ─── Header bits ───────────────────────────────────────────────────────────
function RoleLens({ lens, setLens, reviewCount }: { lens: 'tenant' | 'platform'; setLens: (l: 'tenant' | 'platform') => void; reviewCount: number }) {
  return (
    <div style={{ display: 'flex', gap: 4, padding: 3, background: INSET, borderRadius: 8 }}>
      {([{ id: 'tenant', label: 'My extensions' }, { id: 'platform', label: 'Review queue' }] as const).map((l) => {
        const on = lens === l.id;
        return (
          <button key={l.id} onClick={() => setLens(l.id)} style={{
            border: 'none', background: on ? SURFACE : 'transparent', padding: '5px 11px', borderRadius: 6, cursor: 'pointer',
            fontSize: 11.5, fontWeight: on ? 600 : 500, color: on ? TXT1 : TXT2,
            boxShadow: on ? '0 1px 2px rgba(0,0,0,0.04)' : 'none', display: 'inline-flex', alignItems: 'center', gap: 6,
          }}>
            {l.label}
            {l.id === 'platform' && reviewCount > 0 && (
              <span style={{ fontFamily: MONO, fontSize: 9.5, fontWeight: 700, minWidth: 15, textAlign: 'center', padding: '0 4px', borderRadius: 999, background: 'var(--color-warning)', color: '#fff' }}>{reviewCount}</span>
            )}
          </button>
        );
      })}
    </div>
  );
}

function TypeFilter({ value, onChange, counts }: { value: 'all' | ExtType; onChange: (v: 'all' | ExtType) => void; counts: Record<string, number> }) {
  const opts: { id: 'all' | ExtType; label: string }[] = [
    { id: 'all', label: 'All' }, { id: 'skill', label: 'Skills' }, { id: 'mcp', label: 'MCP servers' }, { id: 'http', label: 'HTTP tools' },
  ];
  return (
    <div style={{ display: 'flex', gap: 4, flexWrap: 'wrap' }}>
      {opts.map((o) => {
        const on = value === o.id;
        return (
          <button key={o.id} onClick={() => onChange(o.id)} style={{
            background: on ? 'rgba(5,90,96,0.1)' : 'transparent', color: on ? '#055a60' : TXT2,
            border: 'none', borderRadius: 6, padding: '5px 11px', cursor: 'pointer', fontSize: 12, fontWeight: on ? 600 : 500,
            display: 'inline-flex', alignItems: 'center', gap: 6,
          }}>
            {o.label}
            <span style={{ fontFamily: MONO, fontSize: 10, fontWeight: 600, padding: '0 5px', borderRadius: 4, background: on ? SURFACE : INSET, color: on ? '#055a60' : TXT3 }}>{counts[o.id]}</span>
          </button>
        );
      })}
    </div>
  );
}

function LibCard({ e, platform, selected, onClick }: { e: Extension; platform: boolean; selected: boolean; onClick: () => void }) {
  const color = TYPE_META[e.type].color;
  return (
    <div onClick={onClick} style={{
      background: SURFACE, border: `1px solid ${selected ? color : BORDER}`, boxShadow: selected ? `0 0 0 1px ${color}` : 'none',
      borderRadius: 12, padding: 16, display: 'flex', flexDirection: 'column', gap: 12, cursor: 'pointer',
    }}>
      <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between' }}>
        <TypeTile type={e.type} />
        <StateBadge state={e.state} />
      </div>
      <div>
        <div style={{ fontSize: 14, fontWeight: 600, color: TXT1 }}>{e.name}</div>
        <div style={{ fontFamily: MONO, fontSize: 10.5, color: TXT3, marginTop: 2, wordBreak: 'break-all' }}>{e.slug}</div>
      </div>
      <div style={{ fontSize: 12, color: TXT2, lineHeight: 1.5, minHeight: 36 }}>{e.description}</div>
      {platform && e.reviewNotes && (
        <div style={{ fontSize: 11, color: 'var(--color-warning)', display: 'flex', alignItems: 'center', gap: 6 }}>
          <AlertTriangle size={11} /> {e.reviewNotes}
        </div>
      )}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', paddingTop: 10, borderTop: `1px solid ${BORDER}` }}>
        <span style={{ fontSize: 11, color: TXT3, fontFamily: MONO }}>{factLine(e)}</span>
        <TierPill tier={e.tier} />
      </div>
    </div>
  );
}

// ─── Drawer shell ────────────────────────────────────────────────────────
function DrawerShell({ width = 540, onClose, children }: { width?: number; onClose: () => void; children: ReactNode }) {
  return (
    <>
      <div onClick={onClose} style={{ position: 'absolute', inset: 0, background: 'rgba(15,20,28,0.18)', zIndex: 5 }} />
      <div style={{
        position: 'absolute', top: 0, right: 0, bottom: 0, width: '100%', maxWidth: width, zIndex: 6, background: SURFACE,
        borderLeft: `1px solid ${BORDER}`, boxShadow: '-20px 0 50px -20px rgba(0,0,0,0.25)',
        display: 'flex', flexDirection: 'column', animation: 'aeDrawerIn 200ms ease both',
      }}>{children}</div>
    </>
  );
}

function Section({ title, hint, children }: { title: string; hint?: string; children: ReactNode }) {
  return (
    <div>
      <div style={{ fontSize: 11, letterSpacing: '0.06em', textTransform: 'uppercase', fontWeight: 600, color: TXT3 }}>{title}</div>
      {hint && <div style={{ fontSize: 11.5, color: TXT2, marginTop: 3, marginBottom: 8 }}>{hint}</div>}
      <div style={{ marginTop: hint ? 0 : 8 }}>{children}</div>
    </div>
  );
}

// ─── Detail / Review drawer ───────────────────────────────────────────────
function DetailDrawer({ e, review, busy, runAction, onClose, onActed }: {
  e: Extension; review?: boolean; busy: boolean;
  runAction: (a: () => Promise<unknown>, msg: string) => Promise<void>; onClose: () => void; onActed: () => void;
}) {
  const [notes, setNotes] = useState('');

  const transition = (fn: () => Promise<unknown>, msg: string) => runAction(fn, msg).then(onActed);

  const primaryAction = () => {
    if (e.state === 'active') return <Button variant="outline" size="sm" disabled={busy} onClick={() => transition(() => actionFor(e, 'deactivate'), 'Deactivated.')}><Ban className="w-3.5 h-3.5" /> Deactivate</Button>;
    if (e.state === 'approved') return <Button size="sm" disabled={busy} onClick={() => transition(() => actionFor(e, 'activate'), 'Activated.')}><Check className="w-3.5 h-3.5" /> Activate</Button>;
    if (e.state === 'draft') return <Button size="sm" disabled={busy} onClick={() => transition(() => actionFor(e, 'submit'), 'Submitted for review.')}><Upload className="w-3.5 h-3.5" /> Submit for review</Button>;
    if (e.state === 'rejected') return <Button size="sm" disabled={busy} onClick={() => transition(() => actionFor(e, 'submit'), 'Re-submitted for review.')}><Pencil className="w-3.5 h-3.5" /> Resubmit</Button>;
    return <Button variant="outline" size="sm" disabled>Awaiting review</Button>;
  };

  return (
    <DrawerShell onClose={onClose}>
      <div style={{ padding: '18px 20px 14px', borderBottom: `1px solid ${BORDER}` }}>
        <div style={{ display: 'flex', alignItems: 'flex-start', gap: 12 }}>
          <TypeTile type={e.type} size={44} />
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{ fontSize: 16, fontWeight: 700, color: TXT1 }}>{e.name}</div>
            <div style={{ fontFamily: MONO, fontSize: 11, color: TXT3, marginTop: 2, wordBreak: 'break-all' }}>{e.slug}</div>
          </div>
          <button onClick={onClose} style={{ background: 'none', border: 'none', cursor: 'pointer', color: TXT2, padding: 4 }}><X size={16} /></button>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginTop: 12 }}>
          <TypeChip type={e.type} /><StateBadge state={e.state} /><TierPill tier={e.tier} />
          <div style={{ flex: 1 }} />
          {!review && primaryAction()}
        </div>
      </div>

      <div style={{ flex: 1, overflow: 'auto', padding: '16px 20px', display: 'flex', flexDirection: 'column', gap: 16 }}>
        <div style={{ fontSize: 13, color: TXT2, lineHeight: 1.55 }}>{e.description}</div>

        {e.type === 'skill' && <SkillDetail e={e} />}
        {e.type === 'mcp' && <McpDetail e={e} />}
        {e.type === 'http' && <HttpDetail e={e} />}

        {(e.type === 'mcp' || e.type === 'http') && (
          <Section title="Credentials">
            <div style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '10px 12px', background: INSET, border: `1px solid ${BORDER}`, borderRadius: 8 }}>
              <Lock size={14} color={TXT2} />
              <span style={{ fontSize: 12, color: TXT1, flex: 1 }}>{(e.raw as TenantMcpServer | TenantHttpTool).authKind}</span>
              <StateBadge state={e.authConfigured ? 'active' : 'draft'} />
            </div>
            <div style={{ fontSize: 10.5, color: TXT3, marginTop: 6 }}>Write-only — the value is encrypted and never returned to the UI.</div>
          </Section>
        )}

        <Section title="Lifecycle" hint="Draft → In review → Approved → Active"><Timeline e={e} /></Section>
      </div>

      {review && (
        <div style={{ padding: '12px 20px', borderTop: `1px solid ${BORDER}`, background: INSET, display: 'flex', flexDirection: 'column', gap: 8 }}>
          <Input value={notes} onChange={(ev) => setNotes(ev.target.value)} placeholder="Review note (optional)" style={{ height: 32 }} />
          {e.type === 'skill' && e.scriptsPresent && !e.scriptsEnabled && (
            <Button variant="outline" size="sm" disabled={busy} onClick={() => transition(() => svc.skills.enableScripts(e.id, true, notes || undefined), 'Scripts enabled.')}>
              <Terminal className="w-3.5 h-3.5" /> Enable scripts
            </Button>
          )}
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <span style={{ fontSize: 11, color: TXT3, flex: 1 }}>Approving makes it eligible — the tenant still activates.</span>
            <Button variant="ghost" size="sm" disabled={busy} onClick={() => transition(() => reviewFor(e, false, notes), 'Rejected.')} style={{ color: 'var(--color-error)' }}>Reject</Button>
            <Button size="sm" disabled={busy} onClick={() => transition(() => reviewFor(e, true, notes), 'Approved.')}><Check className="w-3.5 h-3.5" /> Approve</Button>
          </div>
        </div>
      )}
    </DrawerShell>
  );
}

function SkillDetail({ e }: { e: Extension }) {
  const s = e.raw as TenantSkill;
  return (
    <Section title="Allowed tools" hint="Intersected with the agent's existing tools — never widens authority">
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
        {s.allowedTools.length === 0 && <span style={{ fontSize: 12, color: TXT3 }}>None — pure procedural knowledge.</span>}
        {s.allowedTools.map((tool) => (
          <span key={tool} style={{ fontFamily: MONO, fontSize: 11, padding: '3px 8px', borderRadius: 4, background: INSET, border: `1px solid ${BORDER}`, color: TXT1 }}>{tool}</span>
        ))}
      </div>
      {s.scriptsPresent && (
        <div style={{ marginTop: 12, padding: '10px 12px', borderRadius: 8, display: 'flex', alignItems: 'center', gap: 10,
          background: s.scriptsEnabled ? 'rgba(31,122,94,0.06)' : 'rgba(180,116,30,0.06)',
          border: `1px solid ${s.scriptsEnabled ? 'rgba(31,122,94,0.2)' : 'rgba(180,116,30,0.2)'}` }}>
          <Terminal size={14} color={s.scriptsEnabled ? '#1f7a5e' : '#b4741e'} />
          <span style={{ fontSize: 11.5, color: TXT2 }}>
            {s.scriptsEnabled ? 'Scripts enabled by platform admin — runs under ScriptApproval.' : 'Scripts present but off — a platform admin must review and enable.'}
          </span>
        </div>
      )}
    </Section>
  );
}

function McpDetail({ e }: { e: Extension }) {
  const s = e.raw as TenantMcpServer;
  return (
    <Section title="Endpoint">
      <div style={{ fontFamily: MONO, fontSize: 11.5, color: TXT2, wordBreak: 'break-all' }}>{s.endpoint}</div>
      <div style={{ fontSize: 11.5, color: TXT3, marginTop: 6 }}>
        Remote {s.transportType} transport · default tier <b>{s.defaultRiskTier}</b> · run “Dry-run connect” in the harness to list its tools and how each is classified.
      </div>
    </Section>
  );
}

function HttpDetail({ e }: { e: Extension }) {
  const s = e.raw as TenantHttpTool;
  const isGet = s.method.toUpperCase() === 'GET';
  return (
    <Section title="Request">
      <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
        <span style={{ fontFamily: MONO, fontSize: 11, fontWeight: 700, padding: '2px 8px', borderRadius: 4,
          background: isGet ? 'rgba(31,122,94,0.12)' : 'rgba(196,69,54,0.12)', color: isGet ? '#1f7a5e' : '#c44536' }}>{s.method}</span>
        <span style={{ fontFamily: MONO, fontSize: 11.5, color: TXT2, wordBreak: 'break-all' }}>{s.urlTemplate}</span>
      </div>
      <div style={{ fontSize: 11.5, color: TXT3, marginTop: 6 }}>Declared parameter schema — the model can't smuggle extra fields.</div>
    </Section>
  );
}

function Timeline({ e }: { e: Extension }) {
  const order: ExtState[] = ['draft', 'review', 'approved', 'active'];
  const rejected = e.state === 'rejected';
  const curIdx = rejected ? 1 : order.indexOf(e.state);
  const labels: Record<string, string> = { draft: 'Created', review: 'Submitted for review', approved: 'Approved by platform', active: 'Activated' };
  return (
    <div style={{ display: 'flex', flexDirection: 'column' }}>
      {order.map((st, i) => {
        const done = i <= curIdx && !(rejected && i > 0);
        const isRejectStop = rejected && i === 1;
        const dotColor = isRejectStop ? 'var(--color-error)' : done ? '#055a60' : INSET;
        return (
          <div key={st} style={{ display: 'flex', gap: 12, alignItems: 'flex-start' }}>
            <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center' }}>
              <div style={{ width: 20, height: 20, borderRadius: 999, background: dotColor, color: '#fff', display: 'inline-flex', alignItems: 'center', justifyContent: 'center', border: done || isRejectStop ? 'none' : `1px solid var(--color-border)` }}>
                {isRejectStop ? <X size={11} /> : done ? <Check size={11} /> : null}
              </div>
              {i < order.length - 1 && <div style={{ width: 2, height: 20, background: done && i < curIdx ? '#055a60' : BORDER }} />}
            </div>
            <div style={{ paddingBottom: 12 }}>
              <div style={{ fontSize: 12.5, fontWeight: done || isRejectStop ? 600 : 500, color: done || isRejectStop ? TXT1 : TXT3 }}>{isRejectStop ? 'Rejected' : labels[st]}</div>
              {isRejectStop && e.reviewNotes && <div style={{ fontSize: 11, color: 'var(--color-error)', marginTop: 2 }}>{e.reviewNotes}</div>}
            </div>
          </div>
        );
      })}
    </div>
  );
}

// ─── Add drawer ────────────────────────────────────────────────────────────
const fieldStyle: CSSProperties = {
  width: '100%', padding: '9px 11px', border: `1px solid ${BORDER}`, borderRadius: 8, fontSize: 13,
  background: SURFACE, color: TXT1, outline: 'none',
};

function FormField({ label, hint, children }: { label: string; hint?: string; children: ReactNode }) {
  return (
    <div>
      <div style={{ fontSize: 11, letterSpacing: '0.04em', textTransform: 'uppercase', fontWeight: 600, color: TXT3, marginBottom: 6 }}>{label}</div>
      {children}
      {hint && <div style={{ fontSize: 10.5, color: TXT3, marginTop: 5 }}>{hint}</div>}
    </div>
  );
}

function AddDrawer({ busy, runAction, onClose }: { busy: boolean; runAction: (a: () => Promise<unknown>, msg: string) => Promise<void>; onClose: () => void }) {
  const [surface, setSurface] = useState<ExtType>('skill');

  // skill
  const [markdown, setMarkdown] = useState('---\nname: my-skill\ndescription: A short description of what this skill helps the agent do.\n---\n\n# My skill\n\nProcedure goes here.\n');
  const [validation, setValidation] = useState<SkillValidation | null>(null);

  // mcp
  const [mcp, setMcp] = useState({ name: '', endpoint: '', transportType: 'Http', authKind: 'None', authSecret: '', authUsername: '', authHeaderName: '' });
  // http
  const [http, setHttp] = useState({ name: '', description: '', method: 'GET', urlTemplate: '', parameterSchemaJson: '{\n  "type": "object",\n  "properties": {}\n}', authKind: 'None', authSecret: '', authUsername: '', authHeaderName: '' });

  const saveSkill = () => runAction(async () => {
    const v = await svc.skills.validate(markdown);
    setValidation(v);
    if (!v.isValid) throw new Error(v.errors[0] ?? 'Validation failed.');
    await svc.skills.upload(markdown);
  }, 'Skill uploaded as a draft.').then(() => { /* keep drawer if invalid handled by throw */ });

  const saveMcp = () => runAction(() => svc.mcp.create({
    name: mcp.name, endpoint: mcp.endpoint, transportType: mcp.transportType, authKind: mcp.authKind,
    authSecret: mcp.authSecret || null, authUsername: mcp.authUsername || null, authHeaderName: mcp.authHeaderName || null,
  }), 'MCP server saved as a draft.').then(onClose);

  const saveHttp = () => runAction(() => svc.http.create({
    name: http.name, description: http.description, method: http.method, urlTemplate: http.urlTemplate,
    parameterSchemaJson: http.parameterSchemaJson, authKind: http.authKind,
    authSecret: http.authSecret || null, authUsername: http.authUsername || null, authHeaderName: http.authHeaderName || null,
  }), 'HTTP tool saved as a draft.').then(onClose);

  return (
    <DrawerShell onClose={onClose}>
      <div style={{ padding: '18px 20px 14px', borderBottom: `1px solid ${BORDER}`, display: 'flex', alignItems: 'center', gap: 12 }}>
        <div style={{ width: 38, height: 38, borderRadius: 10, background: 'rgba(5,90,96,0.1)', color: '#055a60', display: 'inline-flex', alignItems: 'center', justifyContent: 'center' }}><Plus size={18} /></div>
        <div style={{ flex: 1 }}>
          <div style={{ fontSize: 15, fontWeight: 700, color: TXT1 }}>Add extension</div>
          <div style={{ fontSize: 12, color: TXT2 }}>Pick a surface — it saves as a draft you submit for review.</div>
        </div>
        <button onClick={onClose} style={{ background: 'none', border: 'none', cursor: 'pointer', color: TXT2, padding: 4 }}><X size={16} /></button>
      </div>

      <div style={{ flex: 1, overflow: 'auto', padding: '16px 20px', display: 'flex', flexDirection: 'column', gap: 16 }}>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
          {(['skill', 'mcp', 'http'] as ExtType[]).map((s) => {
            const t = TYPE_META[s];
            const on = surface === s;
            const blurb = { skill: 'A SKILL.md package — procedural knowledge for the agent.', mcp: 'A remote MCP server whose tools become callable.', http: 'One declared REST call exposed as a single tool.' }[s];
            return (
              <div key={s} onClick={() => setSurface(s)} style={{ display: 'flex', alignItems: 'center', gap: 12, padding: '12px 14px', borderRadius: 10, cursor: 'pointer', background: on ? t.color + '0c' : SURFACE, border: `1px solid ${on ? t.color : BORDER}`, boxShadow: on ? `0 0 0 1px ${t.color}` : 'none' }}>
                <TypeTile type={s} size={36} />
                <div style={{ flex: 1 }}>
                  <div style={{ fontSize: 13, fontWeight: 600, color: TXT1 }}>{t.label}</div>
                  <div style={{ fontSize: 11.5, color: TXT2 }}>{blurb}</div>
                </div>
                <div style={{ width: 18, height: 18, borderRadius: 999, border: `2px solid ${on ? t.color : 'var(--color-border)'}`, display: 'inline-flex', alignItems: 'center', justifyContent: 'center' }}>
                  {on && <span style={{ width: 8, height: 8, borderRadius: 999, background: t.color }} />}
                </div>
              </div>
            );
          })}
        </div>

        <div style={{ height: 1, background: BORDER }} />

        {surface === 'skill' && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
            <FormField label="SKILL.md" hint="Frontmatter is validated and allowed-tools intersected with the agent's tools on upload.">
              <textarea value={markdown} onChange={(e) => setMarkdown(e.target.value)} rows={12} style={{ ...fieldStyle, fontFamily: MONO, fontSize: 12, resize: 'vertical' }} />
            </FormField>
            {validation && !validation.isValid && (
              <div style={{ padding: '10px 12px', background: 'rgba(196,69,54,0.05)', border: '1px solid rgba(196,69,54,0.16)', borderRadius: 8, fontSize: 11.5, color: TXT2 }}>
                {validation.errors.map((er, i) => <div key={i} style={{ display: 'flex', gap: 6, alignItems: 'flex-start' }}><AlertTriangle size={12} color="var(--color-error)" style={{ marginTop: 2 }} /><span>{er}</span></div>)}
              </div>
            )}
            {validation?.isValid && (
              <div style={{ padding: '10px 12px', background: 'rgba(31,122,94,0.06)', border: '1px solid rgba(31,122,94,0.2)', borderRadius: 8, fontSize: 11.5, color: TXT2, display: 'flex', gap: 6 }}>
                <Check size={13} color="var(--color-success)" /><span>Valid — “{validation.name}”, {validation.allowedTools.length} allowed tools{validation.scriptsPresent ? ', has scripts' : ''}.</span>
              </div>
            )}
          </div>
        )}

        {surface === 'mcp' && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
            <FormField label="Name"><Input value={mcp.name} onChange={(e) => setMcp({ ...mcp, name: e.target.value })} placeholder="e.g. Companies House" /></FormField>
            <FormField label="Endpoint" hint="Remote HTTP/SSE only — host must be on the platform allow-list.">
              <Input value={mcp.endpoint} onChange={(e) => setMcp({ ...mcp, endpoint: e.target.value })} placeholder="https://mcp.example.com/sse" style={{ fontFamily: MONO, fontSize: 12 }} />
            </FormField>
            <FormField label="Transport">
              <select value={mcp.transportType} onChange={(e) => setMcp({ ...mcp, transportType: e.target.value })} style={fieldStyle}><option>Http</option><option>Sse</option></select>
            </FormField>
            <AuthFields kind={mcp.authKind} secret={mcp.authSecret} username={mcp.authUsername} header={mcp.authHeaderName}
              onChange={(p) => setMcp({ ...mcp, authKind: p.kind, authSecret: p.secret, authUsername: p.username, authHeaderName: p.header })} />
          </div>
        )}

        {surface === 'http' && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
            <FormField label="Name"><Input value={http.name} onChange={(e) => setHttp({ ...http, name: e.target.value })} placeholder="e.g. CRM create contact" /></FormField>
            <FormField label="Description"><Input value={http.description} onChange={(e) => setHttp({ ...http, description: e.target.value })} placeholder="What the tool does" /></FormField>
            <FormField label="Request">
              <div style={{ display: 'flex', gap: 8 }}>
                <select value={http.method} onChange={(e) => setHttp({ ...http, method: e.target.value })} style={{ ...fieldStyle, width: 110, fontFamily: MONO, fontSize: 12 }}>
                  {['GET', 'POST', 'PUT', 'PATCH', 'DELETE'].map((m) => <option key={m}>{m}</option>)}
                </select>
                <Input value={http.urlTemplate} onChange={(e) => setHttp({ ...http, urlTemplate: e.target.value })} placeholder="https://api.example.com/v2/{id}" style={{ flex: 1, fontFamily: MONO, fontSize: 12 }} />
              </div>
            </FormField>
            <FormField label="Parameter schema" hint="The fixed surface the model sees — it can't add fields.">
              <textarea value={http.parameterSchemaJson} onChange={(e) => setHttp({ ...http, parameterSchemaJson: e.target.value })} rows={5} style={{ ...fieldStyle, fontFamily: MONO, fontSize: 12, resize: 'vertical' }} />
            </FormField>
            <AuthFields kind={http.authKind} secret={http.authSecret} username={http.authUsername} header={http.authHeaderName}
              onChange={(p) => setHttp({ ...http, authKind: p.kind, authSecret: p.secret, authUsername: p.username, authHeaderName: p.header })} />
            {http.method.toUpperCase() !== 'GET' && (
              <div style={{ padding: '10px 12px', background: 'rgba(196,69,54,0.05)', border: '1px solid rgba(196,69,54,0.16)', borderRadius: 8, fontSize: 11.5, color: TXT2, display: 'flex', gap: 8 }}>
                <AlertTriangle size={13} color="var(--color-error)" />
                <span>A non-GET call writes to an external system, so it defaults to <b style={{ color: '#b3261e' }}>HIGH</b> — a durable proposal that never runs in-band.</span>
              </div>
            )}
          </div>
        )}
      </div>

      <div style={{ padding: '12px 20px', borderTop: `1px solid ${BORDER}`, background: INSET, display: 'flex', alignItems: 'center', gap: 8 }}>
        <span style={{ fontSize: 11, color: TXT3, flex: 1 }}>Mutating tools default to High · a platform admin reviews before it goes live.</span>
        <Button variant="ghost" size="sm" onClick={onClose}>Cancel</Button>
        <Button size="sm" disabled={busy} onClick={() => (surface === 'skill' ? saveSkill() : surface === 'mcp' ? saveMcp() : saveHttp())}><Check className="w-3.5 h-3.5" /> Save draft</Button>
      </div>
    </DrawerShell>
  );
}

function AuthFields({ kind, secret, username, header, onChange }: {
  kind: string; secret: string; username: string; header: string;
  onChange: (p: { kind: string; secret: string; username: string; header: string }) => void;
}) {
  return (
    <FormField label="Auth" hint="Stored encrypted — write-only, never shown again.">
      <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
        <select value={kind} onChange={(e) => onChange({ kind: e.target.value, secret, username, header })} style={fieldStyle}>
          <option value="None">None</option>
          <option value="BearerToken">Bearer token</option>
          <option value="ApiKeyHeader">API key header</option>
          <option value="Basic">Basic</option>
        </select>
        {kind === 'ApiKeyHeader' && (
          <Input value={header} onChange={(e) => onChange({ kind, secret, username, header: e.target.value })} placeholder="Header name (e.g. X-Api-Key)" />
        )}
        {kind === 'Basic' && (
          <Input value={username} onChange={(e) => onChange({ kind, secret, username: e.target.value, header })} placeholder="Username" />
        )}
        {kind !== 'None' && (
          <Input type="password" value={secret} onChange={(e) => onChange({ kind, secret: e.target.value, username, header })} placeholder={kind === 'Basic' ? 'Password' : 'Secret / token'} />
        )}
      </div>
    </FormField>
  );
}

// ─── Harness drawer ──────────────────────────────────────────────────────
function HarnessDrawer({ extensions, initial, onClose }: { extensions: Extension[]; initial?: string; onClose: () => void }) {
  const candidates = extensions;
  const [selId, setSelId] = useState<string | null>(initial ?? candidates[0]?.id ?? null);
  const e = selId ? extensions.find((x) => x.id === selId) ?? null : null;
  const [running, setRunning] = useState(false);
  const [result, setResult] = useState<string | null>(null);
  const [dryRun, setDryRun] = useState<McpDryRun | null>(null);
  const [preview, setPreview] = useState<SkillPreview | null>(null);

  const run = useCallback(async () => {
    if (!e) return;
    setRunning(true); setResult(null); setDryRun(null); setPreview(null);
    try {
      if (e.type === 'skill') {
        const p = await svc.skills.preview(e.id);
        setPreview(p);
        setResult('A skill adds no new tool — it can only reference tools the agent already has. This is exactly what the model sees: the catalogue line up-front, the body on demand via load_skill.');
      } else if (e.type === 'mcp') {
        const r = await svc.mcp.test(e.id);
        setDryRun(r);
        setResult(r.connected ? `Connected — ${r.tools.length} tool(s) discovered.` : `Connect failed: ${r.error}`);
      } else {
        const r = await svc.http.test(e.id);
        setResult(`Tier ${r.tier}. ${r.note}`);
      }
    } catch (err) {
      setResult(getErrorMessage(err, 'Harness run failed.'));
    } finally {
      setRunning(false);
    }
  }, [e]);

  return (
    <DrawerShell width={560} onClose={onClose}>
      <div style={{ padding: '18px 20px 14px', borderBottom: `1px solid ${BORDER}`, display: 'flex', alignItems: 'center', gap: 12 }}>
        <div style={{ width: 38, height: 38, borderRadius: 10, background: 'rgba(5,90,96,0.1)', color: '#055a60', display: 'inline-flex', alignItems: 'center', justifyContent: 'center' }}><FlaskConical size={18} /></div>
        <div style={{ flex: 1 }}>
          <div style={{ fontSize: 15, fontWeight: 700, color: TXT1 }}>Test harness</div>
          <div style={{ fontSize: 12, color: TXT2 }}>Server-truthful — the same code paths production runs.</div>
        </div>
        <button onClick={onClose} style={{ background: 'none', border: 'none', cursor: 'pointer', color: TXT2, padding: 4 }}><X size={16} /></button>
      </div>

      <div style={{ flex: 1, overflow: 'auto', padding: '16px 20px', display: 'flex', flexDirection: 'column', gap: 16 }}>
        <Section title="Testing">
          {candidates.length === 0 ? (
            <div style={{ fontSize: 12, color: TXT3 }}>No extensions to test yet.</div>
          ) : (
            <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
              {candidates.map((x) => {
                const on = x.id === selId;
                const c = TYPE_META[x.type];
                return (
                  <button key={x.id} onClick={() => { setSelId(x.id); setResult(null); setDryRun(null); setPreview(null); }} style={{
                    display: 'inline-flex', alignItems: 'center', gap: 6, padding: '5px 10px', borderRadius: 999, cursor: 'pointer',
                    border: `1px solid ${on ? c.color : BORDER}`, background: on ? c.color + '12' : SURFACE, color: on ? c.color : TXT2, fontSize: 11.5, fontWeight: on ? 600 : 500,
                  }}><c.Icon size={11} />{x.name}</button>
                );
              })}
            </div>
          )}
        </Section>

        {e && (
          <>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
              {harnessSteps(e).map((s) => (
                <div key={s.key} style={{ display: 'grid', gridTemplateColumns: '32px 1fr auto', gap: 12, alignItems: 'center', padding: '12px 14px', background: SURFACE, border: `1px solid ${BORDER}`, borderRadius: 10 }}>
                  <div style={{ width: 30, height: 30, borderRadius: 8, background: s.run ? 'rgba(5,90,96,0.1)' : 'rgba(31,122,94,0.12)', color: s.run ? '#055a60' : '#1f7a5e', display: 'inline-flex', alignItems: 'center', justifyContent: 'center' }}>
                    <s.Icon size={14} />
                  </div>
                  <div>
                    <div style={{ fontSize: 13, fontWeight: 600, color: TXT1 }}>{s.label}</div>
                    <div style={{ fontSize: 11.5, color: TXT2 }}>{s.desc}</div>
                  </div>
                  {s.run
                    ? <Button variant="outline" size="sm" disabled={running} onClick={() => void run()}><Play className="w-3.5 h-3.5" /> {running ? 'Running…' : 'Run'}</Button>
                    : <StateBadge state="approved" />}
                </div>
              ))}
            </div>

            {result && (
              <div style={{ background: SURFACE, border: `1px solid ${BORDER}`, borderRadius: 12, padding: 14 }}>
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 8 }}>
                  <span style={{ fontSize: 12.5, fontWeight: 600, color: TXT1 }}>Gate verdict</span>
                  <TierPill tier={e.tier === 'na' ? 'readonly' : e.tier} />
                </div>
                <div style={{ padding: '10px 12px', borderRadius: 8, fontSize: 11.5, lineHeight: 1.5, background: INSET, border: `1px solid ${BORDER}`, color: TXT2 }}>{result}</div>
                {dryRun?.tools && dryRun.tools.length > 0 && (
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 6, marginTop: 10 }}>
                    {dryRun.tools.map((tool) => (
                      <div key={tool.name} style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '7px 10px', background: SURFACE, border: `1px solid ${BORDER}`, borderRadius: 8 }}>
                        <span style={{ fontFamily: MONO, fontSize: 12, color: TXT1 }}>{tool.name}</span>
                        <TierPill tier={tool.tier.toLowerCase()} />
                      </div>
                    ))}
                  </div>
                )}
                {preview && (
                  <div style={{ marginTop: 10 }}>
                    <div style={{ fontSize: 11, fontWeight: 600, color: TXT3, textTransform: 'uppercase', letterSpacing: '0.06em', marginBottom: 6 }}>Injected catalogue</div>
                    <pre style={{ margin: 0, padding: '10px 12px', background: INSET, border: `1px solid ${BORDER}`, borderRadius: 8, fontFamily: MONO, fontSize: 11, color: TXT1, whiteSpace: 'pre-wrap', wordBreak: 'break-word' }}>{preview.catalogueText}</pre>
                    <div style={{ fontSize: 11, fontWeight: 600, color: TXT3, textTransform: 'uppercase', letterSpacing: '0.06em', margin: '10px 0 6px' }}>SKILL.md (loaded on demand)</div>
                    <pre style={{ margin: 0, padding: '10px 12px', background: INSET, border: `1px solid ${BORDER}`, borderRadius: 8, fontFamily: MONO, fontSize: 11, color: TXT2, whiteSpace: 'pre-wrap', wordBreak: 'break-word', maxHeight: 220, overflow: 'auto' }}>{preview.markdown || '(empty)'}</pre>
                  </div>
                )}
              </div>
            )}
          </>
        )}
      </div>
    </DrawerShell>
  );
}

function harnessSteps(e: Extension): { key: string; label: string; desc: string; run: boolean; Icon: typeof Check }[] {
  const steps: { key: string; label: string; desc: string; run: boolean; Icon: typeof Check }[] = [
    { key: 'validate', label: 'Validate', desc: e.type === 'skill' ? 'Frontmatter + allowed-tools intersection' : 'Schema + auth reference', run: false, Icon: Check },
  ];
  if (e.type === 'mcp') steps.push({ key: 'dryrun', label: 'Dry-run connect & list', desc: 'Connect, list tools, classify each', run: true, Icon: Plug });
  else if (e.type === 'http') steps.push({ key: 'classify', label: 'Classification', desc: 'The tier the gate would assign', run: true, Icon: Plug });
  else steps.push({ key: 'preview', label: 'Preview injected text', desc: 'Exactly what the model will see', run: true, Icon: Eye });
  return steps;
}

// ─── Action dispatch helpers ────────────────────────────────────────────────
function actionFor(e: Extension, action: 'submit' | 'activate' | 'deactivate'): Promise<unknown> {
  const s = e.type === 'skill' ? svc.skills : e.type === 'mcp' ? svc.mcp : svc.http;
  return s[action](e.id);
}

function reviewFor(e: Extension, approve: boolean, notes: string): Promise<unknown> {
  const n = notes.trim() || undefined;
  if (e.type === 'skill') return svc.skills.review(e.id, approve, n);
  if (e.type === 'mcp') return svc.mcp.review(e.id, approve, n);
  return svc.http.review(e.id, approve, n);
}

function getErrorMessage(err: unknown, fallback: string): string {
  if (err && typeof err === 'object') {
    const o = err as { userMessage?: string; message?: string };
    const m = (o.userMessage ?? o.message ?? '').trim();
    if (m) return m;
  }
  return fallback;
}
