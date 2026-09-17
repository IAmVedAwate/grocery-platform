using Xunit;

namespace Integration;

/// <summary>One SQL Server container shared across every test in this
/// project — see CustomWebApplicationFactory.</summary>
[CollectionDefinition("Integration")]
public sealed class IntegrationTestCollection : ICollectionFixture<CustomWebApplicationFactory>;
