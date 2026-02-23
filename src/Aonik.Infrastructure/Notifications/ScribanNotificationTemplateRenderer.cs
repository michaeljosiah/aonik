using System.Linq;

using Scriban;
using Scriban.Runtime;
using Aonik.Platform.Contracts.Services.Notifications;

namespace Aonik.Infrastructure.Notifications;

public class ScribanNotificationTemplateRenderer : INotificationTemplateRenderer
{
    public Task<string> RenderAsync(
        string template,
        object? model,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(template))
            return Task.FromResult(string.Empty);

        var parsedTemplate = Template.Parse(template);

        if (parsedTemplate.HasErrors)
        {
            var errors = string.Join("; ", parsedTemplate.Messages.Select(message => message.Message));
            throw new InvalidOperationException($"Template parsing failed: {errors}");
        }

        var scriptObject = new ScriptObject();

        if (model != null)
        {
            scriptObject.Import(model, renamer: member => member.Name);
        }

        var context = new TemplateContext
        {
            MemberRenamer = member => member.Name
        };

        context.PushGlobal(scriptObject);

        var result = parsedTemplate.Render(context);
        return Task.FromResult(result);
    }
}
