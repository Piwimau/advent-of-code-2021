using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BenchmarkDotNet.Running;
using CommunityToolkit.Diagnostics;

namespace Dive;

internal sealed partial class Dive {

    /// <summary>
    /// Represents an enumeration of all possible directions for steering a <see cref="Submarine"/>.
    /// </summary>
    private enum Direction { Forward, Down, Up }

    /// <summary>
    /// Represents a <see cref="Command"/> for steering a <see cref="Submarine"/>.
    /// </summary>
    /// <param name="Direction">
    /// <see cref="Dive.Direction"/> of the <see cref="Command"/>.
    /// </param>
    /// <param name="Amount">Amount by which the <see cref="Command"/> should steer.</param>
    private readonly partial record struct Command(Direction Direction, int Amount) {

        [GeneratedRegex("^(?:forward|up|down) \\d+$")]
        private static partial Regex CommandRegex();

        /// <summary>Parses a <see cref="Command"/> from a given string.</summary>
        /// <remarks>
        /// The string <paramref name="s"/> must have the required format described by
        /// <see cref="CommandRegex"/>.
        /// </remarks>
        /// <param name="s">String containing a <see cref="Command"/> to parse.</param>
        /// <returns>A <see cref="Command"/> parsed from the given string.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="s"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="s"/> does not match the required format described by
        /// <see cref="CommandRegex"/>.
        /// </exception>
        public static Command Parse(string s) {
            Guard.IsNotNull(s);
            if (!CommandRegex().IsMatch(s)) {
                throw new ArgumentOutOfRangeException(
                    nameof(s),
                    $"The string '{s}' does not represent a valid command."
                );
            }
            ReadOnlySpan<string> parts = s.Split(' ');
            Direction direction = Enum.Parse<Direction>(parts[0], true);
            int amount = int.Parse(parts[1], CultureInfo.InvariantCulture);
            return new Command(direction, amount);
        }

    }

    /// <summary>Represents a steerable <see cref="Submarine"/>.</summary>
    /// <param name="Horizontal">Horizontal position of the <see cref="Submarine"/>.</param>
    /// <param name="Depth">Depth of the <see cref="Submarine"/>.</param>
    /// <param name="Aim">Aim of the <see cref="Submarine"/>.</param>
    private readonly record struct Submarine(int Horizontal, int Depth, int Aim) {

        /// <summary>Gets the score of this <see cref="Submarine"/>.</summary>
        public int Score => Horizontal * Depth;

    }

    private static readonly string InputFile = Path.Combine(
        AppContext.BaseDirectory,
        "resources",
        "input.txt"
    );

    /// <summary>Follows a planned course by executing a sequence of commands.</summary>
    /// <param name="commands">Sequence of commands describing the planned course.</param>
    /// <returns>
    /// A tuple containing the final scores of two submarines following the planned course,
    /// once interpreted in a simple and once in a more complicated way.
    /// </returns>
    private static (int SimpleScore, int ComplicatedScore) FollowPlannedCourse(
        ReadOnlySpan<Command> commands
    ) {
        Submarine simple = new();
        Submarine complicated = new();
        foreach (Command command in commands) {
            switch (command.Direction) {
                case Direction.Forward:
                    simple = simple with { Horizontal = simple.Horizontal + command.Amount };
                    complicated = complicated with {
                        Horizontal = complicated.Horizontal + command.Amount,
                        Depth = complicated.Depth + (complicated.Aim * command.Amount)
                    };
                    break;
                case Direction.Down:
                    simple = simple with { Depth = simple.Depth + command.Amount };
                    complicated = complicated with { Aim = complicated.Aim + command.Amount };
                    break;
                case Direction.Up:
                    simple = simple with { Depth = simple.Depth - command.Amount };
                    complicated = complicated with { Aim = complicated.Aim - command.Amount };
                    break;
                default:
                    throw new InvalidOperationException("Unreachable.");
            }
        }
        return (simple.Score, complicated.Score);
    }

    /// <summary>Solves the <see cref="Dive"/> puzzle.</summary>
    /// <param name="textWriter"><see cref="TextWriter"/> to write the results to.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="textWriter"/> is <see langword="null"/>.
    /// </exception>
    internal static void Solve(TextWriter textWriter) {
        Guard.IsNotNull(textWriter);
        ReadOnlySpan<Command> commands = [.. File.ReadLines(InputFile).Select(Command.Parse)];
        (int simpleScore, int complicatedScore) = FollowPlannedCourse(commands);
        textWriter.WriteLine($"The simple score is {simpleScore}.");
        textWriter.WriteLine($"The complicated score is {complicatedScore}.");
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