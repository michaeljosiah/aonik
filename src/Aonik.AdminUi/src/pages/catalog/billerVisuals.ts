// Deterministic logo-tile visuals for billers/connectors when no LogoUrl is set (Spec 040 §10.3):
// a stable symbol + colour derived from the name, so the same biller always looks the same.

// Categorical chart tokens (they flip with the theme); tile text uses
// --primary-foreground so it reads on both the light and dark hues.
const PALETTE = Array.from({ length: 10 }, (_, i) => `var(--chart-${i + 1})`);

export function billerColor(seed: string): string {
  let hash = 0;
  for (let i = 0; i < seed.length; i++) {
    hash = (hash * 31 + seed.charCodeAt(i)) >>> 0;
  }
  return PALETTE[hash % PALETTE.length];
}

export function billerInitials(name: string): string {
  const words = name.trim().split(/\s+/).filter(Boolean);
  if (words.length === 0) return '?';
  if (words.length === 1) return words[0].slice(0, 2).toUpperCase();
  return (words[0][0] + words[1][0]).toUpperCase();
}

export function connectorColor(type: string): string {
  const t = (type || '').toLowerCase();
  if (t.includes('flutterwave')) return '#0e7490'; // guardrail-ignore: provider brand colour
  if (t.includes('paystack')) return '#0a7d4b'; // guardrail-ignore: provider brand colour
  if (t.includes('stripe')) return '#635bff'; // guardrail-ignore: provider brand colour
  if (t.includes('simulated')) return '#7b76b6'; // guardrail-ignore: provider brand colour
  return '#0e7490'; // guardrail-ignore: provider brand colour
}

export function formatSyncTime(iso?: string | null): string | null {
  if (!iso) return null;
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return null;
  return date.toLocaleString('en-GB', {
    day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit',
  });
}
