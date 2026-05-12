using System.Diagnostics.CodeAnalysis;
using System.IO;
using BenchmarkDotNet.Attributes;

namespace ArithmeticLogicUnit;

/// <summary>
/// Represents a <see cref="Benchmark"/> for the <see cref="ArithmeticLogicUnit"/> puzzle.
/// </summary>
[MemoryDiagnoser]
public class Benchmark {

    /// <summary>Runs a benchmark for the <see cref="ArithmeticLogicUnit"/> puzzle.</summary>
    [Benchmark]
    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Benchmarking static methods is not supported."
    )]
    public void Run() => ArithmeticLogicUnit.Solve(TextWriter.Null);

}