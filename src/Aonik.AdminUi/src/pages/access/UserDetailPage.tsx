import { useState, useEffect, useCallback, useRef } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs';
import { Breadcrumb } from '@/components/ui/breadcrumb';
import {
  RefreshCw,
  AlertCircle,
  TrendingUp,
  TrendingDown,
  ChevronDown,
  ChevronRight,
  Pencil,
  Download,
  Clock,
  Shield,
  Mail,
  Phone,
  MapPin,
  Camera,
  Trash2,
  Wrench,
  CheckCircle2,
  Loader2,
} from 'lucide-react';
import { userService } from '@/services/userService';
import { EditUserProfileDialog } from '@/components/dialogs/EditUserProfileDialog';
import type { AccessUserDetail, UpdateUserProfileRequest, UserDiagnosticResult } from '@/types';

// Detail Item Component
function DetailItem({ label, value }: { label: string; value: string }) {
  return (
    <div className="py-3 border-b border-[var(--color-border-light)] last:border-b-0">
      <p className="text-xs font-medium text-[var(--color-text-primary)] mb-0.5">{label}</p>
      <p className="text-sm text-[var(--color-text-secondary)]">{value}</p>
    </div>
  );
}

const statusStyles: Record<string, { text: string; bg: string }> = {
  Active: { text: 'text-[var(--color-success)]', bg: 'bg-[var(--color-success-light)]' },
  Invited: { text: 'text-[var(--color-warning)]', bg: 'bg-[var(--color-warning-light)]' },
  Pending: { text: 'text-[var(--color-warning)]', bg: 'bg-[var(--color-warning-light)]' },
  Deactivated: { text: 'text-[var(--color-text-tertiary)]', bg: 'bg-[var(--color-surface-inset)]' },
  Suspended: { text: 'text-[var(--color-error)]', bg: 'bg-[var(--color-error-light)]' },
};

export function UserDetailPage() {
  const navigate = useNavigate();
  const { userId } = useParams<{ userId: string }>();
  
  const [user, setUser] = useState<AccessUserDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState('overview');
  const [detailsExpanded, setDetailsExpanded] = useState(true);
  const [editDialogOpen, setEditDialogOpen] = useState(false);
  const [uploadingPhoto, setUploadingPhoto] = useState(false);
  const [photoError, setPhotoError] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [imageLoading, setImageLoading] = useState(false);
  const [imageError, setImageError] = useState(false);
  const [diagnostic, setDiagnostic] = useState<UserDiagnosticResult | null>(null);
  const [repairing, setRepairing] = useState(false);
  const [repairSuccess, setRepairSuccess] = useState<string[] | null>(null);

  const loadDiagnostic = useCallback(async (uid: string) => {
    try {
      const result = await userService.diagnose(uid);
      setDiagnostic(result);
    } catch {
      // Diagnostic is best-effort; don't block the page
    }
  }, []);

  const handleRepair = async () => {
    if (!userId) return;
    setRepairing(true);
    setRepairSuccess(null);
    try {
      const result = await userService.repair(userId);
      setRepairSuccess(result.repairsApplied);
      await loadUser();
      await loadDiagnostic(userId);
    } catch (err) {
      console.error('Repair failed:', err);
    } finally {
      setRepairing(false);
    }
  };

  const loadUser = useCallback(async () => {
    if (!userId) return;
    
    setLoading(true);
    setError(null);
    try {
      const data = await userService.get(userId);
      setUser(data);
    } catch (err: unknown) {
      console.error('Failed to load user:', err);
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setError(message || 'Failed to load user. Please try again.');
    } finally {
      setLoading(false);
    }
  }, [userId]);

  useEffect(() => {
    loadUser();
    if (userId) loadDiagnostic(userId);
  }, [loadUser, loadDiagnostic, userId]);

  const getInitials = (name?: string | null, email?: string) => {
    if (name) {
      return name.split(' ').map(n => n[0]).join('').toUpperCase();
    }
    return email?.charAt(0).toUpperCase() || 'U';
  };

  const handleSaveProfile = async (data: UpdateUserProfileRequest) => {
    if (!userId) return;
    try {
      await userService.updateProfile(userId, data);
      await loadUser(); // Reload user data to show updated values
    } catch (err) {
      // Error is handled by the dialog component
      throw err;
    }
  };

  const handlePhotoUpload = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file || !userId) return;

    // Validate file size (5MB)
    const maxSize = 5 * 1024 * 1024;
    if (file.size > maxSize) {
      setPhotoError('Image must be less than 5MB');
      return;
    }

    // Validate file type
    const allowedTypes = ['image/jpeg', 'image/jpg', 'image/png', 'image/gif', 'image/webp', 'image/bmp'];
    if (!allowedTypes.includes(file.type)) {
      setPhotoError('Please select a valid image file (JPG, PNG, GIF, WebP, or BMP)');
      return;
    }

    setPhotoError(null);
    setUploadingPhoto(true);
    setImageError(false);
    setImageLoading(true);

    try {
      await userService.uploadPhoto(userId, file);
      await loadUser(); // Reload to show new photo
    } catch (err: unknown) {
      console.error('Failed to upload photo:', err);
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setPhotoError(message || 'Failed to upload photo. Please try again.');
    } finally {
      setUploadingPhoto(false);
      if (fileInputRef.current) {
        fileInputRef.current.value = '';
      }
    }
  };

  const handleDeletePhoto = async () => {
    if (!userId || !user?.personProfile?.photoUrl) return;

    if (!confirm('Are you sure you want to delete this profile photo?')) {
      return;
    }

    setUploadingPhoto(true);
    setPhotoError(null);

    try {
      await userService.deletePhoto(userId);
      await loadUser(); // Reload to remove photo
    } catch (err: unknown) {
      console.error('Failed to delete photo:', err);
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setPhotoError(message || 'Failed to delete photo. Please try again.');
    } finally {
      setUploadingPhoto(false);
    }
  };

  const handleAvatarClick = () => {
    fileInputRef.current?.click();
  };

  const formatDate = (dateString?: string | null) => {
    if (!dateString) return 'Not provided';
    return new Date(dateString).toLocaleDateString('en-US', { 
      year: 'numeric', 
      month: 'short', 
      day: 'numeric' 
    });
  };

  const getFullName = () => {
    if (user?.personProfile) {
      const { firstName, lastName, title } = user.personProfile;
      const name = [firstName, lastName].filter(Boolean).join(' ');
      return title ? `${title} ${name}` : name;
    }
    return user?.displayName || user?.email || 'Unknown User';
  };

  const getPhotoUrl = (size: 'original' | 'medium' | 'small' | 'tiny' = 'small') => {
    if (!user?.personProfile) return null;
    
    // Select the appropriate thumbnail size
    let photoUrl: string | null | undefined;
    switch (size) {
      case 'original':
        photoUrl = user.personProfile.photoUrl;
        break;
      case 'medium':
        photoUrl = user.personProfile.photoUrlMedium || user.personProfile.photoUrl;
        break;
      case 'small':
        photoUrl = user.personProfile.photoUrlSmall || user.personProfile.photoUrl;
        break;
      case 'tiny':
        photoUrl = user.personProfile.photoUrlTiny || user.personProfile.photoUrl;
        break;
    }
    
    if (!photoUrl) return null;
    
    // Check if it's already a full URL (e.g., from CDN with PublicBaseUrl)
    if (photoUrl.startsWith('http')) {
      return photoUrl;
    }
    
    // For local storage, the URL is relative to the API (e.g., /storage/profiles/customers/...)
    const apiBaseUrl = import.meta.env.VITE_API_URL || 'https://localhost:5001';
    return `${apiBaseUrl}${photoUrl}`;
  };

  const breadcrumbItems = [
    { label: 'Home', href: '/' },
    { label: 'Users', href: '/access/users' },
  ];

  if (loading) {
    return (
      <div className="flex-1 flex items-center justify-center">
        <div className="text-center">
          <RefreshCw className="w-8 h-8 animate-spin mx-auto mb-3 text-[var(--color-brand-primary)]" />
          <p className="text-[var(--color-text-secondary)]">Loading user...</p>
        </div>
      </div>
    );
  }

  if (!user) {
    return (
      <div className="flex-1 flex items-center justify-center">
        <div className="text-center">
          <AlertCircle className="w-12 h-12 mx-auto mb-3 text-[var(--color-error)]" />
          <h2 className="text-xl font-semibold text-[var(--color-text-primary)] mb-2">User Not Found</h2>
          <p className="text-[var(--color-text-secondary)] mb-4">The user you're looking for doesn't exist or has been deleted.</p>
          <Button onClick={() => navigate('/access/users')}>
            Back to Users
          </Button>
        </div>
      </div>
    );
  }

  const statusStyle = statusStyles[user.status] ?? { text: 'text-[var(--color-text-secondary)]', bg: 'bg-[var(--color-surface-inset)]' };

  return (
    <div className="h-full overflow-auto bg-[var(--color-background)]">
      {/* Header */}
      <div className="px-6 py-4 flex items-center justify-between border-b border-[var(--color-border-light)] bg-[var(--color-surface)]">
        <div>
          <h1 className="text-lg font-semibold text-[var(--color-text-primary)]">User Details</h1>
          <Breadcrumb items={breadcrumbItems} className="mt-1" />
        </div>
        <div className="flex items-center gap-2">
          <Button variant="outline" size="sm">
            <Shield className="w-4 h-4 mr-2" />
            Filter
          </Button>
          <Button size="sm">
            Create
          </Button>
        </div>
      </div>

      {/* Error Alert */}
      {error && (
        <div className="px-6 pt-4">
          <Card className="border-[var(--color-error)] bg-[var(--color-error-light)]">
            <CardContent className="p-4 flex items-center gap-3 text-[var(--color-error)]">
              <AlertCircle className="w-5 h-5 flex-shrink-0" />
              <span className="flex-1">{error}</span>
              <Button variant="ghost" size="sm" onClick={loadUser}>
                Retry
              </Button>
            </CardContent>
          </Card>
        </div>
      )}

      {/* Diagnostic Banner */}
      {diagnostic?.hasIssues && (
        <div className="px-6 pt-4">
          <Card className="border-[var(--color-warning)] bg-[var(--color-warning-light)]">
            <CardContent className="p-4">
              <div className="flex items-start gap-3">
                <AlertCircle className="w-5 h-5 flex-shrink-0 text-[var(--color-warning)] mt-0.5" />
                <div className="flex-1 min-w-0">
                  <p className="text-sm font-medium text-[var(--color-text-primary)] mb-1">
                    {diagnostic.issues.length} issue{diagnostic.issues.length !== 1 ? 's' : ''} detected
                  </p>
                  <ul className="space-y-1">
                    {diagnostic.issues.map((issue) => (
                      <li key={issue.code} className="text-xs text-[var(--color-text-secondary)]">
                        {issue.description}
                      </li>
                    ))}
                  </ul>
                </div>
                {diagnostic.issues.some(i => i.repairable) && (
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={handleRepair}
                    disabled={repairing}
                    className="flex-shrink-0"
                  >
                    {repairing ? (
                      <Loader2 className="w-4 h-4 mr-1.5 animate-spin" />
                    ) : (
                      <Wrench className="w-4 h-4 mr-1.5" />
                    )}
                    {repairing ? 'Repairing...' : 'Repair'}
                  </Button>
                )}
              </div>
            </CardContent>
          </Card>
        </div>
      )}

      {/* Repair Success */}
      {repairSuccess && repairSuccess.length > 0 && !diagnostic?.hasIssues && (
        <div className="px-6 pt-4">
          <Card className="border-[var(--color-success)] bg-[var(--color-success-light)]">
            <CardContent className="p-4 flex items-center gap-3">
              <CheckCircle2 className="w-5 h-5 flex-shrink-0 text-[var(--color-success)]" />
              <div className="flex-1">
                <p className="text-sm font-medium text-[var(--color-text-primary)]">
                  Repairs applied successfully
                </p>
                <p className="text-xs text-[var(--color-text-secondary)]">
                  {repairSuccess.join('. ')}.
                </p>
              </div>
            </CardContent>
          </Card>
        </div>
      )}

      {/* Main Content */}
      <div className="p-6">
        <div className="flex gap-6">
          {/* Left Sidebar - Profile Card */}
          <div className="w-72 flex-shrink-0 space-y-6">
            {/* Profile Card */}
            <Card>
              <CardContent className="p-6">
                {/* Avatar & Name */}
                <div className="text-center mb-6">
                  <div className="relative inline-block mb-3 group">
                    <Avatar className="h-20 w-20 mx-auto relative">
                      {getPhotoUrl('small') && !imageError && (
                        <>
                          <AvatarImage 
                            src={getPhotoUrl('small')!} 
                            alt={getFullName()}
                            onLoad={() => {
                              setImageLoading(false);
                              setImageError(false);
                            }}
                            onError={() => {
                              setImageLoading(false);
                              setImageError(true);
                            }}
                            className={imageLoading ? 'opacity-0' : 'opacity-100 transition-opacity duration-200'}
                          />
                          {imageLoading && (
                            <div className="absolute inset-0 flex items-center justify-center bg-[var(--color-surface-inset)]">
                              <div className="w-8 h-8 border-2 border-[var(--color-border-light)] border-t-[var(--color-brand-primary)] rounded-full animate-spin" />
                            </div>
                          )}
                        </>
                      )}
                      <AvatarFallback className="text-xl">
                        {getInitials(getFullName(), user.email)}
                      </AvatarFallback>
                    </Avatar>
                    
                    {/* Photo Upload Overlay */}
                    <div 
                      className="absolute inset-0 bg-black bg-opacity-60 rounded-full flex items-center justify-center gap-2 opacity-0 group-hover:opacity-100 transition-opacity cursor-pointer"
                      onClick={(e) => e.stopPropagation()}
                    >
                      <button
                        onClick={handleAvatarClick}
                        disabled={uploadingPhoto}
                        className="p-2 bg-white rounded-full hover:bg-gray-100 transition-colors disabled:opacity-50"
                        title="Change photo"
                      >
                        {uploadingPhoto ? (
                          <RefreshCw className="w-4 h-4 text-gray-700 animate-spin" />
                        ) : (
                          <Camera className="w-4 h-4 text-gray-700" />
                        )}
                      </button>
                      {user.personProfile?.photoUrl && (
                        <button
                          onClick={handleDeletePhoto}
                          disabled={uploadingPhoto}
                          className="p-2 bg-white rounded-full hover:bg-[color-mix(in_srgb,var(--color-danger)_10%,transparent)] transition-colors disabled:opacity-50"
                          title="Delete photo"
                        >
                          <Trash2 className="w-4 h-4 text-[var(--color-danger)]" />
                        </button>
                      )}
                    </div>
                    
                    {/* Hidden file input */}
                    <input
                      ref={fileInputRef}
                      type="file"
                      accept="image/jpeg,image/jpg,image/png,image/gif,image/webp,image/bmp"
                      onChange={handlePhotoUpload}
                      className="hidden"
                    />
                    
                    {user.status === 'Active' && (
                      <span className="absolute bottom-0 right-0 w-4 h-4 bg-[var(--color-success)] border-2 border-white rounded-full" />
                    )}
                  </div>
                  
                  {/* Photo Error Message */}
                  {photoError && (
                    <div className="mt-2 text-xs text-[var(--color-error)] bg-[var(--color-error-light)] px-3 py-2 rounded">
                      {photoError}
                    </div>
                  )}
                  
                  <h2 className="text-lg font-semibold text-[var(--color-text-primary)]">
                    {getFullName()}
                  </h2>
                  <p className="text-sm text-[var(--color-text-tertiary)]">
                    {user.personProfile?.occupation || user.partyType || 'User'}
                  </p>
                </div>

                {/* Stats */}
                <div className="flex justify-center gap-6 mb-6 pb-6 border-b border-[var(--color-border-light)]">
                  <div className="text-center">
                    <div className="flex items-center gap-1 justify-center">
                      <span className="text-lg font-semibold text-[var(--color-text-primary)]">
                        {user.roles?.length || 0}
                      </span>
                      <TrendingUp className="w-3 h-3 text-[var(--color-success)]" />
                    </div>
                    <p className="text-xs text-[var(--color-text-tertiary)]">Roles</p>
                  </div>
                  <div className="text-center">
                    <div className="flex items-center gap-1 justify-center">
                      <span className="text-lg font-semibold text-[var(--color-text-primary)]">
                        {user.permissions?.length || 0}
                      </span>
                      <TrendingDown className="w-3 h-3 text-[var(--color-error)]" />
                    </div>
                    <p className="text-xs text-[var(--color-text-tertiary)]">Permissions</p>
                  </div>
                  <div className="text-center">
                    <div className="flex items-center gap-1 justify-center">
                      <span className="text-lg font-semibold text-[var(--color-text-primary)]">
                        {user.lastLoginAt ? '1' : '0'}
                      </span>
                      <TrendingUp className="w-3 h-3 text-[var(--color-success)]" />
                    </div>
                    <p className="text-xs text-[var(--color-text-tertiary)]">Logins</p>
                  </div>
                </div>

                {/* Details Section */}
                <div>
                  <div className="flex items-center justify-between mb-2">
                    <div 
                      className="flex items-center gap-2 cursor-pointer" 
                      onClick={() => setDetailsExpanded(!detailsExpanded)}
                    >
                      <span className="text-sm font-medium text-[var(--color-text-primary)]">Details</span>
                      {detailsExpanded ? (
                        <ChevronDown className="w-4 h-4 text-[var(--color-text-tertiary)]" />
                      ) : (
                        <ChevronRight className="w-4 h-4 text-[var(--color-text-tertiary)]" />
                      )}
                    </div>
                    {user.personProfile && (
                      <Button 
                        variant="outline" 
                        size="sm" 
                        className="h-7 text-xs"
                        onClick={() => setEditDialogOpen(true)}
                      >
                        <Pencil className="w-3 h-3 mr-1" />
                        Edit
                      </Button>
                    )}
                  </div>

                  {detailsExpanded && (
                    <div>
                      <div className="mb-3">
                        <Badge className={`${statusStyle.bg} ${statusStyle.text} text-xs`}>
                          {user.status === 'Active' ? 'Premium user' : user.status}
                        </Badge>
                        {user.personProfile?.idvStatus && (
                          <Badge className="ml-2 bg-[var(--color-info-light)] text-[var(--color-info)] text-xs">
                            IDV: {user.personProfile.idvStatus}
                          </Badge>
                        )}
                      </div>

                      <DetailItem label="User ID" value={user.userId.substring(0, 12)} />
                      <DetailItem label="Party ID" value={user.partyId?.substring(0, 12) || 'N/A'} />
                      <DetailItem label="Email" value={user.email} />
                      {user.personProfile && (
                        <>
                          {user.personProfile.dob && (
                            <DetailItem label="Date of Birth" value={formatDate(user.personProfile.dob)} />
                          )}
                          {user.personProfile.nationality && (
                            <DetailItem label="Nationality" value={user.personProfile.nationality} />
                          )}
                          {user.personProfile.occupation && (
                            <DetailItem label="Occupation" value={user.personProfile.occupation} />
                          )}
                          {user.personProfile.countryCode && (
                            <DetailItem label="Country" value={user.personProfile.countryCode} />
                          )}
                        </>
                      )}
                      <DetailItem label="Last Login" value={formatDate(user.lastLoginAt)} />
                    </div>
                  )}
                </div>

                {/* Contact Information */}
                {user.contacts && user.contacts.length > 0 && (
                  <div className="mt-6 pt-6 border-t border-[var(--color-border-light)]">
                    <h3 className="text-sm font-medium text-[var(--color-text-primary)] mb-3">Contact Information</h3>
                    <div className="space-y-2">
                      {user.contacts.map(contact => (
                        <div key={contact.contactId} className="flex items-start gap-2 text-sm">
                          {contact.type === 'Email' ? (
                            <Mail className="w-4 h-4 text-[var(--color-text-tertiary)] mt-0.5" />
                          ) : (
                            <Phone className="w-4 h-4 text-[var(--color-text-tertiary)] mt-0.5" />
                          )}
                          <div className="flex-1 min-w-0">
                            <div className="flex items-center gap-2">
                              <span className="text-[var(--color-text-secondary)] truncate">{contact.value}</span>
                              {contact.isPrimary && (
                                <Badge className="bg-[var(--color-brand-primary-light)] text-[var(--color-brand-primary)] text-xs">
                                  Primary
                                </Badge>
                              )}
                            </div>
                            <span className="text-xs text-[var(--color-text-tertiary)]">{contact.type}</span>
                          </div>
                        </div>
                      ))}
                    </div>
                  </div>
                )}

                {/* Addresses */}
                {user.addresses && user.addresses.length > 0 && (
                  <div className="mt-6 pt-6 border-t border-[var(--color-border-light)]">
                    <h3 className="text-sm font-medium text-[var(--color-text-primary)] mb-3">Addresses</h3>
                    <div className="space-y-3">
                      {user.addresses.map(address => (
                        <div key={address.addressId} className="flex items-start gap-2 text-sm">
                          <MapPin className="w-4 h-4 text-[var(--color-text-tertiary)] mt-0.5" />
                          <div className="flex-1 min-w-0">
                            <div className="font-medium text-[var(--color-text-primary)] mb-1">{address.type}</div>
                            <div className="text-[var(--color-text-secondary)] text-xs space-y-0.5">
                              {address.line1 && <div>{address.line1}</div>}
                              {address.line2 && <div>{address.line2}</div>}
                              {address.line3 && <div>{address.line3}</div>}
                              <div>
                                {[address.city, address.state, address.postcode].filter(Boolean).join(', ')}
                              </div>
                              {address.country && <div>{address.country}</div>}
                            </div>
                          </div>
                        </div>
                      ))}
                    </div>
                  </div>
                )}
              </CardContent>
            </Card>
          </div>

          {/* Right Content - Tabs */}
          <div className="flex-1 min-w-0">
            <Card>
              <CardContent className="p-0">
                <Tabs value={activeTab} onValueChange={setActiveTab}>
                  {/* Tabs Header */}
                  <div className="flex items-center justify-between border-b border-[var(--color-border-light)] px-4">
                    <TabsList className="bg-transparent p-0 h-auto gap-0">
                      <TabsTrigger
                        value="overview"
                        className="px-4 py-3 text-sm rounded-none border-b-2 border-transparent data-[state=active]:border-[var(--color-brand-primary)] data-[state=active]:bg-transparent data-[state=active]:text-[var(--color-brand-primary)]"
                      >
                        Overview
                      </TabsTrigger>
                      <TabsTrigger
                        value="events"
                        className="px-4 py-3 text-sm rounded-none border-b-2 border-transparent data-[state=active]:border-[var(--color-brand-primary)] data-[state=active]:bg-transparent data-[state=active]:text-[var(--color-brand-primary)]"
                      >
                        Events & Logs
                      </TabsTrigger>
                      <TabsTrigger
                        value="statements"
                        className="px-4 py-3 text-sm rounded-none border-b-2 border-transparent data-[state=active]:border-[var(--color-brand-primary)] data-[state=active]:bg-transparent data-[state=active]:text-[var(--color-brand-primary)]"
                      >
                        Statements
                      </TabsTrigger>
                    </TabsList>
                    <Button size="sm">
                      Actions <ChevronDown className="w-4 h-4 ml-2" />
                    </Button>
                  </div>

                  {/* Tab Content */}
                  <div className="p-6">
                    <TabsContent value="overview" className="mt-0">
                    {/* User Profile Summary */}
                    <div className="mb-8">
                      <h3 className="text-base font-semibold text-[var(--color-text-primary)] mb-4">Profile Summary</h3>
                      <div className="grid grid-cols-2 gap-6">
                        {/* Personal Information */}
                        <Card>
                          <CardHeader>
                            <CardTitle className="text-sm">Personal Information</CardTitle>
                          </CardHeader>
                          <CardContent>
                            {user.personProfile ? (
                              <div className="space-y-2 text-sm">
                                <div className="flex justify-between">
                                  <span className="text-[var(--color-text-tertiary)]">Full Name</span>
                                  <span className="text-[var(--color-text-primary)] font-medium">{getFullName()}</span>
                                </div>
                                {user.personProfile.dob && (
                                  <div className="flex justify-between">
                                    <span className="text-[var(--color-text-tertiary)]">Date of Birth</span>
                                    <span className="text-[var(--color-text-primary)]">{formatDate(user.personProfile.dob)}</span>
                                  </div>
                                )}
                                {user.personProfile.nationality && (
                                  <div className="flex justify-between">
                                    <span className="text-[var(--color-text-tertiary)]">Nationality</span>
                                    <span className="text-[var(--color-text-primary)]">{user.personProfile.nationality}</span>
                                  </div>
                                )}
                                {user.personProfile.occupation && (
                                  <div className="flex justify-between">
                                    <span className="text-[var(--color-text-tertiary)]">Occupation</span>
                                    <span className="text-[var(--color-text-primary)]">{user.personProfile.occupation}</span>
                                  </div>
                                )}
                                {user.personProfile.idvStatus && (
                                  <div className="flex justify-between">
                                    <span className="text-[var(--color-text-tertiary)]">Verification</span>
                                    <Badge className="bg-[var(--color-info-light)] text-[var(--color-info)] text-xs">
                                      {user.personProfile.idvStatus}
                                    </Badge>
                                  </div>
                                )}
                              </div>
                            ) : user.businessProfile ? (
                              <div className="space-y-2 text-sm">
                                <div className="flex justify-between">
                                  <span className="text-[var(--color-text-tertiary)]">Business Name</span>
                                  <span className="text-[var(--color-text-primary)] font-medium">{user.displayName}</span>
                                </div>
                                {user.businessProfile.registrationNumber && (
                                  <div className="flex justify-between">
                                    <span className="text-[var(--color-text-tertiary)]">Registration No.</span>
                                    <span className="text-[var(--color-text-primary)]">{user.businessProfile.registrationNumber}</span>
                                  </div>
                                )}
                                {user.businessProfile.incorporationCountry && (
                                  <div className="flex justify-between">
                                    <span className="text-[var(--color-text-tertiary)]">Country</span>
                                    <span className="text-[var(--color-text-primary)]">{user.businessProfile.incorporationCountry}</span>
                                  </div>
                                )}
                                {user.businessProfile.industry && (
                                  <div className="flex justify-between">
                                    <span className="text-[var(--color-text-tertiary)]">Industry</span>
                                    <span className="text-[var(--color-text-primary)]">{user.businessProfile.industry}</span>
                                  </div>
                                )}
                                {user.businessProfile.kybStatus && (
                                  <div className="flex justify-between">
                                    <span className="text-[var(--color-text-tertiary)]">Verification</span>
                                    <Badge className="bg-[var(--color-info-light)] text-[var(--color-info)] text-xs">
                                      {user.businessProfile.kybStatus}
                                    </Badge>
                                  </div>
                                )}
                              </div>
                            ) : (
                              <div className="text-center py-6 text-[var(--color-text-tertiary)] text-sm">
                                No profile information available
                              </div>
                            )}
                          </CardContent>
                        </Card>

                        {/* Account Information */}
                        <Card>
                          <CardHeader>
                            <CardTitle className="text-sm">Account Information</CardTitle>
                          </CardHeader>
                          <CardContent>
                            <div className="space-y-2 text-sm">
                              <div className="flex justify-between">
                                <span className="text-[var(--color-text-tertiary)]">Status</span>
                                <Badge className={`${statusStyle.bg} ${statusStyle.text} text-xs`}>
                                  {user.status}
                                </Badge>
                              </div>
                              <div className="flex justify-between">
                                <span className="text-[var(--color-text-tertiary)]">Party Type</span>
                                <span className="text-[var(--color-text-primary)]">{user.partyType || 'N/A'}</span>
                              </div>
                              <div className="flex justify-between">
                                <span className="text-[var(--color-text-tertiary)]">Email</span>
                                <span className="text-[var(--color-text-primary)]">{user.email}</span>
                              </div>
                              <div className="flex justify-between">
                                <span className="text-[var(--color-text-tertiary)]">Last Login</span>
                                <span className="text-[var(--color-text-primary)]">{formatDate(user.lastLoginAt)}</span>
                              </div>
                              <div className="flex justify-between">
                                <span className="text-[var(--color-text-tertiary)]">Roles</span>
                                <span className="text-[var(--color-text-primary)]">{user.roles?.length || 0}</span>
                              </div>
                              <div className="flex justify-between">
                                <span className="text-[var(--color-text-tertiary)]">Permissions</span>
                                <span className="text-[var(--color-text-primary)]">{user.permissions?.length || 0}</span>
                              </div>
                            </div>
                          </CardContent>
                        </Card>
                      </div>
                    </div>

                    {/* Roles & Permissions */}
                    {(user.roles && user.roles.length > 0) && (
                      <div className="mb-8">
                        <h3 className="text-base font-semibold text-[var(--color-text-primary)] mb-4">Roles</h3>
                        <div className="flex flex-wrap gap-2">
                          {user.roles.map((role) => (
                            <Badge key={role.roleId} className="bg-[var(--color-surface-inset)] text-[var(--color-text-primary)]">
                              {role.name}
                            </Badge>
                          ))}
                        </div>
                      </div>
                    )}
                  </TabsContent>

                  <TabsContent value="events" className="mt-0">
                    <div className="text-center py-12">
                      <Clock className="w-12 h-12 mx-auto mb-3 text-[var(--color-text-tertiary)]" />
                      <h3 className="text-lg font-medium text-[var(--color-text-primary)] mb-2">Events & Logs</h3>
                      <p className="text-[var(--color-text-secondary)]">User activity and event logs will appear here.</p>
                    </div>
                  </TabsContent>

                    <TabsContent value="statements" className="mt-0">
                      <div className="text-center py-12">
                        <Download className="w-12 h-12 mx-auto mb-3 text-[var(--color-text-tertiary)]" />
                        <h3 className="text-lg font-medium text-[var(--color-text-primary)] mb-2">Statements</h3>
                        <p className="text-[var(--color-text-secondary)]">Account statements will appear here.</p>
                      </div>
                    </TabsContent>
                  </div>
                </Tabs>
              </CardContent>
            </Card>
          </div>
        </div>
      </div>

      {/* Edit Profile Dialog */}
      <EditUserProfileDialog
        open={editDialogOpen}
        onOpenChange={setEditDialogOpen}
        profile={user?.personProfile}
        onSave={handleSaveProfile}
      />
    </div>
  );
}
