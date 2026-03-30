export type WizardStep = 'configure' | 'generate' | 'review' | 'save';

export type SuggestionStatus = 'pending' | 'approved' | 'rejected';

export interface ContentSuggestion {
  id: string;
  contentKey: string;
  title: string;
  slug?: string;
  body: string;
  area: string;
  format: string;
  locale: string;
  priority: number;
  status: SuggestionStatus;
  imagePrompt?: string;
}

export interface ImageDimensions {
  width: number;
  height: number;
  label: string;
}

export interface WizardConfig {
  area: string;
  format: string;
  locale: string;
  topic: string;
  tone: string;
  count: number;
  includeImages: boolean;
  imageDimensions: ImageDimensions;
}

export const WIZARD_STEPS: { key: WizardStep; label: string }[] = [
  { key: 'configure', label: 'Configure' },
  { key: 'generate', label: 'Generate' },
  { key: 'review', label: 'Review' },
  { key: 'save', label: 'Save' },
];

export const AREA_OPTIONS = [
  'General',
  'Banner',
  'Hero',
  'Sidebar',
  'Footer',
  'MySpaceBanner',
  'CommunityNews',
  'CommunityVideo',
  'CommunityVideoCategory',
] as const;

export const FORMAT_OPTIONS = ['Markdown', 'Html', 'Json'] as const;

export const TONE_OPTIONS = [
  'Professional',
  'Friendly',
  'Educational',
  'Motivational',
  'Casual',
] as const;

export const IMAGE_DIMENSION_PRESETS: ImageDimensions[] = [
  { width: 1792, height: 1024, label: 'Landscape (1792 x 1024)' },
  { width: 1024, height: 1024, label: 'Square (1024 x 1024)' },
  { width: 1024, height: 1792, label: 'Portrait (1024 x 1792)' },
];

/** Default image dimensions per content area. */
export const AREA_IMAGE_DEFAULTS: Record<string, ImageDimensions> = {
  Banner: { width: 1792, height: 1024, label: 'Landscape (1792 x 1024)' },
  Hero: { width: 1792, height: 1024, label: 'Landscape (1792 x 1024)' },
  MySpaceBanner: { width: 1792, height: 1024, label: 'Landscape (1792 x 1024)' },
  CommunityNews: { width: 1792, height: 1024, label: 'Landscape (1792 x 1024)' },
  CommunityVideo: { width: 1792, height: 1024, label: 'Landscape (1792 x 1024)' },
  Sidebar: { width: 1024, height: 1792, label: 'Portrait (1024 x 1792)' },
  General: { width: 1024, height: 1024, label: 'Square (1024 x 1024)' },
  Footer: { width: 1792, height: 1024, label: 'Landscape (1792 x 1024)' },
  CommunityVideoCategory: { width: 1792, height: 1024, label: 'Landscape (1792 x 1024)' },
};

export const DEFAULT_WIZARD_CONFIG: WizardConfig = {
  area: 'CommunityNews',
  format: 'Markdown',
  locale: 'en',
  topic: '',
  tone: 'Professional',
  count: 3,
  includeImages: false,
  imageDimensions: AREA_IMAGE_DEFAULTS['CommunityNews'],
};
