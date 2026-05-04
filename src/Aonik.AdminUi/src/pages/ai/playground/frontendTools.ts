import type {
  PlaygroundFrontendToolHandler,
  PlaygroundFrontendToolRegistration,
} from '@/lib/playground-client';

export const playgroundFrontendToolNames = [
  'confirmAction',
  'display_fx_rate_chart',
  'display_budget_breakdown',
  'display_spending_pie_chart',
  'display_autopilot_proposal',
  'display_follow_up_suggestions',
  'display_option_selector',
] as const;

export type PlaygroundFrontendToolName = (typeof playgroundFrontendToolNames)[number];

interface ConfirmActionArgs {
  action?: string;
  description?: string;
  severity?: 'low' | 'medium' | 'high';
}

interface CreatePlaygroundFrontendToolsOptions {
  /** Promise-based confirm handler that receives toolCallId for React component resolution. */
  confirmAction?: (
    toolCallId: string,
    args: Required<ConfirmActionArgs>,
  ) => Promise<string>;
  /** Promise-based option selector that receives toolCallId for React component resolution. */
  selectOptions?: (
    toolCallId: string,
    args: {
      question: string;
      options: Array<{ label: string; description?: string }>;
      multiSelect: boolean;
    },
  ) => Promise<string>;
  includeConfirmAction?: boolean;
  includeDisplayTools?: boolean;
  includeOptionSelector?: boolean;
}

/** Fallback using browser native window.confirm (used when no React handler is provided). */
function defaultConfirmAction(
  _toolCallId: string,
  args: Required<ConfirmActionArgs>,
): Promise<string> {
  if (typeof window === 'undefined' || typeof window.confirm !== 'function') {
    return Promise.resolve('rejected');
  }

  const approved = window.confirm(
    `[${args.severity.toUpperCase()}] ${args.action}\n\n${args.description}\n\nApprove this action?`,
  );
  return Promise.resolve(approved ? 'approved' : 'rejected');
}

/** Fallback using browser native window.prompt (used when no React handler is provided). */
function defaultSelectOptions(
  _toolCallId: string,
  args: {
    question: string;
    options: Array<{ label: string; description?: string }>;
    multiSelect: boolean;
  },
): Promise<string> {
  if (args.options.length === 0) {
    return Promise.resolve('');
  }

  if (typeof window === 'undefined' || typeof window.prompt !== 'function') {
    return Promise.resolve(args.options[0].label);
  }

  const promptBody = args.options
    .map((option, index) => `${index + 1}. ${option.label}${option.description ? ` - ${option.description}` : ''}`)
    .join('\n');

  const rawSelection = window.prompt(
    `${args.question}\n\n${promptBody}\n\n${args.multiSelect ? 'Enter comma-separated option numbers.' : 'Enter one option number.'}`,
    '1',
  );

  if (!rawSelection) {
    return Promise.resolve(args.options[0].label);
  }

  const selectedLabels = rawSelection
    .split(',')
    .map((value) => Number.parseInt(value.trim(), 10))
    .filter((value) => Number.isFinite(value) && value >= 1 && value <= args.options.length)
    .map((value) => args.options[value - 1]?.label)
    .filter((label): label is string => typeof label === 'string' && label.length > 0);

  if (selectedLabels.length === 0) {
    return Promise.resolve(args.options[0].label);
  }

  const result = args.multiSelect ? selectedLabels : [selectedLabels[0]];
  return Promise.resolve(result.length <= 1 ? (result[0] ?? '') : JSON.stringify(result));
}

const displayHandler: PlaygroundFrontendToolHandler = async () => 'displayed';

export function createPlaygroundFrontendTools(
  options: CreatePlaygroundFrontendToolsOptions = {},
): Map<string, PlaygroundFrontendToolRegistration> {
  const registrations = new Map<string, PlaygroundFrontendToolRegistration>();
  const confirmAction = options.confirmAction ?? defaultConfirmAction;
  const selectOptions = options.selectOptions ?? defaultSelectOptions;
  const includeConfirmAction = options.includeConfirmAction ?? true;
  const includeDisplayTools = options.includeDisplayTools ?? true;
  const includeOptionSelector = options.includeOptionSelector ?? true;

  const confirmHandler: PlaygroundFrontendToolHandler = async (args, context) => {
    const normalizedArgs: Required<ConfirmActionArgs> = {
      action: typeof args.action === 'string' && args.action.trim().length > 0 ? args.action : 'Unknown action',
      description:
        typeof args.description === 'string' && args.description.trim().length > 0 ? args.description : '',
      severity:
        typeof args.severity === 'string' && ['low', 'medium', 'high'].includes(args.severity as string)
          ? (args.severity as 'low' | 'medium' | 'high')
          : 'medium',
    };

    return confirmAction(context.toolCallId, normalizedArgs);
  };

  if (includeConfirmAction) {
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
  }

  if (includeDisplayTools) {
    registrations.set('display_fx_rate_chart', {
      tool: {
        name: 'display_fx_rate_chart',
        description:
          'Display an FX rate chart showing a currency pair rate window with a timing signal (buy/hold/wait). Use when the user asks about exchange rates or remittance timing.',
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
          'Display a budget breakdown showing spending categories with over/under status. Use when the user asks about their budget, spending breakdown, or where their money is going.',
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

    registrations.set('display_spending_pie_chart', {
      tool: {
        name: 'display_spending_pie_chart',
        description:
          'Display a pie chart showing spending distribution by category. Use after pf_get_category_breakdown or pf_get_spending_summary to visualise how spending is split across categories.',
        parameters: {
          type: 'object',
          properties: {
            title: {
              type: 'string',
              description: 'Chart title (e.g., "Spending by Category — April 2026")',
            },
            currency: {
              type: 'string',
              description: 'ISO 4217 currency code (e.g., "USD")',
            },
            totalSpent: {
              type: 'number',
              description: 'Total amount spent across all categories',
            },
            categories: {
              type: 'array',
              description: 'Spending categories with amounts',
              items: {
                type: 'object',
                properties: {
                  name: {
                    type: 'string',
                    description: 'Category name (e.g., "Groceries")',
                  },
                  amount: {
                    type: 'number',
                    description: 'Amount spent in this category',
                  },
                  percentage: {
                    type: 'number',
                    description: 'Percentage of total spending (0-100)',
                  },
                },
                required: ['name', 'amount'],
              },
            },
          },
          required: ['currency', 'totalSpent', 'categories'],
        },
      },
      handler: displayHandler,
    });

    registrations.set('display_autopilot_proposal', {
      tool: {
        name: 'display_autopilot_proposal',
        description:
          'Display a structured proposal card for an automated action that an agent wants to take. Use when presenting a specific recommendation with details the user should review before the agent proceeds.',
        parameters: {
          type: 'object',
          properties: {
            agent: {
              type: 'string',
              description: 'Name of the agent making the proposal (e.g., "Bill Agent", "Savings Agent")',
            },
            action: {
              type: 'string',
              description: 'Short action title (e.g., "Schedule auto-pay for electricity")',
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
                    description: 'Detail label (e.g., "Amount")',
                  },
                  value: {
                    type: 'string',
                    description: 'Detail value (e.g., "£85.00")',
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

    registrations.set('display_follow_up_suggestions', {
      tool: {
        name: 'display_follow_up_suggestions',
        description:
          'Display 2 to 6 tappable follow-up suggestions in the chat. Use for optional next questions or next actions after answering the user. Do not use when the agent must block for a required choice; use display_option_selector for that.',
        parameters: {
          type: 'object',
          properties: {
            prompt: {
              type: 'string',
              description: 'Short lead-in above the suggestion chips (e.g., "Want to keep going?")',
            },
            suggestions: {
              type: 'array',
              description: 'The suggested follow-up prompts to show',
              items: {
                type: 'object',
                properties: {
                  label: {
                    type: 'string',
                    description: 'Short chip label shown to the user',
                  },
                  prompt: {
                    type: 'string',
                    description: 'Exact user message to send if the chip is tapped',
                  },
                  description: {
                    type: 'string',
                    description: 'Optional extra context shown under the chip label',
                  },
                },
                required: ['label', 'prompt'],
              },
            },
          },
          required: ['suggestions'],
        },
      },
      handler: displayHandler,
    });
  }

  if (includeOptionSelector) {
    registrations.set('display_option_selector', {
      tool: {
        name: 'display_option_selector',
        description:
          'Present a set of options for the user to choose from before proceeding. This tool blocks until the user selects. Use when you need the user to pick from a list (e.g., "Which account?", "Pick a category").',
        parameters: {
          type: 'object',
          properties: {
            question: {
              type: 'string',
              description: 'The prompt text (e.g., "Which account should I use?")',
            },
            options: {
              type: 'array',
              description: 'The available options to choose from',
              items: {
                type: 'object',
                properties: {
                  label: {
                    type: 'string',
                    description: 'Option label shown to the user',
                  },
                  description: {
                    type: 'string',
                    description: 'Optional description for the option',
                  },
                },
                required: ['label'],
              },
            },
            multiSelect: {
              type: 'boolean',
              description: 'If true, the user may select multiple options. Defaults to false.',
            },
          },
          required: ['question', 'options'],
        },
      },
      handler: async (args, context) => {
        const question =
          typeof args.question === 'string' && args.question.trim().length > 0
            ? args.question
            : 'Please choose an option';
        const optionsArg = Array.isArray(args.options) ? args.options : [];
        const normalizedOptions = optionsArg
          .filter((item): item is Record<string, unknown> => typeof item === 'object' && item !== null)
          .map((item) => ({
            label:
              typeof item.label === 'string' && item.label.trim().length > 0
                ? item.label
                : '',
            description:
              typeof item.description === 'string' && item.description.trim().length > 0
                ? item.description
                : undefined,
          }))
          .filter((item) => item.label.length > 0);

        return selectOptions(context.toolCallId, {
          question,
          options: normalizedOptions,
          multiSelect: args.multiSelect === true,
        });
      },
    });
  }

  return registrations;
}
