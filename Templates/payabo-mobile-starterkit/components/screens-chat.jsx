// Chat with Simi + Voice mode + Transaction detail + Send-money flow + Onboarding.

// ─── CHAT (full) ──────────────────────────────────────────────────────────
function ChatScreen({ tweaks, onVoice, onBack }) {
  const isFresh = tweaks && tweaks.dataMode === 'fresh';
  const [historyOpen, setHistoryOpen] = React.useState(false);
  const [messages, setMessages] = React.useState(isFresh ? [
    { who: 'simi', t: "Hi Kwame, I'm Simi. Once you link an account I can watch your bills, flag unusual spending, and help you send money home.", quick: ['Link an account', 'How does this work?'] },
  ] : [
    { who: 'simi', t: "Good morning, Kwame. Here's what I'm watching for you today.", quick: ['Show bills', 'Net worth', 'Send to Mum'] },
    { who: 'simi', t: 'Your Sky Broadband bill of £42.99 is due Thursday. Want me to schedule it from your main account?' },
    { who: 'you', t: 'Yes please — from my main account.' },
    { who: 'simi', t: "Done. I'll pay Sky on Thursday from your GBP account. I'll also nudge you if your balance drops below £300.", card: 'bill-scheduled' },
  ]);
  const [input, setInput] = React.useState('');
  const [typing, setTyping] = React.useState(false);
  const scrollRef = React.useRef(null);

  React.useEffect(() => {
    if (scrollRef.current) scrollRef.current.scrollTop = scrollRef.current.scrollHeight;
  }, [messages, typing]);

  function send(text) {
    if (!text.trim()) return;
    setMessages(m => [...m, { who: 'you', t: text }]);
    setInput('');
    setTyping(true);
    setTimeout(() => {
      setTyping(false);
      const reply = /mum|ama|send/i.test(text)
        ? { who: 'simi', t: "Sending £100 to Ama in Accra. At today's rate that's GHS 1,942 — should arrive in 90 seconds.", card: 'send-confirm' }
        : /net|worth|balance/i.test(text)
        ? { who: 'simi', t: "You're at £18,642.90 across four accounts — up £412.80 this month. Your savings are doing most of the work." }
        : /bill|sky|vodafone/i.test(text)
        ? { who: 'simi', t: "Three bills queued for May: Sky (£42.99 Thu), Vodafone (£28 Sat), EE (£21 Dec 2). Want me to schedule them all?" }
        : { who: 'simi', t: "Got it. Anything else you'd like me to keep an eye on?" };
      setMessages(m => [...m, reply]);
    }, 1100);
  }

  return (
    <div style={{ background: payChatHero, color: 'white', height: '100%', display: 'flex', flexDirection: 'column', position: 'relative', overflow: 'hidden' }}>
      <GlowOrb size={280} top={-80} left={-100} opacity={0.35}/>
      <GlowOrb size={220} top={140} right={-80} color="#D7A14E" opacity={0.2} blur={70}/>

      <div style={{ padding: '12px 16px 8px', display: 'flex', alignItems: 'center', gap: 12, position: 'relative', zIndex: 2 }}>
        <div onClick={() => setHistoryOpen(true)} style={{ cursor: 'pointer', width: 38, height: 38, borderRadius: 50, background: 'rgba(255,255,255,0.06)', border: '1px solid rgba(255,255,255,0.1)', display: 'flex', alignItems: 'center', justifyContent: 'center', color: 'rgba(255,255,255,0.85)' }}>
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M3 6h18M3 12h18M3 18h12"/></svg>
        </div>
        {onBack && <div onClick={onBack} style={{ cursor: 'pointer', color: 'rgba(255,255,255,0.55)' }}><Icon name="back" size={18}/></div>}
        <div style={{ position: 'relative' }}>
          <div style={{
            width: 44, height: 44, borderRadius: 50,
            backgroundImage: "url('assets/simi.png')",
            backgroundSize: 'cover', backgroundPosition: '50% 22%',
            border: '2px solid rgba(243,121,32,0.6)',
          }}/>
          <div style={{
            position: 'absolute', bottom: -2, right: -2, width: 12, height: 12, borderRadius: 50,
            background: PAY.success, border: `2px solid ${PAY.chatTop}`,
          }}/>
        </div>
        <div style={{ flex: 1 }}>
          <div style={{ font: `700 16px/20px ${PAY.font}`, color: 'white' }}>Simi</div>
          <div style={{ font: `400 11px/14px ${PAY.font}`, color: 'rgba(255,255,255,0.6)' }}>AI companion · always listening</div>
        </div>
        <div onClick={onVoice} style={{
          width: 38, height: 38, borderRadius: 50, background: 'rgba(255,255,255,0.08)',
          border: '1px solid rgba(255,255,255,0.12)', display: 'flex', alignItems: 'center', justifyContent: 'center',
          color: 'white', cursor: 'pointer',
        }}><Icon name="waveform" size={18}/></div>
      </div>

      <ChatHistoryOverlay open={historyOpen} onClose={() => setHistoryOpen(false)} isFresh={isFresh}/>

      <div ref={scrollRef} style={{ flex: 1, padding: '8px 16px 10px', overflow: 'auto', position: 'relative', zIndex: 2 }}>
        <div style={{ textAlign: 'center', font: `500 10px/14px ${PAY.font}`, color: 'rgba(255,255,255,0.4)', letterSpacing: 1, textTransform: 'uppercase', padding: '8px 0 16px' }}>
          Today, 9:30 am
        </div>
        {messages.map((m, i) => (
          <MsgBubble key={i} who={m.who} t={m.t} card={m.card} quick={m.quick} onQuick={send} animate={i === messages.length - 1}/>
        ))}
        {typing && <TypingDots/>}
      </div>

      <div style={{ padding: '8px 12px 14px', display: 'flex', gap: 8, alignItems: 'center', position: 'relative', zIndex: 2 }}>
        <div style={{ display: 'flex', flex: 1, alignItems: 'center', padding: '6px 6px 6px 16px', borderRadius: 50, background: 'rgba(255,255,255,0.08)', border: '1px solid rgba(255,255,255,0.12)' }}>
          <input
            value={input}
            onChange={e => setInput(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && send(input)}
            placeholder="Ask Simi anything…"
            style={{
              flex: 1, border: 0, outline: 'none', background: 'transparent',
              color: 'white', font: `400 13px/18px ${PAY.font}`,
            }}
          />
          <div onClick={() => send(input)} style={{
            width: 34, height: 34, borderRadius: 50, background: PAY.orange,
            display: 'flex', alignItems: 'center', justifyContent: 'center', cursor: 'pointer',
            opacity: input.trim() ? 1 : 0.5,
          }}><Icon name="send" size={16} color="white"/></div>
        </div>
        <div onClick={onVoice} style={{
          width: 44, height: 44, borderRadius: 50,
          background: 'radial-gradient(circle at 35% 30%, #FFD3A4 0%, #F37920 60%, #C95F0B 100%)',
          boxShadow: '0 4px 16px rgba(243,121,32,0.4)',
          display: 'flex', alignItems: 'center', justifyContent: 'center', cursor: 'pointer',
        }}><Icon name="mic" size={18} color="white"/></div>
      </div>
    </div>
  );
}

function MsgBubble({ who, t, card, quick, onQuick, animate }) {
  const isYou = who === 'you';
  return (
    <div style={{ display: 'flex', justifyContent: isYou ? 'flex-end' : 'flex-start', marginBottom: 8, animation: animate ? 'payRise 220ms ease-out' : 'none' }}>
      <div style={{ maxWidth: '82%' }}>
        <div style={{
          padding: '10px 14px',
          borderRadius: isYou ? '18px 18px 4px 18px' : '4px 18px 18px 18px',
          background: isYou ? PAY.orange : 'rgba(255,255,255,0.07)',
          border: isYou ? 'none' : '1px solid rgba(255,255,255,0.08)',
          backdropFilter: 'blur(8px)',
          font: `400 13px/19px ${PAY.font}`, color: 'white',
        }}>
          {animate && !isYou ? <Typewriter text={t} speed={14} cursor={false}/> : t}
        </div>
        {card === 'bill-scheduled' && <SchedCard/>}
        {card === 'send-confirm' && <SendConfirmCard/>}
        {quick && (
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6, marginTop: 8 }}>
            {quick.map(q => (
              <div key={q} onClick={() => onQuick(q)} style={{
                padding: '6px 12px', borderRadius: 50,
                background: 'rgba(255,255,255,0.06)', border: '1px solid rgba(255,255,255,0.12)',
                color: 'white', font: `600 11px/14px ${PAY.font}`, cursor: 'pointer',
              }}>{q}</div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}

function SchedCard() {
  return (
    <div style={{
      marginTop: 8, padding: 14, borderRadius: 14,
      background: 'rgba(255,255,255,0.05)', border: '1px solid rgba(255,255,255,0.1)',
    }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
        <div style={{ width: 32, height: 32, borderRadius: 8, background: '#0B1A4A', color: 'white', display: 'flex', alignItems: 'center', justifyContent: 'center', font: `700 9px/12px ${PAY.font}` }}>SKY</div>
        <div style={{ flex: 1 }}>
          <div style={{ font: `700 13px/16px ${PAY.font}`, color: 'white' }}>Sky Broadband</div>
          <div style={{ font: `400 11px/14px ${PAY.font}`, color: 'rgba(255,255,255,0.6)' }}>Thursday, May 14 · 9:00 AM</div>
        </div>
        <div style={{ font: `700 14px/18px ${PAY.font}`, color: PAY.orangeSoft }}>£42.99</div>
      </div>
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginTop: 10, padding: '6px 10px', borderRadius: 8, background: 'rgba(74,203,100,0.08)', border: '1px solid rgba(74,203,100,0.2)' }}>
        <Icon name="check" size={12} color="#7CE0A0"/>
        <div style={{ font: `600 11px/14px ${PAY.font}`, color: '#7CE0A0' }}>Scheduled — Simi will pay from Monzo •• 4521</div>
      </div>
    </div>
  );
}

function SendConfirmCard() {
  return (
    <div style={{
      marginTop: 8, padding: 14, borderRadius: 14,
      background: 'linear-gradient(135deg, rgba(243,121,32,0.12) 0%, rgba(243,168,92,0.04) 100%)',
      border: '1px solid rgba(243,121,32,0.25)',
    }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
        <div style={{ width: 32, height: 32, borderRadius: 50, background: '#FFEFE3', color: '#7A3211', display: 'flex', alignItems: 'center', justifyContent: 'center', font: `700 11px/14px ${PAY.font}` }}>AS</div>
        <div style={{ flex: 1 }}>
          <div style={{ font: `700 13px/16px ${PAY.font}`, color: 'white' }}>Ama Serwaa · MTN MoMo</div>
          <div style={{ font: `400 11px/14px ${PAY.font}`, color: 'rgba(255,255,255,0.6)' }}>£100 → GHS 1,942</div>
        </div>
        <div style={{ width: 22, height: 22, borderRadius: 50, backgroundImage: "url('assets/flags/gh.svg')", backgroundSize: 'cover' }}/>
      </div>
      <div style={{ display: 'flex', gap: 8, marginTop: 12 }}>
        <PayButton variant="primary" size="sm" full>Confirm send</PayButton>
        <PayButton variant="dark" size="sm">Cancel</PayButton>
      </div>
    </div>
  );
}

function TypingDots() {
  return (
    <div style={{ display: 'flex', justifyContent: 'flex-start', marginBottom: 8 }}>
      <div style={{
        padding: '12px 14px', borderRadius: '4px 18px 18px 18px',
        background: 'rgba(255,255,255,0.06)', border: '1px solid rgba(255,255,255,0.08)',
        display: 'flex', gap: 4,
      }}>
        {[0, 1, 2].map(i => (
          <span key={i} style={{
            width: 6, height: 6, borderRadius: 50, background: PAY.orangeSoft,
            animation: `payTyping 1.2s ${i * 0.15}s infinite ease-in-out`,
          }}/>
        ))}
      </div>
    </div>
  );
}

// ─── Voice mode (full-screen orb) ─────────────────────────────────────────
function VoiceScreen({ onBack }) {
  // cycles user-listening → connecting → bot-speaking, then loops
  const [phase, setPhase] = React.useState('listening');
  const [transcript, setTranscript] = React.useState("Send a hundred pounds to my mum");
  React.useEffect(() => {
    const seq = [
      ['listening', 2400],
      ['thinking', 1200],
      ['speaking', 4200],
    ];
    let idx = 0, timer;
    const next = () => {
      const [p, ms] = seq[idx % seq.length];
      setPhase(p);
      idx++;
      timer = setTimeout(next, ms);
    };
    next();
    return () => clearTimeout(timer);
  }, []);
  const label = phase === 'listening' ? 'Simi listening' : phase === 'thinking' ? 'Thinking' : 'Simi speaking';
  const speaker = phase === 'speaking' ? 'bot' : phase === 'listening' ? 'user' : 'none';
  return (
    <div style={{ background: payChatHero, height: '100%', color: 'white', position: 'relative', overflow: 'hidden', display: 'flex', flexDirection: 'column' }}>
      <GlowOrb size={400} top={'30%'} left={'10%'} opacity={0.4} blur={80}/>
      <div style={{ padding: '14px 16px', display: 'flex', alignItems: 'center', gap: 12, position: 'relative', zIndex: 2 }}>
        <div onClick={onBack} style={{ cursor: 'pointer', width: 38, height: 38, borderRadius: 50, background: 'rgba(255,255,255,0.06)', border: '1px solid rgba(255,255,255,0.1)', display: 'flex', alignItems: 'center', justifyContent: 'center' }}><Icon name="close" size={18}/></div>
        <div style={{ flex: 1 }}/>
        <div style={{ font: `700 10px/14px ${PAY.font}`, letterSpacing: 1.4, textTransform: 'uppercase', color: PAY.orangeSoft, display: 'flex', alignItems: 'center', gap: 6 }}>
          <PulseDot color={PAY.orangeSoft} size={6}/>{label}
        </div>
      </div>

      <div style={{ flex: 1, display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', padding: '0 24px', position: 'relative', zIndex: 2 }}>
        <VoiceOrb size={240} speaker={speaker} phase={phase}/>
        <div style={{ font: `700 26px/30px ${PAY.font}`, marginTop: 32, letterSpacing: -0.4, textAlign: 'center' }}>
          {phase === 'speaking'
            ? <Typewriter text='Sending £100 to Ama in Accra.' speed={28} cursor={false}/>
            : transcript}
        </div>
        {phase === 'speaking' && (
          <div style={{ font: `400 14px/20px ${PAY.font}`, color: 'rgba(255,255,255,0.65)', marginTop: 10, textAlign: 'center' }}>
            That's GHS 1,942 at today's rate.
          </div>
        )}
      </div>

      <div style={{ padding: '20px 16px 18px', display: 'flex', justifyContent: 'center', gap: 14, position: 'relative', zIndex: 2 }}>
        <div style={{ width: 56, height: 56, borderRadius: 50, background: 'rgba(255,255,255,0.06)', border: '1px solid rgba(255,255,255,0.12)', display: 'flex', alignItems: 'center', justifyContent: 'center', cursor: 'pointer' }}>
          <Icon name="pause" size={20}/>
        </div>
        <div onClick={onBack} style={{ width: 64, height: 64, borderRadius: 50, background: PAY.orange, display: 'flex', alignItems: 'center', justifyContent: 'center', cursor: 'pointer', boxShadow: '0 4px 16px rgba(243,121,32,0.5)' }}>
          <Icon name="close" size={24} color="white" strokeWidth={2.5}/>
        </div>
        <div style={{ width: 56, height: 56, borderRadius: 50, background: 'rgba(255,255,255,0.06)', border: '1px solid rgba(255,255,255,0.12)', display: 'flex', alignItems: 'center', justifyContent: 'center', cursor: 'pointer' }}>
          <Icon name="chat" size={20}/>
        </div>
      </div>
    </div>
  );
}

// ─── Transaction detail (mirrors transaction_detail_screen.dart) ─────────
function TxnDetailScreen({ onBack }) {
  const [excluded, setExcluded] = React.useState(false);
  const cardBg = '#FFFFFF';
  const cardBorder = '1px solid #F1E5D1';
  const accentBrown = '#4D3120';
  const muted = '#9C8674';
  const successSoft = 'rgba(74,160,76,0.14)';
  const success = '#1F8A3A';
  const warmAccent = '#FBEBD2';

  return (
    <div style={{ background: payWarmScreen, height: '100%', display: 'flex', flexDirection: 'column', overflow: 'auto' }}>
      {/* ── Back button only ─────────────────────── */}
      <div style={{ padding: '14px 20px 0' }}>
        <div onClick={onBack} style={{ cursor: 'pointer', width: 28, height: 28, display: 'flex', alignItems: 'center', color: accentBrown }}>
          <Icon name="back" size={22}/>
        </div>
      </div>

      <div style={{ padding: '12px 24px 32px' }}>
        {/* ── Transaction header: icon + merchant/date + amount ── */}
        <div style={{ display: 'flex', alignItems: 'flex-start', gap: 16, marginTop: 4 }}>
          <div style={{ width: 56, height: 56, borderRadius: '50%', background: '#FFEFE3', display: 'flex', alignItems: 'center', justifyContent: 'center', font: `700 22px/28px ${PAY.font}`, color: '#7A3211', flexShrink: 0 }}>A</div>
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{ font: `700 22px/26px ${PAY.font}`, color: accentBrown, letterSpacing: -0.3 }}>Ama Serwaa</div>
            <div style={{ font: `400 13px/18px ${PAY.font}`, color: muted, marginTop: 2 }}>Monday, 11 May</div>
          </div>
          <div style={{ display: 'flex', alignItems: 'baseline', color: accentBrown }}>
            <span style={{ font: `700 18px/22px ${PAY.font}` }}>£</span>
            <span style={{ font: `800 30px/30px ${PAY.font}`, letterSpacing: -0.6 }}>120</span>
            <span style={{ font: `600 16px/20px ${PAY.font}`, opacity: 0.6 }}>.00</span>
          </div>
        </div>

        {/* ── Status card ──────────────────────────── */}
        <div style={{ marginTop: 24, background: cardBg, border: cardBorder, borderRadius: 20, padding: 20 }}>
          <div style={{ font: `700 15px/20px ${PAY.font}`, color: accentBrown }}>Status</div>
          <div style={{ font: `400 13px/19px ${PAY.font}`, color: muted, marginTop: 6 }}>
            This transaction is now complete and cannot be reversed
          </div>
          <div style={{ display: 'inline-flex', marginTop: 14, padding: '6px 12px', borderRadius: 999, background: successSoft }}>
            <span style={{ font: `700 12px/14px ${PAY.font}`, color: success, letterSpacing: 0.2 }}>Completed</span>
          </div>
        </div>

        {/* ── Exclude from budget ──────────────────── */}
        <div style={{ marginTop: 14, background: cardBg, border: cardBorder, borderRadius: 20, padding: 20, display: 'flex', alignItems: 'center', gap: 14 }}>
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{ font: `700 15px/20px ${PAY.font}`, color: accentBrown }}>Exclude from budget</div>
            <div style={{ font: `400 12px/18px ${PAY.font}`, color: muted, marginTop: 4 }}>
              Excluding this transaction will remove it from all budget calculations
            </div>
          </div>
          <div onClick={() => setExcluded(v => !v)} style={{
            width: 44, height: 26, borderRadius: 999, padding: 3,
            background: excluded ? PAY.orange : '#E7DBC8',
            display: 'flex', alignItems: 'center',
            justifyContent: excluded ? 'flex-end' : 'flex-start',
            transition: 'background 180ms, justify-content 180ms', cursor: 'pointer', flexShrink: 0,
          }}>
            <div style={{ width: 20, height: 20, borderRadius: '50%', background: 'white', boxShadow: '0 1px 3px rgba(0,0,0,0.18)' }}/>
          </div>
        </div>

        {/* ── Category ─────────────────────────────── */}
        <div style={{ marginTop: 14, background: cardBg, border: cardBorder, borderRadius: 20, padding: '16px 20px', display: 'flex', alignItems: 'center', gap: 12 }}>
          <div style={{ flex: 1, font: `700 15px/20px ${PAY.font}`, color: accentBrown }}>Category</div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 6, padding: '6px 12px', borderRadius: 999, background: warmAccent }}>
            <Icon name="home" size={16} color={accentBrown}/>
            <span style={{ font: `600 13px/16px ${PAY.font}`, color: accentBrown }}>Family · Support</span>
          </div>
        </div>

        {/* ── Notes ────────────────────────────────── */}
        <div style={{ marginTop: 14, background: cardBg, border: cardBorder, borderRadius: 20, padding: 20 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <Icon name="chat" size={18} color={accentBrown}/>
            <div style={{ font: `700 15px/20px ${PAY.font}`, color: accentBrown }}>Notes</div>
          </div>
          <div style={{ font: `400 13px/19px ${PAY.font}`, color: muted, marginTop: 10 }}>
            Family support · monthly transfer to Mum in Accra.
          </div>
        </div>

        {/* ── Attachments ──────────────────────────── */}
        <div style={{ marginTop: 14, background: cardBg, border: cardBorder, borderRadius: 20, padding: 20 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <Icon name="download" size={18} color={accentBrown}/>
            <div style={{ font: `700 15px/20px ${PAY.font}`, color: accentBrown }}>Attachments</div>
          </div>
          <div style={{ font: `400 13px/19px ${PAY.font}`, color: muted, marginTop: 10 }}>No attachments yet</div>
          <div style={{ marginTop: 14, display: 'inline-flex', alignItems: 'center', gap: 6, padding: '8px 14px', border: '1px solid #E7DBC8', borderRadius: 10, cursor: 'pointer' }}>
            <span style={{ font: `700 18px/18px ${PAY.font}`, color: accentBrown }}>+</span>
            <span style={{ font: `600 13px/16px ${PAY.font}`, color: accentBrown }}>Attach file</span>
          </div>
        </div>

        {/* ── History ──────────────────────────────── */}
        <div style={{ marginTop: 14, background: cardBg, border: cardBorder, borderRadius: 20, padding: 20 }}>
          <div style={{ display: 'flex', alignItems: 'center' }}>
            <div style={{ flex: 1, font: `700 15px/20px ${PAY.font}`, color: accentBrown }}>History</div>
            <Icon name="chev" size={20} color={accentBrown}/>
          </div>
          <div style={{ marginTop: 16, display: 'flex', justifyContent: 'space-between' }}>
            <span style={{ font: `400 13px/18px ${PAY.font}`, color: muted }}>Number of transactions</span>
            <span style={{ font: `600 13px/18px ${PAY.font}`, color: accentBrown }}>12</span>
          </div>
          <div style={{ marginTop: 10, display: 'flex', justifyContent: 'space-between' }}>
            <span style={{ font: `400 13px/18px ${PAY.font}`, color: muted }}>Average spend</span>
            <span style={{ font: `600 13px/18px ${PAY.font}`, color: accentBrown }}>£108.50</span>
          </div>
          <div style={{ marginTop: 10, display: 'flex', justifyContent: 'space-between' }}>
            <span style={{ font: `700 13px/18px ${PAY.font}`, color: accentBrown }}>Total spent</span>
            <span style={{ font: `800 13px/18px ${PAY.font}`, color: accentBrown }}>£1,302.00</span>
          </div>
        </div>

        {/* ── Mark as duplicate ────────────────────── */}
        <div style={{ marginTop: 24, textAlign: 'center' }}>
          <span style={{ font: `700 14px/20px ${PAY.font}`, color: accentBrown, cursor: 'pointer' }}>Mark as duplicate</span>
        </div>
      </div>
    </div>
  );
}

// ─── Send money flow (modal sheet) ────────────────────────────────────────
function SendFlow({ onClose, onDone }) {
  const [step, setStep] = React.useState(1);
  const [amount, setAmount] = React.useState('100');
  const [recipient, setRecipient] = React.useState({ name: 'Ama Serwaa', cc: 'gh', sub: 'MTN MoMo · •• 0277' });
  const ghs = (parseFloat(amount || 0) * 19.42).toFixed(2);

  return (
    <div style={{
      position: 'absolute', inset: 0, zIndex: 50,
      background: 'rgba(15, 13, 14, 0.5)', backdropFilter: 'blur(8px)',
      display: 'flex', alignItems: 'flex-end',
      animation: 'payFade 200ms ease-out',
    }} onClick={onClose}>
      <div onClick={e => e.stopPropagation()} style={{
        width: '100%', background: PAY.warm100, borderRadius: '24px 24px 0 0',
        padding: '14px 18px 24px', maxHeight: '85%', overflow: 'auto',
        animation: 'paySlideUp 280ms cubic-bezier(.2,.8,.2,1)',
      }}>
        <div style={{ width: 44, height: 4, borderRadius: 4, background: '#DCCDB7', margin: '0 auto 14px' }}/>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 14 }}>
          {step > 1 && <div onClick={() => setStep(step - 1)} style={{ cursor: 'pointer', color: PAY.warm900 }}><Icon name="back" size={20}/></div>}
          <div style={{ flex: 1 }}>
            <div style={{ font: `400 10px/14px ${PAY.font}`, color: PAY.warm800, letterSpacing: 1, textTransform: 'uppercase' }}>Step {step} of 3</div>
            <div style={{ font: `700 18px/22px ${PAY.font}`, color: PAY.warm900 }}>
              {step === 1 ? 'How much?' : step === 2 ? 'To whom?' : 'Review & send'}
            </div>
          </div>
          <div onClick={onClose} style={{ cursor: 'pointer', color: PAY.warm900 }}><Icon name="close" size={20}/></div>
        </div>

        {step === 1 && (
          <>
            <div style={{ background: 'white', borderRadius: 20, padding: 18, border: '1px solid #F1E5D1', boxShadow: '0 2px 10px rgba(77,49,32,0.05)' }}>
              <div style={{ font: `600 11px/14px ${PAY.font}`, color: PAY.warm800, letterSpacing: 0.6, textTransform: 'uppercase' }}>You send</div>
              <div style={{ display: 'flex', alignItems: 'baseline', gap: 6, marginTop: 6 }}>
                <div style={{ font: `700 28px/32px ${PAY.font}`, color: PAY.warm900 }}>£</div>
                <input value={amount} onChange={e => setAmount(e.target.value.replace(/[^0-9.]/g, ''))} style={{ flex: 1, font: `800 44px/48px ${PAY.font}`, letterSpacing: -1, border: 0, outline: 'none', background: 'transparent', color: PAY.ink, width: '100%' }}/>
              </div>
              <div style={{ height: 1, background: '#F5EADB', margin: '14px 0' }}/>
              <div style={{ font: `600 11px/14px ${PAY.font}`, color: PAY.warm800, letterSpacing: 0.6, textTransform: 'uppercase' }}>They receive</div>
              <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginTop: 6 }}>
                <div style={{ width: 28, height: 28, borderRadius: 50, backgroundImage: "url('assets/flags/gh.svg')", backgroundSize: 'cover', boxShadow: '0 0 0 1.5px #F1E5D1' }}/>
                <div style={{ font: `700 22px/26px ${PAY.font}`, color: PAY.warm900 }}>GHS {ghs}</div>
              </div>
              <div style={{ font: `400 11px/14px ${PAY.font}`, color: PAY.warm800, marginTop: 6 }}>£1 = GHS 19.42 · Fee £1.50 · Arrives in ~2 min</div>
            </div>
            <div style={{ display: 'flex', gap: 6, marginTop: 12 }}>
              {[50, 100, 200, 500].map(q => (
                <div key={q} onClick={() => setAmount(String(q))} style={{
                  flex: 1, padding: '10px 0', textAlign: 'center', borderRadius: 50,
                  background: amount === String(q) ? PAY.warm900 : 'white',
                  color: amount === String(q) ? 'white' : PAY.warm900,
                  border: `1px solid ${amount === String(q) ? PAY.warm900 : '#F1E5D1'}`,
                  font: `700 12px/16px ${PAY.font}`, cursor: 'pointer',
                }}>£{q}</div>
              ))}
            </div>
            <div style={{ marginTop: 16 }}>
              <PayButton variant="primary" full onClick={() => setStep(2)}>Continue</PayButton>
            </div>
          </>
        )}

        {step === 2 && (
          <>
            <div style={{ font: `600 11px/14px ${PAY.font}`, color: PAY.warm800, letterSpacing: 0.6, textTransform: 'uppercase', marginBottom: 8 }}>Frequent</div>
            {[
              { name: 'Ama Serwaa', cc: 'gh', sub: 'Mum · MTN MoMo' },
              { name: 'Kofi Owusu', cc: 'gh', sub: 'Brother · MTN MoMo' },
              { name: 'Ebo Bonsu', cc: 'ng', sub: 'Uncle · Kuda Bank' },
              { name: 'Yaa Asantewaa', cc: 'gh', sub: 'Sister · Vodafone Cash' },
            ].map((r, i) => (
              <div key={i} onClick={() => { setRecipient(r); setStep(3); }} style={{
                display: 'flex', alignItems: 'center', gap: 12, padding: '12px 14px', borderRadius: 14,
                background: 'white', border: '1px solid #F1E5D1', marginBottom: 8, cursor: 'pointer',
              }}>
                <div style={{ position: 'relative' }}>
                  <div style={{ width: 40, height: 40, borderRadius: 50, background: '#FFEFE3', color: '#7A3211', display: 'flex', alignItems: 'center', justifyContent: 'center', font: `700 13px/18px ${PAY.font}` }}>{r.name.split(' ').map(w => w[0]).join('').slice(0,2)}</div>
                  <div style={{ position: 'absolute', bottom: -2, right: -2, width: 16, height: 16, borderRadius: 50, backgroundImage: `url('assets/flags/${r.cc}.svg')`, backgroundSize: 'cover', border: '1.5px solid white' }}/>
                </div>
                <div style={{ flex: 1 }}>
                  <div style={{ font: `700 13px/18px ${PAY.font}`, color: PAY.ink }}>{r.name}</div>
                  <div style={{ font: `400 11px/14px ${PAY.font}`, color: PAY.warm800 }}>{r.sub}</div>
                </div>
                <Icon name="chev" size={16} color={PAY.warm800}/>
              </div>
            ))}
          </>
        )}

        {step === 3 && (
          <>
            <div style={{ background: 'white', borderRadius: 20, padding: 18, border: '1px solid #F1E5D1', boxShadow: '0 2px 10px rgba(77,49,32,0.05)' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
                <div style={{ position: 'relative' }}>
                  <div style={{ width: 44, height: 44, borderRadius: 50, background: '#FFEFE3', color: '#7A3211', display: 'flex', alignItems: 'center', justifyContent: 'center', font: `700 14px/20px ${PAY.font}` }}>{recipient.name.split(' ').map(w => w[0]).join('').slice(0,2)}</div>
                  <div style={{ position: 'absolute', bottom: -2, right: -2, width: 18, height: 18, borderRadius: 50, backgroundImage: `url('assets/flags/${recipient.cc}.svg')`, backgroundSize: 'cover', border: '2px solid white' }}/>
                </div>
                <div style={{ flex: 1 }}>
                  <div style={{ font: `400 11px/14px ${PAY.font}`, color: PAY.warm800, letterSpacing: 0.4, textTransform: 'uppercase' }}>Sending to</div>
                  <div style={{ font: `700 15px/20px ${PAY.font}`, color: PAY.warm900 }}>{recipient.name}</div>
                  <div style={{ font: `400 11px/14px ${PAY.font}`, color: PAY.warm800 }}>{recipient.sub}</div>
                </div>
              </div>
              <div style={{ height: 1, background: '#F5EADB', margin: '14px 0' }}/>
              <div style={{ display: 'flex', justifyContent: 'space-between' }}><div style={{ font: `400 12px/17px ${PAY.font}`, color: PAY.warm800 }}>You send</div><div style={{ font: `700 14px/18px ${PAY.font}`, color: PAY.ink }}>£{amount}.00</div></div>
              <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: 6 }}><div style={{ font: `400 12px/17px ${PAY.font}`, color: PAY.warm800 }}>Fee</div><div style={{ font: `600 14px/18px ${PAY.font}`, color: PAY.ink }}>£1.50</div></div>
              <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: 6 }}><div style={{ font: `400 12px/17px ${PAY.font}`, color: PAY.warm800 }}>Rate</div><div style={{ font: `600 14px/18px ${PAY.font}`, color: PAY.ink }}>£1 = GHS 19.42</div></div>
              <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: 10, paddingTop: 10, borderTop: '1px solid #F5EADB' }}><div style={{ font: `700 14px/18px ${PAY.font}`, color: PAY.warm900 }}>They receive</div><div style={{ font: `800 20px/24px ${PAY.font}`, color: PAY.orange }}>GHS {ghs}</div></div>
            </div>
            <div style={{ marginTop: 16 }}>
              <PayButton variant="primary" full size="lg" onClick={onDone}>Slide to send</PayButton>
            </div>
            <div style={{ font: `400 11px/16px ${PAY.font}`, color: PAY.warm800, textAlign: 'center', marginTop: 10 }}>
              Protected by Face ID · arrives in about 2 minutes
            </div>
          </>
        )}
      </div>
    </div>
  );
}

// ─── Onboarding ───────────────────────────────────────────────────────────
function OnboardingScreen({ onDone }) {
  const [step, setStep] = React.useState(0);
  const slides = [
    {
      kicker: 'Welcome',
      title: 'One place for\nmoney that moves\nbetween homes.',
      body: 'Pay bills in the UK, send to family back home, and let Simi keep an eye on it all.',
      hero: 'assets/setup-hero.png',
    },
    {
      kicker: 'Meet Simi',
      title: 'Your AI companion\nfor money matters.',
      body: 'Simi watches your bills, flags unusual spending, and answers questions in plain language.',
      hero: 'assets/slider-img-02.png',
      isSimi: true,
    },
    {
      kicker: 'Diaspora-first',
      title: 'Mid-market rates.\nNo hidden fees.',
      body: 'Send to Ghana, Nigeria, Zambia, Zimbabwe and Botswana — at the rates banks see.',
      hero: 'assets/slider-img-04.png',
      flags: ['gh', 'ng', 'zm', 'zw', 'bw'],
    },
  ];
  const s = slides[step];

  return (
    <div style={{ background: '#0F0D0E', height: '100%', position: 'relative', overflow: 'hidden', color: 'white' }}>
      {/* hero image */}
      <div style={{
        position: 'absolute', inset: 0,
        backgroundImage: `url('${s.hero}')`,
        backgroundSize: 'cover', backgroundPosition: 'center',
        transition: 'opacity 400ms', opacity: 0.95,
      }}/>
      <div style={{
        position: 'absolute', inset: 0,
        background: 'linear-gradient(180deg, rgba(15,13,14,0.2) 0%, rgba(15,13,14,0.65) 50%, rgba(15,13,14,0.95) 90%)',
      }}/>
      <GlowOrb size={300} bottom={'30%'} left={-80} opacity={0.32}/>

      {/* skip */}
      <div style={{ position: 'absolute', top: 14, right: 18, zIndex: 3 }}>
        <div onClick={onDone} style={{ font: `700 11px/14px ${PAY.font}`, color: 'rgba(255,255,255,0.7)', cursor: 'pointer', letterSpacing: 0.8, textTransform: 'uppercase' }}>Skip</div>
      </div>

      <div style={{ position: 'absolute', bottom: 0, left: 0, right: 0, padding: '32px 26px 28px', zIndex: 3 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 18 }}>
          <div style={{ font: `800 18px/22px ${PAY.font}`, color: 'white', letterSpacing: -0.4 }}>Payabo<span style={{ color: PAY.orange, marginLeft: 2 }}>·</span></div>
        </div>

        <div style={{ font: `700 11px/14px ${PAY.font}`, color: PAY.orangeSoft, letterSpacing: 1.4, textTransform: 'uppercase' }}>{s.kicker}</div>
        <div style={{ font: `800 32px/36px ${PAY.font}`, color: 'white', letterSpacing: -0.8, marginTop: 8, whiteSpace: 'pre-line' }}>{s.title}</div>
        <div style={{ font: `400 14px/22px ${PAY.font}`, color: 'rgba(255,255,255,0.78)', marginTop: 14 }}>{s.body}</div>

        {s.flags && (
          <div style={{ display: 'flex', gap: 8, marginTop: 16 }}>
            {s.flags.map(cc => (
              <div key={cc} style={{ width: 32, height: 32, borderRadius: 50, backgroundImage: `url('assets/flags/${cc}.svg')`, backgroundSize: 'cover', boxShadow: '0 0 0 2px rgba(255,255,255,0.4)' }}/>
            ))}
          </div>
        )}

        <div style={{ display: 'flex', alignItems: 'center', gap: 14, marginTop: 28 }}>
          <div style={{ display: 'flex', gap: 6 }}>
            {slides.map((_, i) => (
              <div key={i} style={{
                width: i === step ? 22 : 6, height: 6, borderRadius: 50,
                background: i === step ? PAY.orange : 'rgba(255,255,255,0.3)',
                transition: 'all 200ms',
              }}/>
            ))}
          </div>
          <div style={{ flex: 1 }}/>
          <div onClick={() => step < slides.length - 1 ? setStep(step + 1) : onDone()} style={{
            width: 60, height: 60, borderRadius: 50,
            background: PAY.orange, display: 'flex', alignItems: 'center', justifyContent: 'center',
            cursor: 'pointer', boxShadow: '0 8px 24px rgba(243,121,32,0.5)',
          }}>
            <Icon name="chev" size={22} color="white" strokeWidth={2.5}/>
          </div>
        </div>
      </div>
    </div>
  );
}

// ─── Chat history slide-out (from chat_history_screen.dart) ────────────────
function ChatHistoryOverlay({ open, onClose, isFresh }) {
  const items = isFresh ? [] : [
    { id: '1', title: 'Sky broadband — schedule for Thursday', date: 'Today, 9:31 AM' },
    { id: '2', title: 'Net worth check-in', date: 'Yesterday, 8:14 PM' },
    { id: '3', title: 'Send £100 to Mum (Ama)', date: 'Yesterday, 12:02 PM' },
    { id: '4', title: 'Vodafone bill paid', date: 'Monday, 11:05 AM' },
    { id: '5', title: 'May spending review', date: 'May 3, 7:18 PM' },
    { id: '6', title: 'Switch GBP main to Monzo', date: 'Apr 28, 4:42 PM' },
  ];
  const [q, setQ] = React.useState('');
  const filtered = items.filter(i => i.title.toLowerCase().includes(q.toLowerCase()));
  return (
    <div style={{
      position: 'absolute', inset: 0, zIndex: 40, pointerEvents: open ? 'auto' : 'none',
    }}>
      <div onClick={onClose} style={{
        position: 'absolute', inset: 0,
        background: open ? 'rgba(0,0,0,0.45)' : 'rgba(0,0,0,0)',
        transition: 'background 240ms ease-out',
      }}/>
      <div style={{
        position: 'absolute', top: 0, bottom: 0, left: 0, width: '90%',
        background: 'linear-gradient(180deg, #34231B 0%, #1A120E 42%, #070505 100%)',
        transform: open ? 'translateX(0)' : 'translateX(-100%)',
        transition: 'transform 280ms cubic-bezier(.2,.8,.2,1)',
        boxShadow: open ? '4px 0 24px rgba(0,0,0,0.4)' : 'none',
        color: 'white', display: 'flex', flexDirection: 'column', overflow: 'hidden',
      }}>
        <GlowOrb size={280} top={-100} left={-80} color="#38251B" opacity={0.32} blur={70}/>
        <GlowOrb size={260} top={-70} right={-80} color="#462D1C" opacity={0.26} blur={70}/>

        <div style={{ padding: '14px 20px 4px', display: 'flex', alignItems: 'center', gap: 10, position: 'relative', zIndex: 2 }}>
          <div style={{
            flex: 1, height: 44, borderRadius: 14, background: 'rgba(255,255,255,0.06)',
            display: 'flex', alignItems: 'center', padding: '0 12px', gap: 8,
          }}>
            <Icon name="search" size={18} color="rgba(255,255,255,0.62)"/>
            <input
              value={q} onChange={e => setQ(e.target.value)}
              placeholder="Search conversations"
              style={{ flex: 1, border: 0, outline: 'none', background: 'transparent', color: 'rgba(255,255,255,0.92)', font: `400 13px/18px ${PAY.font}` }}
            />
          </div>
          <div onClick={onClose} style={{
            width: 44, height: 44, borderRadius: 50, background: 'rgba(255,255,255,0.06)',
            display: 'flex', alignItems: 'center', justifyContent: 'center', cursor: 'pointer',
          }}><Icon name="close" size={19}/></div>
        </div>

        <div style={{ flex: 1, overflow: 'auto', padding: '20px 20px 24px', position: 'relative', zIndex: 2 }}>
          <div style={{ font: `700 22px/26px ${PAY.font}`, letterSpacing: -0.5 }}>Conversation history</div>
          <div style={{ font: `400 12px/18px ${PAY.font}`, color: 'rgba(255,255,255,0.62)', marginTop: 4 }}>Every thread with Simi, ready to pick back up.</div>

          <div style={{ marginTop: 18 }}>
            {filtered.length === 0 && (
              <div style={{ padding: '20px 4px', font: `400 13px/19px ${PAY.font}`, color: 'rgba(255,255,255,0.55)' }}>
                {isFresh && !q ? 'No conversation history yet in this demo state.' : 'No conversations match your search.'}
              </div>
            )}
            {filtered.map((it, i) => (
              <div key={it.id} onClick={onClose} style={{
                display: 'flex', alignItems: 'center', gap: 12, padding: '14px 6px',
                borderBottom: i < filtered.length - 1 ? '1px solid rgba(255,255,255,0.06)' : 'none',
                cursor: 'pointer',
              }}>
                <div style={{ width: 42, height: 42, borderRadius: 50, background: '#1E1611', display: 'flex', alignItems: 'center', justifyContent: 'center', color: '#C8A882' }}>
                  <Icon name="chat" size={18}/>
                </div>
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{ font: `600 13px/17px ${PAY.font}`, color: 'rgba(255,255,255,0.92)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{it.title}</div>
                  <div style={{ font: `400 11px/15px ${PAY.font}`, color: 'rgba(255,255,255,0.55)', marginTop: 2 }}>{it.date}</div>
                </div>
                <Icon name="chev" size={16} color="rgba(255,255,255,0.55)"/>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}

Object.assign(window, { ChatScreen, VoiceScreen, TxnDetailScreen, SendFlow, OnboardingScreen, ChatHistoryOverlay });
