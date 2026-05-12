using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Running;
using CommunityToolkit.Diagnostics;

namespace Snailfish;

internal sealed class Snailfish {

    /// <summary>Represents an individual <see cref="SnailfishNumber"/>.</summary>
    private sealed class SnailfishNumber {

        /// <summary>
        /// Represents a <see cref="Node"/> as part of a <see cref="SnailfishNumber"/>.
        /// </summary>
        private readonly record struct Node {

            /// <summary>Maximum possible depth of a <see cref="Node"/> after reduction.</summary>
            public const int MaxDepthAfterReduction = 3;

            /// <summary>Minimum possible value of a <see cref="Node"/> after reduction.</summary>
            public const int MinValueAfterReduction = 0;

            /// <summary>Maximum possible value of a <see cref="Node"/> after reduction.</summary>
            public const int MaxValueAfterReduction = 9;

            /// <summary>Gets the value of this <see cref="Node"/>.</summary>
            public int Value { get; init; }

            /// <summary>Gets the depth of this <see cref="Node"/>.</summary>
            public int Depth { get; init; }

            /// <summary>
            /// Initializes a new <see cref="Node"/> with a given value and depth.
            /// </summary>
            /// <param name="value">Value of the <see cref="Node"/>.</param>
            /// <param name="depth">Depth of the <see cref="Node"/>.</param>
            /// <exception cref="ArgumentOutOfRangeException">
            /// Thrown when <paramref name="value"/> or <paramref name="depth"/> is negative.
            /// </exception>
            public Node(int value, int depth) {
                Guard.IsGreaterThanOrEqualTo(value, 0);
                Guard.IsGreaterThanOrEqualTo(depth, 0);
                Value = value;
                Depth = depth;
            }

        }

        /// <summary>Array of nodes of this <see cref="SnailfishNumber"/>.</summary>
        private readonly List<Node> nodes;

        /// <summary>
        /// Initializes a new <see cref="SnailfishNumber"/> with a given sequence of nodes.
        /// </summary>
        /// <param name="nodes">Sequence of nodes of the <see cref="SnailfishNumber"/>.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="nodes"/> is <see langword="null"/>
        /// </exception>
        private SnailfishNumber(IEnumerable<Node> nodes) {
            Guard.IsNotNull(nodes);
            this.nodes = [.. nodes];
        }

        /// <summary>Parses a <see cref="SnailfishNumber"/> from a given string.</summary>
        /// <remarks>
        /// The string <paramref name="s"/> must only contain square braces, digits or commas.
        /// </remarks>
        /// <param name="s">String to parse a <see cref="SnailfishNumber"/> from.</param>
        /// <returns>A <see cref="SnailfishNumber"/> parsed from the given string.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="s"/> has an invalid format.
        /// </exception>
        public static SnailfishNumber Parse(ReadOnlySpan<char> s) {
            List<Node> nodes = [];
            int depth = -1;
            foreach (char c in s) {
                switch (c) {
                    case '[':
                        depth++;
                        break;
                    case ']':
                        depth--;
                        break;
                    default:
                        if ((c >= '0') && (c <= '9')) {
                            nodes.Add(new Node(c - '0', depth));
                        }
                        else if (c != ',') {
                            throw new ArgumentOutOfRangeException(
                                nameof(s),
                                $"The string \"{s}\" does not represent a valid snailfish number."
                            );
                        }
                        break;
                }
            }
            return new SnailfishNumber(nodes);
        }

        /// <summary>Returns a deep copy of this <see cref="SnailfishNumber"/>.</summary>
        /// <returns>A deep copy of this <see cref="SnailfishNumber"/>.</returns>
        public SnailfishNumber Clone() => new(nodes);

        /// <summary>
        /// Explodes this <see cref="SnailfishNumber"/>, which is the first part of the reduction
        /// algorithm used while adding two snailfish numbers.
        /// </summary>
        /// <returns>
        /// <see langword="True"/> if this <see cref="SnailfishNumber"/> changed,
        /// otherwise <see langword="false"/>.
        /// </returns>
        private bool Explode() {
            for (int i = 0; i < nodes.Count; i++) {
                Node node = nodes[i];
                if (node.Depth > Node.MaxDepthAfterReduction) {
                    if (i > 0) {
                        nodes[i - 1] = nodes[i - 1] with {
                            Value = nodes[i - 1].Value + node.Value
                        };
                    }
                    if (i < nodes.Count - 2) {
                        nodes[i + 2] = nodes[i + 2] with {
                            Value = nodes[i + 1].Value + nodes[i + 2].Value
                        };
                    }
                    nodes[i] = new Node(0, Node.MaxDepthAfterReduction);
                    nodes.RemoveAt(i + 1);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Splits this <see cref="SnailfishNumber"/>, which is the second part of the reduction
        /// algorithm used while adding two snailfish numbers.
        /// </summary>
        /// <returns>
        /// <see langword="True"/> if this <see cref="SnailfishNumber"/> changed,
        /// otherwise <see langword="false"/>.
        /// </returns>
        private bool Split() {
            for (int i = 0; i < nodes.Count; i++) {
                Node node = nodes[i];
                if (node.Value > Node.MaxValueAfterReduction) {
                    nodes[i] = new Node(node.Value / 2, node.Depth + 1);
                    nodes.Insert(
                        i + 1,
                        new Node(
                            (node.Value / 2) + ((node.Value % 2 != 0) ? 1 : 0),
                            node.Depth + 1
                        )
                    );
                    return true;
                }
            }
            return false;
        }

        /// <summary>Adds a given <see cref="SnailfishNumber"/> to this instance.</summary>
        /// <remarks>The given <see cref="SnailfishNumber"/> remains unchanged.</remarks>
        /// <param name="other"><see cref="SnailfishNumber"/> to add to this instance.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="other"/> is <see langword="null"/>.
        /// </exception>
        public void Add(SnailfishNumber other) {
            Guard.IsNotNull(other);
            nodes.AddRange(other.nodes);
            for (int i = 0; i < nodes.Count; i++) {
                nodes[i] = nodes[i] with { Depth = nodes[i].Depth + 1 };
            }
            while (Explode() || Split()) { }
        }

        /// <summary>Returns the magnitude of this <see cref="SnailfishNumber"/>.</summary>
        /// <returns>The magnitude of this <see cref="SnailfishNumber"/>.</returns>
        public int Magnitude() {
            List<Node> nodes = [.. this.nodes];
            while (nodes.Count > 1) {
                for (int i = 0; i < (nodes.Count - 1); i++) {
                    Node node = nodes[i];
                    Node next = nodes[i + 1];
                    if (node.Depth == next.Depth) {
                        int value = (3 * node.Value) + (2 * next.Value);
                        int depth = Math.Max(Node.MinValueAfterReduction, node.Depth - 1);
                        nodes[i] = new Node(value, depth);
                        nodes.RemoveAt(i + 1);
                        break;
                    }
                }
            }
            return nodes[0].Value;
        }

    }

    private static readonly string InputFile = Path.Combine(
        AppContext.BaseDirectory,
        "resources",
        "input.txt"
    );

    /// <summary>
    /// Returns the magnitude of the total sum of a given sequence of snailfish numbers.
    /// </summary>
    /// <param name="numbers">Sequence of snailfish numbers for the calculation.</param>
    /// <returns>
    /// The magnitude of the total sum of the given sequence of snailfish numbers.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="numbers"/> is empty.
    /// </exception>
    private static int MagnitudeOfTotalSum(ReadOnlySpan<SnailfishNumber> numbers) {
        if (numbers.IsEmpty) {
            throw new ArgumentOutOfRangeException(
                nameof(numbers),
                "Cannot calculate the magnitude of the total sum of an empty sequence of "
                    + "snailfish numbers."
            );
        }
        if (numbers.Length == 1) {
            return numbers[0].Magnitude();
        }
        // Addition of snailfish numbers is a destructive (mutating) operation, so we make a deep
        // copy here to prevent modification of the original number.
        SnailfishNumber totalSum = numbers[0].Clone();
        foreach (SnailfishNumber number in numbers[1..]) {
            totalSum.Add(number);
        }
        return totalSum.Magnitude();
    }

    /// <summary>
    /// Returns the largest magnitude of the sum of any two different snailfish numbers in a given
    /// sequence.
    /// </summary>
    /// <param name="numbers">Sequence of snailfish numbers for the calculation.</param>
    /// <returns>
    /// The largest magnitude of the sum of any two different snailfish numbers in the given
    /// sequence.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="numbers"/> is empty.
    /// </exception>
    private static int LargestMagnitudeOfSumOfAnyTwoDifferent(
        ReadOnlySpan<SnailfishNumber> numbers
    ) {
        if (numbers.IsEmpty) {
            throw new ArgumentOutOfRangeException(
                nameof(numbers),
                "Cannot calculate the largest magnitude of the sum of any two different "
                    + "snailfish numbers in an empty sequence."
            );
        }
        int largestMagnitude = int.MinValue;
        for (int i = 0; i < numbers.Length; i++) {
            for (int j = 0; j < numbers.Length; j++) {
                if (i != j) {
                    // Addition of snailfish numbers is a destructive (mutating) operation, so we
                    // make a deep copy here to prevent modification of the original number.
                    SnailfishNumber sum = numbers[i].Clone();
                    sum.Add(numbers[j]);
                    largestMagnitude = Math.Max(largestMagnitude, sum.Magnitude());
                    // Addition of snailfish numbers is also not generally commutative, i. e.
                    // (a + b) is not necessarily equal to (b + a). We therefore also need to
                    // consider the sum in reversed order, once again making a deep copy (of the
                    // second number this time).
                    sum = numbers[j].Clone();
                    sum.Add(numbers[i]);
                    largestMagnitude = Math.Max(largestMagnitude, sum.Magnitude());
                }
            }
        }
        return largestMagnitude;
    }

    /// <summary>Solves the <see cref="Snailfish"/> puzzle.</summary>
    /// <param name="textWriter"><see cref="TextWriter"/> to write the results to.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="textWriter"/> is <see langword="null"/>.
    /// </exception>
    internal static void Solve(TextWriter textWriter) {
        Guard.IsNotNull(textWriter);
        ReadOnlySpan<SnailfishNumber> numbers = [.. File.ReadLines(InputFile)
            .Select(line => SnailfishNumber.Parse(line))
        ];
        int magnitudeOfTotalSum = MagnitudeOfTotalSum(numbers);
        int largestMagnitude = LargestMagnitudeOfSumOfAnyTwoDifferent(numbers);
        textWriter.WriteLine(
            $"The magnitude of the total sum of numbers is {magnitudeOfTotalSum}."
            );
        textWriter.WriteLine(
            $"The largest magnitude of the sum of any two different numbers is {largestMagnitude}."
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