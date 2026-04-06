namespace Aonik.Cli.Models;

public enum OutputMode
{
    Text,
    Json,
    Ndjson
}

public static class OutputModeParser
{
    public static OutputMode Parse(string? value)
    {
        if (string.Equals(value, "ndjson", StringComparison.OrdinalIgnoreCase))
        {
            return OutputMode.Ndjson;
        }

        if (string.Equals(value, "json", StringComparison.OrdinalIgnoreCase))
        {
            return OutputMode.Json;
        }

        return OutputMode.Text;
    }
}
