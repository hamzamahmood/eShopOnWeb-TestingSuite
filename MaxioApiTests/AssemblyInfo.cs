using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;
using MaxioApiTests;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
[assembly: TestCollectionOrderer("MaxioApiTests.FaultLastCollectionOrderer", "MaxioApiTests")]

namespace MaxioApiTests;

/// <summary>Orders test collections with faults last.</summary>
public sealed class FaultLastCollectionOrderer : ITestCollectionOrderer
{
    public IEnumerable<ITestCollection> OrderTestCollections(IEnumerable<ITestCollection> testCollections) =>
        testCollections
            .OrderBy(c => c.DisplayName.Contains("ServerFault", StringComparison.Ordinal) ? 1 : 0)
            .ThenBy(c => c.DisplayName, StringComparer.Ordinal);
}
