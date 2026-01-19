using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Aonik.Application.Abstractions.BackgroundJobs;

namespace Aonik.Application.Services.BackgroundJobs.Samples;

/// <summary>
/// Sample background job that generates monthly financial reports.
/// This demonstrates how to implement a synchronous background job.
/// </summary>
public class GenerateMonthlyReportJob : IBackgroundJob<GenerateMonthlyReportArgs>
{
    private readonly IReportGenerator _reportGenerator;
    private readonly ILogger<GenerateMonthlyReportJob> _logger;

    public GenerateMonthlyReportJob(
        IReportGenerator reportGenerator,
        ILogger<GenerateMonthlyReportJob> logger)
    {
        _reportGenerator = reportGenerator;
        _logger = logger;
    }

    public void Execute(GenerateMonthlyReportArgs args)
    {
        _logger.LogInformation(
            "Generating {ReportType} report for tenant {TenantId} for period {Year}-{Month}",
            args.ReportType,
            args.TenantId,
            args.Year,
            args.Month);

        var report = _reportGenerator.GenerateReport(
            args.TenantId,
            args.ReportType,
            args.Year,
            args.Month);

        _reportGenerator.SaveReport(args.TenantId, report);

        _logger.LogInformation(
            "Successfully generated {ReportType} report for tenant {TenantId}",
            args.ReportType,
            args.TenantId);
    }
}

/// <summary>
/// Arguments for the <see cref="GenerateMonthlyReportJob"/>.
/// </summary>
public class GenerateMonthlyReportArgs
{
    public Guid TenantId { get; set; }
    public string ReportType { get; set; } = "FinancialSummary";
    public int Year { get; set; }
    public int Month { get; set; }
    public Guid? RequestedBy { get; set; }
}

/// <summary>
/// Interface for report generation - demonstration purposes only.
/// </summary>
public interface IReportGenerator
{
    ReportData GenerateReport(Guid tenantId, string reportType, int year, int month);
    void SaveReport(Guid tenantId, ReportData report);
}

/// <summary>
/// Represents report data.
/// </summary>
public class ReportData
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string ReportType { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Month { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string Format { get; set; } = "PDF";
}
