namespace JobsGeParser.IntegrationTests;

/// <summary>
/// One container for the whole suite, and no parallelism: every test truncates shared tables.
/// </summary>
[CollectionDefinition("Alerts", DisableParallelization = true)]
public sealed class AlertsCollection : ICollectionFixture<AlertsFixture>;
