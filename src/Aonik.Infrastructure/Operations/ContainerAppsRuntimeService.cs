using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aonik.Platform.Contracts.Api.Observability;
using Aonik.Platform.Contracts.Services.Operations;
using Aonik.SharedKernel.Abstractions;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aonik.Infrastructure.Operations;

internal sealed class ContainerAppsRuntimeService : IRuntimeOperationsService
{
    private const string ApiVersion = "2024-03-01";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly ContainerAppsRuntimeOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IClock _clock;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<ContainerAppsRuntimeService> _logger;
    private readonly TokenCredential _credential;

    public ContainerAppsRuntimeService(
        IOptions<ContainerAppsRuntimeOptions> options,
        IHttpClientFactory httpClientFactory,
        IClock clock,
        IAuditLogWriter auditLogWriter,
        ICurrentUserProvider currentUserProvider,
        ILogger<ContainerAppsRuntimeService> logger)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _clock = clock;
        _auditLogWriter = auditLogWriter;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
        _credential = new DefaultAzureCredential();
    }

    public async Task<IReadOnlyList<RuntimeServiceStatus>> ListRuntimeServicesAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured())
        {
            return BuildDisabledStatuses("Azure Container Apps runtime control is not configured.");
        }

        var services = GetManagedServices();
        var tasks = services.Select(service => GetServiceStatusSafeAsync(service, cancellationToken));
        return [.. await Task.WhenAll(tasks)];
    }

    public async Task<RuntimeServiceActionResponse> StartServiceAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        var service = ResolveService(serviceName);
        if (service is null)
        {
            return new RuntimeServiceActionResponse(serviceName, "start", false, "Runtime service not found.", null);
        }

        if (!IsConfigured())
        {
            return new RuntimeServiceActionResponse(service.Name, "start", false, "Azure Container Apps runtime control is not configured.", CreateDisabledStatus(service));
        }

        if (!string.Equals(_options.EnvironmentName, "dev", StringComparison.OrdinalIgnoreCase))
        {
            var status = await GetServiceStatusSafeAsync(service, cancellationToken);
            return new RuntimeServiceActionResponse(service.Name, "start", false, "Runtime start is restricted to the dev environment.", status);
        }

        var current = await GetServiceStatusSafeAsync(service, cancellationToken);
        if (!current.Exists)
        {
            return new RuntimeServiceActionResponse(service.Name, "start", false, current.Message ?? "Runtime service was not found in Azure.", current);
        }

        if (!current.IsStartable)
        {
            return new RuntimeServiceActionResponse(service.Name, "start", false, current.Message ?? "This runtime service is not startable from the admin surface.", current);
        }

        if (current.IsRunning)
        {
            return new RuntimeServiceActionResponse(service.Name, "start", true, $"{service.DisplayName} is already running.", current);
        }

        var app = await GetContainerAppAsync(service.ContainerAppName, cancellationToken);
        if (app is null)
        {
            return new RuntimeServiceActionResponse(service.Name, "start", false, "Container App metadata could not be loaded.", current);
        }

        var minReplicas = Math.Max(current.MinReplicas ?? 0, service.DefaultStartMinReplicas);
        app.Properties.Template ??= new ArmTemplate();
        app.Properties.Template.Scale ??= new ArmScale();
        app.Properties.Template.Scale.MinReplicas = minReplicas;

        await UpdateContainerAppAsync(app, cancellationToken);
        await WriteAuditLogAsync(service, minReplicas, cancellationToken);

        var refreshed = await GetServiceStatusSafeAsync(service, cancellationToken);
        return new RuntimeServiceActionResponse(
            service.Name,
            "start",
            true,
            $"Requested start for {service.DisplayName}. Minimum replicas set to {minReplicas}.",
            refreshed);
    }

    private bool IsConfigured()
    {
        return _options.Enabled
            && !string.IsNullOrWhiteSpace(_options.SubscriptionId)
            && !string.IsNullOrWhiteSpace(_options.ResourceGroupName)
            && !string.IsNullOrWhiteSpace(_options.EnvironmentName)
            && !string.IsNullOrWhiteSpace(_options.WorkloadName);
    }

    private IReadOnlyList<RuntimeServiceStatus> BuildDisabledStatuses(string message)
    {
        return GetManagedServices()
            .Select(service => CreateDisabledStatus(service with { DisabledMessage = message }))
            .ToList();
    }

    private IReadOnlyList<ManagedRuntimeService> GetManagedServices()
    {
        var prefix = $"{_options.WorkloadName}-{_options.EnvironmentName}".ToLowerInvariant();

        return
        [
            new ManagedRuntimeService("api", "API", "service", $"{prefix}-api", 1, false, null),
            new ManagedRuntimeService("worker", "Background Worker", "worker", $"{prefix}-worker", 1, true, null),
            new ManagedRuntimeService("admin-ui", "Admin UI", "service", $"{prefix}-adminui", 1, false, null),
            new ManagedRuntimeService("qdrant", "Qdrant", "datastore", $"{prefix}-qdrant", 1, true, null),
        ];
    }

    private ManagedRuntimeService? ResolveService(string serviceName)
    {
        return GetManagedServices().FirstOrDefault(service => string.Equals(service.Name, serviceName, StringComparison.OrdinalIgnoreCase));
    }

    private RuntimeServiceStatus CreateDisabledStatus(ManagedRuntimeService service)
    {
        return new RuntimeServiceStatus(
            service.Name,
            service.DisplayName,
            service.ServiceType,
            "unavailable",
            "Unknown",
            false,
            service.AllowStart,
            false,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            service.DisabledMessage ?? "Azure Container Apps runtime control is not configured.");
    }

    private async Task<RuntimeServiceStatus> GetServiceStatusSafeAsync(ManagedRuntimeService service, CancellationToken cancellationToken)
    {
        try
        {
            if (!IsConfigured())
            {
                return CreateDisabledStatus(service);
            }

            return await GetServiceStatusAsync(service, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load runtime status for container app {ContainerAppName}.", service.ContainerAppName);
            return new RuntimeServiceStatus(
                service.Name,
                service.DisplayName,
                service.ServiceType,
                "unknown",
                "Unknown",
                false,
                service.AllowStart,
                false,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                "Failed to load runtime state from Azure.");
        }
    }

    private async Task<RuntimeServiceStatus> GetServiceStatusAsync(ManagedRuntimeService service, CancellationToken cancellationToken)
    {
        var app = await GetContainerAppAsync(service.ContainerAppName, cancellationToken);
        if (app is null)
        {
            return new RuntimeServiceStatus(
                service.Name,
                service.DisplayName,
                service.ServiceType,
                "missing",
                "Missing",
                false,
                service.AllowStart,
                false,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                "Container App resource was not found.");
        }

        var revisions = await ListRevisionsAsync(service.ContainerAppName, cancellationToken);
        var activeRevision = revisions
            .Where(revision => revision.Properties?.Active == true)
            .OrderByDescending(revision => revision.Properties?.CreatedTime)
            .FirstOrDefault();

        var replicas = activeRevision?.Properties?.Replicas;
        var minReplicas = app.Properties.Template?.Scale?.MinReplicas;
        var maxReplicas = app.Properties.Template?.Scale?.MaxReplicas;
        var revisionRunningState = activeRevision?.Properties?.RunningState;
        var revisionHealthState = activeRevision?.Properties?.HealthState;
        var runtimeState = DetermineRuntimeState(app.Properties.ProvisioningState, revisionRunningState, replicas, minReplicas);
        var isRunning = string.Equals(runtimeState, "running", StringComparison.OrdinalIgnoreCase)
            || string.Equals(runtimeState, "processing", StringComparison.OrdinalIgnoreCase);

        var message = runtimeState switch
        {
            "scaled-to-zero" => $"{service.DisplayName} is deployed but currently has zero active replicas.",
            "degraded" => $"{service.DisplayName} revision is degraded.",
            "missing" => "Container App resource was not found.",
            _ => null,
        };

        return new RuntimeServiceStatus(
            service.Name,
            service.DisplayName,
            service.ServiceType,
            runtimeState,
            app.Properties.ProvisioningState ?? "Unknown",
            true,
            service.AllowStart,
            isRunning,
            replicas,
            minReplicas,
            maxReplicas,
            activeRevision?.Properties?.LastActiveTime,
            revisionHealthState,
            revisionRunningState,
            activeRevision?.Name ?? app.Properties.LatestRevisionName,
            message);
    }

    private static string DetermineRuntimeState(string? provisioningState, string? revisionRunningState, int? replicas, int? minReplicas)
    {
        if (string.Equals(provisioningState, "Failed", StringComparison.OrdinalIgnoreCase))
        {
            return "failed";
        }

        if (string.Equals(revisionRunningState, "Degraded", StringComparison.OrdinalIgnoreCase)
            || string.Equals(revisionRunningState, "Failed", StringComparison.OrdinalIgnoreCase))
        {
            return "degraded";
        }

        if (replicas.GetValueOrDefault() > 0)
        {
            return string.Equals(revisionRunningState, "Processing", StringComparison.OrdinalIgnoreCase)
                ? "processing"
                : "running";
        }

        if (minReplicas.GetValueOrDefault() == 0)
        {
            return "scaled-to-zero";
        }

        if (string.Equals(revisionRunningState, "Stopped", StringComparison.OrdinalIgnoreCase))
        {
            return "stopped";
        }

        return "unknown";
    }

    private async Task<ContainerAppArmResource?> GetContainerAppAsync(string containerAppName, CancellationToken cancellationToken)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, BuildContainerAppRelativeUri(containerAppName), cancellationToken);
        using var response = await SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ContainerAppArmResource>(JsonOptions, cancellationToken);
    }

    private async Task<IReadOnlyList<ContainerAppRevisionArmResource>> ListRevisionsAsync(string containerAppName, CancellationToken cancellationToken)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, BuildRevisionsRelativeUri(containerAppName), cancellationToken);
        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<RevisionListResponse>(JsonOptions, cancellationToken);
        return payload?.Value ?? [];
    }

    private async Task UpdateContainerAppAsync(ContainerAppArmResource app, CancellationToken cancellationToken)
    {
        var patch = new ContainerAppPatchRequest
        {
            Location = app.Location,
            Tags = app.Tags,
            Identity = app.Identity,
            Properties = new ContainerAppPatchProperties
            {
                ManagedEnvironmentId = app.Properties.ManagedEnvironmentId,
                WorkloadProfileName = app.Properties.WorkloadProfileName,
                Configuration = app.Properties.Configuration,
                Template = app.Properties.Template,
            },
        };

        using var request = await CreateRequestAsync(new HttpMethod("PATCH"), BuildContainerAppRelativeUri(app.Name), cancellationToken);
        request.Content = JsonContent.Create(patch, options: JsonOptions);

        using var response = await SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string relativeUri, CancellationToken cancellationToken)
    {
        var token = await _credential.GetTokenAsync(
            new TokenRequestContext(["https://management.azure.com/.default"]),
            cancellationToken);

        var request = new HttpRequestMessage(method, relativeUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("AzureResourceManager");
        return await client.SendAsync(request, cancellationToken);
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogWarning(
            "Azure Resource Manager request failed with status {StatusCode}: {Body}",
            (int)response.StatusCode,
            errorBody);

        response.EnsureSuccessStatusCode();
    }

    private string BuildContainerAppRelativeUri(string containerAppName)
    {
        return $"subscriptions/{_options.SubscriptionId}/resourceGroups/{_options.ResourceGroupName}/providers/Microsoft.App/containerApps/{containerAppName}?api-version={ApiVersion}";
    }

    private string BuildRevisionsRelativeUri(string containerAppName)
    {
        return $"subscriptions/{_options.SubscriptionId}/resourceGroups/{_options.ResourceGroupName}/providers/Microsoft.App/containerApps/{containerAppName}/revisions?api-version={ApiVersion}";
    }

    private async Task WriteAuditLogAsync(ManagedRuntimeService service, int requestedMinReplicas, CancellationToken cancellationToken)
    {
        try
        {
            var detailsJson = JsonSerializer.Serialize(new
            {
                service = service.Name,
                containerAppName = service.ContainerAppName,
                action = "start",
                requestedMinReplicas,
                environment = _options.EnvironmentName,
                at = _clock.UtcNow,
            });

            await _auditLogWriter.LogAsync(
                AuditEventNames.RuntimeServiceStartRequested,
                "RuntimeService",
                Guid.Empty,
                Guid.Empty,
                _currentUserProvider.GetCurrentUserId(),
                service.Name,
                detailsJson,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write audit log for runtime start request on {ServiceName}.", service.Name);
        }
    }

    private sealed record ManagedRuntimeService(
        string Name,
        string DisplayName,
        string ServiceType,
        string ContainerAppName,
        int DefaultStartMinReplicas,
        bool AllowStart,
        string? DisabledMessage);

    private sealed class RevisionListResponse
    {
        public List<ContainerAppRevisionArmResource> Value { get; set; } = [];
    }

    private sealed class ContainerAppPatchRequest
    {
        public string Location { get; set; } = string.Empty;
        public Dictionary<string, string>? Tags { get; set; }
        public ArmManagedIdentity? Identity { get; set; }
        public ContainerAppPatchProperties Properties { get; set; } = new();
    }

    private sealed class ContainerAppPatchProperties
    {
        public string? ManagedEnvironmentId { get; set; }
        public string? WorkloadProfileName { get; set; }
        public ArmConfiguration? Configuration { get; set; }
        public ArmTemplate? Template { get; set; }
    }

    private sealed class ContainerAppArmResource
    {
        public string Name { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public Dictionary<string, string>? Tags { get; set; }
        public ArmManagedIdentity? Identity { get; set; }
        public ContainerAppArmProperties Properties { get; set; } = new();
    }

    private sealed class ContainerAppArmProperties
    {
        public string? ProvisioningState { get; set; }
        public string? ManagedEnvironmentId { get; set; }
        public string? WorkloadProfileName { get; set; }
        public string? LatestRevisionName { get; set; }
        public ArmConfiguration? Configuration { get; set; }
        public ArmTemplate? Template { get; set; }
    }

    private sealed class ContainerAppRevisionArmResource
    {
        public string Name { get; set; } = string.Empty;
        public ContainerAppRevisionProperties? Properties { get; set; }
    }

    private sealed class ContainerAppRevisionProperties
    {
        public bool Active { get; set; }
        public int? Replicas { get; set; }
        public string? RunningState { get; set; }
        public string? HealthState { get; set; }
        public DateTime? LastActiveTime { get; set; }
        public DateTime? CreatedTime { get; set; }
    }

    private sealed class ArmManagedIdentity
    {
        public string? Type { get; set; }
        public Dictionary<string, JsonElement>? UserAssignedIdentities { get; set; }
    }

    private sealed class ArmConfiguration
    {
        public string? ActiveRevisionsMode { get; set; }
        public ArmIngress? Ingress { get; set; }
        public List<ArmRegistryCredential>? Registries { get; set; }
        public List<ArmSecret>? Secrets { get; set; }
        public int? MaxInactiveRevisions { get; set; }
    }

    private sealed class ArmIngress
    {
        public bool? External { get; set; }
        public int? TargetPort { get; set; }
        public string? Transport { get; set; }
        public int? ExposedPort { get; set; }
        public bool? AllowInsecure { get; set; }
    }

    private sealed class ArmRegistryCredential
    {
        public string? Server { get; set; }
        public string? Identity { get; set; }
        public string? Username { get; set; }
        public string? PasswordSecretRef { get; set; }
    }

    private sealed class ArmSecret
    {
        public string? Name { get; set; }
        public string? Value { get; set; }
        public string? Identity { get; set; }
        public string? KeyVaultUrl { get; set; }
    }

    private sealed class ArmTemplate
    {
        public List<ArmContainer>? Containers { get; set; }
        public List<ArmInitContainer>? InitContainers { get; set; }
        public ArmScale? Scale { get; set; }
        public List<ArmVolume>? Volumes { get; set; }
    }

    private sealed class ArmContainer
    {
        public string? Name { get; set; }
        public string? Image { get; set; }
        public List<ArmEnvironmentVariable>? Env { get; set; }
        public ArmContainerResources? Resources { get; set; }
        public List<ArmVolumeMount>? VolumeMounts { get; set; }
        public List<ArmProbe>? Probes { get; set; }
    }

    private sealed class ArmInitContainer
    {
        public string? Name { get; set; }
        public string? Image { get; set; }
        public ArmContainerResources? Resources { get; set; }
    }

    private sealed class ArmEnvironmentVariable
    {
        public string? Name { get; set; }
        public string? Value { get; set; }
        public string? SecretRef { get; set; }
    }

    private sealed class ArmContainerResources
    {
        public double? Cpu { get; set; }
        public string? Memory { get; set; }
    }

    private sealed class ArmVolumeMount
    {
        public string? VolumeName { get; set; }
        public string? MountPath { get; set; }
    }

    private sealed class ArmProbe
    {
        public string? Type { get; set; }
        public ArmHttpGet? HttpGet { get; set; }
        public int? InitialDelaySeconds { get; set; }
        public int? PeriodSeconds { get; set; }
        public int? TimeoutSeconds { get; set; }
        public int? FailureThreshold { get; set; }
    }

    private sealed class ArmHttpGet
    {
        public string? Path { get; set; }
        public int? Port { get; set; }
    }

    private sealed class ArmScale
    {
        public int? MinReplicas { get; set; }
        public int? MaxReplicas { get; set; }
        public List<ArmScaleRule>? Rules { get; set; }
    }

    private sealed class ArmScaleRule
    {
        public string? Name { get; set; }
        public JsonElement? Http { get; set; }
        public JsonElement? Tcp { get; set; }
        public JsonElement? Custom { get; set; }
        public JsonElement? AzureQueue { get; set; }
    }

    private sealed class ArmVolume
    {
        public string? Name { get; set; }
        public string? StorageType { get; set; }
        public string? StorageName { get; set; }
    }
}
