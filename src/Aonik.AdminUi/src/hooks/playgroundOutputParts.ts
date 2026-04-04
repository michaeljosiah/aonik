import type { PlaygroundOutputPart } from '@/hooks/usePlaygroundChat';

export function upsertTrailingTextPart(
  parts: PlaygroundOutputPart[],
  content: string,
): PlaygroundOutputPart[] {
  const updated = [...parts];
  const lastPart = updated[updated.length - 1];

  if (lastPart && lastPart.type === 'text') {
    updated[updated.length - 1] = { type: 'text', content };
    return updated;
  }

  updated.push({ type: 'text', content });
  return updated;
}
