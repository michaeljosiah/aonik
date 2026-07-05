using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Entities;

namespace Aonik.PersonalFinance.Services;

/// <summary>Maps the anemic <see cref="CompassPlan"/> entity to its response DTO.</summary>
internal static class CompassPlanMapper
{
    public static CompassPlanResponse Map(CompassPlan plan) =>
        new(
            PlanId: plan.Id,
            GoalId: plan.GoalId,
            UserId: plan.UserId,
            Version: plan.Version,
            Status: plan.Status,
            PlanJson: plan.PlanJson,
            HorizonStartUtc: plan.HorizonStartUtc,
            HorizonEndUtc: plan.HorizonEndUtc,
            SnapshotId: plan.SnapshotId,
            AiRunId: plan.AiRunId,
            SupersededById: plan.SupersededById,
            CreatedAt: plan.CreatedAt);
}
