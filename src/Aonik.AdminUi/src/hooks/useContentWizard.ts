import { useCallback, useEffect, useRef, useState } from 'react';
import { generateId } from '@/lib/agui-client';
import type { UseAguiChatReturn, FrontendToolConfig } from '@/hooks/useAguiChat';
import type {
  WizardStep,
  WizardConfig,
  ContentSuggestion,
  SuggestionStatus,
} from '@/types/contentWizard';
import { DEFAULT_WIZARD_CONFIG } from '@/types/contentWizard';

export interface UseContentWizardReturn {
  step: WizardStep;
  setStep: (step: WizardStep) => void;
  config: WizardConfig;
  setConfig: (config: WizardConfig) => void;
  suggestions: ContentSuggestion[];
  updateSuggestionStatus: (id: string, status: SuggestionStatus) => void;
  updateSuggestion: (id: string, updates: Partial<ContentSuggestion>) => void;
  approvedSuggestions: ContentSuggestion[];
  startGeneration: () => void;
  isGenerating: boolean;
  savedCount: number;
  setSavedCount: (count: number) => void;
  resetWizard: () => void;
}

export function useContentWizard(chat: UseAguiChatReturn): UseContentWizardReturn {
  const [step, setStep] = useState<WizardStep>('configure');
  const [config, setConfig] = useState<WizardConfig>(DEFAULT_WIZARD_CONFIG);
  const [suggestions, setSuggestions] = useState<ContentSuggestion[]>([]);
  const [savedCount, setSavedCount] = useState(0);
  const toolsRegistered = useRef(false);

  // Register frontend tools with AG-UI chat
  useEffect(() => {
    if (toolsRegistered.current) return;
    toolsRegistered.current = true;

    const proposeContentBlock: FrontendToolConfig = {
      name: 'proposeContentBlock',
      description:
        'Propose a new content block for the user to review. Call this once per suggestion. The content will appear as a card in the wizard UI.',
      parameters: {
        type: 'object',
        properties: {
          contentKey: {
            type: 'string',
            description: 'Unique key like "community-news.budget-planning-101"',
          },
          title: { type: 'string', description: 'The article/content title' },
          body: {
            type: 'string',
            description: 'The full content body in the configured format (Markdown, Html, or Json)',
          },
          area: {
            type: 'string',
            enum: [
              'General', 'Banner', 'Hero', 'Sidebar', 'Footer',
              'MySpaceBanner', 'CommunityNews', 'CommunityVideo', 'CommunityVideoCategory',
            ],
          },
          format: { type: 'string', enum: ['Markdown', 'Html', 'Json'] },
          slug: { type: 'string', description: 'URL-friendly slug' },
          locale: { type: 'string', description: 'ISO locale code, e.g. "en"' },
          priority: { type: 'number', description: 'Display priority (lower = first)' },
          imagePrompt: {
            type: 'string',
            description: 'A descriptive prompt for generating a hero image for the article (only if image generation was requested)',
          },
        },
        required: ['contentKey', 'title', 'body', 'area', 'format'],
      },
      handler: (args: Record<string, unknown>) => {
        const suggestion: ContentSuggestion = {
          id: generateId(),
          contentKey: args.contentKey as string,
          title: args.title as string,
          body: args.body as string,
          area: args.area as string,
          format: args.format as string,
          slug: (args.slug as string) || undefined,
          locale: (args.locale as string) || 'en',
          priority: (args.priority as number) || 100,
          status: 'pending',
          imagePrompt: (args.imagePrompt as string) || undefined,
        };
        setSuggestions((prev) => [...prev, suggestion]);
        return JSON.stringify({ success: true, suggestionId: suggestion.id, message: `Proposed: "${suggestion.title}"` });
      },
    };

    const updateContentSuggestion: FrontendToolConfig = {
      name: 'updateContentSuggestion',
      description:
        'Update a previously proposed content block by its suggestion ID. Use this when the user asks for refinements.',
      parameters: {
        type: 'object',
        properties: {
          suggestionId: { type: 'string', description: 'The ID of the suggestion to update' },
          title: { type: 'string' },
          body: { type: 'string' },
          contentKey: { type: 'string' },
          slug: { type: 'string' },
          area: { type: 'string' },
          format: { type: 'string' },
          locale: { type: 'string' },
          priority: { type: 'number' },
        },
        required: ['suggestionId'],
      },
      handler: (args: Record<string, unknown>) => {
        const { suggestionId, ...updates } = args;
        setSuggestions((prev) =>
          prev.map((s) => {
            if (s.id !== suggestionId) return s;
            const updated = { ...s };
            if (updates.title) updated.title = updates.title as string;
            if (updates.body) updated.body = updates.body as string;
            if (updates.contentKey) updated.contentKey = updates.contentKey as string;
            if (updates.slug) updated.slug = updates.slug as string;
            if (updates.area) updated.area = updates.area as string;
            if (updates.format) updated.format = updates.format as string;
            if (updates.locale) updated.locale = updates.locale as string;
            if (updates.priority != null) updated.priority = updates.priority as number;
            return updated;
          }),
        );
        return JSON.stringify({ success: true, message: 'Suggestion updated' });
      },
    };

    const getWizardState: FrontendToolConfig = {
      name: 'getWizardState',
      description:
        'Get the current wizard configuration, all proposed suggestions, and their approval statuses. Use this to understand context before making refinements.',
      parameters: {
        type: 'object',
        properties: {},
      },
      handler: () => {
        return JSON.stringify({
          config,
          suggestions: suggestions.map((s) => ({
            id: s.id,
            contentKey: s.contentKey,
            title: s.title,
            area: s.area,
            format: s.format,
            status: s.status,
          })),
          currentStep: step,
        });
      },
    };

    chat.registerTool(proposeContentBlock);
    chat.registerTool(updateContentSuggestion);
    chat.registerTool(getWizardState);

    return () => {
      chat.unregisterTool('proposeContentBlock');
      chat.unregisterTool('updateContentSuggestion');
      chat.unregisterTool('getWizardState');
      toolsRegistered.current = false;
    };
  }, [chat.registerTool, chat.unregisterTool]);

  const updateSuggestionStatus = useCallback((id: string, status: SuggestionStatus) => {
    setSuggestions((prev) =>
      prev.map((s) => (s.id === id ? { ...s, status } : s)),
    );
  }, []);

  const updateSuggestion = useCallback((id: string, updates: Partial<ContentSuggestion>) => {
    setSuggestions((prev) =>
      prev.map((s) => (s.id === id ? { ...s, ...updates } : s)),
    );
  }, []);

  const approvedSuggestions = suggestions.filter((s) => s.status === 'approved');

  const startGeneration = useCallback(() => {
    setSuggestions([]);
    setStep('generate');

    const lines = [
      `Generate ${config.count} content block${config.count > 1 ? 's' : ''} for the "${config.area}" area.`,
      `Topic: ${config.topic}`,
      `Format: ${config.format}`,
      `Locale: ${config.locale}`,
      `Tone: ${config.tone}`,
      '',
      'For each content block, call the proposeContentBlock tool with a unique contentKey, title, body, area, format, slug, locale, and priority.',
      `The area must be "${config.area}" and format must be "${config.format}".`,
      'Make each piece of content substantive, well-structured, and ready to publish.',
      'Use the contentKey format: "<area-slug>.<topic-slug>" (e.g., "community-news.budget-planning-101").',
    ];

    if (config.includeImages) {
      lines.push(
        '',
        'For each content block, also include an imagePrompt — a detailed, descriptive prompt suitable for an AI image generator to create a hero/banner image for the article.',
        'The imagePrompt should describe a professional, visually appealing image relevant to the article topic. Do not include text in the image.',
      );
    }

    const prompt = lines.join('\n');

    chat.sendMessage(prompt);
  }, [config, chat]);

  const isGenerating = chat.isStreaming && step === 'generate';

  const resetWizard = useCallback(() => {
    setStep('configure');
    setConfig(DEFAULT_WIZARD_CONFIG);
    setSuggestions([]);
    setSavedCount(0);
    chat.resetChat();
  }, [chat]);

  return {
    step,
    setStep,
    config,
    setConfig,
    suggestions,
    updateSuggestionStatus,
    updateSuggestion,
    approvedSuggestions,
    startGeneration,
    isGenerating,
    savedCount,
    setSavedCount,
    resetWizard,
  };
}
