import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Building2,
  Globe,
  Layers3,
  Mail,
  CheckCircle2,
  Upload,
  Info,
  ArrowRight,
  ArrowLeft,
  Check,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { cn } from '@/lib/utils';
import { tenantService } from '@/services/tenantService';
import { tenantFeatureService } from '@/services/tenantFeatureService';
import { catalogService } from '@/services/catalogService';
import { identityService } from '@/services/identityService';
import type { CatalogCountryItem, CatalogCurrencyItem, Tenant, TenantFeatureItemResponse } from '@/types';

interface TenantSetupWizardPageProps {
  onComplete?: () => void;
}

// Industry options
const industries = [
  { value: 'financial_services', label: 'Financial Services' },
  { value: 'banking', label: 'Banking' },
  { value: 'fintech', label: 'Fintech' },
  { value: 'insurance', label: 'Insurance' },
  { value: 'money_transfer', label: 'Money Transfer / Remittance' },
  { value: 'payments', label: 'Payments' },
  { value: 'technology', label: 'Technology' },
  { value: 'telecommunications', label: 'Telecommunications' },
  { value: 'retail', label: 'Retail / E-commerce' },
  { value: 'healthcare', label: 'Healthcare' },
  { value: 'education', label: 'Education' },
  { value: 'government', label: 'Government' },
  { value: 'non_profit', label: 'Non-profit' },
  { value: 'other', label: 'Other' },
];

// Company size options
const companySizes = [
  { value: '1-10', label: '1-10 employees' },
  { value: '11-50', label: '11-50 employees' },
  { value: '51-200', label: '51-200 employees' },
  { value: '201-500', label: '201-500 employees' },
  { value: '501-1000', label: '501-1000 employees' },
  { value: '1001+', label: '1001+ employees' },
];

// Feature groups matching the ones in SetupJourneyPage
const featureGroups = [
  {
    id: 'bill-payments',
    label: 'Bill Payments',
    description: 'Enable bill payment processing, biller catalog, and invoice management.',
    icon: Building2,
    flags: [
      { key: 'BillPayments.Invoicing.Create', label: 'Create invoices' },
      { key: 'BillPayments.Invoicing.Issue', label: 'Issue invoices' },
      { key: 'BillPayments.Invoicing.Payment', label: 'Collect invoice payments' },
      { key: 'BillPayments.BillerCatalog.Browse', label: 'Browse biller catalog' },
      { key: 'BillPayments.BillerCatalog.Services', label: 'Access biller services' },
      { key: 'BillPayments.BillPaymentOrders.Create', label: 'Create bill payment orders' },
      { key: 'BillPayments.BillPaymentOrders.Submit', label: 'Submit bill payment orders' },
      { key: 'BillPayments.BillPaymentOrders.History', label: 'View bill payment history' },
    ],
  },
  {
    id: 'money-transfer',
    label: 'Money Transfer',
    description: 'Enable cross-border payments, FX, and payout capabilities.',
    icon: Globe,
    flags: [
      { key: 'MoneyTransfer.PaymentIntents.Create', label: 'Create payment intents' },
      { key: 'MoneyTransfer.PaymentIntents.Capture', label: 'Capture payment intents' },
      { key: 'MoneyTransfer.Payouts.Create', label: 'Create payouts' },
      { key: 'MoneyTransfer.Payouts.Tracking', label: 'Track payouts' },
      { key: 'MoneyTransfer.FX.RateQuotes', label: 'FX rate quotes' },
      { key: 'MoneyTransfer.FX.CurrencyConversion', label: 'FX currency conversion' },
      { key: 'MoneyTransfer.Partners.Management', label: 'Manage partners' },
      { key: 'MoneyTransfer.Partners.Routing', label: 'Route payments' },
      { key: 'MoneyTransfer.Limits.TransactionLimits', label: 'Transaction limits' },
    ],
  },
  {
    id: 'personal-finance',
    label: 'Personal Finance',
    description: 'Enable budgeting, goals, and subscription tracking features.',
    icon: Layers3,
    flags: [
      { key: 'PersonalFinance.Budgets.Create', label: 'Create budgets' },
      { key: 'PersonalFinance.Budgets.Tracking', label: 'Track budgets' },
      { key: 'PersonalFinance.Goals.Create', label: 'Create goals' },
      { key: 'PersonalFinance.Goals.Tracking', label: 'Track goals' },
      { key: 'PersonalFinance.Subscriptions.Detection', label: 'Detect subscriptions' },
      { key: 'PersonalFinance.Subscriptions.Tracking', label: 'Track subscriptions' },
      { key: 'PersonalFinance.Bills.Reminders', label: 'Bill reminders' },
      { key: 'PersonalFinance.Bills.AutoPay', label: 'Bill autopay' },
    ],
  },
  {
    id: 'ai',
    label: 'AI & Intelligence',
    description: 'Enable AI-powered insights, agents, and automation.',
    icon: Layers3,
    flags: [
      { key: 'AI.Platform.MultiProvider', label: 'Multi-provider routing' },
      { key: 'AI.Platform.ModelSelection', label: 'Model selection' },
      { key: 'AI.Platform.RunTracking', label: 'Run tracking' },
      { key: 'AI.Prompts.Versioning', label: 'Prompt versioning' },
      { key: 'AI.Tools.DomainTools', label: 'Domain tools' },
      { key: 'AI.Insights.General', label: 'AI insights' },
      { key: 'AI.Agents.DomainAgents', label: 'Domain agents' },
      { key: 'AI.Proposals.ApprovalWorkflow', label: 'Proposal approvals' },
    ],
  },
];

// Step definitions
const steps = [
  {
    id: 1,
    title: 'Company Setup',
    description: 'Tell us about your organization. We\'ll use this to customize templates and ensure compliance with your industry regulations.',
    icon: Building2,
  },
  {
    id: 2,
    title: 'Regional Settings',
    description: 'Configure your base country, currency, and company details for accurate localization.',
    icon: Globe,
  },
  {
    id: 3,
    title: 'Features',
    description: 'Select the features and capabilities you want to enable for your organization.',
    icon: Layers3,
  },
  {
    id: 4,
    title: 'Contact Details',
    description: 'Provide contact information and company address for billing and communication.',
    icon: Mail,
  },
  {
    id: 5,
    title: 'Summary',
    description: 'Review your setup and confirm everything looks correct before completing.',
    icon: CheckCircle2,
  },
];

// Form state interface
interface FormData {
  // Step 1
  companyName: string;
  logoUrl: string | null;
  industry: string;
  // Step 2
  baseCountry: string;
  baseCurrency: string;
  companySize: string;
  website: string;
  // Step 3
  enabledFeatures: Record<string, boolean>;
  // Step 4
  contactEmail: string;
  contactMobile: string;
  addressLine1: string;
  addressLine2: string;
  city: string;
  stateProvince: string;
  postalCode: string;
  country: string;
}

export function TenantSetupWizardPage({ onComplete }: TenantSetupWizardPageProps) {
  const navigate = useNavigate();
  const [currentStep, setCurrentStep] = useState(1);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [currentTenantId, setCurrentTenantId] = useState<string>('');
  const [currentTenant, setCurrentTenant] = useState<Tenant | null>(null);
  const [countries, setCountries] = useState<CatalogCountryItem[]>([]);
  const [currencies, setCurrencies] = useState<CatalogCurrencyItem[]>([]);
  const [userName, setUserName] = useState<string>('');

  const [formData, setFormData] = useState<FormData>({
    companyName: '',
    logoUrl: null,
    industry: '',
    baseCountry: '',
    baseCurrency: '',
    companySize: '',
    website: '',
    enabledFeatures: {},
    contactEmail: '',
    contactMobile: '',
    addressLine1: '',
    addressLine2: '',
    city: '',
    stateProvince: '',
    postalCode: '',
    country: '',
  });

  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

  // Load initial data
  useEffect(() => {
    const loadData = async () => {
      setLoading(true);
      setError(null);
      try {
        const [currentUser, countriesResponse, currenciesResponse] = await Promise.all([
          identityService.getCurrentUser(),
          catalogService.getTenantCountries(),
          catalogService.getTenantCurrencies(),
        ]);

        setCurrentTenantId(currentUser.tenantId);
        setUserName(currentUser.displayName ?? currentUser.email ?? 'User');
        setCountries(countriesResponse.countries ?? []);
        setCurrencies(currenciesResponse.currencies ?? []);

        const tenant = await tenantService.get(currentUser.tenantId);
        setCurrentTenant(tenant);

        // Set initial step from tenant's setup progress
        if (tenant.setupStep > 0 && tenant.setupStep <= 5) {
          setCurrentStep(tenant.setupStep);
        }

        // Load existing tenant features
        let existingFeatures: Record<string, boolean> = {};
        try {
          const featureResponse = await tenantFeatureService.get(currentUser.tenantId);
          if (featureResponse.features.length > 0) {
            featureResponse.features.forEach((feature: TenantFeatureItemResponse) => {
              existingFeatures[feature.featureName] = feature.isEnabled;
            });
          } else {
            // Default all features to enabled
            featureGroups.forEach((group) => {
              group.flags.forEach((flag) => {
                existingFeatures[flag.key] = true;
              });
            });
          }
        } catch {
          // Default all features to enabled
          featureGroups.forEach((group) => {
            group.flags.forEach((flag) => {
              existingFeatures[flag.key] = true;
            });
          });
        }

        // Populate form with existing data
        setFormData({
          companyName: tenant.name ?? '',
          logoUrl: tenant.logoUrl ?? null,
          industry: tenant.industry ?? '',
          baseCountry: tenant.supportedCountries?.[0] ?? '',
          baseCurrency: tenant.defaultCurrency ?? '',
          companySize: tenant.companySize ?? '',
          website: tenant.website ?? '',
          enabledFeatures: existingFeatures,
          contactEmail: tenant.contactEmail ?? currentUser.email ?? '',
          contactMobile: tenant.contactMobile ?? '',
          addressLine1: tenant.addressLine1 ?? '',
          addressLine2: tenant.addressLine2 ?? '',
          city: tenant.city ?? '',
          stateProvince: tenant.stateProvince ?? '',
          postalCode: tenant.postalCode ?? '',
          country: tenant.country ?? tenant.supportedCountries?.[0] ?? '',
        });
      } catch (err) {
        setError('Unable to load tenant data. Please refresh and try again.');
      } finally {
        setLoading(false);
      }
    };

    loadData();
  }, []);

  // Validation functions for each step
  const validateStep = useCallback((step: number): boolean => {
    const errors: Record<string, string> = {};

    switch (step) {
      case 1:
        if (!formData.companyName.trim()) {
          errors.companyName = 'Company name is required';
        }
        if (!formData.industry) {
          errors.industry = 'Industry is required';
        }
        break;
      case 2:
        if (!formData.baseCountry) {
          errors.baseCountry = 'Base country is required';
        }
        if (!formData.baseCurrency) {
          errors.baseCurrency = 'Base currency is required';
        }
        break;
      case 3:
        // Features are optional, no validation needed
        break;
      case 4:
        if (!formData.contactEmail.trim()) {
          errors.contactEmail = 'Contact email is required';
        } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(formData.contactEmail)) {
          errors.contactEmail = 'Please enter a valid email address';
        }
        break;
      case 5:
        // Summary step - no validation
        break;
    }

    setFieldErrors(errors);
    return Object.keys(errors).length === 0;
  }, [formData]);

  // Save current step progress
  const saveStepProgress = useCallback(async (step: number) => {
    if (!currentTenantId) return;
    setSaving(true);
    setError(null);

    try {
      // Build update request based on current step
      const updateRequest: Record<string, unknown> = {
        setupStep: step + 1, // Save the next step as current progress
      };

      switch (step) {
        case 1:
          updateRequest.name = formData.companyName;
          updateRequest.logoUrl = formData.logoUrl;
          updateRequest.industry = formData.industry;
          break;
        case 2:
          updateRequest.supportedCountries = formData.baseCountry ? [formData.baseCountry] : [];
          updateRequest.defaultCurrency = formData.baseCurrency;
          updateRequest.companySize = formData.companySize;
          updateRequest.website = formData.website;
          break;
        case 3:
          // Save features separately
          const features = Object.entries(formData.enabledFeatures).map(([featureName, isEnabled]) => ({
            featureName,
            isEnabled,
          }));
          await tenantFeatureService.update(currentTenantId, { features });
          break;
        case 4:
          updateRequest.contactEmail = formData.contactEmail;
          updateRequest.contactMobile = formData.contactMobile;
          updateRequest.addressLine1 = formData.addressLine1;
          updateRequest.addressLine2 = formData.addressLine2;
          updateRequest.city = formData.city;
          updateRequest.stateProvince = formData.stateProvince;
          updateRequest.postalCode = formData.postalCode;
          updateRequest.country = formData.country;
          break;
        case 5:
          // Mark setup as complete
          updateRequest.isSetupComplete = true;
          break;
      }

      if (Object.keys(updateRequest).length > 1 || step === 5) {
        await tenantService.update(currentTenantId, updateRequest);
      }
    } catch (err) {
      setError('Failed to save progress. Please try again.');
      throw err;
    } finally {
      setSaving(false);
    }
  }, [currentTenantId, formData]);

  // Handle continue button
  const handleContinue = async () => {
    if (!validateStep(currentStep)) return;

    try {
      await saveStepProgress(currentStep);
      
      if (currentStep < 5) {
        setCurrentStep(currentStep + 1);
      } else {
        // Complete setup
        onComplete?.();
        navigate('/');
      }
    } catch {
      // Error already handled in saveStepProgress
    }
  };

  // Handle back button
  const handleBack = () => {
    if (currentStep > 1) {
      setCurrentStep(currentStep - 1);
    }
  };

  // Handle skip for now
  const handleSkip = async () => {
    try {
      setSaving(true);
      setError(null);
      
      // Save current progress without marking setup as complete
      if (currentTenantId) {
        await tenantService.update(currentTenantId, {
          setupStep: currentStep,
        });
      }
      
      // Navigate to dashboard
      onComplete?.();
      navigate('/');
    } catch (err) {
      setError('Failed to save progress. Please try again.');
    } finally {
      setSaving(false);
    }
  };

  // Update form data
  const updateFormData = (field: keyof FormData, value: unknown) => {
    setFormData((prev) => ({ ...prev, [field]: value }));
    if (fieldErrors[field]) {
      setFieldErrors((prev) => {
        const { [field]: _, ...rest } = prev;
        return rest;
      });
    }
  };

  // Toggle feature
  const toggleFeature = (featureKey: string) => {
    setFormData((prev) => ({
      ...prev,
      enabledFeatures: {
        ...prev.enabledFeatures,
        [featureKey]: !prev.enabledFeatures[featureKey],
      },
    }));
  };

  // Toggle all features in a group
  const toggleFeatureGroup = (groupId: string) => {
    const group = featureGroups.find((g) => g.id === groupId);
    if (!group) return;

    const allEnabled = group.flags.every((flag) => formData.enabledFeatures[flag.key]);
    const updates: Record<string, boolean> = {};
    group.flags.forEach((flag) => {
      updates[flag.key] = !allEnabled;
    });

    setFormData((prev) => ({
      ...prev,
      enabledFeatures: {
        ...prev.enabledFeatures,
        ...updates,
      },
    }));
  };

  // Calculate progress percentage
  const progressPercentage = Math.round((currentStep / 5) * 100);

  // Get enabled features count
  const enabledFeaturesCount = Object.values(formData.enabledFeatures).filter(Boolean).length;
  const totalFeaturesCount = featureGroups.reduce((sum, group) => sum + group.flags.length, 0);

  if (loading) {
    return (
      <div className="fixed inset-0 flex items-center justify-center bg-[var(--color-background)]">
        <div className="flex flex-col items-center gap-4">
          <div className="h-8 w-8 border-4 border-[var(--color-brand-primary)] border-t-transparent rounded-full animate-spin" />
          <p className="text-sm text-[var(--color-text-secondary)]">Loading...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="fixed inset-0 flex bg-[var(--color-background)]">
      {/* Left Panel - Form */}
      <div className="flex-1 overflow-y-auto min-w-0">
        <div className="max-w-3xl mx-auto px-10 py-10 w-full">
          {/* Logo */}
          <div className="flex items-center gap-2 mb-12">
            <div className="w-8 h-8 rounded bg-[var(--color-brand-primary)] flex items-center justify-center">
              <span className="text-white font-bold text-sm">A</span>
            </div>
            <span className="font-semibold text-[var(--color-text-primary)]">Aonik</span>
          </div>

          {/* Welcome message */}
          <div className="mb-8 max-w-[36rem]">
            <h1 className="text-2xl font-bold text-[var(--color-text-primary)] mb-2">
              Welcome, {userName.split(' ')[0]}
            </h1>
            <p className="text-[var(--color-text-secondary)] whitespace-normal max-w-none">
              Let's get your workspace set up in a few simple steps
            </p>
          </div>

          {/* Progress */}
          <div className="mb-8">
            <div className="flex items-center justify-between text-sm mb-2">
              <span className="text-[var(--color-text-secondary)]">Step {currentStep} of 5</span>
              <span className="text-[var(--color-text-tertiary)]">{progressPercentage}% complete</span>
            </div>
            <div className="h-1.5 bg-[var(--color-surface-inset)] rounded-full overflow-hidden">
              <div
                className="h-full bg-[var(--color-brand-primary)] rounded-full transition-all duration-500"
                style={{ width: `${progressPercentage}%` }}
              />
            </div>
          </div>

          {/* Form content based on current step */}
          <div className="space-y-6">
            {currentStep === 1 && (
              <>
                {/* Company Name */}
                <div className="space-y-2">
                  <Label htmlFor="companyName" className="text-sm font-medium">
                    Company Name <span className="text-[var(--color-error)]">*</span>
                  </Label>
                  <Input
                    id="companyName"
                    placeholder="Acme Corporation"
                    value={formData.companyName}
                    onChange={(e) => updateFormData('companyName', e.target.value)}
                    className={cn(fieldErrors.companyName && 'border-[var(--color-error)]')}
                  />
                  {fieldErrors.companyName && (
                    <p className="text-xs text-[var(--color-error)]">{fieldErrors.companyName}</p>
                  )}
                </div>

                {/* Organization Logo */}
                <div className="space-y-2">
                  <Label className="text-sm font-medium">Organization Logo (Optional)</Label>
                  <div className="border-2 border-dashed border-[var(--color-border)] rounded-lg p-8 text-center hover:border-[var(--color-brand-primary)] transition-colors cursor-pointer">
                    <Upload className="h-8 w-8 mx-auto text-[var(--color-text-tertiary)] mb-3" />
                    <p className="text-sm text-[var(--color-text-secondary)] mb-1">
                      Click to upload or drag and drop
                    </p>
                    <p className="text-xs text-[var(--color-text-tertiary)]">
                      PNG, JPG, SVG (max 5MB)
                    </p>
                  </div>
                </div>

                {/* Industry */}
                <div className="space-y-2">
                  <Label htmlFor="industry" className="text-sm font-medium">
                    Industry <span className="text-[var(--color-error)]">*</span>
                  </Label>
                  <Select value={formData.industry} onValueChange={(value) => updateFormData('industry', value)}>
                    <SelectTrigger className={cn(fieldErrors.industry && 'border-[var(--color-error)]')}>
                      <SelectValue placeholder="Select your industry" />
                    </SelectTrigger>
                    <SelectContent>
                      {industries.map((industry) => (
                        <SelectItem key={industry.value} value={industry.value}>
                          {industry.label}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  {fieldErrors.industry && (
                    <p className="text-xs text-[var(--color-error)]">{fieldErrors.industry}</p>
                  )}
                </div>
              </>
            )}

            {currentStep === 2 && (
              <>
                {/* Base Country */}
                <div className="space-y-2">
                  <Label htmlFor="baseCountry" className="text-sm font-medium">
                    Base Country <span className="text-[var(--color-error)]">*</span>
                  </Label>
                  <Select value={formData.baseCountry} onValueChange={(value) => updateFormData('baseCountry', value)}>
                    <SelectTrigger className={cn(fieldErrors.baseCountry && 'border-[var(--color-error)]')}>
                      <SelectValue placeholder="Select your base country" />
                    </SelectTrigger>
                    <SelectContent>
                      {countries.map((country) => (
                        <SelectItem key={country.countryCode} value={country.countryCode}>
                          {country.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  {fieldErrors.baseCountry && (
                    <p className="text-xs text-[var(--color-error)]">{fieldErrors.baseCountry}</p>
                  )}
                </div>

                {/* Base Currency */}
                <div className="space-y-2">
                  <Label htmlFor="baseCurrency" className="text-sm font-medium">
                    Base Currency <span className="text-[var(--color-error)]">*</span>
                  </Label>
                  <Select value={formData.baseCurrency} onValueChange={(value) => updateFormData('baseCurrency', value)}>
                    <SelectTrigger className={cn(fieldErrors.baseCurrency && 'border-[var(--color-error)]')}>
                      <SelectValue placeholder="Select your base currency" />
                    </SelectTrigger>
                    <SelectContent>
                      {currencies.map((currency) => (
                        <SelectItem key={currency.code} value={currency.code}>
                          {currency.code} - {currency.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  {fieldErrors.baseCurrency && (
                    <p className="text-xs text-[var(--color-error)]">{fieldErrors.baseCurrency}</p>
                  )}
                </div>

                {/* Company Size */}
                <div className="space-y-2">
                  <Label htmlFor="companySize" className="text-sm font-medium">
                    Company Size
                  </Label>
                  <Select value={formData.companySize} onValueChange={(value) => updateFormData('companySize', value)}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select company size" />
                    </SelectTrigger>
                    <SelectContent>
                      {companySizes.map((size) => (
                        <SelectItem key={size.value} value={size.value}>
                          {size.label}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>

                {/* Company Website */}
                <div className="space-y-2">
                  <Label htmlFor="website" className="text-sm font-medium">
                    Company Website
                  </Label>
                  <Input
                    id="website"
                    placeholder="https://example.com"
                    value={formData.website}
                    onChange={(e) => updateFormData('website', e.target.value)}
                  />
                </div>
              </>
            )}

            {currentStep === 3 && (
              <>
                <p className="text-sm text-[var(--color-text-secondary)] mb-6">
                  Select the features you want to enable for your organization. You can change these later in settings.
                </p>

                <div className="space-y-4">
                  {featureGroups.map((group) => {
                    const Icon = group.icon;
                    const enabledCount = group.flags.filter((f) => formData.enabledFeatures[f.key]).length;
                    const isAllEnabled = enabledCount === group.flags.length;
                    const isPartialEnabled = enabledCount > 0 && !isAllEnabled;

                    return (
                      <div
                        key={group.id}
                        className={cn(
                          'rounded-lg border p-4 cursor-pointer transition-all',
                          isAllEnabled
                            ? 'border-[var(--color-brand-primary)] bg-[var(--color-brand-primary-light)]'
                            : isPartialEnabled
                            ? 'border-[var(--color-brand-primary)]/50'
                            : 'border-[var(--color-border)] hover:border-[var(--color-border-dark)]'
                        )}
                        onClick={() => toggleFeatureGroup(group.id)}
                      >
                        <div className="flex items-start gap-4">
                          <div
                            className={cn(
                              'w-10 h-10 rounded-lg flex items-center justify-center shrink-0',
                              isAllEnabled
                                ? 'bg-[var(--color-brand-primary)] text-white'
                                : 'bg-[var(--color-surface-inset)] text-[var(--color-text-tertiary)]'
                            )}
                          >
                            <Icon className="h-5 w-5" />
                          </div>
                          <div className="flex-1 min-w-0">
                            <div className="flex items-center justify-between">
                              <h3 className="font-semibold text-[var(--color-text-primary)]">{group.label}</h3>
                              <div
                                className={cn(
                                  'w-5 h-5 rounded border-2 flex items-center justify-center',
                                  isAllEnabled
                                    ? 'bg-[var(--color-brand-primary)] border-[var(--color-brand-primary)]'
                                    : isPartialEnabled
                                    ? 'border-[var(--color-brand-primary)] bg-[var(--color-brand-primary)]/20'
                                    : 'border-[var(--color-border)]'
                                )}
                              >
                                {(isAllEnabled || isPartialEnabled) && (
                                  <Check className="h-3 w-3 text-white" />
                                )}
                              </div>
                            </div>
                            <p className="text-sm text-[var(--color-text-secondary)] mt-1">{group.description}</p>
                            <p className="text-xs text-[var(--color-text-tertiary)] mt-2">
                              {enabledCount} of {group.flags.length} features enabled
                            </p>
                          </div>
                        </div>
                      </div>
                    );
                  })}
                </div>
              </>
            )}

            {currentStep === 4 && (
              <>
                {/* Contact Email */}
                <div className="space-y-2">
                  <Label htmlFor="contactEmail" className="text-sm font-medium">
                    Primary Contact Email <span className="text-[var(--color-error)]">*</span>
                  </Label>
                  <Input
                    id="contactEmail"
                    type="email"
                    placeholder="contact@company.com"
                    value={formData.contactEmail}
                    onChange={(e) => updateFormData('contactEmail', e.target.value)}
                    className={cn(fieldErrors.contactEmail && 'border-[var(--color-error)]')}
                  />
                  {fieldErrors.contactEmail && (
                    <p className="text-xs text-[var(--color-error)]">{fieldErrors.contactEmail}</p>
                  )}
                </div>

                {/* Contact Mobile */}
                <div className="space-y-2">
                  <Label htmlFor="contactMobile" className="text-sm font-medium">
                    Primary Contact Mobile
                  </Label>
                  <Input
                    id="contactMobile"
                    type="tel"
                    placeholder="+1 (555) 000-0000"
                    value={formData.contactMobile}
                    onChange={(e) => updateFormData('contactMobile', e.target.value)}
                  />
                </div>

                {/* Address Section */}
                <div className="pt-4">
                  <h3 className="font-medium text-[var(--color-text-primary)] mb-4">Company Address</h3>
                  
                  <div className="space-y-4">
                    {/* Address Line 1 */}
                    <div className="space-y-2">
                      <Label htmlFor="addressLine1" className="text-sm font-medium">
                        Address Line 1
                      </Label>
                      <Input
                        id="addressLine1"
                        placeholder="Street address"
                        value={formData.addressLine1}
                        onChange={(e) => updateFormData('addressLine1', e.target.value)}
                      />
                    </div>

                    {/* Address Line 2 */}
                    <div className="space-y-2">
                      <Label htmlFor="addressLine2" className="text-sm font-medium">
                        Address Line 2
                      </Label>
                      <Input
                        id="addressLine2"
                        placeholder="Apartment, suite, unit, etc."
                        value={formData.addressLine2}
                        onChange={(e) => updateFormData('addressLine2', e.target.value)}
                      />
                    </div>

                    {/* City & State */}
                    <div className="grid grid-cols-2 gap-4">
                      <div className="space-y-2">
                        <Label htmlFor="city" className="text-sm font-medium">
                          City
                        </Label>
                        <Input
                          id="city"
                          placeholder="City"
                          value={formData.city}
                          onChange={(e) => updateFormData('city', e.target.value)}
                        />
                      </div>
                      <div className="space-y-2">
                        <Label htmlFor="stateProvince" className="text-sm font-medium">
                          State / Province
                        </Label>
                        <Input
                          id="stateProvince"
                          placeholder="State"
                          value={formData.stateProvince}
                          onChange={(e) => updateFormData('stateProvince', e.target.value)}
                        />
                      </div>
                    </div>

                    {/* Postal Code & Country */}
                    <div className="grid grid-cols-2 gap-4">
                      <div className="space-y-2">
                        <Label htmlFor="postalCode" className="text-sm font-medium">
                          Postal Code
                        </Label>
                        <Input
                          id="postalCode"
                          placeholder="12345"
                          value={formData.postalCode}
                          onChange={(e) => updateFormData('postalCode', e.target.value)}
                        />
                      </div>
                      <div className="space-y-2">
                        <Label htmlFor="country" className="text-sm font-medium">
                          Country
                        </Label>
                        <Select value={formData.country} onValueChange={(value) => updateFormData('country', value)}>
                          <SelectTrigger>
                            <SelectValue placeholder="Select country" />
                          </SelectTrigger>
                          <SelectContent>
                            {countries.map((country) => (
                              <SelectItem key={country.countryCode} value={country.countryCode}>
                                {country.name}
                              </SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      </div>
                    </div>
                  </div>
                </div>
              </>
            )}

            {currentStep === 5 && (
              <>
                <p className="text-sm text-[var(--color-text-secondary)] mb-6">
                  Please review your setup details before completing.
                </p>

                {/* Company Setup Summary */}
                <div className="rounded-lg border border-[var(--color-border)] overflow-hidden mb-4">
                  <div className="bg-[var(--color-surface-inset)] px-4 py-3 border-b border-[var(--color-border)]">
                    <h3 className="font-medium text-[var(--color-text-primary)]">Company Setup</h3>
                  </div>
                  <div className="p-4 space-y-3">
                    <div className="flex justify-between">
                      <span className="text-[var(--color-text-secondary)]">Company Name</span>
                      <span className="font-medium text-[var(--color-text-primary)]">{formData.companyName}</span>
                    </div>
                    <div className="flex justify-between">
                      <span className="text-[var(--color-text-secondary)]">Industry</span>
                      <span className="font-medium text-[var(--color-text-primary)]">
                        {industries.find((i) => i.value === formData.industry)?.label ?? '-'}
                      </span>
                    </div>
                  </div>
                </div>

                {/* Regional Settings Summary */}
                <div className="rounded-lg border border-[var(--color-border)] overflow-hidden mb-4">
                  <div className="bg-[var(--color-surface-inset)] px-4 py-3 border-b border-[var(--color-border)]">
                    <h3 className="font-medium text-[var(--color-text-primary)]">Regional Settings</h3>
                  </div>
                  <div className="p-4 space-y-3">
                    <div className="flex justify-between">
                      <span className="text-[var(--color-text-secondary)]">Base Country</span>
                      <span className="font-medium text-[var(--color-text-primary)]">
                        {countries.find((c) => c.countryCode === formData.baseCountry)?.name ?? '-'}
                      </span>
                    </div>
                    <div className="flex justify-between">
                      <span className="text-[var(--color-text-secondary)]">Base Currency</span>
                      <span className="font-medium text-[var(--color-text-primary)]">{formData.baseCurrency}</span>
                    </div>
                    {formData.companySize && (
                      <div className="flex justify-between">
                        <span className="text-[var(--color-text-secondary)]">Company Size</span>
                        <span className="font-medium text-[var(--color-text-primary)]">
                          {companySizes.find((s) => s.value === formData.companySize)?.label ?? '-'}
                        </span>
                      </div>
                    )}
                    {formData.website && (
                      <div className="flex justify-between">
                        <span className="text-[var(--color-text-secondary)]">Website</span>
                        <span className="font-medium text-[var(--color-text-primary)]">{formData.website}</span>
                      </div>
                    )}
                  </div>
                </div>

                {/* Features Summary */}
                <div className="rounded-lg border border-[var(--color-border)] overflow-hidden mb-4">
                  <div className="bg-[var(--color-surface-inset)] px-4 py-3 border-b border-[var(--color-border)]">
                    <h3 className="font-medium text-[var(--color-text-primary)]">Features</h3>
                  </div>
                  <div className="p-4">
                    <div className="flex justify-between">
                      <span className="text-[var(--color-text-secondary)]">Enabled Features</span>
                      <span className="font-medium text-[var(--color-text-primary)]">
                        {enabledFeaturesCount} of {totalFeaturesCount}
                      </span>
                    </div>
                  </div>
                </div>

                {/* Contact Details Summary */}
                <div className="rounded-lg border border-[var(--color-border)] overflow-hidden">
                  <div className="bg-[var(--color-surface-inset)] px-4 py-3 border-b border-[var(--color-border)]">
                    <h3 className="font-medium text-[var(--color-text-primary)]">Contact Details</h3>
                  </div>
                  <div className="p-4 space-y-3">
                    <div className="flex justify-between">
                      <span className="text-[var(--color-text-secondary)]">Email</span>
                      <span className="font-medium text-[var(--color-text-primary)]">{formData.contactEmail}</span>
                    </div>
                    {formData.contactMobile && (
                      <div className="flex justify-between">
                        <span className="text-[var(--color-text-secondary)]">Mobile</span>
                        <span className="font-medium text-[var(--color-text-primary)]">{formData.contactMobile}</span>
                      </div>
                    )}
                    {(formData.addressLine1 || formData.city) && (
                      <div className="flex justify-between">
                        <span className="text-[var(--color-text-secondary)]">Address</span>
                        <span className="font-medium text-[var(--color-text-primary)] text-right">
                          {[formData.addressLine1, formData.city, formData.stateProvince, formData.postalCode]
                            .filter(Boolean)
                            .join(', ')}
                        </span>
                      </div>
                    )}
                  </div>
                </div>
              </>
            )}
          </div>

          {/* Error message */}
          {error && (
            <div className="mt-6 rounded-lg border border-[var(--color-error)]/20 bg-[var(--color-error-light)] p-4 text-sm text-[var(--color-error)]">
              {error}
            </div>
          )}

          {/* Navigation buttons */}
          <div className="flex items-center justify-between mt-8 pt-6 border-t border-[var(--color-border)]">
            <div className="flex items-center gap-2">
              <Button
                variant="ghost"
                onClick={handleBack}
                disabled={currentStep === 1 || saving}
                className="gap-2"
              >
                <ArrowLeft className="h-4 w-4" />
                Back
              </Button>
              {currentStep < 5 && (
                <Button
                  variant="ghost"
                  onClick={handleSkip}
                  disabled={saving}
                  className="text-[var(--color-text-tertiary)] hover:text-[var(--color-text-secondary)]"
                >
                  Skip for now
                </Button>
              )}
            </div>
            <Button onClick={handleContinue} disabled={saving} className="gap-2">
              {saving ? (
                <>
                  <div className="h-4 w-4 border-2 border-white border-t-transparent rounded-full animate-spin" />
                  Saving...
                </>
              ) : currentStep === 5 ? (
                <>
                  Complete Setup
                  <Check className="h-4 w-4" />
                </>
              ) : (
                <>
                  Continue
                  <ArrowRight className="h-4 w-4" />
                </>
              )}
            </Button>
          </div>

          {/* Footer note */}
          <p className="text-center text-xs text-[var(--color-text-tertiary)] mt-6">
            You can always change these settings later
          </p>
        </div>
      </div>

      {/* Right Panel - Info */}
      <div className="hidden lg:flex w-[480px] bg-[var(--color-surface-inset)] flex-col">
        <div className="flex-1 p-10 flex flex-col">
          {/* Progress indicator */}
          <div className="mb-8">
            <div className="flex items-center justify-between mb-2">
              <span className="text-sm font-medium text-[var(--color-text-secondary)]">Setup Progress</span>
              <span className="text-sm font-semibold text-[var(--color-brand-primary)]">{progressPercentage}%</span>
            </div>
            <div className="h-2 bg-[var(--color-border)] rounded-full overflow-hidden">
              <div
                className="h-full bg-[var(--color-brand-primary)] rounded-full transition-all duration-500"
                style={{ width: `${progressPercentage}%` }}
              />
            </div>
          </div>

          {/* Step badge */}
          <div className="inline-flex items-center gap-2 px-3 py-1.5 rounded-full bg-[var(--color-brand-primary-light)] text-[var(--color-brand-primary)] text-sm font-medium w-fit mb-6">
            <CheckCircle2 className="h-4 w-4" />
            Step {currentStep} of 5
          </div>

          {/* Current step info */}
          <h2 className="text-3xl font-bold text-[var(--color-text-primary)] mb-4">
            {steps[currentStep - 1].title}
          </h2>
          <p className="text-[var(--color-text-secondary)] mb-8 leading-relaxed">
            {steps[currentStep - 1].description}
          </p>

          {/* Info box */}
          <div className="rounded-lg border border-[var(--color-border)] bg-[var(--color-surface)] p-4 mb-8">
            <div className="flex items-start gap-3">
              <Info className="h-5 w-5 text-[var(--color-brand-primary)] shrink-0 mt-0.5" />
              <div>
                <h4 className="font-medium text-[var(--color-text-primary)] mb-1">
                  Don't worry about perfection
                </h4>
                <p className="text-sm text-[var(--color-text-secondary)]">
                  You can always update these settings later from your workspace settings
                </p>
              </div>
            </div>
          </div>

          {/* What you'll get */}
          <div className="mt-auto">
            <h3 className="text-xs font-semibold uppercase tracking-wider text-[var(--color-text-tertiary)] mb-4">
              What you'll get
            </h3>
            <div className="grid grid-cols-3 gap-6">
              <div>
                <div className="text-2xl font-bold text-[var(--color-brand-primary)] mb-1">4</div>
                <div className="text-sm text-[var(--color-text-secondary)]">Countries supported</div>
              </div>
              <div>
                <div className="text-2xl font-bold text-[var(--color-brand-primary)] mb-1">AI</div>
                <div className="text-sm text-[var(--color-text-secondary)]">Powered drafting</div>
              </div>
              <div>
                <div className="text-2xl font-bold text-[var(--color-brand-primary)] mb-1">24/7</div>
                <div className="text-sm text-[var(--color-text-secondary)]">Platform access</div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
