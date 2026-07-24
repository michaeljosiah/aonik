// Commerce · Storefront — Spec 069: Delivery (fulfilment calendar + promise)
//   GET /commerce/admin/fulfilment-calendar   · calendar + CurrentPromise echo
//   PUT /commerce/admin/fulfilment-calendar   · full replace; response echoes the
//                                               recomputed promise (A5) instantly
//   GET /commerce/config/delivery             · the public read — 404 when
//                                               unconfigured: NO date, never a guess
// Weekly cycle: orders after Tuesday 18:00 fall to the following week's close.
// Comparisons happen on UTC instants; a clock rolling back never reopens the book.

function ScreenStorefrontDelivery() {
  const cal = CS_CALENDAR;
  const [days, setDays] = React.useState(cal.deliveryDays);
  const weekdays = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];
  const toggle = d => setDays(x => x.includes(d) ? x.filter(y => y !== d) : [...x, d]);

  // August 2026 grid: firstDow 6 = Saturday under a Mon-first header (offset 5).
  const cells = [];
  for (let i = 0; i < 5; i++) cells.push(null);
  for (let d = 1; d <= cal.days; d++) cells.push(d);
  const dowOf = d => weekdays[(5 + d - 1) % 7];
  const isDelivery = d => d && days.includes(dowOf(d));
  const isBlackout = d => d && cal.blackoutDates.includes('2026-08-' + String(d).padStart(2, '0'));
  const isPromise = d => d === 6;

  const kpis = [
    { l: 'Next delivery', v: 'Thu 6 Aug', s: 'the live promise, ' + cal.timezone },
    { l: 'Order cutoff', v: cal.cutoffDayOfWeek.slice(0, 3) + ' ' + cal.cutoffLocal, s: 'weekly cycle — after it, next week' },
    { l: 'Lead days', v: cal.leadDays, s: 'preparation time before dispatch' },
    { l: 'Blackouts', v: cal.blackoutDates.length, s: 'Thu 27 Aug — no fulfilment', warn: true },
  ];

  return (
    <div style={{ height: '100%', overflow: 'auto', padding: '22px 28px', display: 'flex', flexDirection: 'column', gap: 18 }}>
      <div>
        <div style={{ fontSize: 22, fontWeight: 700, color: 'var(--text-primary)', letterSpacing: '-0.01em' }}>Delivery</div>
        <div style={{ fontSize: 13, color: 'var(--text-secondary)', marginTop: 2 }}>The fulfilment calendar and the single most-repeated promise on the storefront — the earliest delivery date. Every save re-echoes the recomputed promise so cause and effect share one screen.</div>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 12 }}>
        {kpis.map(k => (
          <div key={k.l} style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, padding: '14px 16px' }}>
            <div style={{ fontSize: 11, color: 'var(--text-tertiary)', textTransform: 'uppercase', letterSpacing: '0.05em', fontWeight: 600 }}>{k.l}</div>
            <div style={{ fontSize: 22, fontWeight: 700, color: k.warn ? 'var(--warning)' : 'var(--text-primary)', marginTop: 4, fontFamily: 'var(--font-mono)' }}>{k.v}</div>
            <div style={{ fontSize: 11.5, color: 'var(--text-secondary)', marginTop: 2 }}>{k.s}</div>
          </div>
        ))}
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '380px 1fr', gap: 16, alignItems: 'start' }}>
        {/* Calendar editor — mirrors UpsertFulfilmentCalendarCommand exactly */}
        <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, padding: '15px 17px', display: 'flex', flexDirection: 'column', gap: 13 }}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <span style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--text-primary)' }}>Fulfilment calendar</span>
            <Pill tone={cal.active ? 'success' : 'muted'} dot size="sm">{cal.active ? 'Active' : 'Parked'}</Pill>
          </div>

          <div>
            <div style={{ fontSize: 10.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.05em', textTransform: 'uppercase', marginBottom: 7 }}>Delivery days</div>
            <div style={{ display: 'flex', gap: 5 }}>
              {weekdays.map(d => {
                const on = days.includes(d);
                return <button key={d} onClick={() => toggle(d)} style={{ flex: 1, height: 34, borderRadius: 8, cursor: 'pointer', fontSize: 11.5, fontWeight: on ? 700 : 500, border: '1px solid ' + (on ? 'var(--brand-primary)' : 'var(--border-light)'), background: on ? 'var(--brand-primary)' : 'var(--surface)', color: on ? '#fff' : 'var(--text-secondary)' }}>{d.slice(0, 2)}</button>;
              })}
            </div>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
            <div>
              <div style={{ fontSize: 10.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.05em', textTransform: 'uppercase', marginBottom: 6 }}>Cutoff (local)</div>
              <div style={{ fontFamily: 'var(--font-mono)', fontSize: 13, fontWeight: 600, color: 'var(--text-primary)', background: 'var(--surface-inset)', border: '1px solid var(--border-light)', borderRadius: 8, padding: '8px 11px' }}>{cal.cutoffLocal}</div>
            </div>
            <div>
              <div style={{ fontSize: 10.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.05em', textTransform: 'uppercase', marginBottom: 6 }}>Cycle day</div>
              <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)', background: 'var(--surface-inset)', border: '1px solid var(--border-light)', borderRadius: 8, padding: '8px 11px' }}>{cal.cutoffDayOfWeek}</div>
            </div>
            <div>
              <div style={{ fontSize: 10.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.05em', textTransform: 'uppercase', marginBottom: 6 }}>Lead days</div>
              <div style={{ fontFamily: 'var(--font-mono)', fontSize: 13, fontWeight: 600, color: 'var(--text-primary)', background: 'var(--surface-inset)', border: '1px solid var(--border-light)', borderRadius: 8, padding: '8px 11px' }}>{cal.leadDays}</div>
            </div>
            <div>
              <div style={{ fontSize: 10.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.05em', textTransform: 'uppercase', marginBottom: 6 }}>Timezone</div>
              <div style={{ fontFamily: 'var(--font-mono)', fontSize: 12, fontWeight: 600, color: 'var(--text-primary)', background: 'var(--surface-inset)', border: '1px solid var(--border-light)', borderRadius: 8, padding: '9px 11px' }}>{cal.timezone}</div>
            </div>
          </div>

          <div>
            <div style={{ fontSize: 10.5, fontWeight: 600, color: 'var(--text-tertiary)', letterSpacing: '0.05em', textTransform: 'uppercase', marginBottom: 7 }}>Blackout dates</div>
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
              {cal.blackoutDates.map(b => (
                <span key={b} style={{ display: 'inline-flex', alignItems: 'center', gap: 6, fontSize: 11.5, fontFamily: 'var(--font-mono)', color: 'var(--danger)', background: 'var(--danger-light)', borderRadius: 999, padding: '4px 11px' }}>{b}<Icon name="close" size={10} color="var(--danger)" /></span>
              ))}
              <button className="btn btn-ghost btn-sm"><Icon name="plus" size={11} /> Add date</button>
            </div>
            <div style={{ fontSize: 10.5, color: 'var(--text-tertiary)', marginTop: 6 }}>Expired dates prune on save; capped at 100 future dates.</div>
          </div>

          <div style={{ borderTop: '1px solid var(--border-light)', paddingTop: 12, display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <span style={{ fontSize: 11, color: 'var(--text-tertiary)' }}>Saving echoes the recomputed promise below.</span>
            <button className="btn btn-primary btn-sm"><Icon name="check" size={12} /> Save calendar</button>
          </div>
        </div>

        {/* Month grid + promise echo */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
          <div style={{ background: 'var(--brand-primary-10)', border: '1px solid var(--brand-primary)', borderRadius: 10, padding: '14px 18px', display: 'flex', alignItems: 'center', gap: 14 }}>
            <span style={{ width: 42, height: 42, borderRadius: 999, background: 'var(--brand-primary)', display: 'grid', placeItems: 'center', flex: 'none' }}><Icon name="calendar" size={19} color="#fff" /></span>
            <div style={{ flex: 1 }}>
              <div style={{ fontSize: 10.5, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.06em', color: 'var(--brand-primary)' }}>The storefront promises</div>
              <div style={{ fontSize: 19, fontWeight: 700, color: 'var(--text-primary)', marginTop: 1 }}>Earliest delivery {cal.promise.label}</div>
            </div>
            <div style={{ textAlign: 'right', fontSize: 11, color: 'var(--text-secondary)', lineHeight: 1.5 }}>
              Ordered before <span style={{ fontFamily: 'var(--font-mono)' }}>Tue 18:00</span> makes this week's close.<br />It is now <span style={{ fontFamily: 'var(--font-mono)' }}>Tue 17:20</span> — inside the window.
            </div>
          </div>

          <div style={{ background: 'var(--surface)', border: '1px solid var(--border-light)', borderRadius: 10, padding: '14px 16px' }}>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 10 }}>
              <span style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--text-primary)' }}>{cal.monthLabel}</span>
              <div style={{ display: 'flex', gap: 12, fontSize: 10.5, color: 'var(--text-tertiary)' }}>
                <span style={{ display: 'inline-flex', alignItems: 'center', gap: 5 }}><span style={{ width: 9, height: 9, borderRadius: 3, background: 'var(--brand-primary-20)' }} /> delivery day</span>
                <span style={{ display: 'inline-flex', alignItems: 'center', gap: 5 }}><span style={{ width: 9, height: 9, borderRadius: 3, background: 'var(--danger-light)' }} /> blackout</span>
                <span style={{ display: 'inline-flex', alignItems: 'center', gap: 5 }}><span style={{ width: 9, height: 9, borderRadius: 99, border: '2px solid var(--brand-primary)' }} /> the promise</span>
              </div>
            </div>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(7, 1fr)', gap: 4 }}>
              {['Mo', 'Tu', 'We', 'Th', 'Fr', 'Sa', 'Su'].map(h => (
                <div key={h} style={{ fontSize: 9.5, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.06em', color: 'var(--text-tertiary)', textAlign: 'center', padding: '2px 0' }}>{h}</div>
              ))}
              {cells.map((d, i) => {
                if (d == null) return <div key={'e' + i} />;
                const del = isDelivery(d), blk = isBlackout(d), prm = isPromise(d);
                return (
                  <div key={d} style={{
                    height: 44, borderRadius: 8, display: 'grid', placeItems: 'center', position: 'relative',
                    background: blk ? 'var(--danger-light)' : del ? 'var(--brand-primary-10)' : 'var(--surface-inset)',
                    border: prm ? '2px solid var(--brand-primary)' : '1px solid ' + (del && !blk ? 'var(--brand-primary-20)' : 'var(--border-light)'),
                  }}>
                    <span style={{ fontFamily: 'var(--font-mono)', fontSize: 12.5, fontWeight: prm ? 700 : del ? 600 : 500, color: blk ? 'var(--danger)' : del ? 'var(--brand-primary)' : 'var(--text-tertiary)', textDecoration: blk ? 'line-through' : 'none' }}>{d}</span>
                    {blk && <span style={{ position: 'absolute', bottom: 3, fontSize: 8, fontWeight: 700, letterSpacing: '0.05em', color: 'var(--danger)' }}>CLOSED</span>}
                  </div>
                );
              })}
            </div>
            <div style={{ fontSize: 11, color: 'var(--text-secondary)', marginTop: 10 }}>Thu 27 is blacked out, so that week's orders land Thu 3 September — the promise walks forward, it never guesses.</div>
          </div>

          <div style={{ background: 'var(--surface)', border: '1px dashed var(--border-medium)', borderRadius: 10, padding: '11px 14px', fontSize: 11.5, color: 'var(--text-secondary)', display: 'flex', gap: 8 }}>
            <Icon name="eye" size={13} color="var(--text-tertiary)" />
            <span><b style={{ color: 'var(--text-primary)' }}>The honest empty state:</b> parked calendar, no delivery days, or an unresolvable timezone all mean the public read returns 404 — the storefront shows <i>no date anywhere</i> rather than a wrong one.</span>
          </div>
        </div>
      </div>
    </div>
  );
}

Object.assign(window, { ScreenStorefrontDelivery });
