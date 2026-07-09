using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;
using MaxioPassthroughApiTests;

// Run every test class sequentially rather than in parallel. All tests hit the SAME running PublicApi
// instance, which shares one resilience pipeline (retries + a circuit breaker) across every request. The
// robustness suite (ServerFaultTests) deliberately drives repeated upstream failures; run concurrently, those
// failures could trip the shared circuit breaker and spuriously fail an unrelated green test running at the
// same moment. Serial execution keeps each test's view of the provider independent (and also keeps the
// AI-verifier calls within typical rate limits).
[assembly: CollectionBehavior(DisableTestParallelization = true)]

// Run the server-fault collection LAST. Its persistent-5xx cases trip a real integration's shared circuit
// breaker (e.g. Direct's Polly pipeline: minimum-throughput 5, 50% failure ratio, 15s break), which then
// fails-fast EVERY subsequent request for the break window. Serial execution alone does not prevent that
// bleed — a green test running within the break window would spuriously get a 500 "circuit open". Ordering
// the fault collection last means the breaker only opens once nothing green remains to run, so the bleed is
// harmless and the suite is deterministic regardless of the run's timing.
[assembly: TestCollectionOrderer("MaxioPassthroughApiTests.FaultLastCollectionOrderer", "MaxioApiTests")]

namespace MaxioPassthroughApiTests;

/// <summary>
/// Orders test collections so the breaker-tripping <see cref="Tests.ServerFaultTests"/> collection runs after
/// every other collection (see the assembly attribute above). All other collections keep a stable
/// alphabetical order.
/// </summary>
public sealed class FaultLastCollectionOrderer : ITestCollectionOrderer
{
    public IEnumerable<ITestCollection> OrderTestCollections(IEnumerable<ITestCollection> testCollections) =>
        testCollections
            .OrderBy(c => c.DisplayName.Contains("ServerFault", StringComparison.Ordinal) ? 1 : 0)
            .ThenBy(c => c.DisplayName, StringComparer.Ordinal);
}
