using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using BenchmarkDotNet.Running;
using CommunityToolkit.Diagnostics;

namespace GiantSquid;

internal sealed partial class GiantSquid {

    /// <summary>Represents a <see cref="Board"/> for playing a game of bingo.</summary>
    private sealed partial class Board {

        /// <summary>Represents a single <see cref="Spot"/> on a <see cref="Board"/>.</summary>
        /// <param name="Number">Number of the <see cref="Spot"/>.</param>
        /// <param name="IsMarked">Marked status of the <see cref="Spot"/>.</param>
        private readonly record struct Spot(int Number, bool IsMarked = false);

        /// <summary>Width and height of the <see cref="Board"/>.</summary>
        private const int SideLength = 5;

        /// <summary>
        /// Spots of this <see cref="Board"/> that are marked while playing a game of bingo.
        /// </summary>
        /// <remarks>
        /// Note that this is actually a two-dimensional array stored as a one-dimensional
        /// one (in row-major order) for reasons of improved performance and cache locality.
        /// </remarks>
        private readonly Spot[] spots;

        /// <summary>
        /// Initializes a new <see cref="Board"/> using a given sequence of spots.
        /// </summary>
        /// <param name="spots">Sequence of spots for the initialization.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="spots"/> does not have exactly
        /// <see cref="SideLength"/> * <see cref="SideLength"/> spots.
        /// </exception>
        private Board(ReadOnlySpan<Spot> spots) {
            Guard.IsEqualTo(spots.Length, SideLength * SideLength);
            this.spots = [.. spots];
        }

        [GeneratedRegex("^ *\\d+ +\\d+ +\\d+ +\\d+ +\\d+$")]
        private static partial Regex LineRegex();

        /// <summary>Parses a <see cref="Board"/> from a given string.</summary>
        /// <remarks>
        /// The string <paramref name="s"/> must contain <see cref="SideLength"/> lines with
        /// <see cref="SideLength"/> positive integers each (separated by one or more spaces,
        /// as described by <see cref="LineRegex"/>).<br/>
        /// An example might be the following:
        /// <example>
        /// <code>
        /// 22 13 17 11  0
        ///  8  2 23  4 24
        /// 21  9 14 16  7
        ///  6 10  3 18  5
        ///  1 12 20 15 19
        /// </code>
        /// </example>
        /// </remarks>
        /// <param name="s">String to parse a <see cref="Board"/> from.</param>
        /// <returns>A <see cref="Board"/> parsed from the given string.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="s"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="s"/> has an invalid format.
        /// </exception>
        public static Board Parse(string s) {
            Guard.IsNotNull(s);
            IReadOnlyList<string> lines = s.Split(Environment.NewLine);
            if (lines.Any(line => !LineRegex().IsMatch(line))) {
                throw new ArgumentOutOfRangeException(
                    nameof(s),
                    "The following string does not represent a valid board:"
                        + $"{Environment.NewLine}{Environment.NewLine}{s}"
                );
            }
            ReadOnlySpan<Spot> spots = [.. lines
                .SelectMany(line => line
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Select(number => new Spot(int.Parse(number, CultureInfo.InvariantCulture)))
                )
            ];
            return new Board(spots);
        }

        /// <summary>
        /// Marks all numbers on this <see cref="Board"/> that are equal to a given one.
        /// </summary>
        /// <param name="number">Number to mark.</param>
        public void MarkNumber(int number) {
            for (int i = 0; i < spots.Length; i++) {
                if (spots[i].Number == number) {
                    spots[i] = spots[i] with { IsMarked = true };
                }
            }
        }

        /// <summary>Returns the index of a <see cref="Spot"/> at a given position.</summary>
        /// <param name="x">
        /// X-coordinate of the <see cref="Spot"/> (in the range [0; <see cref="SideLength"/> - 1]).
        /// </param>
        /// <param name="y">
        /// Y-coordinate of the <see cref="Spot"/> (in the range [0; <see cref="SideLength"/> - 1]).
        /// </param>
        /// <returns>The index of the <see cref="Spot"/> at the given position.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SpotIndex(int x, int y) => (y * SideLength) + x;

        /// <summary>Determines if this <see cref="Board"/> has won.</summary>
        /// <remarks>
        /// A <see cref="Board"/> has won if a completely marked row or column of numbers exists.
        /// </remarks>
        /// <returns>
        /// <see langword="True"/> if this <see cref="Board"/> has won,
        /// otherwise <see langword="false"/>.
        /// </returns>
        public bool HasWon() {
            bool anyRowComplete = Enumerable.Range(0, SideLength)
                .Any(y => Enumerable.Range(0, SideLength)
                .All(x => spots[SpotIndex(x, y)].IsMarked));
            if (anyRowComplete) {
                return true;
            }
            bool anyColumnComplete = Enumerable.Range(0, SideLength)
                .Any(x => Enumerable.Range(0, SideLength)
                .All(y => spots[SpotIndex(x, y)].IsMarked));
            return anyColumnComplete;
        }

        /// <summary>Returns all unmarked numbers on this <see cref="Board"/>.</summary>
        /// <returns>All unmarked numbers on this <see cref="Board"/>.</returns>
        public IEnumerable<int> UnmarkedNumbers()
            => spots.Where(spot => !spot.IsMarked).Select(spot => spot.Number);

    }

    private static readonly string InputFile = Path.Combine(
        AppContext.BaseDirectory,
        "resources",
        "input.txt"
    );

    /// <summary>
    /// Plays a game of bingo using a given sequence of numbers to mark, as well as a sequence
    /// of boards.
    /// </summary>
    /// <param name="numbers">Sequence of numbers to mark in the game of bingo.</param>
    /// <param name="boards">Sequence of boards playing the game of bingo.</param>
    /// <returns>
    /// A tuple containing the scores of the first and last winning board. Note that these may be
    /// <see langword="null"/> in case <paramref name="numbers"/> or <paramref name="boards"/> is
    /// empty, or if there simply happened to be no winning board.
    /// </returns>
    private static (int? FirstWinningScore, int? LastWinningScore) PlayBingo(
        ReadOnlySpan<int> numbers,
        ReadOnlySpan<Board> boards
    ) {
        List<Board> remainingBoards = [.. boards];
        int? firstWinningScore = null;
        int? lastWinningScore = null;
        foreach (int number in numbers) {
            List<Board> winningBoards = [];
            foreach (Board remainingBoard in remainingBoards) {
                remainingBoard.MarkNumber(number);
                if (remainingBoard.HasWon()) {
                    winningBoards.Add(remainingBoard);
                    int winningScore = remainingBoard.UnmarkedNumbers().Sum() * number;
                    firstWinningScore ??= winningScore;
                    lastWinningScore = winningScore;
                }
            }
            remainingBoards.RemoveAll(winningBoards.Contains);
        }
        return (firstWinningScore, lastWinningScore);
    }

    /// <summary>Solves the <see cref="GiantSquid"/> puzzle.</summary>
    /// <param name="textWriter"><see cref="TextWriter"/> to write the results to.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="textWriter"/> is <see langword="null"/>.
    /// </exception>
    internal static void Solve(TextWriter textWriter) {
        Guard.IsNotNull(textWriter);
        IReadOnlyList<string> parts = [.. File.ReadAllText(InputFile)
            .Split($"{Environment.NewLine}{Environment.NewLine}")
        ];
        ReadOnlySpan<int> numbers = [.. parts[0].Split(',').Select(int.Parse)];
        ReadOnlySpan<Board> boards = [.. parts.Skip(1).Select(Board.Parse)];
        (int? firstWinningScore, int? lastWinningScore) = PlayBingo(numbers, boards);
        textWriter.WriteLine($"The score of the first winning board is {firstWinningScore}.");
        textWriter.WriteLine($"The score of the last winning board is {lastWinningScore}.");
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