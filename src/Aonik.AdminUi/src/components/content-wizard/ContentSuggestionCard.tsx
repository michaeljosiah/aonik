import { useState } from 'react';
import { Card, CardContent, CardHeader } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Label } from '@/components/ui/label';
import { Check, X, Pencil, ChevronDown, ChevronUp, ImageIcon } from 'lucide-react';
import type { ContentSuggestion, SuggestionStatus } from '@/types/contentWizard';

interface ContentSuggestionCardProps {
  suggestion: ContentSuggestion;
  index: number;
  onStatusChange: (id: string, status: SuggestionStatus) => void;
  onUpdate: (id: string, updates: Partial<ContentSuggestion>) => void;
  readOnly?: boolean;
}

export function ContentSuggestionCard({
  suggestion,
  index,
  onStatusChange,
  onUpdate,
  readOnly = false,
}: ContentSuggestionCardProps) {
  const [expanded, setExpanded] = useState(false);
  const [editing, setEditing] = useState(false);
  const [editTitle, setEditTitle] = useState(suggestion.title);
  const [editBody, setEditBody] = useState(suggestion.body);
  const [editContentKey, setEditContentKey] = useState(suggestion.contentKey);

  const statusColors: Record<SuggestionStatus, string> = {
    pending: 'border-border',
    approved: 'border-success bg-success/5',
    rejected: 'border-destructive bg-destructive/5 opacity-60',
  };

  const statusBadge: Record<SuggestionStatus, { label: string; className: string }> = {
    pending: { label: 'Pending', className: 'bg-muted text-muted-foreground' },
    approved: { label: 'Approved', className: 'bg-success/15 text-success' },
    rejected: { label: 'Rejected', className: 'bg-destructive/15 text-destructive' },
  };

  function handleSaveEdit() {
    onUpdate(suggestion.id, {
      title: editTitle,
      body: editBody,
      contentKey: editContentKey,
    });
    setEditing(false);
  }

  function handleCancelEdit() {
    setEditTitle(suggestion.title);
    setEditBody(suggestion.body);
    setEditContentKey(suggestion.contentKey);
    setEditing(false);
  }

  return (
    <Card className={`transition-colors ${statusColors[suggestion.status]}`}>
      <CardHeader className="pb-2">
        <div className="flex items-start justify-between gap-3">
          <div className="flex-1 min-w-0">
            <div className="flex items-center gap-2 mb-1">
              <span className="text-xs font-mono text-muted-foreground">#{index + 1}</span>
              <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${statusBadge[suggestion.status].className}`}>
                {statusBadge[suggestion.status].label}
              </span>
              <span className="text-xs px-2 py-0.5 rounded bg-muted text-muted-foreground">
                {suggestion.area}
              </span>
              <span className="text-xs px-2 py-0.5 rounded bg-muted text-muted-foreground">
                {suggestion.format}
              </span>
            </div>
            {editing ? (
              <Input
                value={editTitle}
                onChange={(e) => setEditTitle(e.target.value)}
                className="font-semibold"
              />
            ) : (
              <h3 className="font-semibold text-foreground truncate">
                {suggestion.title}
              </h3>
            )}
            {editing ? (
              <div className="mt-1">
                <Label className="text-xs">Content Key</Label>
                <Input
                  value={editContentKey}
                  onChange={(e) => setEditContentKey(e.target.value)}
                  className="text-xs font-mono"
                />
              </div>
            ) : (
              <p className="text-xs font-mono text-muted-foreground mt-0.5 truncate">
                {suggestion.contentKey}
              </p>
            )}
          </div>
          {!readOnly && (
            <div className="flex items-center gap-1 shrink-0">
              {editing ? (
                <>
                  <Button variant="outline" size="sm" onClick={handleCancelEdit}>Cancel</Button>
                  <Button size="sm" onClick={handleSaveEdit}>Save</Button>
                </>
              ) : (
                <>
                  {suggestion.status !== 'approved' && (
                    <Button
                      variant="outline"
                      size="icon-sm"
                      className="text-success hover:bg-success/10"
                      onClick={() => onStatusChange(suggestion.id, 'approved')}
                      title="Approve"
                    >
                      <Check className="w-4 h-4" />
                    </Button>
                  )}
                  {suggestion.status !== 'rejected' && (
                    <Button
                      variant="outline"
                      size="icon-sm"
                      className="text-destructive hover:bg-destructive/10"
                      onClick={() => onStatusChange(suggestion.id, 'rejected')}
                      title="Reject"
                    >
                      <X className="w-4 h-4" />
                    </Button>
                  )}
                  {suggestion.status === 'approved' && (
                    <Button
                      variant="outline"
                      size="icon-sm"
                      onClick={() => onStatusChange(suggestion.id, 'pending')}
                      title="Undo approval"
                    >
                      Undo
                    </Button>
                  )}
                  <Button
                    variant="outline"
                    size="icon-sm"
                    onClick={() => setEditing(true)}
                    title="Edit"
                  >
                    <Pencil className="w-4 h-4" />
                  </Button>
                </>
              )}
            </div>
          )}
        </div>
      </CardHeader>
      <CardContent className="pt-0">
        {editing ? (
          <div className="space-y-2">
            <Label className="text-xs">Body</Label>
            <Textarea
              value={editBody}
              onChange={(e) => setEditBody(e.target.value)}
              rows={8}
              className="text-sm font-mono"
            />
          </div>
        ) : (
          <>
            <div
              className={`text-sm text-muted-foreground whitespace-pre-wrap ${expanded ? '' : 'line-clamp-3'}`}
            >
              {suggestion.body}
            </div>
            {suggestion.imagePrompt && (
              <div className="mt-2 flex items-center gap-1.5 text-xs text-muted-foreground">
                <ImageIcon className="w-3 h-3" />
                <span className="truncate">Image: {suggestion.imagePrompt}</span>
              </div>
            )}
            <button
              onClick={() => setExpanded(!expanded)}
              className="mt-2 flex items-center gap-1 text-xs text-primary hover:underline"
            >
              {expanded ? (
                <>
                  <ChevronUp className="w-3 h-3" /> Show less
                </>
              ) : (
                <>
                  <ChevronDown className="w-3 h-3" /> Show more
                </>
              )}
            </button>
          </>
        )}
      </CardContent>
    </Card>
  );
}
