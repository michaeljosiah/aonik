using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Services.Workflows;
using Aonik.Agents.Workflows.Graph;
using Aonik.Finance.Entities.Billing;
using Aonik.Finance.Persistence;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Settings;

namespace Aonik.Api.Tests;

public partial class WorkflowEndpointsTests
{
    [Fact]
    public async Task InvoiceApproval_Should_RequireTheAssignedRoleAndSeparateReviewer_AndConsumeOnce()
    {
        Guid tenantId = Guid.NewGuid();
        Guid invoiceId = Guid.NewGuid();
        var author = ReviewerOptions().WithPermissions("Workflow.Write", "Workflow.Publish");
        var reviewer = ReviewerOptions();
        using HttpClient authorClient = await AuthenticatedAsync(author);
        using HttpClient reviewerClient = await AuthenticatedAsync(reviewer);
        using HttpClient otherClient = await AuthenticatedAsync(ReviewerOptions());
        var role = new Role { TenantId = tenantId, Name = "Invoice reviewer" };
        using (IServiceScope scope = WorkflowScope())
        {
            IServiceProvider services = scope.ServiceProvider;
            await services.GetRequiredService<ITenantSettingStore>().SetTenantValueAsync(WorkflowSettingNames.ExecutionEnabled, "true", tenantId);
            PlatformDbContext platform = services.GetRequiredService<PlatformDbContext>();
            platform.Roles.Add(role);
            platform.UserRoles.AddRange(new UserRole { UserId = author.UserId, RoleId = role.Id },
                new UserRole { UserId = reviewer.UserId, RoleId = role.Id });
            await platform.SaveChangesAsync();
            FinanceDbContext finance = services.GetRequiredService<FinanceDbContext>();
            finance.Invoices.Add(new Invoice { Id = invoiceId, TenantId = tenantId, CustomerAccountId = Guid.NewGuid(),
                CreatedBy = author.UserId, Currency = "GBP", Total = 42.25m, Status = "Draft", ProvenanceJson = "{}" });
            await finance.SaveChangesAsync();
        }

        WorkflowSaveRequest draft = Draft();
        string action = JsonSerializer.Serialize(new WorkflowToolParameters("finance.invoices.issue", 1,
            new Dictionary<string, WorkflowInputBinding> { ["invoiceId"] = new(Fact: "invoice.id") }, new(role.Id)), WorkflowDefinitionCodec.JsonOptions);
        draft = draft with { Nodes = [draft.Nodes[0], new("issue", "Tool", "Issue invoice", "", "", 1, 0, action), draft.Nodes[1]],
            Edges = [new("start", "issue", 0, ""), new("issue", "end", 0, "")] };
        HttpResponseMessage saved = await authorClient.PostAsJsonAsync("/ai/workflows", draft);
        saved.StatusCode.Should().Be(HttpStatusCode.Created);
        WorkflowGraphResponse graph = (await saved.Content.ReadFromJsonAsync<WorkflowGraphResponse>())!;
        WorkflowValidation validation = (await (await authorClient.PostAsJsonAsync($"/ai/workflows/{draft.Slug}/validate", new { }))
            .Content.ReadFromJsonAsync<WorkflowValidation>())!;
        HttpResponseMessage publication = await authorClient.PostAsJsonAsync($"/ai/workflows/{draft.Slug}/publication-requests",
            new WorkflowGovernanceCommand(draft.Slug, graph.ExpectedVersion, validation.DefinitionHash, null));
        publication.StatusCode.Should().Be(HttpStatusCode.Accepted);
        WorkflowGovernanceRequestResult queued = (await publication.Content.ReadFromJsonAsync<WorkflowGovernanceRequestResult>())!;
        (await authorClient.PostAsJsonAsync($"/ai/tool-approvals/{queued.ApprovalRequestId}/decide", new { decision = "Approve" })).StatusCode.Should().Be(HttpStatusCode.OK);
        Guid versionId = (await authorClient.GetFromJsonAsync<WorkflowGraphResponse>($"/ai/workflows/{draft.Slug}"))!.PublishedVersionId!.Value;
        HttpResponseMessage started = await authorClient.PostAsJsonAsync($"/ai/workflows/{draft.Slug}/runs",
            new StartWorkflowRunRequest(versionId, "Invoice", invoiceId, "invoice-approval-api"));
        started.StatusCode.Should().Be(HttpStatusCode.Accepted);
        Guid runId = (await started.Content.ReadFromJsonAsync<WorkflowRunDetail>())!.Id;
        using (IServiceScope worker = WorkflowScope())
        {
            GraphWorkflowContinuation waiting = await worker.ServiceProvider.GetRequiredService<IGraphWorkflowRunner>().ContinueAsync(runId, 0);
            waiting.Status.Should().Be("WaitingForApproval", waiting.ErrorCode);
        }

        string runPath = $"/ai/workflow-runs/{runId}";
        WorkflowStepApproval approval = await ReadApprovalAsync(reviewerClient);
        approval.CanDecide.Should().BeTrue();
        approval.RiskTier.Should().Be("Medium");
        approval.Preview!.Title.Should().Be("Issue invoice for 42.25 GBP");
        approval.Preview.Before!.Value.GetProperty("invoiceId").GetGuid().Should().Be(invoiceId);
        approval.Preview.Before.Value.GetProperty("status").GetString().Should().Be("Draft");
        approval.Preview.After!.Value.GetProperty("invoiceId").GetGuid().Should().Be(invoiceId);
        approval.Preview.After.Value.GetProperty("status").GetString().Should().Be("Issued");
        (await ReadApprovalAsync(authorClient)).CanDecide.Should().BeFalse();
        (await ReadApprovalAsync(otherClient)).CanDecide.Should().BeFalse();
        string decisionPath = $"/ai/tool-approvals/{approval.Id}/decide";
        (await authorClient.PostAsJsonAsync(decisionPath, new { decision = "Approve" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await otherClient.PostAsJsonAsync(decisionPath, new { decision = "Approve" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await reviewerClient.PostAsJsonAsync(decisionPath, new { decision = "Approve" })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await reviewerClient.PostAsJsonAsync(decisionPath, new { decision = "Approve" })).StatusCode.Should().Be(HttpStatusCode.Conflict);

        using IServiceScope finished = WorkflowScope();
        GraphWorkflowContinuation completed = await finished.ServiceProvider.GetRequiredService<IGraphWorkflowRunner>().ContinueAsync(runId, 0);
        completed.Status.Should().Be("Completed", completed.ErrorCode);
        Invoice invoice = await finished.ServiceProvider.GetRequiredService<FinanceDbContext>().Invoices.AsNoTracking().SingleAsync(item => item.Id == invoiceId);
        invoice.Status.Should().Be("Issued");
        invoice.IssueApprovedByUserId.Should().Be(reviewer.UserId);
        invoice.IssueApprovalRequestId.Should().Be(approval.Id);

        TestAuthOptions ReviewerOptions() => TestAuthOptions.Create().WithTenant(tenantId).WithRoles("PersonalUser")
            .WithPermissions("Workflow.Read", "Workflow.Run", "Invoice.Read", "Invoice.Issue");

        async Task<HttpClient> AuthenticatedAsync(TestAuthOptions options)
        {
            HttpClient client = await factory.CreateAuthenticatedClientAsync(options);
            client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
            return client;
        }

        IServiceScope WorkflowScope()
        {
            IServiceScope scope = factory.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenantId;
            return scope;
        }

        async Task<WorkflowStepApproval> ReadApprovalAsync(HttpClient client)
        {
            WorkflowRunDetail run = (await client.GetFromJsonAsync<WorkflowRunDetail>(runPath))!;
            return run.Steps.Single(step => step.NodeKey == "issue").Approval!;
        }
    }
}
