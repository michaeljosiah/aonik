---
name: test-admin-ui
description: Launch the Aonik Admin UI dev server, authenticate via Auth0, and interactively test pages using the Playwright MCP browser tools
---

## Purpose

This skill provides step-by-step instructions for starting the Aonik backend API, spinning up the Admin UI, authenticating through the Auth0 login flow, and using the Playwright MCP tools to interact with and test the application in a real browser.

## Prerequisites

- **SQL Server LocalDB** must be installed (ships with Visual Studio, or install via SQL Server Express).
- **Node.js** must be installed (for the Vite frontend dev server).
- The Playwright MCP server is already configured in `.opencode/opencode.json` — use its browser tools for all browser interactions.

## Step 1 — Start the API Backend

The Admin UI requires the Aonik API to be running. You have two options:

### Option A — Aspire AppHost (Recommended, starts everything)

This starts the API, Worker, Admin UI, and Payabo frontend all at once:

```bash
dotnet run --project src/Aonik.AppHost
```

| Service         | URL                          |
|-----------------|------------------------------|
| API (HTTPS)     | `https://localhost:5001`     |
| Admin UI (Vite) | `http://localhost:5173`      |
| Aspire Dashboard| `https://localhost:17070`    |

If using the AppHost, **skip Step 2** — the Admin UI is already running.

### Option B — API Standalone

Start just the API by itself:

```bash
dotnet run --project src/Aonik.Api
```

The standalone API starts at `http://localhost:5049` (default profile) or `https://localhost:7269` (HTTPS profile). However, the Admin UI's `.env.local` expects the API at `https://localhost:5001` (the Aspire port), so **Option A is preferred** unless you update `.env.local` to point at the standalone port.

### Database Setup

On first run in the Development environment, the API **automatically**:
1. Creates the `AonikDb` database on LocalDB
2. Applies all EF Core migrations
3. Seeds identity, catalog, and settings data (including a default tenant)

No manual migration step is needed. If you prefer to run migrations manually:

```bash
dotnet ef database update --project src/Aonik.Infrastructure --startup-project src/Aonik.Api
```

Wait for the API to finish starting (watch for the "Now listening on" log message) before proceeding.

## Step 2 — Start the Admin UI Dev Server (Skip if using AppHost)

If you started the API standalone (Option B), start the Vite dev server separately:

```bash
npm run dev
# Working directory: src/Aonik.AdminUi
```

The server starts at `http://localhost:5173` (strict port). Wait for the "ready" message before proceeding.

Leave this running in the background. All subsequent steps use the Playwright MCP browser tools.

## Step 3 — Navigate to the App

Use Playwright to open the browser:

```
playwright_browser_navigate → http://localhost:5173
```

The app will automatically redirect unauthenticated users to `/login`. Wait for the URL to contain `/login`.

## Step 4 — Wait for the Login Page

Wait for these elements to confirm the page is ready:

- Heading text: **"Welcome back"**
- Info banner: **"Signing in with Auth0"**
- Button: **"Sign in with Auth0"** (visible and enabled)

The page also loads a tenant/organization selector dropdown from the API. Wait for the loading spinner ("Loading organizations...") to disappear before proceeding.

## Step 5 — Select a Tenant (if needed)

On `localhost`, the login page shows an organization selector. The first tenant is auto-selected by default, which is usually acceptable. If a specific tenant is needed:

1. Take a snapshot to see available tenants
2. Click the organization dropdown (aria-label: "Select organization")
3. Select the desired tenant from the list

## Step 6 — Click "Sign in with Auth0"

Click the **"Sign in with Auth0"** button. This triggers a full-page redirect to the Auth0 Universal Login page at `aonik.uk.auth0.com`.

Wait for the URL to contain `aonik.uk.auth0.com` before proceeding.

## Step 7 — Authenticate on the Auth0 Login Page

On the Auth0 hosted login page, enter the test credentials:

| Field    | Value                              |
|----------|------------------------------------|
| Email    | `michael.josiah@mailinator.com`    |
| Password | `Pa55word`                         |

The typical Auth0 Universal Login form has:

- An email/username input (look for `input#username`, `input[name="username"]`, or `input[name="email"]`)
- A password input (look for `input#password` or `input[name="password"]`)
- A submit button (look for `button[type="submit"]` or a "Continue" / "Log In" button)

Use `playwright_browser_snapshot` to inspect the page and identify the exact elements before interacting. Fill the fields and submit the form.

**If a consent/authorization screen appears:** Click the "Accept" / "Authorize" / "Allow" button to grant access.

## Step 8 — Wait for Redirect Back to the App

After successful authentication, Auth0 redirects back to `http://localhost:5173`. Wait for:

1. The URL to return to `localhost:5173`
2. The Auth0 SDK to process the callback (the `?code=` and `&state=` params disappear)
3. The app to navigate to `/` (dashboard)

## Step 9 — Verify Authentication Succeeded

Take a snapshot and confirm:

- The main app layout is visible (sidebar navigation, header)
- The URL is `/` or the dashboard path
- The login page is no longer displayed

You are now authenticated and can interact with any page in the Admin UI.

## Testing Pages

Once authenticated, you can navigate to and test any page. Use these Playwright tools:

| Tool                            | Use For                                      |
|---------------------------------|----------------------------------------------|
| `playwright_browser_navigate`   | Go to a specific route (e.g., `/invoices`)   |
| `playwright_browser_snapshot`   | Inspect current page structure and elements  |
| `playwright_browser_click`      | Click buttons, links, menu items             |
| `playwright_browser_fill_form`  | Fill in form fields                          |
| `playwright_browser_type`       | Type into text inputs                        |
| `playwright_browser_select_option` | Select dropdown values                    |
| `playwright_browser_take_screenshot` | Capture visual state for review          |
| `playwright_browser_wait_for`   | Wait for text, elements, or time delays      |
| `playwright_browser_console_messages` | Check for JavaScript errors              |
| `playwright_browser_network_requests` | Inspect API calls and responses          |

## Troubleshooting

### Login page shows "Loading organizations..." indefinitely
The API backend is not running or not reachable at `https://localhost:5001`. Start it with `dotnet run --project src/Aonik.AppHost` (or `dotnet run --project src/Aonik.Api` for standalone).

### Auth0 page shows an error
Check that `.env.local` in `src/Aonik.AdminUi` has the correct Auth0 configuration:
- `VITE_AUTH_PROVIDER=auth0`
- `VITE_AUTH0_DOMAIN=aonik.uk.auth0.com`
- `VITE_AUTH0_CLIENT_ID=ZoiNbhyAsfjKiNHYPudqsZ75zI1vFyYb`

### Redirect back fails or loops
Clear browser state. Auth0 tokens and tenant selection are cached in localStorage. Use `playwright_browser_evaluate` to run `localStorage.clear()` and reload.

### App shows Setup Wizard instead of login
No tenants exist in the database. Seed the database or create a tenant via the API first.

## Environment Reference

| Setting              | Value                              |
|----------------------|------------------------------------|
| Admin UI URL         | `http://localhost:5173`            |
| API Backend URL      | `https://localhost:5001`           |
| Aspire Dashboard     | `https://localhost:17070`          |
| Auth Provider        | Auth0                              |
| Auth0 Domain         | `aonik.uk.auth0.com`              |
| Database             | LocalDB — `AonikDb` (auto-created)|
| AppHost Project      | `src/Aonik.AppHost`               |
| API Project          | `src/Aonik.Api`                    |
| Admin UI Project     | `src/Aonik.AdminUi`               |
| Package Manager      | npm                                |
| Dev Server Command   | `npm run dev`                      |
| Framework            | React 19 + Vite 7 + TypeScript    |
