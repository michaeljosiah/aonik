namespace Aonik.Ai.Services;

internal sealed class AiTraceExplorerOptions
{
    public string Provider { get; set; } = "Auto";
    public int DefaultPageSize { get; set; } = 50;
    public int MaxPageSize { get; set; } = 100;
}
