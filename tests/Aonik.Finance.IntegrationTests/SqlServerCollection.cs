using Aonik.IntegrationTests.Support;
using Xunit;

namespace Aonik.Finance.IntegrationTests;

/// <summary>
/// Binds the shared <see cref="SqlServerContainerFixture"/> to this assembly's
/// test collection. xUnit only discovers <see cref="CollectionDefinitionAttribute"/>
/// within the assembly that owns the tests, so every integration-test project
/// declares its own definition over the reusable fixture.
/// </summary>
[CollectionDefinition(SqlServerContainerFixture.CollectionName)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerContainerFixture>;
