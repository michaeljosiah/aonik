import { useState } from 'react';
import { AlertCircle } from 'lucide-react';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Alert, AlertDescription } from '@/components/ui/alert';
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
      <DialogContent className="max-w-[500px]">
        <DialogHeader>
          <DialogTitle>Edit user profile</DialogTitle>
          <DialogDescription>
            Update the user's personal information below.
          </DialogDescription>
        </DialogHeader>

        <div className="grid gap-4 py-4">
          {/* Title */}
          <div className="grid gap-2">
            <Label htmlFor="title">
              Title
            </Label>
            <Select
              value={formData.title ?? undefined}
              onValueChange={(value) => handleChange('title', value === '__clear__' ? '' : value)}
            >
              <SelectTrigger
                id="title"
                aria-label="Title"
              >
                <SelectValue placeholder="Select title" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="__clear__">Clear selection</SelectItem>
                <SelectItem value="Mr">Mr</SelectItem>
                <SelectItem value="Mrs">Mrs</SelectItem>
                <SelectItem value="Ms">Ms</SelectItem>
                <SelectItem value="Dr">Dr</SelectItem>
                <SelectItem value="Prof">Prof</SelectItem>
              </SelectContent>
            </Select>
          </div>

          {/* First Name */}
          <div className="grid gap-2">
            <Label htmlFor="firstName">
              First name
            </Label>
            <Input
              id="firstName"
              type="text"
              value={formData.firstName || ''}
              onChange={(e) => handleChange('firstName', e.target.value)}
              placeholder="Enter first name"
            />
          </div>

          {/* Last Name */}
          <div className="grid gap-2">
            <Label htmlFor="lastName">
              Last name
            </Label>
            <Input
              id="lastName"
              type="text"
              value={formData.lastName || ''}
              onChange={(e) => handleChange('lastName', e.target.value)}
              placeholder="Enter last name"
            />
          </div>

          {/* Country Code */}
          <div className="grid gap-2">
            <Label htmlFor="countryCode">
              Country code
            </Label>
            <Input
              id="countryCode"
              type="text"
              value={formData.countryCode || ''}
              onChange={(e) => handleChange('countryCode', e.target.value)}
              placeholder="e.g., US, GB, NG"
              maxLength={2}
            />
          </div>

          {/* Nationality */}
          <div className="grid gap-2">
            <Label htmlFor="nationality">
              Nationality
            </Label>
            <Input
              id="nationality"
              type="text"
              value={formData.nationality || ''}
              onChange={(e) => handleChange('nationality', e.target.value)}
              placeholder="Enter nationality"
            />
          </div>

          {/* Occupation */}
          <div className="grid gap-2">
            <Label htmlFor="occupation">
              Occupation
            </Label>
            <Input
              id="occupation"
              type="text"
              value={formData.occupation || ''}
              onChange={(e) => handleChange('occupation', e.target.value)}
              placeholder="Enter occupation"
            />
          </div>

          {/* Error Display */}
          {error && (
            <Alert variant="destructive">
              <AlertCircle />
              <AlertDescription>{error}</AlertDescription>
            </Alert>
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
