using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Running;
using CommunityToolkit.Diagnostics;

namespace TrenchMap;

internal sealed class TrenchMap {

    /// <summary>Represents an <see cref="Image"/> of pixels to enhance.</summary>
    private sealed class Image {

        /// <summary>Represents an enumeration of all possible <see cref="Pixel"/> states.</summary>
        private enum Pixel { Dark, Light }

        /// <summary>Represents an enumeration of all possible directions.</summary>
        private enum Direction {
            RightDown,
            Down,
            DownLeft,
            Right,
            Middle,
            Left,
            UpRight,
            Up,
            LeftUp
        }

        /// <summary>Represents a two-dimensional <see cref="Position"/>.</summary>
        /// <param name="X">X-coordinate of the <see cref="Position"/>.</param>
        /// <param name="Y">Y-coordinate of the <see cref="Position"/>.</param>
        private readonly record struct Position(int X, int Y);

        /// <summary>Exactly required length of the enhancement index.</summary>
        private const int RequiredEnhancementIndexLength = 512;

        /// <summary>Array of all directions, cached for efficiency.</summary>
        private static readonly ImmutableArray<Direction> Directions = [
            .. Enum.GetValues<Direction>()
        ];

        /// <summary>Enhancement index to be used for enhancing this <see cref="Image"/>.</summary>
        private readonly ImmutableArray<Pixel> enhancementIndex;

        /// <summary>Pixels of this <see cref="Image"/>.</summary>
        /// <remarks>
        /// Note that this is actually a two-dimensional array stored as a one-dimensional one (in
        /// row-major order) for reasons of improved performance and cache locality.
        /// </remarks>
        private Pixel[] pixels;

        /// <summary>Width of this <see cref="Image"/>.</summary>
        private int width;

        /// <summary>Height of this <see cref="Image"/>.</summary>
        private int height;

        /// <summary>
        /// Default <see cref="Pixel"/> chosen during the enhancement process for any pixels lying
        /// outside the region of the original <see cref="Image"/>.
        /// </summary>
        private Pixel defaultPixel;

        /// <summary>
        /// Initializes a new <see cref="Image"/> with a given enhancement index and an array of
        /// pixels.
        /// </summary>
        /// <param name="enhancementIndex">
        /// Enhancement index for enhancing the <see cref="Image"/>, which must consist of exactly
        /// <see cref="RequiredEnhancementIndexLength"/> pixels.
        /// </param>
        /// <param name="pixels">Pixels of the <see cref="Image"/>.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="enhancementIndex"/> has a length other than
        /// <see cref="RequiredEnhancementIndexLength"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="pixels"/> is <see langword="null"/>.
        /// </exception>
        private Image(ReadOnlySpan<Pixel> enhancementIndex, Pixel[][] pixels) {
            Guard.IsEqualTo(enhancementIndex.Length, RequiredEnhancementIndexLength);
            Guard.IsNotNull(pixels);
            this.enhancementIndex = [.. enhancementIndex];
            this.pixels = [.. pixels.SelectMany(row => row)];
            width = pixels[0].Length;
            height = pixels.Length;
            defaultPixel = Pixel.Dark;
        }

        /// <summary>Parses a <see cref="Pixel"/> from a given character.</summary>
        /// <remarks>
        /// Only '.' (<see cref="Pixel.Dark"/>) and '#' (<see cref="Pixel.Light"/>) count as valid
        /// characters.
        /// </remarks>
        /// <param name="c">Character to parse a <see cref="Pixel"/> from.</param>
        /// <returns>A <see cref="Pixel"/> parsed from the given character.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="c"/> is an invalid character.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Pixel ParsePixel(char c) => c switch {
            '.' => Pixel.Dark,
            '#' => Pixel.Light,
            _ => throw new ArgumentOutOfRangeException(
                nameof(c),
                $"'{c}' does not represent a valid pixel."
            )
        };

        /// <summary>Parses an <see cref="Image"/> from a given string.</summary>
        /// <remarks>
        /// The string <paramref name="s"/> must consist of the following parts:
        /// <list type="bullet">
        ///     <item>
        ///     An enhancement index line, consisting of exactly
        ///     <see cref="RequiredEnhancementIndexLength"/> pixels ('.' or '#').
        ///     </item>
        ///     <item>
        ///     An empty line, separating the enhancement index from the image's pixels.
        ///     </item>
        ///     <item>
        ///     Zero or more lines of pixels ('.' or '#') representing the rows of the
        ///     <see cref="Image"/>, which must all be of the same length.
        ///     </item>
        /// </list>
        /// An example for a valid string might be the following (actual newlines rendered,
        /// enhancement index not shown in full length):
        /// <example>
        /// <code>
        /// ..#.#..#####.#.#.#.###.##.....###.##.#..###.####..#####..#....#..#..##..##
        /// 
        /// #..#.
        /// #....
        /// ##..#
        /// ..#..
        /// ..###
        /// </code>
        /// </example>
        /// </remarks>
        /// <param name="s">String to parse an <see cref="Image"/> from.</param>
        /// <returns>An <see cref="Image"/> parsed from the given string.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="s"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="s"/> has an invalid format.
        /// </exception>
        public static Image Parse(string s) {
            Guard.IsNotNull(s);
            ReadOnlySpan<string> parts = s.Split($"{Environment.NewLine}{Environment.NewLine}");
            if (parts.Length != 2) {
                throw new ArgumentOutOfRangeException(
                    nameof(s),
                    $"Expected two parts (enhancement index + image), but got {parts.Length} "
                        + $"in the following string:{Environment.NewLine}{Environment.NewLine}{s}"
                );
            }
            ReadOnlySpan<Pixel> enhancementIndex = [.. parts[0].Select(ParsePixel)];
            Pixel[][] pixels = [
                .. parts[1].Split(Environment.NewLine).Select(
                    line => line.Select(ParsePixel).ToArray()
                )
            ];
            return new Image(enhancementIndex, pixels);
        }

        /// <summary>
        /// Returns the index of a <see cref="Pixel"/> with a given <see cref="Position"/> using a
        /// specified width.
        /// </summary>
        /// <param name="position">Position of the <see cref="Pixel"/> to get the index of.</param>
        /// <param name="width">Width of the <see cref="Image"/>.</param>
        /// <returns>
        /// The index of the <see cref="Pixel"/> with the given <see cref="Position"/> using the
        /// specified width.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Index(Position position, int width) => (position.Y * width) + position.X;

        /// <summary>Enhances this <see cref="Image"/> for a specified number of steps.</summary>
        /// <param name="steps">
        /// Positive number of steps to enhance this <see cref="Image"/> for.
        /// </param>
        /// <returns>
        /// The number of lit pixels after enhancing this <see cref="Image"/> for the specified
        /// number of steps.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="steps"/> is negative.
        /// </exception>
        public int Enhance(int steps) {
            Guard.IsGreaterThanOrEqualTo(steps, 0);
            for (int i = 0; i < steps; i++) {
                // Enhancing the image may cause it to grow by one additional pixel on each side.
                int newWidth = width + 2;
                int newHeight = height + 2;
                Pixel[] updatedPixels = new Pixel[newWidth * newHeight];
                // For the enhancement, we conceptually iterate over all positions of the updated
                // pixels array, only indexing into the old pixels array if we are guaranteed to be
                // within. Otherwise, we use a default pixel state for the edges of the new image,
                // which may change every other step.
                for (int y = -1; y < height + 1; y++) {
                    for (int x = -1; x < width + 1; x++) {
                        Position position = new(x, y);
                        int index = 0;
                        foreach (Direction direction in Directions) {
                            Position neighbor = direction switch {
                                Direction.RightDown => position with {
                                    X = position.X + 1,
                                    Y = position.Y + 1
                                },
                                Direction.Down => position with { Y = position.Y + 1 },
                                Direction.DownLeft => position with {
                                    X = position.X - 1,
                                    Y = position.Y + 1
                                },
                                Direction.Right => position with { X = position.X + 1 },
                                Direction.Middle => position,
                                Direction.Left => position with { X = position.X - 1 },
                                Direction.UpRight => position with {
                                    X = position.X + 1,
                                    Y = position.Y - 1
                                },
                                Direction.Up => position with { Y = position.Y - 1 },
                                Direction.LeftUp => position with {
                                    X = position.X - 1,
                                    Y = position.Y - 1
                                },
                                _ => throw new InvalidOperationException("Unreachable.")
                            };
                            bool isWithin = (neighbor.X >= 0) && (neighbor.X < width)
                                && (neighbor.Y >= 0) && (neighbor.Y < height);
                            Pixel neigborPixel = isWithin
                                ? pixels[Index(neighbor, width)]
                                : defaultPixel;
                            index |= (int) neigborPixel << (int) direction;
                        }
                        // To actually store the updated pixel, we need to fix up the (possibly
                        // negative) index by adding one to both the x- and y-coordinate.
                        position = position with { X = position.X + 1, Y = position.Y + 1 };
                        updatedPixels[Index(position, newWidth)] = enhancementIndex[index];
                    }
                }
                pixels = updatedPixels;
                width = newWidth;
                height = newHeight;
                defaultPixel = enhancementIndex[
                    (defaultPixel == Pixel.Dark) ? 0 : RequiredEnhancementIndexLength - 1
                ];
            }
            return pixels.Count(pixel => pixel == Pixel.Light);
        }

    }

    private static readonly string InputFile = Path.Combine(
        AppContext.BaseDirectory,
        "resources",
        "input.txt"
    );

    /// <summary>Solves the <see cref="TrenchMap"/> puzzle.</summary>
    /// <param name="textWriter"><see cref="TextWriter"/> to write the results to.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="textWriter"/> is <see langword="null"/>.
    /// </exception>
    internal static void Solve(TextWriter textWriter) {
        Guard.IsNotNull(textWriter);
        Image image = Image.Parse(File.ReadAllText(InputFile));
        int litPixelsAfterTwoSteps = image.Enhance(2);
        int litPixelsAfterFiftySteps = image.Enhance(48);
        textWriter.WriteLine($"After 2 steps, {litPixelsAfterTwoSteps} pixels are lit.");
        textWriter.WriteLine($"After 50 steps, {litPixelsAfterFiftySteps} pixels are lit.");
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