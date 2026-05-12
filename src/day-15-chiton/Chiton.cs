using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Running;
using CommunityToolkit.Diagnostics;

namespace Chiton;

internal sealed class Chiton {

    /// <summary>Represents a <see cref="Map"/> of risk levels used for pathfinding.</summary>
    private sealed class Map {

        /// <summary>Represents an enumeration of all possible directions.</summary>
        private enum Direction { Left, Up, Right, Down }

        /// <summary>Represents a two-dimensional <see cref="Position"/>.</summary>
        /// <param name="X">X-coordinate of the <see cref="Position"/>.</param>
        /// <param name="Y">Y-coordinate of the <see cref="Position"/>.</param>
        private readonly record struct Position(int X, int Y);

        /// <summary>Minimum possible risk level.</summary>
        private const int MinRiskLevel = 1;

        /// <summary>Maximum possible risk level.</summary>
        private const int MaxRiskLevel = 9;

        /// <summary>Factor by which a <see cref="Map"/> of risk levels is expanded.</summary>
        private const int ExpansionFactor = 5;

        /// <summary>Array of all possible directions, cached for efficiency.</summary>
        private static readonly ImmutableArray<Direction> Directions = [
            .. Enum.GetValues<Direction>()
        ];

        /// <summary>Array of the risk levels of this <see cref="Map"/>.</summary>
        /// <remarks>
        /// Note that this is actually a two-dimensional array stored as a one-dimensional
        /// one (in row-major order) for reasons of improved performance and cache locality.
        /// </remarks>
        private readonly ImmutableArray<int> riskLevels;

        /// <summary>Width of this <see cref="Map"/>.</summary>
        private readonly int width;

        /// <summary>Height of this <see cref="Map"/>.</summary>
        private readonly int height;

        /// <summary>
        /// Initializes a new <see cref="Map"/> with a given map of risk levels.
        /// </summary>
        /// <param name="riskLevels">Map of risk levels for the initialization.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="riskLevels"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="riskLevels"/> contains an invalid risk level.
        /// All risk levels must be in the range
        /// [<see cref="MinRiskLevel"/>; <see cref="MaxRiskLevel"/>].
        /// </exception>
        private Map(int[][] riskLevels) {
            Guard.IsNotNull(riskLevels);
            this.riskLevels = [.. riskLevels.SelectMany(row => row)];
            if (this.riskLevels.Any(level => level < MinRiskLevel || level > MaxRiskLevel)) {
                throw new ArgumentOutOfRangeException(
                    nameof(riskLevels),
                    "The following sequence contains at least one invalid risk level. "
                        + $"All risk levels must be in the range [{MinRiskLevel}; "
                        + $"{MaxRiskLevel}].{Environment.NewLine}"
                        + $"[{string.Join(", ", this.riskLevels)}]"
                );
            }
            width = riskLevels[0].Length;
            height = riskLevels.Length;
        }

        /// <summary>Parses a <see cref="Map"/> from a given string.</summary>
        /// <remarks>
        /// The string <paramref name="s"/> must contain zero or more newline-separated lines
        /// representing the rows of the <see cref="Map"/>. All of these rows must have the
        /// same length and consist only of digits '1' through '9'.<br/>
        /// An example for a string representing a valid <see cref="Map"/> might be the following
        /// (with actual newlines rendered):
        /// <example>
        /// <code>
        /// 1163751742
        /// 1381373672
        /// 2136511328
        /// 3694931569
        /// 7463417111
        /// 1319128137
        /// 1359912421
        /// 3125421639
        /// 1293138521
        /// 2311944581
        /// </code>
        /// </example>
        /// </remarks>
        /// <param name="s">String to parse a <see cref="Map"/> from.</param>
        /// <returns>A <see cref="Map"/> parsed from the given string.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="s"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="s"/> contains an invalid risk level.
        /// </exception>
        public static Map Parse(string s) {
            Guard.IsNotNull(s);
            int[][] riskLevels = [
                .. s.Split(Environment.NewLine).Select(line => line.Select(c => c - '0').ToArray())
            ];
            return new Map(riskLevels);
        }

        /// <summary>
        /// Returns the index of a risk level at a given <see cref="Position"/> in this
        /// <see cref="Map"/>.
        /// </summary>
        /// <param name="position">
        /// <see cref="Position"/> of the risk level to get the index of.
        /// </param>
        /// <returns>
        /// The index of the risk level at the given <see cref="Position"/> in this
        /// <see cref="Map"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int Index(Position position) => (position.Y * width) + position.X;

        /// <summary>
        /// Determines all existing neighbors of a given <see cref="Position"/> in this
        /// <see cref="Map"/>.
        /// </summary>
        /// <param name="position">
        /// <see cref="Position"/> to determine all existing neighbors of.
        /// </param>
        /// <returns>
        /// All existing neighbors of the given <see cref="Position"/> in this <see cref="Map"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private IEnumerable<Position> Neighbors(Position position) {
            foreach (Direction direction in Directions) {
                Position neighbor = direction switch {
                    Direction.Left => position with { X = position.X - 1 },
                    Direction.Up => position with { Y = position.Y - 1 },
                    Direction.Right => position with { X = position.X + 1 },
                    Direction.Down => position with { Y = position.Y + 1 },
                    _ => throw new InvalidOperationException("Unreachable."),
                };
                if ((neighbor.X >= 0) && (neighbor.X < width)
                        && (neighbor.Y >= 0) && (neighbor.Y < height)) {
                    yield return neighbor;
                }
            }
        }

        /// <summary>
        /// Returns the lowest risk of any path from the top left to the bottom right corner of this
        /// <see cref="Map"/>.
        /// </summary>
        /// <returns>
        /// The lowest risk of any path from the top left to the bottom right corner of this
        /// <see cref="Map"/>.
        /// </returns>
        public int LowestRisk() {
            Position start = new(0, 0);
            Position end = new(width - 1, height - 1);
            Span<int> lowestRisk = new int[riskLevels.Length];
            lowestRisk.Fill(int.MaxValue);
            lowestRisk[Index(start)] = 0;
            PriorityQueue<Position, int> queue = new([(start, 0)]);
            HashSet<Position> visited = [start];
            while (queue.Count > 0) {
                Position position = queue.Dequeue();
                if (position == end) {
                    break;
                }
                int index = Index(position);
                foreach (Position neighbor in Neighbors(position)) {
                    int neighborIndex = Index(neighbor);
                    int newRisk = lowestRisk[index] + riskLevels[neighborIndex];
                    if (newRisk < lowestRisk[neighborIndex]) {
                        lowestRisk[neighborIndex] = newRisk;
                        if (visited.Add(neighbor)) {
                            queue.Enqueue(neighbor, newRisk);
                        }
                    }
                }
            }
            return lowestRisk[Index(end)];
        }

        /// <summary>Expands this <see cref="Map"/> and returns a new one as the result.</summary>
        /// <returns>An independent, expanded version of this <see cref="Map"/>.</returns>
        public Map Expand() {
            int newHeight = height * ExpansionFactor;
            int newWidth = width * ExpansionFactor;
            int[][] expandedRiskLevels = new int[newHeight][];
            for (int y = 0; y < newHeight; y++) {
                expandedRiskLevels[y] = new int[newWidth];
                for (int x = 0; x < newWidth; x++) {
                    Position position = new(x % width, y % height);
                    int oldRiskLevel = riskLevels[Index(position)];
                    int distance = y / height + x / width;
                    expandedRiskLevels[y][x] = (oldRiskLevel + distance - 1) % MaxRiskLevel + 1;
                }
            }
            return new Map(expandedRiskLevels);
        }

    }

    private static readonly string InputFile = Path.Combine(
        AppContext.BaseDirectory,
        "resources",
        "input.txt"
    );

    /// <summary>Solves the <see cref="Chiton"/> puzzle.</summary>
    /// <param name="textWriter"><see cref="TextWriter"/> to write the results to.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="textWriter"/> is <see langword="null"/>.
    /// </exception>
    internal static void Solve(TextWriter textWriter) {
        Guard.IsNotNull(textWriter);
        Map map = Map.Parse(File.ReadAllText(InputFile));
        int lowestRisk = map.LowestRisk();
        int lowestRiskExpanded = map.Expand().LowestRisk();
        textWriter.WriteLine($"The lowest risk with the original map is {lowestRisk}.");
        textWriter.WriteLine($"The lowest risk with the expanded map is {lowestRiskExpanded}.");
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