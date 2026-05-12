using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BenchmarkDotNet.Running;
using CommunityToolkit.Diagnostics;

namespace BeaconScanner;

internal sealed partial class BeaconScanner {

    /// <summary>Represents a three-dimensional <see cref="Vector"/>.</summary>
    /// <param name="X">X-coordinate of the <see cref="Vector"/>.</param>
    /// <param name="Y">Y-coordinate of the <see cref="Vector"/>.</param>
    /// <param name="Z">Z-coordinate of the <see cref="Vector"/>.</param>
    private readonly partial record struct Vector(int X, int Y, int Z) {

        /// <summary>
        /// Minimum allowed index for rotating a <see cref="Vector"/>. This value is the default and
        /// indicates no rotation at all.
        /// </summary>
        public const int MinRotationIndex = 0;

        /// <summary>Maximum allowed index for rotating a <see cref="Vector"/>.</summary>
        public const int MaxRotationIndex = 23;

        /// <summary>Adds a given vector to another one.</summary>
        /// <param name="first">First <see cref="Vector"/> for the addition.</param>
        /// <param name="second">Second <see cref="Vector"/> for the addition.</param>
        /// <returns>
        /// A new <see cref="Vector"/> representing the result of adding <paramref name="first"/>
        /// and <paramref name="second"/>.
        /// </returns>
        public static Vector operator +(Vector first, Vector second)
            => new(first.X + second.X, first.Y + second.Y, first.Z + second.Z);

        /// <summary>Subtracts a given vector from another one.</summary>
        /// <param name="first">First <see cref="Vector"/> for the subtraction.</param>
        /// <param name="second">Second <see cref="Vector"/> for the subtraction.</param>
        /// <returns>
        /// A new <see cref="Vector"/> representing the result of subtracting
        /// <paramref name="second"/> from <paramref name="first"/>.
        /// </returns>
        public static Vector operator -(Vector first, Vector second)
            => new(first.X - second.X, first.Y - second.Y, first.Z - second.Z);

        /// <summary>Rotates this <see cref="Vector"/> using a given rotation index.</summary>
        /// <param name="index">
        /// Index for rotating the <see cref="Vector"/> (in the range
        /// [<see cref="MinRotationIndex"/>; <see cref="MaxRotationIndex"/>]).
        /// </param>
        /// <returns>
        /// A rotated <see cref="Vector"/> corresponding to the given rotation index.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="index"/> is out of range.
        /// </exception>
        public Vector Rotate(int index) => index switch {
            0 => new Vector(X, Y, Z),
            1 => new Vector(X, Z, -Y),
            2 => new Vector(X, -Y, -Z),
            3 => new Vector(X, -Z, Y),
            4 => new Vector(Y, X, -Z),
            5 => new Vector(Y, Z, X),
            6 => new Vector(Y, -X, Z),
            7 => new Vector(Y, -Z, -X),
            8 => new Vector(Z, X, Y),
            9 => new Vector(Z, Y, -X),
            10 => new Vector(Z, -X, -Y),
            11 => new Vector(Z, -Y, X),
            12 => new Vector(-X, Y, -Z),
            13 => new Vector(-X, Z, Y),
            14 => new Vector(-X, -Y, Z),
            15 => new Vector(-X, -Z, -Y),
            16 => new Vector(-Y, X, Z),
            17 => new Vector(-Y, Z, -X),
            18 => new Vector(-Y, -X, -Z),
            19 => new Vector(-Y, -Z, X),
            20 => new Vector(-Z, X, -Y),
            21 => new Vector(-Z, Y, X),
            22 => new Vector(-Z, -X, Y),
            23 => new Vector(-Z, -Y, -X),
            _ => throw new ArgumentOutOfRangeException(
                nameof(index),
                $"Invalid rotation index ({index}). It must be in the range "
                    + $"[{MinRotationIndex}; {MaxRotationIndex}]."
            )
        };

        /// <summary>
        /// Returns the manhattan distance of this <see cref="Vector"/> to a given one.
        /// </summary>
        /// <param name="other">
        /// Other <see cref="Vector"/> to calculate the manhattan distance to.
        /// </param>
        /// <returns>The manhattan distance of this <see cref="Vector"/> to the given one.</returns>
        public int ManhattanDistanceTo(Vector other)
            => Math.Abs(X - other.X) + Math.Abs(Y - other.Y) + Math.Abs(Z - other.Z);

    }

    /// <summary>Minimum number of overlapping beacons necessary for an alignment.</summary>
    private const int MinOverlapsForAlignment = 12;

    private static readonly string InputFile = Path.Combine(
        AppContext.BaseDirectory,
        "resources",
        "input.txt"
    );

    [GeneratedRegex("^--- scanner \\d+ ---$")]
    private static partial Regex ScannerHeaderRegex();

    [GeneratedRegex("^-?\\d+,-?\\d+,-?\\d+$")]
    private static partial Regex BeaconRegex();

    /// <summary>Parses all scanners from a given sequence of lines.</summary>
    /// <remarks>
    /// Each line in <paramref name="lines"/> must be one of the following:
    /// <list type="bullet">
    ///     <item>An empty line used as a separator between scanners.</item>
    ///     <item>
    ///     A scanner header line with the format "--- scanner &lt;Id&gt; ---", where &lt;Id&gt;
    ///     is a positive integer.
    ///     </item>
    ///     <item>
    ///     A beacon line consisting of three comma-separated integers as in "404,-588,-901".
    ///     </item>
    /// </list>
    /// </remarks>
    /// <param name="lines">Sequence of lines to parse all scanners from.</param>
    /// <returns>All scanners parsed from the given sequence of lines.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="lines"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="lines"/> contains an invalid line.
    /// </exception>
    private static IEnumerable<ImmutableArray<Vector>> ParseScanners(IEnumerable<string> lines) {
        Guard.IsNotNull(lines);
        List<Vector> beacons = [];
        foreach (ReadOnlySpan<char> line in lines) {
            if (line.IsEmpty) {
                yield return beacons.ToImmutableArray();
                beacons.Clear();
            }
            else if (!ScannerHeaderRegex().IsMatch(line)) {
                if (!BeaconRegex().IsMatch(line)) {
                    throw new ArgumentOutOfRangeException(
                        nameof(lines),
                        $"The line \"{line}\" does not represent a valid beacon."
                    );
                }
                int firstCommaIndex = line.IndexOf(',');
                int secondCommaIndex = line.LastIndexOf(',');
                int x = int.Parse(line[..firstCommaIndex], CultureInfo.InvariantCulture);
                int y = int.Parse(
                    line[(firstCommaIndex + 1)..secondCommaIndex],
                    CultureInfo.InvariantCulture
                );
                int z = int.Parse(line[(secondCommaIndex + 1)..], CultureInfo.InvariantCulture);
                beacons.Add(new Vector(x, y, z));
            }
        }
        if (beacons.Count > 0) {
            yield return beacons.ToImmutableArray();
        }
    }

    /// <summary>Tries to align a single scanner with a given sequence of beacons.</summary>
    /// <remarks>
    /// Note that the set of <paramref name="knownBeacons"/> is extended and reassigned if an
    /// alignment is found.
    /// </remarks>
    /// <param name="beacons">Sequence of beacons relative to the scanner.</param>
    /// <param name="knownBeacons">Set of all known beacons.</param>
    /// <param name="displacement">
    /// A matching displacement vector used to align the scanner if an alignment was found,
    /// otherwise the <see langword="default"/> value.
    /// </param>
    /// <returns>
    /// <see langword="True"/> if an alignment was found, otherwise <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="knownBeacons"/> is <see langword="null"/>.
    /// </exception>
    private static bool TryAlignScanner(
        ImmutableArray<Vector> beacons,
        ref FrozenSet<Vector> knownBeacons,
        out Vector displacement
    ) {
        Guard.IsNotNull(knownBeacons);
        // These two arrays don't change in size and can be reused in order to avoid unnecessary
        // heap allocations. Stack-allocated spans would be an alternative, but they result in a
        // considerably worse performance in this specific situation (surprisingly).
        Vector[] rotatedBeacons = new Vector[beacons.Length];
        Vector[] translatedBeacons = new Vector[beacons.Length];
        foreach (Vector knownBeacon in knownBeacons) {
            for (int index = Vector.MinRotationIndex; index <= Vector.MaxRotationIndex; index++) {
                for (int i = 0; i < beacons.Length; i++) {
                    rotatedBeacons[i] = beacons[i].Rotate(index);
                }
                foreach (Vector rotatedBeacon in rotatedBeacons) {
                    Vector translation = knownBeacon - rotatedBeacon;
                    // Computing the translated beacons and counting the number of overlaps can be
                    // done in a single loop, which saves quite a bit of time in this hot path.
                    // Note that we count the number of overlaps manually within the loop.
                    // Alternatively, we could determine the size of the set intersection (e. g.
                    // translatedBeacons.Intersect(knownBeacons).Count()) or directly count (e. g.
                    // translatedBeacons.Count(knownBeacons.Contains)), but both of these options
                    // would result in a huge increase in runtime.
                    int overlaps = 0;
                    for (int i = 0; i < rotatedBeacons.Length; i++) {
                        translatedBeacons[i] = rotatedBeacons[i] + translation;
                        if (knownBeacons.Contains(translatedBeacons[i])) {
                            overlaps++;
                        }
                    }
                    if (overlaps >= MinOverlapsForAlignment) {
                        knownBeacons = knownBeacons.Concat(translatedBeacons).ToFrozenSet();
                        displacement = translation;
                        return true;
                    }
                }
            }
        }
        displacement = default;
        return false;
    }

    /// <summary>Aligns a given sequence of scanners.</summary>
    /// <remarks>
    /// All scanners are aligned relative to the first one, therefore the sequence must have at
    /// least one scanner.
    /// </remarks>
    /// <param name="scanners">
    /// Sequence of scanners to align, which must have at least one scanner.
    /// </param>
    /// <returns>
    /// A tuple containing the total number of known beacons and the maximum distance between any
    /// two scanners.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="scanners"/> is empty.
    /// </exception>
    private static (int KnownBeacons, int MaxDistance) AlignScanners(
        ReadOnlySpan<ImmutableArray<Vector>> scanners
    ) {
        if (scanners.IsEmpty) {
            throw new ArgumentOutOfRangeException(
                nameof(scanners),
                "Cannot align an empty sequence of scanners."
            );
        }
        FrozenSet<Vector> knownBeacons = scanners[0].ToFrozenSet();
        List<ImmutableArray<Vector>> scannersToAlign = [.. scanners[1..]];
        Span<Vector> displacements = stackalloc Vector[scannersToAlign.Count];
        while (scannersToAlign.Count > 0) {
            // Iterate in reverse order to allow successfully aligned scanners to be removed.
            for (int i = scannersToAlign.Count - 1; i >= 0; i--) {
                if (TryAlignScanner(
                        scannersToAlign[i],
                        ref knownBeacons,
                        out Vector displacement
                    )) {
                    displacements[^scannersToAlign.Count] = displacement;
                    scannersToAlign.RemoveAt(i);
                }
            }
        }
        int maxDistance = int.MinValue;
        for (int i = 0; i < displacements.Length; i++) {
            for (int j = i + 1; j < displacements.Length; j++) {
                int distance = displacements[i].ManhattanDistanceTo(displacements[j]);
                maxDistance = Math.Max(maxDistance, distance);
            }
        }
        return (knownBeacons.Count, maxDistance);
    }

    /// <summary>Solves the <see cref="BeaconScanner"/> puzzle.</summary>
    /// <param name="textWriter"><see cref="TextWriter"/> to write the results to.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="textWriter"/> is <see langword="null"/>.
    /// </exception>
    internal static void Solve(TextWriter textWriter) {
        Guard.IsNotNull(textWriter);
        ReadOnlySpan<ImmutableArray<Vector>> scanners = [
            .. ParseScanners(File.ReadLines(InputFile))
        ];
        (int knownBeacons, int maxDistance) = AlignScanners(scanners);
        textWriter.WriteLine($"In total, there are {knownBeacons} known beacons.");
        textWriter.WriteLine($"The largest distance between any two scanners is {maxDistance}.");
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