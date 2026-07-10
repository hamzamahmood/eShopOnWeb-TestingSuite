using Xunit.Abstractions;

namespace MaxioApiTests;

/// <summary>
/// Ambient per-test <see cref="ITestOutputHelper"/> so the static <see cref="Expect"/> helpers can emit PASS
/// lines on a passing assertion — symmetric with the intent-bearing failure messages they already produce.
///
/// <para>xUnit v2 has no <c>TestContext.Current.TestOutputHelper</c> ambient, so the helper is injected into
/// each test class's constructor and captured here by <see cref="BlackBoxTest"/>. It is held in an
/// <see cref="AsyncLocal{T}"/> so it flows from the (per-test) constructor into that test's async method and
/// stays isolated across xUnit's parallel test execution.</para>
/// </summary>
internal static class TestOutput
{
    private static readonly AsyncLocal<ITestOutputHelper?> _current = new();

    /// <summary>The output helper for the currently executing test, or <c>null</c> if none was captured.</summary>
    public static ITestOutputHelper? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }
}
