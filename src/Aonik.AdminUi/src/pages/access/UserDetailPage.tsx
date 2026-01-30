import { useState, useEffect, useCallback } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/avatar';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Breadcrumb } from '@/components/ui/breadcrumb';
import {
  ArrowLeft,
  RefreshCw,
  AlertCircle,
  CheckCircle,
  User,
  UsersRound,
  MapPin,
  Mail,
  MoreHorizontal,
  TrendingUp,
  TrendingDown,
  Briefcase,
  Bold,
  Italic,
  Underline,
  Strikethrough,
  Image,
  Link,
  Type,
  Paperclip,
  Smile,
  MessageCircle,
  Heart,
} from 'lucide-react';
import { userService } from '@/services/userService';
import type { AccessUserDetail } from '@/types';

// Mock data for demonstration
const mockActivities = [
  { id: '1', time: '08:42', type: 'info', color: 'yellow', text: 'Outlines keep you honest. And keep structure' },
  { id: '2', time: '10:00', type: 'success', color: 'green', text: 'AEOL meeting' },
  { id: '3', time: '14:37', type: 'error', color: 'red', text: 'Make deposit USD 700. to ESL', highlight: 'USD 700.' },
  { id: '4', time: '16:50', type: 'info', color: 'blue', text: 'Indulging in poorly driving and keep structure keep great' },
  { id: '5', time: '21:03', type: 'error', color: 'red', text: 'New order placed #XF-2356.', link: '#XF-2356' },
  { id: '6', time: '16:50', type: 'info', color: 'blue', text: 'Indulging in poorly driving and keep structure keep great' },
  { id: '7', time: '21:03', type: 'error', color: 'red', text: 'New order placed #XF-2356.', link: '#XF-2356' },
  { id: '8', time: '10:30', type: 'success', color: 'green', text: 'Finance KPI Mobile app launch preparion meeting' },
];

const mockPosts = [
  {
    id: '1',
    author: 'Carles Nilson',
    avatar: null,
    timestamp: 'Yesterday at 5:06 PM',
    content: 'Outlines keep you honest. They stop you from indulging in poorly thought-out metaphors about driving and keep you focused on the overall structure of your post',
    likes: 12,
    comments: 150,
    replies: [
      {
        id: 'r1',
        author: 'Alice Danchik',
        avatar: null,
        timestamp: '1 day',
        content: 'Long before you sit dow to put digital pen to paper you need to make sure you have to sit down and write.',
      },
      {
        id: 'r2',
        author: 'Harris Bold',
        avatar: null,
        timestamp: '2 days',
        content: 'Outlines keep you honest. They stop you from indulging in poorly',
      },
    ],
  },
  {
    id: '2',
    author: 'Carles Nilson',
    avatar: null,
    timestamp: 'Last week at 10:00 PM',
    content: 'Outlines keep you honest. They stop you from indulging in poorly thought-out metaphors about driving and keep you focused on the overall structure of your post',
    likes: 22,
    comments: 59,
    replies: [],
  },
];

const mockStats = [
  { month: 'Jan', value: 45 },
  { month: 'Feb', value: 75 },
  { month: 'Mar', value: 60 },
  { month: 'Apr', value: 85 },
  { month: 'May', value: 95 },
  { month: 'Jun', value: 70 },
  { month: 'Jul', value: 80 },
  { month: 'Aug', value: 90 },
  { month: 'Sep', value: 100 },
  { month: 'Oct', value: 55 },
  { month: 'Nov', value: 50 },
  { month: 'Dec', value: 45 },
];

const statusStyles: Record<string, { text: string; bg: string }> = {
  Active: { text: 'text-[var(--color-success)]', bg: 'bg-[var(--color-success-light)]' },
  Invited: { text: 'text-[var(--color-warning)]', bg: 'bg-[var(--color-warning-light)]' },
  Pending: { text: 'text-[var(--color-warning)]', bg: 'bg-[var(--color-warning-light)]' },
  Deactivated: { text: 'text-[var(--color-text-tertiary)]', bg: 'bg-[var(--color-surface-inset)]' },
  Suspended: { text: 'text-[var(--color-error)]', bg: 'bg-[var(--color-error-light)]' },
};

// Simple bar chart component
function SimpleBarChart({ data }: { data: { month: string; value: number }[] }) {
  const maxValue = Math.max(...data.map(d => d.value));
  
  return (
    <div className="flex items-end gap-2 h-40">
      {data.map((item, index) => (
        <div key={index} className="flex-1 flex flex-col items-center gap-1">
          <div
            className="w-full bg-[var(--color-brand-primary)] rounded-t-sm transition-all hover:bg-[var(--color-brand-primary-hover)]"
            style={{ height: `${(item.value / maxValue) * 100}%`, minHeight: '4px' }}
          />
        </div>
      ))}
    </div>
  );
}

// Progress bar component
function ProgressBar({ value, max = 100 }: { value: number; max?: number }) {
  const percentage = Math.min((value / max) * 100, 100);
  
  return (
    <div className="flex items-center gap-3 w-full">
      <span className="text-sm text-[var(--color-text-secondary)] whitespace-nowrap">Profile Completion</span>
      <div className="flex-1 h-1.5 bg-[var(--color-surface-inset)] rounded-full overflow-hidden">
        <div
          className="h-full bg-[var(--color-brand-secondary)] rounded-full transition-all"
          style={{ width: `${percentage}%` }}
        />
      </div>
      <span className="text-sm font-medium text-[var(--color-text-primary)]">{value}%</span>
    </div>
  );
}

// Activity dot colors
const activityColors: Record<string, string> = {
  yellow: 'bg-[var(--color-warning)]',
  green: 'bg-[var(--color-success)]',
  red: 'bg-[var(--color-error)]',
  blue: 'bg-[var(--color-brand-primary)]',
};

// Post Card Component
function PostCard({ post }: { post: typeof mockPosts[0] }) {
  const getInitials = (name: string) => {
    return name.split(' ').map(n => n[0]).join('').toUpperCase();
  };

  return (
    <Card className="mb-4">
      <CardContent className="p-4">
        {/* Post Header */}
        <div className="flex items-start justify-between mb-3">
          <div className="flex items-center gap-3">
            <Avatar className="h-10 w-10">
              {post.avatar ? (
                <AvatarImage src={post.avatar} alt={post.author} />
              ) : (
                <AvatarFallback>{getInitials(post.author)}</AvatarFallback>
              )}
            </Avatar>
            <div>
              <p className="font-medium text-[var(--color-text-primary)]">{post.author}</p>
              <p className="text-xs text-[var(--color-text-tertiary)]">{post.timestamp}</p>
            </div>
          </div>
          <Button variant="ghost" size="sm">
            <MoreHorizontal className="w-4 h-4" />
          </Button>
        </div>

        {/* Post Content */}
        <p className="text-sm text-[var(--color-text-secondary)] mb-4">{post.content}</p>

        {/* Post Actions */}
        <div className="flex items-center gap-4 mb-4">
          <button className="flex items-center gap-1.5 text-sm text-[var(--color-text-tertiary)] hover:text-[var(--color-text-secondary)]">
            <MessageCircle className="w-4 h-4" />
            <span>{post.likes}</span>
          </button>
          <button className="flex items-center gap-1.5 text-sm text-[var(--color-text-tertiary)] hover:text-[var(--color-text-secondary)]">
            <Heart className="w-4 h-4" />
            <span>{post.comments}</span>
          </button>
        </div>

        {/* Replies */}
        {post.replies.length > 0 && (
          <div className="border-t border-[var(--color-border-light)] pt-4 space-y-4">
            {post.replies.map(reply => (
              <div key={reply.id} className="flex gap-3">
                <Avatar className="h-8 w-8">
                  {reply.avatar ? (
                    <AvatarImage src={reply.avatar} alt={reply.author} />
                  ) : (
                    <AvatarFallback className="text-xs">{getInitials(reply.author)}</AvatarFallback>
                  )}
                </Avatar>
                <div className="flex-1">
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-2">
                      <span className="font-medium text-sm text-[var(--color-text-primary)]">{reply.author}</span>
                      <span className="text-xs text-[var(--color-text-tertiary)]">{reply.timestamp}</span>
                    </div>
                    <button className="text-sm text-[var(--color-brand-primary)] hover:underline">Reply</button>
                  </div>
                  <p className="text-sm text-[var(--color-text-secondary)] mt-1">{reply.content}</p>
                </div>
              </div>
            ))}
          </div>
        )}

        {/* Reply Input */}
        <div className="border-t border-[var(--color-border-light)] pt-4 mt-4">
          <div className="flex items-center gap-3">
            <input
              type="text"
              placeholder="Reply.."
              className="flex-1 px-3 py-2 text-sm border border-[var(--color-border)] rounded-md bg-[var(--color-surface)] focus:outline-none focus:ring-2 focus:ring-[var(--color-brand-primary)] focus:border-transparent"
            />
            <Button variant="ghost" size="sm">
              <Paperclip className="w-4 h-4" />
            </Button>
            <Button variant="ghost" size="sm">
              <Smile className="w-4 h-4" />
            </Button>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}

// Post Composer Component
function PostComposer({ user }: { user: AccessUserDetail }) {
  const getInitials = (name?: string | null, email?: string) => {
    if (name) {
      return name.split(' ').map(n => n[0]).join('').toUpperCase();
    }
    return email?.charAt(0).toUpperCase() || 'U';
  };

  return (
    <Card className="mb-4">
      <CardContent className="p-4">
        <div className="flex gap-3">
          <Avatar className="h-10 w-10">
            <AvatarFallback>{getInitials(user.displayName, user.email)}</AvatarFallback>
          </Avatar>
          <div className="flex-1">
            <div className="mb-2">
              <p className="font-medium text-[var(--color-text-primary)]">{user.displayName || user.email}</p>
              <p className="text-xs text-[var(--color-text-tertiary)]">
                {user.roles?.map(r => r.name).join(', ') || 'No roles assigned'}
              </p>
            </div>
            <textarea
              placeholder="What is on your mind ?"
              className="w-full px-3 py-2 text-sm border border-[var(--color-border)] rounded-md bg-[var(--color-surface)] focus:outline-none focus:ring-2 focus:ring-[var(--color-brand-primary)] focus:border-transparent resize-none"
              rows={2}
            />
            <div className="flex items-center justify-between mt-3">
              <div className="flex items-center gap-1">
                <select className="px-2 py-1 text-xs border border-[var(--color-border)] rounded bg-[var(--color-surface)] text-[var(--color-text-secondary)]">
                  <option>Normal</option>
                  <option>Heading 1</option>
                  <option>Heading 2</option>
                </select>
                <Button variant="ghost" size="sm" className="h-7 w-7 p-0">
                  <Bold className="w-3.5 h-3.5" />
                </Button>
                <Button variant="ghost" size="sm" className="h-7 w-7 p-0">
                  <Italic className="w-3.5 h-3.5" />
                </Button>
                <Button variant="ghost" size="sm" className="h-7 w-7 p-0">
                  <Underline className="w-3.5 h-3.5" />
                </Button>
                <Button variant="ghost" size="sm" className="h-7 w-7 p-0">
                  <Strikethrough className="w-3.5 h-3.5" />
                </Button>
                <Button variant="ghost" size="sm" className="h-7 w-7 p-0">
                  <Image className="w-3.5 h-3.5" />
                </Button>
                <Button variant="ghost" size="sm" className="h-7 w-7 p-0">
                  <Link className="w-3.5 h-3.5" />
                </Button>
                <Button variant="ghost" size="sm" className="h-7 w-7 p-0">
                  <Type className="w-3.5 h-3.5" />
                </Button>
              </div>
              <div className="flex items-center gap-2">
                <Button variant="ghost" size="sm" className="h-7 w-7 p-0">
                  <Paperclip className="w-3.5 h-3.5" />
                </Button>
                <Button variant="ghost" size="sm" className="h-7 w-7 p-0">
                  <Smile className="w-3.5 h-3.5" />
                </Button>
              </div>
            </div>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}

export function UserDetailPage() {
  const navigate = useNavigate();
  const { userId } = useParams<{ userId: string }>();
  
  const [user, setUser] = useState<AccessUserDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState('overview');

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
  }, [loadUser]);

  const getInitials = (name?: string | null, email?: string) => {
    if (name) {
      return name.split(' ').map(n => n[0]).join('').toUpperCase();
    }
    return email?.charAt(0).toUpperCase() || 'U';
  };

  const breadcrumbItems = [
    { label: 'Users & Access', href: '/access', icon: <UsersRound className="w-3.5 h-3.5" /> },
    { label: 'Users', href: '/access/users', icon: <User className="w-3.5 h-3.5" /> },
    { label: user?.displayName || user?.email || 'User Details' },
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
            <ArrowLeft className="w-4 h-4 mr-2" />
            Back to Users
          </Button>
        </div>
      </div>
    );
  }

  const statusStyle = statusStyles[user.status] ?? { text: 'text-[var(--color-text-secondary)]', bg: 'bg-[var(--color-surface-inset)]' };

  // Calculate profile completion (mock calculation)
  const profileCompletion = 50;

  // Format the created date for display
  const memberSince = user.createdAt ? new Date(user.createdAt).toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  }) : 'Unknown';

  return (
    <div className="h-full overflow-auto">
      {/* Breadcrumb */}
      <div className="px-6 pt-6">
        <Breadcrumb items={breadcrumbItems} className="mb-4" />
      </div>

      {/* Error Alert */}
      {error && (
        <div className="px-6">
          <Card className="mb-6 border-[var(--color-error)] bg-[var(--color-error-light)]">
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

      {/* Profile Header Card */}
      <div className="px-6 pb-4">
        <Card>
          <CardContent className="p-6">
            {/* Top Section: Avatar, Info, Actions */}
            <div className="flex items-start gap-6">
              {/* Avatar */}
              <div className="relative">
                <Avatar className="h-24 w-24">
                  <AvatarFallback className="text-2xl">
                    {getInitials(user.displayName, user.email)}
                  </AvatarFallback>
                </Avatar>
                {/* Online indicator */}
                {user.status === 'Active' && (
                  <span className="absolute bottom-1 right-1 w-4 h-4 bg-[var(--color-success)] border-2 border-white rounded-full" />
                )}
              </div>

              {/* User Info */}
              <div className="flex-1">
                <div className="flex items-center gap-3 mb-1">
                  <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">
                    {user.displayName || user.email}
                  </h1>
                  {user.status === 'Active' && (
                    <CheckCircle className="w-5 h-5 text-[var(--color-brand-primary)]" />
                  )}
                  <span className={`inline-flex items-center px-2.5 py-1 rounded-full text-xs font-medium ${statusStyle.bg} ${statusStyle.text}`}>
                    {user.status}
                  </span>
                </div>
                
                <div className="flex items-center gap-4 text-sm text-[var(--color-text-secondary)] mb-4">
                  {user.partyType && (
                    <span className="flex items-center gap-1">
                      <Briefcase className="w-4 h-4" />
                      {user.partyType}
                    </span>
                  )}
                  {user.partyDisplayName && (
                    <span className="flex items-center gap-1">
                      <MapPin className="w-4 h-4" />
                      {user.partyDisplayName}
                    </span>
                  )}
                  <span className="flex items-center gap-1">
                    <Mail className="w-4 h-4" />
                    {user.email}
                  </span>
                  <span className="text-[var(--color-text-tertiary)]">
                    Member since {memberSince}
                  </span>
                </div>

                {/* Stats */}
                <div className="flex items-center gap-6">
                  <div className="flex items-center gap-2">
                    <TrendingUp className="w-4 h-4 text-[var(--color-success)]" />
                    <span className="text-lg font-semibold text-[var(--color-text-primary)]">
                      {user.roles?.length || 0}
                    </span>
                    <span className="text-sm text-[var(--color-text-secondary)]">Roles</span>
                  </div>
                  <div className="flex items-center gap-2">
                    <TrendingDown className="w-4 h-4 text-[var(--color-error)]" />
                    <span className="text-lg font-semibold text-[var(--color-text-primary)]">
                      {user.permissions?.length || 0}
                    </span>
                    <span className="text-sm text-[var(--color-text-secondary)]">Permissions</span>
                  </div>
                  <div className="flex items-center gap-2">
                    <TrendingUp className="w-4 h-4 text-[var(--color-success)]" />
                    <span className="text-lg font-semibold text-[var(--color-text-primary)]">
                      {user.lastLoginAt ? '1' : '0'}
                    </span>
                    <span className="text-sm text-[var(--color-text-secondary)]">Logins</span>
                  </div>
                </div>
              </div>

              {/* Actions & Profile Completion */}
              <div className="flex flex-col items-end gap-4">
                <div className="flex items-center gap-2">
                  <Button variant="outline">Follow</Button>
                  <Button>Hire Me</Button>
                  <Button variant="ghost" size="sm">
                    <MoreHorizontal className="w-4 h-4" />
                  </Button>
                </div>
                <div className="w-64">
                  <ProgressBar value={profileCompletion} />
                </div>
              </div>
            </div>

            {/* Tabs */}
            <div className="mt-6 border-t border-[var(--color-border-light)] pt-4">
              <Tabs value={activeTab} onValueChange={setActiveTab}>
                <TabsList className="bg-transparent p-0 gap-0">
                  <TabsTrigger
                    value="overview"
                    className="px-4 py-2 text-sm data-[state=active]:border-b-2 data-[state=active]:border-[var(--color-brand-primary)] data-[state=active]:bg-transparent rounded-none"
                  >
                    Overview
                  </TabsTrigger>
                  <TabsTrigger
                    value="projects"
                    className="px-4 py-2 text-sm data-[state=active]:border-b-2 data-[state=active]:border-[var(--color-brand-primary)] data-[state=active]:bg-transparent rounded-none"
                  >
                    Projects
                  </TabsTrigger>
                  <TabsTrigger
                    value="campaigns"
                    className="px-4 py-2 text-sm data-[state=active]:border-b-2 data-[state=active]:border-[var(--color-brand-primary)] data-[state=active]:bg-transparent rounded-none"
                  >
                    Campaigns
                  </TabsTrigger>
                  <TabsTrigger
                    value="documents"
                    className="px-4 py-2 text-sm data-[state=active]:border-b-2 data-[state=active]:border-[var(--color-brand-primary)] data-[state=active]:bg-transparent rounded-none"
                  >
                    Documents
                  </TabsTrigger>
                  <TabsTrigger
                    value="followers"
                    className="px-4 py-2 text-sm data-[state=active]:border-b-2 data-[state=active]:border-[var(--color-brand-primary)] data-[state=active]:bg-transparent rounded-none"
                  >
                    Followers
                  </TabsTrigger>
                  <TabsTrigger
                    value="activity"
                    className="px-4 py-2 text-sm data-[state=active]:border-b-2 data-[state=active]:border-[var(--color-brand-primary)] data-[state=active]:bg-transparent rounded-none"
                  >
                    Activity
                  </TabsTrigger>
                </TabsList>
              </Tabs>
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Main Content Area */}
      <div className="px-6 pb-6">
        <div className="grid grid-cols-1 lg:grid-cols-5 gap-6">
          {/* Left Column - Posts/Feed */}
          <div className="lg:col-span-3 space-y-4">
            {/* Post Composer */}
            <PostComposer user={user} />

            {/* Feed placeholder for other users */}
            <Card className="mb-4">
              <CardContent className="p-4">
                <div className="flex items-start justify-between mb-3">
                  <div className="flex items-center gap-3">
                    <Avatar className="h-10 w-10">
                      <AvatarFallback>NL</AvatarFallback>
                    </Avatar>
                    <div>
                      <p className="font-medium text-[var(--color-text-primary)]">Nick Logan</p>
                      <p className="text-xs text-[var(--color-text-tertiary)]">PHP, SQLite, Artisan CLI</p>
                    </div>
                  </div>
                  <Button variant="ghost" size="sm">
                    <MoreHorizontal className="w-4 h-4" />
                  </Button>
                </div>
                <p className="text-sm text-[var(--color-text-secondary)]">
                  Outlines keep you honest. They stop you from indulging in poorly thought-out metaphors about driving and keep you focused on the overall structure of your post
                </p>
              </CardContent>
            </Card>

            {/* Posts */}
            {mockPosts.map(post => (
              <PostCard key={post.id} post={post} />
            ))}

            {/* Load More Button */}
            <Button variant="default" className="w-full">
              More Feeds
            </Button>
          </div>

          {/* Right Column - Stats & Activities */}
          <div className="lg:col-span-2 space-y-6">
            {/* Recent Statistics */}
            <Card>
              <CardHeader className="pb-2">
                <div className="flex items-center justify-between">
                  <div>
                    <CardTitle className="text-base">Recent Statistics</CardTitle>
                    <p className="text-xs text-[var(--color-text-tertiary)] mt-1">More than 400 new members</p>
                  </div>
                  <Button variant="ghost" size="sm">
                    <MoreHorizontal className="w-4 h-4" />
                  </Button>
                </div>
              </CardHeader>
              <CardContent>
                <SimpleBarChart data={mockStats} />
                <div className="flex justify-between text-xs text-[var(--color-text-tertiary)] mt-2">
                  <span>20</span>
                  <span>40</span>
                  <span>60</span>
                  <span>80</span>
                  <span>100</span>
                  <span>120</span>
                </div>
              </CardContent>
            </Card>

            {/* Activities */}
            <Card>
              <CardHeader className="pb-2">
                <div className="flex items-center justify-between">
                  <div>
                    <CardTitle className="text-base">Activities</CardTitle>
                    <p className="text-xs text-[var(--color-text-tertiary)] mt-1">890,344 Sales</p>
                  </div>
                  <Button variant="ghost" size="sm">
                    <MoreHorizontal className="w-4 h-4" />
                  </Button>
                </div>
              </CardHeader>
              <CardContent>
                <div className="space-y-3">
                  {mockActivities.map(activity => (
                    <div key={activity.id} className="flex items-start gap-3">
                      <span className="text-xs text-[var(--color-text-tertiary)] w-12 flex-shrink-0">
                        {activity.time}
                      </span>
                      <span className={`w-2 h-2 rounded-full mt-1.5 flex-shrink-0 ${activityColors[activity.color]}`} />
                      <p className="text-sm text-[var(--color-text-secondary)] flex-1">
                        {activity.link ? (
                          <>
                            {activity.text.split(activity.link)[0]}
                            <a href="#" className="text-[var(--color-brand-primary)] hover:underline">
                              {activity.link}
                            </a>
                            {activity.text.split(activity.link)[1]}
                          </>
                        ) : activity.highlight ? (
                          <>
                            {activity.text.split(activity.highlight)[0]}
                            <span className="text-[var(--color-brand-primary)] font-medium">
                              {activity.highlight}
                            </span>
                            {activity.text.split(activity.highlight)[1]}
                          </>
                        ) : (
                          activity.text
                        )}
                      </p>
                    </div>
                  ))}
                </div>
              </CardContent>
            </Card>
          </div>
        </div>
      </div>
    </div>
  );
}
