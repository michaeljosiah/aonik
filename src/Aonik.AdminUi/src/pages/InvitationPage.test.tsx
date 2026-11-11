// @vitest-environment jsdom
import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { InvitationPage } from './InvitationPage';

const auth = vi.hoisted(() => ({ isAuthenticated: false, isLoading: false, user: { email: 'creator@example.test' }, login: vi.fn(), logout: vi.fn(), getAccessToken: vi.fn() }));
vi.mock('@/auth', () => ({ useAuth: () => auth, apiConfig: { baseUrl: '/api' } }));
let root: Root;
let element: HTMLDivElement;
const request = vi.fn();

beforeEach(() => {
  auth.isAuthenticated = false;
  vi.clearAllMocks();
  vi.stubGlobal('fetch', request);
  vi.stubGlobal('IS_REACT_ACT_ENVIRONMENT', true);
  auth.getAccessToken.mockResolvedValue('test-access-token');
  element = document.createElement('div'); document.body.appendChild(element); root = createRoot(element);
});
afterEach(async () => { await act(async () => root.unmount()); element.remove(); vi.unstubAllGlobals(); });
async function render(path = '/signin?invite=one-shot&tenant=tenant-a') {
  await act(async () => root.render(<MemoryRouter initialEntries={[path]}><InvitationPage /></MemoryRouter>));
}
async function click(text: string) {
  const button = Array.from(element.querySelectorAll('button')).find(item => item.textContent === text);
  expect(button).toBeDefined(); await act(async () => button!.click());
}

it('preserves the invitation across sign-in without consuming it before acceptance', async () => {
  await render(); await click('Sign in or create account');
  expect(auth.login).toHaveBeenCalledWith({ returnTo: '/signin?invite=one-shot&tenant=tenant-a' });
  expect(request).not.toHaveBeenCalled();
});
it('accepts only on an explicit action with the signed-in token and invited tenant', async () => {
  auth.isAuthenticated = true;
  request.mockResolvedValue({ ok: true, json: async () => ({ accepted: true }) });
  await render(); expect(request).not.toHaveBeenCalled(); await click('Accept invitation');
  expect(request).toHaveBeenCalledWith('/api/identity/invite/accept', expect.objectContaining({
    method: 'POST', headers: expect.objectContaining({ Authorization: 'Bearer test-access-token', 'X-Tenant-Id': 'tenant-a' }), body: JSON.stringify({ inviteToken: 'one-shot' }),
  }));
  expect(element.textContent).toContain('Invitation accepted');
  expect(window.location.search).not.toContain('invite');
});
it('shows an expired invitation remedy and never reports successful access', async () => {
  auth.isAuthenticated = true;
  request.mockResolvedValue({ ok: false, json: async () => ({ accepted: false, failureReason: 'invite_expired' }) });
  await render(); await click('Accept invitation');
  expect(element.textContent).toContain('This invitation has expired');
  expect(element.textContent).not.toContain('Invitation accepted');
});
it('does not offer sign-in or acceptance for an incomplete invitation', async () => {
  await render('/signin'); expect(element.textContent).toContain('link is incomplete'); expect(element.querySelector('button')).toBeNull();
});
