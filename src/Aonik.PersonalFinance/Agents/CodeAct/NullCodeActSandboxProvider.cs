using Aonik.SharedKernel.Abstractions.Agents;
using Microsoft.Extensions.AI;

namespace Aonik.PersonalFinance.Agents.CodeAct;

/// <summary>
/// No-op CodeAct provider. Used when <c>Ai:CodeAct:Provider</c> is unset or
/// set to <c>"Disabled"</c>. Always returns <c>null</c> so sub-agent
/// descriptors fall through to the conventional tool-loop path that we
/// validated end-to-end after commit <c>69620409</c>.
/// </summary>
public sealed class NullCodeActSandboxProvider : ICodeActSandboxProvider
{
    public AIFunction? TryBuildExecuteCodeTool(
        CodeActSandboxContext context,
        IReadOnlyList<AIFunction> hostTools) => null;
}
