import fs from "node:fs";

const routesSource = fs.readFileSync(new URL("../src/app/routes.tsx", import.meta.url), "utf8");
const ordersApiSource = fs.readFileSync(new URL("../src/api/orders.ts", import.meta.url), "utf8");
const paymentsApiSource = fs.readFileSync(new URL("../src/api/payments.ts", import.meta.url), "utf8");
const dashboardApiSource = fs.readFileSync(new URL("../src/api/dashboard.ts", import.meta.url), "utf8");
const dashboardPageSource = fs.readFileSync(new URL("../src/pages/dashboard/Dashboard.tsx", import.meta.url), "utf8");
const transactionsPageSource = fs.readFileSync(new URL("../src/pages/dashboard/Transactions.tsx", import.meta.url), "utf8");

const requiredRoutes = [
  'path: "/auth/callback"',
  'path: "/register/success"',
  'path: "/payments/providers"',
  'path: "/payments/service/:id"',
  'path: "/payments/selection"',
  'path: "/payments/card-checkout"',
  'path: "/profile/personal"',
  'path: "/profile/login-details/email"'
];

for (const routeFragment of requiredRoutes) {
  if (!routesSource.includes(routeFragment)) {
    throw new Error(`Missing required route fragment: ${routeFragment}`);
  }
}

if (!ordersApiSource.includes('/public/orders/bill-payments/drafts')) {
  throw new Error("Missing draft order API integration");
}

if (!paymentsApiSource.includes('/public/payments/intents')) {
  throw new Error("Missing payment intent API integration");
}

const authApiSource = fs.readFileSync(new URL("../src/api/auth.ts", import.meta.url), "utf8");

if (!authApiSource.includes('grantType: "authorization_code"')) {
  throw new Error("Missing PKCE authorization code token exchange integration");
}

if (!dashboardApiSource.includes('/orders?')) {
  throw new Error("Missing orders list API integration for dashboard summary");
}

if (!dashboardApiSource.includes('/orders/${order.orderId}')) {
  throw new Error("Missing order detail API integration for dashboard summary");
}

if (dashboardApiSource.includes('/public/dashboard/summary')) {
  throw new Error("Dashboard summary must not depend on removed /public/dashboard/summary endpoint");
}

if (dashboardApiSource.includes('paymentHistory') || dashboardApiSource.includes('draftIntent')) {
  throw new Error("Dashboard summary must not fall back to local storage data");
}

if (!dashboardPageSource.includes('/payments/transaction-details?id=')) {
  throw new Error("Dashboard transactions must deep-link to transaction details");
}

if (transactionsPageSource.includes("paymentHistory")) {
  throw new Error("Transactions page must be API-backed and not import local paymentHistory");
}

if (!transactionsPageSource.includes("getRecentTransactions")) {
  throw new Error("Transactions page must load data from dashboard API contract");
}

console.log("Smoke checks passed: dashboard/profile route and API integration guardrails are in place.");
