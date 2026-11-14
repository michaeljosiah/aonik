namespace Aonik.SharedKernel.Abstractions.Tasks;

/// <summary>The existing Platform/Quartz recurrence calculator exposed to module-owned schedule validation.</summary>
public interface IRecurrenceScheduleCalculator
{
    bool IsValidCron(string? cronExpression);
    DateTime? GetNextOccurrenceUtc(string cronExpression, string? timezone, DateTime afterUtc);
}
