import { describe, expect, it } from 'vitest';
import { connectionRequestResult } from './connectionRequestResult';

describe('trusted connection review results', () => {
  const id = '11111111-1111-1111-1111-111111111111';
  const result = { id, status: 'AwaitingConsent', nextAction: 'Owner consent is required.', reviewPath: `/settings/my-connections?requestId=${id}` };
  it('uses a local review path from a typed server request tool', () => {
    expect(connectionRequestResult('aonik_create_connection_request', JSON.stringify(result))).toEqual(result);
  });
  it('does not treat email text or an assistant display tool as permission to connect', () => {
    expect(connectionRequestResult('gmail_get_message', JSON.stringify(result))).toBeNull();
    expect(connectionRequestResult('display_connection_request', JSON.stringify(result))).toBeNull();
  });
  it.each(['https://evil.example.test/consent', '//evil.example.test/consent', `/settings/my-connections?requestId=other`, 'javascript:alert(1)'])('refuses an injected review destination: %s', reviewPath => {
    expect(connectionRequestResult('aonik_get_connection_request', JSON.stringify({ ...result, reviewPath }))).toBeNull();
  });
});
