using Xunit.Abstractions;

namespace MaxioApiTests;

/// <summary>Ambient test output helper for assertion messages.</summary>
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
