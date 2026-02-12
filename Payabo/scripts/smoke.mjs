import fs from "node:fs";

const routesSource = fs.readFileSync(new URL("../src/app/routes.tsx", import.meta.url), "utf8");
const ordersApiSource = fs.readFileSync(new URL("../src/api/orders.ts", import.meta.url), "utf8");
const paymentsApiSource = fs.readFileSync(new URL("../src/api/payments.ts", import.meta.url), "utf8");

const requiredRoutes = [
  'path: "/payments/providers"',
  'path: "/payments/service/:id"',
  'path: "/payments/selection"',
  'path: "/payments/card-checkout"'
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

console.log("Smoke checks passed: core provider -> service -> payment selection -> checkout wiring is present.");
