using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BenchmarkDotNet.Running;
using CommunityToolkit.Diagnostics;

namespace ExtendedPolymerization;

internal sealed partial class ExtendedPolymerization {

    /// <summary>
    /// Represents a <see cref="Pair"/> of elements used for applying the pair insertion algorithm
    /// on a polymer.
    /// </summary>
    /// <param name="FirstElement">First element of the <see cref="Pair"/>.</param>
    /// <param name="SecondElement">Second element of the <see cref="Pair"/>.</param>
    private readonly record struct Pair(char FirstElement, char SecondElement);

    private static readonly string InputFile = Path.Combine(
        AppContext.BaseDirectory,
        "resources",
        "input.txt"
    );

    [GeneratedRegex("^[A-Z]{2} -> [A-Z]$")]
    private static partial Regex InsertionRuleRegex();

    /// <summary>Parses an insertion rule from a given string.</summary>
    /// <remarks>
    /// The string <paramref name="s"/> must begin with a pair of two uppercase letters,
    /// followed by " -> " and another uppercase letter.<br/>
    /// Examples for valid insertion rules are:
    /// <example>
    /// <code>
    /// "HN -> S"
    /// "FK -> N"
    /// "CH -> P"
    /// "VP -> P"
    /// </code>
    /// </example>
    /// </remarks>
    /// <param name="s">String to parse an insertion rule from.</param>
    /// <returns>An insertion rule parsed from the given string.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="s"/> has an invalid format.
    /// </exception>
    private static (Pair Pair, char InsertionElement) ParseInsertionRule(ReadOnlySpan<char> s) {
        if (!InsertionRuleRegex().IsMatch(s)) {
            throw new ArgumentOutOfRangeException(
                nameof(s),
                $"The string \"{s}\" does not represent a valid insertion rule."
            );
        }
        Pair pair = new(s[0], s[1]);
        char insertionElement = s[s.LastIndexOf(' ') + 1];
        return (pair, insertionElement);
    }

    /// <summary>
    /// Applies the pair insertion algorithm on a given polymer for a specified number of steps
    /// using a given set of insertion rules.
    /// </summary>
    /// <param name="polymer">Polymer to apply the algorithm on.</param>
    /// <param name="insertionRules">Insertion rules for applying algorithm.</param>
    /// <param name="steps">Positive number of steps for which to apply the algorithm.</param>
    /// <returns>
    /// The difference between the most and least common element after applying the pair insertion
    /// algorithm to the given polymer.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="insertionRules"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="steps"/> is negative.
    /// </exception>
    private static long ApplyPairInsertion(
        ReadOnlySpan<char> polymer,
        IReadOnlyDictionary<Pair, char> insertionRules,
        int steps
    ) {
        Guard.IsNotNull(insertionRules);
        Guard.IsGreaterThanOrEqualTo(steps, 0);
        Dictionary<Pair, long> pairCounts = [];
        Dictionary<char, long> elementCounts = [];
        for (int i = 0; i < polymer.Length; i++) {
            char element = polymer[i];
            if (i < (polymer.Length - 1)) {
                Pair pair = new(element, polymer[i + 1]);
                pairCounts[pair] = pairCounts.GetValueOrDefault(pair) + 1L;
            }
            elementCounts[element] = elementCounts.GetValueOrDefault(element) + 1L;
        }
        Dictionary<Pair, long> updatedPairCounts = [];
        for (int i = 0; i < steps; i++) {
            foreach ((Pair pair, long count) in pairCounts) {
                char insertionElement = insertionRules[pair];
                // Surprisingly, non-destructive mutation using a with expression is much faster
                // than allocating a new struct with all but one field copied, despite both having
                // the same effect (at least semantically).
                Pair first = pair with { SecondElement = insertionElement };
                Pair second = pair with { FirstElement = insertionElement };
                updatedPairCounts[first] = updatedPairCounts.GetValueOrDefault(first) + count;
                updatedPairCounts[second] = updatedPairCounts.GetValueOrDefault(second) + count;
                elementCounts[insertionElement] = elementCounts.GetValueOrDefault(insertionElement)
                    + count;
            }
            // We reuse the dictionary of updated pair counts instead of allocating a new one each
            // step, which both reduces time spent for resizing and puts less pressure on the
            // garbage collector (resulting in an improved memory footprint).
            (pairCounts, updatedPairCounts) = (updatedPairCounts, pairCounts);
            updatedPairCounts.Clear();
        }
        return elementCounts.Values.Max() - elementCounts.Values.Min();
    }

    /// <summary>Solves the <see cref="ExtendedPolymerization"/> puzzle.</summary>
    /// <param name="textWriter"><see cref="TextWriter"/> to write the results to.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="textWriter"/> is <see langword="null"/>.
    /// </exception>
    internal static void Solve(TextWriter textWriter) {
        Guard.IsNotNull(textWriter);
        ImmutableArray<string> lines = [.. File.ReadLines(InputFile)];
        string polymer = lines[0];
        IReadOnlyDictionary<Pair, char> insertionRules = lines[2..]
            .Select(line => ParseInsertionRule(line))
            .ToFrozenDictionary(
                insertionRule => insertionRule.Pair,
                insertionRule => insertionRule.InsertionElement
            );
        long difference10 = ApplyPairInsertion(polymer, insertionRules, 10);
        long difference40 = ApplyPairInsertion(polymer, insertionRules, 40);
        textWriter.WriteLine($"After 10 steps, the difference is {difference10}.");
        textWriter.WriteLine($"After 40 steps, the difference is {difference40}.");
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