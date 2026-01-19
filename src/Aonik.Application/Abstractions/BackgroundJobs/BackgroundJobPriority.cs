namespace Aonik.Application.Abstractions.BackgroundJobs;

/// <summary>
/// Represents the priority level for a background job.
/// Lower values indicate lower priority.
/// </summary>
public enum BackgroundJobPriority : byte
{
    /// <summary>
    /// Low priority.
    /// </summary>
    Low = 5,

    /// <summary>
    /// Below normal priority.
    /// </summary>
    BelowNormal = 10,

    /// <summary>
    /// Normal priority (default).
    /// </summary>
    Normal = 15,

    /// <summary>
    /// Above normal priority.
    /// </summary>
    AboveNormal = 20,

    /// <summary>
    /// High priority.
    /// </summary>
    High = 25
}
