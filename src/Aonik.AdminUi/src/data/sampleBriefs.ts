/**
 * Curated sample User Briefs for playground testing.
 * Each brief matches the UserBrief schema from the backend.
 */

export interface SampleBrief {
  id: string;
  name: string;
  description: string;
  json: string;
}

export const sampleBriefs: SampleBrief[] = [
  {
    id: 'established-user-ade',
    name: 'Ade — Established User',
    description:
      'UK-based Nigerian diaspora, 5 months on platform. Supports family in Lagos, has spending history, goals, and budget pressure.',
    json: JSON.stringify(
      {
        asOf: '2026-04-03T10:00:00Z',
        user: { name: 'Ade', country: 'NG' },
        goals: [
          'pay_bills_abroad',
          'send_money',
          'track_spending',
          'build_emergency_fund',
          'reduce_transfer_fees',
        ],
        cash: { balance: 3420.5, currency: 'GBP' },
        period: { inflows: 2800.0, outflows: 1245.3, currency: 'GBP' },
        topCategories: [
          { name: 'Family Support', amount: 620.0 },
          { name: 'Groceries', amount: 215.4 },
          { name: 'Transport', amount: 148.0 },
          { name: 'Dining Out', amount: 132.5 },
        ],
        topMerchants: [
          { name: 'Tesco', amount: 168.9 },
          { name: 'Uber', amount: 92.4 },
        ],
        signals: [
          { title: 'Dining out creeping up', severity: 'Moderate' },
        ],
        risks: ['Dining Out at 132.50% of budget'],
        cashflowRisk: 1,
        missingData: [],
        aiCanDo: [
          'view_balances',
          'categorise_transactions',
          'generate_insights',
          'send_reminders',
        ],
        aiNeedsApproval: [
          'initiate_payment',
          'create_order',
          'modify_bill',
          'cancel_subscription',
        ],
      },
      null,
      2,
    ),
  },
  {
    id: 'new-user-chika',
    name: 'Chika — New User',
    description:
      'US-based, signed up 2 days ago. Completed setup (send money, pay bills). Zero accounts, zero transactions.',
    json: JSON.stringify(
      {
        asOf: '2026-04-03T10:00:00Z',
        user: { name: 'Chika', country: 'NG' },
        goals: ['send_money', 'pay_bills_abroad', 'save_more', 'reduce_transfer_fees'],
        cash: null,
        period: null,
        topCategories: [],
        topMerchants: [],
        signals: [],
        risks: [],
        cashflowRisk: 1,
        missingData: [
          'accounts',
          'transactions',
          'goals',
          'bills_and_subscriptions',
          'customer_insight_snapshot',
          'conversation_history',
        ],
        aiCanDo: [
          'view_balances',
          'categorise_transactions',
          'generate_insights',
          'send_reminders',
        ],
        aiNeedsApproval: [
          'initiate_payment',
          'create_order',
          'modify_bill',
          'cancel_subscription',
        ],
      },
      null,
      2,
    ),
  },
  {
    id: 'high-risk-amara',
    name: 'Amara — Cash-Tight User',
    description:
      'UK-based, active user with high cashflow risk. Bills exceed available balance. Tests Simi under financial pressure.',
    json: JSON.stringify(
      {
        asOf: '2026-04-03T10:00:00Z',
        user: { name: 'Amara', country: 'SN' },
        goals: [
          'send_money',
          'pay_bills_abroad',
          'track_spending',
          'reduce_transfer_fees',
          'build_emergency_fund',
        ],
        cash: { balance: 380.0, currency: 'GBP' },
        period: { inflows: 1600.0, outflows: 1890.0, currency: 'GBP' },
        topCategories: [
          { name: 'Family Support', amount: 1240.0 },
          { name: 'Rent', amount: 350.0 },
          { name: 'Groceries', amount: 180.0 },
        ],
        topMerchants: [
          { name: 'Wise', amount: 1240.0 },
          { name: 'Sainsburys', amount: 120.5 },
        ],
        signals: [
          { title: 'Cashflow shortfall likely this month', severity: 'High' },
          { title: 'Family support exceeding budget', severity: 'Moderate' },
        ],
        risks: [
          'Family Support at 137.80% of budget',
          'Groceries at 120.00% of budget',
          'Available balance below upcoming obligations',
        ],
        cashflowRisk: 3,
        missingData: [],
        aiCanDo: [
          'view_balances',
          'categorise_transactions',
          'generate_insights',
          'send_reminders',
        ],
        aiNeedsApproval: [
          'initiate_payment',
          'create_order',
          'modify_bill',
          'cancel_subscription',
        ],
      },
      null,
      2,
    ),
  },
];
