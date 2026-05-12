using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BenchmarkDotNet.Running;
using CommunityToolkit.Diagnostics;

namespace ArithmeticLogicUnit;

internal sealed partial class ArithmeticLogicUnit {

    /// <summary>
    /// Represents a <see cref="Block"/> of instructions used in the search for accepted model
    /// numbers.
    /// </summary>
    /// <remarks>
    /// There are 14 different instruction blocks in total, each used for one digit of the model
    /// number. They share an identical pattern, except for three varying immediate operands.<br/>
    /// For more information, see the <see cref="Parse(ReadOnlySpan{string})"/> method.
    /// </remarks>
    /// <param name="VaryingDivisor">
    /// Divisor for the single varying divide instruction of this <see cref="Block"/>.
    /// </param>
    /// <param name="FirstVaryingAddOperand">
    /// Immediate operand for the first varying add instruction of this <see cref="Block"/>.
    /// </param>
    /// <param name="SecondVaryingAddOperand">
    /// Immediate operand for the second varying add instruction of this <see cref="Block"/>.
    /// </param>
    private readonly partial record struct Block(
        int VaryingDivisor,
        int FirstVaryingAddOperand,
        int SecondVaryingAddOperand
    ) {

        /// <summary>Index of the single varying divide instruction.</summary>
        private const int VaryingDivideInstruction = 4;

        /// <summary>Index of the first varying add instruction.</summary>
        private const int FirstVaryingAddInstruction = 5;

        /// <summary>Index of the second varying add instruction.</summary>
        private const int SecondVaryingAddInstruction = 15;

        /// <summary>Start index of the immediate operand within any instruction.</summary>
        private const int ImmediateOperandStart = 6;

        /// <summary>
        /// Minimum allowed operand of the first varying add instruction if the <see cref="Block"/>
        /// does not divide by <see cref="Base"/>.
        /// </summary>
        private const int MinFirstVaryingAddOperandIfNotDividingByBase = 10;

        /// <summary>Number of instructions required per <see cref="Block"/>.</summary>
        public const int InstructionsPerBlock = 18;

        [GeneratedRegex(
            """
            ^inp w
            mul x 0
            add x z
            mod x 26
            div z (?:1|26)
            add x -?\d+
            eql x w
            eql x 0
            mul y 0
            add y 25
            mul y x
            add y 1
            mul z y
            mul y 0
            add y w
            add y -?\d+
            mul y x
            add z y$
            """
        )]
        private static partial Regex BlockRegex();

        /// <summary>Parses a <see cref="Block"/> from a given sequence of instructions.</summary>
        /// <remarks>
        /// The sequence must consist of exactly the following <see cref="InstructionsPerBlock"/>
        /// instructions, as described by <see cref="BlockRegex"/>:
        /// <example>
        /// <code>
        /// inp w
        /// mul x 0
        /// add x z
        /// mod x 26
        /// div z &lt;VD&gt;
        /// add x &lt;FVAO&gt;
        /// eql x w
        /// eql x 0
        /// mul y 0
        /// add y 25
        /// mul y x
        /// add y 1
        /// mul z y
        /// mul y 0
        /// add y w
        /// add y &lt;SVAO&gt;
        /// mul y x
        /// add z y
        /// </code>
        /// </example>
        /// <list type="bullet">
        ///     <item>
        ///     The placeholder &lt;VD&gt; marks a varying divisor that may either be 1 or
        ///     <see cref="Base"/>.
        ///     </item>
        ///     <item>
        ///     &lt;FVAO&gt; is the first varying add operand, which must be at least
        ///     <see cref="MinFirstVaryingAddOperandIfNotDividingByBase"/> in case &lt;VD&gt; is 1
        ///     and negative if &lt;VD&gt; is <see cref="Base"/>.
        ///     </item>
        ///     <item>
        ///     &lt;SVAO&gt; is the second varying add operand, for which no requirements are
        ///     imposed at the moment.
        ///     </item>
        /// </list>
        /// </remarks>
        /// <param name="instructions">
        /// Sequence of instructions to parse a <see cref="Block"/> from.
        /// </param>
        /// <returns>A <see cref="Block"/> parsed from the given sequence of instructions.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="instructions"/> violates any requirement stated above.
        /// </exception>
        public static Block Parse(ReadOnlySpan<string> instructions) {
            ReadOnlySpan<char> block = string.Join(Environment.NewLine, [.. instructions]);
            if (!BlockRegex().IsMatch(block)) {
                throw new ArgumentOutOfRangeException(
                    nameof(instructions),
                    "The following sequence of instructions has an invalid format:"
                        + $"{Environment.NewLine}{Environment.NewLine}{block}"
                        + $"{Environment.NewLine}{Environment.NewLine}"
                );
            }
            int varyingDivisor = int.Parse(
                instructions[VaryingDivideInstruction][ImmediateOperandStart..],
                CultureInfo.InvariantCulture
            );
            int firstVaryingAddOperand = int.Parse(
                instructions[FirstVaryingAddInstruction][ImmediateOperandStart..],
                CultureInfo.InvariantCulture
            );
            if ((varyingDivisor == 1)
                    && (firstVaryingAddOperand < MinFirstVaryingAddOperandIfNotDividingByBase)) {
                throw new ArgumentOutOfRangeException(
                    nameof(instructions),
                    "The following sequence of instructions has an invalid format:"
                        + $"{Environment.NewLine}{Environment.NewLine}{block}"
                        + $"{Environment.NewLine}{Environment.NewLine}"
                        + $"The first varying add operand ({firstVaryingAddOperand}) must be at "
                        + $"least {MinFirstVaryingAddOperandIfNotDividingByBase} if the varying "
                        + "divisor is 1."
                );
            }
            else if ((varyingDivisor == Base) && (firstVaryingAddOperand >= 0)) {
                throw new ArgumentOutOfRangeException(
                    nameof(instructions),
                    "The following sequence of instructions has an invalid format:"
                        + $"{Environment.NewLine}{Environment.NewLine}{block}"
                        + $"{Environment.NewLine}{Environment.NewLine}"
                        + $"The first varying add operand ({firstVaryingAddOperand}) must be "
                        + $"negative if the varying divisor is {Base}."
                );
            }
            int secondVaryingAddOperand = int.Parse(
                instructions[SecondVaryingAddInstruction][ImmediateOperandStart..],
                CultureInfo.InvariantCulture
            );
            return new Block(varyingDivisor, firstVaryingAddOperand, secondVaryingAddOperand);
        }

    }

    /// <summary>
    /// Represents an enumeration for controlling the search direction for accepted model numbers.
    /// </summary>
    private enum Search { Ascending, Descending }

    /// <summary>Base of the model numbers.</summary>
    private const int Base = 26;

    /// <summary>Number of digits in a model number.</summary>
    private const int NumberOfDigits = 14;

    private static readonly string InputFile = Path.Combine(
        AppContext.BaseDirectory,
        "resources",
        "input.txt"
    );

    /// <summary>
    /// Array of ascending digits for the search for model numbers, cached for efficiency.
    /// </summary>
    private static readonly ImmutableArray<int> AscendingDigits = [1, 2, 3, 4, 5, 6, 7, 8, 9];

    /// <summary>
    /// Array of descending digits for the search for model numbers, cached for efficiency.
    /// </summary>
    private static readonly ImmutableArray<int> DescendingDigits = [9, 8, 7, 6, 5, 4, 3, 2, 1];

    /// <summary>
    /// Searches for an accepted model number using a given sequence of instruction blocks and
    /// a <see cref="Search"/> direction.
    /// </summary>
    /// <param name="blocks">Sequence of instruction blocks for the search.</param>
    /// <param name="search">
    /// Direction for the search. If equal to <see cref="Search.Ascending"/>, the smallest accepted
    /// model number is returned, otherwise the largest accepted one.
    /// </param>
    /// <returns>
    /// An accepted model number for the given sequence of instructions, either the smallest or
    /// largest one depending on the given direction of <see cref="Search"/>.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no accepted model number could be found using the given sequence of instruction
    /// blocks.
    /// </exception>
    private static long SearchAcceptedModelNumber(ReadOnlySpan<Block> blocks, Search search) {
        HashSet<(int DigitIndex, long Z)> visited = [];
        // We use a queue to avoid having to implement this search method recursively. A stack would
        // also work fine, although all possible digits would have to be checked in reverse order.
        Queue<(int DigitIndex, long Z, long ModelNumber)> states = new([(0, 0, 0)]);
        while (states.Count > 0) {
            (int digitIndex, long z, long modelNumber) = states.Dequeue();
            // We reached the maximum search depth if all digits of the model number are filled in.
            // Either this model number is accepted (indicated by the z register holding a zero) or
            // we simply continue with the next candidate.
            if (digitIndex == NumberOfDigits) {
                if (z == 0L) {
                    return modelNumber;
                }
                continue;
            }
            // If the z register crosses a certain threshold, it cannot be reduced down to zero
            // by the remaining instruction blocks, which in turn cannot indicate an accepted model
            // number. In this case, we simply continue with the next candidate, just like if we
            // have already visited this state.
            if ((z > Math.Pow(Base, NumberOfDigits - digitIndex))
                    || !visited.Add((digitIndex, z))) {
                continue;
            }
            Block block = blocks[digitIndex];
            foreach (int w in (search == Search.Ascending) ? AscendingDigits : DescendingDigits) {
                // The following sequence of statements is a condensed version of the instructions
                // executed by each instruction block to process a single digit (w) of the model
                // number (found by reverse engineering). The x and y registers are only used as
                // temporaries and not carried over between individual blocks. Therefore only the
                // input digit in the w and the result in the z register really matter.
                int x = (int) ((z % Base) + block.FirstVaryingAddOperand);
                long newZ = (x == w)
                    ? z / block.VaryingDivisor
                    : (z / block.VaryingDivisor * Base) + w + block.SecondVaryingAddOperand;
                long newModelNumber = (modelNumber * 10L) + w;
                states.Enqueue((digitIndex + 1, newZ, newModelNumber));
            }
        }
        throw new InvalidOperationException("Failed to find a model number.");
    }

    /// <summary>Solves the <see cref="ArithmeticLogicUnit"/> puzzle.</summary>
    /// <param name="textWriter"><see cref="TextWriter"/> to write the results to.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="textWriter"/> is <see langword="null"/>.
    /// </exception>
    internal static void Solve(TextWriter textWriter) {
        Guard.IsNotNull(textWriter);
        ReadOnlySpan<Block> blocks = [.. File.ReadLines(InputFile)
            .Chunk(Block.InstructionsPerBlock)
            .Select(block => Block.Parse(block))
        ];
        long largestAcceptedModelNumber = SearchAcceptedModelNumber(blocks, Search.Descending);
        long smallestAcceptedModelNumber = SearchAcceptedModelNumber(blocks, Search.Ascending);
        textWriter.WriteLine(
            $"The largest accepted model number is {largestAcceptedModelNumber}."
        );
        textWriter.WriteLine(
            $"The smallest accepted model number is {smallestAcceptedModelNumber}."
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