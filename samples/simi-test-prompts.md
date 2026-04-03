# Simi Test Prompts

Use these with the system prompt + user brief in LanFuse. Each prompt is tagged
with which brief it targets and what behaviour it tests.

---

## Established User (Ade)

### Greeting / open-ended
Tests personality, brevity, and whether Simi picks up context from the brief.

```
Hey Simi, what's up?
```

```
How am I doing this month?
```

### Open loop follow-up
Tests whether Simi uses conversation memory to follow up naturally.

```
Did the naira rate get any better?
```

### Bill payment action
Tests mutation flow (should trigger confirmAction), tone around bills.

```
Pay mum's electricity bill
```

### Spending insight
Tests whether Simi summarises rather than dumps data, and keeps it light.

```
Where's my money going?
```

### Budget pressure nudge
Tests if Simi raises the dining overspend without being preachy.

```
Am I on track with my budgets?
```

### Family support / money transfer
Tests cross-border awareness, FX context, obligations knowledge.

```
I need to send Bisi's school fees next month, how much will that be in pounds?
```

### Goal progress
Tests celebratory tone on partial progress.

```
How's my emergency fund looking?
```

### Proactive suggestion
Tests whether Simi can connect dots (e.g. upcoming bills + balance + FX).

```
Anything I should be thinking about this week?
```

### Jargon avoidance
Tests that Simi keeps it simple when asked a technical-sounding question.

```
What's my net cashflow position?
```

### Out of scope
Tests grounding — Simi should say it can't do this honestly.

```
Can you invest £500 in Tesla stock for me?
```

---

## New User (Chika)

### First greeting
Tests the "getting to know you" path, no data to lean on.

```
Hi!
```

### Exploring features
Tests whether Simi guides without assuming data exists.

```
What can you help me with?
```

### Immediate action request
Tests how Simi handles a request when no accounts are linked yet.

```
I want to send $200 to my brother in Nigeria
```

### Setup follow-through
Tests whether Simi uses setup profile to suggest next steps.

```
What should I do first?
```

### Bill payment with no bills set up
Tests graceful handling of missing data + guiding toward setup.

```
Can you pay my mum's NEPA bill?
```

### Vague financial question
Tests that Simi doesn't fabricate insights for a new user.

```
How am I doing with money?
```
