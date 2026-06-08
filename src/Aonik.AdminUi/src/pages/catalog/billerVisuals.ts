// Deterministic logo-tile visuals for billers/connectors when no LogoUrl is set (Spec 040 §10.3):
// a stable symbol + colour derived from the name, so the same biller always looks the same.

const PALETTE = [
  '#1e4d8c', '#0d3b66', '#16a085', '#2c7a3f', '#e6b800', '#26a65b',
  '#cc0000', '#5b3aaa', '#003087', '#0b6e3a', '#d40e1e', '#1f3a5f',
  '#7b2cbf', '#0e7490', '#b4741e', '#1f6f54',
];

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
  if (t.includes('flutterwave')) return '#0e7490';
  if (t.includes('paystack')) return '#0a7d4b';
  if (t.includes('stripe')) return '#635bff';
  if (t.includes('simulated')) return '#7b76b6';
  return '#0e7490';
}

export function formatSyncTime(iso?: string | null): string | null {
  if (!iso) return null;
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return null;
  return date.toLocaleString('en-GB', {
    day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit',
  });
}
