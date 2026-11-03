import { AGENT_PORTRAITS, resolveAgentPortrait } from './agentPortraits.generated';

const normalize = (value: string) => value.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');

export function agentDisplayName(name?: string): string {
  const key = normalize(name ?? '');
  return AGENT_PORTRAITS.find((entry) => entry.aliases.some((alias) => normalize(alias) === key))?.name
    ?? (name?.trim().slice(0, 128) || 'AI assistant');
}

/** Identity comes from producer metadata, never from notification prose or an image URL. */
export function notificationAgentName(notification: { source: string; metadataJson: string }): string | undefined {
  if (notification.source.toLowerCase() !== 'agent') return undefined;
  try {
    const metadata: unknown = JSON.parse(notification.metadataJson);
    if (!metadata || typeof metadata !== 'object' || Array.isArray(metadata)) return undefined;
    const name = (metadata as Record<string, unknown>).agentName;
    if (typeof name !== 'string' || !name.trim() || name.length > 128 || [...name].some((character) => character.charCodeAt(0) < 32)) return undefined;
    return name.trim();
  } catch {
    return undefined;
  }
}

export { resolveAgentPortrait };
