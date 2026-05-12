using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using BenchmarkDotNet.Running;
using CommunityToolkit.Diagnostics;

namespace SmokeBasin;

internal sealed class SmokeBasin {

    /// <summary>Represents a <see cref="Heightmap"/> used for analyzing caves.</summary>
    private sealed class Heightmap {

        /// <summary>Represents an enumeration of all possible directions.</summary>
        private enum Direction { Left, Up, Right, Down }

        /// <summary>Represents a two-dimensional <see cref="Position"/>.</summary>
        /// <param name="X">X-coordinate of the <see cref="Position"/>.</param>
        /// <param name="Y">Y-coordinate of the <see cref="Position"/>.</param>
        private readonly record struct Position(int X, int Y);

        /// <summary>Minimum allowed height in a <see cref="Heightmap"/>.</summary>
        private const int MinHeight = 0;

        /// <summary>Maximum allowed height in a <see cref="Heightmap"/>.</summary>
        private const int MaxHeight = 9;

        /// <summary>
        /// Maximum height that still counts as being inside a basin of a <see cref="Heightmap"/>.
        /// </summary>
        private const int MaxBasinHeight = 8;

        /// <summary>
        /// Maximum number of top basin sizes to remember when analyzing a <see cref="Heightmap"/>.
        /// </summary>
        private const int MaxTopBasinSizes = 3;

        /// <summary>Array of all possible directions, cached for efficiency.</summary>
        private static readonly ImmutableArray<Direction> Directions = [
            .. Enum.GetValues<Direction>()
        ];

        /// <summary>Array of the height values of this <see cref="Heightmap"/>.</summary>
        /// <remarks>
        /// Note that this is actually a two-dimensional array stored as a one-dimensional
        /// one (in row-major order) for reasons of improved performance and cache locality.
        /// </remarks>
        private readonly ImmutableArray<int> heights;

        /// <summary>Width of this <see cref="Heightmap"/>.</summary>
        private readonly int width;

        /// <summary>Height of this <see cref="Heightmap"/>.</summary>
        private readonly int height;

        /// <summary>
        /// Array of low positions of this <see cref="Heightmap"/>, lazily initialized once
        /// and cached for reasons of efficiency and performance.
        /// </summary>
        private readonly Lazy<ImmutableArray<Position>> lowPositions;

        /// <summary>
        /// Initializes a new <see cref="Heightmap"/> with a given map of height values.
        /// </summary>
        /// <param name="heights">Map of height values for the initialization.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="heights"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="heights"/> contains an invalid height value. All height
        /// values must be in the range [<see cref="MinHeight"/>; <see cref="MaxHeight"/>].
        /// </exception>
        private Heightmap(int[][] heights) {
            Guard.IsNotNull(heights);
            this.heights = [.. heights.SelectMany(row => row)];
            if (this.heights.Any(height => (height < MinHeight) || (height > MaxHeight))) {
                throw new ArgumentOutOfRangeException(
                    nameof(heights),
                    "The following sequence contains at least one invalid height value. "
                        + $"All height values must be in the range [{MinHeight}; {MaxHeight}]."
                        + $"{Environment.NewLine}[{string.Join(", ", this.heights)}]"
                );
            }
            width = heights[0].Length;
            height = heights.Length;
            lowPositions = new Lazy<ImmutableArray<Position>>(
                () => [.. LowPositions()],
                LazyThreadSafetyMode.None
            );
        }

        /// <summary>Parses a <see cref="Heightmap"/> from a given string.</summary>
        /// <remarks>
        /// The string <paramref name="s"/> must contain zero or more newline-separated lines
        /// representing the rows of the <see cref="Heightmap"/>. All of these rows must have the
        /// same length and consist only of digits '0' through '9'.<br/>
        /// An example for a string representing a valid <see cref="Heightmap"/> might be the
        /// following (with actual newlines rendered):
        /// <example>
        /// <code>
        /// 2199943210
        /// 3987894921
        /// 9856789892
        /// 8767896789
        /// 9899965678
        /// </code>
        /// </example>
        /// </remarks>
        /// <param name="s">String to parse a <see cref="Heightmap"/> from.</param>
        /// <returns>A <see cref="Heightmap"/> parsed from the given string.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="s"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="s"/> contains an invalid height value.
        /// </exception>
        public static Heightmap Parse(string s) {
            Guard.IsNotNull(s);
            int[][] heights = [
                .. s.Split(Environment.NewLine).Select(line => line.Select(c => c - '0').ToArray())
            ];
            return new Heightmap(heights);
        }

        /// <summary>
        /// Determines all existing neighbors of a given <see cref="Position"/> in this
        /// <see cref="Heightmap"/>.
        /// </summary>
        /// <param name="position">
        /// <see cref="Position"/> to determine all existing neighbors of.
        /// </param>
        /// <returns>
        /// All existing neighbors of the given <see cref="Position"/> in this
        /// <see cref="Heightmap"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private IEnumerable<Position> Neighbors(Position position) {
            foreach (Direction direction in Directions) {
                Position neighbor = direction switch {
                    Direction.Left => position with { X = position.X - 1 },
                    Direction.Up => position with { Y = position.Y - 1 },
                    Direction.Right => position with { X = position.X + 1 },
                    Direction.Down => position with { Y = position.Y + 1 },
                    _ => throw new InvalidOperationException("Unreachable.")
                };
                if ((neighbor.X >= 0) && (neighbor.X < width)
                        && (neighbor.Y >= 0) && (neighbor.Y < height)) {
                    yield return neighbor;
                }
            }
        }

        /// <summary>
        /// Returns the index of a height value at a given <see cref="Position"/> in this
        /// <see cref="Heightmap"/>.
        /// </summary>
        /// <param name="position">
        /// <see cref="Position"/> of the height value to get the index of.
        /// </param>
        /// <returns>
        /// The index of the height value at the given <see cref="Position"/> in this
        /// <see cref="Heightmap"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int Index(Position position) => position.Y * width + position.X;

        /// <summary>Returns all low positions in this <see cref="Heightmap"/>.</summary>
        /// <remarks>
        /// A <see cref="Position"/> is considered to be low if fully surrounded by larger height
        /// values.
        /// <para></para>
        /// Note that this method is used to lazily initialize the <see cref="lowPositions"/> field,
        /// meaning that it should generally not be necessary to call it manually.
        /// </remarks>
        /// <returns>All low positions in this <see cref="Heightmap"/>.</returns>
        private IEnumerable<Position> LowPositions() {
            for (int y = 0; y < height; y++) {
                for (int x = 0; x < width; x++) {
                    Position position = new(x, y);
                    int index = Index(position);
                    bool isLowPosition = true;
                    foreach (Position neighbor in Neighbors(position)) {
                        if (heights[index] >= heights[Index(neighbor)]) {
                            isLowPosition = false;
                            break;
                        }
                    }
                    if (isLowPosition) {
                        yield return position;
                    }
                }
            }
        }

        /// <summary>Returns the sum of risk levels of this <see cref="Heightmap"/>.</summary>
        /// <returns>The sum of risk levels of this <see cref="Heightmap"/>.</returns>
        public int SumOfRiskLevels() {
            int sumOfRiskLevels = 0;
            foreach (Position lowPosition in lowPositions.Value) {
                sumOfRiskLevels += heights[Index(lowPosition)] + 1;
            }
            return sumOfRiskLevels;
        }

        /// <summary>
        /// Returns the product of the top basin sizes of this <see cref="Heightmap"/>.
        /// </summary>
        /// <returns>The product of the top basin sizes of this <see cref="Heightmap"/>.</returns>
        public int ProductOfTopBasinSizes() {
            HashSet<int> topBasinSizes = new(MaxTopBasinSizes);
            foreach (Position lowPosition in lowPositions.Value) {
                HashSet<Position> visited = [lowPosition];
                Queue<Position> queue = [];
                queue.Enqueue(lowPosition);
                int basinSize = 0;
                while (queue.Count > 0) {
                    Position position = queue.Dequeue();
                    basinSize++;
                    foreach (Position neighbor in Neighbors(position)) {
                        if (visited.Add(neighbor) && (heights[Index(neighbor)] <= MaxBasinHeight)) {
                            queue.Enqueue(neighbor);
                        }
                    }
                }
                topBasinSizes.Add(basinSize);
                if (topBasinSizes.Count > MaxTopBasinSizes) {
                    topBasinSizes.Remove(topBasinSizes.Min());
                }
            }
            return topBasinSizes.Aggregate((product, basinSize) => product * basinSize);
        }

    }

    private static readonly string InputFile = Path.Combine(
        AppContext.BaseDirectory,
        "resources",
        "input.txt"
    );

    /// <summary>Solves the <see cref="SmokeBasin"/> puzzle.</summary>
    /// <param name="textWriter"><see cref="TextWriter"/> to write the results to.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="textWriter"/> is <see langword="null"/>.
    /// </exception>
    internal static void Solve(TextWriter textWriter) {
        Guard.IsNotNull(textWriter);
        Heightmap heightmap = Heightmap.Parse(File.ReadAllText(InputFile));
        int sumOfRiskLevels = heightmap.SumOfRiskLevels();
        int productOfTopBasinSizes = heightmap.ProductOfTopBasinSizes();
        textWriter.WriteLine($"The sum of risk levels is {sumOfRiskLevels}.");
        textWriter.WriteLine($"The product of the top basin sizes is {productOfTopBasinSizes}.");
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