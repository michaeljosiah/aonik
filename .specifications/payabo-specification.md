# payabo-specification

## Objective
Convert the static HTML in `website/MyBillAfrica` (Payabo prototype) into a React application with identical look, feel, and page behavior while preserving the existing visual language, layouts, and styling. The React app should be structured for maintainability, reuse, and future integration with AONIK platform services.

## Project Location
Create the React project in a **new root-level folder named `Payabo/`**. This keeps the marketing prototype (`website/MyBillAfrica`) intact for reference while establishing a dedicated, production-ready front-end workspace.

Proposed root layout:
```
Payabo/
  package.json
  vite.config.ts
  public/
  src/
```

## Source Inventory (Pages & Templates)
Use the current HTML files as the design source of truth. Key page families:

### Marketing / Public Pages
- Home + marketing: `index.html`, `features.html`, `features-page.html`, `about.html`, `help.html`, `community.html`, `get-app.html`, `privacy.html`, `cookies.html`. 

### Auth / Onboarding
- `login.html`, `register.html`.

### Dashboard / Authenticated Shell
- `dashboard-*.html` variants (empty/sample/raw), transactions and calendar variants.
- `managecards-*.html` and `carddetails.html`.

### Bill Payment Flow
- Provider list: `serviceproviderlist-*.html`.
- Service details: `servicedetails-*.html`.
- Payment selection + card/friend checkout: `paymentselection.html`, `cardcheckout-*.html`, `friendcheckout-*.html`, `selectcard.html`, `selectfriend.html`, `selectfriend-*.html`.
- Confirmation/status: `confirmation-*.html`, `status-*.html`, `transactiondetails-raw.html`.

### Profile / Settings
- `profile-*.html` pages for personal details, login details, notifications, and marketing preferences.

### Misc UI Flows
- `friend-message.html`, `frienddetails.html`, `carddetails.html`, `servicedetails-recurringbill.html`.

These pages share a common header, footer, sidebar, and a repeatable card/layout system that should become React components.

## Design Preservation Strategy (Pixel Parity)
1. **Reuse existing CSS & assets**
   - Bring over `css/` and `images/` exactly as-is to avoid regressions.
   - Keep class names unchanged in React markup to ensure identical styling.
   - Preserve SVG icons inline when already embedded in HTML (consistent rendering).

2. **Bootstrap-based layout**
   - Continue using the existing Bootstrap grid classes and utility classes (no redesign).

3. **Behavior parity with existing JS**
   - Re-implement functionality from `js/script.js` using React-friendly libraries or custom hooks while keeping selectors/class names for style and behavior.

## React App Architecture

### Suggested Tech Stack
- React + Vite (aligns with existing tooling but lives in the new `Payabo/` root folder).
- React Router for routing.
- CSS import strategy: global CSS import in `main.tsx` or `App.tsx` for `bootstrap.min.css`, `select2.min.css`, `slick.css`, `intlTelInput.css`, and `style.css`.

### Application Structure (Proposed)
```
Payabo/
  src/
    app/
      App.tsx
      routes.tsx
      layouts/
        MarketingLayout.tsx
        AuthLayout.tsx
        DashboardLayout.tsx
        FlowLayout.tsx
    components/
      common/
        Preloader.tsx
        HeaderMarketing.tsx
        HeaderDashboard.tsx
        Footer.tsx
        CookieAlert.tsx
        ScrollToTop.tsx
      navigation/
        MorphDropdown.tsx
        SidebarNav.tsx
        ProgressBar.tsx
      cards/
        FeatureCard.tsx
        PaymentCard.tsx
        SummaryCard.tsx
        TransactionCard.tsx
      forms/
        TextField.tsx
        PasswordField.tsx
        PhoneField.tsx
        SelectField.tsx
        OTPInput.tsx
      feedback/
        Alert.tsx
        StatusBanner.tsx
      profile/
        ProfileMenu.tsx
        AvatarBlock.tsx
    pages/
      marketing/
        Home.tsx
        Features.tsx
        FeaturesPage.tsx
        About.tsx
        Help.tsx
        Community.tsx
        GetApp.tsx
        Privacy.tsx
        Cookies.tsx
      auth/
        Login.tsx
        Register.tsx
      dashboard/
        Dashboard.tsx
        DashboardEmpty.tsx
        Transactions.tsx
        TransactionsCalendar.tsx
        ManageCards.tsx
        CardDetails.tsx
      payments/
        ProviderList.tsx
        ServiceDetails.tsx
        PaymentSelection.tsx
        CardCheckout.tsx
        FriendCheckout.tsx
        SelectCard.tsx
        SelectFriend.tsx
        ConfirmationBillPaid.tsx
        ConfirmationPaymentSent.tsx
        ConfirmationOrderReceived.tsx
        StatusBillPaid.tsx
        StatusBillPaidFailed.tsx
        StatusPaymentSent.tsx
        StatusOrderReceived.tsx
        TransactionDetails.tsx
      profile/
        PersonalDetails.tsx
        PersonalDetailsEditName.tsx
        PersonalDetailsEditCountry.tsx
        PersonalDetailsPhone.tsx
        PersonalDetailsUpdatePhoto.tsx
        LoginDetails.tsx
        LoginDetailsEmail.tsx
        LoginDetailsPassword.tsx
        NotificationSettings.tsx
        MarketingPreferences.tsx
```

## Component Mapping (HTML → React)

### Layout Components
- **MarketingLayout**
  - Header (public nav with morph dropdown).
  - Footer + CookieAlert.
  - Used by `index.html`, `features.html`, `about.html`, etc.

- **AuthLayout**
  - Fullscreen layout with left image / right form (e.g., `login.html`, `register.html`).

- **DashboardLayout**
  - Header (authenticated), sidebar navigation, optional top banners.
  - Used by dashboard + profile pages.

- **FlowLayout**
  - Progress bar header + summary sidebar (payment flows).

### Shared UI Components
- **Preloader** (from `#loading` block).
- **HeaderMarketing** (morph dropdown + public nav).
- **HeaderDashboard** (user dropdown + inbox).
- **Footer** (company/legal/social + footer bar).
- **CookieAlert** (cookie preference prompt).
- **SidebarNav** (accordion nav on dashboard pages).
- **ProgressBar** (payment steps bar).
- **SummaryCard/ListGroup** (order summary sidebar).
- **Form controls** (text, password toggle, select w/ icon, phone input).

## JavaScript Behavior Migration
Recreate `js/script.js` functionality in React. Mapping:

| Current Behavior | React Approach |
| --- | --- |
| Preloader fade-out | `useEffect` on app mount + CSS transition state |
| Sticky header + navcolumn | `useEffect` scroll listener + class toggle |
| Morph dropdown | Component state + onMouseEnter/onMouseLeave + CSS classes |
| OTP input | `OTPInput` component with key handling |
| Password toggle | `PasswordField` component state |
| Form "not-empty" class | controlled inputs + conditional class |
| `select2` dropdowns | Replace with React Select or Headless UI + custom option rendering |
| `slick` sliders | `react-slick` with same settings |
| `intlTelInput` | `react-intl-tel-input` or `react-phone-input-2` with CSS alignment |
| Tooltips | Bootstrap tooltip wrapper or React tooltip |
| Smooth scroll | `react-scroll` or native `scrollIntoView` |

**Goal:** Ensure visual and interaction parity even if libraries change (match layout, spacing, and hover states).

## Routing Plan
Establish a route per HTML page, matching the current page names for easy QA comparison. Example routes:

- `/` → Home
- `/features`, `/features-page`, `/about`, `/help`, `/community`, `/get-app`, `/privacy`, `/cookies`
- `/login`, `/register`
- `/dashboard`, `/dashboard/empty`, `/transactions`, `/transactions/calendar`
- `/payments/providers`, `/payments/service/:id`, `/payments/selection`, `/payments/card-checkout`, `/payments/friend-checkout`, `/payments/confirm/*`
- `/profile/personal`, `/profile/personal/edit-name`, `/profile/login-details`, etc.

Keep routes aligned with existing filenames during migration for QA parity.

## Assets & Styling
- Copy `website/MyBillAfrica/images` to `Payabo/public/images` (or an equivalent static path) so existing relative URLs still resolve.
- Copy `css` files to `Payabo/src/styles` and ensure they are loaded globally.
- Avoid renaming class names or structural DOM order when possible.

## Data Modeling (Front-End Only)
Use mock data providers to mirror the static content until APIs exist.
- `data/` folder with JSON for services, bills, transactions, notifications, profile.
- Ensure data models reflect AONIK domain concepts: Orders (intent), Payments (execution), Ledger (display of proof).

## QA / Visual Regression
- Use side-by-side comparison: render each React page next to HTML reference.
- Add visual regression snapshots (Playwright) once routes are stable.
- Ensure cross-device parity (desktop, tablet, mobile breakpoints).

## Phased Migration Plan
1. **Foundation**: Create React app shell in `Payabo/`, global CSS import, routing, assets.
2. **Layouts**: Implement marketing/auth/dashboard/flow layouts.
3. **Shared components**: Header, footer, sidebar, cards, form fields.
4. **Page migrations**: Port pages in batches (marketing → auth → dashboard → payments → profile).
5. **Behavior parity**: Migrate JS behaviors with React equivalents.
6. **Refinement**: QA + pixel parity adjustments.

## Acceptance Criteria
- Each HTML page has a 1:1 React route and component.
- Visual look/feel matches current HTML pages (spacing, typography, colors, icons).
- Interactive behaviors (dropdowns, sliders, form toggles) match the current experience.
- No regressions in responsive layouts (mobile/tablet/desktop).
- React project lives at repo root in `Payabo/`.
