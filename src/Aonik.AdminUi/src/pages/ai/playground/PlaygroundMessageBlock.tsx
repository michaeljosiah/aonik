import { useState } from 'react';
import { Trash2, Save, RotateCcw } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Textarea } from '@/components/ui/textarea';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { agentConfigService } from '@/services/aiService';
import { toast } from 'sonner';

type MessageRole = 'system' | 'user' | 'assistant';

interface PlaygroundMessageBlockProps {
  role: MessageRole;
  content: string;
  index?: number;
  onRoleChange?: (role: MessageRole) => void;
  onContentChange: (content: string) => void;
  onDelete?: () => void;
  readOnly?: boolean;
  roleFixed?: boolean;
  /** Agent name — enables Save/Reset for system prompt blocks */
  agentName?: string | null;
  /** Default prompt from agent config — enables Reset */
  defaultPrompt?: string | null;
}

export function PlaygroundMessageBlock({
  role,
  content,
  index,
  onRoleChange,
  onContentChange,
  onDelete,
  readOnly = false,
  roleFixed = false,
  agentName,
  defaultPrompt,
}: PlaygroundMessageBlockProps) {
  const [saving, setSaving] = useState(false);
  const tokenEstimate = Math.round(content.length / 4);

  const isSystemWithAgent = role === 'system' && agentName;
  const hasChanges = defaultPrompt !== null && defaultPrompt !== undefined && content !== defaultPrompt;

  const handleSave = async () => {
    if (!agentName) return;
    setSaving(true);
    try {
      await agentConfigService.upsert(agentName, { instructionsText: content });
      toast.success('Saved prompt to agent config');
    } catch (err) {
      toast.error(`Save failed: ${(err as Error).message}`);
    } finally {
      setSaving(false);
    }
  };

  const handleReset = () => {
    if (defaultPrompt !== null && defaultPrompt !== undefined) {
      onContentChange(defaultPrompt);
    }
  };

  return (
    <div className="flex gap-3 border-b border-[var(--color-border-light)] px-6 py-4">
      {/* Role selector / label */}
      <div className="w-24 shrink-0 pt-2">
        {roleFixed ? (
          <span className="text-xs font-medium capitalize text-[var(--color-text-secondary)]">
            {role}
          </span>
        ) : (
          <Select value={role} onValueChange={(v) => onRoleChange?.(v as MessageRole)}>
            <SelectTrigger className="h-7 text-xs">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="system">System</SelectItem>
              <SelectItem value="user">User</SelectItem>
              <SelectItem value="assistant">Assistant</SelectItem>
            </SelectContent>
          </Select>
        )}
        {index !== undefined && (
          <span className="mt-0.5 block text-[10px] text-[var(--color-text-tertiary)]">
            #{index}
          </span>
        )}
      </div>

      {/* Content area */}
      <div className="min-w-0 flex-1">
        <Textarea
          value={content}
          onChange={(e) => onContentChange(e.target.value)}
          placeholder={`Enter ${role} message...`}
          rows={role === 'system' ? 10 : 2}
          className="resize-y font-mono text-xs leading-relaxed"
          readOnly={readOnly}
        />
      </div>

      {/* Actions column */}
      <div className="flex shrink-0 flex-col items-end gap-1.5 pt-1">
        <span className="rounded-full bg-[var(--color-surface-inset)] px-2 py-0.5 text-[10px] font-medium tabular-nums text-[var(--color-text-tertiary)]">
          {tokenEstimate}
        </span>

        {/* Save / Reset for system prompt when an agent is selected */}
        {isSystemWithAgent && hasChanges && (
          <>
            <Button
              variant="ghost"
              size="sm"
              onClick={handleReset}
              title="Reset to agent default"
              className="h-6 w-6 p-0 text-[var(--color-text-tertiary)] hover:text-[var(--color-text-primary)]"
            >
              <RotateCcw className="h-3 w-3" />
            </Button>
            <Button
              variant="ghost"
              size="sm"
              onClick={handleSave}
              disabled={saving}
              title="Save prompt to agent config"
              className="h-6 w-6 p-0 text-[var(--color-brand-primary)] hover:text-[var(--color-brand-primary)]"
            >
              <Save className="h-3 w-3" />
            </Button>
          </>
        )}

        {onDelete && (
          <Button
            variant="ghost"
            size="sm"
            onClick={onDelete}
            className="h-6 w-6 p-0 text-[var(--color-text-tertiary)] hover:text-[var(--color-error)]"
          >
            <Trash2 className="h-3 w-3" />
          </Button>
        )}
      </div>
    </div>
  );
}
