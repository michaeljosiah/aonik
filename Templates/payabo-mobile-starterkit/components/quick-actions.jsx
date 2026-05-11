// QuickActionsSheet — the "+" FAB opens this. Mirrors PayaboPrimaryAppShell's
// _showQuickActions sheet in the Flutter app: Pay a bill / Transfer.

function QuickActionsSheet({ onClose, onBill, onTransfer }) {
  return (
    <div style={{ position: 'absolute', inset: 0, zIndex: 70, animation: 'payFade 180ms ease-out' }}>
      {/* scrim */}
      <div onClick={onClose} style={{
        position: 'absolute', inset: 0, background: 'rgba(15, 13, 14, 0.55)',
        backdropFilter: 'blur(2px)',
      }}/>
      {/* sheet */}
      <div style={{
        position: 'absolute', left: 0, right: 0, bottom: 0,
        background: '#FFFCF9', borderRadius: '24px 24px 0 0',
        padding: '14px 16px 28px',
        boxShadow: '0 -12px 32px rgba(0,0,0,0.18)',
        animation: 'paySlideUp 280ms cubic-bezier(.2,.8,.2,1)',
      }}>
        <div style={{ width: 44, height: 4, borderRadius: 4, background: '#DCCDB7', margin: '0 auto 12px' }}/>
        <div style={{ display: 'flex', alignItems: 'baseline', justifyContent: 'space-between', padding: '4px 4px 12px' }}>
          <div style={{ font: `700 18px/22px ${PAY.font}`, color: PAY.warm900, letterSpacing: -0.2 }}>Quick actions</div>
          <div onClick={onClose} style={{
            font: `700 11px/14px ${PAY.font}`, color: PAY.warm800, letterSpacing: 0.6,
            textTransform: 'uppercase', cursor: 'pointer',
          }}>Close</div>
        </div>

        <QuickActionRow
          icon="receipt" iconBg="#FFEFE3" iconFg="#7A3211"
          title="Pay a bill"
          subtitle="Start a bill payment now"
          onClick={onBill}/>

        <QuickActionRow
          icon="transfer" iconBg="#E8F0FF" iconFg="#1E4AB5"
          title="Transfer"
          subtitle="Send money to another account"
          onClick={onTransfer}/>
      </div>
    </div>
  );
}

function QuickActionRow({ icon, iconBg, iconFg, title, subtitle, onClick }) {
  const [pressed, setPressed] = React.useState(false);
  return (
    <div
      onClick={onClick}
      onMouseDown={() => setPressed(true)}
      onMouseUp={() => setPressed(false)}
      onMouseLeave={() => setPressed(false)}
      style={{
        display: 'flex', alignItems: 'center', gap: 14, padding: '14px 12px',
        marginBottom: 8, borderRadius: 12,
        background: pressed ? '#FFF6EA' : 'transparent',
        cursor: 'pointer', transition: 'background 120ms',
      }}>
      <div style={{
        width: 44, height: 44, borderRadius: 12, flex: 'none',
        background: iconBg, color: iconFg,
        display: 'flex', alignItems: 'center', justifyContent: 'center',
      }}><Icon name={icon} size={20}/></div>
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{ font: `700 14px/18px ${PAY.font}`, color: PAY.ink }}>{title}</div>
        <div style={{ font: `400 12px/16px ${PAY.font}`, color: PAY.warm800, marginTop: 2 }}>{subtitle}</div>
      </div>
      <div style={{ color: PAY.warm800 }}><Icon name="chev" size={18}/></div>
    </div>
  );
}

Object.assign(window, { QuickActionsSheet });
