namespace ColumnPadStudio.SmokeTests;

internal sealed class SmokeTestContext
{
    private readonly List<string> _failures = [];

    public int CheckCount { get; private set; }

    public void Check(bool condition, string message)
    {
        CheckCount++;
        if (!condition)
            _failures.Add(message);
    }

    public int Complete()
    {
        if (_failures.Count == 0)
        {
            Console.WriteLine($"Smoke tests passed ({CheckCount} checks).");
            return 0;
        }

        Console.Error.WriteLine($"Smoke tests failed: {_failures.Count} of {CheckCount} checks.");
        foreach (var failure in _failures)
            Console.Error.WriteLine($" - {failure}");

        return 1;
    }
}
