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

namespace ReactorReboot;

internal sealed partial class ReactorReboot {

    /// <summary>Represents a <see cref="Cube"/>, which is part of a larger reactor.</summary>
    /// <param name="IsTurnedOn">Whether this <see cref="Cube"/> is turned on.</param>
    /// <param name="MinX">Minimum x-coordinate of the <see cref="Cube"/>.</param>
    /// <param name="MaxX">Maximum x-coordinate of the <see cref="Cube"/>.</param>
    /// <param name="MinY">Minimum y-coordinate of the <see cref="Cube"/>.</param>
    /// <param name="MaxY">Maximum y-coordinate of the <see cref="Cube"/>.</param>
    /// <param name="MinZ">Minimum z-coordinate of the <see cref="Cube"/>.</param>
    /// <param name="MaxZ">Maximum z-coordinate of the <see cref="Cube"/>.</param>
    private readonly partial record struct Cube(
        bool IsTurnedOn,
        long MinX,
        long MaxX,
        long MinY,
        long MaxY,
        long MinZ,
        long MaxZ
    ) {

        /// <summary>
        /// Determines whether this <see cref="Cube"/> is in the initialization region.
        /// </summary>
        public bool IsInInitializationRegion { get; } = (MinX >= -50L) && (MaxX <= 50L)
            && (MinY >= -50L) && (MaxY <= 50L) && (MinZ >= -50L) && (MaxZ <= 50L);

        /// <summary>Gets the volume of this <see cref="Cube"/>.</summary>
        public long Volume { get; } = (MaxX - MinX + 1) * (MaxY - MinY + 1) * (MaxZ - MinZ + 1);

        [GeneratedRegex(
            "^(?:on|off) x=-?\\d+\\.\\.-?\\d+,y=-?\\d+\\.\\.-?\\d+,z=-?\\d+\\.\\.-?\\d+$"
        )]
        private static partial Regex CubeRegex();

        [GeneratedRegex("-?\\d+")]
        private static partial Regex CoordinateRegex();

        /// <summary>Parses a <see cref="Cube"/> from a given string.</summary>
        /// <remarks>
        /// The string <paramref name="s"/> must have the format described by
        /// <see cref="CubeRegex"/>. In particular, it must start with either "on" or "off",
        /// followed by three ranges of coordinates for the x-, y- and z-axis. Within each range,
        /// the left (minimum) coordinate must be less than or equal to the right (maximum)
        /// coordinate.
        /// <para/>
        /// Examples for valid strings might be the following:
        /// <example>
        /// <code>
        /// "on x=-13..37,y=-36..14,z=-3..45"
        /// "off x=-1..43,y=-15..32,z=-35..15"
        /// "off x=-7..44,y=-6..48,z=-13..38"
        /// "on x=-42..3,y=-42..10,z=-3..44"
        /// </code>
        /// </example>
        /// </remarks>
        /// <param name="s">String to parse a <see cref="Cube"/> from.</param>
        /// <returns>A <see cref="Cube"/> parsed from the given string.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="s"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="s"/> has an invalid format.
        /// </exception>
        public static Cube Parse(string s) {
            Guard.IsNotNull(s);
            if (!CubeRegex().IsMatch(s)) {
                throw new ArgumentOutOfRangeException(
                    nameof(s),
                    $"The string \"{s}\" does not represent a valid cube."
                );
            }
            int spaceIndex = s.IndexOf(' ');
            bool isTurnedOn = s[..spaceIndex] == "on";
            ReadOnlySpan<long> coordinates = [
                .. CoordinateRegex().Matches(s[(spaceIndex + 1)..]).Select(
                    match => long.Parse(match.Value, CultureInfo.InvariantCulture)
                )
            ];
            long minX = coordinates[0];
            long maxX = coordinates[1];
            long minY = coordinates[2];
            long maxY = coordinates[3];
            long minZ = coordinates[4];
            long maxZ = coordinates[5];
            if ((minX > maxX) || (minY > maxY) || (minZ > maxZ)) {
                throw new ArgumentOutOfRangeException(
                    nameof(s),
                    $"The string \"{s}\" does not represent a valid cube. The left (minimum) "
                        + "coordinate must be less than or equal to the right (maximum) coordinate "
                        + "for each axis."
                );
            }
            return new Cube(isTurnedOn, minX, maxX, minY, maxY, minZ, maxZ);
        }

        /// <summary>Tries to intersect this <see cref="Cube"/> with a given one.</summary>
        /// <param name="other">Other <see cref="Cube"/> to intersect this one with.</param>
        /// <param name="intersection">
        /// Intersection between this <see cref="Cube"/> and the given one (indicated by a return
        /// value of <see langword="true"/>), otherwise the <see langword="default"/>.
        /// </param>
        /// <returns>
        /// <see langword="True"/> if an intersection exists, otherwise <see langword="false"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryIntersectWith(Cube other, out Cube intersection) {
            if ((MinX > other.MaxX) || (MaxX < other.MinX)
                    || (MinY > other.MaxY) || (MaxY < other.MinY)
                    || (MinZ > other.MaxZ) || (MaxZ < other.MinZ)) {
                intersection = default;
                return false;
            }
            intersection = new Cube(
                !other.IsTurnedOn,
                Math.Max(MinX, other.MinX),
                Math.Min(MaxX, other.MaxX),
                Math.Max(MinY, other.MinY),
                Math.Min(MaxY, other.MaxY),
                Math.Max(MinZ, other.MinZ),
                Math.Min(MaxZ, other.MaxZ)
            );
            return true;
        }

    }

    private static readonly string InputFile = Path.Combine(
        AppContext.BaseDirectory,
        "resources",
        "input.txt"
    );

    /// <summary>
    /// Counts the number of turned-on cubes based on a sequence of initial cubes.
    /// </summary>
    /// <param name="initialCubes">Sequence of initial cubes for the calculation.</param>
    /// <returns>The number of turned-on cubes.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="initialCubes"/> is <see langword="null"/>.
    /// </exception>
    private static long CountTurnedOnCubes(IEnumerable<Cube> initialCubes) {
        Guard.IsNotNull(initialCubes);
        List<Cube> cubes = [];
        // List of intersections is reused to reduce the number of heap allocations.
        List<Cube> intersections = [];
        foreach (Cube initialCube in initialCubes) {
            foreach (Cube cube in cubes) {
                if (initialCube.TryIntersectWith(cube, out Cube intersection)) {
                    intersections.Add(intersection);
                }
            }
            cubes.AddRange(intersections);
            intersections.Clear();
            if (initialCube.IsTurnedOn) {
                cubes.Add(initialCube);
            }
        }
        return cubes.Sum(cube => cube.Volume * (cube.IsTurnedOn ? 1 : -1));
    }

    /// <summary>Solves the <see cref="ReactorReboot"/> puzzle.</summary>
    /// <param name="textWriter"><see cref="TextWriter"/> to write the results to.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="textWriter"/> is <see langword="null"/>.
    /// </exception>
    internal static void Solve(TextWriter textWriter) {
        Guard.IsNotNull(textWriter);
        ImmutableArray<Cube> cubes = [.. File.ReadLines(InputFile).Select(Cube.Parse)];
        long countInitial = CountTurnedOnCubes(cubes.Where(cube => cube.IsInInitializationRegion));
        long countWhole = CountTurnedOnCubes(cubes);
        textWriter.WriteLine($"{countInitial} cubes are turned on in the initialization region.");
        textWriter.WriteLine($"{countWhole} cubes are turned on in the whole reactor.");
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