// Speech & Voice — admin hub: Providers, Recipes, Voice Mode, Chat Speech.
// Internal left sub-nav. Reached from Settings → AI → Speech & Voice.
//
// Conceptual model:
//   Providers  = plug-ins (STT, TTS, Realtime)
//   Recipes    = reusable voice configurations (chained or realtime)
//   Voice Mode = live spoken conversation (uses one active recipe)
//   Chat Speech = optional voice-over for written chat replies

const SPEECH_TABS = [
  { id: 'providers',  label: 'Providers',   icon: 'layers',  desc: 'STT, TTS, and realtime services' },
  { id: 'recipes',    label: 'Recipes',     icon: 'flow',    desc: 'Reusable voice configurations' },
  { id: 'voicemode',  label: 'Voice Mode',  icon: 'mic',     desc: 'Live spoken conversations' },
  { id: 'chatspeech', label: 'Chat Speech', icon: 'speaker', desc: 'Speak chat responses aloud' },
];

// Shared style tokens
const SPEECH_STYLE = {
  card:        { background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12 },
  cardActive:  { background: 'var(--surface)', border: '2px solid var(--brand-primary)', borderRadius: 12 },
  pillRow:     { display: 'flex', gap: 6, flexWrap: 'wrap' },
  meta:        { fontSize: 11.5, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' },
};

// Mock data ─────────────────────────────────────────────────────────
const PROVIDERS = [
  { id: 'whisper',     name: 'Whisper v3',          vendor: 'OpenAI',      type: 'stt',      kind: 'builtin', active: true,  usedBy: 2, latency: '410ms', languages: '99', icon: 'mic' },
  { id: 'deepgram',    name: 'Nova-3',              vendor: 'Deepgram',    type: 'stt',      kind: 'builtin', active: true,  usedBy: 0, latency: '180ms', languages: '40', icon: 'mic' },
  { id: 'azurestt',    name: 'Azure Speech',        vendor: 'Microsoft',   type: 'stt',      kind: 'builtin', active: false, usedBy: 0, latency: '320ms', languages: '85', icon: 'mic' },
  { id: 'elevenlabs',  name: 'ElevenLabs',          vendor: 'ElevenLabs',  type: 'tts',      kind: 'builtin', active: true,  usedBy: 3, latency: '290ms', voices: '29',    icon: 'speaker' },
  { id: 'voxtral',     name: 'Voxtral',             vendor: 'Mistral',     type: 'tts',      kind: 'builtin', active: true,  usedBy: 1, latency: '350ms', voices: '12',    icon: 'speaker' },
  { id: 'cartesia',    name: 'Sonic',               vendor: 'Cartesia',    type: 'tts',      kind: 'builtin', active: false, usedBy: 0, latency: '95ms',  voices: '20',    icon: 'speaker' },
  { id: 'openairt',    name: 'GPT-4o Realtime',     vendor: 'OpenAI',      type: 'realtime', kind: 'builtin', active: true,  usedBy: 1, latency: '<300ms', voices: '6',    icon: 'radio' },
  { id: 'gemini-live', name: 'Gemini Live',         vendor: 'Google',      type: 'realtime', kind: 'builtin', active: false, usedBy: 0, latency: '<400ms', voices: '4',    icon: 'radio' },
  { id: 'custom-asr',  name: 'In-house Whisper',    vendor: 'Self-hosted', type: 'stt',      kind: 'custom',  active: false, usedBy: 0, latency: '—',     languages: '12', icon: 'mic' },
];

const RECIPES = [
  {
    id: 'standard',
    name: 'Standard Voice',
    kind: 'builtin',
    type: 'chained',
    description: 'Reliable chained pipeline used by most workspaces.',
    steps: [
      { label: 'Listen',     icon: 'mic',     detail: 'Browser mic · push-to-talk' },
      { label: 'Transcribe', icon: 'mic',     detail: 'Whisper v3 · OpenAI' },
      { label: 'Agent',      icon: 'sparkles',detail: 'Orchestrator + domain agents' },
      { label: 'Speak',      icon: 'speaker', detail: 'ElevenLabs · Aria' },
    ],
    activeInVoiceMode: true,
  },
  {
    id: 'fast',
    name: 'Fast Response',
    kind: 'builtin',
    type: 'chained',
    description: 'Lower-latency pipeline that trades voice quality for speed.',
    steps: [
      { label: 'Listen',     icon: 'mic',     detail: 'Browser mic · streaming' },
      { label: 'Transcribe', icon: 'mic',     detail: 'Deepgram Nova-3' },
      { label: 'Agent',      icon: 'sparkles',detail: 'Orchestrator (no domain delegation)' },
      { label: 'Speak',      icon: 'speaker', detail: 'Cartesia Sonic · Coral' },
    ],
  },
  {
    id: 'realtime',
    name: 'Realtime Conversational',
    kind: 'builtin',
    type: 'realtime',
    description: 'Single realtime model handles listening and responding directly.',
    steps: [
      { label: 'Listen & Respond', icon: 'radio', detail: 'GPT-4o Realtime · OpenAI' },
    ],
  },
  {
    id: 'multilingual',
    name: 'Multilingual Voice',
    kind: 'custom',
    type: 'chained',
    description: 'Cloned for the East Africa team — Swahili and Amharic support.',
    cloneOf: 'Standard Voice',
    steps: [
      { label: 'Listen',     icon: 'mic',     detail: 'Browser mic · push-to-talk' },
      { label: 'Transcribe', icon: 'mic',     detail: 'Whisper v3 · OpenAI' },
      { label: 'Agent',      icon: 'sparkles',detail: 'Orchestrator + domain agents' },
      { label: 'Speak',      icon: 'speaker', detail: 'ElevenLabs · Maria (cloned)' },
    ],
  },
];

// ─── Shell ───────────────────────────────────────────────────────────
function SpeechShell({ initial = 'providers' }) {
  const [tab, setTab] = React.useState(initial);

  return (
    <div style={{ display: 'flex', height: '100%', minHeight: 0 }}>
      {/* Inner left rail */}
      <div style={{
        width: 240, flex: 'none',
        borderRight: '1px solid var(--border-light)',
        background: 'var(--surface-inset)',
        display: 'flex', flexDirection: 'column',
        padding: 20,
      }}>
        <div style={{ fontSize: 10, letterSpacing: '0.1em', textTransform: 'uppercase', fontWeight: 600, color: 'var(--text-tertiary)', marginBottom: 8 }}>Settings · AI</div>
        <div style={{ fontSize: 17, fontWeight: 600, color: 'var(--text-primary)', marginBottom: 4 }}>Speech &amp; Voice</div>
        <div style={{ fontSize: 12.5, color: 'var(--text-secondary)', lineHeight: 1.45, marginBottom: 18 }}>
          Configure the providers, recipes, and live experiences that power voice in this workspace.
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
          {SPEECH_TABS.map(t => {
            const active = tab === t.id;
            return (
              <div key={t.id} onClick={() => setTab(t.id)} style={{
                display: 'flex', alignItems: 'flex-start', gap: 10,
                padding: '10px 12px', borderRadius: 6, cursor: 'pointer',
                background: active ? 'var(--brand-primary-10)' : 'transparent',
                color: active ? 'var(--brand-primary)' : 'var(--text-primary)',
              }}>
                <span style={{ marginTop: 2, display: 'inline-flex' }}>
                  <Icon name={t.icon} size={14} color={active ? 'var(--brand-primary)' : 'var(--text-secondary)'}/>
                </span>
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{ fontWeight: active ? 600 : 500, fontSize: 13 }}>{t.label}</div>
                  <div style={{ fontSize: 11, color: active ? 'var(--brand-primary)' : 'var(--text-tertiary)', marginTop: 2, opacity: active ? 0.85 : 1 }}>{t.desc}</div>
                </div>
              </div>
            );
          })}
        </div>

        {/* Quick-status footer */}
        <div style={{ marginTop: 'auto', paddingTop: 16, borderTop: '1px solid var(--border-light)' }}>
          <div style={{ fontSize: 10, letterSpacing: '0.08em', textTransform: 'uppercase', fontWeight: 600, color: 'var(--text-tertiary)', marginBottom: 8 }}>Now active</div>
          <div style={{ fontSize: 12, color: 'var(--text-primary)', display: 'flex', alignItems: 'center', gap: 6, marginBottom: 4 }}>
            <span style={{ width: 6, height: 6, borderRadius: 3, background: 'var(--brand-primary)' }}/>
            Voice Mode · <span style={{ fontWeight: 600 }}>Standard Voice</span>
          </div>
          <div style={{ fontSize: 12, color: 'var(--text-primary)', display: 'flex', alignItems: 'center', gap: 6 }}>
            <span style={{ width: 6, height: 6, borderRadius: 3, background: 'var(--brand-primary)' }}/>
            Chat Speech · <span style={{ fontWeight: 600 }}>Aria</span>
          </div>
        </div>
      </div>

      {/* Right column */}
      <div style={{ flex: 1, minWidth: 0, overflow: 'auto', padding: '32px 40px' }}>
        {tab === 'providers'  && <SpeechProviders/>}
        {tab === 'recipes'    && <SpeechRecipes onJump={setTab}/>}
        {tab === 'voicemode'  && <SpeechVoiceMode onJump={setTab}/>}
        {tab === 'chatspeech' && <SpeechChatSpeech onJump={setTab}/>}
      </div>
    </div>
  );
}

// ─── Providers ───────────────────────────────────────────────────────
function SpeechProviders() {
  const [filter, setFilter] = React.useState('all');
  const filters = [
    { id: 'all',       label: 'All',                 count: PROVIDERS.length },
    { id: 'stt',       label: 'Speech-to-Text',      count: PROVIDERS.filter(p => p.type === 'stt').length },
    { id: 'tts',       label: 'Text-to-Speech',      count: PROVIDERS.filter(p => p.type === 'tts').length },
    { id: 'realtime',  label: 'Realtime Voice',      count: PROVIDERS.filter(p => p.type === 'realtime').length },
  ];
  const visible = PROVIDERS.filter(p => filter === 'all' || p.type === filter);

  return (
    <div>
      <PageHeader
        eyebrow="Speech & Voice"
        title="Providers"
        subtitle="Speech services available to this workspace. Activate a provider here before referencing it from a recipe."
        actions={<>
          <button className="btn btn-ghost btn-sm"><Icon name="refresh" size={12}/> Refresh status</button>
          <button className="btn btn-primary btn-sm"><Icon name="plus" size={12}/> Add provider</button>
        </>}
      />

      {/* Summary KPI strip */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 12, marginTop: 24, marginBottom: 24 }}>
        <ProviderStat label="Active providers" value={PROVIDERS.filter(p => p.active).length} total={PROVIDERS.length} icon="check"/>
        <ProviderStat label="Speech-to-Text"   value={PROVIDERS.filter(p => p.type === 'stt' && p.active).length}      total={PROVIDERS.filter(p => p.type === 'stt').length}      icon="mic"/>
        <ProviderStat label="Text-to-Speech"   value={PROVIDERS.filter(p => p.type === 'tts' && p.active).length}      total={PROVIDERS.filter(p => p.type === 'tts').length}      icon="speaker"/>
        <ProviderStat label="Realtime Voice"   value={PROVIDERS.filter(p => p.type === 'realtime' && p.active).length} total={PROVIDERS.filter(p => p.type === 'realtime').length} icon="radio"/>
      </div>

      {/* Filter pills */}
      <div style={{ display: 'flex', gap: 6, marginBottom: 16, flexWrap: 'wrap' }}>
        {filters.map(f => {
          const active = filter === f.id;
          return (
            <button key={f.id} onClick={() => setFilter(f.id)} className={active ? 'btn btn-primary btn-sm' : 'btn btn-ghost btn-sm'} style={{ borderRadius: 999 }}>
              {f.label}
              <span style={{ marginLeft: 6, fontFamily: 'var(--font-mono)', fontSize: 11, opacity: 0.85 }}>{f.count}</span>
            </button>
          );
        })}
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: 12 }}>
        {visible.map(p => <ProviderCard key={p.id} p={p}/>)}
      </div>
    </div>
  );
}

function ProviderStat({ label, value, total, icon }) {
  return (
    <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, padding: 16 }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 10 }}>
        <div style={{ fontSize: 11.5, color: 'var(--text-secondary)' }}>{label}</div>
        <div style={{ width: 26, height: 26, borderRadius: 6, background: 'var(--brand-primary-10)', display: 'grid', placeItems: 'center' }}>
          <Icon name={icon} size={12} color="var(--brand-primary)"/>
        </div>
      </div>
      <div style={{ fontFamily: 'var(--font-mono)', fontSize: 22, fontWeight: 600, color: 'var(--text-primary)', display: 'flex', alignItems: 'baseline', gap: 6 }}>
        {value}
        <span style={{ fontSize: 13, color: 'var(--text-tertiary)', fontWeight: 400 }}>/ {total}</span>
      </div>
    </div>
  );
}

function ProviderCard({ p }) {
  const typeLabel = { stt: 'Speech-to-Text', tts: 'Text-to-Speech', realtime: 'Realtime Voice' }[p.type];
  const typeTone  = { stt: 'tint',           tts: 'success',         realtime: 'pending'       }[p.type];
  return (
    <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, padding: 18 }}>
      <div style={{ display: 'flex', alignItems: 'flex-start', gap: 14 }}>
        <div style={{
          width: 42, height: 42, borderRadius: 10, flex: 'none',
          background: p.active ? 'var(--brand-primary-10)' : 'var(--surface-inset)',
          border: '1px solid var(--border-light)',
          display: 'grid', placeItems: 'center',
        }}>
          <Icon name={p.icon} size={20} color={p.active ? 'var(--brand-primary)' : 'var(--text-tertiary)'}/>
        </div>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 8 }}>
            <div>
              <div style={{ fontSize: 14, fontWeight: 600, color: 'var(--text-primary)' }}>{p.name}</div>
              <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', marginTop: 2 }}>{p.vendor}</div>
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
              {p.active
                ? <Pill tone="success" size="sm" dot>Active</Pill>
                : <Pill tone="default" size="sm">Inactive</Pill>}
            </div>
          </div>

          <div style={{ display: 'flex', gap: 6, marginTop: 10, flexWrap: 'wrap' }}>
            <Pill tone={typeTone} size="sm">{typeLabel}</Pill>
            {p.kind === 'builtin'
              ? <Pill tone="default" size="sm">Built-in</Pill>
              : <Pill tone="warning"    size="sm">Custom</Pill>}
            {p.usedBy > 0 && (
              <Pill tone="tint" size="sm">Used by {p.usedBy} {p.usedBy === 1 ? 'recipe' : 'recipes'}</Pill>
            )}
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: 10, marginTop: 12, padding: '10px 0', borderTop: '1px solid var(--border-light)' }}>
            <SpeechStat label="Latency"   value={p.latency}/>
            <SpeechStat label={p.type === 'stt' ? 'Languages' : 'Voices'} value={p.languages || p.voices}/>
          </div>

          <div style={{ display: 'flex', gap: 6, marginTop: 12 }}>
            <button className="btn btn-ghost btn-sm" disabled={!p.active}><Icon name="play" size={11}/> Test</button>
            <button className="btn btn-ghost btn-sm"><Icon name="cog" size={11}/> Configure</button>
            {p.usedBy > 0 && (
              <span style={{ marginLeft: 'auto', fontSize: 10.5, color: 'var(--text-tertiary)', alignSelf: 'center' }}>
                <Icon name="lock" size={10}/> In use
              </span>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}

function SpeechStat({ label, value }) {
  return (
    <div>
      <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', textTransform: 'uppercase', letterSpacing: '0.06em', fontWeight: 600 }}>{label}</div>
      <div style={{ fontFamily: 'var(--font-mono)', fontSize: 12.5, color: 'var(--text-primary)', marginTop: 2 }}>{value}</div>
    </div>
  );
}

// ─── Recipes ─────────────────────────────────────────────────────────
function SpeechRecipes({ onJump }) {
  const [filter, setFilter] = React.useState('all');
  const filters = [
    { id: 'all',      label: 'All recipes',  count: RECIPES.length },
    { id: 'chained',  label: 'Chained',      count: RECIPES.filter(r => r.type === 'chained').length },
    { id: 'realtime', label: 'Realtime',     count: RECIPES.filter(r => r.type === 'realtime').length },
  ];
  const visible = RECIPES.filter(r => filter === 'all' || r.type === filter);

  return (
    <div>
      <PageHeader
        eyebrow="Speech & Voice"
        title="Recipes"
        subtitle="Reusable voice configurations. Voice Mode runs whichever recipe is currently active."
        actions={<>
          <button className="btn btn-ghost btn-sm"><Icon name="invoice" size={12}/> Recipe docs</button>
          <button className="btn btn-primary btn-sm"><Icon name="plus" size={12}/> New recipe</button>
        </>}
      />

      {/* Banner explaining clone-before-edit */}
      <div style={{
        marginTop: 24, marginBottom: 16,
        background: 'var(--brand-primary-10)',
        border: '1px solid var(--brand-primary-20, rgba(5,90,96,0.2))',
        borderRadius: 12, padding: '14px 16px',
        display: 'flex', alignItems: 'flex-start', gap: 12,
      }}>
        <div style={{ width: 28, height: 28, borderRadius: 6, background: 'var(--brand-primary)', display: 'grid', placeItems: 'center', flex: 'none' }}>
          <Icon name="bolt" size={14} color="#fff"/>
        </div>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>Built-in recipes are read-only</div>
          <div style={{ fontSize: 12, color: 'var(--text-secondary)', marginTop: 2, lineHeight: 1.5 }}>
            To customise a built-in recipe, clone it first. Custom recipes are stored per workspace and can be edited at any time.
          </div>
        </div>
      </div>

      {/* Filter pills */}
      <div style={{ display: 'flex', gap: 6, marginBottom: 16, flexWrap: 'wrap' }}>
        {filters.map(f => {
          const active = filter === f.id;
          return (
            <button key={f.id} onClick={() => setFilter(f.id)} className={active ? 'btn btn-primary btn-sm' : 'btn btn-ghost btn-sm'} style={{ borderRadius: 999 }}>
              {f.label}
              <span style={{ marginLeft: 6, fontFamily: 'var(--font-mono)', fontSize: 11, opacity: 0.85 }}>{f.count}</span>
            </button>
          );
        })}
      </div>

      <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
        {visible.map(r => <RecipeCard key={r.id} r={r} onJump={onJump}/>)}
      </div>
    </div>
  );
}

function RecipeCard({ r, onJump }) {
  return (
    <div style={{
      ...(r.activeInVoiceMode ? SPEECH_STYLE.cardActive : SPEECH_STYLE.card),
      padding: 20,
    }}>
      {/* Header row */}
      <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: 16, marginBottom: 16 }}>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 4 }}>
            <div style={{ fontSize: 15, fontWeight: 600, color: 'var(--text-primary)' }}>{r.name}</div>
            {r.activeInVoiceMode && <Pill tone="success" size="sm" dot>Active in Voice Mode</Pill>}
            {r.kind === 'builtin'
              ? <Pill tone="default" size="sm">Built-in</Pill>
              : <Pill tone="warning"    size="sm">Custom</Pill>}
            <Pill tone={r.type === 'realtime' ? 'pending' : 'tint'} size="sm">
              {r.type === 'realtime' ? 'Realtime' : 'Chained'}
            </Pill>
          </div>
          <div style={{ fontSize: 12.5, color: 'var(--text-secondary)', lineHeight: 1.5 }}>{r.description}</div>
          {r.cloneOf && (
            <div style={{ fontSize: 11, color: 'var(--text-tertiary)', marginTop: 4 }}>
              <Icon name="copy" size={10}/> Cloned from <span style={{ color: 'var(--text-secondary)', fontWeight: 500 }}>{r.cloneOf}</span>
            </div>
          )}
        </div>
        <div style={{ display: 'flex', gap: 6, flex: 'none' }}>
          <button className="btn btn-ghost btn-sm"><Icon name="play" size={11}/> Test</button>
          {r.kind === 'builtin'
            ? <button className="btn btn-ghost btn-sm"><Icon name="copy" size={11}/> Clone</button>
            : <button className="btn btn-ghost btn-sm"><Icon name="edit" size={11}/> Edit</button>}
          {!r.activeInVoiceMode && (
            <button className="btn btn-primary btn-sm" onClick={() => onJump?.('voicemode')}>
              Activate
            </button>
          )}
        </div>
      </div>

      {/* Visual flow */}
      <RecipeFlow steps={r.steps} kind={r.type}/>
    </div>
  );
}

function RecipeFlow({ steps, kind }) {
  const realtime = kind === 'realtime';
  return (
    <div style={{
      background: 'var(--surface-inset)',
      borderRadius: 10, padding: 18,
      display: 'flex', alignItems: 'center', gap: 8,
      overflowX: 'auto',
    }}>
      {steps.map((s, i) => (
        <React.Fragment key={s.label}>
          <div style={{
            flex: realtime ? 1 : '0 1 auto',
            minWidth: realtime ? 0 : 150,
            background: 'var(--surface)',
            border: '1px solid var(--border-light)',
            borderRadius: 8,
            padding: '10px 12px',
            display: 'flex', alignItems: 'center', gap: 10,
          }}>
            <div style={{
              width: 30, height: 30, borderRadius: 6, flex: 'none',
              background: 'var(--brand-primary-10)',
              display: 'grid', placeItems: 'center',
            }}>
              <Icon name={s.icon} size={14} color="var(--brand-primary)"/>
            </div>
            <div style={{ flex: 1, minWidth: 0 }}>
              <div style={{ fontSize: 11.5, fontWeight: 600, color: 'var(--text-primary)' }}>{s.label}</div>
              <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', marginTop: 1, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{s.detail}</div>
            </div>
          </div>
          {i < steps.length - 1 && (
            <Icon name="arrowright" size={14} color="var(--text-tertiary)"/>
          )}
        </React.Fragment>
      ))}
    </div>
  );
}

// ─── Voice Mode ──────────────────────────────────────────────────────
function SpeechVoiceMode({ onJump }) {
  const [enabled, setEnabled] = React.useState(true);
  const active = RECIPES.find(r => r.activeInVoiceMode);

  return (
    <div>
      <PageHeader
        eyebrow="Speech & Voice"
        title="Voice Mode"
        subtitle="The live spoken conversation experience. One recipe is active at a time."
        actions={<>
          <button className="btn btn-ghost btn-sm"><Icon name="invoice" size={12}/> Voice Mode docs</button>
          <button className="btn btn-primary btn-sm"><Icon name="check" size={12}/> Save changes</button>
        </>}
      />

      {/* Hero status */}
      <div style={{
        marginTop: 24,
        background: enabled ? 'linear-gradient(135deg, var(--brand-primary), #044045)' : 'var(--surface-inset)',
        border: '1px solid ' + (enabled ? 'transparent' : 'var(--border-light)'),
        borderRadius: 14,
        padding: 24,
        color: enabled ? '#fff' : 'var(--text-primary)',
        display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 24,
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 18 }}>
          <div style={{
            width: 56, height: 56, borderRadius: 14, flex: 'none',
            background: enabled ? 'rgba(255,255,255,0.18)' : 'var(--surface)',
            border: enabled ? '1px solid rgba(255,255,255,0.25)' : '1px solid var(--border-light)',
            display: 'grid', placeItems: 'center',
          }}>
            <Icon name={enabled ? 'mic' : 'micoff'} size={26} color={enabled ? '#fff' : 'var(--text-tertiary)'}/>
          </div>
          <div>
            <div style={{ fontSize: 11, letterSpacing: '0.08em', textTransform: 'uppercase', fontWeight: 600, opacity: 0.85, marginBottom: 4 }}>
              {enabled ? 'Voice Mode is on' : 'Voice Mode is off'}
            </div>
            <div style={{ fontSize: 22, fontWeight: 600, marginBottom: 4 }}>
              {enabled ? active?.name : 'No recipe running'}
            </div>
            <div style={{ fontSize: 12.5, opacity: 0.8 }}>
              {enabled
                ? 'Operators can talk to agents in real time using the active recipe below.'
                : 'Spoken conversations are disabled across the workspace.'}
            </div>
          </div>
        </div>
        <div onClick={() => setEnabled(!enabled)} style={{
          width: 56, height: 32, borderRadius: 999, cursor: 'pointer', flex: 'none',
          background: enabled ? 'rgba(255,255,255,0.3)' : 'var(--gray-300, #cbd5e1)',
          position: 'relative',
        }}>
          <span style={{
            position: 'absolute', top: 3, left: enabled ? 27 : 3,
            width: 26, height: 26, borderRadius: '50%',
            background: '#fff', boxShadow: '0 1px 3px rgba(0,0,0,0.15)',
            transition: 'left 150ms ease',
          }}/>
        </div>
      </div>

      <div style={{ marginTop: 20, display: 'grid', gridTemplateColumns: '1fr 360px', gap: 16 }}>
        <div>
          {/* Active recipe */}
          <SettingsSection title="Active recipe" description="Voice Mode plays through this recipe. Switch by selecting another from the list below." action={
            <button className="btn btn-ghost btn-sm" onClick={() => onJump?.('recipes')}><Icon name="layers" size={12}/> Manage recipes</button>
          }>
            {active && (
              <div style={{ ...SPEECH_STYLE.cardActive, padding: 16 }}>
                <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: 12, marginBottom: 12 }}>
                  <div>
                    <div style={{ fontSize: 14, fontWeight: 600, color: 'var(--text-primary)' }}>{active.name}</div>
                    <div style={{ fontSize: 12, color: 'var(--text-secondary)', marginTop: 2 }}>{active.description}</div>
                  </div>
                  <Pill tone="success" size="sm" dot>Active</Pill>
                </div>
                <RecipeFlow steps={active.steps} kind={active.type}/>
              </div>
            )}

            <div style={{ marginTop: 14 }}>
              <div style={{ fontSize: 11, fontWeight: 600, color: 'var(--text-tertiary)', textTransform: 'uppercase', letterSpacing: '0.08em', marginBottom: 8 }}>Switch to</div>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                {RECIPES.filter(r => !r.activeInVoiceMode).map(r => (
                  <div key={r.id} style={{
                    background: 'var(--surface)', border: '1px solid var(--border-light)',
                    borderRadius: 10, padding: 12,
                    display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 12,
                    cursor: 'pointer',
                  }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 10, minWidth: 0 }}>
                      <div style={{ width: 30, height: 30, borderRadius: 6, background: 'var(--surface-inset)', display: 'grid', placeItems: 'center', flex: 'none' }}>
                        <Icon name={r.type === 'realtime' ? 'radio' : 'flow'} size={13} color="var(--text-secondary)"/>
                      </div>
                      <div style={{ minWidth: 0 }}>
                        <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>{r.name}</div>
                        <div style={{ fontSize: 11, color: 'var(--text-tertiary)', marginTop: 1 }}>
                          {r.type === 'realtime' ? 'Realtime' : 'Chained · 4 steps'} · {r.kind === 'builtin' ? 'Built-in' : 'Custom'}
                        </div>
                      </div>
                    </div>
                    <button className="btn btn-ghost btn-sm">Activate</button>
                  </div>
                ))}
              </div>
            </div>
          </SettingsSection>

          {/* Providers in use */}
          <SettingsSection title="Providers used by this recipe" description="Changing any of these providers affects every recipe that references them.">
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: 10 }}>
              {active?.steps.filter(s => s.label !== 'Agent').map((s, i) => (
                <div key={i} style={{
                  background: 'var(--surface-inset)', borderRadius: 10, padding: 12,
                  display: 'flex', alignItems: 'center', gap: 10,
                }}>
                  <div style={{ width: 32, height: 32, borderRadius: 6, background: 'var(--surface)', border: '1px solid var(--border-light)', display: 'grid', placeItems: 'center', flex: 'none' }}>
                    <Icon name={s.icon} size={14} color="var(--brand-primary)"/>
                  </div>
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <div style={{ fontSize: 11, color: 'var(--text-tertiary)', textTransform: 'uppercase', letterSpacing: '0.06em', fontWeight: 600 }}>{s.label}</div>
                    <div style={{ fontSize: 12.5, color: 'var(--text-primary)', marginTop: 2, fontWeight: 500 }}>{s.detail}</div>
                  </div>
                </div>
              ))}
            </div>
          </SettingsSection>
        </div>

        {/* Right rail — live test */}
        <div style={{ position: 'sticky', top: 0, alignSelf: 'flex-start', display: 'flex', flexDirection: 'column', gap: 12 }}>
          <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, padding: 18 }}>
            <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)', marginBottom: 4 }}>Live test</div>
            <div style={{ fontSize: 12, color: 'var(--text-secondary)', marginBottom: 14, lineHeight: 1.5 }}>
              Run a short conversation through the active recipe. Records an AiRun for audit and shows latency at each step.
            </div>

            <button className="btn btn-primary btn-sm" style={{ width: '100%', justifyContent: 'center', marginBottom: 12 }} disabled={!enabled}>
              <Icon name="mic" size={12}/> Start voice test
            </button>

            {/* Mic preview */}
            <div style={{
              background: 'var(--surface-inset)', borderRadius: 10, padding: 14,
              border: '1px dashed var(--border-light)',
              display: 'flex', alignItems: 'center', gap: 12, marginBottom: 12,
            }}>
              <div style={{ width: 36, height: 36, borderRadius: '50%', background: 'var(--brand-primary-10)', display: 'grid', placeItems: 'center', flex: 'none' }}>
                <Icon name="mic" size={16} color="var(--brand-primary)"/>
              </div>
              <svg viewBox="0 0 200 32" style={{ width: '100%', height: 32 }}>
                {Array.from({ length: 60 }).map((_, i) => {
                  const h = 4 + Math.abs(Math.sin(i * 0.7) * 14);
                  return <rect key={i} x={i * 3.3} y={(32 - h) / 2} width="2" height={h} rx="1" fill="var(--brand-primary)" opacity={0.6}/>;
                })}
              </svg>
            </div>

            <div style={{ ...SPEECH_STYLE.meta, display: 'flex', justifyContent: 'space-between' }}>
              <span>Latency budget · 800ms</span>
              <span>P50 · 612ms</span>
            </div>
          </div>

          <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, padding: 18 }}>
            <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)', marginBottom: 12 }}>Last 24 hours</div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
              <UsageRow label="Conversations" value="184"            pct={null}/>
              <UsageRow label="Avg duration"  value="2m 14s"          pct={null}/>
              <UsageRow label="STT minutes"   value="408 / 2,000"     pct={20}/>
              <UsageRow label="TTS characters" value="92,140 / 500k"   pct={18}/>
            </div>
          </div>

          <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, padding: 18 }}>
            <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)', marginBottom: 4 }}>Voice Mode vs Chat Speech</div>
            <div style={{ fontSize: 12, color: 'var(--text-secondary)', lineHeight: 1.55 }}>
              Voice Mode is the live spoken conversation. <span style={{ color: 'var(--text-primary)', fontWeight: 500 }}>Chat Speech</span> is optional voice-over for written replies — they share providers but configure independently.
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

// UsageRow is defined in screens/settings.jsx and reused here.

// ─── Chat Speech ─────────────────────────────────────────────────────
function SpeechChatSpeech({ onJump }) {
  const [enabled, setEnabled] = React.useState(true);
  const [voice, setVoice] = React.useState('aria');
  const voices = [
    { id: 'rachel',  provider: 'ElevenLabs', name: 'Rachel',  desc: 'American · Calm narration',         duration: '0:08' },
    { id: 'aria',    provider: 'ElevenLabs', name: 'Aria',    desc: 'British · Conversational',          duration: '0:09' },
    { id: 'antoni',  provider: 'ElevenLabs', name: 'Antoni',  desc: 'American · Warm, professional',     duration: '0:11' },
    { id: 'sarah',   provider: 'ElevenLabs', name: 'Sarah',   desc: 'British · News-anchor delivery',    duration: '0:07' },
    { id: 'voxtral', provider: 'Mistral',    name: 'Voxtral · Soft', desc: 'Multilingual · Soft tone',   duration: '0:10' },
    { id: 'maria',   provider: 'ElevenLabs', name: 'Maria',   desc: 'Cloned voice · 22s sample',         duration: '0:09', custom: true },
  ];

  return (
    <div>
      <PageHeader
        eyebrow="Speech & Voice"
        title="Chat Speech"
        subtitle="Speak written chat replies aloud. Independent of Voice Mode."
        actions={<>
          <button className="btn btn-ghost btn-sm"><Icon name="upload" size={12}/> Upload sample</button>
          <button className="btn btn-primary btn-sm"><Icon name="check" size={12}/> Save changes</button>
        </>}
      />

      {/* Helper banner — explains the difference */}
      <div style={{
        marginTop: 24, marginBottom: 16,
        background: 'var(--surface-inset)',
        border: '1px solid var(--border-light)',
        borderRadius: 12, padding: '12px 14px',
        display: 'flex', alignItems: 'center', gap: 10,
      }}>
        <Icon name="help" size={14} color="var(--text-secondary)"/>
        <div style={{ fontSize: 12, color: 'var(--text-secondary)', flex: 1, lineHeight: 1.5 }}>
          <span style={{ color: 'var(--text-primary)', fontWeight: 600 }}>Chat Speech</span> reads chat replies aloud. <span style={{ color: 'var(--text-primary)', fontWeight: 600 }}>Voice Mode</span> is live spoken conversation. They share providers but configure independently.
        </div>
        <button className="btn btn-ghost btn-sm" onClick={() => onJump?.('voicemode')}>Open Voice Mode</button>
      </div>

      {/* Hero status */}
      <div style={{
        background: 'var(--surface)',
        border: '1px solid var(--border-light)',
        borderRadius: 12, padding: 18,
        display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 16,
        marginBottom: 16,
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 14 }}>
          <div style={{
            width: 44, height: 44, borderRadius: 10,
            background: enabled ? 'var(--brand-primary-10)' : 'var(--surface-inset)',
            border: '1px solid var(--border-light)',
            display: 'grid', placeItems: 'center',
          }}>
            <Icon name={enabled ? 'speaker' : 'speakeroff'} size={22} color={enabled ? 'var(--brand-primary)' : 'var(--text-tertiary)'}/>
          </div>
          <div>
            <div style={{ fontSize: 14, fontWeight: 600, color: 'var(--text-primary)' }}>
              Chat Speech is {enabled ? 'on' : 'off'}
            </div>
            <div style={{ fontSize: 12.5, color: 'var(--text-secondary)', marginTop: 2 }}>
              {enabled
                ? 'Operators can play chat replies aloud from any chat surface.'
                : 'Spoken playback is disabled. Operators see text replies only.'}
            </div>
          </div>
        </div>
        <span onClick={() => setEnabled(!enabled)} style={{
          width: 44, height: 24, borderRadius: 999, cursor: 'pointer', flex: 'none',
          background: enabled ? 'var(--brand-primary)' : 'var(--gray-300, #cbd5e1)',
          position: 'relative', display: 'inline-block',
        }}>
          <span style={{
            position: 'absolute', top: 2, left: enabled ? 22 : 2,
            width: 20, height: 20, borderRadius: '50%', background: '#fff',
            transition: 'left 150ms ease', boxShadow: '0 1px 2px rgba(0,0,0,0.1)',
          }}/>
        </span>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 360px', gap: 16 }}>
        <div>
          <SettingsSection
            title="Voice"
            description="The voice used to read chat replies. Switching voice has no effect on Voice Mode."
            action={<button className="btn btn-ghost btn-sm"><Icon name="refresh" size={12}/> Refresh voices</button>}
          >
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: 10 }}>
              {voices.map(v => {
                const active = v.id === voice;
                return (
                  <div key={v.id} onClick={() => setVoice(v.id)} style={{
                    padding: 12, borderRadius: 10, cursor: 'pointer',
                    border: active ? '2px solid var(--brand-primary)' : '1px solid var(--border-light)',
                    background: active ? 'var(--brand-primary-10)' : 'var(--surface)',
                    display: 'flex', alignItems: 'center', gap: 12,
                  }}>
                    <div style={{
                      width: 36, height: 36, borderRadius: '50%', flex: 'none',
                      background: 'var(--surface-inset)', display: 'grid', placeItems: 'center',
                      border: '1px solid var(--border-light)',
                    }}>
                      <Icon name={active ? 'check' : 'speaker'} size={13} color={active ? 'var(--brand-primary)' : 'var(--text-secondary)'}/>
                    </div>
                    <div style={{ flex: 1, minWidth: 0 }}>
                      <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)', display: 'flex', alignItems: 'center', gap: 6 }}>
                        {v.name}
                        {v.custom && <Pill tone="success" size="sm">cloned</Pill>}
                      </div>
                      <div style={{ fontSize: 11, color: 'var(--text-secondary)', marginTop: 2 }}>{v.provider} · {v.desc}</div>
                    </div>
                    <button className="btn btn-ghost btn-sm" style={{ padding: '4px 8px' }}>
                      <Icon name="play" size={12}/>
                    </button>
                  </div>
                );
              })}
            </div>
          </SettingsSection>

          <SettingsSection title="Playback" description="How chat replies are spoken when Chat Speech is on.">
            <Field label="Auto-play replies" code="ChatSpeech.AutoPlay" help="Speak each reply automatically as it arrives. Operators can mute per-thread.">
              <Toggle on={false}/>
            </Field>
            <Field label="Show speak button" code="ChatSpeech.ShowSpeakButton" help="Adds a speaker icon next to each chat reply.">
              <Toggle on/>
            </Field>
            <Field label="Speed" code="ChatSpeech.Rate" help="1.0 is natural pace. 1.2 is slightly brisk.">
              <RangeRow value={1.0} suffix="1.0x"/>
            </Field>
          </SettingsSection>
        </div>

        {/* Right rail — preview */}
        <div style={{ position: 'sticky', top: 0, alignSelf: 'flex-start', display: 'flex', flexDirection: 'column', gap: 12 }}>
          <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, padding: 18 }}>
            <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)', marginBottom: 4 }}>Preview voice</div>
            <div style={{ fontSize: 12, color: 'var(--text-secondary)', marginBottom: 12, lineHeight: 1.5 }}>
              Synthesises a sample using the selected voice. Records an AiRun for audit.
            </div>
            <textarea className="input" rows={4}
              defaultValue="Three invoices are awaiting your review, and April fuel spending is trending twelve percent above plan."
              style={{ fontSize: 13, lineHeight: 1.5, resize: 'vertical', marginBottom: 12 }}/>
            <button className="btn btn-primary btn-sm" style={{ width: '100%', justifyContent: 'center', marginBottom: 12 }}>
              <Icon name="play" size={12}/> Synthesize &amp; play
            </button>

            {/* Waveform */}
            <div style={{ background: 'var(--surface-inset)', borderRadius: 8, padding: 14, marginBottom: 10 }}>
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 8 }}>
                <button className="btn btn-ghost btn-sm" style={{ padding: 4 }}><Icon name="play" size={14}/></button>
                <div style={{ ...SPEECH_STYLE.meta }}>0:00 / 0:09</div>
              </div>
              <svg viewBox="0 0 200 32" style={{ width: '100%', height: 32 }}>
                {Array.from({ length: 60 }).map((_, i) => {
                  const h = 6 + Math.abs(Math.sin(i * 0.6) * 12) + (i % 3) * 2;
                  return <rect key={i} x={i * 3.3} y={(32 - h) / 2} width="2" height={h} rx="1" fill="var(--brand-primary)" opacity={i < 24 ? 1 : 0.35}/>;
                })}
              </svg>
            </div>
            <div style={{ ...SPEECH_STYLE.meta, display: 'flex', justifyContent: 'space-between' }}>
              <span>AiRunId · 7f9a-21c</span>
              <span>312ms · 14kb</span>
            </div>
          </div>

          <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, padding: 18 }}>
            <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)', marginBottom: 12 }}>Usage · this month</div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
              <UsageRow label="Characters spoken" value="142,408 / 500,000" pct={28}/>
              <UsageRow label="Cost"              value="$11.40 / $40 limit" pct={28}/>
              <UsageRow label="Replies played"    value="1,204"               pct={null}/>
            </div>
          </div>

          <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 12, padding: 18 }}>
            <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)', marginBottom: 6 }}>Need a custom voice?</div>
            <div style={{ fontSize: 12, color: 'var(--text-secondary)', marginBottom: 12, lineHeight: 1.55 }}>
              Upload a 30-second clean sample to clone a voice. Cloned voices appear with a green badge in the picker.
            </div>
            <button className="btn btn-ghost btn-sm" style={{ width: '100%', justifyContent: 'center' }}>
              <Icon name="upload" size={12}/> Upload sample
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

// ─── Screen exports — one per artboard ──────────────────────────────
function ScreenSpeechProviders()  { return <SpeechShell initial="providers"/>;  }
function ScreenSpeechRecipes()    { return <SpeechShell initial="recipes"/>;    }
function ScreenSpeechVoiceMode()  { return <SpeechShell initial="voicemode"/>;  }
function ScreenSpeechChatSpeech() { return <SpeechShell initial="chatspeech"/>; }
