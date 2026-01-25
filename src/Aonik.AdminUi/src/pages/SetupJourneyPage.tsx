import { useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Handle, Position, ReactFlow, Background, type Node, type Edge, type ReactFlowInstance } from 'reactflow';
import 'reactflow/dist/style.css';
import { CheckCircle2, Circle, PauseCircle, PlayCircle, ArrowRight, ArrowUpRight, ExternalLink } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { cn } from '@/lib/utils';
import type { SetupGuideDefinition, SetupGuideManifest } from '@/services/setupGuideService';
import { getSetupGuideManifest } from '@/services/setupGuideService';

type StepStatus = 'todo' | 'in-progress' | 'blocked' | 'complete' | 'skipped';

interface SetupStep {
  id: string;
  title: string;
  description: string;
  category: string;
  required: boolean;
  href?: string;
  dependsOn?: string[];
  bannerUrl: string;
}

interface SetupNodeData {
  title: string;
  description: string;
  bannerUrl: string;
  status: StepStatus;
  category: string;
  required: boolean;
  onStart: () => void;
  onComplete: () => void;
}

interface SetupJourneyPageProps {
  onSkip?: () => void;
  onComplete?: () => void;
}

const onboardingSkipKey = 'aonik:onboarding:skip';
const onboardingCompleteKey = 'aonik:onboarding:complete';
const onboardingDoneKey = 'aonik:onboarding:completedSteps';
const onboardingSkippedKey = 'aonik:onboarding:skippedSteps';

const baseSteps: SetupStep[] = [
  {
    id: 'tenant-profile',
    title: 'Confirm tenant profile',
    description: 'Set legal name, base currency, and operational preferences.',
    category: 'Identity & Access',
    required: true,
    href: '/settings/general',
    bannerUrl: '/assets/onboarding/tenant-profile.png',
  },
  {
    id: 'roles-permissions',
    title: 'Define roles and permissions',
    description: 'Create the admin and operator roles for your team.',
    category: 'Identity & Access',
    required: true,
    href: '/access/roles',
    bannerUrl: '/assets/onboarding/roles-permissions.png',
  },
  {
    id: 'catalog-billers',
    title: 'Activate catalog services',
    description: 'Select billers and the services you will offer.',
    category: 'Catalog',
    required: true,
    href: '/catalog',
    bannerUrl: '/assets/onboarding/catalog-services.png',
  },
  {
    id: 'pricing-policies',
    title: 'Set pricing & limits',
    description: 'Define fees, FX spreads, and risk limits.',
    category: 'Pricing & Policy',
    required: true,
    href: '/settings/general',
    bannerUrl: '/assets/onboarding/pricing-policy.png',
  },
  {
    id: 'partner-routing',
    title: 'Configure routing',
    description: 'Connect correspondents and routing priorities.',
    category: 'Partners & Routing',
    required: true,
    href: '/settings/general',
    bannerUrl: '/assets/onboarding/routing-partners.png',
  },
];

interface GuideSectionView {
  id: string;
  title: string;
  description?: string;
  guides: SetupGuideDefinition[];
}

const fallbackGuides: SetupGuideDefinition[] = [
  {
    id: 'correspondent-network',
    slug: 'correspondent-network',
    title: 'Setting up your correspondent network',
    description: 'Learn how to connect and manage your payment correspondents for seamless cross-border transactions.',
    category: 'Partners',
    order: 2,
    accent: 'from-blue-500/20 to-cyan-500/20',
  },
  {
    id: 'email-provider',
    slug: 'email-provider',
    title: 'Configuring your email provider',
    description: 'Set up transactional email delivery for notifications, receipts, and customer communications.',
    category: 'Notifications',
    order: 3,
    accent: 'from-purple-500/20 to-pink-500/20',
  },
  {
    id: 'compliance-rules',
    slug: 'compliance-rules',
    title: 'Understanding compliance rules',
    description: 'Configure KYC/KYB workflows, transaction limits, and automated screening policies.',
    category: 'Compliance',
    order: 5,
    accent: 'from-amber-500/20 to-orange-500/20',
  },
  {
    id: 'webhook-integration',
    slug: 'webhook-integration',
    title: 'Integrating webhooks',
    description: 'Receive real-time event notifications for payments, settlements, and status changes.',
    category: 'Integration',
    order: 6,
    accent: 'from-emerald-500/20 to-teal-500/20',
  },
  {
    id: 'fx-rates',
    slug: 'fx-rates',
    title: 'Managing FX rates and spreads',
    description: 'Configure currency exchange rates, margins, and automatic rate refresh schedules.',
    category: 'Pricing',
    order: 7,
    accent: 'from-rose-500/20 to-red-500/20',
  },
  {
    id: 'api-keys',
    slug: 'api-keys',
    title: 'Generating API keys',
    description: 'Create and manage API credentials for secure programmatic access to the platform.',
    category: 'Security',
    order: 4,
    accent: 'from-indigo-500/20 to-violet-500/20',
  },
  {
    id: 'getting-started',
    slug: 'getting-started',
    title: 'Getting started with Aonik',
    description: 'A tour of the setup journey, policies, and core capabilities to launch with confidence.',
    category: 'Foundations',
    order: 1,
    accent: 'from-slate-500/20 to-slate-300/30',
  },
];

const setupNodes = baseSteps.map((step, index) => ({
  id: step.id,
  type: 'setupNode',
  position: { x: index * 320 + 40, y: 140 },
  data: {
    title: step.title,
    description: step.description,
    bannerUrl: step.bannerUrl,
    status: 'todo',
    category: step.category,
    required: step.required,
    onStart: () => {},
    onComplete: () => {},
  },
})) as Node<SetupNodeData>[];

function getStoredList(key: string) {
  const raw = localStorage.getItem(key);
  if (!raw) return [] as string[];
  try {
    return JSON.parse(raw) as string[];
  } catch {
    return [] as string[];
  }
}

function SetupNode({ data }: { data: SetupNodeData }) {
  const statusStyles: Record<StepStatus, string> = {
    todo: 'border-[var(--color-border)] text-[var(--color-text-secondary)] bg-[var(--color-surface)]',
    'in-progress': 'border-[var(--color-info)] text-[var(--color-info)] bg-[var(--color-info-light)]',
    blocked: 'border-[var(--color-warning)] text-[var(--color-warning)] bg-[var(--color-warning-light)]',
    complete: 'border-[var(--color-success)] text-[var(--color-success)] bg-[var(--color-success-light)]',
    skipped: 'border-[var(--color-border)] text-[var(--color-text-tertiary)] bg-[var(--color-surface-inset)]',
  };

  const iconMap: Record<StepStatus, typeof Circle> = {
    todo: Circle,
    'in-progress': PlayCircle,
    blocked: PauseCircle,
    complete: CheckCircle2,
    skipped: PauseCircle,
  };

  const Icon = iconMap[data.status];

  return (
    <div className={cn('w-[280px] rounded-2xl border shadow-sm overflow-hidden', statusStyles[data.status])}>
      <Handle type="target" position={Position.Left} className="!bg-[var(--color-border)]" />
      <div
        className="h-20 bg-cover bg-center"
        style={{
          backgroundImage: data.bannerUrl ? `url(${data.bannerUrl})` : undefined,
        }}
      />
      <div className="px-4 py-4 space-y-3">
        <div className="flex items-center gap-2">
          <Icon className="h-4 w-4" />
          <span className="text-xs font-semibold uppercase tracking-wide text-[var(--color-text-tertiary)]">{data.category}</span>
        </div>
        <div className="space-y-2">
          <p className="text-base font-semibold text-[var(--color-text-primary)]">{data.title}</p>
          <p className="text-sm text-[var(--color-text-secondary)]">{data.description}</p>
        </div>
        <div className="flex items-center gap-2">
          <button
            type="button"
            onClick={(event) => {
              event.stopPropagation();
              data.onStart();
            }}
            className="text-xs font-semibold text-[var(--color-brand-primary)] hover:underline"
          >
            Start
          </button>
          <span className="text-[10px] text-[var(--color-text-tertiary)]">|</span>
          <button
            type="button"
            onClick={(event) => {
              event.stopPropagation();
              data.onComplete();
            }}
            className="text-xs font-semibold text-[var(--color-text-secondary)] hover:text-[var(--color-text-primary)]"
          >
            Mark complete
          </button>
        </div>
      </div>
      <Handle type="source" position={Position.Right} className="!bg-[var(--color-border)]" />
    </div>
  );
}

export function SetupJourneyPage({ onSkip, onComplete }: SetupJourneyPageProps) {
  const [completedSteps, setCompletedSteps] = useState<string[]>(() => getStoredList(onboardingDoneKey));
  const [skippedSteps, setSkippedSteps] = useState<string[]>(() => getStoredList(onboardingSkippedKey));
  const [selectedStepId, setSelectedStepId] = useState(baseSteps[0]?.id ?? '');
  const [wizardOpen, setWizardOpen] = useState(false);
  const [wizardStepIndex, setWizardStepIndex] = useState(0);
  const [isMobile, setIsMobile] = useState(false);
  const [guideManifest, setGuideManifest] = useState<SetupGuideManifest | null>(null);
  const [guideLoading, setGuideLoading] = useState(true);
  const [guideError, setGuideError] = useState<string | null>(null);
  const flowContainerRef = useRef<HTMLDivElement | null>(null);
  const [flowInstance, setFlowInstance] = useState<ReactFlowInstance | null>(null);
  const navigate = useNavigate();

  const stepsWithStatus = useMemo(() => {
    return baseSteps.map((step, index, allSteps) => {
      const isComplete = completedSteps.includes(step.id);
      const isSkipped = skippedSteps.includes(step.id);
      const dependsOn = index > 0 ? [allSteps[index - 1].id] : [];
      const depsComplete = dependsOn.every((dep) => completedSteps.includes(dep));
      let status: StepStatus = 'todo';
      if (isComplete) status = 'complete';
      else if (isSkipped) status = 'skipped';
      else if (!depsComplete && dependsOn.length > 0) status = 'blocked';
      return { ...step, dependsOn, status };
    });
  }, [completedSteps, skippedSteps]);

  useEffect(() => {
    const media = window.matchMedia('(max-width: 1024px)');
    const update = () => setIsMobile(media.matches);
    update();
    media.addEventListener('change', update);
    return () => media.removeEventListener('change', update);
  }, []);

  useEffect(() => {
    let active = true;

    const loadGuides = async () => {
      setGuideLoading(true);
      try {
        const manifest = await getSetupGuideManifest();
        if (!active) return;
        setGuideManifest(manifest);
        setGuideError(null);
      } catch (err) {
        if (!active) return;
        setGuideError('Unable to load guides. Showing defaults.');
      } finally {
        if (active) {
          setGuideLoading(false);
        }
      }
    };

    loadGuides();

    return () => {
      active = false;
    };
  }, []);

  useEffect(() => {
    if (!flowInstance || !flowContainerRef.current) return;
    const observer = new ResizeObserver(() => {
      flowInstance.fitView({ padding: 0.2, duration: 200 });
    });
    observer.observe(flowContainerRef.current);
    return () => observer.disconnect();
  }, [flowInstance]);

  const requiredSteps = stepsWithStatus.filter((step) => step.required);
  const requiredCompleted = requiredSteps.filter((step) => step.status === 'complete').length;
  const isReadyToLaunch = requiredCompleted === requiredSteps.length && requiredSteps.length > 0;
  const flowMinWidth = baseSteps.length * 320 + 80;

  const resolvedManifest = useMemo(() => {
    if (guideManifest) return guideManifest;
    if (guideError) {
      return { version: 1, sections: [], guides: fallbackGuides } as SetupGuideManifest;
    }
    return null;
  }, [guideManifest, guideError]);

  const guideSections = useMemo(() => {
    if (!resolvedManifest) return [] as GuideSectionView[];
    const guidesById = new Map(resolvedManifest.guides.map((guide) => [guide.id, guide]));

    if (resolvedManifest.sections.length > 0) {
      return resolvedManifest.sections
        .slice()
        .sort((a, b) => a.order - b.order)
        .map((section) => ({
          id: section.id,
          title: section.title,
          description: section.description,
          guides: section.guideIds
            .map((guideId) => guidesById.get(guideId))
            .filter((guide): guide is SetupGuideDefinition => Boolean(guide)),
        }))
        .filter((section) => section.guides.length > 0);
    }

    return [
      {
        id: 'all',
        title: 'Helpful guides',
        guides: resolvedManifest.guides.slice().sort((a, b) => a.order - b.order),
      },
    ];
  }, [resolvedManifest]);

  const resolveGuideCover = (guide: SetupGuideDefinition) => {
    if (!guide.cover) return undefined;
    if (guide.cover.startsWith('http://') || guide.cover.startsWith('https://') || guide.cover.startsWith('/')) {
      return guide.cover;
    }
    return `/content/setup-guides/${guide.slug}/${guide.cover}`;
  };

  const openWizardForStep = (stepId: string) => {
    setSelectedStepId(stepId);
    setWizardStepIndex(0);
    setWizardOpen(true);
  };

  const nodes = setupNodes.map((node) => {
    const step = stepsWithStatus.find((item) => item.id === node.id);
    return {
      ...node,
      data: {
        ...node.data,
        status: step?.status ?? 'todo',
        description: step?.description ?? node.data.description,
        bannerUrl: step?.bannerUrl ?? node.data.bannerUrl,
        onStart: () => openWizardForStep(node.id),
        onComplete: () => openWizardForStep(node.id),
      },
    } as Node<SetupNodeData>;
  });

  const edges = stepsWithStatus.slice(1).map((step, index) => {
    const previous = stepsWithStatus[index];
    const isComplete = previous.status === 'complete' && step.status === 'complete';
    return {
      id: `${previous.id}-${step.id}`,
      source: previous.id,
      target: step.id,
      type: 'straight',
      animated: true,
      style: isComplete
        ? { stroke: 'var(--color-brand-primary)', strokeWidth: 2 }
        : { stroke: 'var(--color-border)', strokeDasharray: '6 6', strokeWidth: 2 },
    } as Edge;
  });

  const selectedStep = stepsWithStatus.find((step) => step.id === selectedStepId) ?? stepsWithStatus[0];

  const persistList = (key: string, values: string[]) => {
    localStorage.setItem(key, JSON.stringify(values));
  };

  const markComplete = () => {
    if (!selectedStep) return;
    const updated = Array.from(new Set([...completedSteps, selectedStep.id]));
    setCompletedSteps(updated);
    persistList(onboardingDoneKey, updated);
  };

  const markSkipped = () => {
    if (!selectedStep || selectedStep.required) return;
    const updated = Array.from(new Set([...skippedSteps, selectedStep.id]));
    setSkippedSteps(updated);
    persistList(onboardingSkippedKey, updated);
  };

  const resetOnboarding = () => {
    setCompletedSteps([]);
    setSkippedSteps([]);
    localStorage.removeItem(onboardingDoneKey);
    localStorage.removeItem(onboardingSkippedKey);
    localStorage.removeItem(onboardingSkipKey);
    localStorage.removeItem(onboardingCompleteKey);
  };

  const handleSkip = () => {
    localStorage.setItem(onboardingSkipKey, 'true');
    onSkip?.();
  };

  const handleComplete = () => {
    localStorage.setItem(onboardingCompleteKey, 'true');
    onComplete?.();
  };

  return (
    <div className="flex-1 overflow-auto bg-[var(--color-surface-inset)]">
      <div className="px-6 py-6">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
          <div className="space-y-2 flex-1 min-w-0">
            <p className="text-xs font-semibold uppercase tracking-[0.2em] text-[var(--color-brand-primary)]">Tenant Setup Journey</p>
            <h1 className="text-3xl font-bold text-[var(--color-text-primary)]">Launch your finance stack</h1>
            <p className="w-full max-w-none text-sm text-[var(--color-text-secondary)]">
              Each node represents a platform capability. Complete the required path to go live, or skip optional upgrades and return later.
            </p>
          </div>
          <div className="flex flex-wrap gap-2 shrink-0">
            <Button variant="outline" size="sm" onClick={resetOnboarding}>Reset</Button>
            <Button variant="ghost" size="sm" onClick={handleSkip}>Skip for now</Button>
            <Button size="sm" onClick={handleComplete} disabled={!isReadyToLaunch}>
              Mark live-ready
            </Button>
          </div>
        </div>

        <div className={cn('mt-6 grid grid-cols-1 gap-6', isMobile ? 'grid-cols-1' : 'xl:grid-cols-[minmax(0,1fr)_320px]')}>
          <Card className="overflow-hidden">
            <CardContent className="h-[360px] p-0 bg-[radial-gradient(circle_at_top,_rgba(0,0,0,0.04),_transparent_55%)]">
              {isMobile ? (
                <div className="space-y-4">
                  {stepsWithStatus.map((step) => (
                    <button
                      key={step.id}
                      type="button"
                      onClick={() => setSelectedStepId(step.id)}
                      className="w-full text-left"
                    >
                      <div className="rounded-2xl border border-[var(--color-border-light)] bg-[var(--color-surface)] overflow-hidden">
                        <div
                          className="h-20 bg-cover bg-center"
                          style={{ backgroundImage: `url(${step.bannerUrl})` }}
                        />
                        <div className="px-4 py-4 space-y-2">
                          <div className="flex items-center gap-2">
                            <StatusPill status={step.status} />
                            <span className="text-xs text-[var(--color-text-tertiary)]">{step.category}</span>
                          </div>
                          <p className="text-base font-semibold text-[var(--color-text-primary)]">{step.title}</p>
                          <p className="text-sm text-[var(--color-text-secondary)]">{step.description}</p>
                          <div className="flex items-center gap-2">
                            <Button size="sm" variant="secondary" onClick={() => openWizardForStep(step.id)}>
                              Start
                            </Button>
                            <Button size="sm" variant="ghost" onClick={() => openWizardForStep(step.id)}>
                              Mark complete
                            </Button>
                          </div>
                        </div>
                      </div>
                    </button>
                  ))}
                </div>
              ) : (
                <div ref={flowContainerRef} className="h-full w-full overflow-x-auto overflow-y-hidden">
                  <div style={{ minWidth: `${flowMinWidth}px`, height: '100%' }}>
                    <ReactFlow
                      nodes={nodes}
                      edges={edges}
                      nodeTypes={{ setupNode: SetupNode }}
                      fitView
                      fitViewOptions={{ padding: 0.2 }}
                      minZoom={0.6}
                      maxZoom={1.2}
                      onInit={setFlowInstance}
                      onNodeClick={(_, node) => setSelectedStepId(node.id)}
                      panOnScroll={false}
                      panOnDrag={false}
                      selectionOnDrag={false}
                      nodesDraggable={false}
                      nodesConnectable={false}
                      zoomOnScroll={false}
                      zoomOnPinch={false}
                      zoomOnDoubleClick={false}
                      preventScrolling={false}
                    >
                      <Background color="var(--color-border-light)" gap={28} />
                    </ReactFlow>
                  </div>
                </div>
              )}
            </CardContent>
          </Card>

          <Card className={cn(isMobile && 'hidden')}>
            <CardHeader>
              <CardTitle className="text-base">Step details</CardTitle>
              <CardDescription>Guide your team through the next milestone.</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              {selectedStep && (
                <div className="space-y-3">
                  <div className="flex items-center gap-2">
                    <StatusPill status={selectedStep.status} />
                    <span className="text-xs text-[var(--color-text-tertiary)]">
                      {selectedStep.required ? 'Required' : 'Optional'}
                    </span>
                  </div>
                  <h3 className="text-lg font-semibold text-[var(--color-text-primary)]">{selectedStep.title}</h3>
                  <p className="text-sm text-[var(--color-text-secondary)]">{selectedStep.description}</p>
                  <div className="rounded-lg border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] p-3 text-xs text-[var(--color-text-secondary)]">
                    <p className="font-semibold text-[var(--color-text-primary)]">Dependencies</p>
                    <ul className="mt-2 space-y-1">
                      {(selectedStep.dependsOn ?? []).length === 0 ? (
                        <li>None</li>
                      ) : (
                        selectedStep.dependsOn?.map((dep) => {
                          const match = stepsWithStatus.find((step) => step.id === dep);
                          return (
                            <li key={dep} className="flex items-center gap-2">
                              {match?.status === 'complete' ? (
                                <CheckCircle2 className="h-3.5 w-3.5 text-[var(--color-success)]" />
                              ) : (
                                <Circle className="h-3.5 w-3.5 text-[var(--color-text-tertiary)]" />
                              )}
                              <span>{match?.title ?? dep}</span>
                            </li>
                          );
                        })
                      )}
                    </ul>
                  </div>
                  <div className="flex flex-col gap-2">
                    {selectedStep.href && (
                      <Button variant="secondary" size="sm" onClick={() => (window.location.href = selectedStep.href!)}>
                        Go to step
                        <ArrowUpRight className="ml-2 h-4 w-4" />
                      </Button>
                    )}
                    <Button size="sm" onClick={markComplete} disabled={selectedStep.status === 'complete'}>
                      Mark complete
                      <ArrowRight className="ml-2 h-4 w-4" />
                    </Button>
                    {!selectedStep.required && selectedStep.status !== 'skipped' && (
                      <Button variant="ghost" size="sm" onClick={markSkipped}>
                        Skip this step
                      </Button>
                    )}
                  </div>
                </div>
              )}
            </CardContent>
          </Card>
        </div>

        {/* Guide Articles Section */}
        <div className="mt-10">
          <div className="mb-6">
            <h2 className="text-xl font-bold text-[var(--color-text-primary)]">Helpful guides</h2>
            <p className="mt-1 text-sm text-[var(--color-text-secondary)]">
              Explore detailed documentation to help you get the most out of the platform.
            </p>
            {guideError && (
              <p className="mt-2 text-xs text-[var(--color-text-tertiary)]">{guideError}</p>
            )}
          </div>
          {guideLoading && !resolvedManifest ? (
            <div className="flex items-center gap-3 text-sm text-[var(--color-text-secondary)]">
              <div className="h-4 w-4 border-2 border-[var(--color-brand-primary)] border-t-transparent rounded-full animate-spin" />
              Loading guides...
            </div>
          ) : (
            <div className="space-y-6">
              {guideSections.map((section) => (
                <div key={section.id} className="space-y-3">
                  {section.id !== 'all' && (
                    <div>
                      <h3 className="text-lg font-semibold text-[var(--color-text-primary)]">{section.title}</h3>
                      {section.description && (
                        <p className="text-sm text-[var(--color-text-secondary)]">{section.description}</p>
                      )}
                    </div>
                  )}
                  <div className="grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-3">
                    {section.guides.map((guide) => {
                      const coverUrl = resolveGuideCover(guide);
                      return (
                        <Card key={guide.id} className="overflow-hidden transition-shadow hover:shadow-md">
                          <div
                            className={cn('h-24 bg-cover bg-center', !coverUrl && 'bg-gradient-to-br', guide.accent ?? 'from-slate-200/60 to-slate-100')}
                            style={coverUrl ? { backgroundImage: `url(${coverUrl})` } : undefined}
                          />
                          <CardContent className="p-4">
                            <div className="space-y-3">
                              <span className="inline-block rounded-full bg-[var(--color-surface-inset)] px-2.5 py-1 text-[10px] font-semibold uppercase tracking-wide text-[var(--color-text-tertiary)]">
                                {guide.category}
                              </span>
                              <h3 className="text-sm font-semibold text-[var(--color-text-primary)] line-clamp-2">
                                {guide.title}
                              </h3>
                              <p className="text-xs text-[var(--color-text-secondary)] leading-relaxed line-clamp-3">
                                {guide.description}
                              </p>
                              <Button
                                variant="ghost"
                                size="sm"
                                className="h-8 px-0 text-xs font-semibold text-[var(--color-brand-primary)] hover:text-[var(--color-brand-primary)] hover:bg-transparent"
                                onClick={() => navigate(`/setup-guides/${guide.slug}`)}
                              >
                                Read article
                                <ExternalLink className="ml-1.5 h-3.5 w-3.5" />
                              </Button>
                            </div>
                          </CardContent>
                        </Card>
                      );
                    })}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
      {wizardOpen && selectedStep && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
          <div className="w-full max-w-2xl rounded-2xl border border-[var(--color-border)] bg-[var(--color-surface)] shadow-xl">
            <div
              className="h-32 rounded-t-2xl bg-cover bg-center"
              style={{
                backgroundImage: selectedStep.bannerUrl ? `url(${selectedStep.bannerUrl})` : undefined,
              }}
            />
            <div className="flex items-center justify-between border-b border-[var(--color-border-light)] px-6 py-4">
              <div>
                <p className="text-xs font-semibold uppercase tracking-[0.2em] text-[var(--color-text-tertiary)]">Setup wizard</p>
                <h3 className="text-lg font-semibold text-[var(--color-text-primary)]">{selectedStep.title}</h3>
              </div>
              <button
                type="button"
                onClick={() => setWizardOpen(false)}
                className="text-sm text-[var(--color-text-secondary)] hover:text-[var(--color-text-primary)]"
              >
                Close
              </button>
            </div>
            <div className="px-6 py-6">
              {wizardStepIndex === 0 ? (
                <div className="space-y-5">
                  <p className="text-sm text-[var(--color-text-secondary)]">
                    {selectedStep.description}
                  </p>
                  <div>
                    <label className="block text-xs font-semibold text-[var(--color-text-tertiary)]">Owner</label>
                    <input
                      type="text"
                      placeholder="Assign a team lead"
                      className="mt-2 w-full rounded-md border border-[var(--color-border)] bg-transparent px-4 py-3 text-sm"
                    />
                  </div>
                  <div>
                    <label className="block text-xs font-semibold text-[var(--color-text-tertiary)]">Notes</label>
                    <textarea
                      rows={4}
                      placeholder="Add any context for this step"
                      className="mt-2 w-full rounded-md border border-[var(--color-border)] bg-transparent px-4 py-3 text-sm"
                    />
                  </div>
                </div>
              ) : (
                <div className="space-y-5">
                  <p className="text-sm text-[var(--color-text-secondary)]">
                    Review the setup details and confirm completion for this step.
                  </p>
                  <div className="rounded-lg border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] p-5 text-sm">
                    <p className="font-semibold text-[var(--color-text-primary)]">Ready to mark complete?</p>
                    <p className="mt-2 text-[var(--color-text-secondary)]">
                      This will unlock the next step in your launch path.
                    </p>
                  </div>
                </div>
              )}
            </div>
            <div className="flex items-center justify-between border-t border-[var(--color-border-light)] px-6 py-4">
              <button
                type="button"
                onClick={() => setWizardStepIndex((prev) => Math.max(prev - 1, 0))}
                className="text-sm text-[var(--color-text-secondary)] hover:text-[var(--color-text-primary)]"
                disabled={wizardStepIndex === 0}
              >
                Back
              </button>
              <div className="flex items-center gap-2">
                <Button variant="ghost" size="sm" onClick={() => setWizardOpen(false)}>
                  Cancel
                </Button>
                {wizardStepIndex === 0 ? (
                  <Button size="sm" onClick={() => setWizardStepIndex(1)}>
                    Continue
                  </Button>
                ) : (
                  <Button
                    size="sm"
                    onClick={() => {
                      markComplete();
                      setWizardOpen(false);
                    }}
                  >
                    Mark complete
                  </Button>
                )}
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

function StatusPill({ status }: { status: StepStatus }) {
  const mapping: Record<StepStatus, { label: string; className: string }> = {
    todo: { label: 'Not started', className: 'bg-[var(--color-surface-inset)] text-[var(--color-text-secondary)]' },
    'in-progress': { label: 'In progress', className: 'bg-[var(--color-info-light)] text-[var(--color-info)]' },
    blocked: { label: 'Blocked', className: 'bg-[var(--color-warning-light)] text-[var(--color-warning)]' },
    complete: { label: 'Complete', className: 'bg-[var(--color-success-light)] text-[var(--color-success)]' },
    skipped: { label: 'Skipped', className: 'bg-[var(--color-surface-inset)] text-[var(--color-text-tertiary)]' },
  };

  const { label, className } = mapping[status];
  return <span className={cn('rounded-full px-2 py-1 text-[11px] font-semibold uppercase', className)}>{label}</span>;
}
