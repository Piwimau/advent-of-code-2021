using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BenchmarkDotNet.Running;
using CommunityToolkit.Diagnostics;

namespace TrickShot;

internal sealed partial class TrickShot {

    /// <summary>Represents a two-dimensional <see cref="Vector"/>.</summary>
    /// <param name="X">X-coordinate of the <see cref="Vector"/>.</param>
    /// <param name="Y">Y-coordinate of the <see cref="Vector"/>.</param>
    private readonly record struct Vector(int X, int Y);

    /// <summary>Represents a <see cref="TargetArea"/> in the form of a rectangle.</summary>
    /// <param name="MinX">Minimum x-coordinate of the <see cref="TargetArea"/>.</param>
    /// <param name="MaxX">Maximum x-coordinate of the <see cref="TargetArea"/>.</param>
    /// <param name="MinY">Minimum y-coordinate of the <see cref="TargetArea"/>.</param>
    /// <param name="MaxY">Maximum y-coordinate of the <see cref="TargetArea"/>.</param>
    private readonly partial record struct TargetArea(int MinX, int MaxX, int MinY, int MaxY) {

        [GeneratedRegex("^target area: x=-?\\d+\\.\\.-?\\d+, y=-?\\d+\\.\\.-?\\d+$")]
        private static partial Regex TargetAreaRegex();

        [GeneratedRegex("-?\\d+")]
        private static partial Regex CoordinateRegex();

        /// <summary>Parses a <see cref="TargetArea"/> from a given string.</summary>
        /// <remarks>
        /// The string <paramref name="s"/> must have the format described by
        /// <see cref="TargetAreaRegex"/>. An example might be the following:
        /// <example>
        /// <code>
        /// "target area: x=20..30, y=-10..-5"
        /// </code>
        /// </example>
        /// </remarks>
        /// <param name="s">String to parse a <see cref="TargetArea"/> from.</param>
        /// <returns>A <see cref="TargetArea"/> parsed from the given string.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="s"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="s"/> has an invalid format.
        /// </exception>
        public static TargetArea Parse(string s) {
            if (!TargetAreaRegex().IsMatch(s)) {
                throw new ArgumentOutOfRangeException(
                    nameof(s),
                    $"The string \"{s}\" does not represent a valid target area."
                );
            }
            ReadOnlySpan<int> coordinates = [
                .. CoordinateRegex().Matches(s).Select(
                    match => int.Parse(match.Value, CultureInfo.InvariantCulture)
                )
            ];
            int minX = coordinates[0];
            int maxX = coordinates[1];
            int minY = coordinates[2];
            int maxY = coordinates[3];
            return new TargetArea(minX, maxX, minY, maxY);
        }

        /// <summary>
        /// Determines if this <see cref="TargetArea"/> is hit from a given position using a
        /// specified initial velocity.
        /// </summary>
        /// <param name="position">Initial position for the calculation.</param>
        /// <param name="velocity">Initial velocity for the calculation.</param>
        /// <returns>
        /// <see langword="True"/> if this <see cref="TargetArea"/> is hit,
        /// otherwise <see langword="false"/>.
        /// </returns>
        public bool IsHit(Vector position, Vector velocity) {
            while (true) {
                if ((position.X > MaxX) || (position.Y < MinY)) {
                    return false;
                }
                if ((position.X >= MinX) && (position.Y <= MaxY)) {
                    return true;
                }
                position = position with {
                    X = position.X + velocity.X,
                    Y = position.Y + velocity.Y
                };
                velocity = velocity with { X = Math.Max(0, velocity.X - 1), Y = velocity.Y - 1 };
            }
        }

        /// <summary>
        /// Returns the total number of initial velocities that reach this <see cref="TargetArea"/>.
        /// </summary>
        /// <returns>
        /// The total number of initial velocities that reach this <see cref="TargetArea"/>.
        /// </returns>
        public int TotalHits() {
            Vector initialPosition = new(0, 0);
            int totalHits = 0;
            for (int x = 1; x <= MaxX; x++) {
                for (int y = MinY; y < -MinY; y++) {
                    if (IsHit(initialPosition, new Vector(x, y))) {
                        totalHits++;
                    }
                }
            }
            return totalHits;
        }

    }

    private static readonly string InputFile = Path.Combine(
        AppContext.BaseDirectory,
        "resources",
        "input.txt"
    );

    /// <summary>Solves the <see cref="TrickShot"/> puzzle.</summary>
    /// <param name="textWriter"><see cref="TextWriter"/> to write the results to.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="textWriter"/> is <see langword="null"/>.
    /// </exception>
    internal static void Solve(TextWriter textWriter) {
        Guard.IsNotNull(textWriter);
        TargetArea targetArea = TargetArea.Parse(File.ReadAllText(InputFile));
        int highestY = targetArea.MinY * (targetArea.MinY + 1) / 2;
        int totalHits = targetArea.TotalHits();
        textWriter.WriteLine($"The highest y position reached is {highestY}.");
        textWriter.WriteLine($"A total of {totalHits} initial velocities reach the target area.");
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