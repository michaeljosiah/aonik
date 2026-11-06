using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using Aonik.Publishing.Contracts;
using Aonik.Publishing.Services;

namespace Aonik.Infrastructure.Publishing;

public sealed class DockerPackageVerifier : IPackageVerifier
{
    public const string ReaderPin = "27df122566bcdef18126932c15a74112e18b8573";

    public async Task<PackageVerificationResult> VerifyAsync(VerifiedInput input, PackageVerifierOptions policy, CancellationToken cancellationToken = default)
    {
        if (!Qualified(policy) || !Path.IsPathFullyQualified(input.Path) || input.Path.Contains(',')) return Refused("verifier-unavailable", policy);
        var name = "aonik-verify-" + Guid.NewGuid().ToString("N");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(policy.TimeoutSeconds));
        using var process = new Process { StartInfo = BuildStartInfo(input, policy, name) };
        try
        {
            if (!process.Start()) return Refused("verifier-unavailable", policy);
            using var stop = timeout.Token.Register(() =>
            {
                try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { }
                catch (System.ComponentModel.Win32Exception) { }
            });
            var output = ReadBoundedAsync(process.StandardOutput, 262144, timeout.Token);
            var errors = ReadBoundedAsync(process.StandardError, 8192, timeout.Token);
            await Task.WhenAll(output, errors, process.WaitForExitAsync(timeout.Token));
            if (process.ExitCode != 0) return Refused(process.ExitCode == 137 ? "verifier-resource-limit" : "verifier-unavailable", policy);
            return VerifierProtocol.Parse(await output, input, policy);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return Refused("verifier-timeout", policy); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or InvalidDataException or JsonException)
        { return Refused("verifier-unavailable", policy); }
        finally
        {
            if (process.IdOrNull() is not null && !process.HasExited) process.Kill(entireProcessTree: true);
            // Killing a Docker CLI is not stopping its container. Always reclaim the generated container name.
            using var cleanup = new Process { StartInfo = StartInfo(policy.DockerExecutable, ["rm", "--force", name]) };
            try
            {
                if (cleanup.Start())
                {
                    using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    var stdout = ReadBoundedAsync(cleanup.StandardOutput, 8192, limit.Token);
                    var stderr = ReadBoundedAsync(cleanup.StandardError, 8192, limit.Token);
                    await Task.WhenAll(stdout, stderr, cleanup.WaitForExitAsync(limit.Token));
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException or System.ComponentModel.Win32Exception or IOException or InvalidDataException) { }
            finally { if (cleanup.IdOrNull() is not null && !cleanup.HasExited) cleanup.Kill(entireProcessTree: true); }
        }
    }

    public static ProcessStartInfo BuildStartInfo(VerifiedInput input, PackageVerifierOptions policy, string name)
    {
        if (!Qualified(policy) || !Regex.IsMatch(name, "^aonik-verify-[a-f0-9]{32}$") || !Path.IsPathFullyQualified(input.Path)
            || input.Path.Contains(',') || !Regex.IsMatch(input.Sha256, "^[a-f0-9]{64}$") || input.Bytes < 1)
            throw new ArgumentException("Unqualified verifier configuration or input.");
        return StartInfo(policy.DockerExecutable,
        [
            "run", "--rm", "--name", name, "--label", "aonik.publishing.verifier=true", "--network", "none", "--memory", $"{policy.MemoryMiB}m", "--cpus", "1", "--pids-limit", "32",
            "--read-only", "--cap-drop", "ALL", "--security-opt", "no-new-privileges", "--user", "65532:65532",
            "--tmpfs", $"/scratch:rw,noexec,nosuid,size={policy.ScratchMiB}m,mode=1777",
            "--mount", $"type=bind,source={input.Path},target=/input/package.zip,readonly", policy.Image,
            "/input/package.zip", input.Sha256, input.Bytes.ToString(System.Globalization.CultureInfo.InvariantCulture),
            policy.MaxAssets.ToString(System.Globalization.CultureInfo.InvariantCulture),
            policy.MaxAssetBytes.ToString(System.Globalization.CultureInfo.InvariantCulture),
            policy.MaxDocumentBytes.ToString(System.Globalization.CultureInfo.InvariantCulture), "/scratch",
        ]);
    }
    private static bool Qualified(PackageVerifierOptions p) => p.ReaderCommit == ReaderPin
        && p.Image is { Length: <= 300 } image && Regex.IsMatch(image, "^[A-Za-z0-9./:_-]+@sha256:[a-f0-9]{64}$")
        && !string.IsNullOrWhiteSpace(p.DockerExecutable) && p.DockerExecutable.Length <= 4096 && !p.DockerExecutable.Contains('\0') && p.TimeoutSeconds is >= 1 and <= 90
        && p.MemoryMiB is >= 64 and <= 1024 && p.ScratchMiB is >= 64 and <= 2048
        && p.MaxAssets is >= 1 and <= 1024 && p.MaxAssetBytes is >= 1 and <= 1073741824
        && p.MaxDocumentBytes is >= 1 and <= 8388608;
    private static ProcessStartInfo StartInfo(string executable, string[] arguments)
    {
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in arguments) info.ArgumentList.Add(arg);
        return info;
    }
    private static async Task<string> ReadBoundedAsync(StreamReader reader, int maximum, CancellationToken ct)
    {
        var text = new StringBuilder(); var buffer = new char[4096]; int read;
        while ((read = await reader.ReadAsync(buffer, ct)) != 0)
        {
            if (text.Length + read > maximum) throw new InvalidDataException("Verifier output exceeds its bound.");
            text.Append(buffer, 0, read);
        }
        return text.ToString();
    }
    private static PackageVerificationResult Refused(string code, PackageVerifierOptions p)
        => new(false, code, p.ReaderCommit, "", 0, "", "", "", "", null, null, null, []);
}

internal static class ProcessState
{
    public static int? IdOrNull(this Process process) { try { return process.Id; } catch (InvalidOperationException) { return null; } }
}
