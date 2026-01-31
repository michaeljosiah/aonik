import { useState } from 'react';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import type { PersonProfileDetail, UpdateUserProfileRequest } from '@/types';

interface EditUserProfileDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  profile: PersonProfileDetail | null | undefined;
  onSave: (data: UpdateUserProfileRequest) => Promise<void>;
}

export function EditUserProfileDialog({ open, onOpenChange, profile, onSave }: EditUserProfileDialogProps) {
  const [formData, setFormData] = useState<UpdateUserProfileRequest>({
    firstName: profile?.firstName || '',
    lastName: profile?.lastName || '',
    title: profile?.title || '',
    countryCode: profile?.countryCode || '',
    nationality: profile?.nationality || '',
    occupation: profile?.occupation || '',
  });
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleChange = (field: keyof UpdateUserProfileRequest, value: string) => {
    setFormData(prev => ({ ...prev, [field]: value || null }));
  };

  const handleSave = async () => {
    setSaving(true);
    setError(null);
    try {
      await onSave(formData);
      onOpenChange(false);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to update profile');
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[500px]">
        <DialogHeader>
          <DialogTitle>Edit User Profile</DialogTitle>
          <DialogDescription>
            Update the user's personal information below.
          </DialogDescription>
        </DialogHeader>

        <div className="grid gap-4 py-4">
          {/* Title */}
          <div className="grid gap-2">
            <label htmlFor="title" className="text-sm font-medium text-[var(--color-text-primary)]">
              Title
            </label>
            <select
              id="title"
              value={formData.title || ''}
              onChange={(e) => handleChange('title', e.target.value)}
              className="flex h-10 w-full rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-3 py-2 text-sm ring-offset-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--color-brand-primary)] focus-visible:ring-offset-2"
            >
              <option value="">Select title</option>
              <option value="Mr">Mr</option>
              <option value="Mrs">Mrs</option>
              <option value="Ms">Ms</option>
              <option value="Dr">Dr</option>
              <option value="Prof">Prof</option>
            </select>
          </div>

          {/* First Name */}
          <div className="grid gap-2">
            <label htmlFor="firstName" className="text-sm font-medium text-[var(--color-text-primary)]">
              First Name
            </label>
            <input
              id="firstName"
              type="text"
              value={formData.firstName || ''}
              onChange={(e) => handleChange('firstName', e.target.value)}
              className="flex h-10 w-full rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-3 py-2 text-sm ring-offset-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--color-brand-primary)] focus-visible:ring-offset-2"
              placeholder="Enter first name"
            />
          </div>

          {/* Last Name */}
          <div className="grid gap-2">
            <label htmlFor="lastName" className="text-sm font-medium text-[var(--color-text-primary)]">
              Last Name
            </label>
            <input
              id="lastName"
              type="text"
              value={formData.lastName || ''}
              onChange={(e) => handleChange('lastName', e.target.value)}
              className="flex h-10 w-full rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-3 py-2 text-sm ring-offset-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--color-brand-primary)] focus-visible:ring-offset-2"
              placeholder="Enter last name"
            />
          </div>

          {/* Country Code */}
          <div className="grid gap-2">
            <label htmlFor="countryCode" className="text-sm font-medium text-[var(--color-text-primary)]">
              Country Code
            </label>
            <input
              id="countryCode"
              type="text"
              value={formData.countryCode || ''}
              onChange={(e) => handleChange('countryCode', e.target.value)}
              className="flex h-10 w-full rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-3 py-2 text-sm ring-offset-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--color-brand-primary)] focus-visible:ring-offset-2"
              placeholder="e.g., US, GB, NG"
              maxLength={2}
            />
          </div>

          {/* Nationality */}
          <div className="grid gap-2">
            <label htmlFor="nationality" className="text-sm font-medium text-[var(--color-text-primary)]">
              Nationality
            </label>
            <input
              id="nationality"
              type="text"
              value={formData.nationality || ''}
              onChange={(e) => handleChange('nationality', e.target.value)}
              className="flex h-10 w-full rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-3 py-2 text-sm ring-offset-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--color-brand-primary)] focus-visible:ring-offset-2"
              placeholder="Enter nationality"
            />
          </div>

          {/* Occupation */}
          <div className="grid gap-2">
            <label htmlFor="occupation" className="text-sm font-medium text-[var(--color-text-primary)]">
              Occupation
            </label>
            <input
              id="occupation"
              type="text"
              value={formData.occupation || ''}
              onChange={(e) => handleChange('occupation', e.target.value)}
              className="flex h-10 w-full rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-3 py-2 text-sm ring-offset-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--color-brand-primary)] focus-visible:ring-offset-2"
              placeholder="Enter occupation"
            />
          </div>

          {/* Error Display */}
          {error && (
            <div className="rounded-md bg-[var(--color-error-light)] p-3 text-sm text-[var(--color-error)]">
              {error}
            </div>
          )}
        </div>

        <DialogFooter>
          <Button
            variant="outline"
            onClick={() => onOpenChange(false)}
            disabled={saving}
          >
            Cancel
          </Button>
          <Button
            onClick={handleSave}
            disabled={saving}
          >
            {saving ? 'Saving...' : 'Save Changes'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
