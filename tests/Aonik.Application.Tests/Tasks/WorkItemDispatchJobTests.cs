using System.Threading;
using System.Threading.Tasks;
using Aonik.Platform.Contracts.Services.Tasks;
using Aonik.Worker.Jobs;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Quartz;
using Xunit;

namespace Aonik.Application.Tests.Tasks;

public sealed class WorkItemDispatchJobTests
{
    private static IJobExecutionContext JobContext()
    {
        var mock = new Mock<IJobExecutionContext>();
        mock.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        mock.SetupProperty(c => c.Result);
        return mock.Object;
    }

    [Fact]
    public async Task Execute_Should_DispatchDue_With_ConfiguredOptions_When_Enabled()
    {
        var dispatcher = new Mock<IWorkItemDispatcher>();
        dispatcher
            .Setup(d => d.DispatchDueAsync(It.IsAny<WorkItemDispatchOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkItemDispatchSummary(2, 1, 1, 0, 0));

        var options = Microsoft.Extensions.Options.Options.Create(new ScheduledJobOptions
        {
            WorkItemDispatch = new WorkItemDispatchJobOptions
            {
                Enabled = true,
                BatchSize = 50,
                LeaseSeconds = 120,
                MaxAttempts = 4,
            },
        });
        var job = new WorkItemDispatchJob(dispatcher.Object, options, NullLogger<WorkItemDispatchJob>.Instance);
        var context = JobContext();

        await job.Execute(context);

        dispatcher.Verify(d => d.DispatchDueAsync(
            It.Is<WorkItemDispatchOptions>(o => o.BatchSize == 50 && o.LeaseSeconds == 120 && o.MaxAttempts == 4),
            It.IsAny<CancellationToken>()), Times.Once);
        context.Result.Should().NotBeNull();
    }

    [Fact]
    public async Task Execute_Should_NotDispatch_When_Disabled()
    {
        var dispatcher = new Mock<IWorkItemDispatcher>();
        var options = Microsoft.Extensions.Options.Options.Create(new ScheduledJobOptions
        {
            WorkItemDispatch = new WorkItemDispatchJobOptions { Enabled = false },
        });
        var job = new WorkItemDispatchJob(dispatcher.Object, options, NullLogger<WorkItemDispatchJob>.Instance);

        await job.Execute(JobContext());

        dispatcher.Verify(d => d.DispatchDueAsync(It.IsAny<WorkItemDispatchOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
