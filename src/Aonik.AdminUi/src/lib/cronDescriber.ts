import cronstrue from 'cronstrue';

/**
 * Converts a Quartz-style cron expression (6–7 fields) to a human-readable string.
 * Falls back to the raw expression if parsing fails.
 */
export function describeCron(cron: string | null | undefined): string {
  if (!cron) return 'No schedule';
  try {
    return cronstrue.toString(cron, { use24HourTimeFormat: false, verbose: false });
  } catch {
    return cron;
  }
}

// ── Preset-based builder ────────────────────────────────────────────

export type CronFrequency = 'every-minute' | 'every-n-minutes' | 'hourly' | 'daily' | 'weekly' | 'monthly' | 'custom';

export interface CronPreset {
  frequency: CronFrequency;
  interval?: number;    // for every-n-minutes
  hour?: number;        // 0-23, for daily/weekly/monthly
  minute?: number;      // 0-59
  dayOfWeek?: number;   // 0=Sun … 6=Sat, for weekly
  dayOfMonth?: number;  // 1-31, for monthly
  raw?: string;         // for custom
}

/**
 * Builds a Quartz cron expression (7 fields: sec min hr dom mon dow yr)
 * from a structured preset. Returns the 6-field variant (without year)
 * which Quartz accepts as well.
 */
export function buildCron(preset: CronPreset): string {
  const m = preset.minute ?? 0;
  const h = preset.hour ?? 0;

  switch (preset.frequency) {
    case 'every-minute':
      return '0 * * * * ?';
    case 'every-n-minutes': {
      const n = preset.interval ?? 5;
      return `0 0/${n} * * * ?`;
    }
    case 'hourly':
      return `0 ${m} * * * ?`;
    case 'daily':
      return `0 ${m} ${h} * * ?`;
    case 'weekly': {
      const dow = ['SUN', 'MON', 'TUE', 'WED', 'THU', 'FRI', 'SAT'][preset.dayOfWeek ?? 1];
      return `0 ${m} ${h} ? * ${dow}`;
    }
    case 'monthly': {
      const dom = preset.dayOfMonth ?? 1;
      return `0 ${m} ${h} ${dom} * ?`;
    }
    case 'custom':
      return preset.raw ?? '0 * * * * ?';
  }
}

/**
 * Attempts to parse a Quartz cron string into a CronPreset.
 * Returns a "custom" preset if the expression can't be mapped to a known pattern.
 */
export function parseCron(cron: string): CronPreset {
  const parts = cron.trim().split(/\s+/);
  // Quartz has 6 or 7 fields: sec min hr dom mon dow [yr]
  if (parts.length < 6) return { frequency: 'custom', raw: cron };

  const [, min, hr, dom, , dow] = parts;

  // every minute: 0 * * * * ?
  if (min === '*' && hr === '*') {
    return { frequency: 'every-minute' };
  }

  // every N minutes: 0 0/N * * * ?
  const nMinMatch = min.match(/^0\/(\d+)$/);
  if (nMinMatch && hr === '*') {
    return { frequency: 'every-n-minutes', interval: parseInt(nMinMatch[1], 10) };
  }

  // hourly: 0 M * * * ?
  if (/^\d+$/.test(min) && hr === '*') {
    return { frequency: 'hourly', minute: parseInt(min, 10) };
  }

  // daily: 0 M H * * ?
  if (/^\d+$/.test(min) && /^\d+$/.test(hr) && dom === '*' && dow === '?') {
    return { frequency: 'daily', minute: parseInt(min, 10), hour: parseInt(hr, 10) };
  }

  // weekly: 0 M H ? * DOW
  const dowMap: Record<string, number> = { SUN: 0, MON: 1, TUE: 2, WED: 3, THU: 4, FRI: 5, SAT: 6 };
  if (/^\d+$/.test(min) && /^\d+$/.test(hr) && dom === '?' && dow in dowMap) {
    return { frequency: 'weekly', minute: parseInt(min, 10), hour: parseInt(hr, 10), dayOfWeek: dowMap[dow] };
  }

  // monthly: 0 M H D * ?
  if (/^\d+$/.test(min) && /^\d+$/.test(hr) && /^\d+$/.test(dom) && dow === '?') {
    return { frequency: 'monthly', minute: parseInt(min, 10), hour: parseInt(hr, 10), dayOfMonth: parseInt(dom, 10) };
  }

  return { frequency: 'custom', raw: cron };
}
