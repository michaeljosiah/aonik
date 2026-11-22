import { AlertCircle } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { describeChatError } from '@/lib/chatErrors';

export function ChatErrorNotice({ error }: { error: string }) {
  const description = describeChatError(error);
  return <Alert variant="destructive">
    <AlertCircle />
    <AlertTitle>{description.title}</AlertTitle>
    <AlertDescription>
      <p>{description.message}</p>
      <details className="mt-1 text-xs"><summary className="cursor-pointer">Diagnostic code</summary><p>{description.code}</p></details>
    </AlertDescription>
  </Alert>;
}
