using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using BenchmarkDotNet.Running;
using CommunityToolkit.Diagnostics;

namespace DiracDice;

internal sealed partial class DiracDice {

    /// <summary>Represents a <see cref="Player"/> playing a game of Dirac Dice.</summary>
    /// <param name="Position">Position of the <see cref="Player"/>.</param>
    /// <param name="Score">Score of the <see cref="Player"/>.</param>
    private readonly partial record struct Player(int Position, int Score) {

        /// <summary>Minimum possible position a <see cref="Player"/> may be on.</summary>
        public const int MinPosition = 1;

        /// <summary>Maximum possible position a <see cref="Player"/> may be on.</summary>
        public const int MaxPosition = 10;

        [GeneratedRegex("^Player [12] starting position: (?:[1-9]|10)$")]
        private static partial Regex PlayerRegex();

        /// <summary>Parses a <see cref="Player"/> from a given string.</summary>
        /// <remarks>
        /// The string <paramref name="s"/> must have the format
        /// "Player &lt;Index&gt; starting position: &lt;Position&gt;", where &lt;Index&gt; must be
        /// either 1 or 2 and &lt;Position&gt; is a number in the range
        /// [<see cref="MinPosition"/>; <see cref="MaxPosition"/>].
        /// </remarks>
        /// <param name="s">String to parse a <see cref="Player"/> from.</param>
        /// <returns>A <see cref="Player"/> parsed from the given string.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="s"/> has an invalid format.
        /// </exception>
        public static Player Parse(ReadOnlySpan<char> s) {
            if (!PlayerRegex().IsMatch(s)) {
                throw new ArgumentOutOfRangeException(
                    nameof(s),
                    $"The string \"{s}\" does not represent a valid player."
                );
            }
            int position = int.Parse(s[(s.LastIndexOf(' ') + 1)..], CultureInfo.InvariantCulture);
            return new Player(position, 0);
        }

    }

    /// <summary>Number of times the die is rolled per round.</summary>
    private const int RollsPerRound = 3;

    /// <summary>
    /// Score necessary for a <see cref="Player"/> to win a game of Dirac Dice with the
    /// deterministic die.
    /// </summary>
    private const int DeterministicWinningScore = 1000;

    /// <summary>
    /// Score necessary for a <see cref="Player"/> to win a game of Dirac Dice with the
    /// non-deterministic die.
    /// </summary>
    private const int NonDeterministicWinningScore = 21;

    private static readonly string InputFile = Path.Combine(
        AppContext.BaseDirectory,
        "resources",
        "input.txt"
    );

    /// <summary>
    /// Array of all outcomes possible using the non-deterministic die. Each tuple contains a sum of
    /// three rolls of the die (called "Score) and the number of ways ("Possibilities") to achieve
    /// that score.
    /// For example:
    /// <example>
    /// <list type="bullet">
    ///     <item>A score of 3 can only achieved by rolling a single combination: (1, 1, 1).</item>
    ///     <item>
    ///     A score of 4 is achieved by rolling either of the combinations (1, 1, 2), (1, 2, 1),
    ///     (2, 1, 1), a total of 3 different possibilities.
    ///     </item>
    /// </list>
    /// </example>
    /// And so on...
    /// </summary>
    private static readonly ImmutableArray<(int Score, int Possibilities)> Outcomes = [
        (3, 1), (4, 3), (5, 6), (6, 7), (7, 6), (8, 3), (9, 1)
    ];

    /// <summary>
    /// Normalizes a given position to be in the range
    /// [<see cref="Player.MinPosition"/>; <see cref="Player.MaxPosition"/>].
    /// </summary>
    /// <param name="position">Position to normalize.</param>
    /// <returns>The normalized position.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int NormalizePosition(int position)
        => ((position - Player.MinPosition) % Player.MaxPosition) + Player.MinPosition;

    /// <summary>Calculates the score of a game of Dirac Dice using the deterministic die.</summary>
    /// <remarks>Both players are expected to start with an initial score of zero.</remarks>
    /// <param name="first">First <see cref="Player"/> of the game.</param>
    /// <param name="second">Second <see cref="Player"/> of the game.</param>
    /// <returns>The score of a game of Dirac Dice using the deterministic die.</returns>
    private static int DeterministicScore(Player first, Player second) {
        int rolls = 0;
        while (second.Score < DeterministicWinningScore) {
            int position = NormalizePosition(first.Position + 6 + (RollsPerRound * rolls));
            first = first with { Position = position, Score = first.Score + position };
            (first, second) = (second, first);
            rolls += RollsPerRound;
        }
        return first.Score * rolls;
    }

    /// <summary>
    /// Calculates a statistic for the outcome of a game of Dirac Dice using the non-deterministic
    /// die.
    /// </summary>
    /// <remarks>Both players are expected to start with an initial score of zero.</remarks>
    /// <param name="first">First <see cref="Player"/> of the game.</param>
    /// <param name="second">Second <see cref="Player"/> of the game.</param>
    /// <param name="cache">
    /// Cache of already computed states which is modified by this method and used to improve the
    /// runtime.
    /// </param>
    /// <returns>
    /// A statistic for the outcome of a game of Dirac Dice using the non-deterministic die.
    /// </returns>
    private static (long FirstWins, long SecondWins) NonDeterministicStatistic(
        Player first,
        Player second,
        Dictionary<(Player First, Player Second), (long FirstWins, long SecondWins)>? cache = null
    ) {
        if (second.Score >= NonDeterministicWinningScore) {
            return (0L, 1L);
        }
        cache ??= [];
        if (cache.TryGetValue((first, second), out (long FirstWins, long SecondWins) statistic)) {
            return statistic;
        }
        foreach ((int score, int possibilities) in Outcomes) {
            int position = NormalizePosition(first.Position + score);
            Player player = new(position, first.Score + position);
            (long secondWins, long firstWins) = NonDeterministicStatistic(second, player, cache);
            statistic = statistic with {
                FirstWins = statistic.FirstWins + (firstWins * possibilities),
                SecondWins = statistic.SecondWins + (secondWins * possibilities)
            };
        }
        cache[(first, second)] = statistic;
        return statistic;
    }

    /// <summary>Solves the <see cref="DiracDice"/> puzzle.</summary>
    /// <param name="textWriter"><see cref="TextWriter"/> to write the results to.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="textWriter"/> is <see langword="null"/>.
    /// </exception>
    internal static void Solve(TextWriter textWriter) {
        Guard.IsNotNull(textWriter);
        ReadOnlySpan<Player> players = [.. File.ReadLines(InputFile)
            .Select(line => Player.Parse(line))
        ];
        int deterministicScore = DeterministicScore(players[0], players[1]);
        (long firstWins, long secondWins) = NonDeterministicStatistic(players[0], players[1]);
        textWriter.WriteLine($"The score using the deterministic die is {deterministicScore}.");
        textWriter.WriteLine($"The maximum number of wins is {Math.Max(firstWins, secondWins)}.");
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