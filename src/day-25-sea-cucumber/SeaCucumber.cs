using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Running;
using CommunityToolkit.Diagnostics;

namespace SeaCucumber;

internal sealed class SeaCucumber {

    /// <summary>Represents a <see cref="Seafloor"/> of cucumbers.</summary>
    private sealed class Seafloor {

        /// <summary>
        /// Represents an enumeration of all possible cucumber types on the <see cref="Seafloor"/>.
        /// </summary>
        private enum Cucumber { None, East, South }

        /// <summary>Represents a two-dimensional <see cref="Position"/>.</summary>
        /// <param name="X">X-coordinate of the <see cref="Position"/>.</param>
        /// <param name="Y">Y-coordinate of the <see cref="Position"/>.</param>
        private readonly record struct Position(int X, int Y);

        /// <summary>Array of the cucumbers of this <see cref="Seafloor"/>.</summary>
        /// <remarks>
        /// Note that this is actually a two-dimensional array stored as a one-dimensional
        /// one (in row-major order) for reasons of improved performance and cache locality.
        /// </remarks>
        private readonly Cucumber[] cucumbers;

        /// <summary>Width of this <see cref="Seafloor"/>.</summary>
        private readonly int width;

        /// <summary>Height of this <see cref="Seafloor"/>.</summary>
        private readonly int height;

        /// <summary>
        /// Initializes a new <see cref="Seafloor"/> with a given array of cucumbers.
        /// </summary>
        /// <param name="cucumbers">Array of cucumbers for the initialization.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="cucumbers"/> is <see langword="null"/>.
        /// </exception>
        private Seafloor(Cucumber[][] cucumbers) {
            Guard.IsNotNull(cucumbers);
            this.cucumbers = [.. cucumbers.SelectMany(row => row)];
            width = cucumbers[0].Length;
            height = cucumbers.Length;
        }

        /// <summary>Parses a <see cref="Cucumber"/> from a given character.</summary>
        /// <remarks>
        /// The character <paramref name="c"/> must either be. '.' (<see cref="Cucumber.None"/>),
        /// '>' (<see cref="Cucumber.East"/>) or 'v' (<see cref="Cucumber.South"/>).
        /// </remarks>
        /// <param name="c">Character to parse a <see cref="Cucumber"/> from.</param>
        /// <returns>A <see cref="Cucumber"/> parsed from the given character.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="c"/> is an invalid character.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Cucumber ParseCucumber(char c) => c switch {
            '.' => Cucumber.None,
            '>' => Cucumber.East,
            'v' => Cucumber.South,
            _ => throw new ArgumentOutOfRangeException(
                nameof(c),
                $"'{c}' does not represent a valid cucumber."
            )
        };

        /// <summary>Parses a <see cref="Seafloor"/> from a given string.</summary>
        /// <remarks>
        /// The string <paramref name="s"/> must contain zero or more newline-separated lines
        /// representing the rows of the <see cref="Seafloor"/>. All of these rows must have the
        /// same length and consist only of the characters '.', '>' or 'v'.<br/>
        /// An example for a string representing a valid <see cref="Seafloor"/> might be the
        /// following (with actual newlines rendered):
        /// <example>
        /// <code>
        /// v...>>.vv>
        /// .vv>>.vv..
        /// >>.>v>...v
        /// >>v>>.>.v.
        /// v>v.vv.v..
        /// >.>>..v...
        /// .vv..>.>v.
        /// v.v..>>v.v
        /// ....v..v.>
        /// </code>
        /// </example>
        /// </remarks>
        /// <param name="s">String to parse a <see cref="Seafloor"/> from.</param>
        /// <returns>A <see cref="Seafloor"/> parsed from the given string.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="s"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="s"/> contains an invalid character.
        /// </exception>
        public static Seafloor Parse(string s) {
            Guard.IsNotNull(s);
            Cucumber[][] cucumbers = [
                .. s.Split(Environment.NewLine).Select(line => line.Select(ParseCucumber).ToArray())
            ];
            return new Seafloor(cucumbers);
        }

        /// <summary>
        /// Returns the index of a <see cref="Cucumber"/> at a given <see cref="Position"/>.
        /// </summary>
        /// <param name="position">
        /// <see cref="Position"/> of the <see cref="Cucumber"/> to get the index of.
        /// </param>
        /// <returns>
        /// The index of the <see cref="Cucumber"/> at the given <see cref="Position"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int Index(Position position) => (position.Y * width) + position.X;

        /// <summary>Simulates the movement of cucumbers on this <see cref="Seafloor"/>.</summary>
        /// <param name="typeToMove">The type of <see cref="Cucumber"/> required to move.</param>
        /// <returns>
        /// <see langword="True"/> if any movement occurred, otherwise <see langword="false"/>.
        /// </returns>
        private bool SimulateMovement(Cucumber typeToMove) {
            List<(Position Source, Position Destination)> moves = [];
            for (int y = 0; y < height; y++) {
                for (int x = 0; x < width; x++) {
                    Position source = new(x, y);
                    if (cucumbers[Index(source)] == typeToMove) {
                        Position target = (typeToMove == Cucumber.East)
                            ? source with { X = (source.X + 1) % width }
                            : source with { Y = (source.Y + 1) % height };
                        if (cucumbers[Index(target)] == Cucumber.None) {
                            moves.Add((source, target));
                        }
                    }
                }
            }
            foreach ((Position source, Position target) in moves) {
                int sourceIndex = Index(source);
                cucumbers[Index(target)] = cucumbers[sourceIndex];
                cucumbers[sourceIndex] = Cucumber.None;
            }
            return moves.Count > 0;
        }

        /// <summary>Simulates the movement of cucumbers on this <see cref="Seafloor"/>.</summary>
        /// <returns>The first step in which no cucumbers move.</returns>
        public int SimulateMovement() {
            int step = 0;
            while (SimulateMovement(Cucumber.East) | SimulateMovement(Cucumber.South)) {
                step++;
            }
            return step;
        }

    }

    private static readonly string InputFile = Path.Combine(
        AppContext.BaseDirectory,
        "resources",
        "input.txt"
    );

    /// <summary>Solves the <see cref="SeaCucumber"/> puzzle.</summary>
    /// <param name="textWriter"><see cref="TextWriter"/> to write the results to.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="textWriter"/> is <see langword="null"/>.
    /// </exception>
    internal static void Solve(TextWriter textWriter) {
        Guard.IsNotNull(textWriter);
        Seafloor seafloor = Seafloor.Parse(File.ReadAllText(InputFile));
        int step = seafloor.SimulateMovement();
        textWriter.WriteLine($"The first step in which no cucumbers move is {step + 1}.");
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