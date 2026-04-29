using Aonik.Ai.Contracts.Models;
using Aonik.Ai.Services;
using FluentAssertions;

namespace Aonik.Application.Tests.Ai;

public class AppInsightsAiTraceReaderTests
{
    [Fact]
    public void BuildKql_ShouldScopeDetailQueriesByOperationId_WithoutAiOnlyDependencyFilter()
    {
        var request = new ListAiTraceObservationsRequest
        {
            TraceId = "7c81fedc84747072911db1934f1ded21",
            TimeRange = "24h",
        };

        var kql = AppInsightsAiTraceReader.BuildKql(request, 1, 200);

        kql.Should().Contain("let dependencySpans = dependencies");
        kql.Should().Contain("| where operation_Id == \"7c81fedc84747072911db1934f1ded21\"");
        kql.Should().Contain("let requestSpans = requests");
        kql.Should().NotContain("| where isnotempty(tostring(customDimensions[\"aonik.ai_run_id\"]))");
    }

    [Fact]
    public void BuildKql_ShouldExcludeRequestAndDependencySpans_FromRootTraceListing()
    {
        var request = new ListAiTraceObservationsRequest
        {
            IsRootObservation = true,
            TimeRange = "24h",
        };

        var kql = AppInsightsAiTraceReader.BuildKql(request, 1, 100);

        kql.Should().Contain("let dependencySpans = dependencies");
        kql.Should().Contain("let requestSpans = requests");
        kql.Should().Contain("| where false");
    }

    [Theory]
    [InlineData("00-7c81fedc84747072911db1934f1ded21-4bf92f3577b34da6-01", "4bf92f3577b34da6")]
    [InlineData("|7c81fedc84747072911db1934f1ded21.4bf92f3577b34da6.", "4bf92f3577b34da6")]
    [InlineData("4bf92f3577b34da6", "4bf92f3577b34da6")]
    public void NormalizeSpanId_ShouldReturnSpanSegment(string input, string expected)
    {
        AppInsightsAiTraceReader.NormalizeSpanId(input).Should().Be(expected);
    }

    [Fact]
    public void BuildKql_ShouldTreatAiObservationsUnderTraceRoot_AsRootCandidates()
    {
        var request = new ListAiTraceObservationsRequest
        {
            IsRootObservation = true,
            TimeRange = "24h",
        };

        var kql = AppInsightsAiTraceReader.BuildKql(request, 1, 100);

        kql.Should().Contain("isCandidateRootObservation = isempty(parentObservationId) or parentObservationId == normalizedParentId or normalizedParentId == traceId");
        kql.Should().Contain("| where isCandidateRootObservation == true");
    }
}
