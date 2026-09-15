using System;
using System.Collections.Generic;
using System.Linq;

namespace HelloReactor;

/// <summary>
/// Deliberately ordinary code. The point is only that there are enough real
/// method bodies for NecroBit to encrypt — a class with one trivial method can
/// be optimised down to almost nothing and makes a poor reproduction.
/// </summary>
public sealed class Greeter
{
    private readonly string _name;

    public Greeter(string name) => _name = name ?? throw new ArgumentNullException(nameof(name));

    public string Greet() => $"Hello, {_name}!";

    public IReadOnlyList<string> GreetMany(IEnumerable<string> names)
    {
        if (names is null) throw new ArgumentNullException(nameof(names));
        return names.Where(n => !string.IsNullOrWhiteSpace(n))
                    .Select(n => new Greeter(n).Greet())
                    .ToList();
    }

    public int Fibonacci(int n)
    {
        if (n < 0) throw new ArgumentOutOfRangeException(nameof(n));
        int a = 0, b = 1;
        for (int i = 0; i < n; i++) (a, b) = (b, a + b);
        return a;
    }

    public Dictionary<string, int> WordLengths(string sentence)
        => (sentence ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries)
                           .GroupBy(w => w)
                           .ToDictionary(g => g.Key, g => g.Key.Length);
}
