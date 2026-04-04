import type {
  PlaygroundFrontendToolHandler,
  PlaygroundFrontendToolRegistration,
} from '@/lib/playground-client';

export const playgroundFrontendToolNames = [
  'confirmAction',
  'display_fx_rate_chart',
  'display_budget_breakdown',
  'display_autopilot_proposal',
] as const;

export type PlaygroundFrontendToolName = (typeof playgroundFrontendToolNames)[number];

interface ConfirmActionArgs {
  action?: string;
  description?: string;
  severity?: string;
}

interface CreatePlaygroundFrontendToolsOptions {
  confirmAction?: (args: Required<ConfirmActionArgs>) => Promise<boolean> | boolean;
}

function defaultConfirmAction(args: Required<ConfirmActionArgs>): boolean {
  if (typeof window === 'undefined' || typeof window.confirm !== 'function') {
    return false;
  }

  return window.confirm(
    `[${args.severity.toUpperCase()}] ${args.action}\n\n${args.description}\n\nApprove this action?`,
  );
}

const displayHandler: PlaygroundFrontendToolHandler = async () => 'displayed';

export function createPlaygroundFrontendTools(
  options: CreatePlaygroundFrontendToolsOptions = {},
): Map<string, PlaygroundFrontendToolRegistration> {
  const registrations = new Map<string, PlaygroundFrontendToolRegistration>();
  const confirmAction = options.confirmAction ?? defaultConfirmAction;

  const confirmHandler: PlaygroundFrontendToolHandler = async (args) => {
    const normalizedArgs = {
      action: typeof args.action === 'string' && args.action.trim().length > 0 ? args.action : 'Unknown action',
      description:
        typeof args.description === 'string' && args.description.trim().length > 0 ? args.description : '',
      severity:
        typeof args.severity === 'string' && args.severity.trim().length > 0 ? args.severity : 'medium',
    };

    return (await confirmAction(normalizedArgs)) ? 'approved' : 'rejected';
  };

  registrations.set('confirmAction', {
    tool: {
      name: 'confirmAction',
      description:
        'Request user approval before executing a mutating action. The user will see an approval dialog with Approve/Reject options. Use this for any action that creates, modifies, or deletes data.',
      parameters: {
        type: 'object',
        properties: {
          action: {
            type: 'string',
            description: 'Short name of the action (e.g., "Create Invoice", "Cancel Payment")',
          },
          description: {
            type: 'string',
            description: 'Detailed description of what will happen if approved',
          },
          severity: {
            type: 'string',
            enum: ['low', 'medium', 'high'],
            description: 'Risk level of the action. Defaults to medium.',
          },
        },
        required: ['action', 'description'],
      },
    },
    handler: confirmHandler,
  });

  registrations.set('display_fx_rate_chart', {
    tool: {
      name: 'display_fx_rate_chart',
      description:
        'Display an FX rate chart showing a currency pair rate window with a timing signal.',
      parameters: {
        type: 'object',
        properties: {
          baseCurrency: {
            type: 'string',
            description: 'ISO 4217 base currency code (e.g., "GBP")',
          },
          targetCurrency: {
            type: 'string',
            description: 'ISO 4217 target currency code (e.g., "NGN")',
          },
          rates: {
            type: 'array',
            description: 'Historical rate data points',
            items: {
              type: 'object',
              properties: {
                date: {
                  type: 'string',
                  description: 'Date label (e.g., "Mar 15")',
                },
                rate: {
                  type: 'number',
                  description: 'Exchange rate value',
                },
              },
              required: ['date', 'rate'],
            },
          },
          signal: {
            type: 'string',
            enum: ['buy', 'hold', 'wait'],
            description: 'Timing signal recommendation',
          },
          signalReason: {
            type: 'string',
            description: 'Brief explanation of the signal',
          },
        },
        required: ['baseCurrency', 'targetCurrency', 'rates', 'signal'],
      },
    },
    handler: displayHandler,
  });

  registrations.set('display_budget_breakdown', {
    tool: {
      name: 'display_budget_breakdown',
      description:
        'Display a budget breakdown showing spending categories with over or under status.',
      parameters: {
        type: 'object',
        properties: {
          period: {
            type: 'string',
            description: 'Budget period label (e.g., "March 2026")',
          },
          totalBudget: {
            type: 'number',
            description: 'Total budgeted amount for the period',
          },
          totalSpent: {
            type: 'number',
            description: 'Total amount spent so far',
          },
          currency: {
            type: 'string',
            description: 'ISO 4217 currency code (e.g., "GBP")',
          },
          categories: {
            type: 'array',
            description: 'Spending categories with budget vs actual',
            items: {
              type: 'object',
              properties: {
                name: {
                  type: 'string',
                  description: 'Category name (e.g., "Groceries")',
                },
                budgeted: {
                  type: 'number',
                  description: 'Budgeted amount for this category',
                },
                spent: {
                  type: 'number',
                  description: 'Amount spent in this category',
                },
                status: {
                  type: 'string',
                  enum: ['under', 'on_track', 'over'],
                  description: 'Whether spending is under, on track, or over budget',
                },
              },
              required: ['name', 'budgeted', 'spent', 'status'],
            },
          },
        },
        required: ['period', 'totalBudget', 'totalSpent', 'currency', 'categories'],
      },
    },
    handler: displayHandler,
  });

  registrations.set('display_autopilot_proposal', {
    tool: {
      name: 'display_autopilot_proposal',
      description:
        'Display a structured proposal card for an automated action that the user should review.',
      parameters: {
        type: 'object',
        properties: {
          agent: {
            type: 'string',
            description: 'Name of the agent making the proposal (e.g., "Bill Agent")',
          },
          action: {
            type: 'string',
            description: 'Short action title',
          },
          description: {
            type: 'string',
            description: 'Detailed explanation of the proposal',
          },
          details: {
            type: 'array',
            description: 'Key-value detail rows for the proposal card',
            items: {
              type: 'object',
              properties: {
                label: {
                  type: 'string',
                  description: 'Detail label',
                },
                value: {
                  type: 'string',
                  description: 'Detail value',
                },
              },
              required: ['label', 'value'],
            },
          },
          severity: {
            type: 'string',
            enum: ['low', 'medium', 'high'],
            description: 'Importance level. Defaults to medium.',
          },
        },
        required: ['agent', 'action', 'description'],
      },
    },
    handler: displayHandler,
  });

  return registrations;
}
