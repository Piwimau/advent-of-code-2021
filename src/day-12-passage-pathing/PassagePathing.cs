using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BenchmarkDotNet.Running;
using CommunityToolkit.Diagnostics;

namespace PassagePathing;

internal sealed partial class PassagePathing {

    /// <summary>
    /// Represents an enumeration of all possible types a <see cref="Cave"/> may have.
    /// </summary>
    private enum Type { Start, End, Small, Big }

    /// <summary>Represents a <see cref="Cave"/> used for pathfinding.</summary>
    private sealed class Cave {

        /// <summary>Gets the <see cref="PassagePathing.Type"/> of this <see cref="Cave"/>.</summary>
        public Type Type { get; init; }

        /// <summary>Gets the neighbors of this <see cref="Cave"/>.</summary>
        /// <remarks>
        /// Surprisingly, using an <see cref="ImmutableArray{T}"/> is quite a lot faster than a
        /// <see cref="HashSet{T}"/> or even a <see cref="List{T}"/>. This is probably both due to
        /// effects of cache locality and because caves generally only have a few neighbors anyway.
        /// </remarks>
        public ImmutableArray<Cave> Neighbors { get; private set; }

        /// <summary>Initializes a new <see cref="Cave"/> with a given name.</summary>
        /// <remarks>
        /// The <paramref name="name"/> determines the cave's <see cref="PassagePathing.Type"/>.
        /// See the <see cref="ParseCaves(ReadOnlySpan{string})"/> method for more information.
        /// </remarks>
        /// <param name="name">
        /// Name of the <see cref="Cave"/> which determines its <see cref="PassagePathing.Type"/>.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="name"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="name"/> does not represent a valid cave type.
        /// </exception>
        public Cave(string name) {
            Guard.IsNotNull(name);
            Type = name switch {
                "start" => Type.Start,
                "end" => Type.End,
                string small when small.All(char.IsLower) => Type.Small,
                string big when big.All(char.IsUpper) => Type.Big,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(name),
                    $"The cave name \"{name}\" does not represent a valid cave type."
                )
            };
            Neighbors = [];
        }

        /// <summary>Connects this <see cref="Cave"/> with a given neighbor.</summary>
        /// <remarks>
        /// Note that both this instance, as well as the given <see cref="Cave"/> are modified
        /// by this method.
        /// </remarks>
        /// <param name="neighbor">Neighbor to connect this <see cref="Cave"/> with.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="neighbor"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="neighbor"/> refers to the same instance as this
        /// <see cref="Cave"/>, as a <see cref="Cave"/> cannot be its own neighbor.
        /// </exception>
        public void ConnectWith(Cave neighbor) {
            Guard.IsNotNull(neighbor);
            if (neighbor == this) {
                throw new ArgumentOutOfRangeException(
                    nameof(neighbor),
                    "A cave cannot be its own neighbor."
                );
            }
            if (!Neighbors.Contains(neighbor)) {
                Neighbors = Neighbors.Add(neighbor);
            }
            if (!neighbor.Neighbors.Contains(this)) {
                neighbor.Neighbors = neighbor.Neighbors.Add(this);
            }
        }

    }

    private static readonly string InputFile = Path.Combine(
        AppContext.BaseDirectory,
        "resources",
        "input.txt"
    );

    [GeneratedRegex("^(?:start|end|[a-z]+|[A-Z]+)\\-(?:start|end|[a-z]+|[A-Z]+)$")]
    private static partial Regex LineRegex();

    /// <summary>Parses all caves from a given sequence of lines.</summary>
    /// <remarks>
    /// Every line in <paramref name="lines"/> must match the format described by
    /// <see cref="LineRegex"/>. In particular, it must contain two cave names separated by a '-'.
    /// Each of these in turn must be one of the following:
    /// <list type="bullet">
    ///     <item>The start cave ("start").</item>
    ///     <item>The end cave ("end").</item>
    ///     <item>A small cave (consisting only of lowercase letters 'a' through 'z').</item>
    ///     <item>A big cave (consisting only of  uppercase letters 'A' through 'Z').</item>
    /// </list>
    /// An example for a valid sequence of lines might be the following:
    /// <example>
    /// <code>
    /// start-A
    /// start-b
    /// A-c
    /// A-b
    /// b-d
    /// A-end
    /// b-end
    /// </code>
    /// </example>
    /// A final note: Cave names are unique, i. e. any name that appears more than once always
    /// refers to the same cave.
    /// </remarks>
    /// <param name="lines">Sequence of lines to parse the caves from.</param>
    /// <returns>All caves parsed from the given sequence of lines.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="lines"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="lines"/> contains a line with an invalid format.
    /// </exception>
    private static IEnumerable<Cave> ParseCaves(ReadOnlySpan<string> lines) {
        Dictionary<string, Cave> caves = [];
        foreach (string line in lines) {
            if (!LineRegex().IsMatch(line)) {
                throw new ArgumentOutOfRangeException(
                    nameof(lines),
                    $"The line \"{line}\" has an invalid format."
                );
            }
            int separatorIndex = line.IndexOf('-');
            string firstCaveName = line[..separatorIndex];
            string secondCaveName = line[(separatorIndex + 1)..];
            Cave firstCave = caves.GetValueOrDefault(firstCaveName, new Cave(firstCaveName));
            Cave secondCave = caves.GetValueOrDefault(secondCaveName, new Cave(secondCaveName));
            firstCave.ConnectWith(secondCave);
            caves[firstCaveName] = firstCave;
            caves[secondCaveName] = secondCave;
        }
        return caves.Values;
    }

    /// <summary>
    /// Counts the number of paths from a given start <see cref="Cave"/> to the end.
    /// </summary>
    /// <param name="start"><see cref="Cave"/> to start the search at.</param>
    /// <param name="visitedSingleSmallCaveTwice">
    /// Whether a single small <see cref="Cave"/> has already been visited twice.
    /// </param>
    /// <returns>The number of paths from the given start <see cref="Cave"/> to the end.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="start"/> is <see langword="null"/>.
    /// </exception>
    private static int CountPaths(Cave start, bool visitedSingleSmallCaveTwice) {
        Guard.IsNotNull(start);
        Stack<(Cave Cave, ImmutableArray<Cave> Path, bool VisitedSingleSmallCaveTwice)> stack = [];
        stack.Push((start, [], visitedSingleSmallCaveTwice));
        int paths = 0;
        while (stack.Count > 0) {
            (Cave cave, ImmutableArray<Cave> path, visitedSingleSmallCaveTwice) = stack.Pop();
            if (cave.Type == Type.End) {
                paths++;
                continue;
            }
            if ((cave.Type == Type.Start) || ((cave.Type == Type.Small) && path.Contains(cave))) {
                if ((cave.Type == Type.Start) || visitedSingleSmallCaveTwice) {
                    continue;
                }
                visitedSingleSmallCaveTwice = true;
            }
            ImmutableArray<Cave> extendedPath = [.. path, cave];
            foreach (Cave neighbor in cave.Neighbors) {
                stack.Push((neighbor, extendedPath, visitedSingleSmallCaveTwice));
            }
        }
        return paths;
    }

    /// <summary>Solves the <see cref="PassagePathing"/> puzzle.</summary>
    /// <param name="textWriter"><see cref="TextWriter"/> to write the results to.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="textWriter"/> is <see langword="null"/>.
    /// </exception>
    internal static void Solve(TextWriter textWriter) {
        Guard.IsNotNull(textWriter);
        Cave start = ParseCaves([.. File.ReadLines(InputFile)])
            .First(cave => cave.Type == Type.Start);
        int pathsVisitingOnce = CountPaths(start, true);
        int pathsVisitingAtMostTwice = CountPaths(start, false);
        textWriter.WriteLine($"{pathsVisitingOnce} paths visit all small caves exactly once.");
        textWriter.WriteLine(
            $"{pathsVisitingAtMostTwice} paths visit at most one small cave twice."
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