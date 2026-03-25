Launch the Aonik Admin UI dev server, authenticate via Auth0, and interactively test pages using browser automation tools.

## Prerequisites

- SQL Server LocalDB installed
- Node.js installed
- The API backend must be running (Aspire AppHost or standalone)

## Step 1 — Ensure the API Backend is Running

Check if the API is already running:

```bash
curl -k -s -o /dev/null -w "%{http_code}" https://localhost:5001/health
```

If not running, start it via Aspire AppHost (starts API + Admin UI + Worker + Payabo):

```bash
dotnet run --project src/Aonik.AppHost
```

| Service         | URL                          |
|-----------------|------------------------------|
| API (HTTPS)     | `https://localhost:5001`     |
| Admin UI (Vite) | `http://localhost:5173`      |

If using the AppHost, skip Step 2.

### Database Setup

On first run in Development, the API automatically creates the `AonikDb` database on LocalDB, applies all EF Core migrations, and seeds data (including a default tenant). No manual migration needed.

## Step 2 — Start the Admin UI Dev Server (Skip if using AppHost)

If the API was started standalone:

```bash
cd src/Aonik.AdminUi && npm run dev
```

The server starts at `http://localhost:5173`.

## Step 3 — Navigate to the App

Open a browser tab and navigate to `http://localhost:5173`. The app redirects unauthenticated users to `/login`.

## Step 4 — Wait for the Login Page

Confirm these elements are present:
- Heading: **"Welcome back"**
- Info banner: **"Signing in with Auth0"**
- Button: **"Sign in with Auth0"** (visible and enabled)
- Organization selector has finished loading (no "Loading organizations..." spinner)

## Step 5 — Select a Tenant (if needed)

The first tenant is auto-selected by default. If a specific tenant is needed, use the organization dropdown to select it.

## Step 6 — Click "Sign in with Auth0"

Click the **"Sign in with Auth0"** button. This redirects to Auth0 Universal Login at `aonik.uk.auth0.com`.

Wait for the URL to contain `aonik.uk.auth0.com`.

## Step 7 — Authenticate on the Auth0 Login Page

Enter the test credentials:

| Field    | Value                              |
|----------|------------------------------------|
| Email    | `michael.josiah@mailinator.com`    |
| Password | `Pa55word`                         |

Look for:
- Email input: `input#username`, `input[name="username"]`, or `input[name="email"]`
- Password input: `input#password` or `input[name="password"]`
- Submit button: `button[type="submit"]` or "Continue" / "Log In"

Inspect the page structure before interacting. Fill the fields and submit.

**If a consent screen appears:** Click "Accept" / "Authorize" / "Allow".

## Step 8 — Wait for Redirect Back

After authentication, Auth0 redirects back to `http://localhost:5173`. Wait for:
1. URL to return to `localhost:5173`
2. The `?code=` and `&state=` params to disappear
3. Navigation to `/` (dashboard)

## Step 9 — Verify Authentication

Confirm:
- Main app layout is visible (sidebar navigation, header)
- URL is `/` or the dashboard path
- Login page is no longer displayed

You are now authenticated and can interact with any page in the Admin UI.

## Testing Pages

Navigate to and test any page. Common routes:
- `/` — Dashboard
- `/invoices` — Billing invoices
- `/customers` — Customer accounts
- `/agents` — AI agents
- `/settings` — Platform settings

## Troubleshooting

### Login page shows "Loading organizations..." indefinitely
API backend is not running. Start with `dotnet run --project src/Aonik.AppHost`.

### Auth0 page shows an error
Check `src/Aonik.AdminUi/.env.local` has:
- `VITE_AUTH_PROVIDER=auth0`
- `VITE_AUTH0_DOMAIN=aonik.uk.auth0.com`
- `VITE_AUTH0_CLIENT_ID=ZoiNbhyAsfjKiNHYPudqsZ75zI1vFyYb`

### Redirect back fails or loops
Clear browser localStorage and reload.

### App shows Setup Wizard instead of login
No tenants in database. Restart the API to trigger auto-seed, or create a tenant via the API.

## Environment Reference

| Setting              | Value                              |
|----------------------|------------------------------------|
| Admin UI URL         | `http://localhost:5173`            |
| API Backend URL      | `https://localhost:5001`           |
| Auth Provider        | Auth0                              |
| Auth0 Domain         | `aonik.uk.auth0.com`              |
| Database             | LocalDB — `AonikDb` (auto-created)|
