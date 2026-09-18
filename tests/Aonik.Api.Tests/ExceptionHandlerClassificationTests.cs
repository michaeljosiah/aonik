using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text.Json;

using Aonik.Api.Configuration;
using Aonik.Infrastructure.Persistence;
using Aonik.Platform.Contracts.Services.Modules;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Entities.Modules;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Modules;

using FluentAssertions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aonik.Api.Tests;

/// <summary>
/// Codex P2-1 on Spec 097: a request the module gate denies is a 403 the pipeline produces on
/// purpose. It must not be recorded as an "Unhandled exception … status=500" nor mark the request
/// span as an error — that inflated exception telemetry and tripped 5xx alerts for traffic behaving
/// exactly as designed. The same holds for the dependency 409 and the permission 403. A genuine
/// unhandled exception keeps the existing error logging and span stamping, and every response body
/// is byte-for-byte what it was before the classification landed.
/// </summary>
public class ExceptionHandlerClassificationTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string UnhandledCategory = "Aonik.UnhandledException";
    private const string PolicyCategory = "Aonik.PolicyResponse";

    private readonly CustomWebApplicationFactory _factory;

    public ExceptionHandlerClassificationTests(CustomWebApplicationFactory factory) => _factory = factory;

    // ─── Middleware in isolation ────────────────────────────────────────────────

    [Fact]
    public async Task Handler_Should_LogInformationWithRealStatusAndNotStampSpanAsError_When_ModuleIsDisabled()
    {
        // Act
        var (context, logs, activity) = await RunPipelineAsync(new ModuleDisabledException(ModuleIds.Finance));

        // Assert — response unchanged
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        var body = await ReadBodyAsync(context);
        GetString(body, "code").Should().Be(ModuleErrorCodes.Disabled);
        GetString(body, "moduleId").Should().Be(ModuleIds.Finance);
        GetString(body, "error").Should().NotBeNullOrWhiteSpace();

        // Assert — telemetry classified as a policy answer, not a fault
        logs.Entries.Should().NotContain(e => e.Category == UnhandledCategory,
            "a disabled module is a routine 403, not an unhandled exception");
        var entry = logs.Entries.Should().ContainSingle(e => e.Category == PolicyCategory).Subject;
        entry.Level.Should().Be(LogLevel.Information);
        entry.Exception.Should().BeNull("no stack is attached to a policy answer");
        entry.Message.Should().Contain("status=403")
            .And.Contain($"code={ModuleErrorCodes.Disabled}")
            .And.Contain($"moduleId={ModuleIds.Finance}");

        activity.Status.Should().NotBe(ActivityStatusCode.Error);
        activity.GetTagItem("error").Should().BeNull();
        activity.GetTagItem("aonik.unhandled_exception").Should().BeNull();
        activity.GetTagItem("aonik.policy.code").Should().Be(ModuleErrorCodes.Disabled);
        activity.GetTagItem("aonik.policy.status_code").Should().Be(StatusCodes.Status403Forbidden);
        activity.GetTagItem("aonik.module_id").Should().Be(ModuleIds.Finance);
    }

    [Fact]
    public async Task Handler_Should_LogInformationWith409AndNotStampSpanAsError_When_ModuleDependencyIsViolated()
    {
        // Act
        var (context, logs, activity) = await RunPipelineAsync(new ModuleDependencyException(
            ModuleDependencyException.DependencyMissing, ModuleIds.Commerce, [ModuleIds.Finance]));

        // Assert — response unchanged
        context.Response.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        var body = await ReadBodyAsync(context);
        GetString(body, "code").Should().Be(ModuleErrorCodes.DependencyMissing);
        GetString(body, "moduleId").Should().Be(ModuleIds.Commerce);
        body.GetProperty("relatedModuleIds").EnumerateArray().Select(e => e.GetString()).Should().Equal(ModuleIds.Finance);

        // Assert — telemetry
        logs.Entries.Should().NotContain(e => e.Category == UnhandledCategory);
        var entry = logs.Entries.Should().ContainSingle(e => e.Category == PolicyCategory).Subject;
        entry.Level.Should().Be(LogLevel.Information);
        entry.Message.Should().Contain("status=409").And.Contain($"moduleId={ModuleIds.Commerce}");

        activity.Status.Should().NotBe(ActivityStatusCode.Error);
        activity.GetTagItem("error").Should().BeNull();
        activity.GetTagItem("aonik.policy.status_code").Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task Handler_Should_LogWarningWith403AndNotStampSpanAsError_When_PermissionIsDenied()
    {
        // Act
        var (context, logs, activity) = await RunPipelineAsync(new PermissionDeniedException("Ledger.Read"));

        // Assert — response unchanged: the pre-existing { error, permissionKey } shape, no code field
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        var body = await ReadBodyAsync(context);
        GetString(body, "permissionKey").Should().Be("Ledger.Read");
        GetString(body, "error").Should().Be("Permission Ledger.Read is required.");
        body.TryGetProperty("code", out _).Should().BeFalse("the permission body never carried a code and still must not");

        // Assert — telemetry
        logs.Entries.Should().NotContain(e => e.Category == UnhandledCategory);
        var entry = logs.Entries.Should().ContainSingle(e => e.Category == PolicyCategory).Subject;
        entry.Level.Should().Be(LogLevel.Warning, "repeated permission denials for one principal are a signal, not noise");
        entry.Message.Should().Contain("status=403").And.Contain("detail=Ledger.Read");

        activity.Status.Should().NotBe(ActivityStatusCode.Error);
        activity.GetTagItem("error").Should().BeNull();
        activity.GetTagItem("aonik.module_id").Should().BeNull();
    }

    [Fact]
    public async Task Handler_Should_KeepErrorLoggingAndSpanStamping_When_ExceptionIsGenuinelyUnhandled()
    {
        // Act — the control: nothing about a real fault may have changed.
        var (context, logs, activity) = await RunPipelineAsync(new InvalidOperationException("boom"));

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        var body = await ReadBodyAsync(context);
        GetString(body, "error").Should().Be("An internal error occurred.");

        var entry = logs.Entries.Should().ContainSingle(e => e.Category == UnhandledCategory).Subject;
        entry.Level.Should().Be(LogLevel.Error);
        entry.Exception.Should().BeOfType<InvalidOperationException>();
        entry.Message.Should().Contain("status=500");
        logs.Entries.Should().NotContain(e => e.Category == PolicyCategory);

        activity.Status.Should().Be(ActivityStatusCode.Error);
        activity.GetTagItem("error").Should().Be(true);
        activity.GetTagItem("aonik.unhandled_exception").Should().Be(true);
    }

    [Fact]
    public async Task Handler_Should_Return500WithTypedBodyAndKeepErrorLogging_When_ModuleProvisioningFails()
    {
        // Arrange — a contributor threw while Finance was being switched on; the toggle was not persisted.
        var inner = new InvalidOperationException("ledger bootstrap failed: connection reset");
        var thrown = new ModuleProvisioningException(ModuleIds.Finance, "FinanceTenantProvisioningContributor", inner);

        // Act
        var (context, logs, activity) = await RunPipelineAsync(thrown);

        // Assert — typed body, honest status, no inner-exception text outside Development
        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        var body = await ReadBodyAsync(context);
        GetString(body, "code").Should().Be(ModuleErrorCodes.ProvisioningFailed);
        GetString(body, "moduleId").Should().Be(ModuleIds.Finance);
        GetString(body, "contributor").Should().Be("FinanceTenantProvisioningContributor");
        GetString(body, "error").Should().Contain(ModuleIds.Finance)
            .And.Contain("FinanceTenantProvisioningContributor")
            .And.Contain("No module settings were changed")
            .And.NotContain("connection reset", "the inner exception's message must stay server-side outside Development");

        // Assert — a contributor throwing is a genuine fault: logged as unhandled with the inner chain,
        // span stamped as an error. It is NOT a policy response.
        var entry = logs.Entries.Should().ContainSingle(e => e.Category == UnhandledCategory).Subject;
        entry.Level.Should().Be(LogLevel.Error);
        entry.Exception.Should().BeOfType<ModuleProvisioningException>();
        entry.Message.Should().Contain("status=500");
        logs.Entries.Should().NotContain(e => e.Category == PolicyCategory);

        activity.Status.Should().Be(ActivityStatusCode.Error);
        activity.GetTagItem("error").Should().Be(true);
    }

    // ─── Through the real host ─────────────────────────────────────────────────

    [Fact]
    public async Task ModuleGateDenial_Should_NotProduceAnUnhandledExceptionLogEntry_When_ServedByTheRealPipeline()
    {
        // Arrange — the same host and pipeline as production, with a capturing logger attached.
        var logs = new CapturingLoggerProvider();
        using var host = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureLogging(logging => logging.AddProvider(logs)));

        var tenantId = Guid.NewGuid();
        await SeedActiveTenantWithFinanceOffAsync(host.Services, tenantId);

        var client = host.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.TenantIdHeader, tenantId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "Operations");

        // Act
        var response = await client.GetAsync("/ledger");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        GetString(json, "code").Should().Be(ModuleErrorCodes.Disabled);

        logs.Entries.Should().NotContain(e => e.Category == UnhandledCategory && e.Message.Contains("/ledger"),
            "a module-gate 403 must never surface as an unhandled exception");
        logs.Entries.Should().Contain(e =>
            e.Category == PolicyCategory
            && e.Level == LogLevel.Information
            && e.Message.Contains("/ledger")
            && e.Message.Contains($"moduleId={ModuleIds.Finance}"));
    }

    // ─── Helpers ────────────────────────────────────────────────────────────────

    private static async Task<(DefaultHttpContext Context, CapturingLoggerProvider Logs, Activity Activity)> RunPipelineAsync(Exception toThrow)
    {
        var logs = new CapturingLoggerProvider();
        var services = new ServiceCollection()
            .AddLogging(logging => logging.AddProvider(logs))
            .BuildServiceProvider();

        var app = new ApplicationBuilder(services);
        app.UseAonikExceptionHandler(new TestHostEnvironment());
        app.Run(_ => throw toThrow);
        var pipeline = app.Build();

        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/ledger";
        context.Response.Body = new MemoryStream();

        using var source = new ActivitySource("Aonik.Api.Tests.ExceptionHandler");
        using var listener = new ActivityListener
        {
            ShouldListenTo = candidate => candidate.Name == source.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);

        var activity = source.StartActivity("request");
        activity.Should().NotBeNull("the listener must sample the test span so tag assertions are meaningful");

        try
        {
            await pipeline(context);
        }
        finally
        {
            activity!.Stop();
        }

        return (context, logs, activity);
    }

    private static async Task<JsonElement> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, leaveOpen: true);
        var json = await reader.ReadToEndAsync();
        json.Should().NotBeNullOrWhiteSpace();
        return JsonDocument.Parse(json).RootElement;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
            }
        }

        return null;
    }

    private static async Task SeedActiveTenantWithFinanceOffAsync(IServiceProvider services, Guid tenantId)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.TenantId = tenantId;

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Exception Classification Test Tenant",
            Environment = "Testing",
            DefaultCurrency = "GBP",
            SupportedCountriesJson = "[]",
            Status = TenantStatus.Active,
        });
        db.TenantModules.Add(new TenantModule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ModuleId = ModuleIds.Finance,
            IsEnabled = false,
            Source = TenantModuleSource.Explicit,
            Reason = "exception classification test",
        });
        await db.SaveChangesAsync();
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "Aonik.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

/// <summary>A captured log line: category, level, rendered message and the attached exception, if any.</summary>
public sealed record CapturedLogEntry(string Category, LogLevel Level, string Message, Exception? Exception);

/// <summary>
/// The smallest possible in-memory logger provider: every line from every category lands in
/// <see cref="Entries"/>, so a test can assert on categories and levels without a logging framework.
/// </summary>
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    public ConcurrentQueue<CapturedLogEntry> Entries { get; } = new();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, Entries);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(string category, ConcurrentQueue<CapturedLogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => entries.Enqueue(new CapturedLogEntry(category, logLevel, formatter(state, exception), exception));
    }
}
