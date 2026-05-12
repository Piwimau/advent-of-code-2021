using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Running;
using CommunityToolkit.Diagnostics;

namespace SyntaxScoring;

internal sealed class SyntaxScoring {

    private static readonly string InputFile = Path.Combine(
        AppContext.BaseDirectory,
        "resources",
        "input.txt"
    );

    /// <summary>Dictionary of opening braces to closing braces for fast lookup.</summary>
    private static readonly FrozenDictionary<char, char> OpeningBraceToClosingBrace =
        FrozenDictionary.ToFrozenDictionary([
            KeyValuePair.Create('(', ')'),
            KeyValuePair.Create('[', ']'),
            KeyValuePair.Create('{', '}'),
            KeyValuePair.Create('<', '>')
        ]);

    /// <summary>Dictionary of closing braces to opening braces for fast lookup.</summary>
    private static readonly FrozenDictionary<char, char> ClosingBraceToOpeningBrace =
        FrozenDictionary.ToFrozenDictionary([
            KeyValuePair.Create(')', '('),
            KeyValuePair.Create(']', '['),
            KeyValuePair.Create('}', '{'),
            KeyValuePair.Create('>', '<')
        ]);

    /// <summary>Dictionary of closing braces to syntax error scores for fast lookup.</summary>
    private static readonly FrozenDictionary<char, int> SyntaxErrorScores =
        FrozenDictionary.ToFrozenDictionary([
            KeyValuePair.Create(')', 3),
            KeyValuePair.Create(']', 57),
            KeyValuePair.Create('}', 1197),
            KeyValuePair.Create('>', 25137)
        ]);

    /// <summary>Dictionary of closing braces to autocomplete scores for fast lookup.</summary>
    private static readonly FrozenDictionary<char, int> AutocompleteScores =
        FrozenDictionary.ToFrozenDictionary([
            KeyValuePair.Create(')', 1),
            KeyValuePair.Create(']', 2),
            KeyValuePair.Create('}', 3),
            KeyValuePair.Create('>', 4)
        ]);

    /// <summary>
    /// Determines if a given line is corrupted, meaning that an incorrect closing brace is found.
    /// </summary>
    /// <param name="line">Possibly corrupted line to check.</param>
    /// <param name="incorrectClosingBrace">
    /// First incorrect closing brace found if the line is corrupted, otherwise the
    /// <see langword="default"/>.
    /// </param>
    /// <returns>
    /// <see langword="True"/> if the given line is corrupted, otherwise <see langword="false"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsLineCorrupted(ReadOnlySpan<char> line, out char incorrectClosingBrace) {
        Stack<char> openingBraces = [];
        foreach (char brace in line) {
            if (OpeningBraceToClosingBrace.ContainsKey(brace)) {
                openingBraces.Push(brace);
            }
            else if (openingBraces.Pop() != ClosingBraceToOpeningBrace[brace]) {
                incorrectClosingBrace = brace;
                return true;
            }
        }
        incorrectClosingBrace = default;
        return false;
    }

    /// <summary>Returns all missing closing braces for an incomplete line.</summary>
    /// <param name="line">Incomplete line to find the missing closing braces of.</param>
    /// <returns>All missing closing braces for an incomplete line.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IEnumerable<char> MissingClosingBraces(ReadOnlySpan<char> line) {
        Stack<char> openingBraces = [];
        foreach (char brace in line) {
            if (OpeningBraceToClosingBrace.ContainsKey(brace)) {
                openingBraces.Push(brace);
            }
            else {
                openingBraces.Pop();
            }
        }
        return openingBraces.Select(openingBrace => OpeningBraceToClosingBrace[openingBrace]);
    }

    /// <summary>Performs syntax scoring on a given sequence of lines.</summary>
    /// <param name="lines">Sequence of lines for syntax scoring.</param>
    /// <returns>
    /// A tuple containing the total syntax error score, as well as the median autocomplete score.
    /// </returns>
    private static (long SyntaxErrorScore, long MedianAutocompleteScore) Check(
        ReadOnlySpan<string> lines
    ) {
        long syntaxErrorScore = 0;
        List<long> autocompleteScores = [];
        foreach (ReadOnlySpan<char> line in lines) {
            if (IsLineCorrupted(line, out char incorrectClosingBrace)) {
                syntaxErrorScore += SyntaxErrorScores[incorrectClosingBrace];
            }
            else {
                long autocompleteScore = 0;
                foreach (char missingClosingBrace in MissingClosingBraces(line)) {
                    autocompleteScore = (autocompleteScore * 5L)
                        + AutocompleteScores[missingClosingBrace];
                }
                autocompleteScores.Add(autocompleteScore);
            }
        }
        autocompleteScores.Sort();
        return (syntaxErrorScore, autocompleteScores[autocompleteScores.Count / 2]);
    }

    /// <summary>Solves the <see cref="SyntaxScoring"/> puzzle.</summary>
    /// <param name="textWriter"><see cref="TextWriter"/> to write the results to.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="textWriter"/> is <see langword="null"/>.
    /// </exception>
    internal static void Solve(TextWriter textWriter) {
        Guard.IsNotNull(textWriter);
        ReadOnlySpan<string> lines = [.. File.ReadLines(InputFile)];
        (long syntaxErrorScore, long medianAutocompleteScore) = Check(lines);
        textWriter.WriteLine($"The total syntax error score is {syntaxErrorScore}.");
        textWriter.WriteLine($"The median autocomplete score is {medianAutocompleteScore}.");
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