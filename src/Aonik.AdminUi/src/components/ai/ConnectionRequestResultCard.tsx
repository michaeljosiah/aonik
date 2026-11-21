import { Link } from 'react-router-dom';
import { Card, CardContent } from '@/components/ui/card';
import { connectionRequestResult } from '@/lib/connectionRequestResult';

export function ConnectionRequestResultCard({ toolName, result }: { toolName: string; result?: string }) {
  const request = connectionRequestResult(toolName, result);
  if (!request) return null;
  return <Card className="mt-2"><CardContent className="space-y-2 pt-4 text-sm"><p className="font-medium">Connection request · {request.status}</p><p className="text-muted-foreground">{request.nextAction}</p>
    <Link to={request.reviewPath} className="text-primary underline">Review account and requested access</Link>
    <p className="text-xs text-muted-foreground">Account consent and access decisions continue on the authenticated review page.</p>
  </CardContent></Card>;
}
