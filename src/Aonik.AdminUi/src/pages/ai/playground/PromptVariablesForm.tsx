import { useCallback, useMemo, useState } from 'react';
import { Label } from '@/components/ui/label';
import { Button } from '@/components/ui/button';
import { Plus, Trash2 } from 'lucide-react';

interface PromptVariablesFormProps {
  /** JSON schema string describing available variables (from AiTask.VariablesSchemaJson). */
  variablesSchema: string | null;
  /** Current variable values. */
  variables: Record<string, string>;
  /** Called when variables change. */
  onChange: (variables: Record<string, string>) => void;
}

interface SchemaProperty {
  type?: string;
  description?: string;
  default?: string;
}

/**
 * Renders a form for filling in prompt template variables.
 * If a JSON schema is provided, it renders fields from the schema.
 * Otherwise, it renders a dynamic key-value editor.
 */
export function PromptVariablesForm({
  variablesSchema,
  variables,
  onChange,
}: PromptVariablesFormProps) {
  // Parse schema properties if available
  const schemaProperties = useMemo<Record<string, SchemaProperty> | null>(() => {
    if (!variablesSchema) return null;
    try {
      const parsed = JSON.parse(variablesSchema);
      return parsed.properties ?? null;
    } catch {
      return null;
    }
  }, [variablesSchema]);

  if (schemaProperties) {
    return (
      <SchemaBasedForm
        properties={schemaProperties}
        variables={variables}
        onChange={onChange}
      />
    );
  }

  return (
    <DynamicKeyValueForm variables={variables} onChange={onChange} />
  );
}

// ── Schema-based form ───────────────────────────────────────────────────────

function SchemaBasedForm({
  properties,
  variables,
  onChange,
}: {
  properties: Record<string, SchemaProperty>;
  variables: Record<string, string>;
  onChange: (variables: Record<string, string>) => void;
}) {
  const keys = Object.keys(properties);

  return (
    <div className="space-y-3">
      <p className="text-xs text-[var(--color-text-tertiary)]">
        Fill in the template variables below. These will be substituted into{' '}
        <code className="text-[10px]">{'{{variable}}'}</code> placeholders in the prompt.
      </p>
      {keys.map((key) => {
        const prop = properties[key];
        const isMultiline = (prop.type === 'string' && (prop.description?.includes('JSON') || prop.description?.includes('json'))) ||
          (variables[key]?.length ?? 0) > 100;

        return (
          <div key={key} className="space-y-1">
            <Label className="text-xs font-medium">{key}</Label>
            {prop.description && (
              <p className="text-[10px] text-[var(--color-text-tertiary)]">{prop.description}</p>
            )}
            {isMultiline ? (
              <textarea
                className="h-20 w-full rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-2.5 py-1.5 text-xs text-[var(--color-text-primary)] focus:border-[var(--color-brand-primary)] focus:outline-none"
                value={variables[key] ?? prop.default ?? ''}
                placeholder={prop.default ?? `Enter ${key}...`}
                onChange={(e) => onChange({ ...variables, [key]: e.target.value })}
              />
            ) : (
              <input
                type="text"
                className="h-8 w-full rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-2.5 text-xs text-[var(--color-text-primary)] focus:border-[var(--color-brand-primary)] focus:outline-none"
                value={variables[key] ?? prop.default ?? ''}
                placeholder={prop.default ?? `Enter ${key}...`}
                onChange={(e) => onChange({ ...variables, [key]: e.target.value })}
              />
            )}
          </div>
        );
      })}
      {keys.length === 0 && (
        <p className="text-xs italic text-[var(--color-text-tertiary)]">
          No variables defined in the schema.
        </p>
      )}
    </div>
  );
}

// ── Dynamic key-value form ──────────────────────────────────────────────────

function DynamicKeyValueForm({
  variables,
  onChange,
}: {
  variables: Record<string, string>;
  onChange: (variables: Record<string, string>) => void;
}) {
  const [newKey, setNewKey] = useState('');

  const entries = Object.entries(variables);

  const handleAdd = useCallback(() => {
    const key = newKey.trim();
    if (!key || key in variables) return;
    onChange({ ...variables, [key]: '' });
    setNewKey('');
  }, [newKey, variables, onChange]);

  const handleRemove = useCallback(
    (key: string) => {
      const updated = { ...variables };
      delete updated[key];
      onChange(updated);
    },
    [variables, onChange],
  );

  const handleValueChange = useCallback(
    (key: string, value: string) => {
      onChange({ ...variables, [key]: value });
    },
    [variables, onChange],
  );

  return (
    <div className="space-y-3">
      <p className="text-xs text-[var(--color-text-tertiary)]">
        Add variable values to substitute into{' '}
        <code className="text-[10px]">{'{{variable}}'}</code> placeholders.
      </p>

      {entries.map(([key, value]) => (
        <div key={key} className="flex items-center gap-2">
          <span className="w-28 shrink-0 truncate text-xs font-medium text-[var(--color-text-secondary)]">
            {key}
          </span>
          <input
            type="text"
            className="h-7 flex-1 rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-2 text-xs text-[var(--color-text-primary)] focus:border-[var(--color-brand-primary)] focus:outline-none"
            value={value}
            placeholder={`Value for ${key}`}
            onChange={(e) => handleValueChange(key, e.target.value)}
          />
          <button
            type="button"
            className="rounded p-1 text-[var(--color-text-tertiary)] hover:bg-[var(--color-background)] hover:text-red-500"
            onClick={() => handleRemove(key)}
          >
            <Trash2 className="h-3 w-3" />
          </button>
        </div>
      ))}

      <div className="flex items-center gap-2">
        <input
          type="text"
          className="h-7 flex-1 rounded-md border border-[var(--color-border)] bg-[var(--color-surface)] px-2 text-xs text-[var(--color-text-primary)] placeholder:text-[var(--color-text-tertiary)] focus:border-[var(--color-brand-primary)] focus:outline-none"
          placeholder="Variable name..."
          value={newKey}
          onChange={(e) => setNewKey(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter') {
              e.preventDefault();
              handleAdd();
            }
          }}
        />
        <Button
          variant="ghost"
          size="sm"
          className="h-7 text-xs"
          onClick={handleAdd}
          disabled={!newKey.trim()}
        >
          <Plus className="mr-1 h-3 w-3" />
          Add
        </Button>
      </div>
    </div>
  );
}
