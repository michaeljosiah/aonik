// Forms screens — three patterns, sized to data:
//   1. Slide-out panel  — small/medium contextual entry (Add bank account)
//   2. Modal dialog     — atomic action, 2-3 fields    (Invite user)
//   3. Full page        — long multi-section form      (New customer · KYB)

// ─── Field primitives ──────────────────────────────────────────────
function FieldLabel({ children, required, hint }) {
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginBottom: 6 }}>
      <span style={{ fontSize: 11.5, fontWeight: 500, color: 'var(--text-secondary)', letterSpacing: '0.01em' }}>
        {children}
        {required && <span style={{ color: 'var(--brand-secondary)', marginLeft: 2 }}>*</span>}
      </span>
      {hint && <span style={{ fontSize: 10, color: 'var(--text-tertiary)', marginLeft: 'auto' }}>{hint}</span>}
    </div>
  );
}

function TextField({ label, required, hint, placeholder, value, mono, prefix, suffix, helper, error }) {
  return (
    <div>
      <FieldLabel required={required} hint={hint}>{label}</FieldLabel>
      <div style={{
        display: 'flex', alignItems: 'center',
        background: 'var(--surface)',
        border: `1px solid ${error ? 'var(--danger)' : 'var(--border)'}`,
        borderBottom: `2px solid ${error ? 'var(--danger)' : 'var(--border)'}`,
        borderRadius: 'var(--radius-md)', height: 38, padding: '0 12px',
      }}>
        {prefix && <span style={{ fontSize: 12.5, color: 'var(--text-tertiary)', fontFamily: mono ? 'var(--font-mono)' : 'inherit', marginRight: 8 }}>{prefix}</span>}
        <input
          defaultValue={value} placeholder={placeholder}
          style={{
            flex: 1, border: 'none', background: 'transparent', outline: 'none',
            fontFamily: mono ? 'var(--font-mono)' : 'inherit',
            fontSize: 13, color: 'var(--text-primary)', padding: 0,
          }}
        />
        {suffix && <span style={{ fontSize: 12, color: 'var(--text-tertiary)' }}>{suffix}</span>}
      </div>
      {helper && <div style={{ fontSize: 10.5, color: error ? 'var(--danger)' : 'var(--text-tertiary)', marginTop: 4 }}>{helper}</div>}
    </div>
  );
}

function SelectField({ label, required, hint, value, suffix = 'chevdown' }) {
  return (
    <div>
      <FieldLabel required={required} hint={hint}>{label}</FieldLabel>
      <div style={{
        display: 'flex', alignItems: 'center', justifyContent: 'space-between',
        background: 'var(--surface)', border: '1px solid var(--border)',
        borderBottom: '2px solid var(--border)', borderRadius: 'var(--radius-md)',
        height: 38, padding: '0 12px', cursor: 'pointer',
      }}>
        <span style={{ fontSize: 13, color: value ? 'var(--text-primary)' : 'var(--text-tertiary)' }}>{value || 'Select…'}</span>
        <Icon name={suffix} size={14} color="var(--text-tertiary)"/>
      </div>
    </div>
  );
}

function TextArea({ label, required, hint, placeholder, value, rows = 3, helper, max }) {
  return (
    <div>
      <FieldLabel required={required} hint={hint}>{label}</FieldLabel>
      <div style={{
        background: 'var(--surface)', border: '1px solid var(--border)',
        borderBottom: '2px solid var(--border)', borderRadius: 'var(--radius-md)',
        padding: '8px 12px',
      }}>
        <textarea
          rows={rows} defaultValue={value} placeholder={placeholder}
          style={{
            width: '100%', border: 'none', background: 'transparent', outline: 'none',
            resize: 'none', fontFamily: 'inherit', fontSize: 13, color: 'var(--text-primary)',
            lineHeight: 1.5, padding: 0,
          }}
        />
      </div>
      {(helper || max) && (
        <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: 4 }}>
          {helper && <span style={{ fontSize: 10.5, color: 'var(--text-tertiary)' }}>{helper}</span>}
          {max && <span style={{ fontSize: 10.5, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)', marginLeft: 'auto' }}>{(value || '').length}/{max}</span>}
        </div>
      )}
    </div>
  );
}

function ToggleField({ label, description, on }) {
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 12, padding: '10px 0' }}>
      <div style={{ flex: 1 }}>
        <div style={{ fontSize: 12.5, fontWeight: 500, color: 'var(--text-primary)' }}>{label}</div>
        {description && <div style={{ fontSize: 11, color: 'var(--text-secondary)', marginTop: 2 }}>{description}</div>}
      </div>
      <span style={{
        width: 32, height: 18, borderRadius: 999, padding: 2, flex: 'none',
        background: on ? 'var(--brand-primary)' : 'var(--gray-300)',
        display: 'inline-flex', alignItems: 'center',
      }}>
        <span style={{
          width: 14, height: 14, borderRadius: 999, background: '#fff',
          transform: on ? 'translateX(14px)' : 'translateX(0)',
          transition: 'transform 150ms',
        }}/>
      </span>
    </div>
  );
}

function RadioCard({ icon, title, description, selected }) {
  return (
    <div style={{
      flex: 1, padding: '12px 14px', borderRadius: 8, cursor: 'pointer',
      background: selected ? 'var(--brand-primary-10)' : 'var(--surface)',
      border: `1px solid ${selected ? 'var(--brand-primary)' : 'var(--border-light)'}`,
      display: 'flex', alignItems: 'flex-start', gap: 10,
    }}>
      <span style={{
        width: 16, height: 16, borderRadius: 999, flex: 'none', marginTop: 1,
        border: `1.5px solid ${selected ? 'var(--brand-primary)' : 'var(--gray-400)'}`,
        background: selected ? 'var(--brand-primary)' : 'transparent',
        boxShadow: selected ? 'inset 0 0 0 3px var(--surface)' : 'none',
      }}/>
      <div style={{ flex: 1 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>
          {icon && <Icon name={icon} size={14} color={selected ? 'var(--brand-primary)' : 'var(--text-secondary)'}/>}
          {title}
        </div>
        {description && <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', marginTop: 3, lineHeight: 1.5 }}>{description}</div>}
      </div>
    </div>
  );
}

// ─── 1. Slide-out panel — Add bank account ─────────────────────────
function SlideOutPanel() {
  return (
    <div style={{
      position: 'absolute', top: 0, right: 0, bottom: 0, width: 460,
      background: 'var(--surface)', borderLeft: '1px solid var(--border-light)',
      boxShadow: '-12px 0 32px -8px rgb(0 0 0 / 0.08)',
      display: 'flex', flexDirection: 'column',
    }}>
      {/* header */}
      <div style={{
        padding: '16px 20px', borderBottom: '1px solid var(--border-light)',
        display: 'flex', alignItems: 'center', gap: 10,
      }}>
        <div style={{
          width: 32, height: 32, borderRadius: 8,
          background: 'var(--brand-primary-10)', color: 'var(--brand-primary)',
          display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
        }}><Icon name="bank" size={16}/></div>
        <div style={{ flex: 1 }}>
          <div style={{ fontSize: 14, fontWeight: 600, color: 'var(--text-primary)' }}>Add bank account</div>
          <div style={{ fontSize: 11, color: 'var(--text-secondary)' }}>Primrose Logistics · Operating</div>
        </div>
        <span className="hover-halo"><Icon name="close" size={14}/></span>
      </div>

      {/* progress dots */}
      <div style={{ padding: '12px 20px 0', display: 'flex', alignItems: 'center', gap: 10 }}>
        {[
          { n: 1, l: 'Account', done: true },
          { n: 2, l: 'Routing', done: false, active: true },
          { n: 3, l: 'Review',  done: false },
        ].map((s, i, arr) => (
          <React.Fragment key={i}>
            <div style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}>
              <span style={{
                width: 18, height: 18, borderRadius: 999, fontSize: 10, fontWeight: 600,
                fontFamily: 'var(--font-mono)',
                display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
                background: s.done ? 'var(--success)' : s.active ? 'var(--brand-primary)' : 'var(--gray-200)',
                color: (s.done || s.active) ? '#fff' : 'var(--text-tertiary)',
              }}>{s.done ? <Icon name="check" size={10}/> : s.n}</span>
              <span style={{ fontSize: 11, fontWeight: s.active ? 600 : 400, color: s.active ? 'var(--text-primary)' : 'var(--text-secondary)' }}>{s.l}</span>
            </div>
            {i < arr.length - 1 && <span style={{ flex: 1, height: 1, background: 'var(--border-light)' }}/>}
          </React.Fragment>
        ))}
      </div>

      {/* body */}
      <div style={{ flex: 1, overflow: 'auto', padding: '20px', display: 'flex', flexDirection: 'column', gap: 14 }}>

        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
          <TextField label="Account nickname" required value="Operating · GBP"/>
          <SelectField label="Currency" required value="GBP — British Pound"/>
        </div>

        <TextField label="Institution" required value="Barclays Bank PLC" suffix="✓"/>

        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
          <TextField label="Sort code" required mono value="20-00-00" placeholder="00-00-00"/>
          <TextField label="Account number" required mono value="83726491" placeholder="8 digits"/>
        </div>

        <TextField label="IBAN" mono prefix="GB" placeholder="29 NWBK 6016 1331 9268 19" helper="Optional · used for SEPA / SWIFT routing"/>

        <div>
          <FieldLabel>Linked ledger account</FieldLabel>
          <div style={{
            display: 'flex', alignItems: 'center', gap: 10, padding: 12,
            background: 'var(--surface-inset)', borderRadius: 8, border: '1px dashed var(--border)',
          }}>
            <Icon name="sparkles" size={14} color="var(--brand-primary)"/>
            <div style={{ flex: 1, fontSize: 12, color: 'var(--text-primary)' }}>
              Ledger Agent suggests <b>1010 · Cash at Bank — GBP Operating</b>
            </div>
            <button className="btn btn-outline btn-sm" style={{ height: 24, padding: '0 8px', fontSize: 11 }}>Use</button>
          </div>
        </div>

        <div style={{ borderTop: '1px solid var(--border-light)', paddingTop: 12, marginTop: 4 }}>
          <ToggleField label="Auto-import transactions" description="Pull daily via Open Banking" on/>
          <ToggleField label="Reconcile with Ledger Agent" description="Match incoming txns to invoices automatically" on/>
          <ToggleField label="Alert on threshold breach" description="Notify when balance falls below floor" on={false}/>
        </div>
      </div>

      {/* footer */}
      <div style={{
        padding: '14px 20px', borderTop: '1px solid var(--border-light)',
        background: 'var(--surface-inset)',
        display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 10,
      }}>
        <button className="btn btn-ghost btn-sm">Save as draft</button>
        <div style={{ display: 'flex', gap: 8 }}>
          <button className="btn btn-outline btn-sm">Back</button>
          <button className="btn btn-primary btn-sm">Continue<Icon name="arrowright" size={12}/></button>
        </div>
      </div>
    </div>
  );
}

// Background list to make slide-out feel contextual
function AccountsBackdrop() {
  return (
    <div style={{ padding: '24px 32px' }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 14, marginBottom: 18 }}>
        <h1 style={{ fontFamily: 'var(--font-brand)', fontSize: 22, letterSpacing: '-0.01em' }}>Bank accounts</h1>
        <Pill tone="tint">4 connected</Pill>
        <div style={{ flex: 1 }}/>
        <button className="btn btn-outline btn-sm"><Icon name="filter" size={12}/> Filter</button>
        <button className="btn btn-primary btn-sm"><Icon name="plus" size={12}/> Add account</button>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14, opacity: 0.45 }}>
        {[
          { name: 'Operating · GBP', inst: 'Barclays', bal: '£128,420.14' },
          { name: 'Payroll · GBP',   inst: 'Barclays', bal: '£42,108.00' },
          { name: 'FX Buffer · USD', inst: 'Wise',     bal: '$86,410.22' },
          { name: 'NGN Settlement',  inst: 'Zenith',   bal: '₦41,820,000' },
        ].map((a, i) => (
          <div key={i} style={{
            background: 'var(--surface)', border: '1px solid var(--border-light)',
            borderRadius: 10, padding: 16,
          }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
              <div style={{ width: 32, height: 32, borderRadius: 8, background: 'var(--brand-primary-10)', color: 'var(--brand-primary)', display: 'inline-flex', alignItems: 'center', justifyContent: 'center' }}>
                <Icon name="bank" size={15}/>
              </div>
              <div style={{ flex: 1 }}>
                <div style={{ fontSize: 13, fontWeight: 600 }}>{a.name}</div>
                <div style={{ fontSize: 11, color: 'var(--text-tertiary)' }}>{a.inst}</div>
              </div>
            </div>
            <div style={{ fontFamily: 'var(--font-mono)', fontSize: 22, fontWeight: 600, marginTop: 10 }}>{a.bal}</div>
          </div>
        ))}
      </div>
    </div>
  );
}

function ScreenFormSlideOut() {
  return (
    <div style={{ position: 'relative', height: '100%', overflow: 'hidden' }}>
      <div style={{ position: 'absolute', inset: 0, background: 'rgba(20, 25, 30, 0.18)' }}/>
      <AccountsBackdrop/>
      <SlideOutPanel/>
    </div>
  );
}

// ─── 2. Popup dialog — Invite user ─────────────────────────────────
function ScreenFormDialog() {
  return (
    <div style={{ position: 'relative', height: '100%', overflow: 'hidden' }}>
      <UsersBackdrop/>
      {/* scrim */}
      <div style={{ position: 'absolute', inset: 0, background: 'rgba(15, 20, 28, 0.42)', backdropFilter: 'blur(2px)' }}/>

      {/* dialog */}
      <div style={{
        position: 'absolute', top: '50%', left: '50%', transform: 'translate(-50%, -50%)',
        width: 460, background: 'var(--surface)',
        borderRadius: 14, boxShadow: '0 24px 60px -10px rgb(0 0 0 / 0.35)',
        border: '1px solid var(--border-light)',
        display: 'flex', flexDirection: 'column',
      }}>
        <div style={{
          padding: '18px 22px 14px', borderBottom: '1px solid var(--border-light)',
          display: 'flex', alignItems: 'center', gap: 12,
        }}>
          <div style={{
            width: 36, height: 36, borderRadius: 10,
            background: 'var(--brand-primary-10)', color: 'var(--brand-primary)',
            display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
          }}><Icon name="user" size={18}/></div>
          <div style={{ flex: 1 }}>
            <div style={{ fontSize: 15, fontWeight: 600, color: 'var(--text-primary)' }}>Invite teammate</div>
            <div style={{ fontSize: 12, color: 'var(--text-secondary)' }}>They'll get a sign-in link by email.</div>
          </div>
          <span className="hover-halo"><Icon name="close" size={14}/></span>
        </div>

        <div style={{ padding: '18px 22px', display: 'flex', flexDirection: 'column', gap: 14 }}>
          <TextField label="Email" required placeholder="name@primrose.co" value="amara@primrose.co"/>

          <div>
            <FieldLabel required>Role</FieldLabel>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
              <RadioCard icon="users2" title="Member" description="Can view and edit; cannot manage billing or users." selected/>
              <RadioCard icon="shield" title="Admin"  description="Full access including billing, users, and policies."/>
              <RadioCard icon="eye"    title="Viewer" description="Read-only across the workspace."/>
            </div>
          </div>

          <div style={{
            display: 'flex', alignItems: 'center', gap: 10, padding: '10px 12px',
            background: 'var(--surface-inset)', border: '1px solid var(--border-light)', borderRadius: 8,
          }}>
            <Icon name="lock" size={14} color="var(--text-secondary)"/>
            <span style={{ fontSize: 11.5, color: 'var(--text-secondary)', flex: 1 }}>MFA required after first sign-in</span>
            <Pill tone="success" dot size="sm">policy</Pill>
          </div>
        </div>

        <div style={{
          padding: '14px 22px', borderTop: '1px solid var(--border-light)',
          background: 'var(--surface-inset)',
          display: 'flex', justifyContent: 'flex-end', gap: 8,
          borderBottomLeftRadius: 14, borderBottomRightRadius: 14,
        }}>
          <button className="btn btn-ghost btn-sm">Cancel</button>
          <button className="btn btn-primary btn-sm"><Icon name="send" size={12}/> Send invite</button>
        </div>
      </div>
    </div>
  );
}

function UsersBackdrop() {
  return (
    <div style={{ padding: '24px 32px' }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 14, marginBottom: 18 }}>
        <h1 style={{ fontFamily: 'var(--font-brand)', fontSize: 22, letterSpacing: '-0.01em' }}>Users</h1>
        <Pill tone="tint">8 active</Pill>
        <div style={{ flex: 1 }}/>
        <button className="btn btn-primary btn-sm"><Icon name="plus" size={12}/> Invite</button>
      </div>
      <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, opacity: 0.5 }}>
        {[
          { n: 'Oliver Chen',    e: 'oliver@primrose.co', r: 'Admin',  c: '#7b76b6' },
          { n: 'Maria Gomez',    e: 'maria@primrose.co',  r: 'Member', c: '#055a60' },
          { n: 'James Okonkwo',  e: 'james@primrose.co',  r: 'Member', c: '#3ab795' },
          { n: 'Sarah Williams', e: 'sarah@primrose.co',  r: 'Viewer', c: '#eb5c37' },
          { n: 'David Park',     e: 'david@primrose.co',  r: 'Admin',  c: '#5facbd' },
        ].map((u, i, arr) => (
          <div key={i} style={{
            display: 'grid', gridTemplateColumns: '40px 1fr auto auto auto',
            alignItems: 'center', gap: 14, padding: '12px 16px',
            borderBottom: i < arr.length - 1 ? '1px solid var(--border-light)' : 'none',
          }}>
            <Avatar name={u.n} size={32} color={u.c} textColor="#fff"/>
            <div>
              <div style={{ fontSize: 13, fontWeight: 500 }}>{u.n}</div>
              <div style={{ fontSize: 11, color: 'var(--text-tertiary)' }}>{u.e}</div>
            </div>
            <Pill tone="tint" size="sm">{u.r}</Pill>
            <Pill tone="success" dot size="sm">MFA</Pill>
            <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11, color: 'var(--text-tertiary)' }}>2h ago</span>
          </div>
        ))}
      </div>
    </div>
  );
}

// ─── 3. Full page — New customer · KYB ─────────────────────────────
function ScreenFormFullPage() {
  return (
    <div style={{ display: 'flex', height: '100%' }}>
      {/* Left: section nav */}
      <aside style={{
        width: 240, flex: 'none', background: 'var(--surface-inset)',
        borderRight: '1px solid var(--border-light)', padding: '24px 16px',
      }}>
        <div style={{ fontSize: 10, letterSpacing: '0.08em', color: 'var(--text-tertiary)', textTransform: 'uppercase', fontWeight: 600, padding: '0 10px 10px' }}>
          New customer
        </div>
        {[
          { n: 1, l: 'Entity',            done: true },
          { n: 2, l: 'Registration',      done: true },
          { n: 3, l: 'Beneficial owners', done: false, active: true },
          { n: 4, l: 'Banking',           done: false },
          { n: 5, l: 'Tax & invoicing',   done: false },
          { n: 6, l: 'Documents',         done: false },
          { n: 7, l: 'Review',            done: false },
        ].map((s, i, arr) => (
          <div key={i} style={{ position: 'relative', display: 'flex', alignItems: 'center', gap: 10, padding: '8px 10px', borderRadius: 6,
            background: s.active ? 'var(--surface)' : 'transparent',
            border: s.active ? '1px solid var(--border-light)' : '1px solid transparent',
            color: s.active ? 'var(--text-primary)' : 'var(--text-secondary)',
          }}>
            {i < arr.length - 1 && (
              <span style={{ position: 'absolute', left: 21, top: 30, bottom: -4, width: 1, background: s.done ? 'var(--success)' : 'var(--border-light)' }}/>
            )}
            <span style={{
              width: 22, height: 22, borderRadius: 999, fontSize: 10, fontWeight: 700, fontFamily: 'var(--font-mono)',
              display: 'inline-flex', alignItems: 'center', justifyContent: 'center', flex: 'none',
              background: s.done ? 'var(--success)' : s.active ? 'var(--brand-primary)' : 'var(--surface)',
              color: (s.done || s.active) ? '#fff' : 'var(--text-tertiary)',
              border: !s.done && !s.active ? '1px solid var(--border)' : 'none', position: 'relative', zIndex: 1,
            }}>{s.done ? <Icon name="check" size={11}/> : s.n}</span>
            <span style={{ fontSize: 12.5, fontWeight: s.active ? 600 : 400 }}>{s.l}</span>
          </div>
        ))}

        <div style={{ marginTop: 24, padding: '12px 12px', background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 8 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 11, fontWeight: 600, color: 'var(--brand-primary)', marginBottom: 6 }}>
            <Icon name="sparkles" size={12}/> KYB Agent
          </div>
          <div style={{ fontSize: 11, color: 'var(--text-secondary)', lineHeight: 1.5 }}>
            I've prefilled <b>4 of 7</b> sections from Companies House. Review the highlighted fields.
          </div>
        </div>
      </aside>

      {/* Center: form */}
      <main style={{ flex: 1, overflow: 'auto', padding: '24px 36px', maxWidth: 880 }}>
        {/* breadcrumb / header */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginBottom: 4 }}>
          <span style={{ fontSize: 11, fontFamily: 'var(--font-mono)', color: 'var(--text-tertiary)', letterSpacing: '0.04em' }}>STEP 3 OF 7</span>
          <span style={{ flex: 1 }}/>
          <Pill tone="warning" dot size="sm">Auto-saved · 14s ago</Pill>
        </div>
        <h1 style={{ fontFamily: 'var(--font-brand)', fontSize: 26, fontWeight: 700, letterSpacing: '-0.015em', marginTop: 4 }}>
          Beneficial owners
        </h1>
        <p style={{ fontSize: 13, color: 'var(--text-secondary)', marginTop: 6, lineHeight: 1.6 }}>
          List every individual who owns ≥25% of the entity, or otherwise exercises control. We'll run sanctions and PEP screening on each.
        </p>

        {/* Owner 1 — filled */}
        <section style={{
          marginTop: 24, background: 'var(--surface)', border: '1px solid var(--border-light)',
          borderRadius: 12, padding: 20, display: 'flex', flexDirection: 'column', gap: 14,
        }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, paddingBottom: 4 }}>
            <span style={{
              width: 26, height: 26, borderRadius: 999, background: 'var(--brand-primary-10)',
              color: 'var(--brand-primary)', fontSize: 12, fontWeight: 700,
              display: 'inline-flex', alignItems: 'center', justifyContent: 'center', fontFamily: 'var(--font-mono)',
            }}>1</span>
            <span style={{ fontSize: 14, fontWeight: 600 }}>Owner 1</span>
            <Pill tone="success" dot size="sm">Sanctions clear</Pill>
            <div style={{ flex: 1 }}/>
            <span className="hover-halo"><Icon name="edit" size={13}/></span>
            <span className="hover-halo"><Icon name="trash" size={13}/></span>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14 }}>
            <TextField label="Full legal name" required value="Aderonke Adebayo"/>
            <TextField label="Date of birth" required mono value="1978-04-12" suffix={<Icon name="calendar" size={12}/>}/>
            <TextField label="Nationality" required value="Nigerian · British"/>
            <TextField label="% Ownership" required mono value="48.0" suffix="%"/>
            <TextField label="Role" required value="Director · CEO"/>
            <SelectField label="ID type" required value="Passport · NG"/>
          </div>

          <TextField label="Residential address" required value="14 Dock Road, London E16 1AD"/>
        </section>

        {/* Owner 2 — empty/active */}
        <section style={{
          marginTop: 14, background: 'var(--surface)',
          border: '2px solid var(--brand-primary)',
          borderRadius: 12, padding: 20, display: 'flex', flexDirection: 'column', gap: 14,
          boxShadow: '0 4px 0 -2px var(--brand-primary-60)',
        }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <span style={{
              width: 26, height: 26, borderRadius: 999, background: 'var(--brand-primary)',
              color: '#fff', fontSize: 12, fontWeight: 700,
              display: 'inline-flex', alignItems: 'center', justifyContent: 'center', fontFamily: 'var(--font-mono)',
            }}>2</span>
            <span style={{ fontSize: 14, fontWeight: 600 }}>Owner 2</span>
            <Pill tone="pending" dot size="sm">In progress</Pill>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14 }}>
            <TextField label="Full legal name" required value="Kemi Osagie"/>
            <TextField label="Date of birth" required mono value="" placeholder="YYYY-MM-DD"/>
            <TextField label="Nationality" required value=""/>
            <TextField label="% Ownership" required mono value="27.5" suffix="%"/>
          </div>

          <div style={{
            display: 'flex', alignItems: 'center', gap: 10, padding: 12,
            background: 'var(--brand-primary-10)', border: '1px solid transparent',
            borderRadius: 8,
          }}>
            <Icon name="sparkles" size={14} color="var(--brand-primary)"/>
            <span style={{ fontSize: 12, color: 'var(--text-primary)', flex: 1 }}>
              KYB Agent found <b>Kemi Osagie</b> in Companies House filing — born 1985-09-03, Nigerian.
            </span>
            <button className="btn btn-outline btn-sm" style={{ height: 26, padding: '0 10px', fontSize: 11 }}>Apply prefill</button>
          </div>
        </section>

        <button style={{
          width: '100%', marginTop: 14, padding: 14,
          background: 'transparent', border: '1px dashed var(--border)', borderRadius: 12,
          fontSize: 13, color: 'var(--text-secondary)', cursor: 'pointer',
          display: 'inline-flex', alignItems: 'center', justifyContent: 'center', gap: 8,
        }}>
          <Icon name="plus" size={14}/> Add another beneficial owner
        </button>

        <div style={{ marginTop: 24, padding: '20px 0', borderTop: '1px solid var(--border-light)',
          display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <button className="btn btn-outline"><Icon name="arrowright" size={12} color="var(--text-secondary)"/>Back</button>
          <div style={{ display: 'flex', gap: 8 }}>
            <button className="btn btn-ghost">Save & exit</button>
            <button className="btn btn-primary">Continue<Icon name="arrowright" size={12}/></button>
          </div>
        </div>
      </main>

      {/* Right: summary rail */}
      <aside style={{
        width: 280, flex: 'none', borderLeft: '1px solid var(--border-light)',
        padding: '24px 18px', overflow: 'auto', background: 'var(--surface)',
      }}>
        <div style={{ fontSize: 10, letterSpacing: '0.08em', color: 'var(--text-tertiary)', textTransform: 'uppercase', fontWeight: 600, marginBottom: 12 }}>
          Summary
        </div>

        <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
          {[
            { l: 'Legal name',  v: 'Northstar Logistics NG Ltd' },
            { l: 'Country',     v: 'Nigeria' },
            { l: 'Reg. number', v: 'RC-2849124', mono: true },
            { l: 'Type',        v: 'Corporate · Pvt Ltd' },
            { l: 'Industry',    v: 'Logistics & Freight' },
            { l: 'Owners',      v: '1 of 2 verified' },
            { l: 'Documents',   v: '— pending —' },
          ].map((r, i) => (
            <div key={i} style={{ display: 'grid', gridTemplateColumns: '1fr', gap: 2 }}>
              <span style={{ fontSize: 10.5, color: 'var(--text-tertiary)', letterSpacing: '0.04em' }}>{r.l.toUpperCase()}</span>
              <span style={{ fontSize: 12.5, color: 'var(--text-primary)', fontFamily: r.mono ? 'var(--font-mono)' : 'inherit' }}>{r.v}</span>
            </div>
          ))}
        </div>

        <div style={{ marginTop: 22, padding: 12, background: 'var(--surface-inset)', borderRadius: 8, border: '1px solid var(--border-light)' }}>
          <div style={{ fontSize: 11, fontWeight: 600, color: 'var(--text-primary)', display: 'flex', alignItems: 'center', gap: 6 }}>
            <Icon name="clock" size={11} color="var(--text-secondary)"/> Expected time
          </div>
          <div style={{ fontFamily: 'var(--font-mono)', fontSize: 18, fontWeight: 600, marginTop: 4 }}>~ 4 min</div>
          <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', marginTop: 2 }}>4 fields remaining · agent prefill 78%</div>
        </div>
      </aside>
    </div>
  );
}

Object.assign(window, { ScreenFormSlideOut, ScreenFormDialog, ScreenFormFullPage });
