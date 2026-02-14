# Payabo Agent Instructions

## Playwright Authentication for LLM/E2E Testing

Use this shared test account when a Playwright or browser-driven test needs an authenticated Payabo session:

- **Username:** `michael.josiah@mailinator.com`
- **Password:** `Pa55word`

### Required setup before running Playwright

1. Start the Payabo web app.
2. Ensure `VITE_AONIK_API_BASE_URL` points to the API environment where this account exists.
3. Ensure `VITE_PAYABO_TENANT_ID` is set to the tenant GUID for that same environment.

Example local run command:

```bash
cd Payabo
VITE_AONIK_API_BASE_URL="https://api.aonik.com" \
VITE_PAYABO_TENANT_ID="<tenant-guid>" \
npm run dev -- --host 0.0.0.0 --port 5174
```

### Login flow for automation

1. Open `/login`.
2. Fill `#email-login` with the username.
3. Fill `#password-login` with the password.
4. Click the `LOGIN` button.
5. Wait for redirect to an authenticated route such as `/dashboard`.

### Validation guidance

- Treat successful navigation away from `/login` and rendering of dashboard content as auth success.
- If login fails, capture the on-screen error and verify API base URL + tenant ID are correctly configured for this test account.
