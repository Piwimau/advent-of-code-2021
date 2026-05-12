using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Running;
using CommunityToolkit.Diagnostics;
using Microsoft.CodeAnalysis;

namespace SonarSweep;

internal sealed class SonarSweep {

    private static readonly string InputFile = Path.Combine(
        AppContext.BaseDirectory,
        "resources",
        "input.txt"
    );

    /// <summary>
    /// Counts the number of depth increases in a given sequence of depths using a sliding window of
    /// a given size.
    /// </summary>
    /// <param name="depths">Sequence of depths for the calculation.</param>
    /// <param name="slidingWindowSize">Positive size of the sliding window used.</param>
    /// <returns>
    /// The number of depth increases in the given sequence of depths using a sliding window of the
    /// given size.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="slidingWindowSize"/> is negative.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CountDepthIncreases(ReadOnlySpan<int> depths, int slidingWindowSize) {
        Guard.IsGreaterThanOrEqualTo(slidingWindowSize, 0);
        int depthIncreases = 0;
        for (int i = slidingWindowSize; i < depths.Length; i++) {
            // Two sliding windows of size N share N - 1 items. We therefore only need to compare
            // the two remaining items at the edges to find out if the depth increased.
            if (depths[i] > depths[i - slidingWindowSize]) {
                depthIncreases++;
            }
        }
        return depthIncreases;
    }

    /// <summary>Solves the <see cref="SonarSweep"/> puzzle.</summary>
    /// <param name="textWriter"><see cref="TextWriter"/> to write the results to.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="textWriter"/> is <see langword="null"/>.
    /// </exception>
    internal static void Solve(TextWriter textWriter) {
        Guard.IsNotNull(textWriter);
        ReadOnlySpan<int> depths = [.. File.ReadLines(InputFile).Select(int.Parse)];
        int countOne = CountDepthIncreases(depths, 1);
        int countThree = CountDepthIncreases(depths, 3);
        textWriter.WriteLine(
            $"{countOne} measurements are larger than the previous measurement."
        );
        textWriter.WriteLine(
            $"{countThree} measurements are larger than the previous three measurements."
        );
    }

    private static void Main(string[] args) {
        if (args.Contains("--benchmark")) {
            BenchmarkRunner.Run<Benchmark>();
        }
        else {
            Solve(Console.Out);
        }
    }

}