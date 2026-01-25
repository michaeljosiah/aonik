export interface SetupGuideManifest {
  version: number;
  sections: SetupGuideSection[];
  guides: SetupGuideDefinition[];
}

export interface SetupGuideSection {
  id: string;
  title: string;
  description?: string;
  order: number;
  guideIds: string[];
}

export interface SetupGuideDefinition {
  id: string;
  slug: string;
  title: string;
  description: string;
  category: string;
  order: number;
  accent?: string;
  cover?: string;
}

const basePath = '/content/setup-guides';

export async function getSetupGuideManifest(): Promise<SetupGuideManifest> {
  const response = await fetch(`${basePath}/guides.json`, { cache: 'no-cache' });
  if (!response.ok) {
    throw new Error('Unable to load setup guide manifest.');
  }
  return response.json() as Promise<SetupGuideManifest>;
}

export async function getSetupGuideMarkdown(slug: string): Promise<string> {
  const response = await fetch(`${basePath}/${slug}/index.md`, { cache: 'no-cache' });
  if (!response.ok) {
    throw new Error('Unable to load setup guide content.');
  }
  return response.text();
}
