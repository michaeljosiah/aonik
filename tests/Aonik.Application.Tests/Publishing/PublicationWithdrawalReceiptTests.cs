using System.Text.Json.Nodes;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;

using Aonik.Publishing.Contracts;
using Aonik.Publishing.Entities;
using Aonik.Publishing.Services;
using Aonik.Publishing.Testing;

namespace Aonik.Application.Tests.Publishing;

public class PublicationWithdrawalReceiptTests
{
    [Fact]
    public async Task BaseWithdrawal_Should_UpdateEveryCurrentReceiptInTheSequence()
    {
        using var scenario = new PublicationScenario();
        var (baseId, partId, editions) = await PublishSequenceAsync(scenario);
        var edition = editions.Single(item => item.Kind == "listening" || item.Slot == "world");

        var operation = await scenario.Service.WithdrawAsync(edition.Id, new(Guid.NewGuid(), edition.Version, "Withdraw the synthetic sequence"));
        await scenario.DeliverAsync(operation.Id);

        (await scenario.Db.Submissions.Where(row => row.Id == baseId || row.Id == partId).ToListAsync())
            .Should().OnlyContain(row => row.Status == SubmissionStatuses.Withdrawn);
        JsonNode.Parse(await scenario.HeadAsync())!["worlds"]!.AsArray().Should().BeEmpty();
    }

    [Fact]
    public async Task ExactWithdrawalReplay_Should_RepairLegacyCascadeReceipts_WithoutNewOperationOrSerial()
    {
        using var scenario = new PublicationScenario();
        var (baseId, partId, editions) = await PublishSequenceAsync(scenario);
        var edition = editions.Single(item => item.Kind == "part");
        var command = new PublicationActionRequest(Guid.NewGuid(), edition.Version, "Withdraw the synthetic sequence");
        var operation = await scenario.Service.WithdrawAsync(edition.Id, command);
        await scenario.DeliverAsync(operation.Id);
        var head = await scenario.HeadAsync();
        var operations = await scenario.Db.PublicationOperations.CountAsync();
        var snapshots = await scenario.Db.CatalogueSnapshots.CountAsync();
        // Reproduce the persisted state from the older Worker, not a second withdrawal.
        (await scenario.Db.Submissions.SingleAsync(row => row.Id == baseId)).Status = SubmissionStatuses.Published;
        await scenario.Db.SaveChangesAsync();

        var replay = await scenario.Service.WithdrawAsync(edition.Id, command);
        var repeated = await scenario.Service.WithdrawAsync(edition.Id, command);

        replay.Id.Should().Be(operation.Id);
        repeated.Id.Should().Be(operation.Id);
        replay.Status.Should().Be("committed");
        (await scenario.Db.Submissions.Where(row => row.Id == baseId || row.Id == partId).ToListAsync())
            .Should().OnlyContain(row => row.Status == SubmissionStatuses.Withdrawn);
        (await scenario.HeadAsync()).Should().Be(head);
        (await scenario.Db.PublicationOperations.CountAsync()).Should().Be(operations);
        (await scenario.Db.CatalogueSnapshots.CountAsync()).Should().Be(snapshots);
    }

    [Theory]
    [InlineData("new-revision")]
    [InlineData("live-edition")]
    [InlineData("new-publication")]
    public async Task WithdrawalReplay_Should_PreserveNewerAuthority(string change)
    {
        using var scenario = new PublicationScenario();
        var (baseId, _, editions) = await PublishSequenceAsync(scenario);
        var edition = editions.Single(item => item.Kind == "part");
        var command = new PublicationActionRequest(Guid.NewGuid(), edition.Version, "Withdraw the synthetic sequence");
        var operation = await scenario.Service.WithdrawAsync(edition.Id, command);
        await scenario.DeliverAsync(operation.Id);
        var submission = await scenario.Db.Submissions.SingleAsync(row => row.Id == baseId);
        submission.Status = SubmissionStatuses.Published;
        if (change == "new-revision")
        {
            var revision = new SubmissionRevision { TenantId = scenario.Tenant, SubmissionId = baseId, Number = 2 };
            scenario.Db.Revisions.Add(revision);
            submission.CurrentRevisionId = revision.Id;
        }
        else if (change == "live-edition")
        {
            scenario.Db.PublishedEditions.Add(new PublishedEdition
            {
                TenantId = scenario.Tenant, ChannelKey = "arke-worlds", WorldId = "separate-live-slot", Slot = "world",
                SubmissionId = baseId, RevisionId = submission.CurrentRevisionId, IsCurrent = true
            });
        }
        else submission.Status = SubmissionStatuses.Publishing;
        await scenario.Db.SaveChangesAsync();
        var expected = submission.Status;

        await scenario.Service.WithdrawAsync(edition.Id, command);

        (await scenario.Db.Submissions.SingleAsync(row => row.Id == baseId)).Status.Should().Be(expected);
    }

    [Fact]
    public async Task WithdrawalReplay_Should_RequireUnchangedCommandAndCurrentPermission_BeforeRepairing()
    {
        using var scenario = new PublicationScenario();
        var (baseId, _, editions) = await PublishSequenceAsync(scenario);
        var edition = editions.Single(item => item.Kind == "part");
        var command = new PublicationActionRequest(Guid.NewGuid(), edition.Version, "Withdraw the synthetic sequence");
        var operation = await scenario.Service.WithdrawAsync(edition.Id, command);
        await scenario.DeliverAsync(operation.Id);
        (await scenario.Db.Submissions.SingleAsync(row => row.Id == baseId)).Status = SubmissionStatuses.Published;
        await scenario.Db.SaveChangesAsync();

        await scenario.Service.Invoking(service => service.WithdrawAsync(edition.Id, command with { Reason = "Changed request" }))
            .Should().ThrowAsync<PublishingException>().Where(exception => exception.Status == 409);
        scenario.Allowed = false;
        await scenario.Service.Invoking(service => service.WithdrawAsync(edition.Id, command))
            .Should().ThrowAsync<PublishingException>().Where(exception => exception.Status == 403);

        (await scenario.Db.Submissions.SingleAsync(row => row.Id == baseId)).Status.Should().Be(SubmissionStatuses.Published);
    }

    private static async Task<(Guid BaseId, Guid PartId, IReadOnlyList<PublishedEditionResponse> Editions)> PublishSequenceAsync(PublicationScenario scenario)
    {
        await scenario.InitialiseAsync();
        var baseReceipt = await scenario.ApproveAsync("listening");
        var first = await scenario.Service.PublishAsync(baseReceipt.Id, await scenario.RequestAsync(baseReceipt.Id));
        await scenario.DeliverAsync(first.Id);
        var part = await scenario.ApproveAsync("part");
        var second = await scenario.Service.PublishAsync(part.Id, await scenario.RequestAsync(part.Id));
        await scenario.DeliverAsync(second.Id);
        return (baseReceipt.Id, part.Id, (await scenario.Service.CatalogueAsync("arke-worlds")).Editions);
    }
}
