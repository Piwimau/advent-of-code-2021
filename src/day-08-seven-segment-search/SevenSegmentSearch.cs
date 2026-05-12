using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BenchmarkDotNet.Running;
using CommunityToolkit.Diagnostics;

namespace SevenSegmentSearch;

internal sealed partial class SevenSegmentSearch {

    /// <summary>
    /// Represents an <see cref="Entry"/> used for decoding the seven-segment display.
    /// </summary>
    /// <param name="SignalDigits">Sequence of ten signal digits of the <see cref="Entry"/>.</param>
    /// <param name="OutputDigits">
    /// Sequence of four output digits of the <see cref="Entry"/>.
    /// </param>
    private readonly partial record struct Entry(
        ImmutableArray<string> SignalDigits,
        ImmutableArray<string> OutputDigits
    ) {

        [GeneratedRegex("^(?:[a-g]{2,} ){10}\\|(?: [a-g]{2,}){4}$")]
        private static partial Regex EntryRegex();

        /// <summary>Parses an <see cref="Entry"/> from a given string.</summary>
        /// <remarks>
        /// The string <paramref name="s"/> must have the following format, as described by
        /// <see cref="EntryRegex"/>:
        /// <list type="bullet">
        ///     <item>
        ///     It begins with a sequence of ten unique, space-separated signal digits (containing
        ///     only lowercase characters 'a' through 'g' each).
        ///     </item>
        ///     <item>
        ///     The signal digits are followed by a space, a separator '|' and another space.
        ///     </item>
        ///     <item>
        ///     Finally, the string contains four space-separated output digits (each with the same
        ///     format as one of the signal digits). These must not be unique, i. e. the same output
        ///     digit may appear one or more times.
        ///     </item>
        /// </list>
        /// An example for a string representing a valid <see cref="Entry"/> might be the following:
        /// <example>
        /// <code>
        /// "acedgfb cdfbe gcdfa fbcad dab cefabd cdfgeb eafb cagedb ab | cdfeb fcadb cdfeb cdbaf"
        /// </code>
        /// </example>
        /// </remarks>
        /// <param name="s">String to parse an <see cref="Entry"/> from.</param>
        /// <returns>An <see cref="Entry"/> parsed from the given string.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="s"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="s"/> has an invalid format.
        /// </exception>
        public static Entry Parse(string s) {
            Guard.IsNotNull(s);
            if (!EntryRegex().IsMatch(s)) {
                throw new ArgumentOutOfRangeException(
                    nameof(s),
                    $"The string \"{s}\" does not represent a valid entry."
                );
            }
            ReadOnlySpan<string> parts = s.Split(" | ");
            ImmutableArray<string> signalDigits = [.. parts[0].Split(' ')];
            ImmutableArray<string> outputDigits = [.. parts[1].Split(' ')];
            return new Entry(signalDigits, outputDigits);
        }

    }

    private static readonly string InputFile = Path.Combine(
        AppContext.BaseDirectory,
        "resources",
        "input.txt"
    );

    /// <summary>Decodes the seven-segment display using a given sequence of entries.</summary>
    /// <param name="entries">Sequence of entries for decoding the seven-segment display.</param>
    /// <returns>
    /// A tuple containing the number of directly identifiable output digits, as well as the total
    /// sum of all decoded output values.
    /// </returns>
    private static (int IdentifiableOutputDigits, int SumOfOutputValues) DecodeDisplay(
        ReadOnlySpan<Entry> entries
    ) {
        int identifiableOutputDigits = 0;
        int sumOfOutputValues = 0;
        foreach (Entry entry in entries) {
            string oneDigit = entry.SignalDigits.First(signalDigit => signalDigit.Length == 2);
            string fourDigit = entry.SignalDigits.First(signalDigit => signalDigit.Length == 4);
            int outputValue = 0;
            foreach (string outputDigit in entry.OutputDigits) {
                int outputDigitLength = outputDigit.Length;
                if ((outputDigitLength == 2) || (outputDigitLength == 3)
                        || (outputDigitLength == 4) || (outputDigitLength == 7)) {
                    identifiableOutputDigits++;
                }
                int commonWithOneDigit = outputDigit.Count(oneDigit.Contains);
                int commonWithFourDigit = outputDigit.Count(fourDigit.Contains);
                outputValue = (outputValue * 10)
                    + (outputDigitLength, commonWithOneDigit, commonWithFourDigit) switch {
                        (6, 2, 3) => 0,
                        (2, _, _) => 1,
                        (5, 1, 2) => 2,
                        (5, 2, 3) => 3,
                        (4, _, _) => 4,
                        (5, 1, 3) => 5,
                        (6, 1, 3) => 6,
                        (3, _, _) => 7,
                        (7, _, _) => 8,
                        (6, 2, 4) => 9,
                        (_, _, _) => throw new InvalidOperationException("Unreachable.")
                    };
            }
            sumOfOutputValues += outputValue;
        }
        return (identifiableOutputDigits, sumOfOutputValues);
    }

    /// <summary>Solves the <see cref="SevenSegmentSearch"/> puzzle.</summary>
    /// <param name="textWriter"><see cref="TextWriter"/> to write the results to.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="textWriter"/> is <see langword="null"/>.
    /// </exception>
    internal static void Solve(TextWriter textWriter) {
        Guard.IsNotNull(textWriter);
        ReadOnlySpan<Entry> entries = [.. File.ReadLines(InputFile).Select(Entry.Parse)];
        (int identifiableOutputDigits, int sumOfOutputValues) = DecodeDisplay(entries);
        textWriter.WriteLine(
            $"{identifiableOutputDigits} output digits are directly identifiable."
        );
        textWriter.WriteLine($"The sum of the output values is {sumOfOutputValues}.");
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