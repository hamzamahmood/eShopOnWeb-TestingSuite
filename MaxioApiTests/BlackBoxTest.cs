using Xunit.Abstractions;

namespace MaxioApiTests;

/// <summary>
/// Base class for every black-box test class in the suite. Its constructor captures the xUnit-injected
/// <see cref="ITestOutputHelper"/> into the ambient <see cref="TestOutput"/>, so the static <see cref="Expect"/>
/// helpers can emit a PASS line for each passing assertion (see <see cref="TestOutput"/>).
///
/// <para>xUnit constructs the test class once per test method, immediately before invoking that method, so the
/// captured helper is always the one belonging to the test about to run.</para>
/// </summary>
public abstract class BlackBoxTest
{
    protected BlackBoxTest(ITestOutputHelper output) => TestOutput.Current = output;
}
