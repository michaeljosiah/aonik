namespace Aonik.SharedKernel.Modules;

/// <summary>
/// Endpoint metadata marker that exempts an endpoint from the per-tenant module gate (Spec 097 §11).
/// </summary>
/// <remarks>
/// <para>
/// The HTTP gate resolves every endpoint's module from its assembly's <see cref="AonikModuleAttribute"/>
/// and denies the request with <c>403 module.disabled</c> when the tenant has that module off. A
/// small number of endpoints must keep answering regardless — the module-state read that lets the
/// disabled-module page explain itself, health and manifest endpoints, and anything that resolves
/// the tenant before the gate can run. Those attach an instance of this class as endpoint metadata
/// (for FastEndpoints: <c>Description(b => b.WithMetadata(new ModuleGateExempt()))</c>) and the gate
/// skips them.
/// </para>
/// <para>
/// This is a deliberate, greppable opt-out: a reviewer searching for <c>ModuleGateExempt</c> finds
/// every endpoint that bypasses enforcement. The gate itself lives in the Api host (P3); this marker
/// lives in SharedKernel so module assemblies can declare the exemption without referencing Api.
/// </para>
/// </remarks>
public sealed class ModuleGateExempt
{
}
