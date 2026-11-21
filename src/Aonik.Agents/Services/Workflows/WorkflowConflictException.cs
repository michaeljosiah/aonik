namespace Aonik.Agents.Services.Workflows;

public sealed class WorkflowConflictException(string message) : Exception(message);
