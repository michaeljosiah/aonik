import { describe, expect, it, vi } from 'vitest';
vi.mock('@/lib/api', () => ({ api: {} }));
import { consentUrl } from './connectorService';

describe('connector consent navigation', () => {
  it('accepts the exact HTTPS Google account host returned by the server', () => {
    const url = 'https://accounts.google.com/o/oauth2/auth?state=request&code_challenge=pkce';
    expect(consentUrl(url)).toBe(url);
  });
  it.each(['javascript:alert(1)', 'https://accounts.google.com.evil.test/auth', 'http://accounts.google.com/auth', 'https://user:secret@accounts.google.com/auth'])('rejects an untrusted consent URL: %s', value => {
    expect(() => consentUrl(value)).toThrow();
  });
});
