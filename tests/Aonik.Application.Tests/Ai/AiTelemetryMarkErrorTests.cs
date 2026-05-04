using System.Diagnostics;
using Aonik.SharedKernel.Abstractions.Ai;
using FluentAssertions;

namespace Aonik.Application.Tests.Ai;

/// <summary>
/// Pins the contract of <see cref="AiTelemetry.MarkError"/>. The trace
/// explorer projects every <c>customDimensions</c> tag into its detail
/// view, and the new <c>/admin/observability/traces/explain</c> AI
/// analysis endpoint pulls these tags into the LLM payload — so any
/// missing tag becomes invisible to a human admin debugging a failure.
/// </summary>
public class AiTelemetryMarkErrorTests : IDisposable
{
    private readonly ActivityListener _listener;

    public AiTelemetryMarkErrorTests()
    {
        // ActivitySource won't actually start activities unless something
        // is listening — register a no-op listener so the SUT can create
        // real Activities for assertions.
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AiTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose() => _listener.Dispose();

    [Fact]
    public void MarkError_Should_StampErrorTypeMessageAndStacktrace_OnLiveActivity()
    {
        using var activity = AiTelemetry.ActivitySource.StartActivity(
            "test.markerror.standard", ActivityKind.Internal);
        activity.Should().NotBeNull("the listener should make activities live");

        Exception captured;
        try
        {
            throw new InvalidOperationException("Things went sideways.");
        }
        catch (Exception ex)
        {
            captured = ex;
            AiTelemetry.MarkError(activity, ex);
        }

        activity!.Status.Should().Be(ActivityStatusCode.Error);
        activity.GetTagItem("error.type").Should().Be(nameof(InvalidOperationException));
        activity.GetTagItem("error.message").Should().Be("Things went sideways.");

        var stacktrace = activity.GetTagItem("error.stacktrace") as string;
        stacktrace.Should().NotBeNullOrWhiteSpace(
            "captured exceptions always carry a stack trace, and the analyser needs it to identify the fault site");
        stacktrace!.Should().Contain(nameof(MarkError_Should_StampErrorTypeMessageAndStacktrace_OnLiveActivity),
            "the stacktrace excerpt should include the throwing test method");
        stacktrace.Length.Should().BeLessThanOrEqualTo(AiTelemetry.MaxStacktraceCharacters);

        captured.Should().BeOfType<InvalidOperationException>(); // sanity — the test threw the right type
    }

    [Fact]
    public void MarkError_Should_TruncateStacktrace_WhenItExceedsLimit()
    {
        using var activity = AiTelemetry.ActivitySource.StartActivity(
            "test.markerror.truncate", ActivityKind.Internal);
        activity.Should().NotBeNull();

        // Synthesise an exception with a stack trace longer than the cap.
        var ex = new BloatedStackException(new string('x', AiTelemetry.MaxStacktraceCharacters + 500));

        AiTelemetry.MarkError(activity, ex);

        var stacktrace = activity!.GetTagItem("error.stacktrace") as string;
        stacktrace.Should().NotBeNull();
        stacktrace!.Length.Should().Be(
            AiTelemetry.MaxStacktraceCharacters,
            "an oversized stacktrace must be truncated so a single error doesn't dominate the trace span payload");
    }

    [Fact]
    public void MarkError_Should_SynthesiseMessage_When_OperationCanceledExceptionHasEmptyMessage()
    {
        using var activity = AiTelemetry.ActivitySource.StartActivity(
            "test.markerror.oce", ActivityKind.Internal);
        activity.Should().NotBeNull();

        // Newer .NET runtimes set a default OCE message but older paths
        // and some custom cancellation paths produce empty messages —
        // mirror that case explicitly.
        var ex = new OperationCanceledException(string.Empty);

        AiTelemetry.MarkError(activity, ex);

        activity!.GetTagItem("error.type").Should().Be(nameof(OperationCanceledException));
        activity.GetTagItem("error.message").Should().Be(
            "operation cancelled — likely timeout",
            "an empty cancellation message renders as a useless empty string in the trace explorer");
    }

    [Fact]
    public void MarkError_Should_PreserveExplicitOceMessage_When_Provided()
    {
        using var activity = AiTelemetry.ActivitySource.StartActivity(
            "test.markerror.oce-with-message", ActivityKind.Internal);
        activity.Should().NotBeNull();

        var ex = new TaskCanceledException("Per-chunk timeout fired at 10s.");

        AiTelemetry.MarkError(activity, ex);

        activity!.GetTagItem("error.type").Should().Be(nameof(TaskCanceledException));
        activity.GetTagItem("error.message").Should().Be("Per-chunk timeout fired at 10s.");
    }

    [Fact]
    public void MarkError_Should_BeSafe_When_ActivityIsNull()
    {
        // Ambient sampling sometimes leaves Activity.Current null — the
        // helper must not throw in that case.
        var act = () => AiTelemetry.MarkError(null, new InvalidOperationException("nope"));
        act.Should().NotThrow();
    }

    private sealed class BloatedStackException : Exception
    {
        public BloatedStackException(string fakeStack) : base("bloat") => _stack = fakeStack;
        private readonly string _stack;
        public override string? StackTrace => _stack;
    }
}
