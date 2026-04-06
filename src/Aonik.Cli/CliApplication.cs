using Aonik.Cli.Commands;
using Aonik.Cli.Infrastructure;
using Aonik.Cli.Models;
using Spectre.Console;
using System.CommandLine;

namespace Aonik.Cli;

public sealed class CliApplication
{
    private readonly RootCommand _rootCommand;

    public CliApplication(
        AuthCommandHandler authCommandHandler,
        AgentCommandHandler agentCommandHandler,
        OpsCommandHandler opsCommandHandler,
        ApprovalCommandHandler approvalCommandHandler,
        ShellCommandHandler shellCommandHandler)
    {
        _rootCommand = BuildRootCommand(
            authCommandHandler,
            agentCommandHandler,
            opsCommandHandler,
            approvalCommandHandler,
            shellCommandHandler);
    }

    public Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        return _rootCommand.Parse(args).InvokeAsync(cancellationToken: cancellationToken);
    }

    public static CliApplication CreateDefault()
    {
        var httpClient = new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Aonik.Cli/0.2");

        var apiClient = new AonikCliApiClient(httpClient);
        var sessionStore = new FileSessionStore();
        var outputWriter = new TextWriterCliOutputWriter(Console.Out);
        var authCommandHandler = new AuthCommandHandler(apiClient, sessionStore, outputWriter);
        var agentCommandHandler = new AgentCommandHandler(apiClient, sessionStore, outputWriter);
        var opsCommandHandler = new OpsCommandHandler(apiClient, sessionStore, outputWriter);
        var approvalCommandHandler = new ApprovalCommandHandler(apiClient, sessionStore, outputWriter);
        var shellCommandHandler = new ShellCommandHandler(
            sessionStore,
            authCommandHandler,
            agentCommandHandler,
            approvalCommandHandler,
            outputWriter,
            AnsiConsole.Console);

        return new CliApplication(
            authCommandHandler,
            agentCommandHandler,
            opsCommandHandler,
            approvalCommandHandler,
            shellCommandHandler);
    }

    private static RootCommand BuildRootCommand(
        AuthCommandHandler authCommandHandler,
        AgentCommandHandler agentCommandHandler,
        OpsCommandHandler opsCommandHandler,
        ApprovalCommandHandler approvalCommandHandler,
        ShellCommandHandler shellCommandHandler)
    {
        var rootCommand = new RootCommand("AONIK CLI");
        rootCommand.Add(BuildAuthCommand(authCommandHandler));
        rootCommand.Add(BuildAgentCommand(agentCommandHandler));
        rootCommand.Add(BuildOpsCommand(opsCommandHandler));
        rootCommand.Add(BuildApprovalCommand(approvalCommandHandler));
        rootCommand.Add(BuildShellCommand(shellCommandHandler));
        return rootCommand;
    }

    private static Command BuildAuthCommand(AuthCommandHandler authCommandHandler)
    {
        var authCommand = new Command("auth", "Authenticate and manage the local AONIK session.");

        var loginCommand = new Command("login", "Create a local session using a bearer token or password grant.");
        var baseUrlOption = new Option<string>("--base-url")
        {
            Description = "AONIK API base URL.",
            Required = true
        };
        var usernameOption = new Option<string?>("--username") { Description = "Username or email for password login." };
        var passwordOption = new Option<string?>("--password") { Description = "Password for password login." };
        var accessTokenOption = new Option<string?>("--access-token") { Description = "Existing bearer token." };
        var clientIdOption = new Option<string?>("--client-id") { Description = "Optional OAuth client identifier." };
        var scopeOption = new Option<string?>("--scope") { Description = "Optional OAuth scope." };
        var tenantIdOption = new Option<Guid?>("--tenant-id") { Description = "Optional tenant override." };
        var loginOutputOption = CreateOutputOption(includeNdjson: false);

        loginCommand.Add(baseUrlOption);
        loginCommand.Add(usernameOption);
        loginCommand.Add(passwordOption);
        loginCommand.Add(accessTokenOption);
        loginCommand.Add(clientIdOption);
        loginCommand.Add(scopeOption);
        loginCommand.Add(tenantIdOption);
        loginCommand.Add(loginOutputOption);
        loginCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            return await authCommandHandler.LoginAsync(
                new LoginOptions(
                    parseResult.GetRequiredValue(baseUrlOption),
                    parseResult.GetValue(usernameOption),
                    parseResult.GetValue(passwordOption),
                    parseResult.GetValue(accessTokenOption),
                    parseResult.GetValue(clientIdOption),
                    parseResult.GetValue(scopeOption),
                    parseResult.GetValue(tenantIdOption),
                    OutputModeParser.Parse(parseResult.GetValue(loginOutputOption))),
                cancellationToken);
        });

        var statusCommand = new Command("status", "Show the locally stored session.");
        var statusOutputOption = CreateOutputOption(includeNdjson: false);
        statusCommand.Add(statusOutputOption);
        statusCommand.SetAction((parseResult, cancellationToken) =>
            authCommandHandler.StatusAsync(
                OutputModeParser.Parse(parseResult.GetValue(statusOutputOption)),
                cancellationToken));

        var whoAmICommand = new Command("whoami", "Resolve the authenticated user from the API.");
        var whoAmIOutputOption = CreateOutputOption(includeNdjson: false);
        whoAmICommand.Add(whoAmIOutputOption);
        whoAmICommand.SetAction((parseResult, cancellationToken) =>
            authCommandHandler.WhoAmIAsync(
                OutputModeParser.Parse(parseResult.GetValue(whoAmIOutputOption)),
                cancellationToken));

        var logoutCommand = new Command("logout", "Clear the stored AONIK session.");
        logoutCommand.SetAction((_, cancellationToken) => authCommandHandler.LogoutAsync(cancellationToken));

        authCommand.Add(loginCommand);
        authCommand.Add(statusCommand);
        authCommand.Add(whoAmICommand);
        authCommand.Add(logoutCommand);
        return authCommand;
    }

    private static Command BuildAgentCommand(AgentCommandHandler agentCommandHandler)
    {
        var agentCommand = new Command("agent", "Interact with AONIK domain agents.");

        var listCommand = new Command("list", "List registered agents.");
        var listOutputOption = CreateOutputOption(includeNdjson: false);
        listCommand.Add(listOutputOption);
        listCommand.SetAction((parseResult, cancellationToken) =>
            agentCommandHandler.ListAsync(
                OutputModeParser.Parse(parseResult.GetValue(listOutputOption)),
                cancellationToken));

        var runCommand = new Command("run", "Send a message through the master orchestrator.");
        var messageOption = new Option<string>("--message")
        {
            Description = "User message to send.",
            Required = true
        };
        var sessionIdOption = new Option<string?>("--session-id") { Description = "Optional session override." };
        var threadIdOption = new Option<string?>("--thread-id") { Description = "Optional thread override." };
        var runOutputOption = CreateOutputOption(includeNdjson: false);
        runCommand.Add(messageOption);
        runCommand.Add(sessionIdOption);
        runCommand.Add(threadIdOption);
        runCommand.Add(runOutputOption);
        runCommand.SetAction((parseResult, cancellationToken) =>
            agentCommandHandler.RunAsync(
                new RunAgentOptions(
                    parseResult.GetRequiredValue(messageOption),
                    parseResult.GetValue(sessionIdOption),
                    parseResult.GetValue(threadIdOption),
                    OutputModeParser.Parse(parseResult.GetValue(runOutputOption))),
                cancellationToken));

        var streamCommand = new Command("stream", "Stream AG-UI events from the master orchestrator.");
        var streamMessageOption = new Option<string>("--message")
        {
            Description = "User message to send.",
            Required = true
        };
        var streamThreadIdOption = new Option<string?>("--thread-id") { Description = "Optional thread override." };
        var runIdOption = new Option<string?>("--run-id") { Description = "Optional run identifier." };
        var agentIdOption = new Option<string?>("--agent-id") { Description = "Optional direct agent name." };
        var streamOutputOption = CreateOutputOption(includeNdjson: true);
        streamCommand.Add(streamMessageOption);
        streamCommand.Add(streamThreadIdOption);
        streamCommand.Add(runIdOption);
        streamCommand.Add(agentIdOption);
        streamCommand.Add(streamOutputOption);
        streamCommand.SetAction((parseResult, cancellationToken) =>
            agentCommandHandler.StreamAsync(
                new StreamAgentOptions(
                    parseResult.GetRequiredValue(streamMessageOption),
                    parseResult.GetValue(streamThreadIdOption),
                    parseResult.GetValue(runIdOption),
                    parseResult.GetValue(agentIdOption),
                    OutputModeParser.Parse(parseResult.GetValue(streamOutputOption))),
                cancellationToken));

        var threadsCommand = new Command("threads", "List recent chat threads.");
        var pageOption = new Option<int>("--page") { Description = "Results page number." };
        var pageSizeOption = new Option<int>("--page-size") { Description = "Results per page." };
        var threadsOutputOption = CreateOutputOption(includeNdjson: false);
        threadsCommand.Add(pageOption);
        threadsCommand.Add(pageSizeOption);
        threadsCommand.Add(threadsOutputOption);
        threadsCommand.SetAction((parseResult, cancellationToken) =>
        {
            var parsedPageSize = parseResult.GetValue(pageSizeOption);
            return agentCommandHandler.ListThreadsAsync(
                new ListThreadsOptions(
                    Page: Math.Max(parseResult.GetValue(pageOption), 1),
                    PageSize: parsedPageSize is > 0 and <= 100 ? parsedPageSize : 20,
                    OutputMode: OutputModeParser.Parse(parseResult.GetValue(threadsOutputOption))),
                cancellationToken);
        });

        var threadCommand = new Command("thread", "Get a specific thread and its messages.");
        var threadIdArgument = new Argument<Guid>("thread-id");
        var threadOutputOption = CreateOutputOption(includeNdjson: false);
        threadCommand.Add(threadIdArgument);
        threadCommand.Add(threadOutputOption);
        threadCommand.SetAction((parseResult, cancellationToken) =>
            agentCommandHandler.GetThreadAsync(
                parseResult.GetRequiredValue(threadIdArgument),
                OutputModeParser.Parse(parseResult.GetValue(threadOutputOption)),
                cancellationToken));

        agentCommand.Add(listCommand);
        agentCommand.Add(runCommand);
        agentCommand.Add(streamCommand);
        agentCommand.Add(threadsCommand);
        agentCommand.Add(threadCommand);
        return agentCommand;
    }

    private static Command BuildOpsCommand(OpsCommandHandler opsCommandHandler)
    {
        var opsCommand = new Command("ops", "Run explicit operational commands.");

        var workflowCommand = new Command("workflow", "Run a named advisory workflow.");
        var workflowNameOption = new Option<string>("--workflow-name") { Description = "Workflow name.", Required = true };
        var workflowInputOption = new Option<string>("--input") { Description = "Workflow input.", Required = true };
        var workflowOutputOption = CreateOutputOption(includeNdjson: false);
        workflowCommand.Add(workflowNameOption);
        workflowCommand.Add(workflowInputOption);
        workflowCommand.Add(workflowOutputOption);
        workflowCommand.SetAction((parseResult, cancellationToken) =>
            opsCommandHandler.RunWorkflowAsync(
                new RunWorkflowOptions(
                    parseResult.GetRequiredValue(workflowNameOption),
                    parseResult.GetRequiredValue(workflowInputOption),
                    OutputModeParser.Parse(parseResult.GetValue(workflowOutputOption))),
                cancellationToken));

        var jobsCommand = new Command("jobs", "Inspect and trigger scheduled jobs.");
        var jobsListCommand = new Command("list", "List scheduled jobs.");
        var jobsListOutputOption = CreateOutputOption(includeNdjson: false);
        jobsListCommand.Add(jobsListOutputOption);
        jobsListCommand.SetAction((parseResult, cancellationToken) =>
            opsCommandHandler.ListJobsAsync(
                OutputModeParser.Parse(parseResult.GetValue(jobsListOutputOption)),
                cancellationToken));

        var jobsHealthCommand = new Command("health", "Show scheduler health.");
        var jobsHealthOutputOption = CreateOutputOption(includeNdjson: false);
        jobsHealthCommand.Add(jobsHealthOutputOption);
        jobsHealthCommand.SetAction((parseResult, cancellationToken) =>
            opsCommandHandler.SchedulerHealthAsync(
                OutputModeParser.Parse(parseResult.GetValue(jobsHealthOutputOption)),
                cancellationToken));

        var jobsTriggerCommand = new Command("trigger", "Trigger a scheduled job immediately.");
        var jobNameOption = new Option<string>("--job-name") { Description = "Scheduled job name.", Required = true };
        var jobsTriggerOutputOption = CreateOutputOption(includeNdjson: false);
        jobsTriggerCommand.Add(jobNameOption);
        jobsTriggerCommand.Add(jobsTriggerOutputOption);
        jobsTriggerCommand.SetAction((parseResult, cancellationToken) =>
            opsCommandHandler.TriggerJobAsync(
                new JobTriggerOptions(
                    parseResult.GetRequiredValue(jobNameOption),
                    OutputModeParser.Parse(parseResult.GetValue(jobsTriggerOutputOption))),
                cancellationToken));

        jobsCommand.Add(jobsListCommand);
        jobsCommand.Add(jobsHealthCommand);
        jobsCommand.Add(jobsTriggerCommand);

        var ledgerCommand = new Command("ledger", "Inspect and create ledgers.");
        var ledgerListCommand = new Command("list", "List ledgers.");
        var ledgerListOutputOption = CreateOutputOption(includeNdjson: false);
        ledgerListCommand.Add(ledgerListOutputOption);
        ledgerListCommand.SetAction((parseResult, cancellationToken) =>
            opsCommandHandler.ListLedgersAsync(
                OutputModeParser.Parse(parseResult.GetValue(ledgerListOutputOption)),
                cancellationToken));

        var ledgerCreateCommand = new Command("create", "Create a ledger.");
        var baseCurrencyOption = new Option<string>("--base-currency") { Description = "Ledger base currency.", Required = true };
        var ledgerCreateOutputOption = CreateOutputOption(includeNdjson: false);
        ledgerCreateCommand.Add(baseCurrencyOption);
        ledgerCreateCommand.Add(ledgerCreateOutputOption);
        ledgerCreateCommand.SetAction((parseResult, cancellationToken) =>
            opsCommandHandler.CreateLedgerAsync(
                new CreateLedgerOptions(
                    parseResult.GetRequiredValue(baseCurrencyOption),
                    OutputModeParser.Parse(parseResult.GetValue(ledgerCreateOutputOption))),
                cancellationToken));

        ledgerCommand.Add(ledgerListCommand);
        ledgerCommand.Add(ledgerCreateCommand);

        var invoicesCommand = new Command("invoices", "Inspect invoices.");
        var invoicesListCommand = new Command("list", "List invoices.");
        var invoiceStatusOption = new Option<string?>("--status") { Description = "Optional invoice status filter." };
        var invoicesOutputOption = CreateOutputOption(includeNdjson: false);
        invoicesListCommand.Add(invoiceStatusOption);
        invoicesListCommand.Add(invoicesOutputOption);
        invoicesListCommand.SetAction((parseResult, cancellationToken) =>
            opsCommandHandler.ListInvoicesAsync(
                new ListInvoicesOptions(
                    parseResult.GetValue(invoiceStatusOption),
                    OutputModeParser.Parse(parseResult.GetValue(invoicesOutputOption))),
                cancellationToken));

        invoicesCommand.Add(invoicesListCommand);

        var paymentsCommand = new Command("payments", "Create and manage payment intents.");
        var paymentsCreateCommand = new Command("create-intent", "Create a payment intent.");
        var amountOption = new Option<decimal>("--amount") { Description = "Payment amount.", Required = true };
        var currencyOption = new Option<string>("--currency") { Description = "Payment currency.", Required = true };
        var referenceOption = new Option<string>("--reference") { Description = "Payment reference.", Required = true };
        var orderIdOption = new Option<Guid>("--order-id") { Description = "Order identifier.", Required = true };
        var invoiceIdOption = new Option<Guid?>("--invoice-id") { Description = "Optional invoice identifier." };
        var paymentsCreateOutputOption = CreateOutputOption(includeNdjson: false);
        paymentsCreateCommand.Add(amountOption);
        paymentsCreateCommand.Add(currencyOption);
        paymentsCreateCommand.Add(referenceOption);
        paymentsCreateCommand.Add(orderIdOption);
        paymentsCreateCommand.Add(invoiceIdOption);
        paymentsCreateCommand.Add(paymentsCreateOutputOption);
        paymentsCreateCommand.SetAction((parseResult, cancellationToken) =>
            opsCommandHandler.CreatePaymentIntentAsync(
                new CreatePaymentIntentOptions(
                    parseResult.GetRequiredValue(amountOption),
                    parseResult.GetRequiredValue(currencyOption),
                    parseResult.GetRequiredValue(referenceOption),
                    parseResult.GetRequiredValue(orderIdOption),
                    parseResult.GetValue(invoiceIdOption),
                    OutputModeParser.Parse(parseResult.GetValue(paymentsCreateOutputOption))),
                cancellationToken));

        var paymentIdArgument = new Argument<Guid>("payment-intent-id");

        var paymentsGetCommand = new Command("get", "Get a payment intent.");
        var paymentsGetOutputOption = CreateOutputOption(includeNdjson: false);
        paymentsGetCommand.Add(paymentIdArgument);
        paymentsGetCommand.Add(paymentsGetOutputOption);
        paymentsGetCommand.SetAction((parseResult, cancellationToken) =>
            opsCommandHandler.GetPaymentIntentAsync(
                parseResult.GetRequiredValue(paymentIdArgument),
                OutputModeParser.Parse(parseResult.GetValue(paymentsGetOutputOption)),
                cancellationToken));

        var paymentsCaptureCommand = new Command("capture", "Capture a payment intent.");
        var paymentsCaptureOutputOption = CreateOutputOption(includeNdjson: false);
        paymentsCaptureCommand.Add(new Argument<Guid>("payment-intent-id"));
        paymentsCaptureCommand.Add(paymentsCaptureOutputOption);
        paymentsCaptureCommand.SetAction((parseResult, cancellationToken) =>
            opsCommandHandler.CapturePaymentAsync(
                parseResult.GetRequiredValue<Guid>("payment-intent-id"),
                OutputModeParser.Parse(parseResult.GetValue(paymentsCaptureOutputOption)),
                cancellationToken));

        var paymentsCancelCommand = new Command("cancel", "Cancel a payment intent.");
        var paymentsCancelOutputOption = CreateOutputOption(includeNdjson: false);
        paymentsCancelCommand.Add(new Argument<Guid>("payment-intent-id"));
        paymentsCancelCommand.Add(paymentsCancelOutputOption);
        paymentsCancelCommand.SetAction((parseResult, cancellationToken) =>
            opsCommandHandler.CancelPaymentAsync(
                parseResult.GetRequiredValue<Guid>("payment-intent-id"),
                OutputModeParser.Parse(parseResult.GetValue(paymentsCancelOutputOption)),
                cancellationToken));

        paymentsCommand.Add(paymentsCreateCommand);
        paymentsCommand.Add(paymentsGetCommand);
        paymentsCommand.Add(paymentsCaptureCommand);
        paymentsCommand.Add(paymentsCancelCommand);

        opsCommand.Add(workflowCommand);
        opsCommand.Add(jobsCommand);
        opsCommand.Add(ledgerCommand);
        opsCommand.Add(invoicesCommand);
        opsCommand.Add(paymentsCommand);
        return opsCommand;
    }

    private static Command BuildApprovalCommand(ApprovalCommandHandler approvalCommandHandler)
    {
        var approvalsCommand = new Command("approvals", "Review and resolve pending approval items.");

        var listCommand = new Command("list", "List pending financial life graph proposals.");
        var listOutputOption = CreateOutputOption(includeNdjson: false);
        listCommand.Add(listOutputOption);
        listCommand.SetAction((parseResult, cancellationToken) =>
            approvalCommandHandler.ListAsync(
                OutputModeParser.Parse(parseResult.GetValue(listOutputOption)),
                cancellationToken));

        var proposalIdArgument = new Argument<Guid>("proposal-id");

        var approveCommand = new Command("approve", "Approve a pending proposal.");
        var approveOutputOption = CreateOutputOption(includeNdjson: false);
        approveCommand.Add(proposalIdArgument);
        approveCommand.Add(approveOutputOption);
        approveCommand.SetAction((parseResult, cancellationToken) =>
            approvalCommandHandler.ApproveAsync(
                parseResult.GetRequiredValue(proposalIdArgument),
                OutputModeParser.Parse(parseResult.GetValue(approveOutputOption)),
                cancellationToken));

        var rejectCommand = new Command("reject", "Reject a pending proposal.");
        var rejectReasonOption = new Option<string?>("--reason") { Description = "Optional rejection reason." };
        var rejectOutputOption = CreateOutputOption(includeNdjson: false);
        rejectCommand.Add(new Argument<Guid>("proposal-id"));
        rejectCommand.Add(rejectReasonOption);
        rejectCommand.Add(rejectOutputOption);
        rejectCommand.SetAction((parseResult, cancellationToken) =>
            approvalCommandHandler.RejectAsync(
                parseResult.GetRequiredValue<Guid>("proposal-id"),
                parseResult.GetValue(rejectReasonOption),
                OutputModeParser.Parse(parseResult.GetValue(rejectOutputOption)),
                cancellationToken));

        approvalsCommand.Add(listCommand);
        approvalsCommand.Add(approveCommand);
        approvalsCommand.Add(rejectCommand);
        return approvalsCommand;
    }

    private static Command BuildShellCommand(ShellCommandHandler shellCommandHandler)
    {
        var shellCommand = new Command("shell", "Open the simple interactive AONIK shell.");
        shellCommand.SetAction((_, cancellationToken) => shellCommandHandler.RunAsync(cancellationToken));
        return shellCommand;
    }

    private static Option<string?> CreateOutputOption(bool includeNdjson)
    {
        var option = new Option<string?>("--output")
        {
            Description = includeNdjson
                ? "Output format: text, json, or ndjson."
                : "Output format: text or json."
        };

        option.AcceptOnlyFromAmong(includeNdjson ? ["text", "json", "ndjson"] : ["text", "json"]);
        return option;
    }
}
