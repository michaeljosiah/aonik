using Aonik.Platform.Contracts.Services.Notifications;
using Fluid;

namespace Aonik.Infrastructure.Notifications;

/// <summary>
/// Renders notification templates using the Fluid (Liquid-dialect) template
/// engine. All current callers pass a <see cref="IDictionary{TKey,TValue}"/>
/// with snake_case keys (e.g. <c>first_name</c>, <c>otp_code</c>) so the
/// default Fluid dictionary adapter handles member access natively — no
/// type registration is required.
///
/// If a future caller passes a typed CLR object, register its type with
/// <see cref="TemplateOptions.MemberAccessStrategy"/> so Fluid can walk
/// it. Fluid's default is strict: unregistered types expose no properties,
/// which is the behaviour we want for an admin-authored template surface.
/// </summary>
public class FluidNotificationTemplateRenderer : INotificationTemplateRenderer
{
    private readonly FluidParser _parser;
    private readonly TemplateOptions _options;

    public FluidNotificationTemplateRenderer()
    {
        _parser = new FluidParser();
        _options = new TemplateOptions();

        // Allow nested dictionary walks — e.g. base-template composition
        // passes { "content": rendered, "model": originalModelDict } so
        // `{{ model.first_name }}` must resolve.
        _options.MemberAccessStrategy.Register<IDictionary<string, object?>>();
        _options.MemberAccessStrategy.Register<IDictionary<string, object>>();
    }

    public async Task<string> RenderAsync(
        string template,
        object? model,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(template))
            return string.Empty;

        if (!_parser.TryParse(template, out var parsed, out var error))
        {
            throw new InvalidOperationException($"Template parsing failed: {error}");
        }

        var context = new TemplateContext(model, _options);

        return await parsed.RenderAsync(context);
    }
}
