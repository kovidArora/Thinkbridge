using Microsoft.Extensions.Options;

namespace Quotes.Tests.Unit;

public class TestOptionsSnapshot<T> : IOptionsSnapshot<T> where T : class
{
    public TestOptionsSnapshot(T value)
    {
        Value = value;
    }

    public T Value { get; }

    public T Get(string? name) => Value;
}
