using System.Text.Json;

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
        kql.Should().Contain("| project observationId, traceId, parentObservationId, aiRunId, timestamp, endTime=datetime(null), type, name, traceName, input, output, metadata, level, latencySeconds, costUsd, ttftSeconds, providedModel, inputTokens, outputTokens, totalTokens, operationId, agentId, agentName, durationMs, serviceName;");
        kql.Should().Contain("let dependencySpans = dependencies");
        kql.Should().NotContain("union traceLogs, dependencySpans, requestSpans\n        | where isCandidateRootObservation == true");
    }

    [Fact]
    public void BuildKql_ShouldDeriveTraceLogStartTime_FromLogTimestampMinusDuration()
    {
        // The AiTraceObservation log line is emitted from OnComplete (after
        // the chat call finishes), so the AppInsights timestamp is the END
        // of the operation. We must subtract durationMs to recover the real
        // start, otherwise the GENERATION root span renders to the right of
        // its children in the waterfall. `case` is used (not `iff`) because
        // it treats a null predicate as false — when durationMs is null,
        // iff(null, ...) propagates null and timestamp becomes unparseable.
        var request = new ListAiTraceObservationsRequest { TimeRange = "24h" };

        var kql = AppInsightsAiTraceReader.BuildKql(request, 1, 100);

        kql.Should().Contain("logEndTime = timestamp");
        kql.Should().Contain("logStartTime = case(isnotnull(durationMs) and durationMs > 0, timestamp - 1ms * durationMs, timestamp)");
        kql.Should().Contain("| project observationId, traceId, parentObservationId, aiRunId, timestamp = logStartTime, endTime = logEndTime,");
    }

    [Fact]
    public void ParseRow_ShouldProduceStartTimePlusDurationApproximatelyEqualToEndTime_ForGenerationRow()
    {
        // Row shape mirrors the corrected KQL projection for traceLogs:
        // column 4 = real start time, column 5 = end time, column 23 = durationMs.
        const double durationMs = 4633.0;
        var startUtc = "2026-04-29T08:11:32.5267000Z";
        var endUtc = "2026-04-29T08:11:37.1597000Z";

        var row = new JsonElement[]
        {
            JsonDocument.Parse("\"531907db8d12abcd\"").RootElement,
            JsonDocument.Parse("\"1173b0a559ad3e131f297e9e17045fc7\"").RootElement,
            JsonDocument.Parse("null").RootElement,
            JsonDocument.Parse("\"00000000-0000-0000-0000-000000000000\"").RootElement,
            JsonDocument.Parse($"\"{startUtc}\"").RootElement,
            JsonDocument.Parse($"\"{endUtc}\"").RootElement,
            JsonDocument.Parse("\"GENERATION\"").RootElement,
            JsonDocument.Parse("\"chat\"").RootElement,
            JsonDocument.Parse("\"chat\"").RootElement,
            JsonDocument.Parse("null").RootElement,
            JsonDocument.Parse("null").RootElement,
            JsonDocument.Parse("null").RootElement,
            JsonDocument.Parse("\"DEFAULT\"").RootElement,
            JsonDocument.Parse("4.633").RootElement,
            JsonDocument.Parse("null").RootElement,
            JsonDocument.Parse("null").RootElement,
            JsonDocument.Parse("\"gpt-5-mini-2025-08-07\"").RootElement,
            JsonDocument.Parse("10").RootElement,
            JsonDocument.Parse("16").RootElement,
            JsonDocument.Parse("26").RootElement,
            JsonDocument.Parse("\"1173b0a559ad3e131f297e9e17045fc7\"").RootElement,
            JsonDocument.Parse("null").RootElement,
            JsonDocument.Parse("null").RootElement,
            JsonDocument.Parse($"{durationMs}").RootElement,
            JsonDocument.Parse("\"aonik-dev-api\"").RootElement,
        };

        var parsed = typeof(AppInsightsAiTraceReader)
            .GetMethod("ParseRow", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .Invoke(null, [row]) as AiTraceObservationResponse;

        parsed.Should().NotBeNull();
        parsed!.EndTime.Should().NotBeNull();
        parsed.StartTime.Should().BeBefore(parsed.EndTime!.Value);
        parsed.DurationMs.Should().Be(durationMs);
        parsed.StartTime.AddMilliseconds(parsed.DurationMs!.Value)
            .Should().BeCloseTo(parsed.EndTime!.Value, TimeSpan.FromMilliseconds(2));
    }

    [Fact]
    public void NormalizeSelfParentedAiObservation_ShouldTreatAsRoot()
    {
        var row = new JsonElement[]
        {
            JsonDocument.Parse("\"59a6307613d656d6\"").RootElement,
            JsonDocument.Parse("\"0a2b4930f9bbaf63cdcfebcfaf06d0d2\"").RootElement,
            JsonDocument.Parse("\"59a6307613d656d6\"").RootElement,
            JsonDocument.Parse("\"00000000-0000-0000-0000-000000000000\"").RootElement,
            JsonDocument.Parse("\"2026-04-29T08:11:08.2334883Z\"").RootElement,
            JsonDocument.Parse("null").RootElement,
            JsonDocument.Parse("\"GENERATION\"").RootElement,
            JsonDocument.Parse("\"chat\"").RootElement,
            JsonDocument.Parse("\"chat\"").RootElement,
            JsonDocument.Parse("null").RootElement,
            JsonDocument.Parse("null").RootElement,
            JsonDocument.Parse("null").RootElement,
            JsonDocument.Parse("\"DEFAULT\"").RootElement,
            JsonDocument.Parse("2.961").RootElement,
            JsonDocument.Parse("0.000345").RootElement,
            JsonDocument.Parse("null").RootElement,
            JsonDocument.Parse("\"gpt-5-mini-2025-08-07\"").RootElement,
            JsonDocument.Parse("10").RootElement,
            JsonDocument.Parse("16").RootElement,
            JsonDocument.Parse("26").RootElement,
            JsonDocument.Parse("\"0a2b4930f9bbaf63cdcfebcfaf06d0d2\"").RootElement,
            JsonDocument.Parse("null").RootElement,
            JsonDocument.Parse("null").RootElement,
            JsonDocument.Parse("null").RootElement,
            JsonDocument.Parse("\"aonik-dev-api\"").RootElement,
        };

        var parsed = typeof(AppInsightsAiTraceReader)
            .GetMethod("ParseRow", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .Invoke(null, [row]) as AiTraceObservationResponse;

        parsed.Should().NotBeNull();
        parsed!.ParentObservationId.Should().BeNull();
        parsed.ParentSpanId.Should().BeNull();
        parsed.IsRootObservation.Should().BeTrue();
    }
}
