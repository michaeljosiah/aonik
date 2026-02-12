import fs from "node:fs";

const routesSource = fs.readFileSync(new URL("../src/app/routes.tsx", import.meta.url), "utf8");
const ordersApiSource = fs.readFileSync(new URL("../src/api/orders.ts", import.meta.url), "utf8");
const paymentsApiSource = fs.readFileSync(new URL("../src/api/payments.ts", import.meta.url), "utf8");
const dashboardApiSource = fs.readFileSync(new URL("../src/api/dashboard.ts", import.meta.url), "utf8");
const dashboardPageSource = fs.readFileSync(new URL("../src/pages/dashboard/Dashboard.tsx", import.meta.url), "utf8");

const requiredRoutes = [
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

if (!dashboardApiSource.includes('/public/dashboard/summary')) {
  throw new Error("Missing dashboard summary API integration");
}

if (dashboardApiSource.includes('paymentHistory') || dashboardApiSource.includes('draftIntent')) {
  throw new Error("Dashboard summary must not fall back to local storage data");
}

if (!dashboardPageSource.includes('/payments/transaction-details?id=')) {
  throw new Error("Dashboard transactions must deep-link to transaction details");
}

console.log("Smoke checks passed: dashboard/api/profile route and integration guardrails are in place.");
