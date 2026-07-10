using Xunit.Abstractions;

namespace MaxioApiTests;

/// <summary>
/// Base class for every black-box test class in the suite. Its constructor captures the xUnit-injected
/// output helper into the ambient TestOutput so the assertion helpers can emit a line per assertion.
/// </summary>
public abstract class BlackBoxTest
{
    protected BlackBoxTest(ITestOutputHelper output) => TestOutput.Current = output;
}
