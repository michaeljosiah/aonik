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
        userProfile: {
          preferredName: 'Ade',
          fullName: 'Adeola Okonkwo',
          givenName: 'Adeola',
          email: 'adeola.okonkwo@gmail.com',
          phoneNumber: '+447911234567',
          userCreatedAt: '2025-11-15T09:30:00Z',
          communicationStyle: 'casual',
          financialPosture: 'cautious-optimist',
          corridorCountries: ['NG', 'GB'],
          householdContext: 'Supports mother and younger sister in Lagos',
          incomeRhythm: 'monthly, last working day',
          primaryNeeds: ['bill_payments', 'family_support', 'savings'],
        },
        setupProfile: {
          selectedUseCases: ['pay_bills_abroad', 'send_money', 'track_spending'],
          accountSourceTypes: ['bank_account'],
          connectChoice: 'connect_later',
          responsibilities: ['rent_for_family', 'school_fees', 'utilities'],
          supportType: 'recurring',
          financialGoals: ['build_emergency_fund', 'reduce_transfer_fees'],
          completed: true,
        },
        financialFocus: {
          currentGoals: [
            {
              goalId: 'a1b2c3d4-e5f6-7890-abcd-ef1234567890',
              name: 'Emergency fund',
              targetAmount: 2000.0,
              progressAmount: 850.0,
              currency: 'GBP',
              targetDate: '2026-09-30',
              status: 'on_track',
            },
          ],
          supportObligations: [
            {
              displayName: 'Mum — rent (Lagos)',
              amount: 150000.0,
              currency: 'NGN',
              frequency: 'monthly',
              nextDueDate: '2026-04-25',
            },
            {
              displayName: 'Bisi — school fees',
              amount: 85000.0,
              currency: 'NGN',
              frequency: 'termly',
              nextDueDate: '2026-05-10',
            },
          ],
        },
        currentState: {
          cashSummary: {
            totalBalance: 3420.5,
            availableBalance: 2870.5,
            currency: 'GBP',
          },
          nextBills: [
            {
              billId: 'b1111111-1111-1111-1111-111111111111',
              payee: "IKEDC — Mum's electricity",
              amount: 12500.0,
              currency: 'NGN',
              dueDate: '2026-04-08',
              autopay: false,
            },
            {
              billId: 'b2222222-2222-2222-2222-222222222222',
              payee: 'DSTV Premium',
              amount: 29500.0,
              currency: 'NGN',
              dueDate: '2026-04-15',
              autopay: true,
            },
          ],
          subscriptions: [
            {
              subscriptionId: 's1111111-1111-1111-1111-111111111111',
              merchant: 'DSTV Premium',
              expectedAmount: 29500.0,
              currency: 'NGN',
              renewalDate: '2026-04-15',
            },
          ],
          spendSummaries: [
            {
              currency: 'GBP',
              totalSpend: 1245.3,
              topCategories: [
                { category: 'Family Support', amount: 620.0, percentage: 49.8 },
                { category: 'Groceries', amount: 215.4, percentage: 17.3 },
                { category: 'Transport', amount: 148.0, percentage: 11.9 },
                { category: 'Dining Out', amount: 132.5, percentage: 10.6 },
              ],
              periodStart: '2026-03-01',
              periodEnd: '2026-03-31',
            },
          ],
          budgetPressureCategories: [
            { category: 'Dining Out', budgeted: 100.0, actual: 132.5, percentUsed: 132.5 },
          ],
        },
        customerInsightAiInterpretation: {
          headline: 'Steady earner with strong family commitment — watch dining spend',
          summary:
            "Ade is disciplined with family support obligations and making solid progress on the emergency fund. Dining out has been creeping over budget for two months.",
          keyObservations: [
            'Family support is the single largest spending category at nearly 50% of outflows.',
            'Dining out has exceeded budget two months running.',
            'Emergency fund savings are consistent and on track.',
          ],
          recommendedFocusAreas: [
            'Tighten dining budget or acknowledge the higher baseline.',
            'Review FX timing on the next NGN transfer.',
          ],
          referencedMetricKeys: ['dining_creep', 'consistent_family_support'],
          caveats: [],
        },
        dataAvailability: {
          isNewUser: false,
          hasLimitedFinancialData: false,
          summary: 'Sufficient recent data is available for normal personal-finance guidance.',
          missingDataAreas: [],
        },
        cashflowRisk: 1,
        behaviouralInsights: [
          {
            insightType: 'Spending',
            title: 'Dining out creeping up',
            summary: 'Dining spend exceeded budget by 32.5% in March.',
            confidence: 0.9,
          },
        ],
        recentConversationMemory: [
          {
            sessionDate: '2026-03-28T14:20:00Z',
            summary:
              "Ade asked about the GBP/NGN rate and whether to send mum's rent early. Simi advised holding 2-3 days.",
            openLoops: [
              {
                description: "Follow up on whether Ade sent mum's rent",
                priority: 'medium',
                dueDate: '2026-04-05',
              },
            ],
            recommendationOutcomes: [],
          },
        ],
        policyContext: {
          riskTier: 'standard',
          aiCanDo: ['view_balances', 'categorise_transactions', 'generate_insights'],
          aiCannotDoWithoutApproval: ['initiate_payment', 'create_order', 'modify_bill'],
        },
        generatedAt: '2026-04-03T10:00:00Z',
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
        userProfile: {
          preferredName: 'Chika',
          fullName: 'Chika Nwosu',
          givenName: 'Chika',
          email: 'chika.nwosu@outlook.com',
          phoneNumber: '+14155559876',
          userCreatedAt: '2026-04-01T18:45:00Z',
          corridorCountries: ['NG', 'US'],
          primaryNeeds: [],
        },
        setupProfile: {
          selectedUseCases: ['send_money', 'pay_bills_abroad'],
          accountSourceTypes: ['bank_account'],
          connectChoice: 'connect_later',
          responsibilities: ['utilities', 'school_fees'],
          supportType: 'recurring',
          financialGoals: ['save_more', 'reduce_transfer_fees'],
          completed: true,
        },
        financialFocus: { currentGoals: [], supportObligations: [] },
        currentState: {
          cashSummary: { totalBalance: 0.0, availableBalance: 0.0, currency: 'USD' },
          nextBills: [],
          subscriptions: [],
          spendSummaries: [],
          budgetPressureCategories: [],
        },
        dataAvailability: {
          isNewUser: true,
          hasLimitedFinancialData: true,
          summary:
            'This is a new Payabo user with little or no financial history yet. Use the setup answers as the main context.',
          missingDataAreas: [
            'accounts',
            'transactions',
            'goals',
            'bills_and_subscriptions',
            'customer_insight_snapshot',
            'conversation_history',
          ],
        },
        cashflowRisk: 1,
        behaviouralInsights: [],
        recentConversationMemory: [],
        policyContext: {
          riskTier: 'standard',
          aiCanDo: ['view_balances', 'categorise_transactions', 'generate_insights'],
          aiCannotDoWithoutApproval: ['initiate_payment', 'create_order', 'modify_bill'],
        },
        generatedAt: '2026-04-03T10:00:00Z',
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
        userProfile: {
          preferredName: 'Amara',
          fullName: 'Amara Diallo',
          givenName: 'Amara',
          email: 'amara.diallo@gmail.com',
          phoneNumber: '+447700112233',
          userCreatedAt: '2025-08-20T11:00:00Z',
          communicationStyle: 'direct',
          financialPosture: 'stressed',
          corridorCountries: ['SN', 'GB'],
          householdContext: 'Sole earner, supporting elderly parents in Dakar',
          incomeRhythm: 'biweekly, Friday',
          primaryNeeds: ['family_support', 'bill_payments'],
        },
        setupProfile: {
          selectedUseCases: ['send_money', 'pay_bills_abroad', 'track_spending'],
          accountSourceTypes: ['bank_account'],
          connectChoice: 'connected',
          responsibilities: ['rent_for_family', 'medical', 'utilities'],
          supportType: 'recurring',
          financialGoals: ['reduce_transfer_fees', 'build_emergency_fund'],
          completed: true,
        },
        financialFocus: {
          currentGoals: [
            {
              goalId: 'g1111111-1111-1111-1111-111111111111',
              name: 'Emergency fund',
              targetAmount: 1500.0,
              progressAmount: 120.0,
              currency: 'GBP',
              targetDate: '2026-12-31',
              status: 'behind',
            },
          ],
          supportObligations: [
            {
              displayName: 'Parents — rent (Dakar)',
              amount: 200000.0,
              currency: 'XOF',
              frequency: 'monthly',
              nextDueDate: '2026-04-10',
            },
            {
              displayName: 'Papa — medical',
              amount: 75000.0,
              currency: 'XOF',
              frequency: 'monthly',
              nextDueDate: '2026-04-15',
            },
          ],
        },
        currentState: {
          cashSummary: { totalBalance: 380.0, availableBalance: 280.0, currency: 'GBP' },
          nextBills: [
            {
              billId: 'b4444444-4444-4444-4444-444444444444',
              payee: 'Senelec — electricity',
              amount: 45000.0,
              currency: 'XOF',
              dueDate: '2026-04-07',
              autopay: false,
            },
            {
              billId: 'b5555555-5555-5555-5555-555555555555',
              payee: 'Papa medical appointment',
              amount: 75000.0,
              currency: 'XOF',
              dueDate: '2026-04-15',
              autopay: false,
            },
          ],
          subscriptions: [],
          spendSummaries: [
            {
              currency: 'EUR',
              totalSpend: 1890.0,
              topCategories: [
                { category: 'Family Support', amount: 1240.0, percentage: 65.6 },
                { category: 'Rent', amount: 350.0, percentage: 18.5 },
                { category: 'Groceries', amount: 180.0, percentage: 9.5 },
              ],
              periodStart: '2026-03-01',
              periodEnd: '2026-03-31',
            },
          ],
          budgetPressureCategories: [
            { category: 'Family Support', budgeted: 900.0, actual: 1240.0, percentUsed: 137.8 },
            { category: 'Groceries', budgeted: 150.0, actual: 180.0, percentUsed: 120.0 },
          ],
        },
        dataAvailability: {
          isNewUser: false,
          hasLimitedFinancialData: false,
          summary: 'Sufficient recent data is available for normal personal-finance guidance.',
          missingDataAreas: [],
        },
        cashflowRisk: 3,
        behaviouralInsights: [
          {
            insightType: 'Risk',
            title: 'Cashflow shortfall likely this month',
            summary:
              'Available balance (£280) is below estimated upcoming obligations (~£450 GBP equivalent).',
            confidence: 0.95,
          },
        ],
        recentConversationMemory: [],
        policyContext: {
          riskTier: 'standard',
          aiCanDo: ['view_balances', 'categorise_transactions', 'generate_insights'],
          aiCannotDoWithoutApproval: ['initiate_payment', 'create_order', 'modify_bill'],
        },
        generatedAt: '2026-04-03T10:00:00Z',
      },
      null,
      2,
    ),
  },
];
