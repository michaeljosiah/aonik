using System.Text.Json;

using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Agents.Services.Connectors.Catalog;

internal sealed class ConnectorPackageActionContract
{
    private readonly ConnectorPackageSchema _input;
    private readonly ConnectorPackageSchema _output;
    private readonly ConnectorPackageLimits _limits;
    public ConnectorActionDescriptor Descriptor { get; }

    public ConnectorPackageActionContract(string connector, ConnectorPackageAction action, ConnectorPackageLimits limits)
    {
        _input = new ConnectorPackageSchema(action.InputSchema);
        _output = new ConnectorPackageSchema(action.OutputSchema);
        _limits = limits;
        string tool = ConnectorInvocationIdentity.CuratedToolPrefix + ConnectorPackageReferences.ToolName(action)["connector_".Length..];
        bool read = action.MinimumRiskTier == "ReadOnly";
        ToolApprovalOptions? approval = read ? null : new(ToolApprovalTier.High, action.Key, ConnectorInvocationIdentity.CuratedProposalPrefix + tool);
        JsonElement input = read ? action.InputSchema : ConnectorJson.Element(new
        {
            type = "object", properties = new { idempotencyKey = new { type = "string", minLength = 1, maxLength = 128 }, arguments = action.InputSchema },
            required = new[] { "idempotencyKey", "arguments" }, additionalProperties = false
        });
        Descriptor = new(connector, action.Key, action.Version, tool, [], approval, input, action.OutputSchema);
    }

    public JsonElement RequireInput(JsonElement input)
    {
        JsonElement arguments = input;
        if (Descriptor.Approval is not null)
        {
            if (input.ValueKind != JsonValueKind.Object || input.EnumerateObject().Count() != 2 || !input.TryGetProperty("arguments", out arguments))
                throw new ArgumentException("Select the exact action arguments and a stable operation key.");
            _ = ConnectorInvocationIdentity.IdempotencyKey(input);
        }
        _input.Require(arguments, _limits.MaximumInputBytes);
        return arguments;
    }

    public void RequireOutput(JsonElement output) => _output.Require(output, _limits.MaximumOutputBytes);
}
