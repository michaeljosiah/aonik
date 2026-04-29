// Invoice edit · slide-out panel over the Invoices list
// Mirrors SlideOutPanel chrome (header → progress dots → body → footer)
// but specifically for editing an existing invoice with line items.

function ScreenInvoiceEdit() {
  return (
    <div style={{ position: 'relative', height: '100%', overflow: 'hidden' }}>
      {/* Scrim — same as Add bank account */}
      <div style={{ position: 'absolute', inset: 0, background: 'rgba(20, 25, 30, 0.18)', zIndex: 1 }}/>
      <InvoicesBackdrop/>
      <InvoiceEditPanel/>
    </div>
  );
}

// Faded list of invoices in the background, so the slide-out feels contextual.
function InvoicesBackdrop() {
  const rows = [
    { id: 'INV-2041', party: 'Primrose Logistics', amount: '$12,480.00', status: 'proposed', tone: 'pending', date: '19 Apr', memo: 'Matched to bank_txn_9f2c1a', editing: true },
    { id: 'INV-2040', party: 'Apex Fabrication Ltd', amount: '$4,290.00',  status: 'paid',     tone: 'success', date: '18 Apr', memo: 'Paid via ACH' },
    { id: 'INV-2039', party: 'Meridian Studio',      amount: '$8,750.00',  status: 'paid',     tone: 'success', date: '18 Apr', memo: 'Paid via wire' },
    { id: 'INV-2038', party: 'Northstar Freight',    amount: '$22,100.00', status: 'proposed', tone: 'pending', date: '17 Apr', memo: 'Partial payment detected' },
    { id: 'INV-2037', party: 'Blue Harbor Co',       amount: '$3,190.00',  status: 'overdue',  tone: 'danger',  date: '12 Apr', memo: 'Dunning letter queued' },
    { id: 'INV-2036', party: 'Quill & Co',           amount: '$1,840.00',  status: 'draft',    tone: 'muted',   date: '16 Apr', memo: 'Awaiting review' },
    { id: 'INV-2035', party: 'Cedar Analytics',      amount: '$14,600.00', status: 'paid',     tone: 'success', date: '14 Apr', memo: 'Paid via card' },
    { id: 'INV-2034', party: 'Orinoco Textiles',     amount: '$6,430.00',  status: 'pending',  tone: 'warning', date: '14 Apr', memo: 'Awaiting FX confirmation' },
  ];

  return (
    <div style={{ padding: '24px 32px' }}>
      <PageHeader
        eyebrow="Finance · Ledger"
        title="Invoices"
        subtitle="324 open · $128,431 outstanding · 2 agent proposals"
        actions={
          <>
            <button className="btn btn-outline btn-sm"><Icon name="download" size={12}/> Export</button>
            <button className="btn btn-primary btn-sm"><Icon name="plus" size={12}/> New invoice</button>
          </>
        }
      />

      <div style={{ marginTop: 20 }}>
        <FilterBar
          tabs={['All', 'Proposed', 'Open', 'Overdue', 'Paid']}
          active="All"
          counts={{ 'Proposed': 2, 'Overdue': 14 }}
          search="Filter by party, ref, amount…"
          extra={<button className="btn btn-ghost btn-sm"><Icon name="calendar" size={12}/> Apr 2026</button>}
        />
      </div>

      {/* Faded table */}
      <div style={{
        marginTop: 20,
        background: 'var(--surface)', border: '1px solid var(--border-light)',
        borderRadius: 10, overflow: 'hidden', opacity: 0.55,
      }}>
        <div style={{
          display: 'grid', gridTemplateColumns: '100px 1fr 1fr 90px 120px 120px',
          padding: '10px 16px', gap: 14,
          background: 'var(--surface-inset)',
          fontSize: 10, fontWeight: 600, letterSpacing: '0.06em', textTransform: 'uppercase',
          color: 'var(--text-tertiary)', borderBottom: '1px solid var(--border-light)',
        }}>
          <div>Invoice</div><div>Counterparty</div><div>Memo</div><div>Date</div><div>Status</div>
          <div style={{ textAlign: 'right' }}>Amount</div>
        </div>
        {rows.map((r, i) => (
          <div key={r.id} style={{
            display: 'grid', gridTemplateColumns: '100px 1fr 1fr 90px 120px 120px',
            padding: '12px 16px', gap: 14, alignItems: 'center',
            borderBottom: i < rows.length - 1 ? '1px solid var(--border-light)' : 'none',
            background: r.editing ? 'var(--brand-primary-10)' : 'transparent',
          }}>
            <div style={{ fontFamily: 'var(--font-mono)', fontSize: 12, fontWeight: 500, color: r.editing ? 'var(--brand-primary)' : 'var(--text-primary)' }}>{r.id}</div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
              <Avatar name={r.party} size={26} color={agentColor(r.party) + '22'} textColor={agentColor(r.party)}/>
              <span style={{ fontSize: 13, fontWeight: 500, color: 'var(--text-primary)' }}>{r.party}</span>
            </div>
            <div style={{ fontSize: 12, color: 'var(--text-secondary)' }}>{r.memo}</div>
            <div style={{ fontFamily: 'var(--font-mono)', fontSize: 12, color: 'var(--text-secondary)' }}>{r.date}</div>
            <div><Pill tone={r.tone} dot>{r.status}</Pill></div>
            <div style={{ fontFamily: 'var(--font-mono)', fontSize: 13, fontWeight: 600, textAlign: 'right', color: 'var(--text-primary)' }}>{r.amount}</div>
          </div>
        ))}
      </div>
    </div>
  );
}

// ─── The slide-out itself ──────────────────────────────────────────
function InvoiceEditPanel() {
  return (
    <div style={{
      position: 'absolute', top: 0, right: 0, bottom: 0, width: 540,
      background: 'var(--surface)', borderLeft: '1px solid var(--border-light)',
      boxShadow: '-12px 0 32px -8px rgb(0 0 0 / 0.10)',
      display: 'flex', flexDirection: 'column', zIndex: 2,
    }}>
      {/* Header — invoice ref + counterparty */}
      <div style={{
        padding: '16px 22px', borderBottom: '1px solid var(--border-light)',
        display: 'flex', alignItems: 'center', gap: 12,
      }}>
        <div style={{
          width: 36, height: 36, borderRadius: 9,
          background: 'var(--brand-primary-10)', color: 'var(--brand-primary)',
          display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
        }}><Icon name="invoice" size={17}/></div>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <span style={{ fontSize: 14.5, fontWeight: 600, color: 'var(--text-primary)' }}>Edit invoice</span>
            <span style={{ fontFamily: 'var(--font-mono)', fontSize: 12, fontWeight: 500, color: 'var(--brand-primary)' }}>INV-2041</span>
          </div>
          <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', marginTop: 1 }}>
            Primrose Logistics · Issued 19 Apr 2026 · Due 19 May 2026
          </div>
        </div>
        <Pill tone="pending" dot size="sm">Proposed</Pill>
        <span className="hover-halo"><Icon name="close" size={14}/></span>
      </div>

      {/* Tabs */}
      <div style={{
        padding: '0 22px', borderBottom: '1px solid var(--border-light)',
        display: 'flex', alignItems: 'center', gap: 4,
      }}>
        {[
          { l: 'Details',  active: true },
          { l: 'Line items' },
          { l: 'Payment' },
          { l: 'Activity', count: 4 },
        ].map((t, i) => (
          <div key={i} style={{
            padding: '12px 12px 11px', fontSize: 12.5,
            fontWeight: t.active ? 600 : 500,
            color: t.active ? 'var(--text-primary)' : 'var(--text-secondary)',
            borderBottom: t.active ? '2px solid var(--brand-primary)' : '2px solid transparent',
            display: 'inline-flex', alignItems: 'center', gap: 6, cursor: 'pointer',
          }}>
            {t.l}
            {t.count != null && (
              <span style={{
                fontSize: 10, fontFamily: 'var(--font-mono)', fontWeight: 600,
                padding: '1px 6px', borderRadius: 999,
                background: 'var(--surface-inset)', color: 'var(--text-tertiary)',
              }}>{t.count}</span>
            )}
          </div>
        ))}
      </div>

      {/* Body */}
      <div style={{ flex: 1, overflow: 'auto', padding: '20px 22px', display: 'flex', flexDirection: 'column', gap: 18 }}>

        {/* Agent reconciliation banner */}
        <div style={{
          display: 'flex', alignItems: 'flex-start', gap: 10, padding: '12px 14px',
          background: 'var(--brand-primary-10)', borderRadius: 10,
          border: '1px solid color-mix(in oklab, var(--brand-primary) 22%, transparent)',
        }}>
          <Icon name="sparkles" size={14} color="var(--brand-primary)" style={{ marginTop: 2, flex: 'none' }}/>
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{ fontSize: 12.5, color: 'var(--text-primary)', lineHeight: 1.5 }}>
              <b>Billing Agent</b> matched this invoice to{' '}
              <span style={{ fontFamily: 'var(--font-mono)' }}>bank_txn_9f2c1a</span> with{' '}
              <b>94% confidence</b>. Apply $12,480 and post journal <span style={{ fontFamily: 'var(--font-mono)' }}>JE-88421</span>?
            </div>
            <div style={{ display: 'flex', gap: 6, marginTop: 8 }}>
              <button className="btn btn-primary btn-sm" style={{ height: 26, padding: '0 10px', fontSize: 11 }}>Accept proposal</button>
              <button className="btn btn-ghost btn-sm" style={{ height: 26, padding: '0 10px', fontSize: 11 }}>Dismiss</button>
              <button className="btn btn-ghost btn-sm" style={{ height: 26, padding: '0 10px', fontSize: 11 }}>View txn</button>
            </div>
          </div>
        </div>

        {/* Counterparty + reference */}
        <div>
          <div style={{ fontSize: 10.5, letterSpacing: '0.08em', textTransform: 'uppercase', fontWeight: 600, color: 'var(--text-tertiary)', marginBottom: 10 }}>
            Counterparty & reference
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
            <SelectField label="Customer" required value="Primrose Logistics"/>
            <TextField label="Reference" mono value="PO-7741-A" placeholder="—"/>
            <TextField label="Issued" required mono value="2026-04-19" suffix={<Icon name="calendar" size={12}/>}/>
            <TextField label="Due" required mono value="2026-05-19" suffix={<Icon name="calendar" size={12}/>}/>
          </div>
        </div>

        {/* Line items — header + rows */}
        <div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 10 }}>
            <span style={{ fontSize: 10.5, letterSpacing: '0.08em', textTransform: 'uppercase', fontWeight: 600, color: 'var(--text-tertiary)' }}>
              Line items
            </span>
            <span style={{ flex: 1 }}/>
            <span style={{ fontSize: 11, color: 'var(--text-tertiary)' }}>3 items</span>
          </div>

          <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, overflow: 'hidden' }}>
            {/* column header */}
            <div style={{
              display: 'grid', gridTemplateColumns: '1fr 50px 90px 90px 24px',
              gap: 10, padding: '8px 12px',
              background: 'var(--surface-inset)',
              fontSize: 10, fontWeight: 600, letterSpacing: '0.05em', textTransform: 'uppercase',
              color: 'var(--text-tertiary)', borderBottom: '1px solid var(--border-light)',
            }}>
              <div>Description</div>
              <div style={{ textAlign: 'right' }}>Qty</div>
              <div style={{ textAlign: 'right' }}>Unit</div>
              <div style={{ textAlign: 'right' }}>Total</div>
              <div/>
            </div>

            <LineItem desc="Freight forwarding · LCL Lagos → Felixstowe" sku="SVC-FFW-LCL" qty="1" unit="$8,200.00" total="$8,200.00"/>
            <LineItem desc="Customs brokerage · UK import"           sku="SVC-CUS-UK"  qty="1" unit="$1,480.00" total="$1,480.00"/>
            <LineItem desc="Demurrage · 4 days @ $700"               sku="SVC-DEM-04"  qty="4" unit="$700.00"   total="$2,800.00" agentEdited/>

            {/* add line button */}
            <button style={{
              width: '100%', padding: '10px 12px',
              background: 'transparent', border: 'none',
              borderTop: '1px dashed var(--border)',
              fontSize: 12, color: 'var(--text-secondary)', cursor: 'pointer',
              display: 'inline-flex', alignItems: 'center', justifyContent: 'center', gap: 6,
            }}>
              <Icon name="plus" size={11}/> Add line item
            </button>
          </div>

          {/* totals */}
          <div style={{ marginTop: 12, display: 'flex', flexDirection: 'column', gap: 6, paddingLeft: 'auto' }}>
            <TotalRow label="Subtotal" value="$12,480.00"/>
            <TotalRow label="Tax · VAT 0% (export)" value="$0.00" muted/>
            <TotalRow label="FX adjustment · GBP→USD" value="-$0.00" muted/>
            <div style={{ borderTop: '1px solid var(--border-light)', paddingTop: 6, marginTop: 2 }}>
              <TotalRow label="Total" value="$12,480.00" bold/>
            </div>
          </div>
        </div>

        {/* Memo + ledger */}
        <div>
          <div style={{ fontSize: 10.5, letterSpacing: '0.08em', textTransform: 'uppercase', fontWeight: 600, color: 'var(--text-tertiary)', marginBottom: 10 }}>
            Memo & posting
          </div>
          <TextField label="Internal memo" value="Matched to bank_txn_9f2c1a · auto-recon by Billing Agent"/>

          <div style={{ marginTop: 12 }}>
            <FieldLabel>Ledger account</FieldLabel>
            <div style={{
              display: 'flex', alignItems: 'center', gap: 10, padding: '10px 12px',
              background: 'var(--surface-inset)', borderRadius: 8, border: '1px solid var(--border-light)',
            }}>
              <span style={{ fontFamily: 'var(--font-mono)', fontSize: 11, fontWeight: 600, color: 'var(--brand-primary)' }}>1200</span>
              <span style={{ fontSize: 12.5, color: 'var(--text-primary)', flex: 1 }}>Accounts receivable</span>
              <Icon name="chevdown" size={11} color="var(--text-tertiary)"/>
            </div>
          </div>

          <div style={{ marginTop: 14, borderTop: '1px solid var(--border-light)', paddingTop: 4 }}>
            <ToggleField label="Send receipt on payment" description="Email Primrose AP when reconciled" on/>
            <ToggleField label="Allow partial payments" description="Auto-split incoming bank txns" on/>
          </div>
        </div>
      </div>

      {/* Footer */}
      <div style={{
        padding: '12px 22px', borderTop: '1px solid var(--border-light)',
        background: 'var(--surface-inset)',
        display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 10,
      }}>
        <button className="btn btn-ghost btn-sm" style={{ color: 'var(--danger)' }}>
          <Icon name="trash" size={12}/> Void invoice
        </button>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <span style={{ fontSize: 10.5, color: 'var(--text-tertiary)', fontFamily: 'var(--font-mono)' }}>Saved 14s ago</span>
          <button className="btn btn-outline btn-sm">Cancel</button>
          <button className="btn btn-primary btn-sm">Save changes</button>
        </div>
      </div>
    </div>
  );
}

// ─── Local helpers ─────────────────────────────────────────────────
function LineItem({ desc, sku, qty, unit, total, agentEdited }) {
  return (
    <div style={{
      display: 'grid', gridTemplateColumns: '1fr 50px 90px 90px 24px',
      gap: 10, padding: '10px 12px', alignItems: 'center',
      borderBottom: '1px solid var(--border-light)',
      background: agentEdited ? 'var(--brand-primary-10)' : 'transparent',
    }}>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 2, minWidth: 0 }}>
        <span style={{ fontSize: 12.5, color: 'var(--text-primary)', fontWeight: 500, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
          {desc}
        </span>
        <span style={{ fontFamily: 'var(--font-mono)', fontSize: 10.5, color: 'var(--text-tertiary)', display: 'inline-flex', alignItems: 'center', gap: 6 }}>
          {sku}
          {agentEdited && (
            <span style={{ display: 'inline-flex', alignItems: 'center', gap: 3, color: 'var(--brand-primary)', fontFamily: 'var(--font-base)' }}>
              <Icon name="sparkles" size={9}/> agent-adjusted
            </span>
          )}
        </span>
      </div>
      <div style={{ fontFamily: 'var(--font-mono)', fontSize: 12, textAlign: 'right', color: 'var(--text-secondary)' }}>{qty}</div>
      <div style={{ fontFamily: 'var(--font-mono)', fontSize: 12, textAlign: 'right', color: 'var(--text-secondary)' }}>{unit}</div>
      <div style={{ fontFamily: 'var(--font-mono)', fontSize: 12.5, textAlign: 'right', color: 'var(--text-primary)', fontWeight: 600 }}>{total}</div>
      <span className="hover-halo" style={{ display: 'inline-flex', justifyContent: 'center' }}>
        <Icon name="more" size={12} color="var(--text-tertiary)"/>
      </span>
    </div>
  );
}

function TotalRow({ label, value, bold, muted }) {
  return (
    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
      <span style={{
        fontSize: bold ? 13 : 12,
        fontWeight: bold ? 600 : 400,
        color: muted ? 'var(--text-tertiary)' : 'var(--text-secondary)',
      }}>{label}</span>
      <span style={{
        fontFamily: 'var(--font-mono)',
        fontSize: bold ? 15 : 12.5,
        fontWeight: bold ? 700 : 500,
        color: muted ? 'var(--text-tertiary)' : 'var(--text-primary)',
      }}>{value}</span>
    </div>
  );
}

Object.assign(window, { ScreenInvoiceEdit });
