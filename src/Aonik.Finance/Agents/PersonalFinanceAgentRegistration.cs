using Aonik.Agents.Contracts.Services;
using Aonik.Finance.Agents.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Aonik.Finance.Agents;

/// <summary>
/// Personal finance domain agent descriptor. Builds the personal finance
/// <see cref="ChatClientAgent"/> with account, transaction, bill, and
/// insights tools. Mutating tools (create account, archive, create bill, etc.)
/// are wrapped with <see cref="ApprovalRequiredAIFunction"/> to enforce
/// human-in-the-loop approval via the MAF proposal pattern.
/// </summary>
public sealed class PersonalFinanceAgentDescriptor : IDomainAgentDescriptor
{
    public string Name => "personal-finance-agent";

    public string Description =>
        "Manages personal financial accounts, transactions, bills, and spending insights " +
        "for the current user. Can list and query accounts, transactions, and bills; " +
        "create new accounts, manual transactions, and recurring bills; archive accounts " +
        "and bills; and provide spending summaries, category breakdowns, merchant breakdowns, " +
        "and a personal finance dashboard overview.";

    internal const string Instructions =
        """
        You are the AONIK Personal Finance Agent. You help users manage their personal
        financial life within the AONIK platform.

        Your capabilities include:
        - **Accounts**: List, view, create, and archive personal financial accounts (checking,
          savings, credit cards, investments, loans)
        - **Transactions**: List and search transactions with filters (date range, account,
          category, merchant search), view transaction details, and create manual transactions
        - **Bills**: List, view, create, and archive recurring bills; check upcoming bills
          due within a given time window
        - **Spending Insights**: Get spending summaries, category breakdowns, merchant breakdowns,
          and account-level breakdowns for any analysis period
        - **Dashboard**: Get a comprehensive personal finance dashboard with net worth, available
          balance, upcoming bills, and monthly spending overview

        ## General Rules
        1. Always present monetary amounts with their currency code (e.g. $1,250.00 USD).
        2. When listing transactions, default to the current month if no date range is specified.
        3. Reference entities by their IDs when reporting results.
        4. For spending insights, clearly state the analysis period being used.
        5. When creating accounts or bills, confirm all details with the user before proceeding.
        6. If an operation fails, explain the error clearly and suggest corrective action.
        7. Never expose internal system details or raw exception messages to the user.
        8. For sensitive financial data, summarise rather than dumping raw records when possible.
        """;

    string? IDomainAgentDescriptor.Instructions => Instructions;

    public AIAgent Build(IChatClient chatClient, IServiceProvider serviceProvider)
    {
        var tools = GetTools(serviceProvider).ToList();

        return new ChatClientAgent(
            chatClient,
            name: Name,
            instructions: Instructions,
            tools: tools);
    }

    public AIAgent Build(
        IChatClient chatClient,
        IServiceProvider serviceProvider,
        string? instructionsOverride,
        IReadOnlySet<string>? allowedToolNames)
    {
        var tools = GetTools(serviceProvider)
            .Where(t => allowedToolNames is null || allowedToolNames.Contains(t.Name))
            .ToList();

        return new ChatClientAgent(
            chatClient,
            name: Name,
            instructions: instructionsOverride ?? Instructions,
            tools: tools);
    }

    public IReadOnlyList<string> GetToolNames(IServiceProvider serviceProvider)
    {
        return GetTools(serviceProvider).Select(t => t.Name).ToList();
    }

    private static IEnumerable<AITool> GetTools(IServiceProvider serviceProvider)
    {
        return PersonalFinanceTools.CreateAll(serviceProvider);
    }
}
