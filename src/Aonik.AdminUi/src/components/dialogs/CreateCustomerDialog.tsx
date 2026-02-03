import { useState, useMemo } from 'react';
import { ArrowLeft, ChevronDown, ChevronUp, User, Building2, Mail, Phone } from 'lucide-react';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { CountrySelect } from '@/components/ui/country-select';
import { InputGroup } from '@/components/ui/input-group';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import type {
  CreateCustomerAddressRequest,
  CreateCustomerRequest,
} from '@/types';

type Step = 'selection' | 'person-form' | 'business-form';

interface CreateCustomerDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSave: (data: CreateCustomerRequest) => Promise<void>;
}

const createEmptyAddress = (): CreateCustomerAddressRequest => ({
  type: 'Home',
  line1: '',
  line2: '',
  line3: '',
  city: '',
  state: '',
  postcode: '',
  country: '',
});

const createEmptyPersonForm = (): CreateCustomerRequest => ({
  displayName: '',
  partyType: 'Person',
  status: 'Active',
  customerTierCode: '',
  title: '',
  firstName: '',
  lastName: '',
  dob: null,
  nationality: '',
  occupation: '',
  countryCode: '',
  contacts: [
    { type: 'Email', value: '', isPrimary: true },
    { type: 'Phone', value: '', isPrimary: false },
  ],
  addresses: [],
});

const createEmptyBusinessForm = (): CreateCustomerRequest => ({
  displayName: '',
  partyType: 'Business',
  status: 'Active',
  customerTierCode: '',
  registrationNumber: '',
  incorporationCountry: '',
  industry: '',
  contacts: [
    { type: 'Email', value: '', isPrimary: true },
    { type: 'Phone', value: '', isPrimary: false },
  ],
  addresses: [],
});

const normalizeOptional = (value?: string | null) => {
  if (value == null) return null;
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : null;
};

const normalizeCustomerRequest = (request: CreateCustomerRequest): CreateCustomerRequest => ({
  ...request,
  customerTierCode: normalizeOptional(request.customerTierCode),
  title: normalizeOptional(request.title),
  firstName: normalizeOptional(request.firstName),
  lastName: normalizeOptional(request.lastName),
  dob: normalizeOptional(request.dob),
  nationality: normalizeOptional(request.nationality),
  occupation: normalizeOptional(request.occupation),
  countryCode: normalizeOptional(request.countryCode),
  registrationNumber: normalizeOptional(request.registrationNumber),
  incorporationCountry: normalizeOptional(request.incorporationCountry),
  industry: normalizeOptional(request.industry),
});

export function CreateCustomerDialog({ open, onOpenChange, onSave }: CreateCustomerDialogProps) {
  const [step, setStep] = useState<Step>('selection');
  const [formData, setFormData] = useState<CreateCustomerRequest>(() => createEmptyPersonForm());
  const [addressExpanded, setAddressExpanded] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const fieldClassName =
    "flex h-10 w-full rounded-none border border-[var(--color-form-field-border)] bg-[var(--color-form-field-bg)] px-3 py-2 text-sm leading-5 text-[var(--color-form-field-text)] placeholder:text-[var(--color-form-field-placeholder)] focus-visible:outline-none focus-visible:ring-0 focus-visible:border-[var(--color-form-field-border-focus)]";

  const isValid = useMemo(() => {
    if (!formData.displayName.trim()) return false;
    return true;
  }, [formData.displayName]);

  const resetForm = () => {
    setStep('selection');
    setFormData(createEmptyPersonForm());
    setAddressExpanded(false);
    setError(null);
  };

  const handleClose = (nextOpen: boolean) => {
    if (!nextOpen) {
      resetForm();
    }
    onOpenChange(nextOpen);
  };

  const handleSelectPerson = () => {
    setFormData(createEmptyPersonForm());
    setStep('person-form');
  };

  const handleSelectBusiness = () => {
    setFormData(createEmptyBusinessForm());
    setStep('business-form');
  };

  const handleBack = () => {
    setStep('selection');
    setAddressExpanded(false);
  };

  const handleSave = async () => {
    if (!isValid || saving) return;
    setSaving(true);
    setError(null);
    try {
      await onSave(normalizeCustomerRequest(formData));
      handleClose(false);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create customer');
    } finally {
      setSaving(false);
    }
  };

  const updateField = <K extends keyof CreateCustomerRequest>(field: K, value: CreateCustomerRequest[K]) => {
    setFormData((prev) => ({ ...prev, [field]: value }));
  };

  const updateContact = (index: number, value: string) => {
    setFormData((prev) => ({
      ...prev,
      contacts: prev.contacts.map((contact, i) =>
        i === index ? { ...contact, value } : contact
      ),
    }));
  };

  const updateAddress = (field: keyof CreateCustomerAddressRequest, value: string) => {
    setFormData((prev) => {
      const currentAddress = prev.addresses[0] || createEmptyAddress();
      const updatedAddress = { ...currentAddress, [field]: value };
      return {
        ...prev,
        addresses: [updatedAddress],
      };
    });
  };

  const renderSelectionScreen = () => (
    <div className="space-y-6">
      <DialogHeader>
        <DialogTitle>Create New Customer</DialogTitle>
        <DialogDescription>
          Choose the type of customer you want to register
        </DialogDescription>
      </DialogHeader>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {/* Person Card */}
        <Card
          className="cursor-pointer overflow-hidden hover:shadow-lg transition-all hover:border-[var(--color-brand-primary)] group"
          onClick={handleSelectPerson}
        >
          <div className="h-32 bg-gradient-to-br from-[var(--color-brand-primary)] to-[var(--color-brand-secondary)] flex items-center justify-center relative overflow-hidden">
            <div className="absolute inset-0 opacity-20">
              <img
                src="/assets/images/person-card.png"
                alt=""
                className="w-full h-full object-cover"
                onError={(e) => {
                  // Fallback to icon if image fails to load
                  e.currentTarget.style.display = 'none';
                }}
              />
            </div>
            <User className="w-16 h-16 text-white relative z-10" />
          </div>
          <div className="p-5">
            <h3 className="text-lg font-semibold text-[var(--color-text-primary)] mb-2 group-hover:text-[var(--color-brand-primary)] transition-colors">
              Individual Person
            </h3>
            <p className="text-sm text-[var(--color-text-secondary)]">
              Register an individual customer with personal details, contact information, and address.
            </p>
          </div>
        </Card>

        {/* Business Card */}
        <Card
          className="cursor-pointer overflow-hidden hover:shadow-lg transition-all hover:border-[var(--color-brand-primary)] group"
          onClick={handleSelectBusiness}
        >
          <div className="h-32 bg-gradient-to-br from-[#1a1a2e] to-[#16213e] flex items-center justify-center relative overflow-hidden">
            <div className="absolute inset-0 opacity-20">
              <img
                src="/assets/images/business-card.png"
                alt=""
                className="w-full h-full object-cover"
                onError={(e) => {
                  e.currentTarget.style.display = 'none';
                }}
              />
            </div>
            <Building2 className="w-16 h-16 text-white relative z-10" />
          </div>
          <div className="p-5">
            <h3 className="text-lg font-semibold text-[var(--color-text-primary)] mb-2 group-hover:text-[var(--color-brand-primary)] transition-colors">
              Business Entity
            </h3>
            <p className="text-sm text-[var(--color-text-secondary)]">
              Register a business or organization with company details, contact information, and address.
            </p>
          </div>
        </Card>
      </div>
    </div>
  );

  const renderPersonForm = () => (
    <div className="space-y-6">
      <DialogHeader>
        <div className="flex items-center gap-2">
          <Button variant="ghost" size="icon-sm" onClick={handleBack} className="-ml-2">
            <ArrowLeft className="w-4 h-4" />
          </Button>
          <DialogTitle>Register Individual</DialogTitle>
        </div>
        <DialogDescription>
          Enter the individual's information below
        </DialogDescription>
      </DialogHeader>

      <div className="space-y-4 max-h-[60vh] overflow-y-auto pr-2">
        {/* Basic Info */}
        <div className="space-y-4">
          <h4 className="text-sm font-medium text-[var(--color-text-primary)] border-b border-[var(--color-border-light)] pb-2">
            Basic Information
          </h4>
          
          <div className="grid gap-2">
            <label className="text-sm font-medium text-[var(--color-text-primary)]">
              Display Name <span className="text-[var(--color-error)]">*</span>
            </label>
            <input
              type="text"
              value={formData.displayName}
              onChange={(e) => updateField('displayName', e.target.value)}
              className={fieldClassName}
              placeholder="Enter display name"
            />
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div className="grid gap-2">
              <label className="text-sm font-medium text-[var(--color-text-primary)]">Status</label>
              <Select value={formData.status} onValueChange={(value) => updateField('status', value)}>
                <SelectTrigger>
                  <SelectValue placeholder="Select status" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Active">Active</SelectItem>
                  <SelectItem value="Pending">Pending</SelectItem>
                  <SelectItem value="Deactivated">Deactivated</SelectItem>
                  <SelectItem value="Suspended">Suspended</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="grid gap-2">
              <label className="text-sm font-medium text-[var(--color-text-primary)]">Customer Tier</label>
              <input
                type="text"
                value={formData.customerTierCode || ''}
                onChange={(e) => updateField('customerTierCode', e.target.value || null)}
                className={fieldClassName}
                placeholder="e.g., Standard, Premium"
              />
            </div>
          </div>
        </div>

        {/* Personal Details */}
        <div className="space-y-4">
          <h4 className="text-sm font-medium text-[var(--color-text-primary)] border-b border-[var(--color-border-light)] pb-2">
            Personal Details
          </h4>
          
          <div className="grid grid-cols-3 gap-4 items-start">
            <div className="grid gap-2">
              <label className="text-sm font-medium text-[var(--color-text-primary)]">Title</label>
              <Select value={formData.title || ''} onValueChange={(value) => updateField('title', value || null)}>
                <SelectTrigger>
                  <SelectValue placeholder="Select title" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Mr">Mr</SelectItem>
                  <SelectItem value="Mrs">Mrs</SelectItem>
                  <SelectItem value="Ms">Ms</SelectItem>
                  <SelectItem value="Dr">Dr</SelectItem>
                  <SelectItem value="Prof">Prof</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="grid gap-2">
              <label className="text-sm font-medium text-[var(--color-text-primary)]">First Name</label>
              <input
                type="text"
                value={formData.firstName || ''}
                onChange={(e) => updateField('firstName', e.target.value || null)}
                className={fieldClassName}
              />
            </div>
            <div className="grid gap-2">
              <label className="text-sm font-medium text-[var(--color-text-primary)]">Last Name</label>
              <input
                type="text"
                value={formData.lastName || ''}
                onChange={(e) => updateField('lastName', e.target.value || null)}
                className={fieldClassName}
              />
            </div>
          </div>

          <div className="grid gap-2">
            <label className="text-sm font-medium text-[var(--color-text-primary)]">Date of Birth</label>
            <input
              type="date"
              value={formData.dob || ''}
              onChange={(e) => updateField('dob', e.target.value || null)}
              className={fieldClassName}
            />
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div className="grid gap-2">
              <label className="text-sm font-medium text-[var(--color-text-primary)]">Nationality</label>
              <CountrySelect
                value={formData.nationality || ''}
                onChange={(value) => updateField('nationality', value || null)}
                placeholder="Select nationality"
              />
            </div>
            <div className="grid gap-2">
              <label className="text-sm font-medium text-[var(--color-text-primary)]">Occupation</label>
              <input
                type="text"
                value={formData.occupation || ''}
                onChange={(e) => updateField('occupation', e.target.value || null)}
                className={fieldClassName}
              />
            </div>
          </div>
          <div className="grid gap-2">
            <label className="text-sm font-medium text-[var(--color-text-primary)]">Country</label>
            <CountrySelect
              value={formData.countryCode || ''}
              onChange={(value) => updateField('countryCode', value || null)}
              placeholder="Select country"
            />
          </div>
        </div>

        {/* Contact Information */}
        <div className="space-y-4">
          <h4 className="text-sm font-medium text-[var(--color-text-primary)] border-b border-[var(--color-border-light)] pb-2">
            Contact Information
          </h4>
          
          <div className="grid gap-2">
            <label className="text-sm font-medium text-[var(--color-text-primary)]">Email Address</label>
            <InputGroup
              type="email"
              icon={<Mail className="w-4 h-4" aria-hidden="true" />}
              value={formData.contacts[0]?.value || ''}
              onChange={(e) => updateContact(0, e.target.value)}
              placeholder="email@example.com"
            />
          </div>

          <div className="grid gap-2">
            <label className="text-sm font-medium text-[var(--color-text-primary)]">Phone Number</label>
            <InputGroup
              type="tel"
              icon={<Phone className="w-4 h-4" aria-hidden="true" />}
              value={formData.contacts[1]?.value || ''}
              onChange={(e) => updateContact(1, e.target.value)}
              placeholder="+1234567890"
            />
          </div>
        </div>

        {/* Address Section - Collapsible */}
        <div className="space-y-4 pb-6">
          <button
            type="button"
            onClick={() => setAddressExpanded(!addressExpanded)}
            className="flex items-center justify-between w-full text-left group"
          >
            <h4 className="text-sm font-medium text-[var(--color-text-primary)] border-b border-[var(--color-border-light)] pb-2 flex-1">
              Address Details
            </h4>
            <span className="ml-2 text-[var(--color-text-tertiary)] group-hover:text-[var(--color-brand-primary)] transition-colors">
              {addressExpanded ? <ChevronUp className="w-4 h-4" /> : <ChevronDown className="w-4 h-4" />}
            </span>
          </button>

          {addressExpanded && (
            <div className="space-y-4 animate-in slide-in-from-top-2 duration-200">
              <div className="grid gap-2">
                <label className="text-sm font-medium text-[var(--color-text-primary)]">Address Type</label>
                <Select
                  value={formData.addresses[0]?.type || 'Home'}
                  onValueChange={(value) => updateAddress('type', value as CreateCustomerAddressRequest['type'])}
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Select address type" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Home">Home</SelectItem>
                    <SelectItem value="Work">Work</SelectItem>
                    <SelectItem value="Billing">Billing</SelectItem>
                    <SelectItem value="Shipping">Shipping</SelectItem>
                    <SelectItem value="Other">Other</SelectItem>
                  </SelectContent>
                </Select>
              </div>

              <div className="grid gap-2">
                <label className="text-sm font-medium text-[var(--color-text-primary)]">Line 1</label>
                <input
                  type="text"
                  value={formData.addresses[0]?.line1 || ''}
                  onChange={(e) => updateAddress('line1', e.target.value)}
                  className={fieldClassName}
                  placeholder="Street address"
                />
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div className="grid gap-2">
                  <label className="text-sm font-medium text-[var(--color-text-primary)]">Line 2</label>
                  <input
                    type="text"
                    value={formData.addresses[0]?.line2 || ''}
                    onChange={(e) => updateAddress('line2', e.target.value)}
                    className={fieldClassName}
                    placeholder="Apartment, suite, etc."
                  />
                </div>
                <div className="grid gap-2">
                  <label className="text-sm font-medium text-[var(--color-text-primary)]">Line 3</label>
                  <input
                    type="text"
                    value={formData.addresses[0]?.line3 || ''}
                    onChange={(e) => updateAddress('line3', e.target.value)}
                    className={fieldClassName}
                  />
                </div>
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div className="grid gap-2">
                  <label className="text-sm font-medium text-[var(--color-text-primary)]">City</label>
                  <input
                    type="text"
                    value={formData.addresses[0]?.city || ''}
                    onChange={(e) => updateAddress('city', e.target.value)}
                    className={fieldClassName}
                  />
                </div>
                <div className="grid gap-2">
                  <label className="text-sm font-medium text-[var(--color-text-primary)]">State/Province</label>
                  <input
                    type="text"
                    value={formData.addresses[0]?.state || ''}
                    onChange={(e) => updateAddress('state', e.target.value)}
                    className={fieldClassName}
                  />
                </div>
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div className="grid gap-2">
                  <label className="text-sm font-medium text-[var(--color-text-primary)]">Postcode</label>
                  <input
                    type="text"
                    value={formData.addresses[0]?.postcode || ''}
                    onChange={(e) => updateAddress('postcode', e.target.value)}
                    className={fieldClassName}
                  />
                </div>
                <div className="grid gap-2">
                  <label className="text-sm font-medium text-[var(--color-text-primary)]">Country</label>
                  <CountrySelect
                    value={formData.addresses[0]?.country || ''}
                    onChange={(value) => updateAddress('country', value)}
                    placeholder="Select country"
                  />
                </div>
              </div>
            </div>
          )}
        </div>
      </div>

      {error && (
        <div className="rounded-md bg-[var(--color-error-light)] p-3 text-sm text-[var(--color-error)]">
          {error}
        </div>
      )}

      <DialogFooter>
        <Button variant="outline" onClick={handleBack} disabled={saving}>
          Back
        </Button>
        <Button onClick={handleSave} disabled={saving || !isValid}>
          {saving ? 'Creating...' : 'Create Customer'}
        </Button>
      </DialogFooter>
    </div>
  );

  const renderBusinessForm = () => (
    <div className="space-y-6">
      <DialogHeader>
        <div className="flex items-center gap-2">
          <Button variant="ghost" size="icon-sm" onClick={handleBack} className="-ml-2">
            <ArrowLeft className="w-4 h-4" />
          </Button>
          <DialogTitle>Register Business</DialogTitle>
        </div>
        <DialogDescription>
          Enter the business information below
        </DialogDescription>
      </DialogHeader>

      <div className="space-y-4 max-h-[60vh] overflow-y-auto pr-2">
        {/* Basic Info */}
        <div className="space-y-4">
          <h4 className="text-sm font-medium text-[var(--color-text-primary)] border-b border-[var(--color-border-light)] pb-2">
            Basic Information
          </h4>
          
          <div className="grid gap-2">
            <label className="text-sm font-medium text-[var(--color-text-primary)]">
              Display Name <span className="text-[var(--color-error)]">*</span>
            </label>
            <input
              type="text"
              value={formData.displayName}
              onChange={(e) => updateField('displayName', e.target.value)}
              className={fieldClassName}
              placeholder="Enter business display name"
            />
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div className="grid gap-2">
              <label className="text-sm font-medium text-[var(--color-text-primary)]">Status</label>
              <Select value={formData.status} onValueChange={(value) => updateField('status', value)}>
                <SelectTrigger>
                  <SelectValue placeholder="Select status" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Active">Active</SelectItem>
                  <SelectItem value="Pending">Pending</SelectItem>
                  <SelectItem value="Deactivated">Deactivated</SelectItem>
                  <SelectItem value="Suspended">Suspended</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="grid gap-2">
              <label className="text-sm font-medium text-[var(--color-text-primary)]">Customer Tier</label>
              <input
                type="text"
                value={formData.customerTierCode || ''}
                onChange={(e) => updateField('customerTierCode', e.target.value || null)}
                className={fieldClassName}
                placeholder="e.g., Standard, Premium"
              />
            </div>
          </div>
        </div>

        {/* Business Details */}
        <div className="space-y-4">
          <h4 className="text-sm font-medium text-[var(--color-text-primary)] border-b border-[var(--color-border-light)] pb-2">
            Business Details
          </h4>
          
          <div className="grid gap-2">
            <label className="text-sm font-medium text-[var(--color-text-primary)]">Registration Number</label>
            <input
              type="text"
              value={formData.registrationNumber || ''}
              onChange={(e) => updateField('registrationNumber', e.target.value || null)}
              className={fieldClassName}
              placeholder="Company registration number"
            />
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div className="grid gap-2">
              <label className="text-sm font-medium text-[var(--color-text-primary)]">Incorporation Country</label>
              <CountrySelect
                value={formData.incorporationCountry || ''}
                onChange={(value) => updateField('incorporationCountry', value || null)}
                placeholder="Select country"
              />
            </div>
            <div className="grid gap-2">
              <label className="text-sm font-medium text-[var(--color-text-primary)]">Industry</label>
              <input
                type="text"
                value={formData.industry || ''}
                onChange={(e) => updateField('industry', e.target.value || null)}
                className={fieldClassName}
              />
            </div>
          </div>
        </div>

        {/* Contact Information */}
        <div className="space-y-4">
          <h4 className="text-sm font-medium text-[var(--color-text-primary)] border-b border-[var(--color-border-light)] pb-2">
            Contact Information
          </h4>
          
          <div className="grid gap-2">
            <label className="text-sm font-medium text-[var(--color-text-primary)]">Email Address</label>
            <InputGroup
              type="email"
              icon={<Mail className="w-4 h-4" aria-hidden="true" />}
              value={formData.contacts[0]?.value || ''}
              onChange={(e) => updateContact(0, e.target.value)}
              placeholder="business@example.com"
            />
          </div>

          <div className="grid gap-2">
            <label className="text-sm font-medium text-[var(--color-text-primary)]">Phone Number</label>
            <InputGroup
              type="tel"
              icon={<Phone className="w-4 h-4" aria-hidden="true" />}
              value={formData.contacts[1]?.value || ''}
              onChange={(e) => updateContact(1, e.target.value)}
              placeholder="+1234567890"
            />
          </div>
        </div>

        {/* Address Section - Collapsible */}
        <div className="space-y-4 pb-6">
          <button
            type="button"
            onClick={() => setAddressExpanded(!addressExpanded)}
            className="flex items-center justify-between w-full text-left group"
          >
            <h4 className="text-sm font-medium text-[var(--color-text-primary)] border-b border-[var(--color-border-light)] pb-2 flex-1">
              Address Details
            </h4>
            <span className="ml-2 text-[var(--color-text-tertiary)] group-hover:text-[var(--color-brand-primary)] transition-colors">
              {addressExpanded ? <ChevronUp className="w-4 h-4" /> : <ChevronDown className="w-4 h-4" />}
            </span>
          </button>

          {addressExpanded && (
            <div className="space-y-4 animate-in slide-in-from-top-2 duration-200">
              <div className="grid gap-2">
                <label className="text-sm font-medium text-[var(--color-text-primary)]">Address Type</label>
                <Select
                  value={formData.addresses[0]?.type || 'Work'}
                  onValueChange={(value) => updateAddress('type', value as CreateCustomerAddressRequest['type'])}
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Select address type" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Work">Work</SelectItem>
                    <SelectItem value="Billing">Billing</SelectItem>
                    <SelectItem value="Shipping">Shipping</SelectItem>
                    <SelectItem value="Other">Other</SelectItem>
                  </SelectContent>
                </Select>
              </div>

              <div className="grid gap-2">
                <label className="text-sm font-medium text-[var(--color-text-primary)]">Line 1</label>
                <input
                  type="text"
                  value={formData.addresses[0]?.line1 || ''}
                  onChange={(e) => updateAddress('line1', e.target.value)}
                  className={fieldClassName}
                  placeholder="Street address"
                />
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div className="grid gap-2">
                  <label className="text-sm font-medium text-[var(--color-text-primary)]">Line 2</label>
                  <input
                    type="text"
                    value={formData.addresses[0]?.line2 || ''}
                    onChange={(e) => updateAddress('line2', e.target.value)}
                    className={fieldClassName}
                    placeholder="Suite, floor, etc."
                  />
                </div>
                <div className="grid gap-2">
                  <label className="text-sm font-medium text-[var(--color-text-primary)]">Line 3</label>
                  <input
                    type="text"
                    value={formData.addresses[0]?.line3 || ''}
                    onChange={(e) => updateAddress('line3', e.target.value)}
                    className={fieldClassName}
                  />
                </div>
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div className="grid gap-2">
                  <label className="text-sm font-medium text-[var(--color-text-primary)]">City</label>
                <input
                  type="text"
                  value={formData.addresses[0]?.city || ''}
                  onChange={(e) => updateAddress('city', e.target.value)}
                  className={fieldClassName}
                />
                </div>
                <div className="grid gap-2">
                  <label className="text-sm font-medium text-[var(--color-text-primary)]">State/Province</label>
                <input
                  type="text"
                  value={formData.addresses[0]?.state || ''}
                  onChange={(e) => updateAddress('state', e.target.value)}
                  className={fieldClassName}
                />
                </div>
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div className="grid gap-2">
                  <label className="text-sm font-medium text-[var(--color-text-primary)]">Postcode</label>
                <input
                  type="text"
                  value={formData.addresses[0]?.postcode || ''}
                  onChange={(e) => updateAddress('postcode', e.target.value)}
                  className={fieldClassName}
                />
                </div>
                <div className="grid gap-2">
                  <label className="text-sm font-medium text-[var(--color-text-primary)]">Country</label>
                  <CountrySelect
                    value={formData.addresses[0]?.country || ''}
                    onChange={(value) => updateAddress('country', value)}
                    placeholder="Select country"
                  />
                </div>
              </div>
            </div>
          )}
        </div>
      </div>

      {error && (
        <div className="rounded-md bg-[var(--color-error-light)] p-3 text-sm text-[var(--color-error)]">
          {error}
        </div>
      )}

      <DialogFooter>
        <Button variant="outline" onClick={handleBack} disabled={saving}>
          Back
        </Button>
        <Button onClick={handleSave} disabled={saving || !isValid}>
          {saving ? 'Creating...' : 'Create Customer'}
        </Button>
      </DialogFooter>
    </div>
  );

  return (
    <Dialog open={open} onOpenChange={handleClose}>
      <DialogContent 
        className={`
          ${step === 'selection' ? 'sm:max-w-[700px]' : 'sm:max-w-[600px]'} 
          max-h-[90vh] overflow-y-auto
        `}
      >
        {step === 'selection' && renderSelectionScreen()}
        {step === 'person-form' && renderPersonForm()}
        {step === 'business-form' && renderBusinessForm()}
      </DialogContent>
    </Dialog>
  );
}
