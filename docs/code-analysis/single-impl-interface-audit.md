# Single-Implementation Interface Audit

> Generated: 2026-05-05
> Scope: `C:\Users\mjosi\source\repos\aonik\src` (217 declared interfaces) + `C:\Users\mjosi\source\repos\aonik\tests` (test-double scan)
> Goal: identify single-implementation interfaces with no test doubles that add ceremony without abstraction value

---

## Methodology

1. Enumerated every `interface I*` declared under `src/`.
2. Skipped categories per the audit brief:
   - Anything in `Aonik.SharedKernel.Abstractions.*` (intentional cross-module contracts hardened by commits 201a9841 / ca0a5eb1 / 5098f370).
   - Contributor patterns: `IDomainAgentDescriptor`, `IDemoSeedContributor`, `IGlobalSeedContributor`, `ITenantProvisioningContributor`, `IWorkflowFactory`.
   - Constraint / event interfaces: `ITenantScoped`, `IIntegrationEvent`, `IEventHandler`, `IModule`.
   - System / framework interfaces (`IAuthorizationRequirement`, `IEntityTypeConfiguration<T>`, `IHostedService`, `IFeatureFilter`, `IFeatureManager`, `IDataProtectionKeyContext`, `IJob`, `IDesignTimeDbContextFactory`, `IDisposable`, `IAsyncDisposable`, `IGlobalPreProcessor`, `IChatClient`, `IEmbeddingGenerator`, `IValidatableObject`).
   - Migration and `[GeneratedCode]` files.
3. For surviving candidates, ran two filters:
   - **Implementation count** — must be exactly one concrete class in `src/`.
   - **Test double presence** — must have no `Mock<I…>`, `Fake…`, `Stub…`, `Test…`, `NoOp…`, `Recording…`, or `InMemory…` test class implementing the interface anywhere under `tests/`.
4. For interfaces that survived both filters, classified them based on whether the implementation is `internal sealed` (interface IS the public access path) or whether the interface is genuinely a redundant wrapper.

---

## Summary

| Classification | Count |
|---|---|
| **DELETE** — clear redundancy | 5 |
| **KEEP — exposed contract** (internal impl, public interface is the only access path) | ~70 |
| **KEEP — testability borderline** | 4 |
| **KEEP — has test double** (auto-skipped) | many |
| **KEEP — multi-impl** (auto-skipped) | many |
| **KEEP — SharedKernel.Abstractions** (auto-skipped per brief) | ~50 |

The codebase already follows a disciplined pattern: most "single-impl interface + internal sealed class" pairings are intentional Clean Architecture indirections — the interface is `public` so cross-module code (or endpoints in the same module) can DI-resolve a class that is `internal sealed`. Removing those interfaces would force the implementations to become `public`, which breaks deliberate encapsulation. Those are correctly classified KEEP.

The genuinely redundant interfaces are a small set in Infrastructure.Caching, Platform.Operations alert plumbing, Platform.Notifications realtime publisher, and the Application.Abstractions JSON serializer wrapper.

---

## DELETE — clear win

These interfaces add ceremony without abstraction value: every consumer is in the same assembly, the implementation does not need encapsulation behind the interface, and there is no test double depending on it. Deleting them and using the concrete class directly tightens the design.

### 1. `INotificationRealtimePublisher`

- **Interface:** [src/Aonik.Platform/Services/Notifications/NotificationRealtimePublisher.cs:8](src/Aonik.Platform/Services/Notifications/NotificationRealtimePublisher.cs) (`internal interface`)
- **Implementation:** `NotificationRealtimePublisher` (same file, `internal sealed`)
- **Consumers:** `NotificationService` (same module) and `AdminNotificationStreamingEndpoint` (same module, via `RequestServices.GetRequiredService`)
- **Cross-module usage:** none.
- **Test doubles:** none.
- **Recommendation:** Delete the interface. Register and inject the concrete `NotificationRealtimePublisher` directly. The interface and impl already live in the same file with the same `internal` visibility — no encapsulation is gained.

### 2. `IAlertAudienceResolver`

- **Interface:** [src/Aonik.Platform/Services/Operations/AzureMonitorAlertServices.cs:16](src/Aonik.Platform/Services/Operations/AzureMonitorAlertServices.cs) (`internal interface`)
- **Implementation:** `PlatformAdminAlertAudienceResolver` (same file, `internal sealed`)
- **Consumers:** `AlertProcessingService` only (same file).
- **Cross-module usage:** none.
- **Test doubles:** none.
- **Recommendation:** Delete the interface. `AlertProcessingService` can take `PlatformAdminAlertAudienceResolver` directly. If a second audience resolver appears (e.g. tenant-scoped vs platform-admin), reintroduce the interface then.

### 3. `IAlertProcessingService`

- **Interface:** [src/Aonik.Platform/Services/Operations/AzureMonitorAlertProcessingQueue.cs:32](src/Aonik.Platform/Services/Operations/AzureMonitorAlertProcessingQueue.cs) (`internal interface`)
- **Implementation:** `AlertProcessingService` ([src/Aonik.Platform/Services/Operations/AzureMonitorAlertServices.cs:424](src/Aonik.Platform/Services/Operations/AzureMonitorAlertServices.cs), `internal sealed`)
- **Consumers:** `AlertProcessingQueue.ProcessQueueAsync` only, via `scope.ServiceProvider.GetRequiredService<IAlertProcessingService>()` (same module).
- **Cross-module usage:** none.
- **Test doubles:** none. (`IAlertAnalysisWorkflow` and `IAlertProcessingQueue` are tested via doubles — those KEEP. This one is not.)
- **Recommendation:** Delete the interface. Register `AlertProcessingService` and resolve it directly via `GetRequiredService<AlertProcessingService>()`. Pairs naturally with the `IAlertAudienceResolver` deletion since they live together.

### 4. `ICachePolicyProvider`

- **Interface:** [src/Aonik.Infrastructure/Caching/CachePolicyProvider.cs:6](src/Aonik.Infrastructure/Caching/CachePolicyProvider.cs) (`public interface`)
- **Implementation:** `CachePolicyProvider` (same file, `public class`)
- **Consumers:** `FusionCacheStore` only (same module, `Aonik.Infrastructure.Caching`).
- **Cross-module usage:** none.
- **Test doubles:** none.
- **Recommendation:** Delete the interface. Both interface and impl are `public`, so no visibility constraint. Inject `CachePolicyProvider` directly into `FusionCacheStore`.

### 5. `ICacheSetRegistry`

- **Interface:** [src/Aonik.Infrastructure/Caching/CacheSetRegistry.cs:5](src/Aonik.Infrastructure/Caching/CacheSetRegistry.cs) (`public interface`)
- **Implementation:** `CacheSetRegistry` (same file, `public class`)
- **Consumers:** `FusionCacheStore`, `FusionCacheInvalidationHandler`, `CacheManagementService` — all within `Aonik.Infrastructure.Caching`.
- **Cross-module usage:** none.
- **Test doubles:** none.
- **Recommendation:** Delete the interface. Same justification as `ICachePolicyProvider` — purely a within-module wrapper around a public class.

### 6. `IJsonSerializer` (borderline — flagged for review)

- **Interface:** [src/Aonik.Application/Abstractions/IJsonSerializer.cs:8](src/Aonik.Application/Abstractions/IJsonSerializer.cs) (`public interface`)
- **Implementation:** `SystemTextJsonSerializer` ([src/Aonik.Infrastructure/SystemTextJsonSerializer.cs:11](src/Aonik.Infrastructure/SystemTextJsonSerializer.cs), `public class`)
- **Consumers:** `QuartzJobExecutionAdapter`, `QuartzBackgroundJobManager`, `BackgroundJobsExtensions` — all in `Aonik.Infrastructure.BackgroundJobs`. No consumer in `Aonik.Application` despite the interface being declared there.
- **Cross-module usage:** the interface is declared in Application but consumed only in Infrastructure.
- **Test doubles:** none.
- **Recommendation:** Delete the interface. The Application/Infrastructure inversion is theoretical — no Application code uses it. Background-job code can call `System.Text.Json.JsonSerializer` directly or take the concrete `SystemTextJsonSerializer`. Note: report-01.md flagged the Application layer as "hollow"; this is one of those vestigial contracts.

---

## KEEP — testability borderline

These interfaces have only one impl and no test double today, but the consumer would have to spin up heavy DI dependencies (DbContext, IServiceScopeFactory, IClock, MAF runtime) to test the concrete class. Worth flagging but not auto-deleting.

### `IScheduledJobDefinition`

- **Interface:** [src/Aonik.Worker/Jobs/ScheduledJobDefinitions.cs:6](src/Aonik.Worker/Jobs/ScheduledJobDefinitions.cs) (`internal interface`)
- **Implementation:** generic `ScheduledJobDefinition<TJob>` (same file)
- **Why borderline:** the interface acts as a non-generic abstraction that lets `Create()` return `IReadOnlyList<IScheduledJobDefinition>` over closed generic types. Without it you would need `IReadOnlyList<object>` or factor each job's registration into a lambda. Mild value as a DI seam if a future test wants to assert the registered jobs.
- **Recommendation:** Keep. The non-generic-over-generic abstraction is the value, not the polymorphism.

### `ICliOutputWriter`

- **Interface:** [src/Aonik.Cli/Abstractions/ICliOutputWriter.cs:5](src/Aonik.Cli/Abstractions/ICliOutputWriter.cs)
- **Implementation:** `TextWriterCliOutputWriter` ([src/Aonik.Cli/Infrastructure/TextWriterCliOutputWriter.cs:8](src/Aonik.Cli/Infrastructure/TextWriterCliOutputWriter.cs))
- **Why borderline:** classic abstraction-over-IO. No test double today, but the moment a future test wants to capture CLI output, the interface is the natural seam.
- **Recommendation:** Keep. Cheap insurance — TextWriter wrapping is the canonical case for an output abstraction.

### `IBlobStorageFactory`

- **Interface:** [src/Aonik.Application/Abstractions/Storage/IBlobStorageFactory.cs:6](src/Aonik.Application/Abstractions/Storage/IBlobStorageFactory.cs)
- **Implementation:** `BlobStorageFactoryService` ([src/Aonik.Infrastructure/Storage/BlobStorageFactoryService.cs:9](src/Aonik.Infrastructure/Storage/BlobStorageFactoryService.cs))
- **Why borderline:** Application-layer abstraction over Azure Blob Storage SDK. Consumed by `ProfilePhotoStore`, `FileStore`, `DocumentFileStore`, `ProfilePhotoStorageInitializer` — all Infrastructure. No Application-layer consumers, similar smell to `IJsonSerializer`. But the Azure SDK touch makes the interface useful as a seam if storage is ever reimplemented (S3, local disk emulation, etc.).
- **Recommendation:** Keep. Storage providers are a realistic plug-point; Azure is the current choice but not necessarily forever.

### `IBackgroundJobExecuter`

- **Interface:** [src/Aonik.Application/Abstractions/BackgroundJobs/IBackgroundJobExecuter.cs:8](src/Aonik.Application/Abstractions/BackgroundJobs/IBackgroundJobExecuter.cs)
- **Implementation:** `BackgroundJobExecuter` ([src/Aonik.Infrastructure/BackgroundJobs/BackgroundJobExecuter.cs:18](src/Aonik.Infrastructure/BackgroundJobs/BackgroundJobExecuter.cs))
- **Why borderline:** Consumed by `NullBackgroundJobManager` (Application) and `QuartzJobExecutionAdapter` (Infrastructure). The `IBackgroundJobManager` interface has 3 impls (Quartz / InMemory / Null), and the executer is the inner kernel of all three. Deleting this would force the manager interfaces to take a concrete class, which is fine in isolation but breaks the symmetry of the manager-trio abstraction.
- **Recommendation:** Keep. The job framework as a whole is multi-impl; this is the inner shared piece.

---

## KEEP — exposed contract (representative samples)

Confirmed single-impl, no-test-double interfaces where the implementation is `internal sealed` and the interface is the only public access path for cross-module code or for endpoints that share an assembly with the implementation. Listed for completeness — these are NOT delete candidates.

### Aonik.Agents.Contracts.Services.* (within-module abstractions, per audit nuance KEEP)

| Interface | Single impl |
|---|---|
| `IPlaygroundScenarioService` | `Aonik.Agents.Framework.PlaygroundScenarioService` (`internal sealed`) |
| `IAguiVoiceModeValidator` | `Aonik.Agents.Services.AguiVoiceModeValidator` (`internal sealed`) |
| `IAguiStreamPipeline` | `Aonik.Agents.Services.AguiStreamPipeline` (`internal sealed`) |
| `IAguiRunOptionsBuilder` | `Aonik.Agents.Services.AguiRunOptionsBuilder` (`internal sealed`) |
| `IAguiMessageConverter` | `Aonik.Agents.Services.AguiMessageConverter` (`public sealed`) |
| `IAgentRunService` | `Aonik.Agents.Framework.AgentRunService` (`internal sealed`) |
| `IWorkflowService` | `Aonik.Agents.Services.Workflows.WorkflowService` (`internal sealed`) |
| `IChatThreadTitleGenerator` | `Aonik.Agents.Framework.ChatThreadTitleGenerator` (`internal sealed`) |
| `IChatThreadManager` | `Aonik.Agents.Services.ChatThreadManager` (`public sealed`) |
| `IAgentContextualizer` | `Aonik.Agents.Services.AgentContextualizer` (`public sealed`) |
| `IProposalApprovalService` | `Aonik.Agents.Services.ProposalApprovalService` (`internal sealed`) |
| `IPostStreamPersistenceCoordinator` | `Aonik.Agents.Services.PostStreamPersistenceCoordinator` (`public sealed`) |
| `IToolCallClassifier` | `Aonik.Agents.Services.ToolCallClassifier` (`public sealed`) |
| `IConversationSummaryService` | `Aonik.Agents.Services.ConversationSummaryGenerator` (`internal sealed`) |
| `IMcpToolProvider` | `Aonik.Agents.Framework.McpToolProvider` (`internal sealed`) |

Per the audit brief: "Aonik.Agents.Contracts.Services.* — these are within-module abstractions; many are single-impl by design. Prefer KEEP." Some (e.g. `IAguiMessageConverter`, `IToolCallClassifier`, `IPostStreamPersistenceCoordinator`) wrap `public sealed` classes and are technically deletable, but the Contracts.Services namespace is a deliberate API surface for the module — leaving them keeps the namespace coherent.

### Aonik.Ai.Contracts.Services.* (within-module abstractions, per audit nuance KEEP)

| Interface | Single impl |
|---|---|
| `IAiTaskService` | `Aonik.Ai.Services.AiTaskService` (`internal sealed`) |
| `IAiModelCatalogImportService` | `Aonik.Ai.Services.AiModelCatalogImportService` (`internal sealed`) |
| `IPromptSpecService` | `Aonik.Ai.Services.PromptSpecService` (`internal sealed`) |
| `IRoutePolicyService` | `Aonik.Ai.Services.RoutePolicyService` (`internal sealed`) |

These are public-interface / internal-sealed-impl pairings — the interface is the only way endpoints in the same module can resolve the impl, and removing it forces the impl to become public. Per the audit nuance, KEEP.

### Aonik.Platform.Contracts.Services.* (cross-module exposed contracts)

A large set of interfaces in `Aonik.Platform.Contracts.*` follow the same pattern: public interface, `internal sealed` (or `internal class`) implementation. Deleting any of these would force the impl public and expose Platform's internal services to Aonik.Api / Aonik.Worker / other modules. Examples:

| Interface | Single impl |
|---|---|
| `IAutonumberingService` | `Aonik.Platform.Services.Autonumbering.AutonumberingService` (`internal class`) |
| `IObservabilityService` | `Aonik.Infrastructure.Observability.AppInsightsQueryService` (`public class` — distinct module) |
| `IRuntimeOperationsService` | `Aonik.Infrastructure.Operations.ContainerAppsRuntimeService` (`internal sealed` — distinct module) |
| `IAlertIngestionService` | `Aonik.Platform.Services.Operations.AlertIngestionService` (`internal sealed`) |
| `IAlertAdminService` | `Aonik.Platform.Services.Operations.AlertAdminService` (`internal sealed`) |
| `IScheduledJobAdminService` | `Aonik.Platform.Services.Operations.ScheduledJobAdminService` (`internal class`) |
| `INotificationDeviceService` | `Aonik.Platform.Services.Notifications.NotificationDeviceService` (`internal sealed`) |
| `INotificationService` | `Aonik.Platform.Services.Notifications.NotificationService` (`internal sealed`) |
| `INotificationTemplateRenderer` | `Aonik.Infrastructure.Notifications.FluidNotificationTemplateRenderer` (`public class`) |
| `IUserProvisioningService` | `Aonik.Platform.Services.Identity.UserProvisioningService` (`internal class`) |
| `IUserProfileService` | `Aonik.Platform.Services.Identity.UserProfileService` (`internal class`) |
| `IUserIdentityService` | `Aonik.Platform.Services.Identity.UserIdentityService` (`internal class`) |
| `ITenantService` | `Aonik.Platform.Services.Identity.TenantService` (`internal class`) |
| `IBootstrapService` | `Aonik.Platform.Services.Identity.BootstrapService` (`internal class`) |
| `IBootstrapTenantProvisioner` | `Aonik.Platform.Services.Identity.TenantProvisioner` (`internal class`, also `ITenantProvisioner`) |
| `IAccessManagementService` | `Aonik.Platform.Services.Identity.AccessManagementService` (`internal class`) |
| `ITenantFeatureService` | `Aonik.Platform.Services.Features.TenantFeatureService` (`internal class`) |
| `IPermissionSeedService` | `Aonik.Platform.Services.Seeding.PermissionSeedService` (`internal class`) |
| `IContentBlockService` | `Aonik.Platform.Services.Cms.ContentBlockService` (`internal class`) |
| `IDocumentService` | `Aonik.Platform.Services.Compliance.DocumentService` (`internal class`) |
| `IDemoSeedService` | `Aonik.Platform.Services.Seeding.DemoSeedService` (`internal class`) |
| `ICustomerDataService` | `Aonik.Platform.Services.Customers.CustomerDataService` (`internal class`) |
| `ICustomerAdminService` | `Aonik.Platform.Services.Customers.CustomerAdminService` (`internal class`) |
| `IAuditLogAdminService` | `Aonik.Platform.Services.Compliance.AuditLogAdminService` (`internal sealed`) |
| `IPayaboSetupProfileService` | `Aonik.Platform.Services.Settings.PayaboSetupProfileService` (`internal class`) |
| `IAuthProviderSettingsService` | `Aonik.Platform.Services.Settings.AuthProviderSettingsService` (`internal class`) |
| `ITextToSpeechCredentialSettingsService` | `Aonik.Platform.Services.Settings.TextToSpeechCredentialSettingsService` (`internal sealed`) |
| `IIdpAccountServiceFactory` | `Aonik.Infrastructure.Authentication.Account.IdpAccountServiceFactory` (cross-module) |
| `IAuthTokenServiceFactory` | `Aonik.Infrastructure.Authentication.TokenExchange.AuthTokenServiceFactory` (cross-module) |
| `IIdpUserProvisionerFactory` | `Aonik.Infrastructure.Authentication.Provisioning.IdpUserProvisionerFactory` (cross-module) |
| `IIdpPasswordResetServiceFactory` | `Aonik.Infrastructure.Authentication.PasswordReset.IdpPasswordResetServiceFactory` (cross-module) |
| `ITenantResolver` | `Aonik.Infrastructure.Authentication.TenantResolver` (cross-module) |
| `IPendingTenantUserProvisioner` | `Aonik.Platform.Services.Identity.PendingTenantUserProvisioner` (`internal sealed`) |
| `IVerificationService` | `Aonik.Platform.Services.Identity.VerificationService` (`internal class`) |
| `ISettingManager` | `Aonik.Infrastructure.Settings.SettingService` (cross-module — also implements `ISettingProvider`) |
| `IImageProcessingService` | `Aonik.Infrastructure.Storage.ImageProcessingService` (cross-module) |
| `IRegistrationService` | `Aonik.Platform.Services.Registration.RegistrationService` (`internal class`) |
| `ICacheManagementService` | `Aonik.Infrastructure.Caching.CacheManagementService` (cross-module) |
| `IReferenceDataService` | `Aonik.Infrastructure.ReferenceData.ReferenceDataService` (cross-module) |
| `IIdentityService` | `Aonik.Platform.Services.Identity.IdentityService` (`internal class`) |
| `IUserRoleService` | `Aonik.Platform.Services.Identity.UserRoleService` (`internal class`, has `Mock<IUserRoleService>` consumer-side test would be a pain — but interface is still the public surface) |

**Recommendation for the entire group:** Keep. Each interface is the access point for a deliberately-internal implementation; deletion is a non-trivial visibility change.

### Aonik.Finance.Contracts.Services.* (cross-module exposed contracts)

Same pattern as Platform.Contracts.* — `IBillService`, `IBudgetService`, `ICatalogService`, `IPublicCatalogService`, `IAccountLinkService`, `IPaymentService`, `IPublicPaymentService`, `IOrderService`, `IPublicOrderService`, `ILedgerService`, `IBillingService`, `IPartnerAdminService`, `IPayActivityService`, `IDashboardService`, `IFinanceInsightsService`, `IMySpaceSummaryService`, `IFinancialContextService`, `IFinancialLifeGraphService`, `IFinancialLifeGraphTraversalService`, `IFinancialLifeGraphRetrievalService`, `IFinancialLifeGraphSchemaService`, `ICommitmentService`, `ITransactionClassificationService`, `ITransactionAttachmentService`, `ITransactionAiClassifier`, `ICustomerInsightSnapshotService`, `ICustomerInsightSnapshotReader`, `ICustomerInsightSnapshotGenerator`, `IPersonalAccountService`, `IPersonalTransactionService`, `IPersonalAccountLinkService`, `IStatementImportService`, `IHouseholdService`, `IPersonalFinanceInsightsService`, `IPersonalFinanceNarrativeInsightsService`, `IFxRateService`, `IFxQuoteService`, `IPricingService`, `IPricingPolicyService`.

All are public interfaces with internal sealed (or internal class) implementations. Deletion would force impls public and expose Finance internals to other modules. **All KEEP.**

### Aonik.Cli internal contracts

| Interface | Single impl | Notes |
|---|---|---|
| `ISessionStore` | `FileSessionStore` | Has `InMemorySessionStore` test double — KEEP. |
| `IAonikCliApiClient` | `AonikCliApiClient` | Has `FakeAonikCliApiClient` test double — KEEP. |

### Aonik.Agents.Workflows.Graph

| Interface | Single impl | Notes |
|---|---|---|
| `IGraphWorkflowRunner` | `GraphWorkflowRunner` (`internal sealed`) | Public interface, internal impl, used by `RunWorkflowEndpoint` — KEEP exposed contract. |
| `IWorkflowRunRecorder` | `WorkflowRunRecorder` | Has `Mock<IWorkflowRunRecorder>` in `AgentExecutorTests.cs` — KEEP. |

### Worker / Ai / Platform internals with test doubles (KEEP — has test double)

These were investigated and confirmed to have test doubles:

| Interface | Test double location |
|---|---|
| `ICustomerInsightSnapshotJobUserEnumerator` | `tests/Aonik.Application.Tests/PersonalFinance/CustomerInsightSnapshotJobTests.cs:55` (`StubEnumerator`) |
| `ICustomerInsightAiSummaryJobSnapshotEnumerator` | `tests/Aonik.Application.Tests/PersonalFinance/CustomerInsightAiSummaryJobTests.cs:76` (`StubEnumerator`) |
| `ITextToSpeechRateLimiter` | `tests/Aonik.Application.Tests/Ai/StreamingTextToSpeechServiceTests.cs:312` (`AlwaysOpenRateLimiter`) |
| `ITtsCache` | `tests/Aonik.Application.Tests/Ai/StreamingTextToSpeechServiceTests.cs:382` (`InMemoryTtsCache`) |
| `IFinancialLifeGraphCacheInvalidator` | 7 test files (`NoOpGraphCacheInvalidator`, `RecordingGraphCacheInvalidator`) |
| `IAiModelCatalogSource` | `tests/Aonik.Application.Tests/Ai/AiModelCatalogImportServiceTests.cs:198` (`StubAiModelCatalogSource`) |
| `IUserBriefAiDataProvider` | `tests/Aonik.Application.Tests/UserBrief/UserBriefProjectorTests.cs:37` (`StubAiDataProvider`) |
| `IUserBriefDataProvider` | `tests/Aonik.Application.Tests/UserBrief/UserBriefProjectorTests.cs:27` (`StubFinanceDataProvider`) |
| `IUserBriefContextDataProvider` | `tests/Aonik.Application.Tests/UserBrief/UserBriefProjectorTests.cs:54` (`StubUserContextDataProvider`) |
| `IAiTaskProfileResolver` | several |
| `IUserMemorySaveProvider` | `tests/Aonik.Application.Tests/Ai/ConversationSummaryGeneratorIdempotencyTests.cs:276` (`RecordingMemoryProvider`) |
| `IPersonalProfileProvisioner` | `tests/Aonik.Application.Tests/Identity/IdentityServiceTests.cs:367` (`NoOpPersonalProfileProvisioner`) |
| `IUserNotificationWriter` | `tests/Aonik.Application.Tests/PersonalFinance/HouseholdServiceTests.cs:113` (`RecordingNotificationWriter`) |
| `IInsightWriter` | `tests/Aonik.Application.Tests/PersonalFinance/PersonalFinanceNarrativeInsightsServiceTests.cs:323` (`FakeInsightWriter`) |
| `IAiRunWriter` | `RecordingAiRunWriter`, `FakeAiRunWriter` |
| `IStreamingTextToSpeechService` | `CapturingStreamingTextToSpeechService` |
| `ITextToSpeechCredentialResolver` | `FakeCredentialResolver` |
| `IDomainAgentResolver` | `Mock<IDomainAgentResolver>` + `StubDomainAgentResolver` |
| `IUserBriefProjector` | `StubUserBriefProjector` |
| `IMasterOrchestratorService` | `StubMasterOrchestratorService` |
| `IChatThreadHistoryCache` | `InMemoryHistoryCache` |
| `IChatThreadService` | `StubChatThreadService` |
| `IAlertProcessingQueue` | `Mock<IAlertProcessingQueue>` + `TestAlertProcessingQueue` |
| `IAlertAnalysisWorkflow` | `TestAlertAnalysisWorkflow` |
| `IAiModelResolver` | `StubModelResolver` |
| `ITenantCurrencyProvider` | `StubTenantCurrencyProvider` |
| `IAiRunStatsService` | `StubAiRunStats` |
| `ITenantTextToSpeechSettingsService` | `FakeSettingsService` |
| `IAgentDescriptor` family — handled via contributor pattern, skipped per brief |
| `IPushNotificationSender` | `TestPushNotificationSender` |
| `IPermissionService` | several `AllowAllPermissionService` |
| `IAuditLogWriter` | several `TestAuditLogWriter` / `NoOpAuditLogWriter` |
| `INotificationTemplateService` | `StubNotificationTemplateService` |
| `IAuthTokenServiceFactory` | `StubAuthTokenServiceFactory` |
| `IIdpPasswordResetServiceFactory` | `StubPasswordResetServiceFactory` |
| `IIdpPasswordResetService` | `StubPasswordResetService` (nested) |
| `IAuthTokenService` | `StubAuthTokenService` |
| `IPartyAccountService` | `FakePartyAccountService` |
| `IFileStore` | `FakeFileStore` |
| `IPersonalAccountLinkProviderGateway` | `FakeAccountLinkProviderGateway` (also has 2 production impls — Plaid/PlaidSimulated, multi-impl anyway) |
| `ISettingValueProtector` | `PassthroughSettingValueProtector` |

### Other interfaces auto-skipped

Multi-impl (so not single-impl by definition; not delete candidates regardless): `IPromptStore` (2), `IUserMemoryService` (2), `IContentImageGenerator` (2), `IAiTraceReader` (2), `ITextToSpeechProvider` (2), `IIdpAccountService` (2), `IIdpUserProvisioner` (2), `IIdpPasswordResetService` (2), `IAuthTokenService` (2), `ITenantProvider` (HttpContext + Static + Mcp + tests), `IBackgroundJobManager` (3), `IClock` (System + Mcp + tests), `IExternalIdentity` (2 record impls), `IVectorStore` (Qdrant + Worker adapter), `IEmbeddingService` (OpenAI + Worker adapter).

Skipped contributor patterns: `IDomainAgentDescriptor`, `IDemoSeedContributor`, `IGlobalSeedContributor`, `ITenantProvisioningContributor`, `IWorkflowFactory`.

Skipped SharedKernel.Abstractions (intentional cross-module contracts): `IPartyService`, `IPartyAccountService`, `IComplianceService`, `IPermissionService`, `IAuditLogWriter`, `IClock`, `ITenantProvider`, `ITenantContext`, `ICurrentTenant`, `ICurrentUserProvider`, `ICurrentUserContext`, `ITenantCurrencyProvider`, `ITenantProvisioningContributor`, `IDemoSeedContributor`, `IGlobalSeedContributor`, `ICustomerActivityProvider`, `ICustomerDataExportProvider`, `ICustomerDataImportConsumer`, `ICustomerFinanceStatsProvider`, `ICustomerInsightSnapshotForAi`, `ICurrencyMetadataProvider`, `IFileStore`, `IUserNotificationWriter`, `IUserMemoryRecallProvider`, `IUserMemorySaveProvider`, `IUserBriefAiDataProvider`, `IUserBriefDataProvider`, `IUserBriefContextDataProvider`, `IPersonalProfileProvisioner`, `IPermissionService`, `IInsightReader`, `IInsightWriter`, `IPromptStore`, `IAiTaskReader`, `IAiTaskProfileResolver`, `IAiModelResolver`, `IAiRunWriter`, `IAiRunStatsService`, `IAiProviderSettings`, `ICustomerInsightAiSummaryReader`, `ICustomerInsightAiSummaryService`, `ITenantTextToSpeechSettingsService`, `ITextToSpeechService`, `IStreamingTextToSpeechService`, `ITextToSpeechCredentialResolver`, `IContentImageGenerator`, `ISpeechRenderer`, `IOrderExistenceChecker`, `ISettingProvider`, `ICorrelationContext`, `IAgentDemoCleanup`, `IAgentConfigurationService`, `IAgentProposalStore`, `IAgentProposalQueryService`.

---

## How to action

For each of the 5 DELETE recommendations:

1. Delete the `.cs` interface file.
2. Update DI registration in the relevant module (e.g. `services.AddScoped<IAlertProcessingService, AlertProcessingService>()` becomes `services.AddScoped<AlertProcessingService>()`).
3. Update consumer constructor parameters from `IFoo foo` to `Foo foo`.
4. Update any `RequestServices.GetRequiredService<IFoo>()` to use the concrete class.
5. Build + run the relevant test project to confirm no breakage.

**Estimated effort:** 30-60 minutes per interface. The `IAlertProcessingService` + `IAlertAudienceResolver` pair should be done together as they live in the same alert-processing pipeline.

**Estimated lines saved:** ~100 LOC across 5 interfaces (each interface is 10-30 lines counting using-directives, doc-comments, declaration). The signal-to-noise improvement matters more than the LOC savings — fewer interfaces means fewer "where is the real implementation?" navigation hops in the IDE.
