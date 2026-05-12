using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Running;
using CommunityToolkit.Diagnostics;

namespace DumboOctopus;

internal sealed class DumboOctopus {

    /// <summary>
    /// Represents a <see cref="Grid"/> for simulating the energy levels of octopuses.
    /// </summary>
    private sealed class Grid {

        /// <summary>Represents an enumeration of all possible directions.</summary>
        private enum Direction { Left, LeftUp, Up, UpRight, Right, RightDown, Down, DownLeft }

        /// <summary>Represents a two-dimensional <see cref="Position"/>.</summary>
        /// <param name="X">X-coordinate of the <see cref="Position"/>.</param>
        /// <param name="Y">Y-coordinate of the <see cref="Position"/>.</param>
        private readonly record struct Position(int X, int Y);

        /// <summary>Minimum energy level of octopuses.</summary>
        private const int MinEnergyLevel = 0;

        /// <summary>Maximum energy level of octopuses (before flashing).</summary>
        private const int MaxEnergyLevel = 9;

        /// <summary>Array of all possible directions, cached for efficiency.</summary>
        private static readonly ImmutableArray<Direction> Directions = [
            .. Enum.GetValues<Direction>()
        ];

        /// <summary>Array of the energy levels of this <see cref="Grid"/>.</summary>
        /// <remarks>
        /// Note that this is actually a two-dimensional array stored as a one-dimensional
        /// one (in row-major order) for reasons of improved performance and cache locality.
        /// </remarks>
        private readonly ImmutableArray<int> energyLevels;

        /// <summary>Width of this <see cref="Grid"/>.</summary>
        private readonly int width;

        /// <summary>Height of this <see cref="Grid"/>.</summary>
        private readonly int height;

        /// <summary>
        /// Initializes a new <see cref="Grid"/> with a given map of energy levels.
        /// </summary>
        /// <param name="energyLevels">Map of energy levels for the initialization.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="energyLevels"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="energyLevels"/> contains an invalid energy level.
        /// All energy levels must be in the range
        /// [<see cref="MinEnergyLevel"/>; <see cref="MaxEnergyLevel"/>].
        /// </exception>
        private Grid(int[][] energyLevels) {
            Guard.IsNotNull(energyLevels);
            this.energyLevels = [.. energyLevels.SelectMany(row => row)];
            if (this.energyLevels.Any(e => (e < MinEnergyLevel) || (e > MaxEnergyLevel))) {
                throw new ArgumentOutOfRangeException(
                    nameof(energyLevels),
                    "The following sequence contains at least one invalid energy level. "
                        + $"All energy levels must be in the range [{MinEnergyLevel}; "
                        + $"{MaxEnergyLevel}].{Environment.NewLine}"
                        + $"[{string.Join(", ", this.energyLevels)}]"
                );
            }
            width = energyLevels[0].Length;
            height = energyLevels.Length;
        }

        /// <summary>Parses a <see cref="Grid"/> from a given string.</summary>
        /// <remarks>
        /// The string <paramref name="s"/> must contain zero or more newline-separated lines
        /// representing the rows of the <see cref="Grid"/>. All of these rows must have the
        /// same length and consist only of digits '0' through '9'.<br/>
        /// An example for a string representing a valid <see cref="Grid"/> might be the
        /// following (with actual newlines rendered):
        /// <example>
        /// <code>
        /// 5483143223
        /// 2745854711
        /// 5264556173
        /// 6141336146
        /// 6357385478
        /// 4167524645
        /// 2176841721
        /// 6882881134
        /// 4846848554
        /// 5283751526
        /// </code>
        /// </example>
        /// </remarks>
        /// <param name="s">String to parse a <see cref="Grid"/> from.</param>
        /// <returns>A <see cref="Grid"/> parsed from the given string.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="s"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="s"/> contains an invalid energy level.
        /// </exception>
        public static Grid Parse(string s) {
            Guard.IsNotNull(s);
            int[][] energyLevels = [
                .. s.Split(Environment.NewLine).Select(line => line.Select(c => c - '0').ToArray())
            ];
            return new Grid(energyLevels);
        }

        /// <summary>
        /// Returns the index of an energy level at a given <see cref="Position"/> in this
        /// <see cref="Grid"/>.
        /// </summary>
        /// <param name="position">
        /// <see cref="Position"/> of the energy level to get the index of.
        /// </param>
        /// <returns>
        /// The index of the energy level at the given <see cref="Position"/> in this
        /// <see cref="Grid"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int Index(Position position) => (position.Y * width) + position.X;

        /// <summary>
        /// Determines all existing neighbors of a given <see cref="Position"/> in this
        /// <see cref="Grid"/>.
        /// </summary>
        /// <param name="position">
        /// <see cref="Position"/> to determine all existing neighbors of.
        /// </param>
        /// <returns>
        /// All existing neighbors of the given <see cref="Position"/> in this <see cref="Grid"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private IEnumerable<Position> Neighbors(Position position) {
            foreach (Direction direction in Directions) {
                Position neighbor = direction switch {
                    Direction.Left => position with { X = position.X - 1 },
                    Direction.LeftUp => position with { X = position.X - 1, Y = position.Y - 1 },
                    Direction.Up => position with { Y = position.Y - 1 },
                    Direction.UpRight => position with { X = position.X + 1, Y = position.Y - 1 },
                    Direction.Right => position with { X = position.X + 1 },
                    Direction.RightDown => position with { X = position.X + 1, Y = position.Y + 1 },
                    Direction.Down => position with { Y = position.Y + 1 },
                    Direction.DownLeft => position with { X = position.X - 1, Y = position.Y + 1 },
                    _ => throw new InvalidOperationException("Unreachable."),
                };
                if ((neighbor.X >= 0) && (neighbor.X < width)
                        && (neighbor.Y >= 0) && (neighbor.Y < height)) {
                    yield return neighbor;
                }
            }
        }

        /// <summary>
        /// Simulates the energy levels of this <see cref="Grid"/> until a specified number of steps
        /// is reached or a synchronization event occurs.
        /// </summary>
        /// <param name="steps">
        /// Positive number of steps to simulate or <see langword="null"/> if the simulation should
        /// run until a synchronization event occurs.
        /// </param>
        /// <returns>
        /// A tuple containing the total number of flashes that occurred until the specified number
        /// of steps was reached or a synchronization event occurred.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="steps"/> is not <see langword="null"/> and negative.
        /// </exception>
        public (int Flashes, int? FirstSyncStep) SimulateEnergyLevels(int? steps = null) {
            if ((steps != null) && (steps < 0)) {
                throw new ArgumentOutOfRangeException(
                    nameof(steps),
                    $"The number of steps to simulate ({steps}) must be positive when specified."
                );
            }
            // We make a copy to prevent modifying our original grid of energy levels.
            Span<int> energyLevels = [.. this.energyLevels];
            int flashes = 0;
            int? firstSyncStep = null;
            for (int step = 1; (steps == null) || (step <= steps); step++) {
                Queue<Position> flashing = [];
                for (int y = 0; y < height; y++) {
                    for (int x = 0; x < width; x++) {
                        Position position = new(x, y);
                        int index = Index(position);
                        energyLevels[index]++;
                        if (energyLevels[index] > MaxEnergyLevel) {
                            flashing.Enqueue(position);
                        }
                    }
                }
                HashSet<Position> flashed = [.. flashing];
                while (flashing.Count > 0) {
                    Position position = flashing.Dequeue();
                    energyLevels[Index(position)] = 0;
                    foreach (Position neighbor in Neighbors(position)) {
                        if (!flashed.Contains(neighbor)) {
                            int neighborIndex = Index(neighbor);
                            energyLevels[neighborIndex]++;
                            if (energyLevels[neighborIndex] > MaxEnergyLevel) {
                                flashing.Enqueue(neighbor);
                                flashed.Add(neighbor);
                            }
                        }
                    }
                }
                flashes += flashed.Count;
                if (flashed.Count == energyLevels.Length) {
                    firstSyncStep = step;
                    break;
                }
            }
            return (flashes, firstSyncStep);
        }

    }

    private static readonly string InputFile = Path.Combine(
        AppContext.BaseDirectory,
        "resources",
        "input.txt"
    );

    /// <summary>Solves the <see cref="DumboOctopus"/> puzzle.</summary>
    /// <param name="textWriter"><see cref="TextWriter"/> to write the results to.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="textWriter"/> is <see langword="null"/>.
    /// </exception>
    internal static void Solve(TextWriter textWriter) {
        Guard.IsNotNull(textWriter);
        Grid grid = Grid.Parse(File.ReadAllText(InputFile));
        (int flashes, _) = grid.SimulateEnergyLevels(100);
        (_, int? firstSyncStep) = grid.SimulateEnergyLevels();
        textWriter.WriteLine($"After 100 steps, {flashes} total flashes occurred.");
        textWriter.WriteLine($"The first synchronization occurred after step {firstSyncStep}.");
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