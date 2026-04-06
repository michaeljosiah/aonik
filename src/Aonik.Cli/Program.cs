namespace Aonik.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var application = CliApplication.CreateDefault();

        try
        {
            return await application.RunAsync(args);
        }
        catch (AonikCliException ex)
        {
            await Console.Error.WriteLineAsync($"Error: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Unexpected error: {ex.Message}");
            return 1;
        }
    }
}
