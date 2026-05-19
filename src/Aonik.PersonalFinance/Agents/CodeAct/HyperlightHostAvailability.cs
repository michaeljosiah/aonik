using System.Runtime.InteropServices;
using HyperlightSandbox.Api;
using HyperlightSandbox.Guest.Python;

namespace Aonik.Finance.Agents.CodeAct;

/// <summary>
/// One-shot probe for whether this process can host a Hyperlight Python
/// sandbox (Spec 025 Phase 1). When false, each sub-agent descriptor's
/// <c>Build()</c> falls back to the conventional tool-loop
/// <c>ChatClientAgent</c> path so local Windows dev and Azure Container
/// Apps (which don't expose <c>/dev/kvm</c>) keep working.
/// </summary>
/// <remarks>
/// <para>
/// At hyperlight-sandbox v0.4.0 the Rust core runs on Linux only and
/// requires a hypervisor interface — either KVM (<c>/dev/kvm</c>) or
/// Microsoft Hypervisor (<c>/dev/mshv</c>). The probe gates on:
/// </para>
/// <list type="number">
///   <item>
///     <description>The <c>AONIK_DISABLE_HYPERLIGHT=true</c> environment
///     variable — explicit opt-out for testing the tool-loop fallback on
///     a Linux host that otherwise supports the sandbox.</description>
///   </item>
///   <item>
///     <description>OS check — Windows and macOS short-circuit to false
///     without touching the native loader.</description>
///   </item>
///   <item>
///     <description>Hypervisor device check — <c>/dev/kvm</c> or
///     <c>/dev/mshv</c> must exist. Azure Container Apps and most
///     unprivileged Kubernetes pods don't expose either, so this catches
///     the production failure mode early. Critically, the upstream
///     <c>SandboxBuilder().Build()</c> call defers hypervisor allocation
///     until invocation time — it returns success on hosts where the
///     actual sandbox would later fail with
///     <c>"No Hypervisor was found for Sandbox"</c>. The device-file
///     check is the only reliable pre-flight.</description>
///   </item>
///   <item>
///     <description>A real sandbox build — secondary safety net for
///     cases where the device exists but the library still can't
///     attach (e.g. <c>/dev/kvm</c> permission denied).</description>
///   </item>
/// </list>
/// <para>
/// A failed probe is sticky: the process stays on the tool-loop path until
/// it restarts. That's intentional — fixing hypervisor access at runtime
/// is exotic, and re-probing on every request would burn ~2.5 s per check.
/// </para>
/// </remarks>
internal static class HyperlightHostAvailability
{
    private const string DisableEnvVar = "AONIK_DISABLE_HYPERLIGHT";

    private static readonly string[] HypervisorDevices =
    {
        "/dev/kvm",
        "/dev/mshv",
    };

    private static readonly Lazy<bool> _cachedAvailability = new(Probe);

    /// <summary>
    /// <c>true</c> when this process can build Hyperlight sandboxes and
    /// should route sub-agents through the CodeAct path.
    /// </summary>
    public static bool IsAvailable => _cachedAvailability.Value;

    private static bool Probe()
    {
        if (string.Equals(
                Environment.GetEnvironmentVariable(DisableEnvVar),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return false;
        }

        // ACA / restricted containers don't expose the hypervisor device.
        // Hyperlight's `.Build()` lazily resolves the hypervisor and will
        // happily return a SafeHandle here that explodes on first invoke,
        // so we MUST check device-file existence ourselves to keep the
        // probe honest.
        if (!HypervisorDevices.Any(File.Exists))
        {
            return false;
        }

        try
        {
            using var probe = new SandboxBuilder()
                .WithPythonModule()
                .Build();
            return true;
        }
        catch
        {
            // Hypervisor unavailable (no /dev/kvm, container without nested
            // virtualisation, etc.) — fall back to the tool-loop path. We
            // deliberately swallow the exception: there is no useful caller
            // to surface it to at probe time.
            return false;
        }
    }
}
