import { useEffect, useRef, useState } from 'react';
import { Textarea } from '@/components/ui/textarea';
import { Button } from '@/components/ui/button';

export function JsonField({ label, value, onChange }: { label: string; value: unknown; onChange: (value: unknown) => void }) {
  const encoded = JSON.stringify(value, null, 2);
  const [draft, setDraft] = useState({ encoded, text: encoded, error: false });
  const textarea = useRef<HTMLTextAreaElement>(null);
  const text = draft.encoded === encoded ? draft.text : encoded;
  const error = draft.encoded === encoded && draft.error;
  useEffect(() => { textarea.current?.setCustomValidity(error ? 'Enter valid JSON before saving.' : ''); }, [encoded, error]);
  return <div className="space-y-1 text-xs"><label className="block space-y-1">{label}<Textarea ref={textarea} value={text} rows={5} aria-invalid={error} onChange={event => {
    const updated = event.target.value;
    try { const parsed: unknown = JSON.parse(updated); event.target.setCustomValidity(''); setDraft({ encoded, text: updated, error: false }); onChange(parsed); }
    catch { event.target.setCustomValidity('Enter valid JSON before saving.'); setDraft({ encoded, text: updated, error: true }); }
  }} /></label>{error && <span className="flex flex-wrap items-center gap-2"><span role="alert" className="text-destructive">Enter valid JSON before saving.</span><Button type="button" variant="outline" size="sm" onClick={() => setDraft({ encoded, text: encoded, error: false })}>Discard invalid edit</Button></span>}</div>;
}
