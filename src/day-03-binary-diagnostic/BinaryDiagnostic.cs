using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Running;
using CommunityToolkit.Diagnostics;

namespace BinaryDiagnostic;

internal sealed class BinaryDiagnostic {

    /// <summary>Represents an individual <see cref="Bit"/>.</summary>
    private enum Bit { Zero, One }

    /// <summary>Numbers of bits in each measurement.</summary>
    private const int BitsPerMeasurement = 12;

    private static readonly string InputFile = Path.Combine(
        AppContext.BaseDirectory,
        "resources",
        "input.txt"
    );

    /// <summary>
    /// Determines the most common <see cref="Bit"/> at a given index in a sequence of measurements.
    /// </summary>
    /// <param name="measurements">Sequence of measurements for the calculation.</param>
    /// <param name="index">
    /// Index of the bit to check (in the range [0; <see cref="BitsPerMeasurement"/> - 1]).
    /// Zero marks the least significant bit.
    /// </param>
    /// <returns>
    /// A tuple containing the most common <see cref="Bit"/> and an information indicating whether
    /// <see cref="Bit.Zero"/> and <see cref="Bit.One"/> are equally common.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="index"/> is out of range.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (Bit MostCommonBit, bool EquallyCommon) MostCommonBit(
        ReadOnlySpan<uint> measurements,
        int index
    ) {
        Guard.IsInRange(index, 0, BitsPerMeasurement);
        Span<int> counts = stackalloc int[2];
        foreach (uint measurement in measurements) {
            counts[(int) ((measurement >>> index) & 1U)]++;
        }
        Bit mostCommonBit = counts[0] > counts[1] ? Bit.Zero : Bit.One;
        bool equallyCommon = counts[0] == counts[1];
        return (mostCommonBit, equallyCommon);
    }

    /// <summary>Determines the gamma value based on a given sequence of measurements.</summary>
    /// <param name="measurements">Sequence of measurements for the calculation.</param>
    /// <returns>The gamma value based on the given sequence of measurements.</returns>
    private static uint Gamma(ReadOnlySpan<uint> measurements) {
        uint gamma = 0;
        for (int index = BitsPerMeasurement - 1; index >= 0; index--) {
            (Bit mostCommonBit, _) = MostCommonBit(measurements, index);
            gamma |= (uint) mostCommonBit << index;
        }
        return gamma;
    }

    /// <summary>Determines the epsilon value based on a given gamma value.</summary>
    /// <param name="gamma">Gamma value for the calculation.</param>
    /// <returns>The epsilon value based on the given gamma value.</returns>
    private static uint Epsilon(uint gamma) => ~gamma & ((1U << BitsPerMeasurement) - 1U);

    /// <summary>
    /// Determines if a given measurement has a specified <see cref="Bit"/> at a given index.
    /// </summary>
    /// <param name="measurement">Measurement to check.</param>
    /// <param name="bit">
    /// Bit to check for, either <see cref="Bit.Zero"/> or <see cref="Bit.One"/>.
    /// </param>
    /// <param name="index">
    /// Index of the bit to check (in the range [0; <see cref="BitsPerMeasurement"/> - 1]).
    /// Zero marks the least significant bit.
    /// </param>
    /// <returns>
    /// <see langword="True"/> if the given measurement has specified <see cref="Bit"/> at the given
    /// index, otherwise <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="index"/> is out of range.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool MeasurementHasBit(uint measurement, Bit bit, int index) {
        Guard.IsInRange(index, 0, BitsPerMeasurement);
        return ((measurement >>> index) & 1U) == (uint) bit;
    }

    /// <summary>Determines the oxygen value based on a given sequence of measurements.</summary>
    /// <param name="measurements">Sequence of measurements for the calculation.</param>
    /// <returns>The oxygen value based on the given sequence of measurements.</returns>
    private static uint Oxygen(ReadOnlySpan<uint> measurements) {
        ImmutableArray<uint> oxygenValues = [.. measurements];
        for (int index = BitsPerMeasurement - 1; oxygenValues.Length > 1; index--) {
            (Bit mostCommonBit, bool equallyCommon) = MostCommonBit(oxygenValues.AsSpan(), index);
            oxygenValues = oxygenValues.RemoveAll(measurement =>
                !MeasurementHasBit(measurement, mostCommonBit, index)
                    && !(equallyCommon && MeasurementHasBit(measurement, Bit.One, index))
            );
        }
        return oxygenValues[0];
    }

    /// <summary>Determines the CO2 value based on a given sequence of measurements.</summary>
    /// <param name="measurements">Sequence of measurements for the calculation.</param>
    /// <returns>The CO2 value based on the given sequence of measurements.</returns>
    private static uint CO2(ReadOnlySpan<uint> measurements) {
        ImmutableArray<uint> co2Values = [.. measurements];
        for (int index = BitsPerMeasurement - 1; co2Values.Length > 1; index--) {
            (Bit mostCommonBit, bool equallyCommon) = MostCommonBit(co2Values.AsSpan(), index);
            Bit leastCommonBit = mostCommonBit == Bit.One ? Bit.Zero : Bit.One;
            co2Values = co2Values.RemoveAll(measurement =>
                !MeasurementHasBit(measurement, leastCommonBit, index)
                    && !(equallyCommon && MeasurementHasBit(measurement, Bit.Zero, index))
            );
        }
        return co2Values[0];
    }

    /// <summary>Solves the <see cref="BinaryDiagnostic"/> puzzle.</summary>
    /// <param name="textWriter"><see cref="TextWriter"/> to write the results to.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="textWriter"/> is <see langword="null"/>.
    /// </exception>
    internal static void Solve(TextWriter textWriter) {
        Guard.IsNotNull(textWriter);
        ReadOnlySpan<uint> measurements = [.. File.ReadLines(InputFile)
            .Select(line => Convert.ToUInt32(line, 2))
        ];
        uint gamma = Gamma(measurements);
        uint epsilon = Epsilon(gamma);
        uint oxygen = Oxygen(measurements);
        uint co2 = CO2(measurements);
        textWriter.WriteLine($"The power consumption of the submarine is {gamma * epsilon}.");
        textWriter.WriteLine($"The life support rating of the submarine is {oxygen * co2}.");
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