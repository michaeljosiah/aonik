using Aonik.Cli.Commands;
using Aonik.Cli.Infrastructure;
using Aonik.Cli.Models;
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
        CareEntityCommandHandler careEntityCommandHandler,
        PaymentLogCommandHandler paymentLogCommandHandler,
        CommitmentCommandHandler commitmentCommandHandler,
        DocumentCommandHandler documentCommandHandler,
        CircleCommandHandler circleCommandHandler,
        CaptureCommandHandler captureCommandHandler)
    {
        _rootCommand = BuildRootCommand(
            authCommandHandler,
            agentCommandHandler,
            opsCommandHandler,
            approvalCommandHandler,
            careEntityCommandHandler,
            paymentLogCommandHandler,
            commitmentCommandHandler,
            documentCommandHandler,
            circleCommandHandler,
            captureCommandHandler);
    }

    public Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        // Disable System.CommandLine's default exception handler so domain failures
        // (AonikCliException) propagate to Program.Main, which prints a concise
        // "Error: <message>" instead of dumping an unhandled-exception stack trace.
        var invocationConfiguration = new InvocationConfiguration
        {
            EnableDefaultExceptionHandler = false
        };

        return _rootCommand.Parse(args).InvokeAsync(invocationConfiguration, cancellationToken);
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
        var careEntityCommandHandler = new CareEntityCommandHandler(apiClient, sessionStore, outputWriter);
        var paymentLogCommandHandler = new PaymentLogCommandHandler(apiClient, sessionStore, outputWriter);
        var commitmentCommandHandler = new CommitmentCommandHandler(apiClient, sessionStore, outputWriter);
        var documentCommandHandler = new DocumentCommandHandler(apiClient, sessionStore, outputWriter);
        var circleCommandHandler = new CircleCommandHandler(apiClient, sessionStore, outputWriter);
        var captureCommandHandler = new CaptureCommandHandler(apiClient, sessionStore, outputWriter);

        return new CliApplication(
            authCommandHandler,
            agentCommandHandler,
            opsCommandHandler,
            approvalCommandHandler,
            careEntityCommandHandler,
            paymentLogCommandHandler,
            commitmentCommandHandler,
            documentCommandHandler,
            circleCommandHandler,
            captureCommandHandler);
    }

    private static RootCommand BuildRootCommand(
        AuthCommandHandler authCommandHandler,
        AgentCommandHandler agentCommandHandler,
        OpsCommandHandler opsCommandHandler,
        ApprovalCommandHandler approvalCommandHandler,
        CareEntityCommandHandler careEntityCommandHandler,
        PaymentLogCommandHandler paymentLogCommandHandler,
        CommitmentCommandHandler commitmentCommandHandler,
        DocumentCommandHandler documentCommandHandler,
        CircleCommandHandler circleCommandHandler,
        CaptureCommandHandler captureCommandHandler)
    {
        var rootCommand = new RootCommand("AONIK CLI");
        rootCommand.Add(BuildAuthCommand(authCommandHandler));
        rootCommand.Add(BuildAgentCommand(agentCommandHandler));
        rootCommand.Add(BuildOpsCommand(opsCommandHandler));
        rootCommand.Add(BuildApprovalCommand(approvalCommandHandler));
        rootCommand.Add(BuildCareEntitiesCommand(careEntityCommandHandler));
        rootCommand.Add(BuildPaymentLogsCommand(paymentLogCommandHandler));
        rootCommand.Add(BuildCommitmentsCommand(commitmentCommandHandler));
        rootCommand.Add(BuildDocumentsCommand(documentCommandHandler));
        rootCommand.Add(BuildCircleCommand(circleCommandHandler));
        rootCommand.Add(BuildCaptureCommand(captureCommandHandler));
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

        var jobNameArgument = new Argument<string>("job-name");

        var jobsGetCommand = new Command("get", "Get scheduled job details.");
        var jobsGetOutputOption = CreateOutputOption(includeNdjson: false);
        jobsGetCommand.Add(jobNameArgument);
        jobsGetCommand.Add(jobsGetOutputOption);
        jobsGetCommand.SetAction((parseResult, cancellationToken) =>
            opsCommandHandler.GetScheduledJobAsync(
                parseResult.GetRequiredValue(jobNameArgument),
                OutputModeParser.Parse(parseResult.GetValue(jobsGetOutputOption)),
                cancellationToken));

        var jobsPauseCommand = new Command("pause", "Pause a scheduled job.");
        var jobsPauseOutputOption = CreateOutputOption(includeNdjson: false);
        jobsPauseCommand.Add(new Argument<string>("job-name"));
        jobsPauseCommand.Add(jobsPauseOutputOption);
        jobsPauseCommand.SetAction((parseResult, cancellationToken) =>
            opsCommandHandler.PauseScheduledJobAsync(
                parseResult.GetRequiredValue<string>("job-name"),
                OutputModeParser.Parse(parseResult.GetValue(jobsPauseOutputOption)),
                cancellationToken));

        var jobsResumeCommand = new Command("resume", "Resume a paused scheduled job.");
        var jobsResumeOutputOption = CreateOutputOption(includeNdjson: false);
        jobsResumeCommand.Add(new Argument<string>("job-name"));
        jobsResumeCommand.Add(jobsResumeOutputOption);
        jobsResumeCommand.SetAction((parseResult, cancellationToken) =>
            opsCommandHandler.ResumeScheduledJobAsync(
                parseResult.GetRequiredValue<string>("job-name"),
                OutputModeParser.Parse(parseResult.GetValue(jobsResumeOutputOption)),
                cancellationToken));

        var jobsRunsCommand = new Command("runs", "List scheduled job run history.");
        var jobsRunsPageOption = new Option<int>("--page") { Description = "Results page number." };
        var jobsRunsPageSizeOption = new Option<int>("--page-size") { Description = "Results per page." };
        var jobsRunsOutputOption = CreateOutputOption(includeNdjson: false);
        jobsRunsCommand.Add(new Argument<string>("job-name"));
        jobsRunsCommand.Add(jobsRunsPageOption);
        jobsRunsCommand.Add(jobsRunsPageSizeOption);
        jobsRunsCommand.Add(jobsRunsOutputOption);
        jobsRunsCommand.SetAction((parseResult, cancellationToken) =>
        {
            var parsedPageSize = parseResult.GetValue(jobsRunsPageSizeOption);
            return opsCommandHandler.ListScheduledJobRunsAsync(
                new ListJobRunsOptions(
                    JobName: parseResult.GetRequiredValue<string>("job-name"),
                    Page: Math.Max(parseResult.GetValue(jobsRunsPageOption), 1),
                    PageSize: parsedPageSize is > 0 and <= 100 ? parsedPageSize : 20,
                    OutputMode: OutputModeParser.Parse(parseResult.GetValue(jobsRunsOutputOption))),
                cancellationToken);
        });

        jobsCommand.Add(jobsListCommand);
        jobsCommand.Add(jobsHealthCommand);
        jobsCommand.Add(jobsTriggerCommand);
        jobsCommand.Add(jobsGetCommand);
        jobsCommand.Add(jobsPauseCommand);
        jobsCommand.Add(jobsResumeCommand);
        jobsCommand.Add(jobsRunsCommand);

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

        var invoiceIdArgument = new Argument<Guid>("invoice-id");

        var invoicesGetCommand = new Command("get", "Get an invoice with line items.");
        var invoicesGetOutputOption = CreateOutputOption(includeNdjson: false);
        invoicesGetCommand.Add(invoiceIdArgument);
        invoicesGetCommand.Add(invoicesGetOutputOption);
        invoicesGetCommand.SetAction((parseResult, cancellationToken) =>
            opsCommandHandler.GetInvoiceAsync(
                parseResult.GetRequiredValue(invoiceIdArgument),
                OutputModeParser.Parse(parseResult.GetValue(invoicesGetOutputOption)),
                cancellationToken));

        var invoicesCreateCommand = new Command("create", "Create a draft invoice.");
        var invoiceCustomerIdOption = new Option<Guid>("--customer-id") { Description = "Customer party identifier.", Required = true };
        var invoiceNumberOption = new Option<string>("--invoice-number") { Description = "Invoice number.", Required = true };
        var invoiceCurrencyOption = new Option<string>("--currency") { Description = "Invoice currency.", Required = true };
        var invoiceDueUtcOption = new Option<DateTime>("--due-utc") { Description = "Due date (UTC).", Required = true };
        var invoiceLinesFileOption = new Option<string?>("--lines-file") { Description = "Path to JSON file with line items: [{ description, quantity, unitPrice }]." };
        var invoicesCreateOutputOption = CreateOutputOption(includeNdjson: false);
        invoicesCreateCommand.Add(invoiceCustomerIdOption);
        invoicesCreateCommand.Add(invoiceNumberOption);
        invoicesCreateCommand.Add(invoiceCurrencyOption);
        invoicesCreateCommand.Add(invoiceDueUtcOption);
        invoicesCreateCommand.Add(invoiceLinesFileOption);
        invoicesCreateCommand.Add(invoicesCreateOutputOption);
        invoicesCreateCommand.SetAction((parseResult, cancellationToken) =>
            opsCommandHandler.CreateInvoiceAsync(
                new CreateInvoiceOptions(
                    parseResult.GetRequiredValue(invoiceCustomerIdOption),
                    parseResult.GetRequiredValue(invoiceNumberOption),
                    parseResult.GetRequiredValue(invoiceCurrencyOption),
                    parseResult.GetRequiredValue(invoiceDueUtcOption),
                    parseResult.GetValue(invoiceLinesFileOption),
                    OutputModeParser.Parse(parseResult.GetValue(invoicesCreateOutputOption))),
                cancellationToken));

        var invoicesIssueCommand = BuildInvoiceMutationCommand(
            "issue",
            "Issue a draft invoice.",
            opsCommandHandler.IssueInvoiceAsync);
        var invoicesCancelCommand = BuildInvoiceMutationCommand(
            "cancel",
            "Cancel a draft or issued invoice.",
            opsCommandHandler.CancelInvoiceAsync);
        var invoicesMarkPaidCommand = BuildInvoiceMutationCommand(
            "mark-paid",
            "Mark an issued invoice as paid.",
            opsCommandHandler.MarkInvoicePaidAsync);

        invoicesCommand.Add(invoicesListCommand);
        invoicesCommand.Add(invoicesGetCommand);
        invoicesCommand.Add(invoicesCreateCommand);
        invoicesCommand.Add(invoicesIssueCommand);
        invoicesCommand.Add(invoicesCancelCommand);
        invoicesCommand.Add(invoicesMarkPaidCommand);

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

        var ordersCommand = BuildOrdersCommand(opsCommandHandler);

        opsCommand.Add(workflowCommand);
        opsCommand.Add(jobsCommand);
        opsCommand.Add(ledgerCommand);
        opsCommand.Add(invoicesCommand);
        opsCommand.Add(ordersCommand);
        opsCommand.Add(paymentsCommand);
        return opsCommand;
    }

    private static Command BuildInvoiceMutationCommand(
        string name,
        string description,
        Func<InvoiceMutationOptions, CancellationToken, Task<int>> handler)
    {
        var command = new Command(name, description);
        var invoiceIdArgument = new Argument<Guid>("invoice-id");
        var confirmOption = new Option<bool>("--confirm") { Description = "Confirm the financially material operation." };
        var outputOption = CreateOutputOption(includeNdjson: false);

        command.Add(invoiceIdArgument);
        command.Add(confirmOption);
        command.Add(outputOption);
        command.SetAction((parseResult, cancellationToken) =>
            handler(
                new InvoiceMutationOptions(
                    parseResult.GetRequiredValue(invoiceIdArgument),
                    parseResult.GetValue(confirmOption),
                    OutputModeParser.Parse(parseResult.GetValue(outputOption))),
                cancellationToken));

        return command;
    }

    private static Command BuildOrdersCommand(OpsCommandHandler opsCommandHandler)
    {
        var ordersCommand = new Command("orders", "Inspect and manage orders.");

        var ordersListCommand = new Command("list", "List orders.");
        var ordersStatusOption = new Option<string?>("--status") { Description = "Optional status filter." };
        var ordersOrderTypeOption = new Option<string?>("--order-type") { Description = "Optional order type filter." };
        var ordersSearchOption = new Option<string?>("--search") { Description = "Free-text search across orders." };
        var ordersPayerPartyIdOption = new Option<Guid?>("--payer-party-id") { Description = "Filter by payer party id." };
        var ordersPageOption = new Option<int>("--page") { Description = "Results page number." };
        var ordersPageSizeOption = new Option<int>("--page-size") { Description = "Results per page." };
        var ordersListOutputOption = CreateOutputOption(includeNdjson: false);
        ordersListCommand.Add(ordersStatusOption);
        ordersListCommand.Add(ordersOrderTypeOption);
        ordersListCommand.Add(ordersSearchOption);
        ordersListCommand.Add(ordersPayerPartyIdOption);
        ordersListCommand.Add(ordersPageOption);
        ordersListCommand.Add(ordersPageSizeOption);
        ordersListCommand.Add(ordersListOutputOption);
        ordersListCommand.SetAction((parseResult, cancellationToken) =>
        {
            var parsedPageSize = parseResult.GetValue(ordersPageSizeOption);
            return opsCommandHandler.ListOrdersAsync(
                new ListOrdersOptions(
                    Page: Math.Max(parseResult.GetValue(ordersPageOption), 1),
                    PageSize: parsedPageSize is > 0 and <= 100 ? parsedPageSize : 20,
                    Status: parseResult.GetValue(ordersStatusOption),
                    OrderType: parseResult.GetValue(ordersOrderTypeOption),
                    Search: parseResult.GetValue(ordersSearchOption),
                    PayerPartyId: parseResult.GetValue(ordersPayerPartyIdOption),
                    OutputMode: OutputModeParser.Parse(parseResult.GetValue(ordersListOutputOption))),
                cancellationToken);
        });

        var orderIdArgument = new Argument<Guid>("order-id");

        var ordersGetCommand = new Command("get", "Get an order with line items.");
        var ordersGetOutputOption = CreateOutputOption(includeNdjson: false);
        ordersGetCommand.Add(orderIdArgument);
        ordersGetCommand.Add(ordersGetOutputOption);
        ordersGetCommand.SetAction((parseResult, cancellationToken) =>
            opsCommandHandler.GetOrderAsync(
                parseResult.GetRequiredValue(orderIdArgument),
                OutputModeParser.Parse(parseResult.GetValue(ordersGetOutputOption)),
                cancellationToken));

        var ordersCreateBillPaymentCommand = new Command("create-bill-payment", "Create a bill payment order.");
        var orderPayerPartyIdOption = new Option<Guid>("--payer-party-id") { Description = "Payer party id.", Required = true };
        var orderOriginCountryOption = new Option<string>("--origin-country") { Description = "Origin country code.", Required = true };
        var orderOriginCurrencyOption = new Option<string>("--origin-currency") { Description = "Origin currency.", Required = true };
        var orderPurposeCodeOption = new Option<string?>("--purpose-code") { Description = "Optional purpose code." };
        var orderNotesOption = new Option<string?>("--notes") { Description = "Optional order notes." };
        var orderItemsFileOption = new Option<string?>("--items-file") { Description = "Path to JSON file with bill payment items." };
        var ordersCreateOutputOption = CreateOutputOption(includeNdjson: false);
        ordersCreateBillPaymentCommand.Add(orderPayerPartyIdOption);
        ordersCreateBillPaymentCommand.Add(orderOriginCountryOption);
        ordersCreateBillPaymentCommand.Add(orderOriginCurrencyOption);
        ordersCreateBillPaymentCommand.Add(orderPurposeCodeOption);
        ordersCreateBillPaymentCommand.Add(orderNotesOption);
        ordersCreateBillPaymentCommand.Add(orderItemsFileOption);
        ordersCreateBillPaymentCommand.Add(ordersCreateOutputOption);
        ordersCreateBillPaymentCommand.SetAction((parseResult, cancellationToken) =>
            opsCommandHandler.CreateBillPaymentOrderAsync(
                new CreateBillPaymentOrderOptions(
                    parseResult.GetRequiredValue(orderPayerPartyIdOption),
                    parseResult.GetRequiredValue(orderOriginCountryOption),
                    parseResult.GetRequiredValue(orderOriginCurrencyOption),
                    parseResult.GetValue(orderPurposeCodeOption),
                    parseResult.GetValue(orderNotesOption),
                    parseResult.GetValue(orderItemsFileOption),
                    OutputModeParser.Parse(parseResult.GetValue(ordersCreateOutputOption))),
                cancellationToken));

        var ordersSubmitCommand = new Command("submit", "Submit an order for processing.");
        var orderSubmitConfirmOption = new Option<bool>("--confirm") { Description = "Confirm the financially material operation." };
        var ordersSubmitOutputOption = CreateOutputOption(includeNdjson: false);
        ordersSubmitCommand.Add(new Argument<Guid>("order-id"));
        ordersSubmitCommand.Add(orderSubmitConfirmOption);
        ordersSubmitCommand.Add(ordersSubmitOutputOption);
        ordersSubmitCommand.SetAction((parseResult, cancellationToken) =>
            opsCommandHandler.SubmitOrderAsync(
                new SubmitOrderOptions(
                    parseResult.GetRequiredValue<Guid>("order-id"),
                    parseResult.GetValue(orderSubmitConfirmOption),
                    OutputModeParser.Parse(parseResult.GetValue(ordersSubmitOutputOption))),
                cancellationToken));

        var ordersCancelCommand = new Command("cancel", "Cancel an order.");
        var orderCancelReasonOption = new Option<string?>("--reason") { Description = "Optional cancellation reason." };
        var orderCancelConfirmOption = new Option<bool>("--confirm") { Description = "Confirm the financially material operation." };
        var ordersCancelOutputOption = CreateOutputOption(includeNdjson: false);
        ordersCancelCommand.Add(new Argument<Guid>("order-id"));
        ordersCancelCommand.Add(orderCancelReasonOption);
        ordersCancelCommand.Add(orderCancelConfirmOption);
        ordersCancelCommand.Add(ordersCancelOutputOption);
        ordersCancelCommand.SetAction((parseResult, cancellationToken) =>
            opsCommandHandler.CancelOrderAsync(
                new CancelOrderOptions(
                    parseResult.GetRequiredValue<Guid>("order-id"),
                    parseResult.GetValue(orderCancelReasonOption),
                    parseResult.GetValue(orderCancelConfirmOption),
                    OutputModeParser.Parse(parseResult.GetValue(ordersCancelOutputOption))),
                cancellationToken));

        ordersCommand.Add(ordersListCommand);
        ordersCommand.Add(ordersGetCommand);
        ordersCommand.Add(ordersCreateBillPaymentCommand);
        ordersCommand.Add(ordersSubmitCommand);
        ordersCommand.Add(ordersCancelCommand);
        return ordersCommand;
    }

    private static Command BuildCircleCommand(CircleCommandHandler handler)
    {
        var command = new Command("circle", "Entity-scoped sharing + the Support Statement (Spec 048).");

        // grant
        var grantCommand = new Command("grant", "Share a scoped slice with a member.");
        var memberOption = new Option<Guid>("--member-user-id") { Description = "Member user id.", Required = true };
        var scopeOption = new Option<string>("--scope") { Description = "all | entities | docsOnly.", Required = true };
        var entityIdOption = new Option<Guid[]>("--entity-id") { Description = "CareEntity id (repeatable; for entities/docsOnly).", AllowMultipleArgumentsPerToken = true };
        var noAmountsOption = new Option<bool>("--no-amounts") { Description = "Hide amounts (docsOnly)." };
        var grantOutputOption = CreateOutputOption(includeNdjson: false);
        grantCommand.Add(memberOption);
        grantCommand.Add(scopeOption);
        grantCommand.Add(entityIdOption);
        grantCommand.Add(noAmountsOption);
        grantCommand.Add(grantOutputOption);
        grantCommand.SetAction((parseResult, cancellationToken) =>
            handler.GrantAsync(
                new CreateCircleGrantOptions(
                    parseResult.GetRequiredValue(memberOption),
                    parseResult.GetRequiredValue(scopeOption),
                    parseResult.GetValue(entityIdOption) ?? Array.Empty<Guid>(),
                    parseResult.GetValue(noAmountsOption),
                    OutputModeParser.Parse(parseResult.GetValue(grantOutputOption))),
                cancellationToken));

        // grants (Shared with)
        var grantsCommand = new Command("grants", "List grants you've shared (Shared with).");
        var grantsOutputOption = CreateOutputOption(includeNdjson: false);
        grantsCommand.Add(grantsOutputOption);
        grantsCommand.SetAction((parseResult, cancellationToken) =>
            handler.ListGrantsAsync(OutputModeParser.Parse(parseResult.GetValue(grantsOutputOption)), cancellationToken));

        // shared (Can see)
        var sharedCommand = new Command("shared", "List grants shared with you (Can see).");
        var sharedOutputOption = CreateOutputOption(includeNdjson: false);
        sharedCommand.Add(sharedOutputOption);
        sharedCommand.SetAction((parseResult, cancellationToken) =>
            handler.ListSharedAsync(OutputModeParser.Parse(parseResult.GetValue(sharedOutputOption)), cancellationToken));

        // revoke
        var revokeCommand = new Command("revoke", "Revoke a grant.");
        var revokeIdArgument = new Argument<Guid>("grant-id");
        var revokeOutputOption = CreateOutputOption(includeNdjson: false);
        revokeCommand.Add(revokeIdArgument);
        revokeCommand.Add(revokeOutputOption);
        revokeCommand.SetAction((parseResult, cancellationToken) =>
            handler.RevokeAsync(
                parseResult.GetRequiredValue(revokeIdArgument),
                OutputModeParser.Parse(parseResult.GetValue(revokeOutputOption)),
                cancellationToken));

        // invite
        var inviteCommand = new Command("invite", "Create an invite link.");
        var inviteScopeOption = new Option<string>("--scope") { Description = "all | entities | docsOnly.", Required = true };
        var inviteEntityIdOption = new Option<Guid[]>("--entity-id") { Description = "CareEntity id (repeatable).", AllowMultipleArgumentsPerToken = true };
        var inviteNoAmountsOption = new Option<bool>("--no-amounts") { Description = "Hide amounts (docsOnly)." };
        var inviteChannelOption = new Option<string?>("--channel") { Description = "email | phone | link." };
        var inviteOutputOption = CreateOutputOption(includeNdjson: false);
        inviteCommand.Add(inviteScopeOption);
        inviteCommand.Add(inviteEntityIdOption);
        inviteCommand.Add(inviteNoAmountsOption);
        inviteCommand.Add(inviteChannelOption);
        inviteCommand.Add(inviteOutputOption);
        inviteCommand.SetAction((parseResult, cancellationToken) =>
            handler.InviteAsync(
                new CreateCircleInviteOptions(
                    parseResult.GetRequiredValue(inviteScopeOption),
                    parseResult.GetValue(inviteEntityIdOption) ?? Array.Empty<Guid>(),
                    parseResult.GetValue(inviteNoAmountsOption),
                    parseResult.GetValue(inviteChannelOption),
                    OutputModeParser.Parse(parseResult.GetValue(inviteOutputOption))),
                cancellationToken));

        // accept
        var acceptCommand = new Command("accept", "Accept an invite token.");
        var acceptTokenOption = new Option<string>("--token") { Description = "Invite token.", Required = true };
        var acceptOutputOption = CreateOutputOption(includeNdjson: false);
        acceptCommand.Add(acceptTokenOption);
        acceptCommand.Add(acceptOutputOption);
        acceptCommand.SetAction((parseResult, cancellationToken) =>
            handler.AcceptAsync(
                parseResult.GetRequiredValue(acceptTokenOption),
                OutputModeParser.Parse(parseResult.GetValue(acceptOutputOption)),
                cancellationToken));

        // statement
        var statementCommand = new Command("statement", "Compose a Support Statement for your entity.");
        var statementIdArgument = new Argument<Guid>("care-entity-id");
        var statementFromOption = new Option<DateTime?>("--from") { Description = "From date." };
        var statementToOption = new Option<DateTime?>("--to") { Description = "To date." };
        var statementPreparedForOption = new Option<string?>("--prepared-for") { Description = "Recipient label." };
        var statementOutputOption = CreateOutputOption(includeNdjson: false);
        statementCommand.Add(statementIdArgument);
        statementCommand.Add(statementFromOption);
        statementCommand.Add(statementToOption);
        statementCommand.Add(statementPreparedForOption);
        statementCommand.Add(statementOutputOption);
        statementCommand.SetAction((parseResult, cancellationToken) =>
            handler.StatementAsync(
                parseResult.GetRequiredValue(statementIdArgument),
                parseResult.GetValue(statementFromOption),
                parseResult.GetValue(statementToOption),
                parseResult.GetValue(statementPreparedForOption),
                OutputModeParser.Parse(parseResult.GetValue(statementOutputOption)),
                cancellationToken));

        command.Add(grantCommand);
        command.Add(grantsCommand);
        command.Add(sharedCommand);
        command.Add(revokeCommand);
        command.Add(inviteCommand);
        command.Add(acceptCommand);
        command.Add(statementCommand);
        return command;
    }

    private static Command BuildCaptureCommand(CaptureCommandHandler handler)
    {
        var command = new Command("capture", "AI capture-parse: turn text or an image into a draft proposal (Spec 047).");

        var parseCommand = new Command("parse", "Parse captured text/image into a structured draft (never persisted).");
        var inputTypeOption = new Option<string?>("--input-type") { Description = "text | audioTranscript (image is inferred from --image)." };
        var textOption = new Option<string?>("--text") { Description = "Raw text or transcript to parse." };
        var imageOption = new Option<string?>("--image") { Description = "Path to an image file (base64-encoded, sent as inputType=image)." };
        var hintsOption = new Option<string?>("--hints-json") { Description = "Optional hints JSON: {\"entities\":[{\"id\":..,\"name\":..}],\"openCommitments\":[..]}." };
        var parseOutputOption = CreateOutputOption(includeNdjson: false);
        parseCommand.Add(inputTypeOption);
        parseCommand.Add(textOption);
        parseCommand.Add(imageOption);
        parseCommand.Add(hintsOption);
        parseCommand.Add(parseOutputOption);
        parseCommand.SetAction((parseResult, cancellationToken) =>
            handler.ParseAsync(
                new CaptureParseOptions(
                    parseResult.GetValue(inputTypeOption) ?? "text",
                    parseResult.GetValue(textOption),
                    parseResult.GetValue(imageOption),
                    parseResult.GetValue(hintsOption),
                    OutputModeParser.Parse(parseResult.GetValue(parseOutputOption))),
                cancellationToken));

        command.Add(parseCommand);
        return command;
    }

    private static Command BuildDocumentsCommand(DocumentCommandHandler handler)
    {
        var command = new Command("documents", "Vault: link documents to entities and filter (Spec 046).");

        // list
        var listCommand = new Command("list", "List documents (filter by entity / type / year).");
        var listCareEntityOption = new Option<Guid?>("--care-entity-id") { Description = "Filter by linked CareEntity." };
        var listTypeOption = new Option<string?>("--type") { Description = "Filter by document type." };
        var listYearOption = new Option<int?>("--year") { Description = "Filter by year." };
        var listPageOption = new Option<int>("--page") { Description = "Results page." };
        var listPageSizeOption = new Option<int>("--page-size") { Description = "Results per page." };
        var listOutputOption = CreateOutputOption(includeNdjson: false);
        listCommand.Add(listCareEntityOption);
        listCommand.Add(listTypeOption);
        listCommand.Add(listYearOption);
        listCommand.Add(listPageOption);
        listCommand.Add(listPageSizeOption);
        listCommand.Add(listOutputOption);
        listCommand.SetAction((parseResult, cancellationToken) =>
        {
            var ps = parseResult.GetValue(listPageSizeOption);
            return handler.ListAsync(
                new ListDocumentsOptions(
                    parseResult.GetValue(listCareEntityOption),
                    parseResult.GetValue(listTypeOption),
                    parseResult.GetValue(listYearOption),
                    Math.Max(parseResult.GetValue(listPageOption), 1),
                    ps is > 0 and <= 100 ? ps : 20,
                    OutputModeParser.Parse(parseResult.GetValue(listOutputOption))),
                cancellationToken);
        });

        // links (list)
        var linksCommand = new Command("links", "List a document's links.");
        var linksIdArgument = new Argument<Guid>("id");
        var linksOutputOption = CreateOutputOption(includeNdjson: false);
        linksCommand.Add(linksIdArgument);
        linksCommand.Add(linksOutputOption);
        linksCommand.SetAction((parseResult, cancellationToken) =>
            handler.ListLinksAsync(
                parseResult.GetRequiredValue(linksIdArgument),
                OutputModeParser.Parse(parseResult.GetValue(linksOutputOption)),
                cancellationToken));

        // link (add)
        var linkCommand = new Command("link", "Link a document to a target.");
        var linkIdArgument = new Argument<Guid>("id");
        var linkTargetTypeOption = new Option<string>("--target-type") { Description = "careEntity | paymentLog | commitment.", Required = true };
        var linkTargetIdOption = new Option<Guid>("--target-id") { Description = "Target id.", Required = true };
        var linkOutputOption = CreateOutputOption(includeNdjson: false);
        linkCommand.Add(linkIdArgument);
        linkCommand.Add(linkTargetTypeOption);
        linkCommand.Add(linkTargetIdOption);
        linkCommand.Add(linkOutputOption);
        linkCommand.SetAction((parseResult, cancellationToken) =>
            handler.LinkAsync(
                parseResult.GetRequiredValue(linkIdArgument),
                parseResult.GetRequiredValue(linkTargetTypeOption),
                parseResult.GetRequiredValue(linkTargetIdOption),
                OutputModeParser.Parse(parseResult.GetValue(linkOutputOption)),
                cancellationToken));

        // unlink (remove)
        var unlinkCommand = new Command("unlink", "Remove a document link.");
        var unlinkIdArgument = new Argument<Guid>("id");
        var unlinkLinkIdArgument = new Argument<Guid>("link-id");
        var unlinkOutputOption = CreateOutputOption(includeNdjson: false);
        unlinkCommand.Add(unlinkIdArgument);
        unlinkCommand.Add(unlinkLinkIdArgument);
        unlinkCommand.Add(unlinkOutputOption);
        unlinkCommand.SetAction((parseResult, cancellationToken) =>
            handler.UnlinkAsync(
                parseResult.GetRequiredValue(unlinkIdArgument),
                parseResult.GetRequiredValue(unlinkLinkIdArgument),
                OutputModeParser.Parse(parseResult.GetValue(unlinkOutputOption)),
                cancellationToken));

        command.Add(listCommand);
        command.Add(linksCommand);
        command.Add(linkCommand);
        command.Add(unlinkCommand);
        return command;
    }

    private static Command BuildCommitmentsCommand(CommitmentCommandHandler handler)
    {
        var command = new Command("commitments", "Author and run the lifecycle of Support commitments (Spec 044).");

        // create
        var createCommand = new Command("create", "Author a Support commitment.");
        var careEntityIdOption = new Option<Guid>("--care-entity-id") { Description = "CareEntity this commitment is for.", Required = true };
        var nameOption = new Option<string>("--name") { Description = "Display name.", Required = true };
        var amountOption = new Option<decimal?>("--amount") { Description = "Expected amount." };
        var currencyOption = new Option<string>("--currency") { Description = "ISO-4217 currency.", Required = true };
        var rhythmUnitOption = new Option<string>("--rhythm-unit") { Description = "Weekly | Monthly | Quarterly | Termly | Yearly | OneOff." };
        var rhythmIntervalOption = new Option<int>("--rhythm-interval") { Description = "Every N units (default 1)." };
        var anchorDayOption = new Option<int?>("--anchor-day") { Description = "Day-of-month for monthly rhythms." };
        var firstDueOption = new Option<DateTime>("--first-due") { Description = "First due date.", Required = true };
        var reminderDaysOption = new Option<int?>("--reminder-days-before") { Description = "Reminder lead days (default 3)." };
        var createNotesOption = new Option<string?>("--notes") { Description = "Optional notes." };
        var createOutputOption = CreateOutputOption(includeNdjson: false);
        createCommand.Add(careEntityIdOption);
        createCommand.Add(nameOption);
        createCommand.Add(amountOption);
        createCommand.Add(currencyOption);
        createCommand.Add(rhythmUnitOption);
        createCommand.Add(rhythmIntervalOption);
        createCommand.Add(anchorDayOption);
        createCommand.Add(firstDueOption);
        createCommand.Add(reminderDaysOption);
        createCommand.Add(createNotesOption);
        createCommand.Add(createOutputOption);
        createCommand.SetAction((parseResult, cancellationToken) =>
        {
            var interval = parseResult.GetValue(rhythmIntervalOption);
            return handler.CreateAsync(
                new CreateSupportCommitmentOptions(
                    parseResult.GetRequiredValue(careEntityIdOption),
                    parseResult.GetRequiredValue(nameOption),
                    parseResult.GetValue(amountOption),
                    parseResult.GetRequiredValue(currencyOption),
                    parseResult.GetValue(rhythmUnitOption) ?? "Monthly",
                    interval <= 0 ? 1 : interval,
                    parseResult.GetValue(anchorDayOption),
                    parseResult.GetRequiredValue(firstDueOption),
                    parseResult.GetValue(reminderDaysOption),
                    parseResult.GetValue(createNotesOption),
                    OutputModeParser.Parse(parseResult.GetValue(createOutputOption))),
                cancellationToken);
        });

        // done
        var doneCommand = new Command("done", "Mark the current cycle done.");
        var doneIdArgument = new Argument<Guid>("id");
        var doneAmountOption = new Option<decimal>("--amount") { Description = "Amount paid.", Required = true };
        var doneCurrencyOption = new Option<string>("--currency") { Description = "ISO-4217 currency.", Required = true };
        var doneApproxGbpOption = new Option<decimal?>("--approx-gbp") { Description = "Optional approx GBP label." };
        var doneDateOption = new Option<DateTime?>("--date") { Description = "Date of the act." };
        var doneChannelOption = new Option<string>("--channel") { Description = "bank | wise | cash | other." };
        var doneNoteOption = new Option<string?>("--note") { Description = "Optional note." };
        var doneKeyOption = new Option<Guid?>("--idempotency-key") { Description = "Optional client idempotency key." };
        var doneOutputOption = CreateOutputOption(includeNdjson: false);
        doneCommand.Add(doneIdArgument);
        doneCommand.Add(doneAmountOption);
        doneCommand.Add(doneCurrencyOption);
        doneCommand.Add(doneApproxGbpOption);
        doneCommand.Add(doneDateOption);
        doneCommand.Add(doneChannelOption);
        doneCommand.Add(doneNoteOption);
        doneCommand.Add(doneKeyOption);
        doneCommand.Add(doneOutputOption);
        doneCommand.SetAction((parseResult, cancellationToken) =>
            handler.MarkDoneAsync(
                new MarkCommitmentDoneOptions(
                    parseResult.GetRequiredValue(doneIdArgument),
                    parseResult.GetRequiredValue(doneAmountOption),
                    parseResult.GetRequiredValue(doneCurrencyOption),
                    parseResult.GetValue(doneApproxGbpOption),
                    parseResult.GetValue(doneDateOption),
                    parseResult.GetValue(doneChannelOption) ?? "bank",
                    parseResult.GetValue(doneNoteOption),
                    parseResult.GetValue(doneKeyOption),
                    OutputModeParser.Parse(parseResult.GetValue(doneOutputOption))),
                cancellationToken));

        // skip
        var skipCommand = new Command("skip", "Skip the current cycle.");
        var skipIdArgument = new Argument<Guid>("id");
        var skipReasonOption = new Option<string?>("--reason") { Description = "Optional reason." };
        var skipOutputOption = CreateOutputOption(includeNdjson: false);
        skipCommand.Add(skipIdArgument);
        skipCommand.Add(skipReasonOption);
        skipCommand.Add(skipOutputOption);
        skipCommand.SetAction((parseResult, cancellationToken) =>
            handler.SkipAsync(
                parseResult.GetRequiredValue(skipIdArgument),
                parseResult.GetValue(skipReasonOption),
                OutputModeParser.Parse(parseResult.GetValue(skipOutputOption)),
                cancellationToken));

        // snooze
        var snoozeCommand = new Command("snooze", "Snooze the current cycle's reminder.");
        var snoozeIdArgument = new Argument<Guid>("id");
        var snoozeUntilOption = new Option<DateTime>("--until") { Description = "New reminder date.", Required = true };
        var snoozeOutputOption = CreateOutputOption(includeNdjson: false);
        snoozeCommand.Add(snoozeIdArgument);
        snoozeCommand.Add(snoozeUntilOption);
        snoozeCommand.Add(snoozeOutputOption);
        snoozeCommand.SetAction((parseResult, cancellationToken) =>
            handler.SnoozeAsync(
                parseResult.GetRequiredValue(snoozeIdArgument),
                parseResult.GetRequiredValue(snoozeUntilOption),
                OutputModeParser.Parse(parseResult.GetValue(snoozeOutputOption)),
                cancellationToken));

        // pause
        var pauseCommand = new Command("pause", "Pause a commitment.");
        var pauseIdArgument = new Argument<Guid>("id");
        var pauseOutputOption = CreateOutputOption(includeNdjson: false);
        pauseCommand.Add(pauseIdArgument);
        pauseCommand.Add(pauseOutputOption);
        pauseCommand.SetAction((parseResult, cancellationToken) =>
            handler.PauseAsync(
                parseResult.GetRequiredValue(pauseIdArgument),
                OutputModeParser.Parse(parseResult.GetValue(pauseOutputOption)),
                cancellationToken));

        // resume
        var resumeCommand = new Command("resume", "Resume a commitment.");
        var resumeIdArgument = new Argument<Guid>("id");
        var resumeOutputOption = CreateOutputOption(includeNdjson: false);
        resumeCommand.Add(resumeIdArgument);
        resumeCommand.Add(resumeOutputOption);
        resumeCommand.SetAction((parseResult, cancellationToken) =>
            handler.ResumeAsync(
                parseResult.GetRequiredValue(resumeIdArgument),
                OutputModeParser.Parse(parseResult.GetValue(resumeOutputOption)),
                cancellationToken));

        // cycles
        var cyclesCommand = new Command("cycles", "List a commitment's cycle history.");
        var cyclesIdArgument = new Argument<Guid>("id");
        var cyclesPageOption = new Option<int>("--page") { Description = "Results page." };
        var cyclesPageSizeOption = new Option<int>("--page-size") { Description = "Results per page." };
        var cyclesOutputOption = CreateOutputOption(includeNdjson: false);
        cyclesCommand.Add(cyclesIdArgument);
        cyclesCommand.Add(cyclesPageOption);
        cyclesCommand.Add(cyclesPageSizeOption);
        cyclesCommand.Add(cyclesOutputOption);
        cyclesCommand.SetAction((parseResult, cancellationToken) =>
        {
            var ps = parseResult.GetValue(cyclesPageSizeOption);
            return handler.CyclesAsync(
                parseResult.GetRequiredValue(cyclesIdArgument),
                Math.Max(parseResult.GetValue(cyclesPageOption), 1),
                ps is > 0 and <= 100 ? ps : 20,
                OutputModeParser.Parse(parseResult.GetValue(cyclesOutputOption)),
                cancellationToken);
        });

        command.Add(createCommand);
        command.Add(doneCommand);
        command.Add(skipCommand);
        command.Add(snoozeCommand);
        command.Add(pauseCommand);
        command.Add(resumeCommand);
        command.Add(cyclesCommand);
        return command;
    }

    private static Command BuildPaymentLogsCommand(PaymentLogCommandHandler handler)
    {
        var command = new Command("payment-logs", "Record and manage acts of support (Spec 045).");

        // create
        var createCommand = new Command("create", "Record a payment log.");
        var careEntityIdOption = new Option<Guid>("--care-entity-id") { Description = "CareEntity this act is for.", Required = true };
        var commitmentIdOption = new Option<Guid?>("--commitment-id") { Description = "Optional commitment it honours." };
        var amountOption = new Option<decimal>("--amount") { Description = "Amount paid.", Required = true };
        var currencyOption = new Option<string>("--currency") { Description = "ISO-4217 currency.", Required = true };
        var approxGbpOption = new Option<decimal?>("--approx-gbp") { Description = "Optional approx GBP label (display-only)." };
        var dateOption = new Option<DateTime?>("--date") { Description = "Date of the act (defaults to today)." };
        var channelOption = new Option<string>("--channel") { Description = "bank | wise | cash | other." };
        var originOption = new Option<string>("--origin") { Description = "manual | captureImage | captureText | captureVoice | markDone | plaidDetected." };
        var noteOption = new Option<string?>("--note") { Description = "Optional note." };
        var idempotencyKeyOption = new Option<Guid?>("--idempotency-key") { Description = "Optional client idempotency key." };
        var createOutputOption = CreateOutputOption(includeNdjson: false);
        createCommand.Add(careEntityIdOption);
        createCommand.Add(commitmentIdOption);
        createCommand.Add(amountOption);
        createCommand.Add(currencyOption);
        createCommand.Add(approxGbpOption);
        createCommand.Add(dateOption);
        createCommand.Add(channelOption);
        createCommand.Add(originOption);
        createCommand.Add(noteOption);
        createCommand.Add(idempotencyKeyOption);
        createCommand.Add(createOutputOption);
        createCommand.SetAction((parseResult, cancellationToken) =>
            handler.CreateAsync(
                new CreatePaymentLogOptions(
                    parseResult.GetRequiredValue(careEntityIdOption),
                    parseResult.GetValue(commitmentIdOption),
                    parseResult.GetRequiredValue(amountOption),
                    parseResult.GetRequiredValue(currencyOption),
                    parseResult.GetValue(approxGbpOption),
                    parseResult.GetValue(dateOption),
                    parseResult.GetValue(channelOption) ?? "bank",
                    parseResult.GetValue(originOption) ?? "manual",
                    parseResult.GetValue(noteOption),
                    parseResult.GetValue(idempotencyKeyOption),
                    OutputModeParser.Parse(parseResult.GetValue(createOutputOption))),
                cancellationToken));

        // list
        var listCommand = new Command("list", "List payment logs.");
        var listCareEntityOption = new Option<Guid?>("--care-entity-id") { Description = "Filter by CareEntity." };
        var listCommitmentOption = new Option<Guid?>("--commitment-id") { Description = "Filter by commitment." };
        var listYearOption = new Option<int?>("--year") { Description = "Filter by year." };
        var listPageOption = new Option<int>("--page") { Description = "Results page." };
        var listPageSizeOption = new Option<int>("--page-size") { Description = "Results per page." };
        var listOutputOption = CreateOutputOption(includeNdjson: false);
        listCommand.Add(listCareEntityOption);
        listCommand.Add(listCommitmentOption);
        listCommand.Add(listYearOption);
        listCommand.Add(listPageOption);
        listCommand.Add(listPageSizeOption);
        listCommand.Add(listOutputOption);
        listCommand.SetAction((parseResult, cancellationToken) =>
        {
            var parsedPageSize = parseResult.GetValue(listPageSizeOption);
            return handler.ListAsync(
                new ListPaymentLogsOptions(
                    parseResult.GetValue(listCareEntityOption),
                    parseResult.GetValue(listCommitmentOption),
                    parseResult.GetValue(listYearOption),
                    Math.Max(parseResult.GetValue(listPageOption), 1),
                    parsedPageSize is > 0 and <= 100 ? parsedPageSize : 20,
                    OutputModeParser.Parse(parseResult.GetValue(listOutputOption))),
                cancellationToken);
        });

        // get
        var getCommand = new Command("get", "Get a payment log by id.");
        var getIdArgument = new Argument<Guid>("id");
        var getOutputOption = CreateOutputOption(includeNdjson: false);
        getCommand.Add(getIdArgument);
        getCommand.Add(getOutputOption);
        getCommand.SetAction((parseResult, cancellationToken) =>
            handler.GetAsync(
                parseResult.GetRequiredValue(getIdArgument),
                OutputModeParser.Parse(parseResult.GetValue(getOutputOption)),
                cancellationToken));

        // update
        var updateCommand = new Command("update", "Update a payment log.");
        var updateIdArgument = new Argument<Guid>("id");
        var updateAmountOption = new Option<decimal>("--amount") { Description = "Amount.", Required = true };
        var updateCurrencyOption = new Option<string>("--currency") { Description = "ISO-4217 currency.", Required = true };
        var updateApproxGbpOption = new Option<decimal?>("--approx-gbp") { Description = "Optional approx GBP label." };
        var updateDateOption = new Option<DateTime?>("--date") { Description = "Date." };
        var updateChannelOption = new Option<string>("--channel") { Description = "bank | wise | cash | other." };
        var updateNoteOption = new Option<string?>("--note") { Description = "Optional note." };
        var updateOutputOption = CreateOutputOption(includeNdjson: false);
        updateCommand.Add(updateIdArgument);
        updateCommand.Add(updateAmountOption);
        updateCommand.Add(updateCurrencyOption);
        updateCommand.Add(updateApproxGbpOption);
        updateCommand.Add(updateDateOption);
        updateCommand.Add(updateChannelOption);
        updateCommand.Add(updateNoteOption);
        updateCommand.Add(updateOutputOption);
        updateCommand.SetAction((parseResult, cancellationToken) =>
            handler.UpdateAsync(
                new UpdatePaymentLogOptions(
                    parseResult.GetRequiredValue(updateIdArgument),
                    parseResult.GetRequiredValue(updateAmountOption),
                    parseResult.GetRequiredValue(updateCurrencyOption),
                    parseResult.GetValue(updateApproxGbpOption),
                    parseResult.GetValue(updateDateOption),
                    parseResult.GetValue(updateChannelOption) ?? "bank",
                    parseResult.GetValue(updateNoteOption),
                    OutputModeParser.Parse(parseResult.GetValue(updateOutputOption))),
                cancellationToken));

        // delete
        var deleteCommand = new Command("delete", "Soft-delete a payment log (30-day restore window).");
        var deleteIdArgument = new Argument<Guid>("id");
        var deleteOutputOption = CreateOutputOption(includeNdjson: false);
        deleteCommand.Add(deleteIdArgument);
        deleteCommand.Add(deleteOutputOption);
        deleteCommand.SetAction((parseResult, cancellationToken) =>
            handler.DeleteAsync(
                parseResult.GetRequiredValue(deleteIdArgument),
                OutputModeParser.Parse(parseResult.GetValue(deleteOutputOption)),
                cancellationToken));

        // restore
        var restoreCommand = new Command("restore", "Restore a soft-deleted payment log.");
        var restoreIdArgument = new Argument<Guid>("id");
        var restoreOutputOption = CreateOutputOption(includeNdjson: false);
        restoreCommand.Add(restoreIdArgument);
        restoreCommand.Add(restoreOutputOption);
        restoreCommand.SetAction((parseResult, cancellationToken) =>
            handler.RestoreAsync(
                parseResult.GetRequiredValue(restoreIdArgument),
                OutputModeParser.Parse(parseResult.GetValue(restoreOutputOption)),
                cancellationToken));

        // link-transaction
        var linkCommand = new Command("link-transaction", "Confirm a bank-transaction corroboration link.");
        var linkIdArgument = new Argument<Guid>("id");
        var linkTxOption = new Option<Guid>("--transaction-id") { Description = "PersonalTransaction id.", Required = true };
        var linkOutputOption = CreateOutputOption(includeNdjson: false);
        linkCommand.Add(linkIdArgument);
        linkCommand.Add(linkTxOption);
        linkCommand.Add(linkOutputOption);
        linkCommand.SetAction((parseResult, cancellationToken) =>
            handler.LinkTransactionAsync(
                parseResult.GetRequiredValue(linkIdArgument),
                parseResult.GetRequiredValue(linkTxOption),
                OutputModeParser.Parse(parseResult.GetValue(linkOutputOption)),
                cancellationToken));

        // unlink-transaction
        var unlinkCommand = new Command("unlink-transaction", "Remove a corroboration link.");
        var unlinkIdArgument = new Argument<Guid>("id");
        var unlinkOutputOption = CreateOutputOption(includeNdjson: false);
        unlinkCommand.Add(unlinkIdArgument);
        unlinkCommand.Add(unlinkOutputOption);
        unlinkCommand.SetAction((parseResult, cancellationToken) =>
            handler.UnlinkTransactionAsync(
                parseResult.GetRequiredValue(unlinkIdArgument),
                OutputModeParser.Parse(parseResult.GetValue(unlinkOutputOption)),
                cancellationToken));

        // summary-year
        var summaryCommand = new Command("summary-year", "Per-currency year summary (Today hero).");
        var summaryYearArgument = new Argument<int>("year");
        var summaryOutputOption = CreateOutputOption(includeNdjson: false);
        summaryCommand.Add(summaryYearArgument);
        summaryCommand.Add(summaryOutputOption);
        summaryCommand.SetAction((parseResult, cancellationToken) =>
            handler.YearSummaryAsync(
                parseResult.GetRequiredValue(summaryYearArgument),
                OutputModeParser.Parse(parseResult.GetValue(summaryOutputOption)),
                cancellationToken));

        command.Add(createCommand);
        command.Add(listCommand);
        command.Add(getCommand);
        command.Add(updateCommand);
        command.Add(deleteCommand);
        command.Add(restoreCommand);
        command.Add(linkCommand);
        command.Add(unlinkCommand);
        command.Add(summaryCommand);
        return command;
    }

    private static Command BuildCareEntitiesCommand(CareEntityCommandHandler handler)
    {
        var command = new Command("care-entities", "Manage Simi care entities (people & assets you look after).");

        // list
        var listCommand = new Command("list", "List your care entities.");
        var kindFilterOption = new Option<string?>("--kind") { Description = "Filter by kind: person or asset." };
        var assetTypeFilterOption = new Option<string?>("--asset-type") { Description = "Filter by asset type." };
        var includeArchivedOption = new Option<bool>("--include-archived") { Description = "Include archived entities." };
        var listOutputOption = CreateOutputOption(includeNdjson: false);
        listCommand.Add(kindFilterOption);
        listCommand.Add(assetTypeFilterOption);
        listCommand.Add(includeArchivedOption);
        listCommand.Add(listOutputOption);
        listCommand.SetAction((parseResult, cancellationToken) =>
            handler.ListAsync(
                new ListCareEntitiesOptions(
                    parseResult.GetValue(kindFilterOption),
                    parseResult.GetValue(assetTypeFilterOption),
                    parseResult.GetValue(includeArchivedOption),
                    OutputModeParser.Parse(parseResult.GetValue(listOutputOption))),
                cancellationToken));

        // get
        var getCommand = new Command("get", "Get a care entity by id.");
        var getIdArgument = new Argument<Guid>("id");
        var getOutputOption = CreateOutputOption(includeNdjson: false);
        getCommand.Add(getIdArgument);
        getCommand.Add(getOutputOption);
        getCommand.SetAction((parseResult, cancellationToken) =>
            handler.GetAsync(
                parseResult.GetRequiredValue(getIdArgument),
                OutputModeParser.Parse(parseResult.GetValue(getOutputOption)),
                cancellationToken));

        // create
        var createCommand = new Command("create", "Create a person or asset.");
        var createKindOption = new Option<string>("--kind") { Description = "person or asset.", Required = true };
        var createNameOption = new Option<string>("--name") { Description = "Display name.", Required = true };
        var createCountryOption = new Option<string>("--country") { Description = "ISO-3166-1 alpha-2 country code.", Required = true };
        var createAssetTypeOption = new Option<string?>("--asset-type") { Description = "Asset type (required when --kind asset)." };
        var createRelationshipOption = new Option<string?>("--relationship") { Description = "Free-text relationship label." };
        var createEmojiOption = new Option<string?>("--emoji") { Description = "Avatar emoji." };
        var createPhotoOption = new Option<Guid?>("--photo-document-id") { Description = "Optional avatar document id." };
        var createAttributesFileOption = new Option<string?>("--attributes-file") { Description = "Path to a JSON object of type-specific attributes." };
        var createOutputOption = CreateOutputOption(includeNdjson: false);
        createCommand.Add(createKindOption);
        createCommand.Add(createNameOption);
        createCommand.Add(createCountryOption);
        createCommand.Add(createAssetTypeOption);
        createCommand.Add(createRelationshipOption);
        createCommand.Add(createEmojiOption);
        createCommand.Add(createPhotoOption);
        createCommand.Add(createAttributesFileOption);
        createCommand.Add(createOutputOption);
        createCommand.SetAction((parseResult, cancellationToken) =>
            handler.CreateAsync(
                new CreateCareEntityOptions(
                    parseResult.GetRequiredValue(createKindOption),
                    parseResult.GetValue(createAssetTypeOption),
                    parseResult.GetRequiredValue(createNameOption),
                    parseResult.GetRequiredValue(createCountryOption),
                    parseResult.GetValue(createRelationshipOption),
                    parseResult.GetValue(createEmojiOption),
                    parseResult.GetValue(createPhotoOption),
                    parseResult.GetValue(createAttributesFileOption),
                    OutputModeParser.Parse(parseResult.GetValue(createOutputOption))),
                cancellationToken));

        // update
        var updateCommand = new Command("update", "Update a care entity.");
        var updateIdArgument = new Argument<Guid>("id");
        var updateNameOption = new Option<string>("--name") { Description = "Display name.", Required = true };
        var updateCountryOption = new Option<string>("--country") { Description = "ISO-3166-1 alpha-2 country code.", Required = true };
        var updateAssetTypeOption = new Option<string?>("--asset-type") { Description = "Asset type (assets only)." };
        var updateRelationshipOption = new Option<string?>("--relationship") { Description = "Relationship label." };
        var updateEmojiOption = new Option<string?>("--emoji") { Description = "Avatar emoji." };
        var updatePhotoOption = new Option<Guid?>("--photo-document-id") { Description = "Avatar document id." };
        var updateAttributesFileOption = new Option<string?>("--attributes-file") { Description = "Path to a JSON object of attributes." };
        var updateOutputOption = CreateOutputOption(includeNdjson: false);
        updateCommand.Add(updateIdArgument);
        updateCommand.Add(updateNameOption);
        updateCommand.Add(updateCountryOption);
        updateCommand.Add(updateAssetTypeOption);
        updateCommand.Add(updateRelationshipOption);
        updateCommand.Add(updateEmojiOption);
        updateCommand.Add(updatePhotoOption);
        updateCommand.Add(updateAttributesFileOption);
        updateCommand.Add(updateOutputOption);
        updateCommand.SetAction((parseResult, cancellationToken) =>
            handler.UpdateAsync(
                new UpdateCareEntityOptions(
                    parseResult.GetRequiredValue(updateIdArgument),
                    parseResult.GetRequiredValue(updateNameOption),
                    parseResult.GetValue(updateAssetTypeOption),
                    parseResult.GetRequiredValue(updateCountryOption),
                    parseResult.GetValue(updateRelationshipOption),
                    parseResult.GetValue(updateEmojiOption),
                    parseResult.GetValue(updatePhotoOption),
                    parseResult.GetValue(updateAttributesFileOption),
                    OutputModeParser.Parse(parseResult.GetValue(updateOutputOption))),
                cancellationToken));

        // archive
        var archiveCommand = new Command("archive", "Archive a care entity (soft; history preserved).");
        var archiveIdArgument = new Argument<Guid>("id");
        var archiveOutputOption = CreateOutputOption(includeNdjson: false);
        archiveCommand.Add(archiveIdArgument);
        archiveCommand.Add(archiveOutputOption);
        archiveCommand.SetAction((parseResult, cancellationToken) =>
            handler.ArchiveAsync(
                parseResult.GetRequiredValue(archiveIdArgument),
                OutputModeParser.Parse(parseResult.GetValue(archiveOutputOption)),
                cancellationToken));

        // profile
        var profileCommand = new Command("profile", "Get a care entity's one-call profile projection.");
        var profileIdArgument = new Argument<Guid>("id");
        var profileOutputOption = CreateOutputOption(includeNdjson: false);
        profileCommand.Add(profileIdArgument);
        profileCommand.Add(profileOutputOption);
        profileCommand.SetAction((parseResult, cancellationToken) =>
            handler.ProfileAsync(
                parseResult.GetRequiredValue(profileIdArgument),
                OutputModeParser.Parse(parseResult.GetValue(profileOutputOption)),
                cancellationToken));

        command.Add(listCommand);
        command.Add(getCommand);
        command.Add(createCommand);
        command.Add(updateCommand);
        command.Add(archiveCommand);
        command.Add(profileCommand);
        return command;
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
