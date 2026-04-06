using Aonik.Infrastructure;
using Aonik.Infrastructure.BackgroundJobs;
using Aonik.Infrastructure.VectorStore.Contracts;
using Aonik.Platform;
using Aonik.Finance;
using Aonik.Ai;
using Aonik.Agents;
using Aonik.Agents.Framework;
using Aonik.Worker.Jobs;
using Quartz;

var builder = Host.CreateApplicationBuilder(args);

// Add Aspire service defaults (telemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Register infrastructure services (database, background jobs, etc.)
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);

// Register domain modules
builder.Services.AddPlatformModule(builder.Configuration);
builder.Services.AddFinanceModule(builder.Configuration);
builder.Services.AddAiModule(builder.Configuration);
builder.Services.AddAgentsModule(builder.Configuration);

// Register RAG context provider adapters and RagContextProvider
// This is deferred until after Infrastructure is registered to avoid circular dependencies
builder.Services.AddScoped<Aonik.Agents.Framework.IVectorStore>(sp =>
{
    var infrastructureVectorStore = sp.GetRequiredService<Aonik.Infrastructure.VectorStore.Contracts.IVectorStore>();
    return new Aonik.Worker.VectorStoreAdapterImpl(infrastructureVectorStore);
});

builder.Services.AddScoped<Aonik.Agents.Framework.IEmbeddingService>(sp =>
{
    var infrastructureEmbeddingService = sp.GetRequiredService<Aonik.Infrastructure.VectorStore.Contracts.IEmbeddingService>();
    return new Aonik.Worker.EmbeddingServiceAdapterImpl(infrastructureEmbeddingService);
});

builder.Services.AddScoped<Aonik.Agents.Framework.RagContextProvider>();

// Bind scheduled job options
builder.Services.Configure<ScheduledJobOptions>(
    builder.Configuration.GetSection("Quartz:ScheduledJobs"));

// Read Quartz persistence options
var quartzPersistenceEnabled = builder.Configuration.GetValue<bool>("Quartz:Persistence:Enabled");
var quartzTablePrefix = builder.Configuration.GetValue<string>("Quartz:Persistence:TablePrefix") ?? "QRTZ_";
var quartzSchedulerName = builder.Configuration.GetValue<string>("Quartz:Persistence:SchedulerName") ?? "AonikScheduler";
var quartzMisfireThresholdSeconds = builder.Configuration.GetValue<int>("Quartz:Persistence:MisfireThresholdSeconds", 60);
var quartzClustered = builder.Configuration.GetValue<bool>("Quartz:Persistence:Clustered");
var quartzClusterCheckinIntervalSeconds = builder.Configuration.GetValue<int>("Quartz:Persistence:ClusterCheckinIntervalSeconds", 15);
var quartzConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? builder.Configuration.GetConnectionString("AonikDb");

if (quartzPersistenceEnabled && string.IsNullOrWhiteSpace(quartzConnectionString))
{
    if (builder.Environment.IsDevelopment())
    {
        quartzConnectionString = "Server=(localdb)\\MSSQLLocalDB;Database=AonikDb;Trusted_Connection=True;TrustServerCertificate=True;";
    }
    else
    {
        throw new InvalidOperationException(
            "ConnectionStrings:DefaultConnection or ConnectionStrings:AonikDb is required when Quartz persistence is enabled.");
    }
}

// Read job options for Quartz configuration
var jobOptions = builder.Configuration
    .GetSection("Quartz:ScheduledJobs")
    .Get<ScheduledJobOptions>() ?? new ScheduledJobOptions();

var scheduledJobDefinitions = ScheduledJobDefinitions.Create(jobOptions);

foreach (var definition in scheduledJobDefinitions)
{
    builder.Services.AddSingleton<IScheduledJobDefinition>(definition);
}

builder.Services.AddScoped<ICustomerInsightSnapshotJobUserEnumerator, CustomerInsightSnapshotJobUserEnumerator>();
builder.Services.AddScoped<ICustomerInsightAiSummaryJobSnapshotEnumerator, CustomerInsightAiSummaryJobSnapshotEnumerator>();

// Register the Quartz runtime only in the Worker host.
builder.Services.AddQuartzBackgroundJobRuntime();

// Register job listener, projection sync, and admin command processing.
builder.Services.AddSingleton<ScheduledJobListener>();
builder.Services.AddSingleton<ScheduledJobProjectionSynchronizer>();
builder.Services.AddHostedService<ScheduledJobRegistrar>();
builder.Services.AddHostedService<ScheduledJobCommandProcessor>();
builder.Services.AddHostedService<SchedulerHealthPublisher>();

// Configure Quartz scheduler with cron-scheduled jobs
builder.Services.AddQuartz(q =>
{
    q.SchedulerName = quartzSchedulerName;
    q.SchedulerId = "AUTO";
    q.MisfireThreshold = TimeSpan.FromSeconds(quartzMisfireThresholdSeconds);

    q.UseDefaultThreadPool(tp => tp.MaxConcurrency = 3);

    if (quartzPersistenceEnabled)
    {
        q.UsePersistentStore(store =>
        {
            store.UseProperties = true;
            store.UseSqlServer(sql =>
            {
                sql.ConnectionString = quartzConnectionString!;
                sql.TablePrefix = quartzTablePrefix;
            });
            store.UseSystemTextJsonSerializer();
            store.PerformSchemaValidation = true;

            if (quartzClustered)
            {
                store.UseClustering(cluster =>
                {
                    cluster.CheckinInterval = TimeSpan.FromSeconds(quartzClusterCheckinIntervalSeconds);
                });
            }
        });
    }

    // Refresh projection state after each scheduled execution.
    q.AddJobListener<ScheduledJobListener>();

    foreach (var definition in scheduledJobDefinitions)
    {
        definition.Configure(q);
    }
});

// Quartz hosted service manages scheduler lifecycle (start, graceful shutdown)
builder.Services.AddQuartzHostedService(options =>
{
    options.WaitForJobsToComplete = true;
});

var host = builder.Build();
host.Run();

// Adapter implementations for converting Infrastructure VectorStore interfaces to Agents Framework interfaces
namespace Aonik.Worker
{
    internal sealed class VectorStoreAdapterImpl : Aonik.Agents.Framework.IVectorStore
    {
        private readonly Aonik.Infrastructure.VectorStore.Contracts.IVectorStore innerVectorStore;

        public VectorStoreAdapterImpl(Aonik.Infrastructure.VectorStore.Contracts.IVectorStore innerVectorStore)
        {
            this.innerVectorStore = innerVectorStore;
        }

        public async Task<IEnumerable<Aonik.Agents.Framework.VectorSearchResult>> SearchAsync(
            string collectionName,
            float[] queryEmbedding,
            int limit = 10,
            float scoreThreshold = 0.5f,
            CancellationToken cancellationToken = default)
        {
            var results = await innerVectorStore.SearchAsync(
                collectionName,
                queryEmbedding,
                limit,
                scoreThreshold,
                cancellationToken);

            return results.Select(r => new Aonik.Agents.Framework.VectorSearchResult(r.Id, r.Score, r.Payload));
        }
    }

    internal sealed class EmbeddingServiceAdapterImpl : Aonik.Agents.Framework.IEmbeddingService
    {
        private readonly Aonik.Infrastructure.VectorStore.Contracts.IEmbeddingService innerEmbeddingService;

        public EmbeddingServiceAdapterImpl(Aonik.Infrastructure.VectorStore.Contracts.IEmbeddingService innerEmbeddingService)
        {
            this.innerEmbeddingService = innerEmbeddingService;
        }

        public string ModelName => innerEmbeddingService.ModelName;

        public int Dimensions => innerEmbeddingService.Dimensions;

        public async Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            return await innerEmbeddingService.GetEmbeddingAsync(text, cancellationToken);
        }

        public async Task<IEnumerable<float[]>> GetEmbeddingsBatchAsync(
            IEnumerable<string> texts,
            CancellationToken cancellationToken = default)
        {
            return await innerEmbeddingService.GetEmbeddingsBatchAsync(texts, cancellationToken);
        }
    }
}
