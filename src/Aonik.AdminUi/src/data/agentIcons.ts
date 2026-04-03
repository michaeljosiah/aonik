export interface AgentIconOption {
  label: string;
  url: string;
}

/**
 * Fetches the list of available agent icons from the auto-generated manifest.
 * The manifest is rebuilt whenever files are added/removed from public/images/agents/.
 */
export async function loadAgentIcons(): Promise<AgentIconOption[]> {
  try {
    const res = await fetch('/images/agents/manifest.json');
    if (!res.ok) return [];
    const urls: string[] = await res.json();
    return urls.map((url) => {
      // Derive a human-readable label from the filename
      const filename = url.split('/').pop() ?? '';
      const name = filename.replace(/\.[^.]+$/, ''); // strip extension
      const label = name
        .replace(/^icon-/, '')
        .replace(/[-_]/g, ' ')
        .replace(/\b\w/g, (c) => c.toUpperCase());
      return { label, url };
    });
  } catch {
    return [];
  }
}
