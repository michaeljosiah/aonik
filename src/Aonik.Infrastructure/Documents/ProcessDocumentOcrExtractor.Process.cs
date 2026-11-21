using System.Diagnostics;
using System.Text;

using Aonik.SharedKernel.Abstractions.Documents;

namespace Aonik.Infrastructure.Documents;

internal sealed partial class ProcessDocumentOcrExtractor
{
    private static async Task<ProcessOutput> RunAsync(string executable, IReadOnlyList<string> arguments, string directory, CancellationToken cancellationToken)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var start = new ProcessStartInfo(executable) { WorkingDirectory = directory, UseShellExecute = false,
            CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        start.Environment["OMP_THREAD_LIMIT"] = "2";
        using var process = new Process { StartInfo = start };
        try
        {
            if (!process.Start()) throw new DocumentTextExtractionException("OcrProcessUnavailable");
            Task<string> output = ReadOutputAsync(process.StandardOutput, 2 * 1024 * 1024, budget);
            Task<string> error = ReadOutputAsync(process.StandardError, 16 * 1024, budget);
            await Task.WhenAll(output, error, process.WaitForExitAsync(budget.Token));
            if (process.ExitCode != 0) throw new DocumentTextExtractionException("OcrProcessFailed");
            return new(await output, await error);
        }
        catch (System.ComponentModel.Win32Exception) { throw new DocumentTextExtractionException("OcrProcessUnavailable"); }
        finally
        {
            try
            {
                if (process.Id > 0 && !process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
                }
            }
            catch (InvalidOperationException) { }
        }
    }

    private static async Task<string> ReadOutputAsync(StreamReader input, int maximum, CancellationTokenSource budget)
    {
        var result = new StringBuilder(); char[] buffer = new char[4096]; int read;
        while ((read = await input.ReadAsync(buffer, budget.Token)) > 0)
        {
            if (result.Length + read > maximum) { await budget.CancelAsync(); throw new DocumentTextExtractionException("OcrOutputLimit"); }
            result.Append(buffer, 0, read);
        }
        return result.ToString();
    }
    private sealed record ProcessOutput(string Output, string Error);
}
