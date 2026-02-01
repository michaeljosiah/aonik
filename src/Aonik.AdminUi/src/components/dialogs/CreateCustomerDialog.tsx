import { useMemo, useState } from 'react';
import { Plus, Trash2 } from 'lucide-react';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import type {
  CreateCustomerAddressRequest,
  CreateCustomerContactRequest,
  CreateCustomerRequest,
} from '@/types';

interface CreateCustomerDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSave: (data: CreateCustomerRequest) => Promise<void>;
}

const createEmptyContact = (): CreateCustomerContactRequest => ({
  type: 'Email',
  value: '',
  isPrimary: false,
});

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

const createEmptyForm = (): CreateCustomerRequest => ({
  displayName: '',
  partyType: 'Person',
  status: 'Active',
  customerTierCode: '',
  title: '',
  firstName: '',
  lastName: '',
  dob: '',
  nationality: '',
  occupation: '',
  countryCode: '',
  registrationNumber: '',
  incorporationCountry: '',
  industry: '',
  contacts: [createEmptyContact()],
  addresses: [],
});

export function CreateCustomerDialog({ open, onOpenChange, onSave }: CreateCustomerDialogProps) {
  const [activeTab, setActiveTab] = useState('basic');
  const [formData, setFormData] = useState<CreateCustomerRequest>(() => createEmptyForm());
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const isPerson = formData.partyType === 'Person';

  const isValid = useMemo(() => {
    return formData.displayName.trim().length > 0 && formData.partyType.length > 0;
  }, [formData.displayName, formData.partyType]);

  const updateField = <K extends keyof CreateCustomerRequest>(field: K, value: CreateCustomerRequest[K]) => {
    setFormData((prev) => ({ ...prev, [field]: value }));
  };

  const handlePartyTypeChange = (value: 'Person' | 'Business') => {
    setFormData((prev) => ({
      ...prev,
      partyType: value,
      title: value === 'Person' ? prev.title : '',
      firstName: value === 'Person' ? prev.firstName : '',
      lastName: value === 'Person' ? prev.lastName : '',
      dob: value === 'Person' ? prev.dob : '',
      nationality: value === 'Person' ? prev.nationality : '',
      occupation: value === 'Person' ? prev.occupation : '',
      countryCode: value === 'Person' ? prev.countryCode : '',
      registrationNumber: value === 'Business' ? prev.registrationNumber : '',
      incorporationCountry: value === 'Business' ? prev.incorporationCountry : '',
      industry: value === 'Business' ? prev.industry : '',
    }));
    setActiveTab('profile');
  };

  const resetForm = () => {
    setFormData(createEmptyForm());
    setActiveTab('basic');
    setError(null);
  };

  const handleClose = (nextOpen: boolean) => {
    if (!nextOpen) {
      resetForm();
    }
    onOpenChange(nextOpen);
  };

  const handleSave = async () => {
    if (!isValid || saving) return;
    setSaving(true);
    setError(null);
    try {
      await onSave(formData);
      handleClose(false);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create customer');
    } finally {
      setSaving(false);
    }
  };

  const addContact = () => {
    setFormData((prev) => ({
      ...prev,
      contacts: [...prev.contacts, createEmptyContact()],
    }));
  };

  const removeContact = (index: number) => {
    setFormData((prev) => ({
      ...prev,
      contacts: prev.contacts.filter((_, i) => i !== index),
    }));
  };

  const updateContact = (index: number, field: keyof CreateCustomerContactRequest, value: unknown) => {
    setFormData((prev) => ({
      ...prev,
      contacts: prev.contacts.map((contact, i) =>
        i === index ? { ...contact, [field]: value } : contact,
      ),
    }));
  };

  const addAddress = () => {
    setFormData((prev) => ({
      ...prev,
      addresses: [...prev.addresses, createEmptyAddress()],
    }));
  };

  const removeAddress = (index: number) => {
    setFormData((prev) => ({
      ...prev,
      addresses: prev.addresses.filter((_, i) => i !== index),
    }));
  };

  const updateAddress = (index: number, field: keyof CreateCustomerAddressRequest, value: unknown) => {
    setFormData((prev) => ({
      ...prev,
      addresses: prev.addresses.map((address, i) =>
        i === index ? { ...address, [field]: value } : address,
      ),
    }));
  };

  return (
    <Dialog open={open} onOpenChange={handleClose}>
      <DialogContent className="sm:max-w-[800px] max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Create New Customer</DialogTitle>
          <DialogDescription>
            Add a new customer to your tenant. Mandatory fields are marked with an asterisk.
          </DialogDescription>
        </DialogHeader>

        <div className="grid gap-2">
          <label className="text-sm font-medium text-[var(--color-text-primary)]">
            Party Type <span className="text-[var(--color-error)]">*</span>
          </label>
          <select
            value={formData.partyType}
            onChange={(e) => handlePartyTypeChange(e.target.value as 'Person' | 'Business')}
            className="flex h-10 w-full rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--color-brand-primary)]"
          >
            <option value="Person">Person</option>
            <option value="Business">Business</option>
          </select>
        </div>

        <Tabs value={activeTab} onValueChange={setActiveTab} className="mt-4">
          <TabsList className="grid w-full grid-cols-4 bg-transparent p-0 h-auto gap-0 border-b border-[var(--color-border-light)]">
            <TabsTrigger
              value="basic"
              className="px-4 py-3 text-sm rounded-none border-b-2 border-transparent data-[state=active]:border-[var(--color-brand-primary)] data-[state=active]:text-[var(--color-brand-primary)]"
            >
              Basic Info *
            </TabsTrigger>
            <TabsTrigger
              value="profile"
              className="px-4 py-3 text-sm rounded-none border-b-2 border-transparent data-[state=active]:border-[var(--color-brand-primary)] data-[state=active]:text-[var(--color-brand-primary)]"
            >
              {isPerson ? 'Personal Profile' : 'Business Profile'}
            </TabsTrigger>
            <TabsTrigger
              value="contacts"
              className="px-4 py-3 text-sm rounded-none border-b-2 border-transparent data-[state=active]:border-[var(--color-brand-primary)] data-[state=active]:text-[var(--color-brand-primary)]"
            >
              Contacts
            </TabsTrigger>
            <TabsTrigger
              value="addresses"
              className="px-4 py-3 text-sm rounded-none border-b-2 border-transparent data-[state=active]:border-[var(--color-brand-primary)] data-[state=active]:text-[var(--color-brand-primary)]"
            >
              Addresses
            </TabsTrigger>
          </TabsList>

          <TabsContent value="basic" className="mt-6 space-y-4">
            <div className="grid gap-4">
              <div className="grid gap-2">
                <label className="text-sm font-medium text-[var(--color-text-primary)]">
                  Display Name <span className="text-[var(--color-error)]">*</span>
                </label>
                <input
                  type="text"
                  value={formData.displayName}
                  onChange={(e) => updateField('displayName', e.target.value)}
                  className="flex h-10 w-full rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--color-brand-primary)]"
                  placeholder="Enter display name"
                />
              </div>

              <div className="grid gap-2">
                <label className="text-sm font-medium text-[var(--color-text-primary)]">Status</label>
                <select
                  value={formData.status}
                  onChange={(e) => updateField('status', e.target.value)}
                  className="flex h-10 w-full rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--color-brand-primary)]"
                >
                  <option value="Active">Active</option>
                  <option value="Pending">Pending</option>
                  <option value="Deactivated">Deactivated</option>
                  <option value="Suspended">Suspended</option>
                </select>
              </div>

              <div className="grid gap-2">
                <label className="text-sm font-medium text-[var(--color-text-primary)]">Customer Tier Code</label>
                <input
                  type="text"
                  value={formData.customerTierCode || ''}
                  onChange={(e) => updateField('customerTierCode', e.target.value || null)}
                  className="flex h-10 w-full rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--color-brand-primary)]"
                  placeholder="e.g., Standard, Premium"
                />
              </div>
            </div>
          </TabsContent>

          <TabsContent value="profile" className="mt-6 space-y-4">
            {isPerson ? (
              <div className="grid gap-4">
                <div className="grid grid-cols-3 gap-4">
                  <div className="grid gap-2">
                    <label className="text-sm font-medium text-[var(--color-text-primary)]">Title</label>
                    <select
                      value={formData.title || ''}
                      onChange={(e) => updateField('title', e.target.value || null)}
                      className="flex h-10 w-full rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--color-brand-primary)]"
                    >
                      <option value="">Select title</option>
                      <option value="Mr">Mr</option>
                      <option value="Mrs">Mrs</option>
                      <option value="Ms">Ms</option>
                      <option value="Dr">Dr</option>
                      <option value="Prof">Prof</option>
                    </select>
                  </div>
                  <div className="grid gap-2">
                    <label className="text-sm font-medium text-[var(--color-text-primary)]">First Name</label>
                    <input
                      type="text"
                      value={formData.firstName || ''}
                      onChange={(e) => updateField('firstName', e.target.value || null)}
                      className="flex h-10 w-full rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--color-brand-primary)]"
                    />
                  </div>
                  <div className="grid gap-2">
                    <label className="text-sm font-medium text-[var(--color-text-primary)]">Last Name</label>
                    <input
                      type="text"
                      value={formData.lastName || ''}
                      onChange={(e) => updateField('lastName', e.target.value || null)}
                      className="flex h-10 w-full rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--color-brand-primary)]"
                    />
                  </div>
                </div>

                <div className="grid gap-2">
                  <label className="text-sm font-medium text-[var(--color-text-primary)]">Date of Birth</label>
                  <input
                    type="date"
                    value={formData.dob || ''}
                    onChange={(e) => updateField('dob', e.target.value || null)}
                    className="flex h-10 w-full rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--color-brand-primary)]"
                  />
                </div>

                <div className="grid grid-cols-2 gap-4">
                  <div className="grid gap-2">
                    <label className="text-sm font-medium text-[var(--color-text-primary)]">Nationality</label>
                    <input
                      type="text"
                      value={formData.nationality || ''}
                      onChange={(e) => updateField('nationality', e.target.value || null)}
                      className="flex h-10 w-full rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--color-brand-primary)]"
                    />
                  </div>
                  <div className="grid gap-2">
                    <label className="text-sm font-medium text-[var(--color-text-primary)]">Occupation</label>
                    <input
                      type="text"
                      value={formData.occupation || ''}
                      onChange={(e) => updateField('occupation', e.target.value || null)}
                      className="flex h-10 w-full rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--color-brand-primary)]"
                    />
                  </div>
                </div>

                <div className="grid gap-2">
                  <label className="text-sm font-medium text-[var(--color-text-primary)]">Country Code</label>
                  <input
                    type="text"
                    value={formData.countryCode || ''}
                    onChange={(e) => updateField('countryCode', e.target.value || null)}
                    className="flex h-10 w-full rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--color-brand-primary)]"
                    placeholder="e.g., US, GB, NG"
                    maxLength={2}
                  />
                </div>
              </div>
            ) : (
              <div className="grid gap-4">
                <div className="grid gap-2">
                  <label className="text-sm font-medium text-[var(--color-text-primary)]">Registration Number</label>
                  <input
                    type="text"
                    value={formData.registrationNumber || ''}
                    onChange={(e) => updateField('registrationNumber', e.target.value || null)}
                    className="flex h-10 w-full rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--color-brand-primary)]"
                  />
                </div>

                <div className="grid gap-2">
                  <label className="text-sm font-medium text-[var(--color-text-primary)]">Incorporation Country</label>
                  <input
                    type="text"
                    value={formData.incorporationCountry || ''}
                    onChange={(e) => updateField('incorporationCountry', e.target.value || null)}
                    className="flex h-10 w-full rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--color-brand-primary)]"
                  />
                </div>

                <div className="grid gap-2">
                  <label className="text-sm font-medium text-[var(--color-text-primary)]">Industry</label>
                  <input
                    type="text"
                    value={formData.industry || ''}
                    onChange={(e) => updateField('industry', e.target.value || null)}
                    className="flex h-10 w-full rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--color-brand-primary)]"
                  />
                </div>
              </div>
            )}
          </TabsContent>

          <TabsContent value="contacts" className="mt-6 space-y-4">
            <div className="space-y-4">
              {formData.contacts.length === 0 ? (
                <p className="text-sm text-[var(--color-text-tertiary)] text-center py-6">
                  No contacts added yet. Use the button below to add one.
                </p>
              ) : (
                formData.contacts.map((contact, index) => (
                  <div
                    key={`${contact.type}-${index}`}
                    className="flex items-start gap-3 p-4 border border-[var(--color-border)] rounded-md bg-[var(--color-surface)]"
                  >
                    <div className="flex-1 grid grid-cols-3 gap-3">
                      <div className="grid gap-2">
                        <label className="text-sm font-medium text-[var(--color-text-primary)]">Type</label>
                        <select
                          value={contact.type}
                          onChange={(e) => updateContact(index, 'type', e.target.value as 'Email' | 'Phone')}
                          className="flex h-10 w-full rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--color-brand-primary)]"
                        >
                          <option value="Email">Email</option>
                          <option value="Phone">Phone</option>
                        </select>
                      </div>
                      <div className="grid gap-2">
                        <label className="text-sm font-medium text-[var(--color-text-primary)]">Value</label>
                        <input
                          type={contact.type === 'Email' ? 'email' : 'tel'}
                          value={contact.value}
                          onChange={(e) => updateContact(index, 'value', e.target.value)}
                          className="flex h-10 w-full rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--color-brand-primary)]"
                          placeholder={contact.type === 'Email' ? 'email@example.com' : '+1234567890'}
                        />
                      </div>
                      <div className="grid gap-2">
                        <label className="text-sm font-medium text-[var(--color-text-primary)]">Primary</label>
                        <div className="flex items-center h-10">
                          <input
                            type="checkbox"
                            checked={contact.isPrimary}
                            onChange={(e) => updateContact(index, 'isPrimary', e.target.checked)}
                            className="w-4 h-4 rounded border-[var(--color-border)] text-[var(--color-brand-primary)] focus:ring-[var(--color-brand-primary)]"
                          />
                        </div>
                      </div>
                    </div>
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => removeContact(index)}
                      className="mt-6"
                    >
                      <Trash2 className="w-4 h-4 text-[var(--color-error)]" />
                    </Button>
                  </div>
                ))
              )}
              <Button variant="outline" onClick={addContact} className="w-full">
                <Plus className="w-4 h-4 mr-2" />
                Add Contact
              </Button>
            </div>
          </TabsContent>

          <TabsContent value="addresses" className="mt-6 space-y-4">
            <div className="space-y-4">
              {formData.addresses.length === 0 ? (
                <p className="text-sm text-[var(--color-text-tertiary)] text-center py-6">
                  No addresses added yet. Use the button below to add one.
                </p>
              ) : (
                formData.addresses.map((address, index) => (
                  <div
                    key={`${address.type}-${index}`}
                    className="p-4 border border-[var(--color-border)] rounded-md bg-[var(--color-surface)] space-y-3"
                  >
                    <div className="flex items-center justify-between">
                      <div className="w-48">
                        <label className="text-sm font-medium text-[var(--color-text-primary)]">Type</label>
                        <select
                          value={address.type}
                          onChange={(e) => updateAddress(index, 'type', e.target.value)}
                          className="mt-1 flex h-10 w-full rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--color-brand-primary)]"
                        >
                          <option value="Home">Home</option>
                          <option value="Work">Work</option>
                          <option value="Billing">Billing</option>
                          <option value="Shipping">Shipping</option>
                          <option value="Other">Other</option>
                        </select>
                      </div>
                      <Button variant="ghost" size="sm" onClick={() => removeAddress(index)}>
                        <Trash2 className="w-4 h-4 text-[var(--color-error)]" />
                      </Button>
                    </div>

                    <div className="grid gap-2">
                      <label className="text-sm font-medium text-[var(--color-text-primary)]">Line 1</label>
                      <input
                        type="text"
                        value={address.line1}
                        onChange={(e) => updateAddress(index, 'line1', e.target.value)}
                        className="flex h-10 w-full rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--color-brand-primary)]"
                      />
                    </div>

                    <div className="grid grid-cols-2 gap-3">
                      <div className="grid gap-2">
                        <label className="text-sm font-medium text-[var(--color-text-primary)]">Line 2</label>
                        <input
                          type="text"
                          value={address.line2 || ''}
                          onChange={(e) => updateAddress(index, 'line2', e.target.value || null)}
                          className="flex h-10 w-full rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--color-brand-primary)]"
                        />
                      </div>
                      <div className="grid gap-2">
                        <label className="text-sm font-medium text-[var(--color-text-primary)]">Line 3</label>
                        <input
                          type="text"
                          value={address.line3 || ''}
                          onChange={(e) => updateAddress(index, 'line3', e.target.value || null)}
                          className="flex h-10 w-full rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--color-brand-primary)]"
                        />
                      </div>
                    </div>

                    <div className="grid grid-cols-2 gap-3">
                      <div className="grid gap-2">
                        <label className="text-sm font-medium text-[var(--color-text-primary)]">City</label>
                        <input
                          type="text"
                          value={address.city}
                          onChange={(e) => updateAddress(index, 'city', e.target.value)}
                          className="flex h-10 w-full rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--color-brand-primary)]"
                        />
                      </div>
                      <div className="grid gap-2">
                        <label className="text-sm font-medium text-[var(--color-text-primary)]">State/Province</label>
                        <input
                          type="text"
                          value={address.state || ''}
                          onChange={(e) => updateAddress(index, 'state', e.target.value || null)}
                          className="flex h-10 w-full rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--color-brand-primary)]"
                        />
                      </div>
                    </div>

                    <div className="grid grid-cols-2 gap-3">
                      <div className="grid gap-2">
                        <label className="text-sm font-medium text-[var(--color-text-primary)]">Postcode</label>
                        <input
                          type="text"
                          value={address.postcode}
                          onChange={(e) => updateAddress(index, 'postcode', e.target.value)}
                          className="flex h-10 w-full rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--color-brand-primary)]"
                        />
                      </div>
                      <div className="grid gap-2">
                        <label className="text-sm font-medium text-[var(--color-text-primary)]">Country</label>
                        <input
                          type="text"
                          value={address.country}
                          onChange={(e) => updateAddress(index, 'country', e.target.value)}
                          className="flex h-10 w-full rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--color-brand-primary)]"
                        />
                      </div>
                    </div>
                  </div>
                ))
              )}
              <Button variant="outline" onClick={addAddress} className="w-full">
                <Plus className="w-4 h-4 mr-2" />
                Add Address
              </Button>
            </div>
          </TabsContent>
        </Tabs>

        {error && (
          <div className="rounded-md bg-[var(--color-error-light)] p-3 text-sm text-[var(--color-error)]">
            {error}
          </div>
        )}

        <DialogFooter className="mt-6">
          <Button variant="outline" onClick={() => handleClose(false)} disabled={saving}>
            Cancel
          </Button>
          <Button onClick={handleSave} disabled={saving || !isValid}>
            {saving ? 'Creating...' : 'Create Customer'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
