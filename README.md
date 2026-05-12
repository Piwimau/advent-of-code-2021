# 🎄 Advent of Code 2021 🎄

This repository contains my solutions for [Advent of Code
2021](https://adventofcode.com/2021), the first year I ever participated.

## What Is Advent of Code?

[Advent of Code](https://adventofcode.com/) is a series of small programming
puzzles created by [Eric Wastl](http://was.tl/). Every day from December 1st to
25th, a puzzle is released alongside an engaging fictional Christmas story. Each
puzzle consists of two parts, the second of which usually contains some
interesting twist or changing requirements and is only unlocked after completing
the first one. The objective is to solve all parts and collect fifty stars ⭐
until December 25th to save Christmas.

Many users compete on the [global
leaderboard](https://adventofcode.com/2021/leaderboard) by solving the puzzles
in an unbelievably fast way in order to get some extra points. Personally, I see
Advent of Code as a fun exercise to do during the Advent season while waiting
for Christmas. I often use it to learn a new programming language (like I did in
2021 with `C#`) or some advanced programming concepts. I can only encourage you
to participate as well – of course in a way that you find fun. Just get started
and learn more about Advent of Code [here](https://adventofcode.com/2021/about).

## About This Project

The solutions for Advent of Code 2021 were originally developed using `.NET 6`
and `C# 10` at the time. Since then I have taken some time to update them to
more recent versions (`.NET 9` and `C# 13`), which allowed me to take advantage
of new language features and modern data structures that either did not exist
yet or I did not know about (as I was just starting to learn the whole
ecosystem). These include expression bodies (`=>`), collection expressions
(`[...]`), target-typed `new()`, `Span<T>` and `ReadOnlySpan<T>`, types of the
`System.Collections.Immutable` or `System.Collections.Frozen` namespaces and
much more.

For this project and in general when developing software, I strive to produce
readable and well documented source code. However, I also enjoy benchmarking and
optimizing my code, which is why I sometimes implement a less idiomatic, yet
more efficient solution at the expense of readability. In those situations, I
try to document my design choices with analogies, possible alternative solutions
and sometimes little sketches to better illustrate the way a piece of code
works.

The general structure of this project is as follows:

```plaintext
src/
  day-01-sonar-sweep/
    resources/
      .gitkeep
    Benchmark.cs
    day-01-sonar-sweep.csproj
    SonarSweep.cs
  day-02-dive/
    resources/
      .gitkeep
    Benchmark.cs
    day-02-dive.csproj
    Dive.cs
  ...
  day-25-sea-cucumber/
    ...
.gitignore
advent-of-code-2021.slnx
LICENSE
README.md
```

The [solution file](advent-of-code-2021.slnx) contains 25 standalone projects
for the days of the Advent calendar, organized into separate directories. Each
one provides a corresponding `.csproj` file that can be opened in Visual Studio.
In addition, there is a `Resources` directory which contains the puzzle
description and my personal input for that day. However, [as
requested](https://adventofcode.com/2021/about) by the creator of Advent of
Code, these are only present in my own private copy of the repository and
therefore not publicly available.

> If you're posting a code repository somewhere, please don't include parts of
> Advent of Code like the puzzle text or your inputs.

As a consequence, you will have to provide your own inputs for the days, as
described in more detail in the following section.

## Dependencies and Usage

If you want to try out one of my solutions, simply follow these steps below:

1. Make sure you have `.NET 9` or a later version installed on your machine.

2. Clone the repository (or download the source code) to a directory of your
   choice.

   ```shell
   git clone https://github.com/Piwimau/advent-of-code-2021 ./advent-of-code-2021
   cd ./advent-of-code-2021
   ```

3. Put your input for the day in a file called `input.txt` and copy it to the
   appropriate resources directory. You can get all inputs from the [official
   website](https://adventofcode.com/2021) if you have not downloaded them
   already.

   ```shell
   cp input.txt ./src/day-01-sonar-sweep/resources
   ```

4. Nagivate into the appropriate day's directory.

   ```shell
   cd ./src/day-01-sonar-sweep
   ```

5. Finally, run the code in release mode to take advantage of all optimizations
   and achieve the best performance.

   ```shell
   dotnet run --configuration Release
   ```

   Optionally, specify an additional flag `--benchmark` to benchmark the
   relevant day on your machine. Note that in this mode no output for the
   results of the solved puzzle is produced.

   ```shell
   dotnet run --configuration Release --benchmark
   ```

If you have Visual Studio installed on your machine, you may also just open the
provided [solution file](advent-of-code-2021.slnx) and proceed from there.

## Benchmarks

Finally, here are some (non-scientific) benchmarks I created using the fantastic
[BenchmarkDotNet](https://github.com/dotnet/BenchmarkDotNet) package and my main
machine (Intel Core i9-13900HX, 32 GB DDR5-5600 RAM) running Windows 11 24H2.
All benchmarks include the time spent for reading the input from disk, as well
as printing the puzzle results (although the output is written to
`TextWriter.Null` when benchmarking, which is effectively a no-op and rather
fast).

| Day                              |        Min |        Max |       Mean |     Median | Standard Deviation |
|----------------------------------|-----------:|-----------:|-----------:|-----------:|-------------------:|
| Day 1 – Sonar Sweep              |   0.054 ms |   0.057 ms |   0.055 ms |   0.055 ms |           0.001 ms |
| Day 2 – Dive!                    |   0.101 ms |   0.105 ms |   0.103 ms |   0.102 ms |           0.001 ms |
| Day 3 – Binary Diagnostic        |   0.076 ms |   0.077 ms |   0.076 ms |   0.076 ms |           0.000 ms |
| Day 4 – Giant Squid              |   1.459 ms |   1.516 ms |   1.490 ms |   1.492 ms |           0.016 ms |
| Day 5 – Hydrothermal Venture     |  10.650 ms |  11.047 ms |  10.867 ms |  10.905 ms |           0.112 ms |
| Day 6 – Lanternfish              |   0.019 ms |   0.020 ms |   0.020 ms |   0.020 ms |           0.000 ms |
| Day 7 – The Treachery of Whales  |   3.567 ms |   3.673 ms |   3.624 ms |   3.639 ms |           0.037 ms |
| Day 8 – Seven Segment Search     |   0.175 ms |   0.182 ms |   0.177 ms |   0.177 ms |           0.002 ms |
| Day 9 – Smoke Basin              |   0.994 ms |   1.036 ms |   1.008 ms |   1.004 ms |           0.013 ms |
| Day 10 – Syntax Scoring          |   0.131 ms |   0.135 ms |   0.133 ms |   0.133 ms |           0.001 ms |
| Day 11 – Dumbo Octopus           |   1.027 ms |   1.059 ms |   1.039 ms |   1.039 ms |           0.009 ms |
| Day 12 – Passage Pathing         |  32.794 ms |  34.190 ms |  33.348 ms |  33.262 ms |           0.402 ms |
| Day 13 – Transparent Origami     |   0.132 ms |   0.137 ms |   0.135 ms |   0.135 ms |           0.002 ms |
| Day 14 – Extended Polymerization |   0.208 ms |   0.222 ms |   0.213 ms |   0.212 ms |           0.004 ms |
| Day 15 – Chiton                  |  31.869 ms |  32.248 ms |  32.028 ms |  32.014 ms |           0.108 ms |
| Day 16 – Packet Decoder          |   0.036 ms |   0.039 ms |   0.038 ms |   0.037 ms |           0.001 ms |
| Day 17 – Trick Shot              |   0.467 ms |   0.475 ms |   0.470 ms |   0.470 ms |           0.002 ms |
| Day 18 – Snailfish               |  15.981 ms |  16.463 ms |  16.186 ms |  16.187 ms |           0.137 ms |
| Day 19 – Beacon Scanner          | 234.062 ms | 241.932 ms | 237.632 ms | 237.508 ms |           2.211 ms |
| Day 20 – Trench Map              |  22.148 ms |  22.750 ms |  22.511 ms |  22.535 ms |           0.152 ms |
| Day 21 – Dirac Dice              |   2.759 ms |   2.919 ms |   2.836 ms |   2.840 ms |           0.050 ms |
| Day 22 – Reactor Reboot          |  34.651 ms |  36.186 ms |  35.877 ms |  36.039 ms |           0.401 ms |
| Day 23 – Amphipod                | 270.380 ms | 297.666 ms | 280.041 ms | 280.234 ms |           7.234 ms |
| Day 24 – Arithmetic Logic Unit   | 115.883 ms | 121.170 ms | 118.048 ms | 118.097 ms |           1.429 ms |
| Day 25 – Sea Cucumber            |  53.427 ms |  54.866 ms |  54.009 ms |  53.951 ms |           0.395 ms |
| Total                            | 833.050 ms | 880.170 ms | 851.964 ms | 852.163 ms |          12.720 ms |

## License

This project is licensed under the [MIT License](LICENSE). Feel free to
experiment with the code, adapt it to your own preferences, and share it with
others.