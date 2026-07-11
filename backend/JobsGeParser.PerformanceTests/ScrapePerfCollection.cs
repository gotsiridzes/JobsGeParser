namespace JobsGeParser.PerformanceTests;

[CollectionDefinition("ScrapePerf", DisableParallelization = true)]
public sealed class ScrapePerfCollection : ICollectionFixture<ScrapePerformanceFixture>;
