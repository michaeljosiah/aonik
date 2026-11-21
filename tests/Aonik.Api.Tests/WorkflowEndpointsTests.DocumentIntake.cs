using System.Net;
using System.Net.Http.Json;

using FluentAssertions;

using Aonik.Agents.Contracts.Models.Workflows;

namespace Aonik.Api.Tests;

public partial class WorkflowEndpointsTests
{
    [Fact]
    public async Task FinancialCandidateAndContentRoutes_Should_RequireAuthenticationAndExplicitFinancialDocumentReadPermission()
    {
        string[] routes = [$"/documents/financial-extractions/{Guid.NewGuid()}", $"/documents/{Guid.NewGuid()}/financial-extractions",
            $"/documents/{Guid.NewGuid()}/files/{Guid.NewGuid()}/financial-content"];
        using HttpClient anonymous = factory.CreateClient();
        using HttpClient without = await ClientAsync("Workflow.Read");
        using HttpClient authorized = await ClientAsync("Workflow.Read", "Document.Read");

        foreach (string route in routes)
        {
            (await anonymous.GetAsync(route)).StatusCode.Should().Be(HttpStatusCode.Unauthorized, route);
            (await without.GetAsync(route)).StatusCode.Should().Be(HttpStatusCode.Forbidden, route);
            (await authorized.GetAsync(route)).StatusCode.Should().Be(HttpStatusCode.NotFound, route);
        }
    }

    [Fact]
    public async Task WorkflowCapabilities_Should_AdvertiseExplicitDocumentFacts_AndSeparateExtractionFromConfirmedCreation()
    {
        using HttpClient reader = await ClientAsync("Workflow.Read");

        var capabilities = await reader.GetFromJsonAsync<WorkflowCapabilities>("/ai/workflows/capabilities");

        capabilities!.SubjectTypes.Should().Contain("Document");
        capabilities.SubjectFacts!["Document"].Should().Contain(field => field.Name == "document.fileId" && field.Type == "Id");
        capabilities.Actions.Should().Contain(action => action.Key == "documents.financial.extract" && action.RiskTier == "Low")
            .And.Contain(action => action.Key == "finance.invoices.create_from_document" && action.RiskTier == "Medium");
    }
}
