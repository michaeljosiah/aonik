using System.Text.Json;
using Aonik.Agents.Contracts.Models.Tenant;
using Aonik.Agents.Framework;
using FastEndpoints;
using FluentValidation;

namespace Aonik.Agents.Endpoints.TenantExtensions;

// FastEndpoints auto-discovers these and returns 400 before HandleAsync (Spec 033).

/// <summary>
/// Validates a declarative HTTP tool save (create/update). The tool <c>Name</c> becomes the
/// function name the chat provider sees, so it must satisfy the OpenAI tool-name constraint — a name
/// with spaces/dots or over 64 chars is rejected here, before it can be persisted or activated.
/// </summary>
public sealed class SaveHttpToolRequestValidator : Validator<SaveHttpToolRequest>
{
    private static readonly string[] AllowedMethods = ["GET", "POST", "PUT", "PATCH", "DELETE"];

    public SaveHttpToolRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tool name is required.")
            .Must(ToolNameRules.IsValid).WithMessage(ToolNameRules.Message);

        RuleFor(x => x.Description).MaximumLength(2000);

        RuleFor(x => x.Method)
            .NotEmpty().WithMessage("HTTP method is required.")
            .Must(m => AllowedMethods.Contains(m?.Trim().ToUpperInvariant()))
            .WithMessage("Method must be one of: GET, POST, PUT, PATCH, DELETE.");

        RuleFor(x => x.UrlTemplate)
            .NotEmpty().WithMessage("URL template is required.")
            .MaximumLength(2048);

        RuleFor(x => x.ParameterSchemaJson)
            .MaximumLength(64_000)
            .Must(BeJsonObjectOrEmpty)
            .WithMessage("Parameter schema must be a JSON object.");
    }

    private static bool BeJsonObjectOrEmpty(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return true;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>Validates an MCP server save (create/update). The server name is a display/transport name (not a function name).</summary>
public sealed class SaveMcpServerRequestValidator : Validator<SaveMcpServerRequest>
{
    public SaveMcpServerRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Server name is required.").MaximumLength(200);
        RuleFor(x => x.Endpoint)
            .NotEmpty().WithMessage("Endpoint is required.")
            .MaximumLength(2048)
            .Must(e => Uri.TryCreate(e, UriKind.Absolute, out _))
            .WithMessage("Endpoint must be an absolute URL.");
    }
}
