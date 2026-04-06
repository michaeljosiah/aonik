using Aonik.Cli.Abstractions;
using Aonik.Cli.Models;
using Spectre.Console;

namespace Aonik.Cli.Commands;

public sealed class ShellCommandHandler
{
    private readonly ISessionStore _sessionStore;
    private readonly AuthCommandHandler _authCommandHandler;
    private readonly AgentCommandHandler _agentCommandHandler;
    private readonly ApprovalCommandHandler _approvalCommandHandler;
    private readonly ICliOutputWriter _outputWriter;
    private readonly IAnsiConsole _console;

    public ShellCommandHandler(
        ISessionStore sessionStore,
        AuthCommandHandler authCommandHandler,
        AgentCommandHandler agentCommandHandler,
        ApprovalCommandHandler approvalCommandHandler,
        ICliOutputWriter outputWriter,
        IAnsiConsole console)
    {
        _sessionStore = sessionStore;
        _authCommandHandler = authCommandHandler;
        _agentCommandHandler = agentCommandHandler;
        _approvalCommandHandler = approvalCommandHandler;
        _outputWriter = outputWriter;
        _console = console;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var session = await _sessionStore.LoadAsync(cancellationToken);
        if (session is null)
        {
            throw new AonikCliException("No active session found. Run 'aonik auth login' first.");
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            var choice = _console.Prompt(
                new SelectionPrompt<string>()
                    .Title("AONIK shell")
                    .AddChoices(
                    [
                        "Session status",
                        "Who am I",
                        "List agents",
                        "Send agent message",
                        "Stream agent message",
                        "List threads",
                        "List approvals",
                        "Exit"
                    ]));

            switch (choice)
            {
                case "Session status":
                    await _authCommandHandler.StatusAsync(OutputMode.Text, cancellationToken);
                    break;

                case "Who am I":
                    await _authCommandHandler.WhoAmIAsync(OutputMode.Text, cancellationToken);
                    break;

                case "List agents":
                    await _agentCommandHandler.ListAsync(OutputMode.Text, cancellationToken);
                    break;

                case "Send agent message":
                {
                    var message = _console.Ask<string>("Message");
                    if (string.IsNullOrWhiteSpace(message))
                    {
                        await _outputWriter.WriteInfoAsync("Message cannot be empty.", cancellationToken);
                        break;
                    }

                    await _agentCommandHandler.RunAsync(
                        new RunAgentOptions(message, SessionId: null, ThreadId: null, OutputMode.Text),
                        cancellationToken);
                    break;
                }

                case "Stream agent message":
                {
                    var message = _console.Ask<string>("Message");
                    if (string.IsNullOrWhiteSpace(message))
                    {
                        await _outputWriter.WriteInfoAsync("Message cannot be empty.", cancellationToken);
                        break;
                    }

                    await _agentCommandHandler.StreamAsync(
                        new StreamAgentOptions(message, ThreadId: null, RunId: null, AgentId: null, OutputMode.Text),
                        cancellationToken);
                    break;
                }

                case "List threads":
                    await _agentCommandHandler.ListThreadsAsync(
                        new ListThreadsOptions(Page: 1, PageSize: 20, OutputMode.Text),
                        cancellationToken);
                    break;

                case "List approvals":
                    await _approvalCommandHandler.ListAsync(OutputMode.Text, cancellationToken);
                    break;

                case "Exit":
                    return 0;
            }
        }

        return 0;
    }
}
