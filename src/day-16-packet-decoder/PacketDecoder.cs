using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using BenchmarkDotNet.Running;
using CommunityToolkit.Diagnostics;

namespace PacketDecoder;

internal sealed class PacketDecoder {

    /// <summary>Represents a <see cref="TypeId"/> of an <see cref="IPacket"/>.</summary>
    private enum TypeId { Sum, Product, Minimum, Maximum, LiteralValue, Greater, Less, Equal }

    /// <summary>Represents an <see cref="IPacket"/> that is part of a longer message.</summary>
    private interface IPacket {

        /// <summary>Length of the version inside the header.</summary>
        private const int VersionLength = 3;

        /// <summary>Offset of the <see cref="PacketDecoder.TypeId"/> inside the header.</summary>
        private protected const int TypeIdOffset = 3;

        /// <summary>Length of the <see cref="PacketDecoder.TypeId"/> inside the header.</summary>
        private protected const int TypeIdLength = 3;

        /// <summary>Gets the version of this <see cref="IPacket"/>.</summary>
        int Version { get; }

        /// <summary>
        /// Gets the <see cref="PacketDecoder.TypeId"/> of this <see cref="IPacket"/>.
        /// </summary>
        TypeId TypeId { get; }

        /// <summary>Parses a packet's version from a given message.</summary>
        /// <param name="message">Message to parse the packet's version from.</param>
        /// <param name="index">Current index inside the message.</param>
        /// <returns>The packet's version parsed from the given message.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="index"/> is out of range.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private protected static int ParseVersion(ReadOnlySpan<char> message, int index)
            => int.Parse(
                message.Slice(index, VersionLength),
                NumberStyles.BinaryNumber,
                CultureInfo.InvariantCulture
            );

        /// <summary>
        /// Parses a packet's <see cref="PacketDecoder.TypeId"/> from a given message.
        /// </summary>
        /// <param name="message">
        /// Message to parse the packet's <see cref="PacketDecoder.TypeId"/> from.
        /// </param>
        /// <param name="index">Current index inside the message.</param>
        /// <returns>
        /// The packet's <see cref="PacketDecoder.TypeId"/> parsed from the given message.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="index"/> is out of range.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private protected static TypeId ParseTypeId(ReadOnlySpan<char> message, int index)
            => (TypeId) int.Parse(
                message.Slice(index + TypeIdOffset, TypeIdLength),
                NumberStyles.BinaryNumber,
                CultureInfo.InvariantCulture
            );

        /// <summary>Parses the next <see cref="IPacket"/> from a given message.</summary>
        /// <param name="message">Message to parse the next <see cref="IPacket"/> from.</param>
        /// <param name="index">Current index inside the message.</param>
        /// <returns>The next <see cref="IPacket"/> parsed from the given message.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="index"/> is out of range.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private protected static IPacket ParseNext(ReadOnlySpan<char> message, ref int index)
            => (ParseTypeId(message, index) == TypeId.LiteralValue)
                ? LiteralValue.Parse(message, ref index)
                : Operator.Parse(message, ref index);

        /// <summary>Parses an <see cref="IPacket"/> from a given message.</summary>
        /// <param name="message">Message to parse an <see cref="IPacket"/> from.</param>
        /// <returns>
        /// An <see cref="IPacket"/> parsed from the given message.
        /// </returns>
        static IPacket Parse(ReadOnlySpan<char> message) {
            // Current index inside the message is only relevant while parsing the packet and can
            // immediately be discarded afterwards.
            int index = 0;
            return ParseNext(message, ref index);
        }

    }

    /// <summary>Represents a simple <see cref="LiteralValue"/>.</summary>
    private sealed record class LiteralValue : IPacket {

        /// <summary>Length of value groups for parsing the <see cref="LiteralValue"/>.</summary>
        private const int ValueGroupLength = 4;

        public int Version { get; init; }

        public TypeId TypeId => TypeId.LiteralValue;

        /// <summary>Gets the value of this <see cref="LiteralValue"/>.</summary>
        public long Value { get; init; }

        /// <summary>
        /// Initializes a new <see cref="LiteralValue"/> with a given version and value.
        /// </summary>
        /// <param name="version">Positive version of the <see cref="LiteralValue"/>.</param>
        /// <param name="value">Positive value of the <see cref="LiteralValue"/>.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="version"/> or <paramref name="value"/> is negative.
        /// </exception>
        private LiteralValue(int version, long value) {
            Guard.IsGreaterThanOrEqualTo(version, 0);
            Guard.IsGreaterThanOrEqualTo(value, 0);
            Version = version;
            Value = value;
        }

        /// <summary>Parses a <see cref="LiteralValue"/> from a given message.</summary>
        /// <param name="message">Message to parse a <see cref="LiteralValue"/> from.</param>
        /// <param name="index">Current index inside the message.</param>
        /// <returns>A <see cref="LiteralValue"/> parsed from the given message.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="index"/> is out of range.
        /// </exception>
        public static LiteralValue Parse(ReadOnlySpan<char> message, ref int index) {
            int version = IPacket.ParseVersion(message, index);
            StringBuilder builder = new();
            // The type id is always 4 for literal values, so we can just skip that.
            index += IPacket.TypeIdOffset + IPacket.TypeIdLength;
            // All value groups except the last one have a leading one bit.
            do {
                builder.Append(message.Slice(index + 1, ValueGroupLength));
                index += ValueGroupLength + 1;
            }
            while (message[index - ValueGroupLength - 1] != '0');
            long value = long.Parse(
                builder.ToString().AsSpan(),
                NumberStyles.BinaryNumber,
                CultureInfo.InvariantCulture
            );
            return new LiteralValue(version, value);
        }

    }

    /// <summary>Represents a more complex <see cref="Operator"/>.</summary>
    private sealed record class Operator : IPacket {

        /// <summary>Offset of the length type id in the header.</summary>
        private const int LengthTypeIdOffset = 6;

        /// <summary>A type id indicating the maximum length of all sub-packets.</summary>
        private const char LengthTypeId = '0';

        /// <summary>Length of the length type id in the header.</summary>
        private const int LengthTypeIdLength = 15;

        /// <summary>Length of the total sub-packet number in the header.</summary>
        private const int SubPacketsTypeIdLength = 11;

        public int Version { get; init; }

        public TypeId TypeId { get; init; }

        /// <summary>Array of sub-packets of this <see cref="Operator"/>.</summary>
        public ImmutableArray<IPacket> SubPackets { get; init; }

        /// <summary>
        /// Initializes a new <see cref="Operator"/> with a given version, 
        /// <see cref="PacketDecoder.TypeId"/> and sequence of sub-packets.
        /// </summary>
        /// <param name="version">Version of the <see cref="Operator"/>.</param>
        /// <param name="typeId">
        /// <see cref="PacketDecoder.TypeId"/> of the <see cref="Operator"/>.
        /// </param>
        /// <param name="subPackets">
        /// Sequence of sub-packets of the <see cref="Operator"/>, which must contain at least one
        /// packet.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="version"/> is negative, <paramref name="typeId"/> is
        /// <see cref="TypeId.LiteralValue"/> or <paramref name="subPackets"/> contains no
        /// sub-packets.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="subPackets"/> is <see langword="null"/>.
        /// </exception>
        private Operator(int version, TypeId typeId, IEnumerable<IPacket> subPackets) {
            Guard.IsGreaterThanOrEqualTo(version, 0);
            if (typeId == TypeId.LiteralValue) {
                throw new ArgumentOutOfRangeException(
                    nameof(typeId),
                    "An operator cannot be a literal value."
                );
            }
            Guard.IsNotNull(subPackets);
            Version = version;
            TypeId = typeId;
            SubPackets = [.. subPackets];
            if (SubPackets.Length == 0) {
                throw new ArgumentOutOfRangeException(
                    nameof(subPackets),
                    $"An operator must have at least one sub-packet."
                );
            }
        }

        /// <summary>Parses an <see cref="Operator"/> from a given message.</summary>
        /// <param name="message">Message to parse an <see cref="Operator"/> from.</param>
        /// <param name="index">Current index inside the message.</param>
        /// <returns>An <see cref="Operator"/> parsed from the given message.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="index"/> is out of range.
        /// </exception>
        public static Operator Parse(ReadOnlySpan<char> message, ref int index) {
            int version = IPacket.ParseVersion(message, index);
            TypeId typeId = IPacket.ParseTypeId(message, index);
            List<IPacket> subPackets = [];
            index += LengthTypeIdOffset;
            if (message[index] == LengthTypeId) {
                int maxLength = int.Parse(
                    message.Slice(index + 1, LengthTypeIdLength),
                    NumberStyles.BinaryNumber,
                    CultureInfo.InvariantCulture
                );
                index += 1 + LengthTypeIdLength;
                int length = 0;
                int oldIndex = index;
                while (length < maxLength) {
                    subPackets.Add(IPacket.ParseNext(message, ref index));
                    length += index - oldIndex;
                    oldIndex = index;
                }
            }
            else {
                int maxSubPackets = int.Parse(
                    message.Slice(index + 1, SubPacketsTypeIdLength),
                    NumberStyles.BinaryNumber,
                    CultureInfo.InvariantCulture
                );
                index += 1 + SubPacketsTypeIdLength;
                while (subPackets.Count < maxSubPackets) {
                    subPackets.Add(IPacket.ParseNext(message, ref index));
                }
            }
            return new Operator(version, typeId, subPackets);
        }

    }

    /// <summary>Number of bits necessary to translate a hexadecimal digit to binary.</summary>
    private const int BitsPerHexadecimalDigit = 4;

    private static readonly string InputFile = Path.Combine(
        AppContext.BaseDirectory,
        "resources",
        "input.txt"
    );

    /// <summary>Dictionary for efficient translation of hexadecimal digits to binary.</summary>
    private static readonly FrozenDictionary<char, string> HexadecimalDigitToBinary =
        FrozenDictionary.ToFrozenDictionary([
            KeyValuePair.Create('0', "0000"),
            KeyValuePair.Create('1', "0001"),
            KeyValuePair.Create('2', "0010"),
            KeyValuePair.Create('3', "0011"),
            KeyValuePair.Create('4', "0100"),
            KeyValuePair.Create('5', "0101"),
            KeyValuePair.Create('6', "0110"),
            KeyValuePair.Create('7', "0111"),
            KeyValuePair.Create('8', "1000"),
            KeyValuePair.Create('9', "1001"),
            KeyValuePair.Create('A', "1010"),
            KeyValuePair.Create('B', "1011"),
            KeyValuePair.Create('C', "1100"),
            KeyValuePair.Create('D', "1101"),
            KeyValuePair.Create('E', "1110"),
            KeyValuePair.Create('F', "1111")
        ]);

    /// <summary>Converts a string of hexadecimal digits ('0' through 'F') to binary.</summary>
    /// <remarks>
    /// Each hexadecimal digit in <paramref name="s"/> is replaced by exactly four bits,
    /// causing the resulting string to be four times as long.
    /// </remarks>
    /// <param name="s">String of hexadecimal digits to convert.</param>
    /// <returns>The converted string in binary.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="s"/> contains an invalid hexadecimal digit.
    /// </exception>
    private static string HexadecimalToBinary(ReadOnlySpan<char> s) {
        StringBuilder result = new(s.Length * BitsPerHexadecimalDigit);
        foreach (char c in s) {
            if (!HexadecimalDigitToBinary.TryGetValue(c, out string? binary)) {
                throw new ArgumentOutOfRangeException(
                    nameof(s),
                    $"The string \"{s}\" contains an invalid hexadecimal digit."
                );
            }
            result.Append(binary);
        }
        return result.ToString();
    }

    /// <summary>Returns the sum of all version ids for a given <see cref="IPacket"/>.</summary>
    /// <param name="packet"><see cref="IPacket"/> to sum all version ids of.</param>
    /// <returns>The sum of all version ids for the given <see cref="IPacket"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="packet"/> is <see langword="null"/>.
    /// </exception>
    private static int SumOfVersionIds(IPacket packet) {
        Guard.IsNotNull(packet);
        int sumOfVersionIds = 0;
        Stack<IPacket> stack = new([packet]);
        while (stack.Count > 0) {
            IPacket current = stack.Pop();
            sumOfVersionIds += current.Version;
            if (current is Operator o) {
                foreach (IPacket subPacket in o.SubPackets) {
                    stack.Push(subPacket);
                }
            }
        }
        return sumOfVersionIds;
    }

    /// <summary>Evaluates a given <see cref="IPacket"/>.</summary>
    /// <param name="packet"><see cref="IPacket"/> to evaluate.</param>
    /// <returns>The result of evaluating the given <see cref="IPacket"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="packet"/> is <see langword="null"/>.
    /// </exception>
    private static long Evaluate(IPacket packet) {
        Guard.IsNotNull(packet);
        return packet switch {
            LiteralValue v => v.Value,
            Operator o => o.TypeId switch {
                TypeId.Sum => o.SubPackets.Sum(Evaluate),
                TypeId.Product => o.SubPackets.Aggregate(
                    1L,
                    (product, packet) => product * Evaluate(packet)
                ),
                TypeId.Minimum => o.SubPackets.Min(Evaluate),
                TypeId.Maximum => o.SubPackets.Max(Evaluate),
                TypeId.Greater => Evaluate(o.SubPackets[0]) > Evaluate(o.SubPackets[1]) ? 1 : 0,
                TypeId.Less => Evaluate(o.SubPackets[0]) < Evaluate(o.SubPackets[1]) ? 1 : 0,
                TypeId.Equal => Evaluate(o.SubPackets[0]) == Evaluate(o.SubPackets[1]) ? 1 : 0,
                _ => throw new InvalidOperationException("Unreachable."),
            },
            _ => throw new InvalidOperationException("Unreachable.")
        };
    }

    /// <summary>Solves the <see cref="PacketDecoder"/> puzzle.</summary>
    /// <param name="textWriter"><see cref="TextWriter"/> to write the results to.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="textWriter"/> is <see langword="null"/>.
    /// </exception>
    internal static void Solve(TextWriter textWriter) {
        Guard.IsNotNull(textWriter);
        ReadOnlySpan<char> message = HexadecimalToBinary(File.ReadAllText(InputFile));
        IPacket packet = IPacket.Parse(message);
        textWriter.WriteLine($"The sum of the version ids is {SumOfVersionIds(packet)}.");
        textWriter.WriteLine($"The result of the evaluation is {Evaluate(packet)}.");
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