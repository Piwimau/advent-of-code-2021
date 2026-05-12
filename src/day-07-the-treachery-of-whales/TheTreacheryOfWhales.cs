using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Running;
using CommunityToolkit.Diagnostics;

namespace TheTreacheryOfWhales;

internal sealed class TheTreacheryOfWhales {

    private static readonly string InputFile = Path.Combine(
        AppContext.BaseDirectory,
        "resources",
        "input.txt"
    );

    /// <summary>
    /// Determines the total fuel required to align a given sequence of submarines to a specified
    /// target position using a constant burn rate of fuel.
    /// </summary>
    /// <param name="positions">Sequence of the positions of the submarines.</param>
    /// <param name="target">Target position to which all submarines should be aligned.</param>
    /// <returns>
    /// The total fuel required to align the given sequence of submarines to the specified target
    /// position using a constant burn rate of fuel.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FuelConstant(ReadOnlySpan<int> positions, int target) {
        int fuel = 0;
        foreach (int position in positions) {
            // We assume a constant burn rate, in which case the fuel cost is simply proportional
            // to the distance of the submarine from the target position.
            fuel += Math.Abs(target - position);
        }
        return fuel;
    }

    /// <summary>
    /// Determines the total fuel required to align a given sequence of submarines to a specified
    /// target position using a dynamic burn rate of fuel.
    /// </summary>
    /// <param name="positions">Sequence of the positions of the submarines.</param>
    /// <param name="target">Target position to which all submarines should be aligned.</param>
    /// <returns>
    /// The total fuel required to align the given sequence of submarines to the specified target
    /// position using a dynamic burn rate of fuel.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FuelDynamic(ReadOnlySpan<int> positions, int target) {
        int fuel = 0;
        foreach (int position in positions) {
            // We assume a dynamic burn rate, for which the fuel cost increases by one with each
            // additional step required to reach the target position. This is effectively the sum of
            // the first N natural numbers, or, in this case, the range [1; distance]. Note however
            // that the submarine may already be at the target position (distance = 0), in which
            // case we simply get no additional fuel cost using our formula here.
            int distance = Math.Abs(target - position);
            fuel += distance * (distance + 1) / 2;
        }
        return fuel;
    }

    /// <summary>Aligns a given sequence of submarines to an optimal target position.</summary>
    /// <param name="positions">Sequence of the positions of the submarines.</param>
    /// <returns>
    /// A tuple containing the minimum fuel required to reach the optimal target position,
    /// once assuming a constant and once a dynamic burn rate of fuel.
    /// </returns>
    private static (int MinFuelConstant, int MinFuelDynamic) AlignSubmarines(
        ReadOnlySpan<int> positions
    ) {
        if (positions.IsEmpty) {
            return (0, 0);
        }
        int minPosition = int.MaxValue;
        int maxPosition = int.MinValue;
        foreach (int position in positions) {
            minPosition = Math.Min(minPosition, position);
            maxPosition = Math.Max(maxPosition, position);
        }
        int minFuelConstant = int.MaxValue;
        int minFuelDynamic = int.MaxValue;
        foreach (int position in Enumerable.Range(minPosition, maxPosition - minPosition + 1)) {
            minFuelConstant = Math.Min(minFuelConstant, FuelConstant(positions, position));
            minFuelDynamic = Math.Min(minFuelDynamic, FuelDynamic(positions, position));
        }
        return (minFuelConstant, minFuelDynamic);
    }

    /// <summary>Solves the <see cref="TheTreacheryOfWhales"/> puzzle.</summary>
    /// <param name="textWriter"><see cref="TextWriter"/> to write the results to.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="textWriter"/> is <see langword="null"/>.
    /// </exception>
    internal static void Solve(TextWriter textWriter) {
        Guard.IsNotNull(textWriter);
        ReadOnlySpan<int> positions = [.. File.ReadAllText(InputFile).Split(',')
            .Select(int.Parse)
        ];
        (int minFuelConstant, int minFuelDynamic) = AlignSubmarines(positions);
        textWriter.WriteLine(
            $"Minimum fuel required at a constant burn rate is {minFuelConstant}."
        );
        textWriter.WriteLine($"Minimum fuel required at a dynamic burn rate is {minFuelDynamic}.");
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